using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.Parameters;
using Fallout.Common;

namespace Automation.Fallout.Components.DefaultBuilds;

public class PackageAndVelopackBuild : AzurePipelinesBuild, IShowVersion,
    IClean, ICompile, IRestore, IRunUnitTests, IRunIntegrationTests, 
    IGenerateCoverageReport, ITest, IUpdateChangelog,
    IPackageAzureDevOps, IVelopack, ITagRelease, IAnnounceRelease
{
    public static int Main() => Execute<PackageAndVelopackBuild>(
        x => ((IPackageAzureDevOps)x).ReleasePackage, 
        y => ((IVelopack)y).ReleaseVelopack);

    string IHasVelopack.VelopackProjectName => "Automation.NukeWpfAndPackageExample.WpfExample";
    string IHasVelopack.VelopackIconPath => "Automation.NukeWpfANdPackageExample.WpfExample/assets/reset-password.ico";
    int IHasTests.MinCoverageThreshold => 80;
    //bool IHasTests.BreakBuildOnWarnings => false;

}