using Spectre.Console;

namespace SteamWorkshopUnsub.Workshop;

internal static class WorkshopConsole
{
    public static void ShowHeader(string name, string? version, string? copyright) =>
        AnsiConsole.MarkupLine($"\n[yellow]{Markup.Escape(name)}[/] {Markup.Escape(version ?? string.Empty)} by {Markup.Escape(copyright ?? string.Empty)}\n");

    public static void ShowUsage() =>
        AnsiConsole.MarkupLine("[yellow]Usage:[/] <SteamAppId> [[--list]]");

    public static void ShowRuntime(
        string appId,
        string steamPath,
        int libraryCount,
        uint activeUserId,
        int subscriptionCount,
        int localItemCount)
    {
        AnsiConsole.MarkupLine($"[green]Connected - AppID: {Markup.Escape(appId)}[/]");
        AnsiConsole.MarkupLine($"[blue]Steam path:[/] [grey]{Markup.Escape(steamPath)}[/]");
        AnsiConsole.MarkupLine($"[blue]Steam library folders:[/] [grey]{libraryCount}[/]");
        AnsiConsole.MarkupLine($"[blue]Active user ID:[/] [grey]{activeUserId}[/]");
        AnsiConsole.MarkupLine($"[blue]Steam subscriptions:[/] [grey]{subscriptionCount}[/]");
        AnsiConsole.MarkupLine($"[blue]Local ACF entries:[/] [grey]{localItemCount}[/]");
    }

    public static void ShowItemsTable(IEnumerable<WorkshopItem> items)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]ID[/]");
        table.AddColumn("[yellow]Title[/]");
        table.AddColumn("[yellow]Source[/]");

        foreach (var item in items)
        {
            table.AddRow(
                item.Id.ToString(),
                Markup.Escape(item.Title ?? "Metadata unavailable"),
                FormatSource(item.Source));
        }

        AnsiConsole.Write(table);
    }

    private static string FormatSource(WorkshopItemSource source) => source switch
    {
        WorkshopItemSource.Steam => "[blue]Steam[/]",
        WorkshopItemSource.Acf => "[yellow]ACF[/]",
        WorkshopItemSource.Steam | WorkshopItemSource.Acf => "[green]Steam + ACF[/]",
        _ => "[grey]Unknown[/]"
    };

    public static bool ConfirmRemoval(int count) =>
        AnsiConsole.Confirm($"\n[red]UNSUBSCRIBE[/] and [red]DELETE[/] local data for these [blue]{count}[/] items?");

    public static void ShowUnsubscribeResult(ulong itemId, bool success) =>
        AnsiConsole.MarkupLine(success
            ? $"[green]Unsubscribed[/] from [grey]{itemId}[/]"
            : $"[red]Error:[/] Failed to unsubscribe from [grey]{itemId}[/]");

    public static void ShowNoItems() =>
        AnsiConsole.MarkupLine("[yellow]No subscribed Workshop items were reported by Steam.[/]");

    public static void ShowCompletion(int removedCount, int totalCount)
    {
        AnsiConsole.MarkupLine($"\n[green]Completed:[/] {removedCount} of {totalCount} items unsubscribed. Item-specific local cleanup was attempted.");
        AnsiConsole.MarkupLine("""
            [yellow]Steam may resubscribe to required items, such as official controller configurations.
            Restart Steam so changes to local configuration files are synchronized.[/]
            """);
    }

    public static void ShowError(string message) =>
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");

    public static void ShowCancelled() =>
        AnsiConsole.MarkupLine("\n[yellow]Operation cancelled.[/]");
}
