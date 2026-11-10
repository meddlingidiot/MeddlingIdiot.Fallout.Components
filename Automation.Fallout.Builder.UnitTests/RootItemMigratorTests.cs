using Automation.Fallout.Builder.Services;

namespace Automation.Fallout.Builder.UnitTests;

public class RootItemMigratorTests
{
    private const string NukeToolManifest = """
                                            {
                                              "version": 1,
                                              "isRoot": true,
                                              "tools": {
                                                "nuke.globaltool": {
                                                  "version": "10.0.0",
                                                  "commands": [ "nuke" ],
                                                  "rollForward": false
                                                },
                                                "gitversion.tool": {
                                                  "version": "6.5.1",
                                                  "commands": [ "dotnet-gitversion" ],
                                                  "rollForward": false
                                                }
                                              }
                                            }
                                            """;

    [Test]
    public async Task MigrateToolManifest_ReplacesNukeGlobalToolAndKeepsEverythingElse()
    {
        var result = RootItemMigrator.MigrateToolManifest(NukeToolManifest, "10.5.0", out var changed);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(result).IsNotNull();
            await Assert.That(result!).Contains("fallout.globaltool");
            await Assert.That(result).Contains("10.5.0");
            await Assert.That(result).DoesNotContain("nuke.globaltool");
            // The shim command is the same under either Fallout CLI package id.
            await Assert.That(result).Contains("\"fallout\"");
            // Unrelated tools survive untouched.
            await Assert.That(result).Contains("gitversion.tool");
            await Assert.That(result).Contains("dotnet-gitversion");
        }
    }

    [Test]
    public async Task MigrateToolManifest_ReplacesTheOtherFalloutCliPackageId()
    {
        // fallout.cli has no build on the 10.x line the migration pins, so a manifest left on that
        // id cannot restore and has to be moved onto fallout.globaltool.
        var staleManifest = NukeToolManifest
            .Replace("nuke.globaltool", "fallout.cli")
            .Replace("\"nuke\"", "\"fallout\"");

        var result = RootItemMigrator.MigrateToolManifest(staleManifest, "10.5.0", out var changed);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(result!).Contains("fallout.globaltool");
            await Assert.That(result).DoesNotContain("fallout.cli");
        }
    }

    [Test]
    public async Task MigrateToolManifest_ReportsNoChangeWhenAlreadyMigrated()
    {
        var alreadyMigrated = NukeToolManifest
            .Replace("nuke.globaltool", "fallout.globaltool")
            .Replace("\"nuke\"", "\"fallout\"");

        var result = RootItemMigrator.MigrateToolManifest(alreadyMigrated, "10.5.0", out var changed);

        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(result).IsNull();
        }
    }

    [Test]
    public async Task MigrateBuildScript_PointsTempDirectoryAtFallout()
    {
        var powershell = """$TempDirectory = "$PSScriptRoot\\.nuke\temp" """;
        var bash = """TEMP_DIRECTORY="$SCRIPT_DIR//.nuke/temp" """;

        using (Assert.Multiple())
        {
            await Assert.That(RootItemMigrator.MigrateBuildScript(powershell)).Contains(@".fallout\temp");
            await Assert.That(RootItemMigrator.MigrateBuildScript(bash)).Contains(".fallout/temp");
        }
    }

    [Test]
    public async Task MigrateBuildScript_LeavesAnAlreadyMigratedScriptAlone()
    {
        var script = """TEMP_DIRECTORY="$SCRIPT_DIR//.fallout/temp" """;

        await Assert.That(RootItemMigrator.MigrateBuildScript(script)).IsEqualTo(script);
    }
}
