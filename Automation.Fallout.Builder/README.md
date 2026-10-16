# Automation.Fallout.Builder

A global .NET tool that simplifies the setup, configuration and migration of Fallout build pipelines across multiple projects.

## Features

- **Interactive Setup**: Guided prompts using Spectre.Console for easy configuration
- **Multiple Build Types**: Choose from pre-configured build templates:
  - **CompileBuild**: Basic compilation
  - **TestBuild**: Compilation, testing, and code coverage
  - **PackageBuild**: Full pipeline with NuGet package creation
  - **VelopackBuild**: Application deployment with Velopack
  - **PackageAndVelopackBuild**: Complete pipeline with both NuGet and Velopack
- **Automated Dependencies**: Automatically installs required NuGet packages and tools
- **Configurable Options**:
  - Break build on warnings
  - Minimum code coverage requirements
  - Velopack configuration
  
Additional automation (latest updates):
- Upgrades the generated Fallout build project to target `net10.0`
- Adds `PackageDownload` entries for `GitVersion.Tool (6.8.2)` and `ReportGenerator (5.5.11)`
- Installs `GitVersion.Tool` locally (v6.8.2)
- Copies default root config files to your repository: `nuget.config`, `GitVersion.yml`, `global.json`, `azure-pipelines.yml`

## Installation

Install as a global .NET tool:

```bash
dotnet tool install --global Automation.Fallout.Builder
```

Or install from a local package:

```bash
dotnet tool uninstall -g Automation.Fallout.Builder
dotnet pack ./Automation.Fallout.Builder/Automation.Fallout.Builder.csproj
dotnet tool install --global --add-source "./Automation.Fallout.Builder/nupkg" Automation.Fallout.Builder
```

## Commands

| Command | Purpose |
| --- | --- |
| `autofallout setup` | Scaffold a Fallout build in a repository that does not have one |
| `autofallout migrate` | Repair a repository that was renamed from Nuke to Fallout but no longer compiles |

## Usage

Navigate to your project root directory and run:

```bash
autofallout setup
```

The tool will guide you through:

1. **Build Type Selection**: Choose the appropriate build template for your project
2. **Quality Gates**: Configure whether warnings break the build
3. **Code Coverage**: Set minimum coverage thresholds (if applicable)
4. **Velopack Settings**: Configure deployment options (if applicable)

