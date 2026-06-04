using Behaviour.Crew;
using Source.Personnel;
using Source.Player;
using Source.Util;
using UnityEngine;

namespace VGTractorAuto;

// Single source of truth for the Autopilot (Engineering / "PromptEngineering")
// mastery the mod scales against. IdleManager feeds this tree's XP while autopilot
// runs; we reuse its mastery level. The tree reference is cached on first successful
// lookup so we survive null windows during init.
internal static class AutopilotMastery
{
    private static Skilltree? _tree;

    // Live Engineering-tree mastery for the player commander. Returns 0 whenever the
    // player, commander, or tree is unavailable (→ vanilla). Never throws.
    internal static int Level()
    {
        if (GamePlayer.current == null || GamePlayer.current.commander == null)
            return 0;

        if (_tree == null)
        {
            _tree = Skilltree.Get(
                SkillTreeData.GetSpecializationTreeName(CommanderSpecialization.Engineering));
        }

        return _tree == null ? 0 : _tree.GetMasteryLevel();
    }

    // Pure: bonus beams promoted to auto = floor( clamp01(level / cap) * amountOfBonusBeams ).
    internal static int ComputeExtraAuto(int level, int cap, int amountOfBonusBeams)
    {
        if (cap <= 0 || amountOfBonusBeams <= 0)
            return 0;
        return Mathf.FloorToInt(Mathf.Clamp01((float)level / cap) * amountOfBonusBeams);
    }

    // Pure: conversion percentage = floor( clamp01(level / cap) * 100 ).
    internal static int ComputePercent(int level, int cap)
    {
        if (cap <= 0)
            return 0;
        return Mathf.FloorToInt(Mathf.Clamp01((float)level / cap) * 100f);
    }

    // Live wrappers over the pure helpers, using the current level and level cap.
    internal static int ExtraAuto(int amountOfBonusBeams) => ComputeExtraAuto(Level(), GameMath.maxLevel, amountOfBonusBeams);

    internal static int Percent() => ComputePercent(Level(), GameMath.maxLevel);
}
