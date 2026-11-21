using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FistVR;
using OtherLoader.API.Sledge;
using UnityEngine;
using Valve.Newtonsoft.Json;

namespace MagazinePatcher;

internal static class CacheBuilder
{
    // This is an arbitrary number. If it ever gets used for an ammo type (modded or not), then change this to something else that's unused.
    private const FireArmRoundType InvalidRoundType = (FireArmRoundType)(-1);
    
    private static HashSet<FVRObject> _allMagazines;
    private static HashSet<FVRObject> _allClips;
    private static HashSet<FVRObject> _allSpeedloaders;
    private static HashSet<FVRObject> _allBullets;
    private static HashSet<FVRObject> _allFirearms;
    private static HashSet<FVRObject> _allObjects;
    private static int _allObjectsCount;
    private static int _cachedObjectsCount;
    private static HashSet<string> _firearmSkipList;
    
    public static IEnumerator Build(string savePath)
    {
        PatchLogger.Log($"[{DateTime.Now:HH:mm:ss}] Caching started!", PatchLogger.LogType.General);
        PatcherStatus.AppendCacheLog("Building new magazine cache -- This may take a while!");

        // Create lists of each category of item
        _allMagazines = [.. IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Magazine]];
        _allClips = [.. IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Clip]];
        _allSpeedloaders = [.. IM.Instance.odicTagCategory[FVRObject.ObjectCategory.SpeedLoader]];
        _allBullets = [.. IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Cartridge]];
        _allFirearms = [.. IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm]];
        _allObjects = [.. _allMagazines, .. _allClips, .. _allSpeedloaders, .. _allBullets, .. _allFirearms];
        _allObjectsCount = _allObjects.Count;
        _cachedObjectsCount = 0;
        _firearmSkipList = [];
        
        var cachedFvrObjects = new HashSet<FVRObject>();
        var rootGroups = SledgeRootObjectsResolver.ResolveRootFvrObjectGroups(SledgeSearchMode.All);
        if (rootGroups == null || rootGroups.Count == 0)
        {
            PatcherStatus.AppendCacheLog("Root groups unavailable!");
        }
        else
        {
            foreach (var group in rootGroups)
            {
                // Groups that aren't isolated should be cached at the end just to be safe
                if (!group.IsIsolated)
                    continue;
                
                var set = new HashSet<FVRObject>(group.AllFvrObjectsInThisGroup);
                yield return GoThroughSet(set, group.RootBundle.BundleID);
                foreach (var fvrObject in set)
                {
                    cachedFvrObjects.Add(fvrObject);
                    SledgeUnloader.UnloadFVRObject(fvrObject, true);
                }
            }
        }

        HashSet<FVRObject> remainingUngroupedFvrObjects = [.._allObjects];
        remainingUngroupedFvrObjects.ExceptWith(cachedFvrObjects);

        yield return GoThroughSet(remainingUngroupedFvrObjects, "Ungrouped FVRObjects");
        foreach (var fvrObject in remainingUngroupedFvrObjects)
            SledgeUnloader.UnloadFVRObject(fvrObject, true);

        // Now that all relevant data is saved, we should go back through all entries and add compatible ammo objects
        BuildCacheEntries();

        // Create the cache file 
        PatcherStatus.AppendCacheLog("Saving data");

        using StreamWriter sw = File.CreateText(savePath);
        string cacheString = JsonConvert.SerializeObject(CompatibleMagazineCache.Instance, Formatting.Indented);
        sw.WriteLine(cacheString);
        sw.Close();

        PatchLogger.Log($"[{DateTime.Now:HH:mm:ss}] Caching finished!", PatchLogger.LogType.General);
    }

    private static IEnumerator GoThroughSet(HashSet<FVRObject> set, string setName)
    {
        // Loop through all magazines and build a list of magazine components
        PatcherStatus.AppendCacheLog($"Loading all magazines from {setName}");

        DateTime start = DateTime.Now;

        foreach (FVRObject magazine in set)
        {
            if (!_allMagazines.Contains(magazine)) continue;
            
            if ((DateTime.Now - start).TotalSeconds > 2)
            {
                start = DateTime.Now;
                PatchLogger.Log($"-- {(int)(((float)_cachedObjectsCount) / _allObjectsCount * 100)}% --", PatchLogger.LogType.General);
            }

            MagazinePatcher.LastTouchedItem = magazine.ItemID;
            PatcherStatus.UpdateProgress(Mathf.Min((float)_cachedObjectsCount / _allObjectsCount, 0.999f));
            _cachedObjectsCount++;

            // If this magazine isn't cached, then we should store its data
            if (!CompatibleMagazineCache.Instance.Magazines.Contains(magazine.ItemID))
            {
                var gameObjectCallback = magazine.GetGameObjectAsync()!;
                yield return gameObjectCallback;

                var result = gameObjectCallback.Result;
                if (result == null)
                {
                    PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {magazine.ItemID}");
                    continue;
                }

                var magComp = result.GetComponent<FVRFireArmMagazine>();
                if (magComp != null)
                {
                    if (magComp.ObjectWrapper == null)
                    {
                        PatchLogger.LogWarning(
                            $"Object was found to have no ObjectWrapper assigned! ItemID: {magazine.ItemID}");
                        magComp.ObjectWrapper = magazine;
                        //continue;
                    }

                    CompatibleMagazineCache.Instance.AddMagazineData(magComp);
                }

                CompatibleMagazineCache.Instance.Magazines.Add(magazine.ItemID);
            }
        }

        // Loop through all clips and build a list of stripper clip components
        PatcherStatus.AppendCacheLog($"Loading all clips from {setName}");

        foreach (FVRObject clip in set)
        {
            if (!_allClips.Contains(clip)) continue;
            
            if ((DateTime.Now - start).TotalSeconds > 2)
            {
                start = DateTime.Now;
                PatchLogger.Log($"-- {(int)(((float)_cachedObjectsCount) / _allObjectsCount * 100)}% --", PatchLogger.LogType.General);
            }

            MagazinePatcher.LastTouchedItem = clip.ItemID;
            PatcherStatus.UpdateProgress(Mathf.Min((float)_cachedObjectsCount / _allObjectsCount, 0.999f));
            _cachedObjectsCount++;

            // If this clip isn't cached, then we should store its data
            if (!CompatibleMagazineCache.Instance.Clips.Contains(clip.ItemID))
            {
                var gameObjectCallback = clip.GetGameObjectAsync()!;
                yield return gameObjectCallback;

                var result = gameObjectCallback.Result;
                if (result == null)
                {
                    PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {clip.ItemID}");
                    continue;
                }

                var clipComp = result.GetComponent<FVRFireArmClip>();
                if (clipComp != null)
                {
                    if (clipComp.ObjectWrapper == null)
                    {
                        PatchLogger.LogWarning(
                            $"Object was found to have no ObjectWrapper assigned! ItemID: {clip.ItemID}");
                        clipComp.ObjectWrapper = clip;
                        //continue;
                    }

                    CompatibleMagazineCache.Instance.AddClipData(clipComp);
                }

                CompatibleMagazineCache.Instance.Clips.Add(clip.ItemID);
            }
        }

        // Loop through all clips and build a list of speedloader components
        PatcherStatus.AppendCacheLog($"Loading all speedloaders from {setName}");

        foreach (FVRObject speedloader in set)
        {
            if (!_allSpeedloaders.Contains(speedloader)) continue;
            
            if ((DateTime.Now - start).TotalSeconds > 2)
            {
                start = DateTime.Now;
                PatchLogger.Log($"-- {(int)(((float)_cachedObjectsCount) / _allObjectsCount * 100)}% --", PatchLogger.LogType.General);
            }

            MagazinePatcher.LastTouchedItem = speedloader.ItemID;
            PatcherStatus.UpdateProgress(Mathf.Min((float)_cachedObjectsCount / _allObjectsCount, 0.999f));
            _cachedObjectsCount++;

            // If this speedloader isn't cached, then we should store its data
            if (!CompatibleMagazineCache.Instance.SpeedLoaders.Contains(speedloader.ItemID))
            {
                var gameObjectCallback = speedloader.GetGameObjectAsync()!;
                yield return gameObjectCallback;

                var result = gameObjectCallback.Result;
                if (result == null)
                {
                    PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {speedloader.ItemID}");
                    continue;
                }

                var speedloaderComp = result.GetComponent<Speedloader>();
                if (speedloaderComp != null)
                {
                    if (speedloaderComp.ObjectWrapper == null)
                    {
                        PatchLogger.LogWarning(
                            $"Object was found to have no ObjectWrapper assigned! ItemID: {speedloader.ItemID}");
                        speedloaderComp.ObjectWrapper = speedloader;
                        //continue;
                    }

                    CompatibleMagazineCache.Instance.AddSpeedLoaderData(speedloaderComp);
                }

                CompatibleMagazineCache.Instance.SpeedLoaders.Add(speedloader.ItemID);
            }
        }

        // Loop through all bullets and build a list of bullet components
        PatcherStatus.AppendCacheLog($"Loading all bullets from {setName}");

        foreach (FVRObject bullet in set)
        {
            if (!_allBullets.Contains(bullet)) continue;
            
            if ((DateTime.Now - start).TotalSeconds > 2)
            {
                start = DateTime.Now;
                PatchLogger.Log($"-- {(int)(((float)_cachedObjectsCount) / _allObjectsCount * 100)}% --", PatchLogger.LogType.General);
            }

            MagazinePatcher.LastTouchedItem = bullet.ItemID;
            PatcherStatus.UpdateProgress(Mathf.Min((float)_cachedObjectsCount / _allObjectsCount, 0.999f));
            _cachedObjectsCount++;

            // If this bullet isn't cached, then we should store its data
            if (!CompatibleMagazineCache.Instance.Bullets.Contains(bullet.ItemID))
            {
                var gameObjectCallback = bullet.GetGameObjectAsync()!;
                yield return gameObjectCallback;

                var result = gameObjectCallback.Result;
                if (result == null)
                {
                    PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {bullet.ItemID}");
                    continue;
                }

                var bulletComp = result.GetComponent<FVRFireArmRound>();
                if (bulletComp != null)
                {
                    if (bulletComp.ObjectWrapper == null)
                    {
                        PatchLogger.LogWarning(
                            $"Object was found to have no ObjectWrapper assigned! ItemID: {bullet.ItemID}");
                        bulletComp.ObjectWrapper = bullet;
                        //continue;
                    }

                    CompatibleMagazineCache.Instance.AddBulletData(bulletComp);
                }

                CompatibleMagazineCache.Instance.Bullets.Add(bullet.ItemID);
            }
        }

        // Load all firearms into the cache
        PatcherStatus.AppendCacheLog($"Loading all firearms from {setName}");

        foreach (FVRObject firearm in set)
        {
            if (!_allFirearms.Contains(firearm)) continue;
            
            if ((DateTime.Now - start).TotalSeconds > 2)
            {
                start = DateTime.Now;
                PatchLogger.Log($"-- {(int)(((float)_cachedObjectsCount) / _allObjectsCount * 100)}% --", PatchLogger.LogType.General);
            }

            MagazinePatcher.LastTouchedItem = firearm.ItemID;
            PatcherStatus.UpdateProgress(Mathf.Min((float)_cachedObjectsCount / _allObjectsCount, 0.999f));
            _cachedObjectsCount++;

            // Some muzzle loaded vanilla guns should be skipped
            if (!firearm.IsModContent && firearm.TagFirearmAction == FVRObject.OTagFirearmAction.OpenBreach &&
                firearm.TagFirearmFeedOption.Contains(FVRObject.OTagFirearmFeedOption.BreachLoad))
                _firearmSkipList.Add(firearm.ItemID);

            // If this firearm isn't cached, then we should store its data
            if (!CompatibleMagazineCache.Instance.Firearms.Contains(firearm.ItemID))
            {
                if (!IM.OD.ContainsKey(firearm.ItemID))
                {
                    PatchLogger.LogWarning($"Item not found in Object Dictionary! ItemID: {firearm.ItemID}");
                    continue;
                }

                var gameObjectCallback = firearm.GetGameObjectAsync()!;
                yield return gameObjectCallback;

                var result = gameObjectCallback.Result;
                if (result == null)
                {
                    PatchLogger.LogWarning($"No object was found to use FVRObject! ItemID: {firearm.ItemID}");
                    continue;
                }

                // If this firearm is valid, then we create a magazine cache entry for it
                var firearmComp = result.GetComponent<FVRFireArm>();
                if (firearmComp != null)
                {
                    if (firearmComp.ObjectWrapper == null)
                    {
                        PatchLogger.LogWarning(
                            $"Object was found to have no ObjectWrapper assigned! ItemID: {firearm.ItemID}");
                        firearmComp.ObjectWrapper = firearm;
                        //continue;
                    }

                    MagazineCacheEntry entry = new()
                    {
                        FirearmID = firearm.ItemID,
                        MagType = firearmComp.MagazineType,
                        ClipType = firearmComp.ClipType,
                        BulletType = firearmComp.RoundType
                    };

                    // If it's mostly zeroes, set the round type to invalid, otherwise stuff like the Graviton Beamer gets .22LR ammo.
                    // Unfortunately, Anton didn't create a "FireArmRoundType.None", so we have to make one up.
                    if (!MagazinePatcher.ValidFireArm(firearmComp.RoundType, firearmComp.ClipType, firearmComp.MagazineType,
                            firearm.MagazineCapacity))
                    {
                        PatchLogger.Log($"Firearm {firearm.DisplayName} being set to round type {InvalidRoundType}!",
                            PatchLogger.LogType.Debug);
                        entry.BulletType = InvalidRoundType;
                    }

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
    }

    private static void BuildCacheEntries()
    {
        PatcherStatus.AppendCacheLog("Building cache entries");

        foreach (MagazineCacheEntry entry in CompatibleMagazineCache.Instance.Entries.Values)
        {
            if (!IM.OD.ContainsKey(entry.FirearmID))
                continue;

            if (_firearmSkipList.Contains(entry.FirearmID))
                continue;

            MagazinePatcher.LastTouchedItem = entry.FirearmID;

            if (entry.MagType != FireArmMagazineType.mNone &&
                CompatibleMagazineCache.Instance.MagazineData.ContainsKey(entry.MagType))
            {
                foreach (AmmoObjectDataTemplate magazine in CompatibleMagazineCache.Instance.MagazineData
                             [entry.MagType])
                {
                    entry.CompatibleMagazines.Add(magazine.ObjectID);
                }
            }

            if (entry.ClipType != FireArmClipType.None &&
                CompatibleMagazineCache.Instance.ClipData.ContainsKey(entry.ClipType))
            {
                foreach (AmmoObjectDataTemplate clip in CompatibleMagazineCache.Instance.ClipData[entry.ClipType])
                {
                    entry.CompatibleClips.Add(clip.ObjectID);
                }
            }

            if (entry.DoesUseSpeedloader &&
                CompatibleMagazineCache.Instance.SpeedLoaderData.ContainsKey(entry.BulletType))
            {
                foreach (AmmoObjectDataTemplate speedloader in CompatibleMagazineCache.Instance.SpeedLoaderData[
                             entry.BulletType])
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
    }
}