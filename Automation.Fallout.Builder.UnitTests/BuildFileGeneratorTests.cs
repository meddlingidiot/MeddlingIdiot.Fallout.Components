using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;

namespace Automation.Fallout.Builder.UnitTests;

public class BuildFileGeneratorTests
{
    [Test]
    public async Task GenerateBuildFile_CompileBuild_GeneratesCorrectOutput()
    {
        var config = new BuildConfiguration
        {
            BuildType = "CompileBuild"
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "CompileBuild",
            Description = "Compile only build",
            RequiresTests = false,
            RequiresPackaging = false,
            RequiresVelopack = false
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("using Fallout.Common;");
            await Assert.That(result).Contains("using Automation.Fallout.Components;");
            await Assert.That(result).Contains("public class Build : AzurePipelinesBuild, IShowVersion, IClean, ICompile, IRestore");
            await Assert.That(result).DoesNotContain("IScanForSecrets");
            await Assert.That(result).Contains("public static int Main() => Execute<Build>(");
            await Assert.That(result).Contains("x => ((ICompile)x).Compile);");
        }
    }

    [Test]
    public async Task GenerateBuildFile_TestBuild_GeneratesCorrectOutput()
    {
        var config = new BuildConfiguration
        {
            BuildType = "TestBuild"
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "TestBuild",
            Description = "Test build",
            RequiresTests = true,
            RequiresPackaging = false,
            RequiresVelopack = false
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("IShowVersion, IClean, ICompile, IRestore, IRunUnitTests");
            await Assert.That(result).Contains("IRunIntegrationTests, IGenerateCoverageReport, ITest");
            await Assert.That(result).Contains("x => ((ITest)x).Test);");
        }
    }

    [Test]
    public async Task GenerateBuildFile_PackageBuild_GeneratesCorrectOutput()
    {
        var config = new BuildConfiguration
        {
            BuildType = "PackageBuild"
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "PackageBuild",
            Description = "Package build",
            RequiresTests = true,
            RequiresPackaging = true,
            RequiresVelopack = false
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("IUpdateChangelog, IPackageAzureDevOps, ITagRelease, IAnnounceRelease");
            await Assert.That(result).Contains("x => ((IPackageAzureDevOps)x).ReleasePackage);");
        }
    }

    [Test]
    public async Task GenerateBuildFile_VelopackBuild_GeneratesCorrectOutput()
    {
        var config = new BuildConfiguration
        {
            BuildType = "VelopackBuild",
            VelopackProjectName = "MyApp"
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "VelopackBuild",
            Description = "Velopack build",
            RequiresTests = true,
            RequiresPackaging = false,
            RequiresVelopack = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("IUpdateChangelog, IVelopack, ITagRelease, IAnnounceRelease");
            await Assert.That(result).Contains("y => ((IVelopack)y).ReleaseVelopack);");
            await Assert.That(result).Contains("string IHasVelopack.VelopackProjectName => \"MyApp\";");
        }
    }

    [Test]
    public async Task GenerateBuildFile_VelopackBuild_WithIcon_GeneratesCorrectOutput()
    {
        var config = new BuildConfiguration
        {
            BuildType = "VelopackBuild",
            VelopackProjectName = "MyApp",
            VelopackIconPath = "icon.ico"
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "VelopackBuild",
            Description = "Velopack build",
            RequiresTests = true,
            RequiresPackaging = false,
            RequiresVelopack = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("string IHasVelopack.VelopackProjectName => \"MyApp\";");
            await Assert.That(result).Contains("string IHasVelopack.VelopackIconPath => @\"icon.ico\";");
        }
    }

    [Test]
    public async Task GenerateBuildFile_PackageAndVelopackBuild_GeneratesCorrectOutput()
    {
        var config = new BuildConfiguration
        {
            BuildType = "PackageAndVelopackBuild",
            VelopackProjectName = "MyApp"
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "PackageAndVelopackBuild",
            Description = "Package and Velopack build",
            RequiresTests = true,
            RequiresPackaging = true,
            RequiresVelopack = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("IUpdateChangelog, IPackageAzureDevOps, IVelopack, ITagRelease, IAnnounceRelease");
            await Assert.That(result).Contains("x => ((IPackageAzureDevOps)x).ReleasePackage,");
            await Assert.That(result).Contains("y => ((IVelopack)y).ReleaseVelopack);");
        }
    }

