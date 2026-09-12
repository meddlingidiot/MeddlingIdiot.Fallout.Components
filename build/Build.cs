using Fallout.Common;
using Fallout.Solutions;
using Automation.Fallout.Components;
using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.DefaultBuilds;
using Automation.Fallout.Components.Parameters;

/// <summary>
/// Build configuration for PackageBuild
/// </summary>

public class Build : GitHubActionsBuild, IShowVersion, IClean, ICompile, IRestore, IScanForSecrets, IRunUnitTests, IRunIntegrationTests, IGenerateCoverageReport, ITest, IUpdateChangelog, INuGetPublish, ITagRelease, IAnnounceRelease
{

    public static int Main() => Execute<Build>(
        x => ((INuGetPublish)x).PublishNuGet);

    // Was IPackageMultiPlatform on AzurePipelinesBuild, which existed because this repository
    // was a mirror: one build serving both the Azure DevOps feed and GitHub Packages. The
    // mirror is gone, this copy is master, and it publishes to nuget.org and nowhere else.
    string? IHasNuGetOrg.NuGetOwner => "themeddlingidiot";

    // The only publish step here, so it is the one that tags.
    bool INuGetPublish.TagsReleasesFromNuGet => true;

    int IHasTests.MinCoverageThreshold => 20;
    bool ITestExecution.UseMicrosoftTestingPlatform => true;
}
