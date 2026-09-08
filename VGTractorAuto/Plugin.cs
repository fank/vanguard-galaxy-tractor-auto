using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace VGTractorAuto;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("VanguardGalaxy.exe")]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "vgtractorauto";
    public const string PluginName = "Tractor Auto";
    // BepInEx parses PluginVersion through System.Version — plain N.N.N only.
    public const string PluginVersion = "0.2.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal ConfigEntry<bool> CfgEnabled = null!;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        CfgEnabled = Config.Bind("General", "Enabled", true,
            "Let the player ship's Manual Tractor Beams also auto-tractor, scaled 0-100% by " +
            "Autopilot (Engineering) skill-tree mastery. At 0 mastery this is pure vanilla; at " +
            "the level cap, all manual beams auto-tractor. Manual targeting can still claim any " +
            "free beam. When false, vanilla behavior is fully restored.");

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Patches.TractorModulePatches));
        _harmony.PatchAll(typeof(Patches.MasteryTooltipPatches));
        Log.LogInfo($"{PluginName} v{PluginVersion} loaded ({_harmony.GetPatchedMethods().Count()} patches)");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
