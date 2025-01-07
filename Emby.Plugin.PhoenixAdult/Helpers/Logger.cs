using MediaBrowser.Model.Logging;

namespace PhoenixAdult.Helpers
{
    internal static class Logger
    {
        private static ILogger Log { get; } = Plugin.Log;

        public static void Info(string text)
        {
            Log?.Info(text);
        }

        public static void Error(string text)
        {
            Log?.Error(text);
        }

        public static void Debug(string text)
        {
            Log?.Debug(text);
        }

        public static void Warning(string text)
        {
            Log?.Warn(text);
        }
    }
}
