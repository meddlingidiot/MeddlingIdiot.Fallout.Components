using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.Parameters;
using Fallout.Common;

namespace Automation.Fallout.Components.DefaultBuilds;

public class VelopackBuild : AzurePipelinesBuild, IShowVersion,
    IClean, ICompile, IRestore, IRunUnitTests, IRunIntegrationTests, 
    IGenerateCoverageReport, ITest, IUpdateChangelog,
    IVelopack, ITagRelease, IAnnounceRelease
{
    public static int Main() => Execute<VelopackBuild>(
        y => ((IVelopack)y).ReleaseVelopack);

    //string IHasVelopack.VelopackProjectName => "Automation.NukeWpfAndPackageExample.WpfExample";
    //string IHasVelopack.VelopackIconPath => "Automation.NukeWpfANdPackageExample.WpfExample/assets/reset-password.ico";
    
    int IHasTests.MinCoverageThreshold => 20;
    
    //bool IHasTests.BreakBuildOnWarnings => false;

}