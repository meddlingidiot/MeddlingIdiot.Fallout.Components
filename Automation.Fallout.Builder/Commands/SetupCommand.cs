using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;
using Automation.Fallout.Builder.Ui;
using Spectre.Console;

namespace Automation.Fallout.Builder.Commands;

public static class SetupCommand
{
    public static async Task<int> ExecuteAsync()
    {
        FalloutBanner.Render("Fallout Setup", Color.Blue);

        AnsiConsole.MarkupLine("[bold]Welcome to Automation Fallout Builder Setup![/]");
        AnsiConsole.MarkupLine("This tool will help you configure your Fallout build pipeline.\n");

        var workingDirectory = RepositoryLocator.FindRepositoryRoot();
        if (workingDirectory == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find repository root. Please run this command from within a git repository or directory containing a .sln or .slnx file.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Repository Root: {workingDirectory.EscapeMarkup()}[/]\n");

        // Step 1: Ensure Fallout is installed
        if (!await NuGetPackageInstaller.InstallFalloutGlobalToolAsync())
        {
            AnsiConsole.MarkupLine("[red]Failed to install Fallout. Please install it manually and try again.[/]");
            return 1;
        }

        // Step 2: Update Fallout global tool
        await NuGetPackageInstaller.UpdateFalloutGlobalToolAsync();

        // Step 3: Run Fallout setup if not already done
        var buildDir = Path.Combine(workingDirectory, "build");
        if (!Directory.Exists(buildDir))
        {
            if (!await NuGetPackageInstaller.RunFalloutSetupAsync(workingDirectory))
            {
                AnsiConsole.MarkupLine("[red]Fallout setup failed. Please check the errors above.[/]");
                return 1;
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Build directory already exists, skipping Fallout setup[/]\n");
        }

        // Step 4: Find build project
        var buildCsproj = Path.Combine(buildDir, "_build.csproj");
        if (!File.Exists(buildCsproj))
        {
            AnsiConsole.MarkupLine($"[red]Build project not found at {buildCsproj.EscapeMarkup()}[/]");
            return 1;
        }

        // Step 5: Upgrade build project to net10.0
        await NuGetPackageInstaller.UpgradeProjectTargetFrameworkAsync(buildCsproj);

        // Step 6: Add PackageDownloads
        await NuGetPackageInstaller.AddPackageDownloadAsync(buildCsproj, "GitVersion.Tool", "6.8.2");
        await NuGetPackageInstaller.AddPackageDownloadAsync(buildCsproj, "ReportGenerator", "5.5.11");

        // Step 7: Get user configuration
        var config = await PromptForConfigurationAsync(workingDirectory);

        // Step 8: Copy default root items for the chosen platform. The nuget.config among them
        // carries the package feeds, so it has to be in place before any version is looked up.
        await NuGetPackageInstaller.CopyDefaultRootItemsAsync(workingDirectory, config.Platform);

        // Step 9: Install required packages
        var packagesInstalled = await InstallRequiredPackagesAsync(buildCsproj, workingDirectory);

        // Step 10: Install local tools
        await InstallLocalToolsAsync(workingDirectory);

        // Step 11: Delete Configuration.cs if it exists
        var configurationCs = Path.Combine(buildDir, "Configuration.cs");
        await NuGetPackageInstaller.DeleteFileIfExistsAsync(configurationCs);

        // Step 12: Update .gitignore
        await NuGetPackageInstaller.UpdateGitIgnoreAsync(workingDirectory);

        // Step 13: Generate Build.cs
        await GenerateBuildFileAsync(buildDir, config);

        AnsiConsole.MarkupLine("\n[green bold]Setup completed successfully![/]");

        if (!packagesInstalled)
        {
            AnsiConsole.MarkupLine(
                "\n[yellow]Not every package could be added to build/_build.csproj - see the errors above. " +
                "The build will not compile until they are referenced.[/]");
        }

        AnsiConsole.MarkupLine("\n[yellow]Next steps:[/]");
        AnsiConsole.MarkupLine("  1. Review the generated build/Build.cs file");
        AnsiConsole.MarkupLine("  2. Run [cyan]fallout --help[/] to see available targets");
        AnsiConsole.MarkupLine("  3. Run [cyan]fallout[/] to execute the default build");

        return 0;
    }

    private static async Task<BuildConfiguration> PromptForConfigurationAsync(string workingDirectory)
    {
        var config = new BuildConfiguration();

        // Select CI platform first - it decides the build base class, the packaging interface
        // and which root items land in the repository.
        var platformChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Which CI platform is this project built on?[/]")
                .AddChoices(
                    "Azure DevOps - Azure Pipelines, packages pushed to Azure Artifacts feeds",
                    "GitHub Actions - GitHub workflow, packages pushed to GitHub Packages"));

        config.Platform = platformChoice.StartsWith("GitHub")
            ? BuildPlatform.GitHubActions
            : BuildPlatform.AzureDevOps;

        AnsiConsole.MarkupLine($"\n[green]Platform: {config.Platform}[/]\n");

        // Select build type
        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var buildType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select the build type for your project:[/]")
                .AddChoices(builds.Select(b => $"{b.Name} - {b.Description}")));

        var selectedBuildName = buildType.Split(" - ")[0];
        config.BuildType = selectedBuildName;
        var selectedBuild = builds.First(b => b.Name == selectedBuildName);

        AnsiConsole.MarkupLine($"\n[green]Selected: {selectedBuildName.EscapeMarkup()}[/]\n");

        // Ask about warnings
        config.BreakBuildOnWarnings = AnsiConsole.Confirm(
            "[yellow]Break build on warnings?[/]",
            defaultValue: true);

        // Ask about code coverage if tests are included
        if (selectedBuild.RequiresTests)
        {
            config.EnableCodeCoverage = AnsiConsole.Confirm(
                "[yellow]Enable code coverage requirement?[/]",
                defaultValue: false);

            if (config.EnableCodeCoverage)
            {
                config.MinCodeCoverage = AnsiConsole.Prompt(
                    new TextPrompt<int>("[yellow]Minimum code coverage percentage (0-100):[/]")
                        .DefaultValue(80)
                        .Validate(value =>
                        {
                            return value switch
                            {
                                < 0 => ValidationResult.Error("[red]Coverage must be at least 0[/]"),
                                > 100 => ValidationResult.Error("[red]Coverage cannot exceed 100[/]"),
                                _ => ValidationResult.Success()
                            };
                        }));
            }
        }

        // Ask about Velopack configuration if needed
        if (selectedBuild.RequiresVelopack)
        {
            AnsiConsole.MarkupLine("\n[yellow]Velopack Configuration:[/]");

            config.VelopackProjectName = ProjectDiscovery.PromptForProject(
                workingDirectory,
                "[yellow]Select the Velopack project (the executable project):[/]");

            // Scan for .ico files in the project directory
            var iconFiles = Directory.GetFiles(workingDirectory, "*.ico", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(workingDirectory, f))
                .ToList();

            if (iconFiles.Count > 0)
            {
                var choices = new List<string> { "None (use default)" };
                choices.AddRange(iconFiles);
                choices.Add("Enter custom path...");

                var iconChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]Select an icon for Velopack:[/]")
                        .AddChoices(choices));

                if (iconChoice == "Enter custom path...")
                {
                    config.VelopackIconPath = AnsiConsole.Ask<string>(
                        "[yellow]Enter the icon path (relative to solution root):[/]");
                }
                else if (iconChoice != "None (use default)")
                {
                    config.VelopackIconPath = iconChoice;
                }
            }
            else
            {
                if (AnsiConsole.Confirm("[yellow]Do you have a custom icon for Velopack?[/]", defaultValue: false))
                {
                    config.VelopackIconPath = AnsiConsole.Ask<string>(
                        "[yellow]Enter the icon path (relative to solution root):[/]");
                }
            }
        }

        return config;
    }

    /// <summary>
    /// References the two packages a Fallout build needs. Returns false when either could not be
    /// added, so the caller can say so rather than leave a project that will not compile.
    /// </summary>
    private static async Task<bool> InstallRequiredPackagesAsync(string buildCsproj, string workingDirectory)
    {
        AnsiConsole.MarkupLine("\n[yellow bold]Installing required NuGet packages...[/]\n");

        // The components package moves independently of the CLI, so setup takes its newest release.
        var componentsAdded = await NuGetPackageInstaller.AddPackageToProjectAsync(
            buildCsproj,
            "Automation.Fallout.Components",
            repositoryRoot: workingDirectory,
            fallbackVersion: MigrationDefaults.ComponentsVersion);

        // Fallout.Common is pinned instead: it has to match the Fallout CLI this tool installs.
        var falloutCommonAdded = await NuGetPackageInstaller.AddPackageToProjectAsync(
            buildCsproj,
            "Fallout.Common",
            MigrationDefaults.FalloutCommonVersion,
            workingDirectory);

        // Overrides the NuGet.Packaging that arrives transitively with Fallout.Common so the build
        // host's NuGet.Frameworks matches the SDK pinned in global.json. See MigrationDefaults.
        var nugetPackagingAdded = await NuGetPackageInstaller.AddPackageToProjectAsync(
            buildCsproj,
            "NuGet.Packaging",
            MigrationDefaults.NuGetPackagingVersion,
            workingDirectory);

        return componentsAdded && falloutCommonAdded && nugetPackagingAdded;
    }

    private static async Task InstallLocalToolsAsync(string workingDirectory)
    {
        AnsiConsole.MarkupLine("\n[yellow bold]Installing local tools...[/]\n");

        // Install GitVersion.Tool 6.8.2
        await NuGetPackageInstaller.InstallToolLocallyAsync(
            workingDirectory,
            "GitVersion.Tool",
            "6.8.2");
    }

    private static async Task GenerateBuildFileAsync(string buildDir, BuildConfiguration config)
    {
        AnsiConsole.MarkupLine("\n[yellow bold]Generating Build.cs file...[/]\n");

        var builds = DefaultBuildDiscovery.GetAvailableBuilds();
        var selectedBuild = builds.First(b => b.Name == config.BuildType);

        var buildFileContent = BuildFileGenerator.GenerateBuildFile(config, selectedBuild);
        var buildFilePath = Path.Combine(buildDir, "Build.cs");

        await File.WriteAllTextAsync(buildFilePath, buildFileContent);

        AnsiConsole.MarkupLine($"[green]Build.cs generated at: {buildFilePath.EscapeMarkup()}[/]");
    }
}
