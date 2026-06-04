using Behaviour.Crew;
using Behaviour.UI;
using Behaviour.UI.Tooltip;
using HarmonyLib;
using Source.Personnel;
using Source.Util;

namespace VGTractorAuto.Patches;

[HarmonyPatch(typeof(MasteryBadge))]
internal static class MasteryTooltipPatches
{
    private static bool Enabled => Plugin.Instance != null && Plugin.Instance.CfgEnabled.Value;

    // Append a live conversion-% line to the Engineering ("Autopilot") mastery tooltip,
    // below vanilla's own bonus lines. Shown whenever enabled (including 0%) so the
    // mechanic is discoverable. Colors are applied via HighlightWithColor rich-text tags
    // (body bonus-green, (VGTractorAuto) marker muted) rather than setting .Text.color —
    // that property is typed TMP_Text and would force a TextMeshPro assembly reference we
    // don't otherwise need.
    [HarmonyPostfix]
    [HarmonyPatch(nameof(MasteryBadge.AddTooltipCustomContent))]
    private static void AddTooltipCustomContent_Postfix(MasteryBadge __instance, UITooltip tooltip)
    {
        if (!Enabled)
            return;

        Skilltree engineering = Skilltree.Get(
            SkillTreeData.GetSpecializationTreeName(CommanderSpecialization.Engineering));
        if (__instance.skillTree != engineering)
            return;

        string body = $"Manual Tractor Beams act as automatic: {AutopilotMastery.Percent()}%"
            .HighlightWithColor(ColorHelper.greenish);
        string marker = "(VGTractorAuto)".HighlightWithColor(ColorHelper.detailsColor);
        tooltip.AddTextLine($"{body} {marker}");
    }
}
