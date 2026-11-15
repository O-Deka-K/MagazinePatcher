using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FistVR;
using UnityEngine;
using Valve.Newtonsoft.Json;

namespace MagazinePatcher;

internal static class CacheBuilder
{
    // This is an arbitrary number. If it ever gets used for an ammo type (modded or not), then change this to something else that's unused.
    private const FireArmRoundType InvalidRoundType = (FireArmRoundType)(-1);
    
    public static IEnumerable Build(string savePath)
    {
        PatchLogger.Log($"[{DateTime.Now:HH:mm:ss}] Caching started!", PatchLogger.LogType.General);
        PatcherStatus.AppendCacheLog("Building new magazine cache -- This may take a while!");

        // Create lists of each category of item
        List<FVRObject> magazines = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Magazine];
        List<FVRObject> clips = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Clip];
        List<FVRObject> speedloaders = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.SpeedLoader];
        List<FVRObject> bullets = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Cartridge];
        List<FVRObject> firearms = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm];
        int totalObjects = magazines.Count + clips.Count + bullets.Count + speedloaders.Count + firearms.Count;
        int progress = 0;

        // Loop through all magazines and build a list of magazine components
        PatcherStatus.AppendCacheLog("Loading all magazines");

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

            MagazinePatcher.LastTouchedItem = magazine.ItemID;

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
                        //magComp.ObjectWrapper = magazine;
                        //continue;
                    }

                    CompatibleMagazineCache.Instance.AddMagazineData(magComp);
                    OtherLoader.Sledge.SledgeUnloader.UnloadFVRObject(magazine.ItemID);
                }

                CompatibleMagazineCache.Instance.Magazines.Add(magazine.ItemID);
            }
        }

        // Loop through all clips and build a list of stripper clip components
        PatcherStatus.AppendCacheLog("Loading all clips");

        foreach (FVRObject clip in clips)
        {
            if ((DateTime.Now - start).TotalSeconds > 2)
            {
                start = DateTime.Now;
                PatchLogger.Log($"-- {(int)(((float)progress) / totalObjects * 100)}% --", PatchLogger.LogType.General);
            }

            PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.999f));
            progress++;

            MagazinePatcher.LastTouchedItem = clip.ItemID;

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
                        //clipComp.ObjectWrapper = clip;
                        //continue;
                    }

                    CompatibleMagazineCache.Instance.AddClipData(clipComp);
                    OtherLoader.Sledge.SledgeUnloader.UnloadFVRObject(clip.ItemID);
                }

                CompatibleMagazineCache.Instance.Clips.Add(clip.ItemID);
            }
        }

        // Loop through all clips and build a list of speedloader components
        PatcherStatus.AppendCacheLog("Loading all speedloaders");

        foreach (FVRObject speedloader in speedloaders)
        {
            if ((DateTime.Now - start).TotalSeconds > 2)
            {
                start = DateTime.Now;
                PatchLogger.Log($"-- {(int)(((float)progress) / totalObjects * 100)}% --", PatchLogger.LogType.General);
            }

            PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.999f));
            progress++;

            MagazinePatcher.LastTouchedItem = speedloader.ItemID;

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
                        //speedloaderComp.ObjectWrapper = speedloader;
                        //continue;
                    }

                    CompatibleMagazineCache.Instance.AddSpeedLoaderData(speedloaderComp);
                    OtherLoader.Sledge.SledgeUnloader.UnloadFVRObject(speedloader.ItemID);
                }

                CompatibleMagazineCache.Instance.SpeedLoaders.Add(speedloader.ItemID);
            }
        }

        // Loop through all bullets and build a list of bullet components
        PatcherStatus.AppendCacheLog("Loading all bullets");

        foreach (FVRObject bullet in bullets)
        {
            if ((DateTime.Now - start).TotalSeconds > 2)
            {
                start = DateTime.Now;
                PatchLogger.Log($"-- {(int)(((float)progress) / totalObjects * 100)}% --", PatchLogger.LogType.General);
            }

            PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.999f));
            progress++;

            MagazinePatcher.LastTouchedItem = bullet.ItemID;

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
                        //bulletComp.ObjectWrapper = bullet;
                        //continue;
                    }

                    CompatibleMagazineCache.Instance.AddBulletData(bulletComp);
                    OtherLoader.Sledge.SledgeUnloader.UnloadFVRObject(bullet.ItemID);
                }

                CompatibleMagazineCache.Instance.Bullets.Add(bullet.ItemID);
            }
        }

        // Load all firearms into the cache
        PatcherStatus.AppendCacheLog("Loading all firearms");
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

            MagazinePatcher.LastTouchedItem = firearm.ItemID;

            // Some muzzle loaded vanilla guns should be skipped
            if (!firearm.IsModContent && firearm.TagFirearmAction == FVRObject.OTagFirearmAction.OpenBreach &&
                firearm.TagFirearmFeedOption.Contains(FVRObject.OTagFirearmFeedOption.BreachLoad))
                skipList.Add(firearm.ItemID);

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
                        //firearmComp.ObjectWrapper = firearm;
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
                    OtherLoader.Sledge.SledgeUnloader.UnloadFVRObject(firearm.ItemID);
                }

                CompatibleMagazineCache.Instance.Firearms.Add(firearm.ItemID);
            }
        }

        // Now that all relevant data is saved, we should go back through all entries and add compatible ammo objects
        PatcherStatus.AppendCacheLog("Building cache entries");

        foreach (MagazineCacheEntry entry in CompatibleMagazineCache.Instance.Entries.Values)
        {
            if (!IM.OD.ContainsKey(entry.FirearmID))
                continue;

            if (skipList.Contains(entry.FirearmID))
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

        // Create the cache file 
        PatcherStatus.AppendCacheLog("Saving data");

        using StreamWriter sw = File.CreateText(savePath);
        string cacheString = JsonConvert.SerializeObject(CompatibleMagazineCache.Instance, Formatting.Indented);
        sw.WriteLine(cacheString);
        sw.Close();

        PatchLogger.Log($"[{DateTime.Now:HH:mm:ss}] Caching finished!", PatchLogger.LogType.General);
    }
}