# BrickVault 1.0.0
<p align="center">
	<img src="Images/brickvaultico.png" width="250">
</p>

A cross-platform archive extractor for *all* LEGO TTGames

## Dependencies
For both Windows and macOS, the **.NET Runtime 8** is required which can be found [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

## Compatibility
This tool has been extensively tested against all games ranging from the original LEGO Star Wars I up to the latest LEGO Harry Potter Collection. Unless stated in the issues list below, assume that the tool can successfully parse and extract all files from that game/archive.

### Issues
- LEGO Harry Potter Collection
	1. File paths are not included in the archive for this game. To ensure that files extract with file names, you will need to place a **.list** file in the tool's directory where the file contains a list of file paths in the GAME archive.
	2. The archives can only be extracted on Windows: The archives are compressed using LZHAM, a binary is provided with the application to successfully extract these files, this binary only works on windows.
- LEGO Star Wars: The Skywalker Saga
	1. The archives can only be extracted on Windows: The archives are compressed using Oodle, you must place the oo2core_8_win64.dll binary from your game in the tool's directory. **This will not work on macOS**.

## Getting started
View the getting started guide [here](Support/GettingStarted.md).

## Features
- Compared to other tools, BrickVault is comparably faster taking less than a few minutes to extract the largest archives.
- BrickVault has a higher accuracy than other tools, as it ensures that all paths are correctly determined. This is down to testing a wide-range of archives.
- Ability to open multiple archives in a folder at once.
- Multi-threaded support: Whilst not implemented for singular archives due to filesystem throughput concerns, the tool can extract multiple archives at once using individual threads for each archive.
- Ability to set a custom output directory when holding down the SHIFT key.
- Nearly complete platform-independence. Other than the two games listed above, all archives can be extracted exactly the same on both Windows and macOS.