using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;
using Automation.Fallout.Builder.Ui;
using Spectre.Console;

namespace Automation.Fallout.Builder.Commands;

/// <summary>
/// Repairs a repository that has been renamed from Nuke to Fallout but no longer compiles.
/// </summary>
public static class MigrateCommand
{
    public static async Task<int> ExecuteAsync(MigrationOptions options)
    {
        FalloutBanner.Render("AFTR Fallout Migrate", Color.Orange1);

        var workingDirectory = options.Path ?? RepositoryLocator.FindRepositoryRoot();
        if (workingDirectory == null || !Directory.Exists(workingDirectory))
        {
            AnsiConsole.MarkupLine("[red]Could not find repository root. Run this from within a git repository or a directory containing a .sln or .slnx file, or pass --path.[/]");
            return 1;
        }

        var buildDirectory = Path.Combine(workingDirectory, "build");
        var buildProject = Path.Combine(buildDirectory, "_build.csproj");
        if (!File.Exists(buildProject))
        {
            AnsiConsole.MarkupLine($"[red]No build project found at {buildProject.EscapeMarkup()}.[/]");
            AnsiConsole.MarkupLine("[yellow]This repository has not been set up yet - run [cyan]autofallout setup[/] instead.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Repository Root: {workingDirectory.EscapeMarkup()}[/]");
        if (options.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow bold]Dry run - no files will be written.[/]");
        }
        AnsiConsole.WriteLine();

        var report = new MigrationReport();
        var workspace = new MigrationWorkspace(workingDirectory, options, report);

        // Resolved up front: the platform decides which packaging interface the build sources are
        // retargeted at, not just which root items get copied.
        var platform = ResolvePlatform(workingDirectory, options);

        // Asked before anything is written: the versions this tool ships with come off the Azure
        // DevOps feed, so they are a last resort rather than the answer.
        var versions = await MigrationPackageVersionResolver.ResolveAsync(workingDirectory, buildProject, options);

        MigrateBuildProject(buildProject, versions, workspace, report);
        MigrateBuildSources(buildDirectory, platform, workspace, report);
        MigrateToolManifest(workspace, report, options);
        RootItemMigrator.MigrateScripts(workspace, report);
        RootItemMigrator.MigrateConfigDirectory(workspace);
        RootItemMigrator.RemoveSecretScanningConfig(workspace);

        if (options.KeepRootItems)
        {
            report.Skip("azure-pipelines.yml, GitVersion.yml, global.json, nuget.config", "Left in place (--keep-root-items)");
        }
        else
        {
            RootItemMigrator.CopyDefaultRootItems(workspace, platform);
        }

        WarnIfGitHubFeedIsMissing(workingDirectory, platform, workspace, report);
        MigrateGitIgnore(workspace, report);

        if (options.RefreshPackages)
        {
            await RefreshPackagesAsync(buildProject, options, report, workspace);
        }

        RenderReport(report, workspace, options);

        return 0;
    }

    /// <summary>
    /// An explicit --platform always wins. Otherwise the platform comes from the feeds in the
    /// repository's nuget.config: a GitHub Packages feed means GitHub, an Azure Artifacts feed means
    /// Azure DevOps. Anything inconclusive falls back to Azure DevOps, which is where the
    /// Nuke-to-Fallout migration originated.
    /// </summary>
    private static BuildPlatform ResolvePlatform(string repositoryRoot, MigrationOptions options)
    {
        if (options.Platform.HasValue)
        {
            AnsiConsole.MarkupLine($"[grey]Platform: {options.Platform.Value} (from --platform)[/]");
            return options.Platform.Value;
        }

        var detected = PlatformDetector.DetectFromRepository(repositoryRoot, out var nugetConfigPath);

        if (detected.HasValue)
        {
            AnsiConsole.MarkupLine(
                $"[grey]Platform: {detected.Value} (detected from {Path.GetFileName(nugetConfigPath).EscapeMarkup()})[/]");
            return detected.Value;
        }

        var reason = nugetConfigPath == null
            ? "no nuget.config found"
            : "no recognisable feed in nuget.config";

        AnsiConsole.MarkupLine($"[yellow]Platform: {BuildPlatform.AzureDevOps} (default - {reason})[/]");
        AnsiConsole.MarkupLine("[grey]Pass --platform to choose explicitly.[/]");

        return BuildPlatform.AzureDevOps;
    }

