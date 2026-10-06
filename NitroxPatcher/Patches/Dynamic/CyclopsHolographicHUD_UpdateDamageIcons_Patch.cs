using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The Cyclops hologram only removes a lava larva's icon when the larva lets go. This periodic update also removes the icons of larvae
///     which aren't attached anymore, whatever made them stay (see <see cref="LavaLarvas" />).
/// </summary>
public sealed partial class CyclopsHolographicHUD_UpdateDamageIcons_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsHolographicHUD t) => t.UpdateDamageIcons());

    public static void Postfix(CyclopsHolographicHUD __instance)
    {
        Resolve<LavaLarvas>().RemoveStaleHologramIcons(__instance);
    }
}
