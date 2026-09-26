using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("local.seaofstars.generousblock", "Parries and Blocks", "2.2.4")]
public sealed class Plugin : BasePlugin
{
    private Harmony harmony;

    public override void Load()
    {
        ModSettings.Initialize(Config);
        harmony = new Harmony("local.seaofstars.generousblock");
        try
        {
            HoldAssist.Install(harmony, Log);
            ModsMenu.Install(harmony, Log);
            AddComponent<ComboHudDriver>();
            Log.LogInfo($"Settings: block={ModSettings.BlockHoldDuration.Value:0.0}s, " +
                $"attack={ModSettings.AttackHoldDuration.Value:0.0}s, combo={ModSettings.SpecialComboTarget.Value}, " +
                $"special={ModSettings.SpecialHoldDuration.Value:0.0}s.");
            Log.LogInfo("Parries and Blocks 2.2.4 loaded with bounded hold automation and complete counter-mode special support.");
        }
        catch
        {
            harmony.UnpatchSelf();
            throw;
        }
    }

    public override bool Unload()
    {
        ComboHud.Dispose();
        harmony?.UnpatchSelf();
        return true;
    }
}

public sealed class ComboHudDriver : MonoBehaviour
{
    public void Update() => ComboHud.Tick();
}

public static class ModSettings
{
    public static ConfigEntry<bool> Enabled { get; private set; }
    public static ConfigEntry<float> BlockHoldDuration { get; private set; }
    public static ConfigEntry<float> AttackHoldDuration { get; private set; }
    public static ConfigEntry<int> SpecialComboTarget { get; private set; }
    public static ConfigEntry<float> SpecialHoldDuration { get; private set; }

    public static void Initialize(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true, "Enable the mod.");
        BlockHoldDuration = Seconds(config, "BlockHoldDurationSeconds", 1.0f,
            "Maximum continuous hold time for automatic blocks.", 0.1f, 10f);
        AttackHoldDuration = Seconds(config, "AttackHoldDurationSeconds", 1.0f,
            "Maximum continuous hold time for normal attacks and locked special attacks.", 0.1f, 10f);
        SpecialComboTarget = config.Bind("General", "SpecialComboTarget", 5,
            new ConfigDescription("Consecutive special successes required to unlock extended hold automation.",
                new AcceptableValueRange<int>(1, 20)));
        SpecialHoldDuration = Seconds(config, "SpecialHoldDurationSeconds", 10.0f,
            "Extended hold automation time after reaching the special combo target.", 0.1f, 30f);
    }

    private static ConfigEntry<float> Seconds(ConfigFile config, string key, float value, string description,
        float minimum, float maximum)
        => config.Bind("General", key, value,
            new ConfigDescription(description, new AcceptableValueRange<float>(minimum, maximum)));
}
