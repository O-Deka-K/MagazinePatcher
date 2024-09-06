namespace MagazinePatcher
{
    public static class PatcherStatus
    {
        public static float PatcherProgress { get => patcherProgress; }

        public static string CacheLog = "";

        private static float patcherProgress = 0;

        public static bool CachingFailed = false;

        public static void UpdateProgress(float progress)
        {
            patcherProgress = progress;
        }

        public static void AppendCacheLog(string log)
        {
            CacheLog += "\n" + log;
        }

    }
}
