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
using UnityEngine;
using Valve.Newtonsoft.Json;

namespace MagazinePatcher
{
    [BepInPlugin("h3vr.magazinepatcher", "MagazinePatcher", "0.3.1")]
    [BepInDependency("h3vr.otherloader", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("nrgill28.Sodalite", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(StratumRoot.GUID, StratumRoot.Version)]
    public class MagazinePatcher : StratumPlugin
    {
        private static ConfigEntry<bool> ResetBasicCacheOnNextStart;
        private static ConfigEntry<bool> ResetXLCacheOnNextStart;
        private static ConfigEntry<bool> DeleteCacheOnNextStart;
        private static ConfigEntry<bool> EnableLogging;
        private static ConfigEntry<bool> LogDebugInfo;
        private static string FullCachePath;
        private static string BlacklistPath;
        private static string BasicCachePath;
        private static string XLCachePath;
        private static string LastTouchedItem;

        private void Awake()
        {
            PatchLogger.Init();
            GetPaths();
            LoadConfigFile();
        }

        private void GetPaths()
        {
            string cachePath = Path.Combine(BepInEx.Paths.CachePath, "MagazinePatcher");

            FullCachePath = Path.Combine(cachePath, "CachedCompatibleMags.json");
            PatchLogger.Log($"Full cache path: {FullCachePath}", PatchLogger.LogType.Debug);

            DirectoryInfo dirInfo = new(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            Stratum.PluginDirectories dirs = new(dirInfo);

            BlacklistPath = Path.Combine(dirs.Data.FullName, "MagazineCacheBlacklist.json");
            PatchLogger.Log($"Blacklist path: {BlacklistPath}", PatchLogger.LogType.Debug);

            string backupPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "backup");

            BasicCachePath = Path.Combine(backupPath, "CachedCompatibleMags-Basic.json");
            PatchLogger.Log($"Backup cache path: {BasicCachePath}", PatchLogger.LogType.Debug);

            XLCachePath = Path.Combine(backupPath, "CachedCompatibleMags-XL.json");
            PatchLogger.Log($"Backup cache path: {BasicCachePath}", PatchLogger.LogType.Debug);
        }

        private void LoadConfigFile()
        {
            PatchLogger.Log("Getting config file", PatchLogger.LogType.General);

            ResetBasicCacheOnNextStart = Config.Bind("General",
                "Reset to Basic Cache on Next Start",
                false,
                "If true, resets the cache from a basic starting cache on the next start of H3VR. MagazinePatcher will then cache any items that are missing from it. This setting will always be set back to false after startup.");

            ResetXLCacheOnNextStart = Config.Bind("General",
                "Reset to XL Cache on Next Start",
                false,
                "Experimental. If true, resets the cache from the XL starting cache on the next start of H3VR. This is a large cache made from many available mods. This setting will always be set back to false after startup.");

            DeleteCacheOnNextStart = Config.Bind("General",
                "Delete Cache on Next Start",
                false,
                "Nuclear option. If true, deletes the cache on the next start of H3VR. The cache will be built from scratch. This takes the most time and the most RAM to finish. This setting will always be set back to false after startup.");

            EnableLogging = Config.Bind("Debug",
                "EnableLogging",
                true,
                "Set to true to enable logging");

            LogDebugInfo = Config.Bind("Debug",
                "LogDebugInfo",
                false,
                "If true, logs extra debugging info");

            Config.SettingChanged += ConfigSettingChanged;

            PatchLogger.AllowLogging = EnableLogging.Value;
            PatchLogger.LogDebug = LogDebugInfo.Value;
        }

        private void ConfigSettingChanged(object sender, SettingChangedEventArgs e)
        {
            // Only one of these can be active at a time
            if (e.ChangedSetting == ResetBasicCacheOnNextStart)
            {
                if (ResetBasicCacheOnNextStart.Value)
                {
                    ResetXLCacheOnNextStart.Value = false;
                    DeleteCacheOnNextStart.Value = false;
                }
            }
            else if (e.ChangedSetting == ResetXLCacheOnNextStart)
            {
                if (ResetXLCacheOnNextStart.Value)
                {
                    ResetBasicCacheOnNextStart.Value = false;
                    DeleteCacheOnNextStart.Value = false;
                }
            }
            else if (e.ChangedSetting == DeleteCacheOnNextStart)
            {
                if (DeleteCacheOnNextStart.Value)
                {
                    ResetBasicCacheOnNextStart.Value = false;
                    ResetXLCacheOnNextStart.Value = false;
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
                if (string.IsNullOrEmpty(BlacklistPath) || !File.Exists(BlacklistPath))
                {
                    PatchLogger.LogError("Failed to load magazine blacklist! Creating new one!");
                    return CreateNewBlacklist();
                }

                // Read the blacklist
                string blacklistString = File.ReadAllText(BlacklistPath);

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

            StreamWriter sw = File.CreateText(BlacklistPath);
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
            PatchLogger.Log("Patching has started", PatchLogger.LogType.General);

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
                PatchLogger.Log($"[{DateTime.Now:HH:mm:ss}] Caching started!", PatchLogger.LogType.General);
                PatchLogger.Log("Building new magazine cache -- This may take a while!", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Started -- This may take a while!");

                // Create lists of each category of item
                List<FVRObject> magazines = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Magazine];
                List<FVRObject> clips = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Clip];
                List<FVRObject> speedloaders = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.SpeedLoader];
                List<FVRObject> bullets = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Cartridge];
                List<FVRObject> firearms = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm];
                int totalObjects = magazines.Count + clips.Count + bullets.Count + speedloaders.Count + firearms.Count;
                int progress = 0;

                // Loop through all magazines and build a list of magazine components
                PatchLogger.Log("Loading all magazines", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Magazines");

                AnvilCallback<GameObject> gameObjectCallback;
                DateTime start = DateTime.Now;

                foreach (FVRObject magazine in magazines)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log($"-- {(int)(((float)progress) / totalObjects * 100)}% --", PatchLogger.LogType.General);
                    }

                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.999f));
                    progress++;

