using Microsoft.Win32;
using System.Runtime.Versioning;
namespace SteamWorkshopUnsub.Steam;

internal sealed record SteamRuntimeInfo(string SteamPath, IReadOnlyList<string> LibraryPaths);

internal static class SteamLocator
{
    public static SteamRuntimeInfo? GetSteamRuntimeInfo()
    {
        IEnumerable<string> candidates = OperatingSystem.IsWindows()
            ? GetWindowsCandidates()
            : OperatingSystem.IsLinux()
                ? GetLinuxCandidates()
                : [];

        // The main libraryfolders.vdf contains every additional Steam library configured by the user.
        string? steamPath = ExistingDistinctDirectories(candidates).FirstOrDefault(IsSteamRoot);
        if (steamPath is null)
        {
            return null;
        }

        IReadOnlyList<string> libraryPaths = ExistingDistinctDirectories(GetLibraryCandidates(steamPath))
            .Where(path => Directory.Exists(Path.Combine(path, "steamapps")))
            .ToList();

        return new SteamRuntimeInfo(steamPath, libraryPaths);
    }

    private static bool IsSteamRoot(string path) =>
        Directory.Exists(Path.Combine(path, "steamapps")) &&
        (Directory.Exists(Path.Combine(path, "config")) || File.Exists(Path.Combine(path, "Steam.exe")));

    private static IEnumerable<string> GetLibraryCandidates(string steamPath)
    {
        yield return steamPath;

        string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            yield break;
        }

        VdfObject? libraries;
        try
        {
            libraries = VdfFile.Load(libraryFoldersPath).FindObject("libraryfolders");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // Keep using the main Steam library if the library list cannot be read.
            yield break;
        }

        if (libraries is null)
        {
            yield break;
        }

        foreach (var entry in libraries.Entries)
        {
            // Steam may store the library path inside an object or as a direct value.
            string? path = entry.Children?.FindValue("path") ?? entry.Value;
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> GetLinuxCandidates()
    {
        string? overridePath = Environment.GetEnvironmentVariable("STEAM_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            yield return overridePath;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            yield return Path.Combine(xdgDataHome, "Steam");
        }

        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".steam", "root");
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        yield return Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam");
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> GetWindowsCandidates()
    {
        string? overridePath = Environment.GetEnvironmentVariable("STEAM_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            yield return overridePath;
        }

        using RegistryKey? steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (steamKey?.GetValue("SteamPath") is string steamPath)
        {
            yield return steamPath;
        }

        if (steamKey?.GetValue("SteamExe") is string steamExe && Path.GetDirectoryName(steamExe) is string steamExeDirectory)
        {
            yield return steamExeDirectory;
        }

        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Steam");
        }
    }

    private static IEnumerable<string> ExistingDistinctDirectories(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

        foreach (string path in paths)
        {
            string normalized;
            try
            {
                normalized = Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                continue;
            }

            if (Directory.Exists(normalized) && seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }
}