The tool will:
- Install the Fallout global tool (if not already installed)
- Update the Fallout global tool to the latest available
- Run `fallout :setup` to create the build infrastructure (if `build/` doesn't exist)
- Upgrade the generated build project to target `net10.0`
- Add required NuGet packages to your build project (`Automation.Fallout.Components` and `Fallout.Common`)
- Add `PackageDownload` entries for `GitVersion.Tool (6.8.2)` and `ReportGenerator (5.5.11)`
- Install local tools (installs `GitVersion.Tool` v6.8.2)
- Copy default root items: `nuget.config`, `GitVersion.yml`, `global.json`, `azure-pipelines.yml`
- Remove the legacy `Configuration.cs` file in `build/` if present
- Generate a customized `Build.cs` file based on your selections

## Migrating an existing Nuke repository

A straight `Nuke` → `Fallout` find/replace leaves a repository that does not compile. Run:

```bash
autofallout migrate
```

from anywhere inside the repository (or pass `--path`). It repairs, in order:

**`build/_build.csproj`**
- Corrects the `Fallout.Common` version. This is the one that actually breaks the build: the rename keeps the old `Nuke.Common` version (e.g. `10.1.0`), which resolves an *unrelated* 10.x `Fallout.Common` and fails restore with `NU1605: Detected package downgrade`.
- Renames `Automation.Nuke.Components` → `Automation.Fallout.Components` and re-pins it, since the Nuke-era version does not exist for the Fallout package.
- Renames the `Nuke*` MSBuild properties (`NukeRootDirectory` → `FalloutRootDirectory`, and friends).
- Drops any leftover `Nuke.*` package references.
- Upgrades `TargetFramework` to `net10.0` and adds the `GitVersion.Tool` / `ReportGenerator` `PackageDownload` entries.

**`build/**/*.cs` (including custom components)**
- Rewrites `Nuke.Common.*` → `Fallout.Common.*`, `NukeBuild` → `FalloutBuild`, `INukeBuild` → `IFalloutBuild`.
- Rewrites `Nuke.Common.ProjectModel` → `Fallout.Solutions`. This is the namespace that does *not* map one-to-one: it moved out from under `Common` entirely, so a blind rename produces `Fallout.Common.ProjectModel` and fails with `CS0234`. The migrator repairs that spelling too.
- Removes `IScanForSecrets` from the build class, along with any `BreakBuildOnSecretLeaks` override or `ScanForSecrets` dependency, and warns about any reference it cannot remove on its own.
- Warns about non-verbatim string literals holding Windows paths, where `\assets\reset-password.ico` silently compiles as `\a` + `\r` escape sequences.

**Everything else**
- Replaces `nuke.globaltool` (and the now-unlisted `fallout.globaltool`) with `fallout.cli` in `.config/dotnet-tools.json`.
- Points `build.ps1` / `build.sh` / `build.cmd` at `.fallout/temp`.
- Carries `parameters.json` over from `.nuke/` and deletes the legacy directory.
- Refreshes `azure-pipelines.yml`, `GitVersion.yml`, `global.json` and `nuget.config` from the Fallout defaults.
- Deletes `.gitleaks.toml` / `gitleaks.toml` — secret scanning is no longer part of Fallout.
- Adds the Fallout and Rider entries to `.gitignore`.

Originals are copied to `.fallout/Backup/migrate-<timestamp>/` before anything is rewritten. The command is idempotent — a second run reports no changes.

### Options

| Option | Description |
| --- | --- |
| `--path`, `-p` | Repository root. Defaults to the repository containing the current directory. |
| `--dry-run` | Print the change table without writing anything. |
| `--no-backup` | Skip the `.fallout/Backup` copies. |
| `--keep-root-items` | Leave `azure-pipelines.yml`, `GitVersion.yml`, `global.json` and `nuget.config` alone. Use this when the pipeline is customised. |
| `--refresh-packages` | After repairing the project, run `dotnet add package` to move to the newest feed versions. |
| `--fallout-version` | Pin `Fallout.Common` explicitly. |
| `--components-version` | Pin `Automation.Fallout.Components` explicitly. |
| `--global-tool-version` | `Fallout.Cli` version for the tool manifest. |

### Recommended flow

```bash
autofallout migrate --dry-run
autofallout migrate
dotnet build build/_build.csproj
```

Build the `_build` project on its own first — every migration error lands there, and it fails far faster than the full solution.

### Non-interactive (CI) usage

You can re-run setup in CI or scripts to enforce conventions without prompts by answering with defaults when asked. For fully non-interactive flows, pre-create a `_build.csproj` that already references `Automation.Fallout.Components` and commit your chosen `Build.cs` so `autofallout setup` becomes a no-op.

`autofallout migrate` is fully non-interactive already and is safe to run in a pipeline.

## Build Types

### CompileBuild
Basic compilation pipeline with:
- Version display
- Code compilation

**Use Case**: Simple libraries or projects that only need compilation verification

### TestBuild
Testing pipeline with:
- All CompileBuild features
- Unit test execution
- Code coverage reporting

**Use Case**: Libraries with test suites

### PackageBuild
NuGet packaging pipeline with:
- All TestBuild features
- Changelog updates
- NuGet package creation
- Git tagging

**Use Case**: Libraries published to NuGet

### VelopackBuild
Application deployment pipeline with:
- All TestBuild features
- Velopack installer creation
- Git tagging

**Use Case**: Desktop applications using Velopack for updates

### PackageAndVelopackBuild
Complete pipeline with:
- All features from TestBuild
- NuGet package creation
- Velopack installer creation
- Git tagging

**Use Case**: Complex projects requiring both library and application distribution

## Example

```bash
cd MyProject
autofallout setup

# Follow the prompts:
# - Select build type: TestBuild
# - Break build on warnings? Yes
# - Enable code coverage requirement? Yes
# - Minimum code coverage: 80

# Run your build
nuke
```

## Roll your own step (custom component)

After the tool scaffolds your `build/` project, you can add your own reusable step as a Fallout component. Components are just small interfaces that contribute `Target`s and optional parameters. Implement your interface on the `Build` class to include the step.

1) Create a custom component interface in your build project (or a shared library):

