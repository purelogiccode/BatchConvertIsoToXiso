namespace XISOStudio.Interfaces;

/// <summary>
/// Sends anonymous startup statistics to the remote API.
/// </summary>
public interface IStatsService
{
    /// <summary>Sends the application ID and current version as a best-effort startup statistic.</summary>
    Task SendStatsAsync();
}