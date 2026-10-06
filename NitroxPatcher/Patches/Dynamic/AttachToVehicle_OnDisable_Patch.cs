using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An attached lava larva which gets destroyed (e.g. killed or removed by another player) would leave its icon on the Cyclops hologram forever,
///     because OnDisable forgets the larva's target without removing the icon.
/// </summary>
public sealed partial class AttachToVehicle_OnDisable_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachToVehicle t) => t.OnDisable());

    // Typed as the base class in case the game doesn't declare this method here anymore
    public static void Prefix(CreatureAction __instance)
    {
        if (__instance is AttachToVehicle attachToVehicle)
        {
            LavaLarvas.RemoveHologramIcon(attachToVehicle);
        }
    }
}
