namespace SteamWorkshopUnsub.Steam;

internal sealed class SteamAppIdContext : IDisposable
{
    private readonly string _originalDirectory = Environment.CurrentDirectory;
    private readonly string? _originalAppId = Environment.GetEnvironmentVariable("SteamAppId");
    private readonly string _temporaryDirectory;

    public SteamAppIdContext(string appId)
    {
        // SteamAPI checks the working directory for steam_appid.txt. Keeping it
        // in an isolated temporary directory avoids polluting the user's home.
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"SteamWorkshopUnsub-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        File.WriteAllText(Path.Combine(_temporaryDirectory, "steam_appid.txt"), appId);

        Environment.SetEnvironmentVariable("SteamAppId", appId);
        Environment.CurrentDirectory = _temporaryDirectory;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDirectory;
        Environment.SetEnvironmentVariable("SteamAppId", _originalAppId);

        try
        {
            Directory.Delete(_temporaryDirectory, true);
        }
        catch (IOException)
        {
            // The operating system will eventually clear its temporary directory.
        }
        catch (UnauthorizedAccessException)
        {
            // The operating system will eventually clear its temporary directory.
        }
    }
}