    private static void MigrateBuildProject(string buildProject, MigrationPackageVersions versions,
        MigrationWorkspace workspace, MigrationReport report)
    {
        var original = workspace.ReadText(buildProject);
        if (original == null)
            return;

        try
        {
            // A repository that manages versions centrally keeps every version in
            // Directory.Packages.props, so the pins go there and the build project only gets the bare
            // Include - a Version on the PackageReference fails restore with NU1008.
            var buildDirectory = Path.GetDirectoryName(Path.GetFullPath(buildProject))!;
            var propsFile = CentralPackageManagement.FindPropsFile(buildDirectory, workspace.Root);
            var propsXml = propsFile == null ? null : workspace.ReadText(propsFile);
            var centrallyManaged = propsXml != null && CentralPackageManagement.IsEnabled(propsXml);

            if (centrallyManaged)
            {
                MigratePackageVersions(propsFile!, propsXml!, versions, workspace);
            }

            var result = BuildProjectMigrator.Migrate(original, versions, centrallyManaged);

            if (result.Notes.Count == 0)
            {
                report.Skip(workspace.Relative(buildProject), "Already migrated");
                return;
            }

            workspace.WriteText(buildProject, result.Xml, string.Join("; ", result.Notes));
        }
        catch (Exception ex)
        {
            report.Warn(workspace.Relative(buildProject), $"Could not be migrated: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes the resolved versions into Directory.Packages.props as PackageVersion items. The build
    /// project's references are left bare by <see cref="BuildProjectMigrator"/>.
    /// </summary>
    private static void MigratePackageVersions(string propsFile, string propsXml, MigrationPackageVersions versions,
        MigrationWorkspace workspace)
    {
        var pins = new[]
        {
            (PackageId: MigrationPackageVersionResolver.FalloutCommonPackageId, Version: versions.FalloutCommon),
            (PackageId: MigrationPackageVersionResolver.ComponentsPackageId, Version: versions.Components),
            // BuildProjectMigrator writes this reference bare like the others, so it needs a
            // PackageVersion here too or restore fails with NU1010.
            (PackageId: "NuGet.Packaging", Version: MigrationDefaults.NuGetPackagingVersion)
        };

        var xml = propsXml;
        var notes = new List<string>();

        foreach (var pin in pins)
        {
            var edit = CentralPackageManagement.UpsertPackageVersion(xml, pin.PackageId, pin.Version);
            if (!edit.Changed)
                continue;

            xml = edit.Xml;
            notes.Add($"Pinned {pin.PackageId} to {pin.Version}");
        }

        if (notes.Count == 0)
            return;

        workspace.WriteText(propsFile, xml, string.Join("; ", notes));
    }

    private static void MigrateBuildSources(string buildDirectory, BuildPlatform platform, MigrationWorkspace workspace,
        MigrationReport report)
    {
        var sources = Directory.GetFiles(buildDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(buildDirectory, file));

        foreach (var source in sources)
        {
            var original = workspace.ReadText(source);
            if (original == null)
                continue;

            var details = new List<string>();

            var migrated = NamespaceMigrator.Rewrite(original);
            if (!string.Equals(original, migrated, StringComparison.Ordinal))
            {
                details.Add("Rewrote Nuke namespaces and types to Fallout");
            }

            migrated = PackageInterfaceMigrator.Rewrite(migrated, platform, out var packageNotes);
            details.AddRange(packageNotes);

            migrated = SecretScanningMigrator.Rewrite(migrated, out var secretScanningNotes);
            details.AddRange(secretScanningNotes);

            if (details.Count > 0)
            {
                workspace.WriteText(source, migrated, string.Join("; ", details));
            }

            // The Nuke IPackage pushed to GitHub Packages whatever the platform, so retargeting an
            // Azure DevOps build changes where its packages land. That is the intended destination
            // for the platform, but it is not something to change quietly.
            if (packageNotes.Count > 0 && platform == BuildPlatform.AzureDevOps)
            {
                report.Warn(workspace.Relative(source),
                    "ReleasePackage now pushes to Azure DevOps Artifacts instead of GitHub Packages. Re-run with --platform GitHubActions if that is wrong");
            }

            foreach (var leftover in SecretScanningMigrator.FindLeftovers(migrated))
            {
                report.Warn(workspace.Relative(source),
                    $"Still references {leftover}, which was removed with secret scanning - delete it by hand");
            }

            foreach (var literal in NamespaceMigrator.FindUnescapedPathLiterals(migrated))
            {
                report.Warn(workspace.Relative(source),
                    $"{literal} is not a verbatim string - the backslashes are read as escape sequences. Prefix it with @");
            }
        }
    }

    private static bool IsGenerated(string buildDirectory, string file)
    {
        var relative = Path.GetRelativePath(buildDirectory, file);
        return relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void MigrateToolManifest(MigrationWorkspace workspace, MigrationReport report, MigrationOptions options)
    {
        var manifestPath = workspace.Resolve(".config", "dotnet-tools.json");
        var original = workspace.ReadText(manifestPath);
        if (original == null)
            return;

        try
        {
            var migrated = RootItemMigrator.MigrateToolManifest(original, options.ResolvedGlobalToolVersion, out var changed);
            if (!changed || migrated == null)
                return;

            workspace.WriteText(manifestPath, migrated,
                $"Replaced the legacy global tool with {MigrationDefaults.GlobalToolPackageId} {options.ResolvedGlobalToolVersion}");
        }
        catch (Exception ex)
        {
            report.Warn(workspace.Relative(manifestPath), $"Could not be migrated: {ex.Message}");
        }
    }

    /// <summary>
    /// An existing nuget.config is never replaced - it carries the repository's own feeds and
    /// credentials - so a GitHub repository that predates the GitHub Packages feed being added to the
    /// default keeps a config that cannot restore Automation.Fallout.Components.
    /// </summary>
    private static void WarnIfGitHubFeedIsMissing(string repositoryRoot, BuildPlatform platform,
        MigrationWorkspace workspace, MigrationReport report)
    {
        if (platform != BuildPlatform.GitHubActions)
            return;

        var nugetConfig = PlatformDetector.FindNuGetConfig(repositoryRoot);
        if (nugetConfig == null || !File.Exists(nugetConfig))
            return;

        if (PlatformDetector.NamesGitHubPackagesFeed(File.ReadAllText(nugetConfig)))
            return;

        report.Warn(workspace.Relative(nugetConfig),
            "Names no GitHub Packages feed, so Automation.Fallout.Components cannot be restored. Add https://nuget.pkg.github.com/<owner>/index.json with %GITHUB_TOKEN% credentials");
    }

    private static void MigrateGitIgnore(MigrationWorkspace workspace, MigrationReport report)
    {
        var gitignorePath = workspace.Resolve(".gitignore");
        var original = workspace.ReadText(gitignorePath) ?? string.Empty;

        var additions = NuGetPackageInstaller.BuildGitIgnoreAdditions(original);
        if (additions.Length == 0)
            return;

        workspace.WriteText(gitignorePath, original + additions, "Added Fallout and Rider ignore entries");
    }

    private static async Task RefreshPackagesAsync(string buildProject, MigrationOptions options, MigrationReport report,
        MigrationWorkspace workspace)
    {
        if (options.DryRun)
        {
            report.Skip(workspace.Relative(buildProject), "Would refresh packages to the newest feed versions (--refresh-packages)");
            return;
        }

        AnsiConsole.MarkupLine("\n[yellow bold]Refreshing packages to the newest feed versions...[/]\n");

        // Components first: it is what constrains the Fallout.Common floor.
        if (options.ComponentsVersion == null)
        {
            await NuGetPackageInstaller.AddPackageToProjectAsync(buildProject, "Automation.Fallout.Components",
                repositoryRoot: workspace.Root, fallbackVersion: MigrationDefaults.ComponentsVersion);
        }

        if (options.FalloutCommonVersion == null)
        {
            await NuGetPackageInstaller.AddPackageToProjectAsync(buildProject, "Fallout.Common",
                repositoryRoot: workspace.Root, fallbackVersion: MigrationDefaults.FalloutCommonVersion);
        }

        report.Add(workspace.Relative(buildProject), "Refreshed package versions from the configured feeds");
    }

    private static void RenderReport(MigrationReport report, MigrationWorkspace workspace, MigrationOptions options)
    {
        AnsiConsole.WriteLine();

        if (report.Changes.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]Nothing to migrate - this repository is already on Fallout.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Action[/]")
            .AddColumn("[bold]File[/]")
            .AddColumn("[bold]Detail[/]");

        foreach (var change in report.Changes)
        {
            table.AddRow(
                Describe(change.Kind),
                change.File.EscapeMarkup(),
                change.Detail.EscapeMarkup());
        }

        AnsiConsole.Write(table);

        if (options.DryRun)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Dry run complete - {report.ChangeCount} file(s) would change. Re-run without [cyan]--dry-run[/] to apply.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"\n[green bold]Migration complete - {report.ChangeCount} file(s) changed.[/]");

        if (workspace.BackupDirectory != null)
        {
            AnsiConsole.MarkupLine($"[grey]Originals backed up to {workspace.Relative(workspace.BackupDirectory).EscapeMarkup()}[/]");
        }

        if (report.HasWarnings)
        {
            AnsiConsole.MarkupLine("\n[yellow bold]Review the warnings above - they need a human.[/]");
        }

        AnsiConsole.MarkupLine("\n[yellow]Next steps:[/]");
        AnsiConsole.MarkupLine("  1. Run [cyan]dotnet build build/_build.csproj[/] - it fails faster than the full solution");
        AnsiConsole.MarkupLine("  2. Run [cyan]dotnet run --project build/_build.csproj -- --help[/] to confirm the Fallout engine loads");
        AnsiConsole.MarkupLine("  3. Diff [cyan]azure-pipelines.yml[/] against your backup if the pipeline was customised");
    }

    private static string Describe(MigrationChangeKind kind) => kind switch
    {
        MigrationChangeKind.Added => "[green]added[/]",
        MigrationChangeKind.Changed => "[green]changed[/]",
        MigrationChangeKind.Removed => "[red]removed[/]",
        MigrationChangeKind.Skipped => "[grey]skipped[/]",
        MigrationChangeKind.Warning => "[yellow]warning[/]",
        _ => kind.ToString().ToLowerInvariant()
    };
}
