using System.Reflection;

namespace XboxIsoStudio.Services;

public static class GetApplicationVersion
{
    public static string GetProgramVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "Unknown";
    }
}