    [Test]
    public async Task GenerateBuildFile_WithCodeCoverage_GeneratesMinCoverageThreshold()
    {
        var config = new BuildConfiguration
        {
            BuildType = "TestBuild",
            EnableCodeCoverage = true,
            MinCodeCoverage = 80
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "TestBuild",
            Description = "Test build",
            RequiresTests = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        await Assert.That(result).Contains("int IHasTests.MinCoverageThreshold => 80;");
    }

    [Test]
    public async Task GenerateBuildFile_WithoutCodeCoverage_DoesNotGenerateMinCoverageThreshold()
    {
        var config = new BuildConfiguration
        {
            BuildType = "TestBuild",
            EnableCodeCoverage = false,
            MinCodeCoverage = 0
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "TestBuild",
            Description = "Test build",
            RequiresTests = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        await Assert.That(result).DoesNotContain("int IHasTests.MinCoverageThreshold");
    }

    [Test]
    public async Task GenerateBuildFile_BreakBuildOnWarningsFalse_GeneratesOverride()
    {
        var config = new BuildConfiguration
        {
            BuildType = "TestBuild",
            BreakBuildOnWarnings = false
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "TestBuild",
            Description = "Test build",
            RequiresTests = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        await Assert.That(result).Contains("bool IHasTests.BreakBuildOnWarnings => false;");
    }

    [Test]
    public async Task GenerateBuildFile_BreakBuildOnWarningsTrue_DoesNotGenerateOverride()
    {
        var config = new BuildConfiguration
        {
            BuildType = "TestBuild",
            BreakBuildOnWarnings = true
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "TestBuild",
            Description = "Test build",
            RequiresTests = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        await Assert.That(result).DoesNotContain("bool IHasTests.BreakBuildOnWarnings => false;");
    }

    [Test]
    public async Task GenerateBuildFile_ContainsXmlDocComments()
    {
        var config = new BuildConfiguration
        {
            BuildType = "CompileBuild"
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "CompileBuild",
            Description = "Compile only build"
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("/// <summary>");
            await Assert.That(result).Contains("/// Build configuration for CompileBuild");
            await Assert.That(result).Contains("/// </summary>");
        }
    }

    [Test]
    public async Task GenerateBuildFile_UnknownBuildType_GeneratesEmptyInterfaces()
    {
        var config = new BuildConfiguration
        {
            BuildType = "UnknownBuild"
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "UnknownBuild",
            Description = "Unknown build"
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("public class Build : AzurePipelinesBuild, ");
            await Assert.That(result).DoesNotContain("ICompile");
            await Assert.That(result).DoesNotContain("ITest");
        }
    }

    [Test]
    public async Task GenerateBuildFile_WithZeroMinCodeCoverage_DoesNotGenerateThreshold()
    {
        var config = new BuildConfiguration
        {
            BuildType = "TestBuild",
            EnableCodeCoverage = true,
            MinCodeCoverage = 0
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "TestBuild",
            Description = "Test build",
            RequiresTests = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        await Assert.That(result).DoesNotContain("int IHasTests.MinCoverageThreshold");
    }

    [Test]
    public async Task GenerateBuildFile_VelopackWithoutProjectName_DoesNotGenerateVelopackProperties()
    {
        var config = new BuildConfiguration
        {
            BuildType = "VelopackBuild",
            VelopackProjectName = null
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "VelopackBuild",
            Description = "Velopack build",
            RequiresVelopack = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).DoesNotContain("string IHasVelopack.VelopackProjectName");
            await Assert.That(result).DoesNotContain("string IHasVelopack.VelopackIconPath");
        }
    }

    [Test]
    public async Task GenerateBuildFile_VelopackWithEmptyIconPath_DoesNotGenerateIconProperty()
    {
        var config = new BuildConfiguration
        {
            BuildType = "VelopackBuild",
            VelopackProjectName = "MyApp",
            VelopackIconPath = string.Empty
        };
        var buildInfo = new DefaultBuildInfo
        {
            Name = "VelopackBuild",
            Description = "Velopack build",
            RequiresVelopack = true
        };

        var result = BuildFileGenerator.GenerateBuildFile(config, buildInfo);

        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("string IHasVelopack.VelopackProjectName => \"MyApp\";");
            await Assert.That(result).DoesNotContain("string IHasVelopack.VelopackIconPath");
        }
    }
}
