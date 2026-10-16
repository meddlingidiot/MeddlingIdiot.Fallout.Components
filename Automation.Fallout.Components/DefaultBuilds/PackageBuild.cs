using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.Parameters;
using Fallout.Common;

namespace Automation.Fallout.Components.DefaultBuilds;

public class PackageBuild : AzurePipelinesBuild, IShowVersion,
    IClean, ICompile, IRestore, IRunUnitTests, IRunIntegrationTests, 
    IGenerateCoverageReport, ITest, IUpdateChangelog,
    IPackageAzureDevOps, ITagRelease, IAnnounceRelease
{
    public static int Main() => Execute<PackageBuild>(
        x => ((IPackageAzureDevOps)x).ReleasePackage);

    int IHasTests.MinCoverageThreshold => 20;
    //bool IHasTests.BreakBuildOnWarnings => false;

}
