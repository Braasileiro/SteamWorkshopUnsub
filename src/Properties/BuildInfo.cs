using System.Reflection;

namespace SteamWorkshopUnsub;

internal static class BuildInfo
{
    private static readonly Assembly CurrentAssembly = typeof(BuildInfo).Assembly;

    public static string Name { get; } = CurrentAssembly.GetName().Name!;

    public static string? Version { get; } = GetDisplayVersion();

    public static string? Copyright { get; } = CurrentAssembly
        .GetCustomAttribute<AssemblyCopyrightAttribute>()?
        .Copyright;

    private static string? GetDisplayVersion()
    {
        string? informationalVersion = CurrentAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return informationalVersion?.Split('+')[0] ?? CurrentAssembly.GetName().Version?.ToString();
    }
}
