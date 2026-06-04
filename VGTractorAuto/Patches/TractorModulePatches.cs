using System.Collections.Generic;
using System.Linq;
using Behaviour.Crew;
using Behaviour.Equipment.Module;
using Behaviour.Tractoring;
using Behaviour.Weapons;
using HarmonyLib;
using System.Reflection;
using Source.Personnel;
using Source.Player;
using Source.Util;
using UnityEngine;

namespace VGTractorAuto.Patches;

[HarmonyPatch(typeof(TractorModule))]
internal static class TractorModulePatches
{
    // Cached "Autopilot" skill tree — internally the Engineering / "PromptEngineering"
    // specialization (IdleManager feeds its mastery XP while autopilot runs). We reuse
    // its mastery level as the scaling axis. Resolved lazily on first successful lookup
    // so we survive null windows during init.
    private static Skilltree? _autopilotTree;

    // Vanilla's private crew-pod target filter (blocks hostile/non-roll crew pods when
    // crew/brig space is full). Not exposed by the publicized stub we compile against,
    // so we bind it reflectively; null on older builds that lack it → treated as not
    // blocked, which matches those builds' vanilla behavior.
    private static readonly MethodInfo? _isCrewPodTargetingBlocked = AccessTools.Method(
        typeof(TractorModule), "IsCrewPodTargetingBlocked", new[] { typeof(TractorableItem) });

    private static bool IsCrewPodTargetingBlocked(TractorModule module, TractorableItem item)
        => _isCrewPodTargetingBlocked != null
            && (bool)_isCrewPodTargetingBlocked.Invoke(module, new object[] { item });

    private static bool Enabled => Plugin.Instance != null && Plugin.Instance.CfgEnabled.Value;

    // Live Engineering-tree mastery for the player commander. Returns 0 (→ vanilla)
    // whenever the player, the tree, or its skill-tree data is unavailable.
    private static int ResolveMasteryLevel()
    {
        if (GamePlayer.current == null || GamePlayer.current.commander == null)
            return 0;

        if (_autopilotTree == null)
        {
            _autopilotTree = Skilltree.Get(
                SkillTreeData.GetSpecializationTreeName(CommanderSpecialization.Engineering));
        }

        return _autopilotTree == null ? 0 : _autopilotTree.GetMasteryLevel();
    }

    // Pure: how many bonus (manual) beams are promoted to auto at this mastery.
    // extraAuto = floor( clamp01(mastery / cap) * amountOfBonusBeams )
    internal static int ComputeExtraAuto(int masteryLevel, int cap, int amountOfBonusBeams)
    {
        if (cap <= 0 || amountOfBonusBeams <= 0)
            return 0;
        float pct = Mathf.Clamp01((float)masteryLevel / cap);
        return Mathf.FloorToInt(pct * amountOfBonusBeams);
    }

    // Max beams auto-targeting may occupy: base auto beams + mastery-scaled bonus beams.
    private static int AutoCap(TractorModule module)
    {
        int extraAuto = ComputeExtraAuto(ResolveMasteryLevel(), GameMath.maxLevel, module.amountOfBonusBeams);
        return module.amountOfBeams + extraAuto;
    }

    private static TractorBeam? FirstFreeBeam(TractorModule module)
    {
        foreach (TractorBeam beam in module.tractorBeams)
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
        int inUse = __instance.tractorBeams.Count(b => b.HasTarget());
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
