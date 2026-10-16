using Automation.Fallout.Builder.Models;

namespace Automation.Fallout.Builder.Services;

public static class DefaultBuildDiscovery
{
    public static List<DefaultBuildInfo> GetAvailableBuilds()
    {
        return new List<DefaultBuildInfo>
        {
            new()
            {
                Name = "CompileBuild",
                Description = "Basic compilation",
                Components = new() { "IShowVersion", "IClean", "IRestore", "ICompile" },
                RequiresTests = false,
                RequiresPackaging = false,
                RequiresVelopack = false
            },
            new()
            {
                Name = "TestBuild",
                Description = "Compilation, testing, and code coverage",
                Components = new() { "IShowVersion", "IClean", "IRestore", "ICompile",
                    "IRunUnitTests", "IRunIntegrationTests", "IGenerateCoverageReport", "ITest" },
                RequiresTests = true,
                RequiresPackaging = false,
                RequiresVelopack = false
            },
            new()
            {
                Name = "PackageBuild",
                Description = "Full pipeline with NuGet package creation",
                Components = new() { "IShowVersion", "IClean", "IRestore", "ICompile",
                    "IRunUnitTests", "IRunIntegrationTests", "IGenerateCoverageReport", "ITest", "IUpdateChangelog",
                    "IPackage", "ITagRelease", "IAnnounceRelease" },
                RequiresTests = true,
                RequiresPackaging = true,
                RequiresVelopack = false
            },
            new()
            {
                Name = "VelopackBuild",
                Description = "Application deployment with Velopack",
                Components = new() { "IShowVersion", "IClean", "IRestore", "ICompile",
                    "IRunUnitTests", "IRunIntegrationTests", "IGenerateCoverageReport", "ITest", "IUpdateChangelog", 
                    "IVelopack", "ITagRelease", "IAnnounceRelease" },
                RequiresTests = true,
                RequiresPackaging = false,
                RequiresVelopack = true
            },
            new()
            {
                Name = "PackageAndVelopackBuild",
                Description = "Complete pipeline with both NuGet and Velopack",
                Components = new() { "IShowVersion", "IClean", "IRestore", "ICompile",
                    "IRunUnitTests", "IRunIntegrationTests", "IGenerateCoverageReport", "ITest", "IUpdateChangelog",
                    "IPackage", "IVelopack", "ITagRelease", "IAnnounceRelease" },
                RequiresTests = true,
                RequiresPackaging = true,
                RequiresVelopack = true
            }
        };
    }
}
