using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic
{
    /// <summary>
    ///     Handles all of the <see cref="Fire" />s in the game. Currently, the only known Fire spawning is in
    ///     <see cref="SubFire.CreateFire(SubFire.RoomFire)" />. The
    ///     fires in the Aurora come loaded with the map and do not grow in size. If we want to create a Fire spawning mechanic
    ///     outside of Cyclops fires, it should be
    ///     added to <see cref="Fires.Create(CyclopsFireData)" />. Fire dousing goes by Id and does not need to be
    ///     modified
    /// </summary>
    public class Fires
    {
        // Which fires we are currently broadcasting as dousing
        private readonly HashSet<NitroxId> currentlyDousing = [];

        // Whether Douse() was called on a fire this frame
        private readonly HashSet<NitroxId> dousedThisFrame = [];

        // The last douse rate per second of a fire
        private readonly Dictionary<NitroxId, float> localDouseRates = [];

        private readonly LocalPlayer localPlayer;

        private readonly IPacketSender packetSender;

        // How much other players are dousing fires
        private readonly Dictionary<NitroxId, Dictionary<SessionId, float>> remoteDouseRates = [];

        // Lowest health our estimation of the other players' dousing can bring a fire to
        private const float MIN_ESTIMATED_HEALTH = 1f;

        public Fires(IPacketSender packetSender, LocalPlayer localPlayer, PlayerManager playerManager)
        {
            this.packetSender = packetSender;
            this.localPlayer = localPlayer;

            // A player leaving while dousing won't send the packet telling that they stopped
            playerManager.OnRemove += (sessionId, _) => RemoveRemoteDouseRates(sessionId);
        }

        /// <summary>
        ///     Triggered when <see cref="SubFire.CreateFire(SubFire.RoomFire)" /> is executed. To create a new fire manually,
        ///     call <see cref="Create(CyclopsFireData)" />
        /// </summary>
        public void OnCreate(Fire fire, SubFire.RoomFire room, int nodeIndex)
        {
            if (!fire.fireSubRoot.TryGetIdOrWarn(out NitroxId subRootId))
            {
                return;
            }

            GameObject gameObject = fire.transform.parent.gameObject;

            NitroxId fireId = NitroxEntity.GenerateNewId(gameObject);
            CyclopsFireEntity entity = new(room.roomLinks.room, nodeIndex, fireId, subRootId);

            EntitySpawnedByClient packet = new(entity);
            packetSender.Send(packet);
        }

        public void OnDouse(Fire fire, float amount)
        {
            if (PacketSuppressor<FireDoused>.IsSuppressed)
            {
                return;
            }

            if (!fire.transform.parent.TryGetIdOrWarn(out NitroxId fireId))
            {
                return;
            }

            if (fire.livemixin.health <= 0f || fire.IsExtinguished())
            {
                FireDoused packet = new(fireId, fire.livemixin.health, null, 0f, true);
                packetSender.Send(packet);
                Unregister(fireId);
                return;
            }

            if (amount >= 20f)
            {
                // Large values are one-time and not done every frame
                FireDoused packet = new(fireId, fire.livemixin.health, null, 0f, true);
                packetSender.Send(packet);
                return;
            }

            localDouseRates[fireId] = amount / Time.deltaTime;
            dousedThisFrame.Add(fireId);
        }

        public void OnUpdate(Fire fire)
        {
            if (!fire.transform.parent.TryGetIdOrWarn(out NitroxId fireId))
            {
                return;
            }

            // Fires don't track whether are not they are currently being doused.
            // Instead, the FireExtinguisher just calls Douse() on the fire every
            // frame, or not at all if the extinguisher is not in use.
            // We detect this method call by continuously updating dousedThisFrame,
            // then convert that into a more stable flag in currentlyDoused.

            bool isDousing = dousedThisFrame.Contains(fireId);
            bool wasDousing = currentlyDousing.Contains(fireId);

            if (isDousing && !wasDousing)
            {
                currentlyDousing.Add(fireId);
                float douseRate = localDouseRates.GetOrDefault(fireId, 20f);
                FireDoused packet = new(fireId, fire.livemixin.health, localPlayer.SessionId, douseRate, false);
                packetSender.Send(packet);
            }
            else if (!isDousing && wasDousing)
            {
                currentlyDousing.Remove(fireId);
                FireDoused packet = new(fireId, fire.livemixin.health, localPlayer.SessionId, 0f, false);
                packetSender.Send(packet);
            }

            if (remoteDouseRates.TryGetValue(fireId, out Dictionary<SessionId, float> rates))
            {
                // Smoothly apply damage effect from other players. This estimation never puts the fire out by itself: only the dousing player knows
                // when it's extinguished, otherwise the fire could disappear here while it keeps burning for the others.
                float douseAmount = Mathf.Min(rates.Values.Sum() * Time.deltaTime, fire.livemixin.health - MIN_ESTIMATED_HEALTH);
                if (douseAmount > 0f)
                {
                    using (PacketSuppressor<FireDoused>.Suppress())
                    {
                        fire.Douse(douseAmount);
                    }
                }
            }

            dousedThisFrame.Remove(fireId);
        }

        public void Douse(NitroxId fireId, float health, SessionId? sessionId, float douseRate, bool oneShot)
        {
            Optional<GameObject> fireGameObject = NitroxEntity.GetObjectFrom(fireId);
            if (!fireGameObject.HasValue)
            {
                Log.Warn($"Can't find fire entity with id {fireId}");
                return;
            }

            Fire fire = fireGameObject.Value.GetComponentInChildren<Fire>();
            if (!fire)
            {
                Log.Error($"Fire object with id {fireId} missing component");
                return;
            }

            // Prevents a desync where the fire could extinguish for one player but not another
            float douseAmount = fire.livemixin.health - health;

            if (douseAmount > 0f)
            {
                using (PacketSuppressor<FireDoused>.Suppress())
                {
                    fire.Douse(douseAmount);
                }
            }
            else if (douseAmount < 0f)
            {
                // Fire health went up (it regrows, and our estimation of the other players' dousing can go too far)
                fire.livemixin.health = health;
                using (PacketSuppressor<FireDoused>.Suppress())
                {
                    // Refreshes the fire's size and effects
                    fire.Douse(0f);
                }
            }

            if (health <= 0f)
            {
                // Fire.Douse already calls it when the fire's health reaches 0
                if (!fire.IsExtinguished())
                {
                    fire.Extinguished();
                }
                Unregister(fireId);
                return;
            }

            if (!oneShot && sessionId.HasValue)
            {
                remoteDouseRates.TryAdd(fireId, []);
                remoteDouseRates[fireId][sessionId.Value] = douseRate;
            }
        }

        private void Unregister(NitroxId id)
        {
            remoteDouseRates.Remove(id);
            localDouseRates.Remove(id);
            currentlyDousing.Remove(id);
        }

        private void RemoveRemoteDouseRates(SessionId sessionId)
        {
            foreach (Dictionary<SessionId, float> rates in remoteDouseRates.Values)
            {
                rates.Remove(sessionId);
            }
        }
    }
}
