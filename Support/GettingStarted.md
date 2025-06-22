# Getting started

## Installing BrickVault

1. To begin, download the latest release of the GUI version of BrickVault - **BrickVaultApp** - from [here](https://github.com/connorh315/BrickVault/releases) as well as the **.NET Runtime 8** which can be found [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
2. Install the .NET Runtime 8 and extract the BrickVaultApp.zip
> [!WARNING]
> If you wish to extract files from LEGO Star Wars: The Skywalker Saga (<ins>and you're on a Windows PC</ins>), then you must copy the **oo2core_8_win64.dll** file into the BrickVaultApp directory.
>
> If you wish to extract files from LEGO Harry Potter Collection (<ins>and you're on a Windows PC</ins>), then you must place a ".list" file into the BrickVaultApp directory.
3. Open BrickVaultApp

## Using BrickVault

The most common use case is to extract a games' entire collection of archives, so that you can then install custom mods by replacing some of the crucial files. To do this:

1. Click on the "File" button in the menu at the top of the tool
2. Click on the "Open Folder" button and select the directory that contains the .DAT files you wish to extract.
3. Once the tool has opened the .DAT files, click on the "Extract" button in the menu and then click on "Extract All".
4. Wait for the tool to have extracted all the files.
5. Go to the game folder, and check that new folders/files have been created, if there are no errors from BrickVault, this means that the game has extracted successfully.
6. Create a new folder called "backupdatfiles" in the game directory, and place the original .DAT archives from the game in there so that you have them as a backup should anything go wrong when modding. This step is crucial to ensure that the game does not continue to use the original archives, rather than the extracted files.

> [!IMPORTANT]
> Please note that newer games require a "patch" to be applied to the executable to acknowledge the extracted files. 

## Finding specific files

When searching for files, enter the name of the file you're looking to extract into the search box at the top, the tool will then list all the file paths that match that name. You can then click/ctrl+click items you wish to extract, or alternatively double-click on a file to view it's location in the archive.

## Extracting to a specific location

To extract to a specific location, hold down the SHIFT key before clicking on either the "Extract selection" or "Extract all files" buttons.