                    LastTouchedItem = magazine.ItemID;

                    // If this magazine isn't cached, then we should store its data
                    if (!CompatibleMagazineCache.Instance.Magazines.Contains(magazine.ItemID))
                    {
                        gameObjectCallback = magazine.GetGameObjectAsync();
                        yield return gameObjectCallback;

                        if (magazine.GetGameObject() == null)
                        {
                            PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {magazine.ItemID}");
                            continue;
                        }

                        FVRFireArmMagazine magComp = magazine.GetGameObject().GetComponent<FVRFireArmMagazine>();

                        if (magComp != null)
                        {
                            if (magComp.ObjectWrapper == null)
                            {
                                PatchLogger.LogWarning($"Object was found to have no ObjectWrapper assigned! ItemID: {magazine.ItemID}");
                                continue;
                            }

                            CompatibleMagazineCache.Instance.AddMagazineData(magComp);
                        }

                        CompatibleMagazineCache.Instance.Magazines.Add(magazine.ItemID);
                    }
                }

                // Loop through all clips and build a list of stripper clip components
                PatchLogger.Log("Loading all clips", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Clips");

                foreach (FVRObject clip in clips)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log($"-- {(int)(((float)progress) / totalObjects * 100)}% --", PatchLogger.LogType.General);
                    }

                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.999f));
                    progress++;

                    LastTouchedItem = clip.ItemID;

                    // If this clip isn't cached, then we should store its data
                    if (!CompatibleMagazineCache.Instance.Clips.Contains(clip.ItemID))
                    {
                        gameObjectCallback = clip.GetGameObjectAsync();
                        yield return gameObjectCallback;

                        if (clip.GetGameObject() == null)
                        {
                            PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {clip.ItemID}");
                            continue;
                        }

                        FVRFireArmClip clipComp = clip.GetGameObject().GetComponent<FVRFireArmClip>();

                        if (clipComp != null)
                        {
                            if (clipComp.ObjectWrapper == null)
                            {
                                PatchLogger.LogWarning($"Object was found to have no ObjectWrapper assigned! ItemID: {clip.ItemID}");
                                continue;
                            }

                            CompatibleMagazineCache.Instance.AddClipData(clipComp);
                        }

                        CompatibleMagazineCache.Instance.Clips.Add(clip.ItemID);
                    }
                }

                // Loop through all clips and build a list of speedloader components
                PatchLogger.Log("Loading all speedloaders", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Speedloaders");

                foreach (FVRObject speedloader in speedloaders)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log($"-- {(int)(((float)progress) / totalObjects * 100)}% --", PatchLogger.LogType.General);
                    }

                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.999f));
                    progress++;

                    LastTouchedItem = speedloader.ItemID;

                    // If this speedloader isn't cached, then we should store its data
                    if (!CompatibleMagazineCache.Instance.SpeedLoaders.Contains(speedloader.ItemID))
                    {
                        gameObjectCallback = speedloader.GetGameObjectAsync();
                        yield return gameObjectCallback;

                        if (speedloader.GetGameObject() == null)
                        {
                            PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {speedloader.ItemID}");
                            continue;
                        }

                        Speedloader speedloaderComp = speedloader.GetGameObject().GetComponent<Speedloader>();
                        if (speedloaderComp != null)
                        {
                            if (speedloaderComp.ObjectWrapper == null)
                            {
                                PatchLogger.LogWarning($"Object was found to have no ObjectWrapper assigned! ItemID: {speedloader.ItemID}");
                                continue;
                            }

                            CompatibleMagazineCache.Instance.AddSpeedLoaderData(speedloaderComp);
                        }

                        CompatibleMagazineCache.Instance.SpeedLoaders.Add(speedloader.ItemID);
                    }
                }

                // Loop through all bullets and build a list of bullet components
                PatchLogger.Log("Loading all bullets", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Bullets");

                foreach (FVRObject bullet in bullets)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log($"-- {(int)(((float)progress) / totalObjects * 100)}% --", PatchLogger.LogType.General);
                    }

                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.999f));
                    progress++;

                    LastTouchedItem = bullet.ItemID;

                    // If this bullet isn't cached, then we should store its data
                    if (!CompatibleMagazineCache.Instance.Bullets.Contains(bullet.ItemID))
                    {
                        gameObjectCallback = bullet.GetGameObjectAsync();
                        yield return gameObjectCallback;

                        if (bullet.GetGameObject() == null)
                        {
                            PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {bullet.ItemID}");
                            continue;
                        }


                        FVRFireArmRound bulletComp = bullet.GetGameObject().GetComponent<FVRFireArmRound>();

                        if (bulletComp != null)
                        {
                            if (bulletComp.ObjectWrapper == null)
                            {
                                PatchLogger.LogWarning($"Object was found to have no ObjectWrapper assigned! ItemID: {bullet.ItemID}");
                                continue;
                            }

                            CompatibleMagazineCache.Instance.AddBulletData(bulletComp);
                        }

                        CompatibleMagazineCache.Instance.Bullets.Add(bullet.ItemID);
                    }
                }

                // Load all firearms into the cache
                PatchLogger.Log("Loading all firearms", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Firearms");
                List<string> skipList = [];

                foreach (FVRObject firearm in firearms)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log($"-- {(int)(((float)progress) / totalObjects * 100)}% --", PatchLogger.LogType.General);
                    }

                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.999f));
                    progress++;

                    LastTouchedItem = firearm.ItemID;

                    // Some muzzle loaded vanilla guns should be skipped
                    if (!firearm.IsModContent && firearm.TagFirearmAction == FVRObject.OTagFirearmAction.OpenBreach && firearm.TagFirearmFeedOption.Contains(FVRObject.OTagFirearmFeedOption.BreachLoad))
                        skipList.Add(firearm.ItemID);

                    // If this firearm isn't cached, then we should store its data
                    if (!CompatibleMagazineCache.Instance.Firearms.Contains(firearm.ItemID))
                    {
                        if (!IM.OD.ContainsKey(firearm.ItemID))
                        {
                            PatchLogger.LogWarning($"Item not found in Object Dictionary! ItemID: {firearm.ItemID}");
                            continue;
                        }

                        gameObjectCallback = firearm.GetGameObjectAsync();
                        yield return gameObjectCallback;

                        if (firearm.GetGameObject() == null)
                        {
                            PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {firearm.ItemID}");
                            continue;
                        }

                        // If this firearm is valid, then we create a magazine cache entry for it
                        FVRFireArm firearmComp = firearm.GetGameObject().GetComponent<FVRFireArm>();
                        if (firearmComp != null)
                        {
                            if (firearmComp.ObjectWrapper == null)
                            {
                                PatchLogger.LogWarning($"Object was found to have no ObjectWrapper assigned! ItemID: {firearm.ItemID}");
                                continue;
                            }

                            // If it's mostly zeroes, skip it, otherwise stuff like the Graviton Beamer gets .22LR ammo
                            if (!ValidFireArm(firearmComp.RoundType, firearmComp.ClipType, firearmComp.MagazineType, firearm.MagazineCapacity))
                            {
                                PatchLogger.Log($"Firearm {firearm.DisplayName} skipped!", PatchLogger.LogType.Debug);
                                continue;
                            }

                            MagazineCacheEntry entry = new()
                            {
                                FirearmID = firearm.ItemID,
                                MagType = firearmComp.MagazineType,
                                ClipType = firearmComp.ClipType,
                                BulletType = firearmComp.RoundType
                            };

                            // Extra part that handles revolver capacity for speedloader compatibility
                            Revolver revolverComp = firearmComp.gameObject.GetComponent<Revolver>();
                            if (revolverComp != null)
                            {
                                entry.DoesUseSpeedloader = true;
                                IM.OD[entry.FirearmID].MagazineCapacity = revolverComp.Chambers.Length;
                            }

                            CompatibleMagazineCache.Instance.Entries.Add(firearm.ItemID, entry);
                        }

                        CompatibleMagazineCache.Instance.Firearms.Add(firearm.ItemID);
                    }
                }

                // Now that all relevant data is saved, we should go back through all entries and add compatible ammo objects
                PatchLogger.Log("Building Cache Entries", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Building Cache");

                foreach (MagazineCacheEntry entry in CompatibleMagazineCache.Instance.Entries.Values)
                {
                    if (!IM.OD.ContainsKey(entry.FirearmID))
                        continue;

                    if (skipList.Contains(entry.FirearmID))
                        continue;

                    LastTouchedItem = entry.FirearmID;

                    if (CompatibleMagazineCache.Instance.MagazineData.ContainsKey(entry.MagType))
                    {
                        foreach (AmmoObjectDataTemplate magazine in CompatibleMagazineCache.Instance.MagazineData[entry.MagType])
                        {
                            entry.CompatibleMagazines.Add(magazine.ObjectID);
                        }
                    }

                    if (CompatibleMagazineCache.Instance.ClipData.ContainsKey(entry.ClipType))
                    {
                        foreach (AmmoObjectDataTemplate clip in CompatibleMagazineCache.Instance.ClipData[entry.ClipType])
                        {
                            entry.CompatibleClips.Add(clip.ObjectID);
                        }
                    }

                    if (entry.DoesUseSpeedloader && CompatibleMagazineCache.Instance.SpeedLoaderData.ContainsKey(entry.BulletType))
                    {
                        foreach (AmmoObjectDataTemplate speedloader in CompatibleMagazineCache.Instance.SpeedLoaderData[entry.BulletType])
                        {
                            if (IM.OD[entry.FirearmID].MagazineCapacity == speedloader.Capacity)
                            {
                                entry.CompatibleSpeedLoaders.Add(speedloader.ObjectID);
                            }
                        }
                    }

                    if (CompatibleMagazineCache.Instance.BulletData.ContainsKey(entry.BulletType))
                    {
                        foreach (AmmoObjectDataTemplate bullet in CompatibleMagazineCache.Instance.BulletData[entry.BulletType])
                        {
                            entry.CompatibleBullets.Add(bullet.ObjectID);
                        }
                    }
                }

                // Create the cache file 
                PatchLogger.Log("Saving Data", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Saving");

                using StreamWriter sw = File.CreateText(FullCachePath);
                string cacheString = JsonConvert.SerializeObject(CompatibleMagazineCache.Instance, Formatting.Indented);
                sw.WriteLine(cacheString);
                sw.Close();

                PatchLogger.Log($"[{DateTime.Now:HH:mm:ss}] Caching finished!", PatchLogger.LogType.General);
            }

            PatchLogger.Log("Applying magazine cache to firearms", PatchLogger.LogType.General);

            ApplyMagazineCache(CompatibleMagazineCache.Instance);
            RemoveBlacklistedMagazines(CompatibleMagazineCache.BlacklistEntries);

            PatcherStatus.UpdateProgress(1);
        }

        private static bool CheckBackupCache()
        {
            // Only one of these can be set at once
            bool forceRestoreBasic = ResetBasicCacheOnNextStart.Value;
            bool forceRestoreXL = !forceRestoreBasic && ResetXLCacheOnNextStart.Value;
            bool forceDelete = !forceRestoreBasic && !forceRestoreXL && DeleteCacheOnNextStart.Value;

            // Always reset these options, whether we do them or not
            ResetBasicCacheOnNextStart.Value = false;
            ResetXLCacheOnNextStart.Value = false;
            DeleteCacheOnNextStart.Value = false;

            if (forceDelete)
            {
                PatchLogger.Log($"Deleted cache and starting from scratch!", PatchLogger.LogType.General);
                if (File.Exists(FullCachePath))
                    File.Delete(FullCachePath);

                return true;
            }

            string startingCachePath = (forceRestoreXL ? XLCachePath : BasicCachePath);

            // If starting cache doesn't exist, we can't do anything
            if (!File.Exists(startingCachePath))
            {
                PatchLogger.Log($"Starting cache is missing: {startingCachePath}!", PatchLogger.LogType.General);
                return false;
            }

            // If Full cache doesn't exist, or if Reset option has been set, copy starting cache to Full cache
            if (!File.Exists(FullCachePath) || forceRestoreBasic || forceRestoreXL)
            {
                File.Copy(startingCachePath, FullCachePath, true);
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
            if (!string.IsNullOrEmpty(FullCachePath) && File.Exists(FullCachePath))
            {
                try
                {
                    string cacheJson = File.ReadAllText(FullCachePath);
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

                    File.Delete(FullCachePath);
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

            // NOTE: You could return false immediately in here, but we don't for the sake of debugging
            foreach (string mag in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Magazine].Select(f => f.ItemID))
            {
                if (!magazineCache.Magazines.Contains(mag))
                {
                    PatchLogger.LogWarning($"Magazine not found in cache: {mag}");
                    cacheValid = false;
                }
            }

            foreach (string firearm in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm].Select(f => f.ItemID))
            {
                if (!magazineCache.Firearms.Contains(firearm))
                {
                    PatchLogger.LogWarning($"Firearm not found in cache: {firearm}");
                    cacheValid = false;
                }
            }

            foreach (string clip in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Clip].Select(f => f.ItemID))
            {
                if (!magazineCache.Clips.Contains(clip))
                {
                    PatchLogger.LogWarning($"Clip not found in cache: {clip}");
                    cacheValid = false;
                }
            }

            foreach (string bullet in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Cartridge].Select(f => f.ItemID))
            {
                if (!magazineCache.Bullets.Contains(bullet))
                {
                    PatchLogger.LogWarning($"Bullet not found in cache: {bullet}");
                    cacheValid = false;
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
