using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Syncs lava larvae attaching to a Cyclops or a vehicle (<see cref="AttachToVehicle" />).
/// </summary>
/// <remarks>
///     Only the simulating player runs a larva's AI: it decides when the larva attaches and lets go, and it's the only one draining the target's energy.
///     The other players reproduce the attachment from <see cref="LavaLarvaMetadata" /> (larva on the hull, icon on the Cyclops hologram, slower vehicle)
///     and keep it when the simulation moves to another player, who carries on with it.
/// </remarks>
public class LavaLarvas
{
    private readonly Entities entities;
    private readonly SimulationOwnership simulationOwnership;

    private readonly HashSet<GameObject> hologramLarvae = [];
    private readonly List<GameObject> staleHologramLarvae = [];

    public LavaLarvas(Entities entities, SimulationOwnership simulationOwnership)
    {
        this.entities = entities;
        this.simulationOwnership = simulationOwnership;
    }

    public static bool IsAttached(GameObject gameObject)
    {
        return gameObject.TryGetComponent(out AttachToVehicle attachToVehicle) && attachToVehicle.IsAttached();
    }

    /// <returns>True if the local player decides when the larva attaches and lets go</returns>
    public bool IsSimulatedLocally(AttachToVehicle attachToVehicle)
    {
        // Larvae unknown to the server keep the game's behaviour
        return !attachToVehicle.TryGetNitroxId(out NitroxId larvaId) || simulationOwnership.HasAnyLockType(larvaId);
    }

    public static LavaLarvaMetadata CreateMetadata(AttachToVehicle attachToVehicle)
    {
        if (TryGetAttachment(attachToVehicle, out NitroxId targetId, out int attachPointIndex))
        {
            return new LavaLarvaMetadata(targetId, attachPointIndex);
        }
        return new LavaLarvaMetadata(null, -1);
    }

    /// <summary>
    ///     Tells the other players that the larva attached or let go, if the local player simulates it.
    /// </summary>
    public void BroadcastAttachment(AttachToVehicle attachToVehicle)
    {
        if (PacketSuppressor<EntityMetadataUpdate>.IsSuppressed)
        {
            return;
        }
        if (!attachToVehicle.TryGetNitroxId(out NitroxId larvaId))
        {
            Log.WarnOnce($"[{nameof(LavaLarvas)}] A lava larva unknown to the server attached or let go, the other players don't see it");
            return;
        }
        if (!simulationOwnership.HasAnyLockType(larvaId))
        {
            return;
        }

        LavaLarvaMetadata metadata = CreateMetadata(attachToVehicle);
        Log.Info($"[{nameof(LavaLarvas)}] Sending the attachment of larva {larvaId}: {metadata}");
        entities.BroadcastMetadataUpdate(larvaId, metadata);
    }

    /// <summary>
    ///     Reproduces the attachment decided by the larva's simulating player.
    /// </summary>
    public void ApplyAttachment(GameObject larva, LavaLarvaMetadata metadata)
    {
        if (!larva.TryGetComponent(out AttachToVehicle attachToVehicle))
        {
            Log.Error($"[{nameof(LavaLarvas)}] Can't apply {metadata} to {larva.name} which has no {nameof(AttachToVehicle)}");
            return;
        }

        // The simulating player's AI decides, the metadata is the other players' copy (a larva which just spawned gets its simulation afterwards)
        if (IsSimulatedLocally(attachToVehicle))
        {
            return;
        }

        // Nothing to send back to the other players
        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        {
            if (!TryFindAttachPoint(metadata, out LavaLarvaTarget target, out LavaLarvaAttachPoint attachPoint))
            {
                if (attachToVehicle.IsAttached())
                {
                    Log.Info($"[{nameof(LavaLarvas)}] Larva {GetLogName(larva)} lets go: {metadata}");
                    attachToVehicle.SetDetached();
                }
                return;
            }

            if (attachToVehicle.IsAttached())
            {
                if (attachToVehicle.currentAttachPoint == attachPoint)
                {
                    return;
                }
                attachToVehicle.SetDetached();
            }

            if (!attachToVehicle.creature)
            {
                Log.Warn($"[{nameof(LavaLarvas)}] Can't attach larva {GetLogName(larva)} before its creature is set up: {metadata}");
                return;
            }

            Log.Info($"[{nameof(LavaLarvas)}] Larva {GetLogName(larva)} attaches to {target.name}: {metadata}");
            Attach(attachToVehicle, target, attachPoint);
        }
    }

    /// <summary>
    ///     Called when the larva gets disabled or destroyed. The game then forgets the larva's target without calling SetDetached, which is the only
    ///     thing removing the larva's icon from the Cyclops hologram.
    /// </summary>
    public static void RemoveHologramIcon(AttachToVehicle attachToVehicle)
    {
        LavaLarvaTarget target = attachToVehicle.currTarget;
        if (attachToVehicle.IsAttached() && target && target.IsCyclops())
        {
            // Like AttachToVehicle.SetDetached
            target.subControl.BroadcastMessage(nameof(CyclopsHolographicHUD.DetachedLavaLarva), attachToVehicle.gameObject, SendMessageOptions.DontRequireReceiver);
        }
    }

