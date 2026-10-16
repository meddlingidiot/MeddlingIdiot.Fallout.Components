using System.Text.Json;
using System.Text.Json.Nodes;
using Automation.Fallout.Builder.Models;

namespace Automation.Fallout.Builder.Services;

/// <summary>
/// Migrates everything outside the build project: the tool manifest, the bootstrap scripts, the
/// .nuke configuration directory and the root config files.
/// </summary>
public static class RootItemMigrator
{
    private static readonly string[] BuildScripts = { "build.ps1", "build.sh", "build.cmd" };

    private static readonly string[] CarriedConfigFiles = { "parameters.json", "parameters.profile.json" };

    /// <summary>
    /// Tool manifest entries that are replaced by <see cref="MigrationDefaults.GlobalToolPackageId"/>.
    /// fallout.cli is included because it is the Fallout CLI's other package id and has no build on
    /// the line <see cref="MigrationDefaults.GlobalToolVersion"/> sits on.
    /// </summary>
    private static readonly string[] SupersededGlobalTools = { "nuke.globaltool", "fallout.cli" };

    /// <summary>
    /// Replaces the nuke.globaltool entry in .config/dotnet-tools.json with fallout.cli.
    /// </summary>
    public static string? MigrateToolManifest(string manifestJson, string globalToolVersion, out bool changed)
    {
        changed = false;

        if (JsonNode.Parse(manifestJson) is not JsonObject manifest || manifest["tools"] is not JsonObject tools)
            return null;

        var migrated = new JsonObject();

        foreach (var tool in tools)
        {
            if (SupersededGlobalTools.Contains(tool.Key, StringComparer.OrdinalIgnoreCase))
            {
                migrated[MigrationDefaults.GlobalToolPackageId] = new JsonObject
                {
                    ["version"] = globalToolVersion,
                    ["commands"] = new JsonArray("fallout"),
                    ["rollForward"] = false
                };
                changed = true;
                continue;
            }

            migrated[tool.Key] = tool.Value?.DeepClone();
        }

        if (!changed)
            return null;

        manifest["tools"] = migrated;

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }

    /// <summary>
    /// Points the bootstrap scripts at .fallout instead of the legacy .nuke directory.
    /// </summary>
    public static string MigrateBuildScript(string script) =>
        script.Replace(@".nuke\temp", @".fallout\temp", StringComparison.Ordinal)
            .Replace(".nuke/temp", ".fallout/temp", StringComparison.Ordinal);

    public static void MigrateScripts(MigrationWorkspace workspace, MigrationReport report)
    {
        foreach (var scriptName in BuildScripts)
        {
            var path = workspace.Resolve(scriptName);
            var original = workspace.ReadText(path);
            if (original == null)
                continue;

            var migrated = MigrateBuildScript(original);
            if (!string.Equals(original, migrated, StringComparison.Ordinal))
            {
                workspace.WriteText(path, migrated, "Pointed temp directory at .fallout");
            }

            if (original.Contains("NUKE_ENTERPRISE_TOKEN", StringComparison.Ordinal))
            {
                report.Warn(workspace.Relative(path),
                    "Still contains the NUKE_ENTERPRISE_TOKEN feed block - harmless, but remove it if you never used Nuke Enterprise");
            }
        }
    }

    /// <summary>
    /// Carries any saved parameters over from .nuke to .fallout and deletes the stale directory.
    /// </summary>
    public static void MigrateConfigDirectory(MigrationWorkspace workspace)
    {
        var legacyDirectory = workspace.Resolve(MigrationDefaults.NukeConfigDirectory);
        if (!Directory.Exists(legacyDirectory))
            return;

        foreach (var fileName in CarriedConfigFiles)
        {
            var legacyFile = Path.Combine(legacyDirectory, fileName);
            var target = workspace.Resolve(MigrationDefaults.FalloutConfigDirectory, fileName);

            var contents = workspace.ReadText(legacyFile);
            if (contents == null || File.Exists(target))
                continue;

            workspace.WriteText(target, contents, $"Carried {fileName} over from .nuke");
        }

        // build.schema.json is regenerated on the next run and is gitignored.
        workspace.DeleteDirectory(legacyDirectory, "Removed the legacy .nuke directory");
    }

    /// <summary>
    /// Deletes the Gitleaks configuration. Secret scanning was removed from Fallout, so nothing reads
    /// it any more.
    /// </summary>
    public static void RemoveSecretScanningConfig(MigrationWorkspace workspace)
    {
        foreach (var fileName in SecretScanningMigrator.ConfigFiles)
        {
            workspace.DeleteFile(workspace.Resolve(fileName), "Removed the Gitleaks configuration - secret scanning is no longer part of Fallout");
        }
    }

    public static void CopyDefaultRootItems(MigrationWorkspace workspace, BuildPlatform platform)
    {
        foreach (var item in NuGetPackageInstaller.GetDefaultRootItems(platform))
        {
            // build.yml belongs under .github/workflows, not at the repository root.
            var target = workspace.Resolve(NuGetPackageInstaller.ResolveRootItemDestination(item.FileName));

            // An existing nuget.config carries the repository's own feeds and credentials, so it is
            // never overwritten - replacing it would silently cut the repository off from its packages.
            if (NuGetPackageInstaller.IsProtectedRootItem(item.FileName) && File.Exists(target))
            {
                workspace.Report.Skip(workspace.Relative(target), "Left in place (already exists)");
                continue;
            }

            workspace.WriteBytes(target, item.Content, "Refreshed from the Fallout defaults");
        }
    }
}
