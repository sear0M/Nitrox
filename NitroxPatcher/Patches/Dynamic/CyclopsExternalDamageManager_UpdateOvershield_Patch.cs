using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxPatcher.Patches.Dynamic;

// Fixes a very annoying vanilla game bug where the Cyclops will appear
// to be leaking from the inside even though there is no actual damage.
public sealed partial class CyclopsExternalDamageManager_UpdateOvershield_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsExternalDamageManager t) => t.UpdateOvershield());

    public static void Postfix(CyclopsExternalDamageManager __instance)
    {
        RestoreMissingDamagePoint(__instance);
        __instance.ToggleLeakPointsBasedOnDamage();
    }

    /// <summary>
    ///     Vanilla rebuilds the damage points from the hull health in <see cref="CyclopsExternalDamageManager.Start" />, which runs before the synced
    ///     health is applied. A damaged hull without any damage point (e.g. from a save made before damage points were entities) would otherwise
    ///     leak from the inside with nothing to repair. One point is enough because repairing the last point restores the hull to full health.
    /// </summary>
    private static void RestoreMissingDamagePoint(CyclopsExternalDamageManager damageManager)
    {
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted || Resolve<Entities>().SpawningEntities)
        {
            return;
        }

        if (damageManager.unusedDamagePoints == null || damageManager.unusedDamagePoints.Count != damageManager.damagePoints.Length)
        {
            return;
        }

        if (damageManager.subRoot.subDestroyed || !damageManager.subLiveMixin.IsAlive())
        {
            return;
        }

        // CreatePoint is blocked for the other players and broadcast by CyclopsExternalDamageManager_CreatePoint_Patch for the simulating one
        if (!damageManager.subRoot.TryGetNitroxId(out NitroxId id) || !Resolve<SimulationOwnership>().HasAnyLockType(id))
        {
            return;
        }

        if (damageManager.EvaluatePlaceNewPoint())
        {
            damageManager.CreatePoint();
        }
    }
}
