using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     When we stop simulating an attached lava larva, its new simulating player decides when it lets go (see <see cref="LavaLarvas" />).
///     Without this, the larva would let go for us only.
/// </summary>
public sealed partial class AttachToVehicle_StopPerform_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachToVehicle t) => t.StopPerform(default, default));

    // Typed as the base class in case the game doesn't override this method anymore (it would then be CreatureAction's)
    public static bool Prefix(CreatureAction __instance)
    {
        if (__instance is AttachToVehicle attachToVehicle && attachToVehicle.IsAttached())
        {
            return Resolve<LavaLarvas>().IsSimulatedLocally(attachToVehicle);
        }
        return true;
    }
}
