# SteamWorkshopUnsub

CLI tool for listing and unsubscribing from Steam Workshop items. It checks both Steam subscriptions
and local ACF entries, which helps find items that are not shown as regular Workshop subscriptions.

## Features

- **List items**: Preview the items, titles and sources without changing anything.
- **Local cleanup**: Removes item-specific Workshop files and references after unsubscribing.
- **Multiple libraries**: Searches every Steam library registered in `libraryfolders.vdf`.
- **Windows and Linux**: Supports x64 installations of Steam on both systems.

## Important Information

Steam must be running with the correct account logged in.

Without `--list`, the tool lets you choose the items before asking for confirmation. All items are
selected by default. Use the arrow keys to navigate, Space to select or deselect an item, and Enter
to continue. Toggle the `All items` entry to select or deselect the entire list at once. It is a
good idea to run with `--list` first and check the detected items.

Steam may subscribe again to required items, such as official controller configurations. Restart
Steam after making changes so its local configuration can be synchronized.

## Installation

1. Download the latest [release](https://github.com/Braasileiro/SteamWorkshopUnsub/releases/latest)
   for your system.
2. Extract all files to the same folder. The Steamworks native library must remain beside the
   executable.
3. Open a terminal in that folder and run the tool with the game's Steam AppID.

## Usage

```text
SteamWorkshopUnsub <SteamAppId> [--list]
```

The examples below use `SteamWorkshopUnsub` for readability. Run it as `./SteamWorkshopUnsub` on
Linux or `.\SteamWorkshopUnsub.exe` on Windows when the executable is in the current folder. Replace
`APP_ID` with the Steam AppID of the game you want to manage.

List all detected items without removing anything:

```bash
SteamWorkshopUnsub APP_ID --list
```

Run without `--list` to review the detected items, choose which ones to remove, and receive the
removal confirmation:

```bash
SteamWorkshopUnsub APP_ID
```

If Steam is installed in a location the tool cannot detect, set `STEAM_PATH` to the main Steam
directory before running it.

## Building

The project requires the .NET 10 SDK.

```bash
dotnet build SteamWorkshopUnsub.slnx
dotnet run --project src/SteamWorkshopUnsub.csproj -- APP_ID --list
```

To create a self-contained build for the current system:

```bash
dotnet publish src/SteamWorkshopUnsub.csproj --configuration Release --property:PublishProfile=Bundled
```

## References

- [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET)
- [Steamworks API](https://partner.steamgames.com/doc/sdk/api)
- [Spectre.Console](https://github.com/spectreconsole/spectre.console)
