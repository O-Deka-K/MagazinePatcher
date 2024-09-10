using BepInEx.Logging;

namespace MagazinePatcher
{
    static class PatchLogger
    {
        public static ManualLogSource BepLog;

        public static bool AllowLogging = false;
        public static bool LogDebug = false;

        public enum LogType
        {
            General,
            Debug,
        }

        public static void Init()
        {
            BepLog = Logger.CreateLogSource("MagazinePatcher");
        }

        public static void Log(string log, LogType type)
        {
            if (AllowLogging)
            {
                if (type == LogType.General)
                {
                    BepLog.LogInfo(log);
                }
                else if (type == LogType.Debug && LogDebug)
                {
                    BepLog.LogInfo(log);
                }
            }
        }

        public static void LogWarning(string log)
        {
            BepLog.LogWarning(log);
        }

        public static void LogError(string log)
        {
            BepLog.LogError(log);
        }

    }
}
