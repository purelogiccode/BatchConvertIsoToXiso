using System.Text.Json.Serialization;

namespace XISOStudio.Models;

/// <summary>
/// The subset of the GitHub "latest release" API response used by the update checker.
/// </summary>
public class GitHubReleaseInfo
{
    /// <summary>Gets or sets the release tag name (for example <c>v1.2.3</c>).</summary>
    [JsonPropertyName("tag_name")] public string? TagName { get; set; }

    /// <summary>Gets or sets the URL of the release page on GitHub.</summary>
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
}