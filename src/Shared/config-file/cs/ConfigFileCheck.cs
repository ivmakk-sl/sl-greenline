// Library copy of shared/config-file 1.0.0. Do not edit: see src/Shared/config-file/VERSION.
using System;
using System.IO;

namespace SlShared.ConfigFile
{
    // Tells whether the config file of a mod was written since the last record: its write time or its size changed.
    // BepInEx does not watch the file, so a mod checks it at a few points (cs-game/ConfigFileReload.cs) and reads it
    // again only when it changed. A mod records after each read and after each save of its own.
    internal sealed class ConfigFileCheck
    {
        private readonly string path;
        private DateTime? time;
        private long size;

        public ConfigFileCheck(string path)
        {
            this.path = path;
        }

        // A missing file is no change: there is nothing to read again.
        public bool Changed()
        {
            if (!File.Exists(path)) return false;
            var info = new FileInfo(path);
            return time != info.LastWriteTimeUtc || size != info.Length;
        }

        public void Record()
        {
            var info = new FileInfo(path);
            time = info.Exists ? info.LastWriteTimeUtc : (DateTime?)null;
            size = info.Exists ? info.Length : 0;
        }

        // The ready message of a HUD page (type 2 with the page id CoreUI1 or CoreUI0), which the web UI sends at each
        // save load: the point where a mod reads a hand edit of its file at a load.
        public static bool IsHudReady(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            string[] fields = message.Split('\u001E');
            return fields.Length >= 2 && fields[0] == "2" && (fields[1] == "CoreUI1" || fields[1] == "CoreUI0");
        }
    }
}
