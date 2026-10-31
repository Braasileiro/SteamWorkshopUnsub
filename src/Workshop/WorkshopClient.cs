using System.Diagnostics;
using Steamworks;

namespace SteamWorkshopUnsub.Workshop;

internal static class WorkshopClient
{
    private const int QueryBatchSize = 1_000;
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(30);

    public static uint GetCurrentAccountId() => SteamUser.GetSteamID().GetAccountID().m_AccountID;

    public static List<ulong> GetSubscribedItemIds()
    {
        uint count = SteamUGC.GetNumSubscribedItems(true);
        if (count == 0)
        {
            return [];
        }

        var buffer = new PublishedFileId_t[count];
        uint returnedCount = SteamUGC.GetSubscribedItems(buffer, count, true);
        return buffer
            .Take((int)returnedCount)
            .Select(id => id.m_PublishedFileId)
            .Where(id => id != 0)
            .Distinct()
            .ToList();
    }

    public static async Task<List<WorkshopItem>> GetItemsAsync(
        IReadOnlyList<ulong> ids,
        CancellationToken cancellationToken)
    {
        var itemsById = ids.ToDictionary(id => id, id => new WorkshopItem { Id = id });

        // Steam limits detail queries to 1,000 IDs, so larger collections are
        // queried sequentially while preserving the original subscription order.
        foreach (ulong[] batch in ids.Chunk(QueryBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in await GetItemBatchAsync(batch, cancellationToken))
            {
                itemsById[item.Id] = item;
            }
        }

        return ids.Select(id => itemsById[id]).ToList();
    }

    private static async Task<List<WorkshopItem>> GetItemBatchAsync(
        ulong[] ids,
        CancellationToken cancellationToken)
    {
        var queryIds = ids.Select(id => new PublishedFileId_t(id)).ToArray();
        var handle = SteamUGC.CreateQueryUGCDetailsRequest(queryIds, (uint)queryIds.Length);

        try
        {
            var completion = new TaskCompletionSource<(SteamUGCQueryCompleted_t Result, bool Failure)>();
            using var callback = CallResult<SteamUGCQueryCompleted_t>.Create((result, failure) =>
                completion.TrySetResult((result, failure)));

            callback.Set(SteamUGC.SendQueryUGCRequest(handle));
            var response = await WaitForCallResultAsync(completion.Task, "Workshop details query", cancellationToken);
            if (response.Failure || response.Result.m_eResult != EResult.k_EResultOK)
            {
                throw new InvalidOperationException($"Steam rejected the Workshop details query: {response.Result.m_eResult}.");
            }

            var items = new List<WorkshopItem>((int)response.Result.m_unNumResultsReturned);
            for (uint index = 0; index < response.Result.m_unNumResultsReturned; index++)
            {
                if (SteamUGC.GetQueryUGCResult(handle, index, out SteamUGCDetails_t details))
                {
                    items.Add(new WorkshopItem
                    {
                        Id = details.m_nPublishedFileId.m_PublishedFileId,
                        Title = details.m_rgchTitle
                    });
                }
            }

            return items;
        }
        finally
        {
            // Every UGC query handle must be released, including failed or timed-out queries.
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    public static async Task<bool> UnsubscribeItemAsync(ulong id, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<(RemoteStorageUnsubscribePublishedFileResult_t Result, bool Failure)>();
        using var callback = CallResult<RemoteStorageUnsubscribePublishedFileResult_t>.Create((result, failure) =>
            completion.TrySetResult((result, failure)));

        callback.Set(SteamUGC.UnsubscribeItem(new PublishedFileId_t(id)));
        var response = await WaitForCallResultAsync(completion.Task, $"Unsubscribe {id}", cancellationToken);
        return !response.Failure && response.Result.m_eResult == EResult.k_EResultOK;
    }

    private static async Task<T> WaitForCallResultAsync<T>(
        Task<T> task,
        string operation,
        CancellationToken cancellationToken)
    {
        // Steamworks callbacks must be pumped while waiting for the result.
        // The timeout avoids hanging forever if the client stops responding.
        var stopwatch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SteamAPI.RunCallbacks();
            if (stopwatch.Elapsed >= CallbackTimeout)
            {
                throw new TimeoutException($"{operation} timed out after {CallbackTimeout.TotalSeconds:0} seconds.");
            }

            await Task.Delay(20, cancellationToken);
        }

        return await task;
    }
}
