namespace SteamWorkshopUnsub.Workshop;

[Flags]
internal enum WorkshopItemSource
{
    None = 0,
    Steam = 1,
    Acf = 2
}

internal sealed class WorkshopItem
{
    public ulong Id { get; set; }
    public string? Title { get; set; }
    public WorkshopItemSource Source { get; set; }
}
