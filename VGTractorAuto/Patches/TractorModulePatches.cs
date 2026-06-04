using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Behaviour.Equipment.Module;
using Behaviour.Tractoring;
using Behaviour.Weapons;
using HarmonyLib;

namespace VGTractorAuto.Patches;

[HarmonyPatch(typeof(TractorModule))]
internal static class TractorModulePatches
{
    // Vanilla's private crew-pod target filter (blocks hostile/non-roll crew pods when
    // crew/brig space is full). Not exposed by the publicized stub we compile against,
    // so we bind it reflectively; null on older builds that lack it → treated as not
    // blocked, which matches those builds' vanilla behavior.
    private static readonly MethodInfo? _isCrewPodTargetingBlocked = AccessTools.Method(
        typeof(TractorModule), "IsCrewPodTargetingBlocked", new[] { typeof(TractorableItem) });

    private static bool IsCrewPodTargetingBlocked(TractorModule module, TractorableItem item)
        => _isCrewPodTargetingBlocked != null
            && (bool)_isCrewPodTargetingBlocked.Invoke(module, new object[] { item });

    // The publicized stub exposes `tractorBeams` as public, but it is private in the
    // shipped DLL — Mono enforces field access checks at runtime (unlike public
    // methods), so direct access throws FieldAccessException. Bind it through a Harmony
    // FieldRef, whose dynamic accessor skips the visibility check.
    private static readonly AccessTools.FieldRef<TractorModule, List<TractorBeam>> _tractorBeams =
        AccessTools.FieldRefAccess<TractorModule, List<TractorBeam>>("tractorBeams");

    private static bool Enabled => Plugin.Instance != null && Plugin.Instance.CfgEnabled.Value;

    // Max beams auto-targeting may occupy: base auto beams + mastery-scaled bonus beams.
    private static int AutoCap(TractorModule module)
        => module.amountOfBeams + AutopilotMastery.ExtraAuto(module.amountOfBonusBeams);

    private static TractorBeam? FirstFreeBeam(TractorModule module)
    {
        foreach (TractorBeam beam in _tractorBeams(module))
        {
            if (!beam.HasTarget())
                return beam;
        }
        return null;
    }

    // Patch 1 — the precise lever. Only acts when vanilla found no beam in the
    // requested pool, on the player ship, with the feature enabled.
    [HarmonyPostfix]
    [HarmonyPatch(nameof(TractorModule.GetAvailableTractorBeam))]
    private static void GetAvailableTractorBeam_Postfix(TractorModule __instance, bool bonus, ref TractorBeam __result)
    {
        if (__result != null || !Enabled || !__instance.IsPlayer())
            return;

        if (bonus)
        {
            // Manual request, bonus pool exhausted: borrow any free beam (symmetric half).
            TractorBeam? borrowed = FirstFreeBeam(__instance);
            if (borrowed != null)
                __result = borrowed;
            return;
        }

        // Auto request, non-bonus pool exhausted: lend a bonus beam only while the
        // number of beams currently in use is below the mastery-scaled auto capacity.
        // inUse counts ALL busy beams (no per-beam auto/manual ownership tag exists),
        // so this is conservative by design: auto never occupies more than extraAuto
        // bonus beams, and if manual targeting is borrowing simultaneously, auto may
        // promote slightly fewer. Intentional — see the design spec's risk notes.
        int inUse = _tractorBeams(__instance).Count(b => b.HasTarget());
        if (inUse < AutoCap(__instance))
        {
            TractorBeam? promoted = FirstFreeBeam(__instance);
            if (promoted != null)
                __result = promoted;
        }
    }

    // Patch 2 — raise the auto-candidate cap to autoCap so the converted beams
    // engage in a single targeting cycle instead of trickling in over later ticks.
    [HarmonyPostfix]
    [HarmonyPatch(nameof(TractorModule.UpdateAvailableTargets))]
    private static void UpdateAvailableTargets_Postfix(TractorModule __instance, IEnumerable<TargetableUnit> targets)
    {
        if (!Enabled || !__instance.IsPlayer())
            return;

        int autoCap = AutoCap(__instance);
        List<TargetableUnit> filtered = __instance.filteredTargets;
        if (filtered.Count >= autoCap)
            return;

        foreach (TargetableUnit target in targets)
        {
            if (filtered.Count >= autoCap)
                break;
            if (target is TractorableItem item
                && !item.isTractored
                && item.CanBeAutoTractoredBy(__instance.parent)
                && !IsCrewPodTargetingBlocked(__instance, item)
                && !filtered.Contains(item))
            {
                filtered.Add(item);
            }
        }
    }
}
