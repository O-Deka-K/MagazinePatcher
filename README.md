# MagazinePatcher

#### Better Magazine Spawning!

MagazinePatcher is an H3VR mod that assigns more magazines to compatible firearms. 

**Did You Know:** Normally, magazines and clips are assigned to firearms by hand in H3VR?

This is not ideal, as some firearms can only spawn with one or two magazines, when it ***could*** spawn with several! This mod aims to fix that, by assigning compatible magazines programatically.

## Compatibility

This is a continuation of MagazinePatcher by devyndamonster, since Devyn has retired from H3VR. It's meant to replace the original MagazinePatcher entirely. The code has been updated from Deli to Stratum, which fixes the bug where the TNH lobby gets stuck on "Caching Items".

This mod now has a patcher that **disables the original MagazinePatcher** if you have it installed. It renames the DLL and manifest with the extension .bak. You can disable or uninstall the original MagazinePatcher in r2modman if you like.

Yes, you _can_ use **CachedCompatibleMags.json** from the original version if you want.

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
4. If you have `devyndamonster-MagazinePatcher`, then you can disable it. In r2modman, press the button "**Disable devyndamonster-MagazinePatcher only**" after you click the disable switch.
5. Install [MagazinePatcher](https://thunderstore.io/c/h3vr/p/ODekaK/MagazinePatcher/) using r2modman, or download `ODekaK-MagazinePatcher-X.X.X.zip` and import as a local mod (Settings > Profile > Import local mod).

## Caching Tips

This mod builds a cache of all firearms, magazines, clips, speedloaders and ammo. It saves it to a file so that it can be loaded every time you start the game. When you add new mods, they will be added to the cache. The cache is located in your r2modman profile under:

`\H3VR\profiles\<profile_name>\BepInEx\cache\MagazinePatcher\CachedCompatibleMags.json`

When you start H3VR for the first time after installing MagazinePatcher, it will build the cache. This takes some time. You can go to the TNH lobby and view the progress above the character selection screen.

If you have a whole lot of mods, then the caching process can run out of memory and possibly crash the game. Mods like ModulAK and ModulAR are especially large. If this happens, then **try the XL Cache option below**.

If that doesn't work for you for some reason, then you can build the cache incrementally. Here's how:

1. In r2modman, disable at least half of your guns or other mods that contain items.
2. Start a modded game, go to the TNH lobby, and wait for it to cache.
3. If it succeeds, then close the game, enable more mods, and repeat from step 2 until done. You do NOT need to disable mods that are already done.
4. If it fails, the close the game, disable more mods, and repeat from step 2.
5. If the game is chugging when it's done, then you should restart H3VR one more time.
 
## New Features/Options

From outside of H3VR, you can go to r2modman Config Editor > `BepInEx\config\h3vr.magazinepatcher.cfg` to edit these.

From within H3VR, you can go to wrist menu > Custom Buttons > Spawn Mod Panel > Plugins > MagazinePatcher.

The following three options are mutually exclusive, meaning that only one can be applied at a time.

**Reset to Basic Cache on Next Start:** This option resets the cache from a basic starting cache on the next start on H3VR. MagazinePatcher will then cache any items that are missing from it. This setting will always be set back to false after startup.

**Reset to XL Cache on Next Start:** Experimental. This option resets the cache from the XL starting cache on the next start of H3VR. This is a large cache made from many available mods. This setting will always be set back to false after startup.

**Delete Cache on Next Start:** Nuclear option. This option deletes the cache on the next start of H3VR. The cache will be built from scratch. This takes the most time and the most RAM to finish. This setting will always be set back to false after startup.

The starting cache is used as a starting point on the first run of MagazinePatcher. MagazinePatcher will cache items from any mods that are not currently in the cache. It will update whenever you add new mods, and items will never be removed from it unless you reset it or delete it.

The default starting cache currently has all of the vanilla items from Update 113. The XL starting cache was donated by **42nfl19** and includes items from a lot of mods!

## Credits
devyndamonster - For creating this mod and sharing it on GitHub

APintOfGravy - For the OldTweakerDisabler code, which the OldMagazinePatcherDisabler is based on, and for the techinique of impersonating the old MagazinePatcher so that TNHTweaker doesn't have a tantrum.

42nfl19 - For donating the XL starting cache