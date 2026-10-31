using Spectre.Console;
using SteamWorkshopUnsub.Steam;

namespace SteamWorkshopUnsub.Workshop;

internal static class WorkshopStorage
{
    public static IReadOnlyList<string> FindAcfPaths(string appId, IEnumerable<string> libraryPaths)
    {
        return libraryPaths
            .Select(path => Path.Combine(path, "steamapps", "workshop", $"appworkshop_{appId}.acf"))
            .Where(File.Exists)
            .ToList();
    }

    public static List<ulong> ReadItemIds(IEnumerable<string> acfPaths)
    {
        // ACF entries are kept as a second source because Steam-managed items,
        // such as controller configurations, may not be normal subscriptions.
        return acfPaths
            .SelectMany(ReadItemIdsFromAcf)
            .Distinct()
            .ToList();
    }

    private static IEnumerable<ulong> ReadItemIdsFromAcf(string acfPath)
    {
        VdfObject? installedItems = VdfFile.Load(acfPath)
            .FindObject("AppWorkshop")?
            .FindObject("WorkshopItemsInstalled");

        if (installedItems is null)
        {
            return [];
        }

        return installedItems.Entries
            .Where(entry => entry.IsObject && entry.Children?.FindValue("manifest") is not null)
            .Select(entry => ulong.TryParse(entry.Key, out ulong id) ? id : 0)
            .Where(id => id != 0);
    }

    public static void RemoveItemFromAcf(ulong itemId, IEnumerable<string> acfPaths)
    {
        foreach (string acfPath in acfPaths.Where(File.Exists))
        {
            TryModify(() => RemoveItemFromAcfFile(itemId, acfPath));
        }
    }

    private static void RemoveItemFromAcfFile(ulong itemId, string acfPath)
    {
        VdfObject document = VdfFile.Load(acfPath);
        VdfObject? appWorkshop = document.FindObject("AppWorkshop");
        bool changed = appWorkshop?.FindObject("WorkshopItemsInstalled")?.Remove(itemId.ToString()) == true;
        changed |= appWorkshop?.FindObject("WorkshopItemDetails")?.Remove(itemId.ToString()) == true;

        if (changed)
        {
            VdfFile.SaveAtomic(acfPath, document);
        }
    }

    public static void RemoveItemFromConsumedVdf(string appId, ulong itemId, string userUgcPath)
    {
        string vdfPath = Path.Combine(userUgcPath, "consumed.vdf");
        if (!File.Exists(vdfPath))
        {
            return;
        }

        TryModify(() =>
        {
            VdfObject document = VdfFile.Load(vdfPath);
            // consumed.vdf can nest each AppID at different depths depending
            // on the Steam client version, so the tree is searched recursively.
            List<VdfObject> appEntries = document.Descendants(appId)
                .Select(entry => entry.Children)
                .Where(children => children is not null)
                .Cast<VdfObject>()
                .ToList();

            bool changed = false;
            foreach (VdfObject app in appEntries)
            {
                if (!app.Entries.Any(entry => ulong.TryParse(entry.Value, out ulong id) && id == itemId))
                {
                    continue;
                }

                List<string> remainingIds = app.Entries
                    .Where(entry => ulong.TryParse(entry.Value, out ulong id) && id != itemId)
                    .Select(entry => entry.Value!)
                    .ToList();
                List<VdfEntry> otherEntries = app.Entries
                    .Where(entry => !ulong.TryParse(entry.Value, out _))
                    .ToList();

                app.Entries.Clear();
                app.Entries.AddRange(remainingIds.Select((id, index) => new VdfEntry(index.ToString(), id, null)));
                app.Entries.AddRange(otherEntries);
                changed = true;
            }

            if (changed)
            {
                VdfFile.SaveAtomic(vdfPath, document);
            }
        });
    }

    public static void DeleteItemData(
        string appId,
        ulong itemId,
        IEnumerable<string> libraryPaths,
        string userUgcPath)
    {
        string itemIdText = itemId.ToString();

        foreach (string libraryPath in libraryPaths)
        {
            string workshopPath = Path.Combine(libraryPath, "steamapps", "workshop");

            // Workshop caches have varied between direct item folders and
            // AppID-scoped folders, so both safe, item-specific forms are checked.
            DeleteCandidates(workshopPath, appId, itemIdText, "content", "downloads", "temp");
        }

        // Steam Input also keeps per-user UGC caches outside steamapps/workshop.
        DeleteCandidates(userUgcPath, appId, itemIdText, "download", "referenced", "temp");
    }

    private static void TryModify(Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLine($"[red]Unhandled exception:[/] {Markup.Escape(exception.Message)}");
        }
    }

    private static void DeleteCandidates(
        string root,
        string appId,
        string itemId,
        params string[] categories)
    {
        foreach (string category in categories)
        {
            string categoryPath = Path.Combine(root, category);
            string appPath = Path.Combine(categoryPath, appId);
            TryDeletePath(Path.Combine(appPath, itemId));
            TryDeletePath(Path.Combine(categoryPath, itemId));
            TryDeleteEmptyDirectory(appPath);
        }
    }

    private static void TryDeletePath(string path)
    {
        if (Directory.Exists(path))
        {
            TryModify(() => Directory.Delete(path, true));
        }
        else if (File.Exists(path))
        {
            TryModify(() => File.Delete(path));
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            TryModify(() => Directory.Delete(path));
        }
    }
}
