// Library copy of shared/config-file 1.0.0. Do not edit: see src/Shared/config-file/VERSION.
using System;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;

namespace SlShared.ConfigFile
{
    // Reads the config file of one mod again after a hand edit, at the HUD ready message of each save load and at each
    // open of the settings window. BepInEx reads the file only at the game start and does not watch it. The read goes
    // through the mod (reload: ConfigFile.Reload()), which fires SettingChanged for each changed entry, so the mod applies
    // a changed switch as after a change in a config manager. A config manager changes an entry in memory and saves the
    // file: the mod records after its own saves (Record), so that save does not read the file again.
    // Each mod has its own copy of this class, so each mod sets it up for its own file in Load, before its patches, and
    // adds ConfigFileReloadOnHudReady and ConfigFileReloadOnSettings to its patch list.
    internal static class ConfigFileReload
    {
        private static ConfigFileCheck check;
        private static Action reload;
        private static Action<string> warn;
        private static Action<string> debug;

        // path: ConfigFile.ConfigFilePath of the mod. reload: ConfigFile.Reload() of the mod. warn: a warning to the log
        // of the mod. debug: a debug line, which the mod writes only when Verbose is on.
        internal static void Setup(string path, Action reload, Action<string> warn, Action<string> debug)
        {
            check = new ConfigFileCheck(path);
            check.Record();
            ConfigFileReload.reload = reload;
            ConfigFileReload.warn = warn;
            ConfigFileReload.debug = debug;
        }

        // After a save of the file by the mod or by a config manager in the game.
        internal static void Record() => check?.Record();

        // Reads the file again when it changed since the last record. The record comes only after a good read, so a
        // file that an editor still writes is read again at the next check.
        internal static void ReloadIfChanged(string why)
        {
            if (check == null || !check.Changed()) return;
            debug?.Invoke($"config: the config file changed, reading it again ({why})");
            try
            {
                reload();
                check.Record();
            }
            catch (Exception e)
            {
                warn?.Invoke($"could not read the config file again: {e.Message}");
            }
        }
    }

    // The ready message of a HUD page comes at each save load. The Postfix leaves the game's own handling as it is.
    [HarmonyPatch(typeof(WebUILayer), "OnMessageFromJS")]
    internal static class ConfigFileReloadOnHudReady
    {
        private static void Postfix(Vuplex.WebView.EventArgs<string> eventArgs)
        {
            if (ConfigFileCheck.IsHudReady(eventArgs?.Value)) ConfigFileReload.ReloadIfChanged("a save load");
        }
    }

    // The open of the settings window (the Settings item of the pause window, Ac_OutSetting_OpenSettings).
    [HarmonyPatch(typeof(Reducer_Web_OutSetting), nameof(Reducer_Web_OutSetting.Trigger_RA_OpenSettings))]
    internal static class ConfigFileReloadOnSettings
    {
        private static void Postfix() => ConfigFileReload.ReloadIfChanged("the settings window");
    }
}
