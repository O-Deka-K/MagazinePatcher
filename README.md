# MagazinePatcher

#### Better Magazine Spawning!

MagazinePatcher is an H3VR mod that assigns more magazines to compatible firearms. 

**Did You Know:** Normally, magazines and clips are assigned to firearms by hand in H3VR?

This is not ideal, as some firearms can only spawn with one or two magazines, when it ***could*** spawn with several! This mod aims to fix that, by assigning compatible magazines programatically.

## Compatibility

This is a continuation of MagazinePatcher by devyndamonster, since Devyn has retired from H3VR. It's meant to replace the original MagazinePatcher entirely. The code has been updated from Deli to Stratum, which fixes the bug where the TNH lobby gets stuck on "Caching Items".

**Disable the original MagazinePatcher** and install this one instead. You don't need the original. Running both at the same time will cause problems.

## Why should you use MagazinePatcher?
- It allows modded weapons to spawn with magazines
- It adds variety to magazine drops in TNH
- It provides important datastructures which other mods can use to get compatible magazines

## Comparisons with vanilla

#### Magazines that can spawn in vanilla
![Vanilla](https://i.imgur.com/BjJHrSa.jpg)

#### Magazines that can spawn with MagazinePatcher
![Patched](https://i.imgur.com/Eb0zFme.jpg)

## Installation
1. Install [r2modman](https://thunderstore.io/c/h3vr/p/ebkr/r2modman/) (mod manager) and set it up for H3VR.
2. Install [Stratum](https://thunderstore.io/c/h3vr/p/Stratum/Stratum/) and any related dependencies using r2modman.
3. Install [Otherloader](https://thunderstore.io/c/h3vr/p/devyndamonster/OtherLoader/) and any related dependencies using r2modman.
4. **IMPORTANT:** If you have `devyndamonster-MagazinePatcher`, then disable it! In r2modman, press the button **Disable devyndamonster-MagazinePatcher only** after you click the disable switch.
5. Download `ODekaK-MagazinePatcher-X.X.X.zip` and import as a local mod (Settings > Profile > Import local mod), or install it from Thunderstore if it's available.

## Caching Tips

This mod builds a cache of all firearms, magazines, clips, speedloaders and ammo. It saves it to a file so that it can be loaded every time you start the game. When you add new mods, they will be added to the cache. The cache is located in your r2modman profile under:

`\H3VR\profiles\<profile_name>\BepInEx\plugins\ODekaK-MagazinePatcher\data\CachedCompatibleMags.json`

When you start H3VR for the first time after installing MagazinePatcher, it will build the cache. This takes some time. You can go to the TNH lobby and view the progress above the character selection screen.

If you have a whole lot of mods, then the caching process can run out of memory and possibly crash the game. Mods like ModulAK and ModulAR are especially large. If this happens, then you can build the cache incrementally instead. Here's how:

1. Make sure to start with the version of **CachedCompatibleMags.json** that comes with this mod. It has all of the vanilla items already loaded.
2. In r2modman, disable at least half of your guns or other mods that contain items.
3. Start a modded game, go to the TNH lobby, and wait for it to cache.
4. If it succeeds, then close the game, enable more mods, and repeat from step 3 until done. You do NOT need to disable mods that are already done.
5. If it fails, the close the game, disable more mods, and repeat from step 3.
 
For step 1, you _can_ delete **CachedCompatibleMags.json** completely, but this make may things worse, as it has more items to load. If you need to delete it, then I recommend disabling ALL of your mods, except for the ones needed for MagazinePatcher itself. Run it once to cache all of the vanilla items before enabling mods. **Note:** Do NOT "Start vanilla", as MagazinePatcher won't run at all.

## Changelog

### [0.2.0] - 2024-09-10 - Stratum Update
- Updated to Stratum mod
- Added logging options.

## Credits

devyndamonster - For creating this mod and sharing it on GitHub
