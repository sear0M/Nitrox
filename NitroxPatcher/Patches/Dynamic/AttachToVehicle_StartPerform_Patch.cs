using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A lava larva which is already attached when we start simulating it (see <see cref="LavaLarvas" />) carries on with its attachment
///     instead of looking for another attach point.
/// </summary>
public sealed partial class AttachToVehicle_StartPerform_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachToVehicle t) => t.StartPerform(default, default));

    // Typed as the base class in case the game doesn't override this method anymore (it would then be CreatureAction's)
    public static bool Prefix(CreatureAction __instance)
    {
        if (__instance is AttachToVehicle attachToVehicle && attachToVehicle.IsAttached())
        {
            return false;
        }
        return true;
    }
}
