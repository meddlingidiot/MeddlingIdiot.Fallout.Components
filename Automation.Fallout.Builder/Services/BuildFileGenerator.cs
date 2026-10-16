using System.Text;
using Automation.Fallout.Builder.Models;

namespace Automation.Fallout.Builder.Services;

public static class BuildFileGenerator
{
    public static string GenerateBuildFile(BuildConfiguration config, DefaultBuildInfo buildInfo)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using Fallout.Common;");
        sb.AppendLine("using Fallout.Solutions;");
        sb.AppendLine("using Automation.Fallout.Components;");
        sb.AppendLine("using Automation.Fallout.Components.Components;");
        sb.AppendLine("using Automation.Fallout.Components.DefaultBuilds;");
        sb.AppendLine("using Automation.Fallout.Components.Parameters;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Build configuration for {buildInfo.Name}");
        sb.AppendLine("/// </summary>");
        sb.AppendLine();

        // Build class declaration - use generic Build with interfaces from the selected DefaultBuild
        var interfaces = GetInterfacesForBuild(buildInfo.Name, config.Platform);
        var interfaceString = string.Join(", ", interfaces);
        sb.AppendLine($"public class Build : {GetBaseClass(config.Platform)}, {interfaceString}");
        sb.AppendLine("{");
        sb.AppendLine();

        // Generate Main method
        var targets = GenerateTargets(buildInfo, config.Platform);
        if (targets.Count == 1)
        {
            sb.AppendLine($"    public static int Main() => Execute<Build>(");
            sb.AppendLine($"        {targets[0]});");
        }
        else
        {
            sb.AppendLine($"    public static int Main() => Execute<Build>(");
            for (int i = 0; i < targets.Count; i++)
            {
                var comma = i < targets.Count - 1 ? "," : ");";
                sb.AppendLine($"        {targets[i]}{comma}");
            }
        }
        sb.AppendLine();
        
        // Generate property overrides
        if (buildInfo.RequiresVelopack && !string.IsNullOrEmpty(config.VelopackProjectName))
        {
            sb.AppendLine($"    string IHasVelopack.VelopackProjectName => \"{config.VelopackProjectName}\";");
            if (!string.IsNullOrEmpty(config.VelopackIconPath))
            {
                sb.AppendLine($"    string IHasVelopack.VelopackIconPath => @\"{config.VelopackIconPath}\";");
            }
        }
        
        if (buildInfo.RequiresTests)
        {
            if (config.EnableCodeCoverage && config.MinCodeCoverage > 0)
            {
                sb.AppendLine($"    int IHasTests.MinCoverageThreshold => {config.MinCodeCoverage};");
            }
            
            if (!config.BreakBuildOnWarnings)
            {
                sb.AppendLine("    bool IHasTests.BreakBuildOnWarnings => false;");
            }
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// The build base class supplying platform-specific CI helpers.
    /// </summary>
    private static string GetBaseClass(BuildPlatform platform) => platform switch
    {
        BuildPlatform.GitHubActions => "GitHubActionsBuild",
        _ => "AzurePipelinesBuild"
    };

    /// <summary>
    /// The packaging interface for the platform. Packaging is split per platform because the push
    /// destination differs - see IPackageGitHub / IPackageAzureDevOps.
    /// </summary>
    private static string GetPackageInterface(BuildPlatform platform) => platform switch
    {
        BuildPlatform.GitHubActions => "IPackageGitHub",
        _ => "IPackageAzureDevOps"
    };

    private static List<string> GetInterfacesForBuild(string buildName, BuildPlatform platform)
    {
        var interfaces = GetInterfacesForBuildCore(buildName);

        // The generic "IPackage" placeholder only names the packaging step; swap in the concrete
        // platform interface that actually carries the ReleasePackage target.
        for (var i = 0; i < interfaces.Count; i++)
        {
            if (interfaces[i] == "IPackage")
                interfaces[i] = GetPackageInterface(platform);
        }

        // GitHub builds can additionally create a GitHub release from the pushed tag.
        if (platform == BuildPlatform.GitHubActions && interfaces.Contains("ITagRelease"))
            interfaces.Add("ICreateGitHubRelease");

        return interfaces;
    }

    private static List<string> GetInterfacesForBuildCore(string buildName)
    {
        return buildName switch
        {
            "CompileBuild" => new List<string>
            {
                "IShowVersion", "IClean", "ICompile", "IRestore"
            },
            "TestBuild" => new List<string>
            {
                "IShowVersion", "IClean", "ICompile", "IRestore", "IRunUnitTests",
                "IRunIntegrationTests", "IGenerateCoverageReport", "ITest"
            },
            "PackageBuild" => new List<string>
            {
                "IShowVersion", "IClean", "ICompile", "IRestore",  "IRunUnitTests",
                "IRunIntegrationTests", "IGenerateCoverageReport", "ITest",
                "IUpdateChangelog", "IPackage", "ITagRelease", "IAnnounceRelease"
            },
            "VelopackBuild" => new List<string>
            {
                "IShowVersion", "IClean", "ICompile", "IRestore",  "IRunUnitTests",
                "IRunIntegrationTests", "IGenerateCoverageReport", "ITest",
                "IUpdateChangelog", "IVelopack", "ITagRelease", "IAnnounceRelease"
            },
            "PackageAndVelopackBuild" => new List<string>
            {
                "IShowVersion", "IClean", "ICompile", "IRestore", "IRunUnitTests",
                "IRunIntegrationTests", "IGenerateCoverageReport", "ITest", 
                "IUpdateChangelog", "IPackage", "IVelopack", "ITagRelease", "IAnnounceRelease"
            },
            _ => new List<string>()
        };
    }

    private static List<string> GenerateTargets(DefaultBuildInfo buildInfo, BuildPlatform platform)
    {
        var targets = new List<string>();
        var package = GetPackageInterface(platform);

        switch (buildInfo.Name)
        {
            case "CompileBuild":
                targets.Add("x => ((ICompile)x).Compile");
                break;
            case "TestBuild":
                targets.Add("x => ((ITest)x).Test");
                break;
            case "PackageBuild":
                targets.Add($"x => (({package})x).ReleasePackage");
                break;
            case "VelopackBuild":
                targets.Add("y => ((IVelopack)y).ReleaseVelopack");
                break;
            case "PackageAndVelopackBuild":
                targets.Add($"x => (({package})x).ReleasePackage");
                targets.Add("y => ((IVelopack)y).ReleaseVelopack");
                break;
        }

        return targets;
    }
}