```csharp
// build/Components/IMyLint.cs
using Fallout.Common;
using Fallout.Common.IO;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

namespace Automation.Fallout.Components.Components; // keep namespace style consistent with other components

public interface IMyLint : IFalloutBuild
{
    AbsolutePath Src => RootDirectory / "src"; // example input

    Target MyLint => _ => _
        .Description("Run custom lint")
        .Executes(() =>
        {
            // Replace with your tool of choice
            DotNet($"format {Src} --verify-no-changes");
        });
}
```

2) Wire it into your `Build` by implementing the interface and (optionally) adding dependencies:

```csharp
// build/Build.cs
using Fallout.Common;
using Automation.Fallout.Components;
using Automation.Fallout.Components.Components;

public class Build : AzurePipelinesBuild, IShowVersion, IClean, IRestore, ICompile, ITest, IMyLint
{
    public new static int Main() => Execute<Build>(x => ((ITest)x).Test);

    // Optionally make your main pipeline depend on your custom step
    Target Default => _ => _
        .DependsOn(((IMyLint)this).MyLint)
        .DependsOn(((ITest)this).Test);
}
```

3) Add parameters to your component by composing with existing `IHas*` contracts. For example, to use solution/configuration values:

```csharp
public interface IMyLint : IFalloutBuild, IHasSolution, IHasConfiguration
{
    Target MyLint => _ => _
        .Executes(() =>
        {
            Serilog.Log.Information("Linting {Solution} in {Configuration}", Solution, Configuration);
        });
}
```

That’s it—your step is now first-class and can be shared across builds by moving the interface into a common library.

## Requirements

- .NET SDK 8.0 or higher to run this tool (`Automation.Fallout.Builder`)
- .NET SDK 10.0 to build and run the generated Fallout build (the setup upgrades `_build.csproj` to `net10.0`)
- Git (for version control features)
- Fallout.Cli (installed/updated automatically by this tool; exposes the `fallout` command)
- GitVersion.Tool (installed automatically as a local tool)

## Generated Project Structure

After running setup, your project will have:

```
MyProject/
├── build/
│   ├── Build.cs              # Generated build script
│   ├── _build.csproj         # Build project with dependencies (targets net10.0)
│   └── .fallout              # Fallout configuration
├── nuget.config              # Default NuGet sources configuration
├── GitVersion.yml            # Default GitVersion configuration
├── global.json               # Pins the .NET SDK version the repository builds with
├── azure-pipelines.yml       # Sample Azure Pipelines YAML
├── .config/
│   └── dotnet-tools.json     # Local tool manifest
└── build.cmd / build.sh      # Build entry points
```

## Customization

After generation, you can customize the `build/Build.cs` file to:
- Override default parameter values
- Add custom targets
- Extend component behavior
- Add project-specific logic

See “Roll your own step” above for a full example of creating your own component.

## Troubleshooting

**Fallout not found**: Ensure .NET global tools are in your PATH
```bash
export PATH="$PATH:$HOME/.dotnet/tools"  # Linux/Mac
# or add %USERPROFILE%\.dotnet\tools to PATH on Windows
```

**Package not found**: Ensure the Automation.Fallout.Components package is published to your NuGet feed

**Permission errors**: Run with appropriate permissions or use `--global` flag for tool installation

**Build fails targeting net10.0**: Install the .NET 10 SDK so the generated `_build.csproj` can compile.

## Development

To build and test locally:

```bash
cd Automation.Fallout.Builder
dotnet build
dotnet pack
dotnet tool install --global --add-source ./nupkg Automation.Fallout.Builder
```

## Contributing

This tool is designed to work with the Automation.Nuke.Components library. Ensure both are kept in sync when adding new features or build types.

## License

[Your License Here]
