using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;

namespace Automation.Fallout.Builder.UnitTests;

/// <summary>
/// Migrate replaces the pipeline definition with the embedded default, and a pipeline is the one
/// root item people customise by hand. The original has to survive in .fallout/Backup or those
/// customisations are gone.
/// </summary>
public class PipelineBackupTests : IAsyncDisposable
{
    private const string CustomisedPipeline = "# hand-tuned pipeline\ntrigger: none\n";

    private readonly string _testDirectory;

    public PipelineBackupTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PipelineBackupTests_{Guid.NewGuid()}");
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

    [Test]
    [Arguments(BuildPlatform.AzureDevOps, "azure-pipelines.yml")]
    [Arguments(BuildPlatform.GitHubActions, ".github/workflows/build.yml")]
    public async Task CopyDefaultRootItems_BacksUpThePipelineBeforeReplacingIt(BuildPlatform platform, string relativePath)
    {
        var pipeline = await WriteCustomisedPipelineAsync(relativePath);
        var workspace = new MigrationWorkspace(_testDirectory, new MigrationOptions(), new MigrationReport());

        RootItemMigrator.CopyDefaultRootItems(workspace, platform);

        using (Assert.Multiple())
        {
            await Assert.That(await File.ReadAllTextAsync(pipeline)).IsNotEqualTo(CustomisedPipeline);
            await Assert.That(workspace.BackupDirectory).IsNotNull();

            var backup = Path.Combine(workspace.BackupDirectory!, relativePath);
            await Assert.That(File.Exists(backup)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(backup)).IsEqualTo(CustomisedPipeline);
        }
    }

    [Test]
    public async Task CopyDefaultRootItems_BacksUpGitVersionYmlToo()
    {
        var gitVersion = await WriteCustomisedPipelineAsync("GitVersion.yml");
        var workspace = new MigrationWorkspace(_testDirectory, new MigrationOptions(), new MigrationReport());

        RootItemMigrator.CopyDefaultRootItems(workspace, BuildPlatform.AzureDevOps);

        using (Assert.Multiple())
        {
            await Assert.That(await File.ReadAllTextAsync(gitVersion)).IsNotEqualTo(CustomisedPipeline);
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(workspace.BackupDirectory!, "GitVersion.yml")))
                .IsEqualTo(CustomisedPipeline);
        }
    }

    [Test]
    public async Task CopyDefaultRootItems_DryRunLeavesThePipelineAndWritesNoBackup()
    {
        var pipeline = await WriteCustomisedPipelineAsync("azure-pipelines.yml");
        var workspace = new MigrationWorkspace(_testDirectory, new MigrationOptions { DryRun = true }, new MigrationReport());

        RootItemMigrator.CopyDefaultRootItems(workspace, BuildPlatform.AzureDevOps);

        using (Assert.Multiple())
        {
            await Assert.That(await File.ReadAllTextAsync(pipeline)).IsEqualTo(CustomisedPipeline);
            await Assert.That(workspace.BackupDirectory).IsNull();
        }
    }

    private async Task<string> WriteCustomisedPipelineAsync(string relativePath)
    {
        var path = Path.Combine(_testDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, CustomisedPipeline);
        return path;
    }
}
