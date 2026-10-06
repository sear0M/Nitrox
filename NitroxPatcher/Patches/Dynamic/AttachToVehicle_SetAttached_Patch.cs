using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Tells the other players where a lava larva we simulate attached (see <see cref="LavaLarvas" />).
/// </summary>
public sealed partial class AttachToVehicle_SetAttached_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachToVehicle t) => t.SetAttached());

    public static void Postfix(AttachToVehicle __instance)
    {
        Resolve<LavaLarvas>().BroadcastAttachment(__instance);
    }
}
