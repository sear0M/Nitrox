using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Tells the other players that a lava larva we simulate let go (see <see cref="LavaLarvas" />).
/// </summary>
public sealed partial class AttachToVehicle_SetDetached_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachToVehicle t) => t.SetDetached());

    public static void Prefix(AttachToVehicle __instance, out bool __state)
    {
        // SetDetached is also called on larvae which aren't attached
        __state = __instance.IsAttached();
    }

    public static void Postfix(AttachToVehicle __instance, bool __state)
    {
        if (__state)
        {
            Resolve<LavaLarvas>().BroadcastAttachment(__instance);
        }
    }
}
