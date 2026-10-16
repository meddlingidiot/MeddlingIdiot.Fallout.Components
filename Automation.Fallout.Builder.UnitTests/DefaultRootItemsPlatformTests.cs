using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;

namespace Automation.Fallout.Builder.UnitTests;

/// <summary>
/// Root items are embedded per platform: everyone gets the Common folder, and only the selected
/// platform's CI definition and feed configuration come along with it.
/// </summary>
public class DefaultRootItemsPlatformTests
{
    [Test]
    [Arguments(BuildPlatform.GitHubActions)]
    [Arguments(BuildPlatform.AzureDevOps)]
    public async Task CommonItems_ShipWithEveryPlatform(BuildPlatform platform)
    {
        var names = NuGetPackageInstaller.GetDefaultRootItems(platform).Select(x => x.FileName).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(names).Contains("GitVersion.yml");
            await Assert.That(names).DoesNotContain(".gitleaks.toml");
            await Assert.That(names).Contains("nuget.config");
            await Assert.That(names).Contains("global.json");
        }
    }

    /// <summary>
    /// The SDK floor is the whole point of shipping global.json. It pins the feature band rather than
    /// an exact patch: the band decides which NuGet.Frameworks MSBuild binds, and that has to agree
    /// with the NuGet.Packaging pinned into build/_build.csproj or project parsing fails outright.
    /// latestFeature keeps newer patches usable without re-opening that mismatch.
    /// </summary>
    [Test]
    [Arguments(BuildPlatform.GitHubActions)]
    [Arguments(BuildPlatform.AzureDevOps)]
    public async Task GlobalJson_PinsTheSdkFeatureBand(BuildPlatform platform)
    {
        var content = NuGetPackageInstaller.GetDefaultRootItems(platform).Single(x => x.FileName == "global.json").Content;

        var sdk = System.Text.Json.JsonDocument.Parse(content).RootElement.GetProperty("sdk");

        using (Assert.Multiple())
        {
            await Assert.That(sdk.GetProperty("version").GetString()).IsEqualTo(MigrationDefaults.SdkVersion);
            await Assert.That(sdk.GetProperty("rollForward").GetString()).IsEqualTo("latestFeature");
        }
    }

    /// <summary>
    /// global.json sits at the repository root; nothing maps it elsewhere, and it is refreshed on
    /// every run rather than left in place, so an out of date pin gets corrected.
    /// </summary>
    [Test]
    public async Task GlobalJson_LandsAtTheRootAndIsRefreshed()
    {
        using (Assert.Multiple())
        {
            await Assert.That(NuGetPackageInstaller.ResolveRootItemDestination("global.json")).IsEqualTo("global.json");
            await Assert.That(NuGetPackageInstaller.IsProtectedRootItem("global.json")).IsFalse();
        }
    }

    [Test]
    public async Task GitHubActions_ShipsWorkflowAndNotAzurePipelines()
    {
        var names = NuGetPackageInstaller.GetDefaultRootItems(BuildPlatform.GitHubActions)
            .Select(x => x.FileName).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(names).Contains("build.yml");
            await Assert.That(names).DoesNotContain("azure-pipelines.yml");
        }
    }

    [Test]
    public async Task AzureDevOps_ShipsPipelineAndNotWorkflow()
    {
        var names = NuGetPackageInstaller.GetDefaultRootItems(BuildPlatform.AzureDevOps)
            .Select(x => x.FileName).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(names).Contains("azure-pipelines.yml");
            await Assert.That(names).DoesNotContain("build.yml");
        }
    }

    /// <summary>
    /// The AFTR feeds are private, so they must only reach repositories that actually build on
    /// Azure DevOps - a GitHub consumer restoring against them would fail.
    /// </summary>
    [Test]
    public async Task NuGetConfig_TargetsTheFeedsForItsPlatform()
    {
        var github = NuGetPackageInstaller.GetDefaultRootItems(BuildPlatform.GitHubActions)
            .Single(x => x.FileName == "nuget.config");
        var azure = NuGetPackageInstaller.GetDefaultRootItems(BuildPlatform.AzureDevOps)
            .Single(x => x.FileName == "nuget.config");

        var githubText = System.Text.Encoding.UTF8.GetString(github.Content);
        var azureText = System.Text.Encoding.UTF8.GetString(azure.Content);

        using (Assert.Multiple())
        {
            await Assert.That(githubText).DoesNotContain("pkgs.dev.azure.com");
            await Assert.That(githubText).Contains("api.nuget.org");
            await Assert.That(azureText).Contains("pkgs.dev.azure.com");
        }
    }

    /// <summary>
    /// Automation.Fallout.Components is published to GitHub Packages and is not on nuget.org, so a
    /// GitHub repository whose nuget.config names only nuget.org cannot restore its own build project.
    /// </summary>
    [Test]
    public async Task GitHubNuGetConfig_NamesTheGitHubPackagesFeed()
    {
        var text = ReadNuGetConfig(BuildPlatform.GitHubActions);

        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("nuget.pkg.github.com");
            // The feed rejects anonymous requests even for public packages.
            await Assert.That(text).Contains("%GITHUB_TOKEN%");
        }
    }

    /// <summary>
    /// Fallout.Common's 11.x releases live on nuget.org, so the mapping must not divert Fallout.*
    /// to the GitHub feed - only the Automation.* packages come from there.
    /// </summary>
    [Test]
    public async Task GitHubNuGetConfig_MapsOnlyAutomationPackagesToGitHub()
    {
        var text = ReadNuGetConfig(BuildPlatform.GitHubActions);

        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("""<package pattern="Automation.*" />""");
            await Assert.That(text).DoesNotContain("""<package pattern="Fallout.*" />""");
        }
    }

    /// <summary>
    /// The platform is detected from the feeds in nuget.config, so a template naming no
    /// platform-specific feed reads back as inconclusive and falls through to the Azure default.
    /// </summary>
    [Test]
    [Arguments(BuildPlatform.GitHubActions)]
    [Arguments(BuildPlatform.AzureDevOps)]
    public async Task NuGetConfig_ReadsBackAsItsOwnPlatform(BuildPlatform platform)
    {
        var detected = PlatformDetector.DetectFromContent(ReadNuGetConfig(platform));

        await Assert.That(detected).IsEqualTo(platform);
    }

    private static string ReadNuGetConfig(BuildPlatform platform) =>
        System.Text.Encoding.UTF8.GetString(
            NuGetPackageInstaller.GetDefaultRootItems(platform).Single(x => x.FileName == "nuget.config").Content);
}
