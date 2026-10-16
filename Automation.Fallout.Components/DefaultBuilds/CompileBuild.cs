using Automation.Fallout.Components.Components;
using Fallout.Common;

namespace Automation.Fallout.Components.DefaultBuilds;

public class CompileBuild : AzurePipelinesBuild, IShowVersion, IClean, ICompile, IRestore
{
    public static int Main() => Execute<CompileBuild>(x => ((ICompile)x).Compile); 

    //bool IHasTests.BreakBuildOnWarnings => false;

}