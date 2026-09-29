using System.Reflection;

namespace XISOStudio.Services;

/// <summary>
/// Reads the application version from the executing assembly.
/// </summary>
public static class GetApplicationVersion
{
    /// <summary>
    /// Gets the version of the executing assembly.
    /// </summary>
    /// <returns>The assembly version string, or "Unknown" when it cannot be determined.</returns>
    public static string GetProgramVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "Unknown";
    }
}