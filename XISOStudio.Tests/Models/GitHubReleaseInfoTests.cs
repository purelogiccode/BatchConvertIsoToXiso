using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests the default values and settable properties of <c>GitHubReleaseInfo</c>.</summary>
public class GitHubReleaseInfoTests
{
    [Fact]
    public void DefaultValuesAreNull()
    {
        var info = new GitHubReleaseInfo();

        Assert.Null(info.TagName);
        Assert.Null(info.HtmlUrl);
    }

    [Fact]
    public void PropertiesCanBeSet()
    {
        var info = new GitHubReleaseInfo
        {
            TagName = "v2.3.1",
            HtmlUrl = "https://github.com/test/releases/tag/v2.3.1"
        };

        Assert.Equal("v2.3.1", info.TagName);
        Assert.Equal("https://github.com/test/releases/tag/v2.3.1", info.HtmlUrl);
    }

    [Fact]
    public void JsonPropertyNameAttributesMapToGitHubApiNames()
    {
        var tagName = typeof(GitHubReleaseInfo).GetProperty(nameof(GitHubReleaseInfo.TagName));
        var htmlUrl = typeof(GitHubReleaseInfo).GetProperty(nameof(GitHubReleaseInfo.HtmlUrl));

        Assert.NotNull(tagName);
        Assert.NotNull(htmlUrl);
        Assert.Equal("tag_name", tagName.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
        Assert.Equal("html_url", htmlUrl.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
    }

    [Fact]
    public void SerializationUsesGitHubApiPropertyNames()
    {
        var info = new GitHubReleaseInfo { TagName = "v3.0.0", HtmlUrl = "https://example.com/release" };

        var json = JsonSerializer.Serialize(info);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("v3.0.0", document.RootElement.GetProperty("tag_name").GetString());
        Assert.Equal("https://example.com/release", document.RootElement.GetProperty("html_url").GetString());
        Assert.False(document.RootElement.TryGetProperty(nameof(GitHubReleaseInfo.TagName), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(GitHubReleaseInfo.HtmlUrl), out _));
    }

    [Fact]
    public void DeserializationReadsGitHubApiPropertyNames()
    {
        const string json = "{\"tag_name\":\"v1.2.3\",\"html_url\":\"https://example.com/tag/v1.2.3\"}";

        var info = JsonSerializer.Deserialize<GitHubReleaseInfo>(json);

        Assert.NotNull(info);
        Assert.Equal("v1.2.3", info.TagName);
        Assert.Equal("https://example.com/tag/v1.2.3", info.HtmlUrl);
    }

    [Fact]
    public void DeserializationOfMissingPropertiesLeavesNulls()
    {
        var info = JsonSerializer.Deserialize<GitHubReleaseInfo>("{}");

        Assert.NotNull(info);
        Assert.Null(info.TagName);
        Assert.Null(info.HtmlUrl);
    }

    [Fact]
    public void PropertiesCanBeResetToNull()
    {
        var info = new GitHubReleaseInfo { TagName = "v1.0.0", HtmlUrl = "https://example.com" };

        info.TagName = null;
        info.HtmlUrl = null;

        Assert.Null(info.TagName);
        Assert.Null(info.HtmlUrl);
    }

    [Fact]
    public void InstancesAreIndependent()
    {
        var first = new GitHubReleaseInfo { TagName = "v1.0.0" };
        var second = new GitHubReleaseInfo { TagName = "v2.0.0" };

        Assert.Equal("v1.0.0", first.TagName);
        Assert.Equal("v2.0.0", second.TagName);
        Assert.Null(second.HtmlUrl);
    }
}