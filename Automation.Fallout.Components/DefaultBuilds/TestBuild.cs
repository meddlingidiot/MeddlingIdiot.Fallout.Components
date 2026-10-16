using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.Parameters;
using Fallout.Common;

namespace Automation.Fallout.Components.DefaultBuilds;

public class TestBuild : AzurePipelinesBuild, IShowVersion, IClean, ICompile, IRestore,
    IRunUnitTests, IRunIntegrationTests, IGenerateCoverageReport, ITest, IUpdateChangelog,
    IPackageAzureDevOps, IVelopack, ITagRelease, IAnnounceRelease
{
    public static int Main() => Execute<TestBuild>(
        x => ((ITest)x).Test);

    int IHasTests.MinCoverageThreshold => 20;
    //bool IHasTests.BreakBuildOnWarnings => false;

}