    /// <summary>
    ///     Removes from the Cyclops hologram the icons of larvae which aren't attached to this Cyclops (anymore), whatever made them stay.
    /// </summary>
    public void RemoveStaleHologramIcons(CyclopsHolographicHUD hud)
    {
        SubRoot subRoot = hud.GetComponentInParent<SubRoot>();
        foreach (CyclopsHolographicHUD.LavaLarvaIcon icon in hud.lavaLarvaIcons)
        {
            // A larva can't have two icons
            if (!hologramLarvae.Add(icon.refGo) || !IsAttachedTo(icon.refGo, subRoot))
            {
                staleHologramLarvae.Add(icon.refGo);
            }
        }

        foreach (GameObject larva in staleHologramLarvae)
        {
            hud.DetachedLavaLarva(larva);
        }

        hologramLarvae.Clear();
        staleHologramLarvae.Clear();
    }

    private static string GetLogName(GameObject gameObject)
    {
        return gameObject.TryGetNitroxId(out NitroxId id) ? id.ToString() : gameObject.name;
    }

    private static bool IsAttachedTo(GameObject larva, SubRoot subRoot)
    {
        if (!larva || !subRoot || !larva.TryGetComponent(out AttachToVehicle attachToVehicle) || !attachToVehicle.IsAttached())
        {
            return false;
        }

        LavaLarvaTarget target = attachToVehicle.currTarget;
        return target && target.subControl && target.subControl.gameObject == subRoot.gameObject;
    }

    private static bool TryGetAttachment(AttachToVehicle attachToVehicle, [NotNullWhen(true)] out NitroxId? targetId, out int attachPointIndex)
    {
        targetId = null;
        attachPointIndex = -1;

        LavaLarvaTarget target = attachToVehicle.currTarget;
        LavaLarvaAttachPoint attachPoint = attachToVehicle.currentAttachPoint;
        if (!attachToVehicle.IsAttached() || !target || !attachPoint)
        {
            return false;
        }

        int index = Array.IndexOf(target.attachPoints, attachPoint);
        if (!TryFindEntity(target, out NitroxEntity targetEntity) || index < 0)
        {
            return false;
        }

        targetId = targetEntity.Id;
        attachPointIndex = index;
        return true;
    }

    private static bool TryFindAttachPoint(LavaLarvaMetadata metadata, [NotNullWhen(true)] out LavaLarvaTarget? target, [NotNullWhen(true)] out LavaLarvaAttachPoint? attachPoint)
    {
        target = null;
        attachPoint = null;
        if (metadata.TargetId == null)
        {
            return false;
        }
        if (!NitroxEntity.TryGetObjectFrom(metadata.TargetId, out GameObject targetObject))
        {
            Log.Info($"[{nameof(LavaLarvas)}] Can't attach a larva to {metadata.TargetId} which isn't loaded");
            return false;
        }

        foreach (LavaLarvaTarget candidate in targetObject.GetComponentsInChildren<LavaLarvaTarget>(true))
        {
            // A vehicle docked in a Cyclops has its own target, which belongs to the vehicle's entity
            if (!TryFindEntity(candidate, out NitroxEntity candidateEntity) || candidateEntity.gameObject != targetObject)
            {
                continue;
            }

            if (metadata.AttachPointIndex < 0 || metadata.AttachPointIndex >= candidate.attachPoints.Length || !candidate.attachPoints[metadata.AttachPointIndex])
            {
                Log.Warn($"[{nameof(LavaLarvas)}] {targetObject.name} has no larva attach point at index {metadata.AttachPointIndex}");
                return false;
            }

            target = candidate;
            attachPoint = candidate.attachPoints[metadata.AttachPointIndex];
            return true;
        }

        Log.Warn($"[{nameof(LavaLarvas)}] {targetObject.name} has no larva target for {metadata}");
        return false;
    }

    /// <summary>
    ///     Finds the entity of a larva target, which can be on a child of the Cyclops or vehicle.
    /// </summary>
    private static bool TryFindEntity(Component component, [NotNullWhen(true)] out NitroxEntity? entity)
    {
        for (Transform current = component.transform; current; current = current.parent)
        {
            if (current.TryGetComponent(out entity))
            {
                return true;
            }
        }

        entity = null;
        return false;
    }

    /// <summary>
    ///     What AttachToVehicle.StartPerform and AttachToVehicle.Perform do until the larva reaches its attach point.
    /// </summary>
    private static void Attach(AttachToVehicle attachToVehicle, LavaLarvaTarget target, LavaLarvaAttachPoint attachPoint)
    {
        // Point reserved by the larva's AI on its way to a target, if we were simulating it
        LavaLarvaAttachPoint previousAttachPoint = attachToVehicle.currentAttachPoint;
        if (previousAttachPoint && previousAttachPoint != attachPoint)
        {
            previousAttachPoint.Clear();
        }

        attachToVehicle.currTarget = target;
        attachToVehicle.currentAttachPoint = attachPoint;
        attachPoint.occupied = true;
        attachPoint.lavaLarva = attachToVehicle.gameObject;

        Transform larvaTransform = attachToVehicle.transform;
        attachToVehicle.state = AttachToVehicle.State.Transition;
        attachToVehicle.timeTargetSet = Time.time;
        attachToVehicle.startPos = larvaTransform.position;
        attachToVehicle.startRot = larvaTransform.rotation;
        attachPoint.attached = true;
        attachToVehicle.SetAttached();
    }
}
