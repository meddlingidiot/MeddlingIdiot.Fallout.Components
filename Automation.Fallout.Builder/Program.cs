using System.CommandLine;
using System.Text;
using Automation.Fallout.Builder.Commands;
using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;

namespace Automation.Fallout.Builder;

class Program
{
    static int Main(string[] args)
    {
        // The Vault Boy banner is braille - a non-UTF-8 code page renders it as question marks.
        TrySetUtf8Output();

        var rootCommand = new RootCommand("Automation Fallout Builder - Simplify Fallout build setup");

        var setupCommand = new Command("setup", "Setup Fallout build for the current project");
        setupCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            return await SetupCommand.ExecuteAsync();
        });

        rootCommand.Subcommands.Add(setupCommand);
        rootCommand.Subcommands.Add(BuildMigrateCommand());

        return rootCommand.Parse(args).Invoke();
    }

    private static void TrySetUtf8Output()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // No console attached (output redirected to a pipe on some hosts) - the art degrades, nothing else does.
        }
    }

    private static Command BuildMigrateCommand()
    {
        var pathOption = new Option<string?>("--path", "-p")
        {
            Description = "Repository root to migrate. Defaults to the repository containing the current directory."
        };

        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report what would change without writing any file."
        };

        var noBackupOption = new Option<bool>("--no-backup")
        {
            Description = "Do not copy originals into .fallout/Backup before rewriting them."
        };

        var keepRootItemsOption = new Option<bool>("--keep-root-items")
        {
            Description = "Leave azure-pipelines.yml, GitVersion.yml, global.json and nuget.config untouched."
        };

        var platformOption = new Option<BuildPlatform?>("--platform")
        {
            Description = "CI platform whose root items get refreshed. " +
                          "Detected from the feeds in the repository's nuget.config when omitted."
        };

        var refreshPackagesOption = new Option<bool>("--refresh-packages")
        {
            Description = "After repairing the project, run 'dotnet add package' to move to the newest feed versions."
        };

        var falloutVersionOption = new Option<string?>("--fallout-version")
        {
            Description = $"Fallout.Common version to pin. Default: {MigrationDefaults.FalloutCommonVersion}."
        };

        var componentsVersionOption = new Option<string?>("--components-version")
        {
            Description = $"{MigrationPackageVersionResolver.ComponentsPackageId} version to pin. Default: {MigrationDefaults.ComponentsVersion}."
        };

        var globalToolVersionOption = new Option<string?>("--global-tool-version")
        {
            Description = $"Fallout.Cli version for .config/dotnet-tools.json. Default: {MigrationDefaults.GlobalToolVersion}."
        };

        var migrateCommand = new Command("migrate",
            "Repair a repository that was renamed from Nuke to Fallout but no longer compiles");

        migrateCommand.Options.Add(pathOption);
        migrateCommand.Options.Add(dryRunOption);
        migrateCommand.Options.Add(noBackupOption);
        migrateCommand.Options.Add(keepRootItemsOption);
        migrateCommand.Options.Add(platformOption);
        migrateCommand.Options.Add(refreshPackagesOption);
        migrateCommand.Options.Add(falloutVersionOption);
        migrateCommand.Options.Add(componentsVersionOption);
        migrateCommand.Options.Add(globalToolVersionOption);

        migrateCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var options = new MigrationOptions
            {
                Path = parseResult.GetValue(pathOption),
                DryRun = parseResult.GetValue(dryRunOption),
                NoBackup = parseResult.GetValue(noBackupOption),
                KeepRootItems = parseResult.GetValue(keepRootItemsOption),
                Platform = parseResult.GetValue(platformOption),
                RefreshPackages = parseResult.GetValue(refreshPackagesOption),
                FalloutCommonVersion = parseResult.GetValue(falloutVersionOption),
                ComponentsVersion = parseResult.GetValue(componentsVersionOption),
                GlobalToolVersion = parseResult.GetValue(globalToolVersionOption)
            };

            return await MigrateCommand.ExecuteAsync(options);
        });

        return migrateCommand;
    }
}
