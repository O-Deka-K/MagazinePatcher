### [0.2.0] - 2024-09-10 - Stratum Update
- First release, based on devyndamonster-MagazinePatcher 0.1.12. 
- Updated to Stratum mod
- Added logging options.

### [0.2.1] - 2024-09-10 - Manifest Update
- Updated manifest.json to say the author is devyndamonster. TakeAndHoldTweaker won't work without it. TNHFramework works fine with it.

### [0.2.2] - 2024-09-10 - Documentation Update
- Previous changelog was incorrect. The manifest.json didn't have to be updated and it wasn't. The plugin itself had to tell BepInEx that it was the same name as the original version.

### [0.2.3] - 2024-09-10 - Dependency Update
- Removed Deli as a dependency.

### [0.2.4] - 2024-09-10 - Structure Update
- Had to move files around. I thought I had tested it and it was working, but apparently not. Sorry for any trouble.

### [0.3.0] - 2024-09-13 - Cache Options Update
- Moved cache from plugin directory to BepInEx cache directory (see above for location). This prevents the cache from being deleted whenever this mod is updated.
- Added options to reset cache to basic or XL starting cache, or to delete it entirely.
- Added OldMagazinePatcherDisabler. This disables the original version of MagazinePatcher if you have it enabled.

### [0.3.1] - 2024-09-14 - Credits Update
- Added credit to APintOfGravy

### [0.3.2] - 2024-11-08 - MagazineType/ClipType Fix
- Disallow MagazineType 0 and ClipType 0. This fixes things like ammo boxes from mods being spawned with guns.

### [0.3.3] - 2025-03-26 - Caching Tweaks and Blacklist Additions
- If an item is missing an ObjectWrapper value, assign it rather than skip it.
- Patch firearms that don't have any ammo (e.g. Graviton Beamer) so that .22 LR ammo doesn't spawn with it.
- Don't invalidate the cache if an item is missing from the cache, but the item is bugged and can't be loaded.
- AMagII and Modul_1911 9mm incorrectly share the same magazine type, so these have been separated via blacklist.
- SW41 and Modul_1911 10mm incorrectly share the same magazine type, so these have been separated via blacklist.
- Defender 45 mag causes clipping issues in some 1911s, so it's been blacklisted from those guns.
