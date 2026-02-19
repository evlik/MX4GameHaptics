namespace Loupedeck.LogiHapticPlugin
{
    using System;

    /// <summary>
    /// Static helper for plugin logging.
    /// </summary>
    internal static class PluginLog
    {
        private static PluginLogFile _log;

        public static void Init(PluginLogFile log) => _log = log;

        public static void Verbose(String message) => _log?.Verbose(message);
        public static void Info(String message) => _log?.Info(message);
        public static void Warning(String message) => _log?.Warning(message);
        public static void Warning(Exception ex, String message) => _log?.Warning(ex, message);
        public static void Error(String message) => _log?.Error(message);
        public static void Error(Exception ex, String message) => _log?.Error(ex, message);
    }
}
