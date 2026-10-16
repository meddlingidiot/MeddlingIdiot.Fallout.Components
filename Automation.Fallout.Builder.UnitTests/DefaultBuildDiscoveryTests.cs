using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;

namespace Automation.Fallout.Builder.UnitTests;

public class DefaultBuildDiscoveryTests
{
    [Test]
    public async Task GetAvailableBuilds_ReturnsNonEmptyList()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        using (Assert.Multiple())
        {
            await Assert.That(builds).IsNotNull();
            await Assert.That(builds.Count).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task GetAvailableBuilds_ReturnsFiveBuilds()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        await Assert.That(builds.Count).IsEqualTo(5);
    }

    [Test]
    public async Task GetAvailableBuilds_ContainsCompileBuild()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var compileBuild = builds.FirstOrDefault(b => b.Name == "CompileBuild");

        using (Assert.Multiple())
        {
            await Assert.That(compileBuild).IsNotNull();
            await Assert.That(compileBuild!.Description).IsEqualTo("Basic compilation");
            await Assert.That(compileBuild.RequiresTests).IsFalse();
            await Assert.That(compileBuild.RequiresPackaging).IsFalse();
            await Assert.That(compileBuild.RequiresVelopack).IsFalse();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_ContainsTestBuild()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var testBuild = builds.FirstOrDefault(b => b.Name == "TestBuild");

        using (Assert.Multiple())
        {
            await Assert.That(testBuild).IsNotNull();
            await Assert.That(testBuild!.Description).IsEqualTo("Compilation, testing, and code coverage");
            await Assert.That(testBuild.RequiresTests).IsTrue();
            await Assert.That(testBuild.RequiresPackaging).IsFalse();
            await Assert.That(testBuild.RequiresVelopack).IsFalse();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_ContainsPackageBuild()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var packageBuild = builds.FirstOrDefault(b => b.Name == "PackageBuild");

        using (Assert.Multiple())
        {
            await Assert.That(packageBuild).IsNotNull();
            await Assert.That(packageBuild!.Description).IsEqualTo("Full pipeline with NuGet package creation");
            await Assert.That(packageBuild.RequiresTests).IsTrue();
            await Assert.That(packageBuild.RequiresPackaging).IsTrue();
            await Assert.That(packageBuild.RequiresVelopack).IsFalse();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_ContainsVelopackBuild()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var velopackBuild = builds.FirstOrDefault(b => b.Name == "VelopackBuild");

        using (Assert.Multiple())
        {
            await Assert.That(velopackBuild).IsNotNull();
            await Assert.That(velopackBuild!.Description).IsEqualTo("Application deployment with Velopack");
            await Assert.That(velopackBuild.RequiresTests).IsTrue();
            await Assert.That(velopackBuild.RequiresPackaging).IsFalse();
            await Assert.That(velopackBuild.RequiresVelopack).IsTrue();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_ContainsPackageAndVelopackBuild()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var packageAndVelopackBuild = builds.FirstOrDefault(b => b.Name == "PackageAndVelopackBuild");

        using (Assert.Multiple())
        {
            await Assert.That(packageAndVelopackBuild).IsNotNull();
            await Assert.That(packageAndVelopackBuild!.Description).IsEqualTo("Complete pipeline with both NuGet and Velopack");
            await Assert.That(packageAndVelopackBuild.RequiresTests).IsTrue();
            await Assert.That(packageAndVelopackBuild.RequiresPackaging).IsTrue();
            await Assert.That(packageAndVelopackBuild.RequiresVelopack).IsTrue();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_CompileBuild_HasCorrectComponents()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var compileBuild = builds.First(b => b.Name == "CompileBuild");

        using (Assert.Multiple())
        {
            await Assert.That(compileBuild.Components).IsNotNull();
            await Assert.That(compileBuild.Components.Count).IsEqualTo(4);
            await Assert.That(compileBuild.Components).Contains("IShowVersion");
            await Assert.That(compileBuild.Components).Contains("IClean");
            await Assert.That(compileBuild.Components).Contains("IRestore");
            await Assert.That(compileBuild.Components).Contains("ICompile");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_TestBuild_HasCorrectComponents()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var testBuild = builds.First(b => b.Name == "TestBuild");

        using (Assert.Multiple())
        {
            await Assert.That(testBuild.Components).IsNotNull();
            await Assert.That(testBuild.Components.Count).IsEqualTo(8);
            await Assert.That(testBuild.Components).Contains("IShowVersion");
            await Assert.That(testBuild.Components).Contains("IClean");
            await Assert.That(testBuild.Components).Contains("IRestore");
            await Assert.That(testBuild.Components).Contains("ICompile");
            await Assert.That(testBuild.Components).Contains("IRunUnitTests");
            await Assert.That(testBuild.Components).Contains("IRunIntegrationTests");
            await Assert.That(testBuild.Components).Contains("IGenerateCoverageReport");
            await Assert.That(testBuild.Components).Contains("ITest");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_PackageBuild_HasCorrectComponents()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var packageBuild = builds.First(b => b.Name == "PackageBuild");

        using (Assert.Multiple())
        {
            await Assert.That(packageBuild.Components).IsNotNull();
            await Assert.That(packageBuild.Components.Count).IsEqualTo(12);
            await Assert.That(packageBuild.Components).Contains("IShowVersion");
            await Assert.That(packageBuild.Components).Contains("IClean");
            await Assert.That(packageBuild.Components).Contains("IRestore");
            await Assert.That(packageBuild.Components).Contains("ICompile");
            await Assert.That(packageBuild.Components).Contains("IRunUnitTests");
            await Assert.That(packageBuild.Components).Contains("IRunIntegrationTests");
            await Assert.That(packageBuild.Components).Contains("IGenerateCoverageReport");
            await Assert.That(packageBuild.Components).Contains("ITest");
            await Assert.That(packageBuild.Components).Contains("IUpdateChangelog");
            await Assert.That(packageBuild.Components).Contains("IPackage");
            await Assert.That(packageBuild.Components).Contains("ITagRelease");
            await Assert.That(packageBuild.Components).Contains("IAnnounceRelease");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_VelopackBuild_HasCorrectComponents()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var velopackBuild = builds.First(b => b.Name == "VelopackBuild");

        using (Assert.Multiple())
        {
            await Assert.That(velopackBuild.Components).IsNotNull();
            await Assert.That(velopackBuild.Components.Count).IsEqualTo(12);
            await Assert.That(velopackBuild.Components).Contains("IShowVersion");
            await Assert.That(velopackBuild.Components).Contains("IClean");
            await Assert.That(velopackBuild.Components).Contains("IRestore");
            await Assert.That(velopackBuild.Components).Contains("ICompile");
            await Assert.That(velopackBuild.Components).Contains("IRunUnitTests");
            await Assert.That(velopackBuild.Components).Contains("IRunIntegrationTests");
            await Assert.That(velopackBuild.Components).Contains("IGenerateCoverageReport");
            await Assert.That(velopackBuild.Components).Contains("ITest");
            await Assert.That(velopackBuild.Components).Contains("IUpdateChangelog");
            await Assert.That(velopackBuild.Components).Contains("IVelopack");
            await Assert.That(velopackBuild.Components).Contains("ITagRelease");
            await Assert.That(velopackBuild.Components).Contains("IAnnounceRelease");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_PackageAndVelopackBuild_HasCorrectComponents()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var packageAndVelopackBuild = builds.First(b => b.Name == "PackageAndVelopackBuild");

        using (Assert.Multiple())
        {
            await Assert.That(packageAndVelopackBuild.Components).IsNotNull();
            await Assert.That(packageAndVelopackBuild.Components.Count).IsEqualTo(13);
            await Assert.That(packageAndVelopackBuild.Components).Contains("IShowVersion");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IClean");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IRestore");
            await Assert.That(packageAndVelopackBuild.Components).Contains("ICompile");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IRunUnitTests");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IRunIntegrationTests");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IGenerateCoverageReport");
            await Assert.That(packageAndVelopackBuild.Components).Contains("ITest");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IUpdateChangelog");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IPackage");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IVelopack");
            await Assert.That(packageAndVelopackBuild.Components).Contains("ITagRelease");
            await Assert.That(packageAndVelopackBuild.Components).Contains("IAnnounceRelease");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_AllBuildsHaveUniqueName()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var uniqueNames = builds.Select(b => b.Name).Distinct().Count();

        await Assert.That(uniqueNames).IsEqualTo(builds.Count);
    }

    [Test]
    public async Task GetAvailableBuilds_AllBuildsHaveNonEmptyDescription()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        foreach (var build in builds)
        {
            await Assert.That(string.IsNullOrWhiteSpace(build.Description)).IsFalse();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_AllBuildsHaveNonEmptyName()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        foreach (var build in builds)
        {
            await Assert.That(string.IsNullOrWhiteSpace(build.Name)).IsFalse();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_AllBuildsHaveComponents()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        foreach (var build in builds)
        {
            using (Assert.Multiple())
            {
                await Assert.That(build.Components).IsNotNull();
                await Assert.That(build.Components.Count).IsGreaterThan(0);
            }
        }
    }

    [Test]
    public async Task GetAvailableBuilds_AllBuildsIncludeIShowVersion()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        foreach (var build in builds)
        {
            await Assert.That(build.Components).Contains("IShowVersion");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_AllBuildsIncludeIClean()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        foreach (var build in builds)
        {
            await Assert.That(build.Components).Contains("IClean");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_AllBuildsIncludeIRestore()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        foreach (var build in builds)
        {
            await Assert.That(build.Components).Contains("IRestore");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_AllBuildsIncludeICompile()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        foreach (var build in builds)
        {
            await Assert.That(build.Components).Contains("ICompile");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_NoBuildIncludesIScanForSecrets()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();

        foreach (var build in builds)
        {
            await Assert.That(build.Components).DoesNotContain("IScanForSecrets");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_OnlyCompileBuildDoesNotRequireTests()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildsRequiringTests = builds.Where(b => b.RequiresTests).ToList();
        var buildsNotRequiringTests = builds.Where(b => !b.RequiresTests).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(buildsRequiringTests.Count).IsEqualTo(4);
            await Assert.That(buildsNotRequiringTests).HasSingleItem();
            await Assert.That(buildsNotRequiringTests.First().Name).IsEqualTo("CompileBuild");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_OnlyTwoBuildsRequirePackaging()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildsRequiringPackaging = builds.Where(b => b.RequiresPackaging).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(buildsRequiringPackaging.Count).IsEqualTo(2);
            await Assert.That(buildsRequiringPackaging.Any(b => b.Name == "PackageBuild")).IsTrue();
            await Assert.That(buildsRequiringPackaging.Any(b => b.Name == "PackageAndVelopackBuild")).IsTrue();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_OnlyTwoBuildsRequireVelopack()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildsRequiringVelopack = builds.Where(b => b.RequiresVelopack).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(buildsRequiringVelopack.Count).IsEqualTo(2);
            await Assert.That(buildsRequiringVelopack.Any(b => b.Name == "VelopackBuild")).IsTrue();
            await Assert.That(buildsRequiringVelopack.Any(b => b.Name == "PackageAndVelopackBuild")).IsTrue();
        }
    }

    [Test]
    public async Task GetAvailableBuilds_CalledMultipleTimes_ReturnsEquivalentData()
    {
        var builds1 = DefaultBuildDiscovery.GetAvailableBuilds();
        var builds2 = DefaultBuildDiscovery.GetAvailableBuilds();

        await Assert.That(builds1.Count).IsEqualTo(builds2.Count);
        for (int i = 0; i < builds1.Count; i++)
        {
            using (Assert.Multiple())
            {
                await Assert.That(builds1[i].Name).IsEqualTo(builds2[i].Name);
                await Assert.That(builds1[i].Description).IsEqualTo(builds2[i].Description);
                await Assert.That(builds1[i].RequiresTests).IsEqualTo(builds2[i].RequiresTests);
                await Assert.That(builds1[i].RequiresPackaging).IsEqualTo(builds2[i].RequiresPackaging);
                await Assert.That(builds1[i].RequiresVelopack).IsEqualTo(builds2[i].RequiresVelopack);
                await Assert.That(builds1[i].Components.Count).IsEqualTo(builds2[i].Components.Count);
            }
        }
    }

    [Test]
    public async Task GetAvailableBuilds_BuildsWithTests_IncludeITest()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildsRequiringTests = builds.Where(b => b.RequiresTests);

        foreach (var build in buildsRequiringTests)
        {
            await Assert.That(build.Components).Contains("ITest");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_BuildsWithPackaging_IncludeIPackage()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildsRequiringPackaging = builds.Where(b => b.RequiresPackaging);

        foreach (var build in buildsRequiringPackaging)
        {
            await Assert.That(build.Components).Contains("IPackage");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_BuildsWithVelopack_IncludeIVelopack()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildsRequiringVelopack = builds.Where(b => b.RequiresVelopack);

        foreach (var build in buildsRequiringVelopack)
        {
            await Assert.That(build.Components).Contains("IVelopack");
        }
    }

    [Test]
    public async Task GetAvailableBuilds_BuildsWithTests_IncludeTestingComponents()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildsRequiringTests = builds.Where(b => b.RequiresTests);

        foreach (var build in buildsRequiringTests)
        {
            using (Assert.Multiple())
            {
                await Assert.That(build.Components).Contains("IRunUnitTests");
                await Assert.That(build.Components).Contains("IRunIntegrationTests");
                await Assert.That(build.Components).Contains("IGenerateCoverageReport");
            }
        }
    }

    [Test]
    public async Task GetAvailableBuilds_BuildsWithPackagingOrVelopack_IncludeReleaseComponents()
    {
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildsRequiringRelease = builds.Where(b => b.RequiresPackaging || b.RequiresVelopack);

        foreach (var build in buildsRequiringRelease)
        {
            using (Assert.Multiple())
            {
                await Assert.That(build.Components).Contains("ITagRelease");
                await Assert.That(build.Components).Contains("IAnnounceRelease");
            }
        }
    }
}
