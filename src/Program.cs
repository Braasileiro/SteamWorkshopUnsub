using Spectre.Console;
using Steamworks;
using SteamWorkshopUnsub.Steam;
using SteamWorkshopUnsub.Workshop;

namespace SteamWorkshopUnsub;

internal static class Program
{
    private const int SuccessExitCode = 0;
    private const int InvalidArgumentsExitCode = 2;
    private const int SteamNotFoundExitCode = 3;
    private const int SteamInitializationExitCode = 4;
    private const int OperationFailedExitCode = 5;
    private const int PartialFailureExitCode = 6;
    private const int CancelledExitCode = 130;

    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            // Prevent immediate process termination so finally blocks can shut
            // down SteamAPI and remove the temporary steam_appid.txt directory.
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            return await RunAsync(args, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            WorkshopConsole.ShowCancelled();
            return CancelledExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        WorkshopConsole.ShowHeader(BuildInfo.Name, BuildInfo.Version, BuildInfo.Copyright);

        string? appIdArgument = args.FirstOrDefault();
        bool hasInvalidArgument = args
            .Skip(1)
            .Any(argument => !argument.Equals("--list", StringComparison.OrdinalIgnoreCase));
        if (!uint.TryParse(appIdArgument, out uint appId) || hasInvalidArgument)
        {
            WorkshopConsole.ShowUsage();
            return InvalidArgumentsExitCode;
        }

        string appIdText = appId.ToString();
        bool listOnly = args.Contains("--list", StringComparer.OrdinalIgnoreCase);

        SteamRuntimeInfo? runtime = SteamLocator.GetSteamRuntimeInfo();
        if (runtime is null)
        {
            WorkshopConsole.ShowError("Steam installation not found. Set STEAM_PATH if Steam uses a custom location.");
            return SteamNotFoundExitCode;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var appIdContext = new SteamAppIdContext(appIdText);
        if (!SteamAPI.Init())
        {
            WorkshopConsole.ShowError("Failed to initialize SteamAPI. Ensure that Steam is running and the AppID is valid.");
            return SteamInitializationExitCode;
        }

        try
        {
            uint activeUserId = WorkshopClient.GetCurrentAccountId();
            string userUgcPath = Path.Combine(runtime.SteamPath, "userdata", activeUserId.ToString(), "ugc");
            IReadOnlyList<string> acfPaths = WorkshopStorage.FindAcfPaths(appIdText, runtime.LibraryPaths);

            List<ulong> subscribedIds = WorkshopClient.GetSubscribedItemIds();
            List<ulong> localIds = WorkshopStorage.ReadItemIds(acfPaths);
            List<ulong> knownIds = subscribedIds.Concat(localIds).Distinct().ToList();

            // Neither source is complete for every AppID.
            // Steamworks includes remote subscriptions, while ACF can include Steam-managed items.
            WorkshopConsole.ShowRuntime(
                appIdText,
                runtime.SteamPath,
                runtime.LibraryPaths.Count,
                activeUserId,
                subscribedIds.Count,
                localIds.Count);
            if (knownIds.Count == 0)
            {
                WorkshopConsole.ShowNoItems();
                return SuccessExitCode;
            }

            List<WorkshopItem> items = await AnsiConsole.Status()
                .StartAsync("Querying SteamAPI...", _ => WorkshopClient.GetItemsAsync(knownIds, cancellationToken));

            var subscribedIdSet = subscribedIds.ToHashSet();
            var localIdSet = localIds.ToHashSet();
            foreach (var item in items)
            {
                item.Source = (subscribedIdSet.Contains(item.Id) ? WorkshopItemSource.Steam : WorkshopItemSource.None) |
                    (localIdSet.Contains(item.Id) ? WorkshopItemSource.Acf : WorkshopItemSource.None);
            }

            if (listOnly)
            {
                WorkshopConsole.ShowItemsTable(items);
                return SuccessExitCode;
            }

            List<WorkshopItem> selectedItems = await WorkshopConsole.SelectItemsForRemovalAsync(items, cancellationToken);
            if (selectedItems.Count == 0)
            {
                WorkshopConsole.ShowNoItemsSelected();
                return SuccessExitCode;
            }

            if (!WorkshopConsole.ConfirmRemoval(selectedItems.Count))
            {
                return SuccessExitCode;
            }

            int removedCount = 0;
            foreach (var item in selectedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool unsubscribed = await WorkshopClient.UnsubscribeItemAsync(item.Id, cancellationToken);
                WorkshopConsole.ShowUnsubscribeResult(item.Id, unsubscribed);
                if (!unsubscribed)
                {
                    continue;
                }

                removedCount++;
                WorkshopStorage.DeleteItemData(appIdText, item.Id, runtime.LibraryPaths, userUgcPath);
                WorkshopStorage.RemoveItemFromAcf(item.Id, acfPaths);
                WorkshopStorage.RemoveItemFromConsumedVdf(appIdText, item.Id, userUgcPath);
            }

            WorkshopConsole.ShowCompletion(removedCount, selectedItems.Count);
            return removedCount == selectedItems.Count ? SuccessExitCode : PartialFailureExitCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            WorkshopConsole.ShowError(exception.Message);
            return OperationFailedExitCode;
        }
        finally
        {
            SteamAPI.Shutdown();
        }
    }
}
