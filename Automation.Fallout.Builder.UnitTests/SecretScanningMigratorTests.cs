using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;

namespace Automation.Fallout.Builder.UnitTests;

public class SecretScanningMigratorTests : IAsyncDisposable
{
    private readonly string _testDirectory;

    public SecretScanningMigratorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SecretScanningMigratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_testDirectory))
        {
            try { Directory.Delete(_testDirectory, true); } catch { /* Ignore cleanup errors */ }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>A build file as setup generated it with "Break build on secret leaks?" answered no.</summary>
    private const string GeneratedBuildFile = """
                                              public class Build : GitHubActionsBuild, IShowVersion, IClean, ICompile, IRestore, IScanForSecrets, IRunUnitTests, ITest
                                              {
                                                  public static int Main() => Execute<Build>(
                                                      x => ((ITest)x).Test);

                                                  int IHasTests.MinCoverageThreshold => 1;

                                                  // Secret scanning off. IScanForSecrets gates its target on this.
                                                  bool IHasTests.BreakBuildOnSecretLeaks => false;
                                              }
                                              """;

    private const string ExpectedBuildFile = """
                                             public class Build : GitHubActionsBuild, IShowVersion, IClean, ICompile, IRestore, IRunUnitTests, ITest
                                             {
                                                 public static int Main() => Execute<Build>(
                                                     x => ((ITest)x).Test);

                                                 int IHasTests.MinCoverageThreshold => 1;
                                             }
                                             """;

    [Test]
    public async Task Rewrite_RemovesInterfaceOverrideAndItsComment()
    {
        var result = SecretScanningMigrator.Rewrite(GeneratedBuildFile, out var notes);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsEqualTo(ExpectedBuildFile);
            await Assert.That(notes.Count).IsEqualTo(2);
        }
    }

    [Test]
    public async Task Rewrite_HandlesCrLfLineEndings()
    {
        var result = SecretScanningMigrator.Rewrite(GeneratedBuildFile.ReplaceLineEndings("\r\n"), out _);

        await Assert.That(result).IsEqualTo(ExpectedBuildFile.ReplaceLineEndings("\r\n"));
    }

    [Test]
    public async Task Rewrite_IsANoOpOnceMigrated()
    {
        var result = SecretScanningMigrator.Rewrite(ExpectedBuildFile, out var notes);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsEqualTo(ExpectedBuildFile);
            await Assert.That(notes).IsEmpty();
        }
    }

    [Test]
    public async Task Rewrite_RemovesInterfaceFromAMultiLineBaseList()
    {
        const string source = """
                              public class Build : AzurePipelinesBuild, IShowVersion,
                                  IClean, ICompile, IRestore, IScanForSecrets,
                                  IRunUnitTests, ITest
                              {
                              }
                              """;

        var result = SecretScanningMigrator.Rewrite(source, out _);

        await Assert.That(result).IsEqualTo("""
                                            public class Build : AzurePipelinesBuild, IShowVersion,
                                                IClean, ICompile, IRestore,
                                                IRunUnitTests, ITest
                                            {
                                            }
                                            """);
    }

    [Test]
    public async Task Rewrite_RemovesInterfaceListedFirst()
    {
        var result = SecretScanningMigrator.Rewrite("class Build : IScanForSecrets, ITest\n{\n}", out _);

        await Assert.That(result).IsEqualTo("class Build : ITest\n{\n}");
    }

    [Test]
    public async Task Rewrite_KeepsTheBlankLineBetweenSurvivingMembers()
    {
        const string source = """
                              class Build : FalloutBuild, ITest
                              {
                                  bool IHasTests.BreakBuildOnWarnings => false;

                                  bool IHasTests.BreakBuildOnSecretLeaks => false;

                                  int IHasTests.MinCoverageThreshold => 1;
                              }
                              """;

        var result = SecretScanningMigrator.Rewrite(source, out _);

        await Assert.That(result).IsEqualTo("""
                                            class Build : FalloutBuild, ITest
                                            {
                                                bool IHasTests.BreakBuildOnWarnings => false;

                                                int IHasTests.MinCoverageThreshold => 1;
                                            }
                                            """);
    }

    [Test]
    public async Task Rewrite_RemovesADependencyOnTheScanTarget()
    {
        const string source = """
                              Target Publish => t => t
                                  .DependsOn<IScanForSecrets>(x => x.ScanForSecrets)
                                  .DependsOn<ITest>(x => x.Test)
                                  .Executes(() => { });
                              """;

        var result = SecretScanningMigrator.Rewrite(source, out _);

        using (Assert.Multiple())
        {
            await Assert.That(result).DoesNotContain("ScanForSecrets");
            await Assert.That(result).Contains(".DependsOn<ITest>(x => x.Test)");
            await Assert.That(SecretScanningMigrator.FindLeftovers(result)).IsEmpty();
        }
    }

    [Test]
    public async Task FindLeftovers_FlagsAMemberSpreadOverSeveralLines()
    {
        const string source = """
                              class Build : FalloutBuild
                              {
                                  string GitleaksVersion
                                      => "8.18.2";
                              }
                              """;

        var result = SecretScanningMigrator.Rewrite(source, out _);

        await Assert.That(SecretScanningMigrator.FindLeftovers(result)).Contains("GitleaksVersion");
    }

    [Test]
    public async Task RemoveSecretScanningConfig_DeletesBothGitleaksFiles()
    {
        foreach (var fileName in SecretScanningMigrator.ConfigFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDirectory, fileName), "title = \"gitleaks\"");
        }

        var report = new MigrationReport();
        var workspace = new MigrationWorkspace(_testDirectory, new MigrationOptions { NoBackup = true }, report);

        RootItemMigrator.RemoveSecretScanningConfig(workspace);

        using (Assert.Multiple())
        {
            await Assert.That(File.Exists(Path.Combine(_testDirectory, ".gitleaks.toml"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(_testDirectory, "gitleaks.toml"))).IsFalse();
            await Assert.That(report.Changes.Count(c => c.Kind == MigrationChangeKind.Removed)).IsEqualTo(2);
        }
    }

    [Test]
    public async Task RemoveSecretScanningConfig_DryRunLeavesTheFile()
    {
        var path = Path.Combine(_testDirectory, ".gitleaks.toml");
        await File.WriteAllTextAsync(path, "title = \"gitleaks\"");

        var report = new MigrationReport();
        var workspace = new MigrationWorkspace(_testDirectory, new MigrationOptions { DryRun = true }, report);

        RootItemMigrator.RemoveSecretScanningConfig(workspace);

        using (Assert.Multiple())
        {
            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(report.Changes.Count(c => c.Kind == MigrationChangeKind.Removed)).IsEqualTo(1);
        }
    }
}
