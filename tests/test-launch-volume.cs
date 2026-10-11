using System;
using System.Collections.Generic;
using MelonLoader;

namespace MelonLoader
{
    public static class MelonLogger
    {
        public static int Warnings;
        public static void Warning(string text) { Warnings++; }
    }
    public sealed class MelonPreferences_Entry<T> { public T Value; }
    public sealed class MelonPreferences_Category
    {
        public MelonPreferences_Entry<T> CreateEntry<T>(string key, T value, string displayName, string description)
        {
            // Mirror the native API's SetupEntryFromRawValue before saving.
            return new MelonPreferences_Entry<T> { Value = MelonPreferences.Saved.ContainsKey(key) ?
                (T)MelonPreferences.Saved[key] : value };
        }
        public void SaveToFile(bool printMessage) { MelonPreferences.Saves++; }
    }
    public static class MelonPreferences
    {
        public static readonly Dictionary<string, object> Saved = new Dictionary<string, object>();
        public static int Saves;
        public static bool Fail;
        public static MelonPreferences_Category CreateCategory(string key, string displayName)
        {
            if (Fail) throw new Exception("simulated preference failure");
            if (key != "HMMWV_LOSAT") throw new Exception("wrong category");
            return new MelonPreferences_Category();
        }
    }
}
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Near(float actual, float expected, string message)
        { Check(Math.Abs(actual - expected) < 0.00001f, message); }

        public static void Main()
        {
            var mod = new Mod();
            Near(mod.GetLaunchAudioVolume(), 1.69f, "safe default before initialization");
            mod.InitializeLaunchAudioSettings();
            Near(mod.GetLaunchAudioVolume(), 1.69f, "35 percent quieter default");
            Check(MelonPreferences.Saves == 1, "initialize saves preference once");
            mod._launchVolume.Value = 0f; Near(mod.GetLaunchAudioVolume(), 0f, "mute");
            mod._launchVolume.Value = 0.5f; Near(mod.GetLaunchAudioVolume(), 1.3f, "half original gain");
            mod._launchVolume.Value = 1f; Near(mod.GetLaunchAudioVolume(), 2.6f, "original gain");
            mod._launchVolume.Value = -10f; Near(mod.GetLaunchAudioVolume(), 0f, "negative clamps");
            mod._launchVolume.Value = 100f; Near(mod.GetLaunchAudioVolume(), 2.6f, "excessive clamps");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            { mod._launchVolume.Value = invalid; Near(mod.GetLaunchAudioVolume(), 1.69f, "invalid fallback"); }
            MelonPreferences.Saved["LaunchVolume"] = 0.3f;
            MelonPreferences.Saved["OtherModValue"] = "preserved";
            var restarted = new Mod(); restarted.InitializeLaunchAudioSettings();
            Near(restarted.GetLaunchAudioVolume(), 0.78f, "existing user setting respected");
            Check((string)MelonPreferences.Saved["OtherModValue"] == "preserved", "other preferences untouched");
            int saves = MelonPreferences.Saves;
            for (int i = 0; i < 10000; i++) Near(restarted.GetLaunchAudioVolume(), 0.78f, "cached read");
            Check(MelonPreferences.Saves == saves, "no per-shot file saves");
            MelonPreferences.Fail = true;
            var fallback = new Mod(); fallback.InitializeLaunchAudioSettings();
            for (int i = 0; i < 10000; i++) Near(fallback.GetLaunchAudioVolume(), 1.69f, "failure fallback");
            Check(MelonLogger.Warnings == 1, "initialization failure warns once, no repeated retries");
            Console.WriteLine("ACTUAL_LAUNCH_SETTINGS_DEFAULT_MUTE_ORIGINAL_CLAMPS_SAVED_VALUE_AND_FAILURE_FALLBACK_PASSED_NOT_GAMEPLAY");
        }
    }
}
