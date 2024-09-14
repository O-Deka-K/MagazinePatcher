﻿using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OldMagazinePatcherDisabler
{
    public static class Disabler
    {
        private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("Deliter");

        public static IEnumerable<string> TargetDLLs => Enumerable.Empty<string>();

        public static void Patch(AssemblyDefinition asm)
        {
            Logger.LogWarning("No DLLs should be patched, but the patch method was called. Assembly: " + asm);
        }

        // Combine any number of path strings instead of just two (e.g. CombinePaths(path1, path2, path3, path4))
        public static string CombinePaths(params string[] paths)
        {
            if (paths == null)
                return "";

            return paths.Aggregate(Path.Combine);
        }

        public static void Initialize()
        {
            string configFilePath = CombinePaths(Paths.ConfigPath, "h3vr.magazinepatcher.cfg");
            Logger.LogInfo($"Config file path: {configFilePath}");

            string fullCacheDir = CombinePaths(Paths.CachePath, "MagazinePatcher");
            if (!Directory.Exists(fullCacheDir))
                Directory.CreateDirectory(fullCacheDir);

            string fullCachePath = CombinePaths(fullCacheDir, "CachedCompatibleMags.json");
            Logger.LogInfo($"Cache path: {fullCachePath}");

            string[] directories = Directory.GetDirectories(Paths.PluginPath);

            foreach (string dir in directories)
            {
                Logger.LogInfo($"Found directory {dir}");

                if (dir.Contains("devyndamonster-MagazinePatcher"))
                {
                    string originalDllPath = CombinePaths(dir, "MagazinePatcher", "MagazinePatcher.dll");
                    string originalManifestPath = CombinePaths(dir, "MagazinePatcher", "manifest.json");
                    string originalCachePath = CombinePaths(dir, "MagazinePatcher", "CachedCompatibleMags.json");

                    // Is this the first time that this patcher has been run?
                    // The plugin config file is created the first time the new MagazinePatcher is run
                    // The original MagazinePatcher did not have a config file
                    if (!File.Exists(configFilePath))
                    {
                        if (!File.Exists(fullCachePath))
                        {
                            // Original MagazinePatcher is disabled in r2modman
                            if (File.Exists(originalDllPath + ".old"))
                            {
                                // Copy the original cache, which should also be in disabled state
                                if (File.Exists(originalCachePath + ".old"))
                                {
                                    File.Copy(originalCachePath + ".old", fullCachePath, true);
                                }
                                // If the original cache exists but isn't in disabled state somehow, copy it instead
                                else if (File.Exists(originalCachePath))
                                {
                                    File.Copy(originalCachePath, fullCachePath, true);
                                }
                            }
                            // Original MagazinePatcher is enabled, so copy the cache over
                            else if (File.Exists(originalDllPath))
                            {
                                File.Copy(originalCachePath, fullCachePath, true);
                            }
                        }
                    }

                    // Original MagazinePatcher is enabled, so rename it with .bak
                    if (File.Exists(originalDllPath))
                    {
                        if (File.Exists(originalDllPath + ".bak"))
                            File.Delete(originalDllPath + ".bak");

                        if (File.Exists(originalManifestPath + ".bak"))
                            File.Delete(originalManifestPath + ".bak");

                        File.Move(originalDllPath, originalDllPath + ".bak");
                        File.Move(originalManifestPath, originalManifestPath + ".bak");

                        Logger.LogInfo("Disabled original MagazinePatcher install. Re-enable it via reinstalling or renaming the DLL. Will break compatibility with new MagazinePatcher.");
                    }

                    break;
                }
            }
        }
    }
}