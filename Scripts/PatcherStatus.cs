using System.Collections.Generic;
using System.Linq;

namespace MagazinePatcher
{
    public static class PatcherStatus
    {
        public static float PatcherProgress { get => patcherProgress; }

        public static string CacheLog = "";

        private static float patcherProgress = 0;

        public static bool CachingFailed = false;

        private static List<string> CacheLogList = [];

        public static void UpdateProgress(float progress)
        {
            patcherProgress = progress;
        }

        public static void AppendCacheLog(string log)
        {
            CacheLogList.Add(log);

            if (CacheLogList.Count > 6)
                CacheLogList.RemoveAt(0);

            CacheLog = string.Join("\n", CacheLogList.ToArray());
        }

    }
}
