using BepInEx;
using BepInEx.Configuration;
using FistVR;
using Stratum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Valve.Newtonsoft.Json;

namespace MagazinePatcher
{
    [BepInPlugin("h3vr.magazinepatcher", "MagazinePatcher", "0.3.3")]
    [BepInDependency("h3vr.otherloader", BepInDependency.DependencyFlags.HardDependency)] // TODO add version
    [BepInDependency("nrgill28.Sodalite", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(StratumRoot.GUID, StratumRoot.Version)]
    public class MagazinePatcher : StratumPlugin
    {
        private static ConfigEntry<bool> _resetBasicCacheOnNextStart;
        private static ConfigEntry<bool> _resetXlCacheOnNextStart;
        private static ConfigEntry<bool> _deleteCacheOnNextStart;
        private static ConfigEntry<bool> _enableLogging;
        private static ConfigEntry<bool> _logDebugInfo;
        private static string _fullCachePath;
        private static string _blacklistPath;
        private static string _basicCachePath;
        private static string _xlCachePath;
        internal static string LastTouchedItem;

        private void Awake()
        {
            PatchLogger.Init();
            GetPaths();
            LoadConfigFile();
        }

        private void GetPaths()
        {
            string cachePath = Path.Combine(BepInEx.Paths.CachePath, "MagazinePatcher");

            _fullCachePath = Path.Combine(cachePath, "CachedCompatibleMags.json");
            PatchLogger.Log($"Full cache path: {_fullCachePath}", PatchLogger.LogType.Debug);

            DirectoryInfo dirInfo = new(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            Stratum.PluginDirectories dirs = new(dirInfo);

            _blacklistPath = Path.Combine(dirs.Data.FullName, "MagazineCacheBlacklist.json");
            PatchLogger.Log($"Blacklist path: {_blacklistPath}", PatchLogger.LogType.Debug);

            string backupPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "backup");

            _basicCachePath = Path.Combine(backupPath, "CachedCompatibleMags-Basic.json");
            PatchLogger.Log($"Backup cache path: {_basicCachePath}", PatchLogger.LogType.Debug);

            _xlCachePath = Path.Combine(backupPath, "CachedCompatibleMags-XL.json");
            PatchLogger.Log($"Backup cache path: {_basicCachePath}", PatchLogger.LogType.Debug);
        }

        private void LoadConfigFile()
        {
            PatchLogger.Log("Getting config file", PatchLogger.LogType.General);

            _resetBasicCacheOnNextStart = Config.Bind("General",
                "Reset to Basic Cache on Next Start",
                false,
                "If true, resets the cache from a basic starting cache on the next start of H3VR. MagazinePatcher will then cache any items that are missing from it. This setting will always be set back to false after startup.");

            _resetXlCacheOnNextStart = Config.Bind("General",
                "Reset to XL Cache on Next Start",
                false,
                "Experimental. If true, resets the cache from the XL starting cache on the next start of H3VR. This is a large cache made from many available mods. This setting will always be set back to false after startup.");

            _deleteCacheOnNextStart = Config.Bind("General",
                "Delete Cache on Next Start",
                false,
                "Nuclear option. If true, deletes the cache on the next start of H3VR. The cache will be built from scratch. This takes the most time and the most RAM to finish. This setting will always be set back to false after startup.");

            _enableLogging = Config.Bind("Debug",
                "EnableLogging",
                true,
                "Set to true to enable logging");

            _logDebugInfo = Config.Bind("Debug",
                "LogDebugInfo",
                false,
                "If true, logs extra debugging info");

            Config.SettingChanged += ConfigSettingChanged;

            PatchLogger.AllowLogging = _enableLogging.Value;
            PatchLogger.LogDebug = _logDebugInfo.Value;
        }

        private void ConfigSettingChanged(object sender, SettingChangedEventArgs e)
        {
            // Only one of these can be active at a time
            if (e.ChangedSetting == _resetBasicCacheOnNextStart)
            {
                if (_resetBasicCacheOnNextStart.Value)
                {
                    _resetXlCacheOnNextStart.Value = false;
                    _deleteCacheOnNextStart.Value = false;
                }
            }
            else if (e.ChangedSetting == _resetXlCacheOnNextStart)
            {
                if (_resetXlCacheOnNextStart.Value)
                {
                    _resetBasicCacheOnNextStart.Value = false;
                    _deleteCacheOnNextStart.Value = false;
                }
            }
            else if (e.ChangedSetting == _deleteCacheOnNextStart)
            {
                if (_deleteCacheOnNextStart.Value)
                {
                    _resetBasicCacheOnNextStart.Value = false;
                    _resetXlCacheOnNextStart.Value = false;
                }
            }
        }

        public override void OnSetup(IStageContext<Empty> ctx)
        {
            // Nothing to see here!
        }

        public override IEnumerator OnRuntime(IStageContext<IEnumerator> ctx)
        {
            PatchLogger.Log("MagazinePatcher runtime has started!", PatchLogger.LogType.General);

            StartCoroutine(RunAndCatch(LoadMagazineCacheAsync(), e =>
            {
                PatcherStatus.AppendCacheLog($"Something bad happened while caching item: {LastTouchedItem}");
                PatcherStatus.CachingFailed = true;

                PatchLogger.LogError($"Something bad happened while caching item: {LastTouchedItem}");
                PatchLogger.LogError(e.ToString());
            }));

            yield break;
        }


        private static Dictionary<string, MagazineBlacklistEntry> GetMagazineCacheBlacklist()
        {
            try
            {
                // If the magazine blacklist file does not exist, log an errpr
                if (string.IsNullOrEmpty(_blacklistPath) || !File.Exists(_blacklistPath))
                {
                    PatchLogger.LogError("Failed to load magazine blacklist! Creating new one!");
                    return CreateNewBlacklist();
                }

                // Read the blacklist
                string blacklistString = File.ReadAllText(_blacklistPath);

                // If the blacklist file is empty, create a new one
                if (string.IsNullOrEmpty(blacklistString))
                {
                    return CreateNewBlacklist();
                }

                // If the file does exist, we'll try to deserialize it
                List<MagazineBlacklistEntry> blacklistDeserialized = JsonConvert.DeserializeObject<List<MagazineBlacklistEntry>>(blacklistString);

                Dictionary<string, MagazineBlacklistEntry> blacklist = [];
                foreach (MagazineBlacklistEntry entry in blacklistDeserialized)
                {
                    blacklist.Add(entry.FirearmID, entry);
                }

                return blacklist;
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Failed to load magazine blacklist! Creating new one! Stack trace below:");
                PatchLogger.LogError(ex.ToString());

                return CreateNewBlacklist();
            }
        }


        private static Dictionary<string, MagazineBlacklistEntry> CreateNewBlacklist()
        {
            PatchLogger.Log("Blacklist does not exist! Building new one", PatchLogger.LogType.General);
            Dictionary<string, MagazineBlacklistEntry> blacklist = [];

            StreamWriter sw = File.CreateText(_blacklistPath);
            List<MagazineBlacklistEntry> blacklistEntry = [];

            MagazineBlacklistEntry sample = new()
            {
                FirearmID = "SKSClassic"
            };
            sample.MagazineWhitelist.Add("None");

            blacklistEntry.Add(sample);

            string blacklistString = JsonConvert.SerializeObject(blacklistEntry, Formatting.Indented);
            sw.WriteLine(blacklistString);
            sw.Close();

            foreach (MagazineBlacklistEntry entry in blacklistEntry)
            {
                blacklist.Add(entry.FirearmID, entry);
            }

            return blacklist;
        }


        private static void PokeOtherLoader()
        {
            OtherLoader.LoaderStatus.GetLoaderProgress();
        }

        private static float GetOtherLoaderProgress()
        {
            return OtherLoader.LoaderStatus.GetLoaderProgress();
        }


        private static IEnumerator LoadMagazineCacheAsync()
        {
            PatcherStatus.AppendCacheLog("Patching has started");

            bool canCache = false;
            bool isOtherloaderLoaded = false;

            try
            {
                PokeOtherLoader();
                isOtherloaderLoaded = true;
                PatchLogger.Log("Otherloader detected!", PatchLogger.LogType.General);
            }
            catch
            {
                PatchLogger.Log("Otherloader not detected!", PatchLogger.LogType.General);
            }

            do
            {
                yield return null;

                if (isOtherloaderLoaded)
                {
                    canCache = GetOtherLoaderProgress() >= 1;
                }
            }
            while (!canCache && isOtherloaderLoaded);

            PatcherStatus.AppendCacheLog("Checking Cache");

            bool isCacheValid = LoadFullCache();

            CompatibleMagazineCache.BlacklistEntries = GetMagazineCacheBlacklist();

            // If the magazine cache file didn't exist, or wasn't valid, we must build a new one
            if (!isCacheValid)
            {
                yield return CacheBuilder.Build(_fullCachePath);
            }

            PatchLogger.Log("Applying magazine cache to firearms", PatchLogger.LogType.General);

            ApplyMagazineCache(CompatibleMagazineCache.Instance);
            RemoveBlacklistedMagazines(CompatibleMagazineCache.BlacklistEntries);

            PatcherStatus.UpdateProgress(1);
            PatchLogger.Log($"[{DateTime.Now:HH:mm:ss}] Completed!", PatchLogger.LogType.General);
        }

        private static bool CheckBackupCache()
        {
            // Only one of these can be set at once
            bool forceRestoreBasic = _resetBasicCacheOnNextStart.Value;
            bool forceRestoreXL = !forceRestoreBasic && _resetXlCacheOnNextStart.Value;
            bool forceDelete = !forceRestoreBasic && !forceRestoreXL && _deleteCacheOnNextStart.Value;

            // Always reset these options, whether we do them or not
            _resetBasicCacheOnNextStart.Value = false;
            _resetXlCacheOnNextStart.Value = false;
            _deleteCacheOnNextStart.Value = false;

            if (forceDelete)
            {
                PatchLogger.Log($"Deleted cache and starting from scratch!", PatchLogger.LogType.General);
                if (File.Exists(_fullCachePath))
                    File.Delete(_fullCachePath);

                return true;
            }

            string startingCachePath = (forceRestoreXL ? _xlCachePath : _basicCachePath);

            // If starting cache doesn't exist, we can't do anything
            if (!File.Exists(startingCachePath))
            {
                PatchLogger.Log($"Starting cache is missing: {startingCachePath}!", PatchLogger.LogType.General);
                return false;
            }

            // If Full cache doesn't exist, or if Reset option has been set, copy starting cache to Full cache
            if (!File.Exists(_fullCachePath) || forceRestoreBasic || forceRestoreXL)
            {
                File.Copy(startingCachePath, _fullCachePath, true);
                PatchLogger.Log($"Starting cache restored from {startingCachePath}!", PatchLogger.LogType.General);
                return true;
            }

            return false;
        }

        private static bool LoadFullCache()
        {
            bool isCacheValid = false;

            CheckBackupCache();

            // If the cache exists, we load it and check it's validity
            if (!string.IsNullOrEmpty(_fullCachePath) && File.Exists(_fullCachePath))
            {
                try
                {
                    string cacheJson = File.ReadAllText(_fullCachePath);
                    CompatibleMagazineCache cache = JsonConvert.DeserializeObject<CompatibleMagazineCache>(cacheJson);
                    CompatibleMagazineCache.Instance = cache;

                    isCacheValid = IsMagazineCacheValid(CompatibleMagazineCache.Instance);

                    PatchLogger.Log("Cache file found! Is Valid? " + isCacheValid, PatchLogger.LogType.General);
                }
                catch (Exception e)
                {
                    CompatibleMagazineCache cache = new();
                    CompatibleMagazineCache.Instance = cache;

                    PatchLogger.LogError("Failed to read cache file!");
                    PatchLogger.LogError(e.ToString());

                    File.Delete(_fullCachePath);
                }
            }
            else
            {
                PatchLogger.Log("Cache file not found! Creating new cache file", PatchLogger.LogType.General);

                CompatibleMagazineCache cache = new();
                CompatibleMagazineCache.Instance = cache;
            }

            return isCacheValid;
        }

        public static bool ValidFireArm(FireArmRoundType roundType, FireArmClipType clipType, FireArmMagazineType magazineType, int magazineCapacity)
        {
            return roundType != FireArmRoundType.a22_LR || magazineType != FireArmMagazineType.mNone || magazineCapacity != 0 || clipType != FireArmClipType.None;
        }

        // Applies the loaded magazine cache onto all firearms, magazines, clips, etc
        private static void ApplyMagazineCache(CompatibleMagazineCache magazineCache)
        {
            // This part fills out the IM.CompatMags dictionary for every magazine, and populates the magazines properties
            foreach (KeyValuePair<FireArmMagazineType, List<AmmoObjectDataTemplate>> pair in CompatibleMagazineCache.Instance.MagazineData)
            {
                if (!IM.CompatMags.ContainsKey(pair.Key))
                {
                    IM.CompatMags.Add(pair.Key, []);
                }

                List<FVRObject> loadedMags = [];
                foreach (AmmoObjectDataTemplate magTemplate in pair.Value)
                {
                    if (IM.OD.ContainsKey(magTemplate.ObjectID))
                    {
                        FVRObject mag = IM.OD[magTemplate.ObjectID];
                        mag.MagazineType = pair.Key;
                        mag.RoundType = magTemplate.RoundType;
                        mag.MagazineCapacity = magTemplate.Capacity;
                        loadedMags.Add(mag);
                    }
                }

                IM.CompatMags[pair.Key] = loadedMags;
            }

            // Apply the magazine cache values to every firearm that is loaded
            foreach (MagazineCacheEntry entry in magazineCache.Entries.Values)
            {
                if (IM.OD.ContainsKey(entry.FirearmID))
                {
                    FVRObject firearm = IM.OD[entry.FirearmID];

                    // Note, only apply magazine type if magazines exist for gun, because of some assumptions made by game code
                    if (IM.CompatMags.ContainsKey(entry.MagType))
                    {
                        firearm.MagazineType = entry.MagType;
                    }

                    firearm.RoundType = entry.BulletType;
                    firearm.ClipType = entry.ClipType;

                    LastTouchedItem = entry.FirearmID;

                    int MaxCapacityRelated = -1;
                    int MinCapacityRelated = -1;

                    if (entry.MagType != FireArmMagazineType.mNone)
                    {
                        foreach (string mag in entry.CompatibleMagazines)
                        {
                            if (IM.OD.ContainsKey(mag) && (!firearm.CompatibleMagazines.Any(o => (o != null && o.ItemID == mag))))
                            {
                                FVRObject magazineObject = IM.OD[mag];
                                firearm.CompatibleMagazines.Add(magazineObject);

                                if (magazineCache.AmmoObjects.ContainsKey(mag))
                                    magazineObject.MagazineCapacity = magazineCache.AmmoObjects[mag].Capacity;

                                if (MaxCapacityRelated < magazineObject.MagazineCapacity)
                                    MaxCapacityRelated = magazineObject.MagazineCapacity;

                                if (MinCapacityRelated == -1)
                                    MinCapacityRelated = magazineObject.MagazineCapacity;
                                else if (MinCapacityRelated > magazineObject.MagazineCapacity)
                                    MinCapacityRelated = magazineObject.MagazineCapacity;
                            }
                        }
                    }

                    if (entry.ClipType != FireArmClipType.None)
                    {
                        foreach (string clip in entry.CompatibleClips)
                        {
                            if (IM.OD.ContainsKey(clip) && (!firearm.CompatibleClips.Any(o => (o != null && o.ItemID == clip))))
                            {
                                FVRObject clipObject = IM.OD[clip];
                                firearm.CompatibleClips.Add(clipObject);

                                if (magazineCache.AmmoObjects.ContainsKey(clip))
                                    clipObject.MagazineCapacity = magazineCache.AmmoObjects[clip].Capacity;

                                if (MaxCapacityRelated < clipObject.MagazineCapacity)
                                    MaxCapacityRelated = clipObject.MagazineCapacity;

                                if (MinCapacityRelated == -1)
                                    MinCapacityRelated = clipObject.MagazineCapacity;
                                else if (MinCapacityRelated > clipObject.MagazineCapacity)
                                    MinCapacityRelated = clipObject.MagazineCapacity;
                            }
                        }
                    }

                    foreach (string speedloader in entry.CompatibleSpeedLoaders)
                    {
                        if (IM.OD.ContainsKey(speedloader) && (!firearm.CompatibleSpeedLoaders.Any(o => (o != null && o.ItemID == speedloader))))
                        {
                            FVRObject speedloaderObject = IM.OD[speedloader];
                            firearm.CompatibleSpeedLoaders.Add(speedloaderObject);

                            if (magazineCache.AmmoObjects.ContainsKey(speedloader))
                                speedloaderObject.MagazineCapacity = magazineCache.AmmoObjects[speedloader].Capacity;

                            if (MaxCapacityRelated < speedloaderObject.MagazineCapacity)
                                MaxCapacityRelated = speedloaderObject.MagazineCapacity;

                            if (MinCapacityRelated == -1)
                                MinCapacityRelated = speedloaderObject.MagazineCapacity;
                            else if (MinCapacityRelated > speedloaderObject.MagazineCapacity)
                                MinCapacityRelated = speedloaderObject.MagazineCapacity;
                        }
                    }

                    foreach (string bullet in entry.CompatibleBullets)
                    {
                        if (IM.OD.ContainsKey(bullet) && (!firearm.CompatibleSingleRounds.Any(o => (o != null && o.ItemID == bullet))))
                        {
                            firearm.CompatibleSingleRounds.Add(IM.OD[bullet]);
                        }
                    }

                    if (MaxCapacityRelated != -1)
                        firearm.MaxCapacityRelated = MaxCapacityRelated;

                    if (MinCapacityRelated != -1)
                        firearm.MinCapacityRelated = MinCapacityRelated;
                }
            }
        }


        private static void RemoveBlacklistedMagazines(Dictionary<string, MagazineBlacklistEntry> blacklist)
        {
            foreach (FVRObject firearm in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm])
            {
                if (blacklist.ContainsKey(firearm.ItemID))
                {
                    for (int i = firearm.CompatibleMagazines.Count - 1; i >= 0; i--)
                    {
                        if (!blacklist[firearm.ItemID].IsMagazineAllowed(firearm.CompatibleMagazines[i].ItemID))
                        {
                            firearm.CompatibleMagazines.RemoveAt(i);
                        }
                    }

                    for (int i = firearm.CompatibleClips.Count - 1; i >= 0; i--)
                    {
                        if (!blacklist[firearm.ItemID].IsClipAllowed(firearm.CompatibleClips[i].ItemID))
                        {
                            firearm.CompatibleClips.RemoveAt(i);
                        }
                    }

                    for (int i = firearm.CompatibleSingleRounds.Count - 1; i >= 0; i--)
                    {
                        if (!blacklist[firearm.ItemID].IsRoundAllowed(firearm.CompatibleSingleRounds[i].ItemID))
                        {
                            firearm.CompatibleSingleRounds.RemoveAt(i);
                        }
                    }

                    for (int i = firearm.CompatibleSpeedLoaders.Count - 1; i >= 0; i--)
                    {
                        if (!blacklist[firearm.ItemID].IsSpeedloaderAllowed(firearm.CompatibleSpeedLoaders[i].ItemID))
                        {
                            firearm.CompatibleSpeedLoaders.RemoveAt(i);
                        }
                    }
                }
            }
        }


        private static bool IsMagazineCacheValid(CompatibleMagazineCache magazineCache)
        {
            bool cacheValid = true;

            foreach (string mag in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Magazine].Select(f => f.ItemID))
            {
                if (!magazineCache.Magazines.Contains(mag))
                {
                    if (cacheValid && IM.OD[mag].GetGameObject() != null)
                    {
                        PatchLogger.LogWarning($"Magazine not found in cache: {mag}");
                        cacheValid = false;
                        // NOTE: You could return false immediately in here, but we don't for the sake of debugging
                    }
                }
            }

            foreach (string firearm in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm].Select(f => f.ItemID))
            {
                if (!magazineCache.Firearms.Contains(firearm))
                {
                    if (cacheValid && IM.OD[firearm].GetGameObject() != null)
                    {
                        PatchLogger.LogWarning($"Firearm not found in cache: {firearm}");
                        cacheValid = false;
                    }
                }
            }

            foreach (string clip in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Clip].Select(f => f.ItemID))
            {
                if (!magazineCache.Clips.Contains(clip))
                {
                    if (cacheValid && IM.OD[clip].GetGameObject() != null)
                    {
                        PatchLogger.LogWarning($"Clip not found in cache: {clip}");
                        cacheValid = false;
                    }
                }
            }

            foreach (string bullet in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Cartridge].Select(f => f.ItemID))
            {
                if (!magazineCache.Bullets.Contains(bullet))
                {
                    if (cacheValid && IM.OD[bullet].GetGameObject() != null)
                    {
                        PatchLogger.LogWarning($"Bullet not found in cache: {bullet}");
                        cacheValid = false;
                    }
                }
            }

            return cacheValid;
        }


        public static IEnumerator RunAndCatch(IEnumerator routine, Action<Exception> onError = null)
        {
            bool more = true;
            while (more)
            {
                try
                {
                    more = routine.MoveNext();
                }
                catch (Exception e)
                {
                    onError?.Invoke(e);

                    yield break;
                }

                if (more)
                {
                    yield return routine.Current;
                }
            }
        }
    }
}
