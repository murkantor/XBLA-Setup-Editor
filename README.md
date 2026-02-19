# XBLA Setup Editor

> **Disclaimer:** This tool was slop-coded with Claude Opus 4.5. If you don't like that, don't use it and feel free to make your own.

A modding tool for GoldenEye 007 XBLA (Xbox Live Arcade) that allows editing of XEX files, setup files, weapon configurations, and more.

## Features

### STR Editor
Edit STR/ADB string database files used for in-game text and localization.

### MP Weapon Sets
Edit multiplayer weapon set configurations directly in the XEX:
- Customize weapon loadouts for each multiplayer weapon set
- Add or remove weapons from sets
- Configure armour removal options

### Setup Patching
Convert and patch level setup files:
- Single level conversion using `setupconv.exe`
- Batch conversion for all solo levels
- Automatic XEX patching with converted setups
- Support for splitting setups across two XEX files (for large mods)
- MP pool overflow support for additional space

### Skies, Fog and Music
Import sky, fog, and music settings from N64 21990 files:
- Apply sky colors and fog settings per level
- Separate N64 and XBLA fog distance controls
- Apply sky colors to fog for cohesive atmospheres
- Import music track assignments
- Import level names and descriptions

### Weapon Stats
Edit weapon statistics and properties:
- Damage values
- Fire rates
- Ammo capacities
- And more weapon parameters

### XEX Extender
Extend XEX files with additional data space for larger mods.

### XDelta Patch Creation
Automatically create xdelta patches after saving:
- Single XEX patch creation
- Split XEX dual patch creation
- Easy distribution of mods without sharing full XEX files

## Requirements

- Windows with .NET 8.0 runtime
- `setupconv.exe` (place in application directory for setup patching)
- `xdelta3.exe` (place in application directory for patch creation)

## Usage

1. Load a GoldenEye XBLA XEX file using the top toolbar
2. Optionally load a 21990 file for sky/fog/music importing
3. Use the tabs to access different editing features
4. Save your modified XEX (always saves as a new file)
5. Optionally create an xdelta patch for distribution

## Building

```bash
dotnet build
```

## License

This project is licensed under the MIT License.

This distribution also includes:

- `setupconv.exe` (MIT License)
- `xdelta3.exe` (GNU GPL v2 or later)

These components are distributed as separate executables and are not linked
into this application.

See THIRD_PARTY_LICENSES.txt for details.

This tool is for personal modding use. Please respect other creators' work:
- Do not distribute setups or mods created by others without permission
- Always credit original creators when sharing modified work
- Do not claim another creator's work as your own

## Acknowledgments

Thanks to the GoldenEye modding community for their continued work preserving and enhancing this classic game.
