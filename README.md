# Automation.Fallout.Components

> **Renamed, and no longer a mirror.** This repository is now master for the MeddlingIdiot
> line of these packages, published to nuget.org:
>
> | Was (private feeds) | Now (nuget.org) |
> |---|---|
> | `Automation.Fallout.Components` | `MeddlingIdiot.Fallout.Components` |
> | `Automation.Fallout.Builder` | `MeddlingIdiot.Fallout.Builder` |
>
> The ids changed because the repository this was forked from still publishes the old ones to
> its own feed, and a single id served by two feeds from two diverged codebases resolves to
> whichever version number happens to be higher. Assembly names, namespaces and the
> `autofallout` command are unchanged, so consuming repositories need a new
> `PackageReference` id and nothing else.

A **convention-based build system for .NET, delivered as a NuGet package.** Build logic lives in
versioned components that repositories *compose*, not in scripts that repositories *copy*. A
consuming repo holds a ten-line `Build.cs` declaring which components apply; everything those
components do — compile, test, coverage gates, secret scanning, packaging, tagging, release —
comes from this package and is upgraded by bumping a version.

Built on [Fallout](https://github.com/ChrisonSimtian/Fallout) (the maintained hard-fork of NUKE).
Designed to standardize 80+ internal repositories.

> **Migrated from NUKE.** This repository previously targeted NUKE, which is no longer maintained.
> See [Migration from NUKE](#-migration-from-nuke) for what changed and how to update a consuming repo.

---

## 🧭 What this is — and what it isn't

**It is convention-based, not template-based.** The difference is not cosmetic; it determines what
happens when a build rule changes.

| | Template-based | **This system** |
|---|---|---|
| Where the logic lives | Copied into each repo | A NuGet package the repo references |
| Adding a build step | Edit N repos, or regenerate and re-merge | Add one interface to `Build.cs` |
| Fixing a bug in a step | Re-apply the template across N repos, diff the drift | Publish a version, repos bump it |
| What a repo owns | The whole script | The composition, plus deliberate overrides |
| Drift over time | Inevitable — every repo is a fork | Structural — repos differ only where they declared a difference |

The single distinguishing question: **when a build rule changes, where does the fix land?** Here it
lands in one package. That is the definition of convention over template.

**It is not a CI/CD platform.** Azure DevOps and GitHub Actions remain the platform — they schedule
the work, provide the agents, and hold the credentials. The YAML in a consuming repo is a thin shim
that installs the SDK and calls `build.cmd`. Everything meaningful — order, gates, versioning,
publishing — is C# in these components, which is why the same build runs identically on a developer
laptop and on an agent. A fair description of the whole system is *a standardized build pipeline
distributed as a library, with a scaffolding tool to bootstrap it.*

**Where templating does appear**, it is confined to the bootstrap: [`autofallout setup`](#-bootstrap-the-autofallout-tool)
generates a starting `Build.cs`, and `DefaultBuilds/` offers ready-made compositions. Those are
starting points — one-time text generation. They are not how behavior is delivered or updated.

---

## ⚡ The model in 30 seconds

A component is an interface carrying a `Target` as a default-implemented property:

```csharp
public interface ICompile : IFalloutBuild, IHasSolution, IHasConfiguration, IHasGitVersion, IHasTests
{
    Target Compile => t => t
        .DependsOn<IRestore>()
        .Description("Compile Solution")
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .SetTreatWarningsAsErrors(BreakBuildOnWarnings)
            .SetAssemblyVersion(GitVersion.AssemblySemVer)));
}
```

A build is the list of components you implement:

```csharp
public class Build : AzurePipelinesBuild,
    IShowVersion, IClean, ICompile, IRestore, IScanForSecrets,
    IRunUnitTests, IRunIntegrationTests, IGenerateCoverageReport, ITest,
    IUpdateChangelog, IPackageAzureDevOps, ITagRelease, IAnnounceRelease
{
    public static int Main() => Execute<Build>(x => ((IPackageAzureDevOps)x).ReleasePackage);

    int IHasTests.MinCoverageThreshold => 80;   // a deliberate deviation from the default
}
```

Three properties follow from this shape:

- **Dependencies bind by type, not by name.** `.DependsOn<IRestore>()` resolves against whatever the
  build class implements. Implement a component and it wires itself into the graph; omit it and the
  edge simply does not exist.
- **Configuration is an interface member.** `int IHasTests.MinCoverageThreshold => 80;` is an
  explicit interface implementation — compiler-checked, discoverable by Go To Definition, and
  impossible to typo into silence the way a YAML key can be.
- **Every step is replaceable in place.** Components expose overridable methods
  (`ITestExecution.RunTests`, `IGitTagging.PerformGitTagging`, `IAnnounceRelease.PerformAnnouncement`)
  so a repo can change *how* a step works without giving up the graph it sits in.

---

## 📐 The conventions

These are the assumptions the components make. Knowing them is most of knowing this system.

| Convention | The assumption | Defined in |
|---|---|---|
| **Solution** | The single `.sln`/`.slnx` found by Fallout's `[Solution]` injection | [`IHasSolution`](Automation.Fallout.Components/Parameters/IHasSolution.cs) |
| **Configuration** | `Debug` on a developer machine, `Release` on a build server | [`IHasConfiguration`](Automation.Fallout.Components/Parameters/IHasConfiguration.cs) |
| **Version** | GitVersion is the only source of version. Nothing is hand-stamped — assembly, file, informational and package versions all derive from it | [`IHasGitVersion`](Automation.Fallout.Components/Parameters/IHasGitVersion.cs) |
| **Unit tests** | Every project whose **name contains `UnitTests`** | [`IRunUnitTests`](Automation.Fallout.Components/Components/IRunUnitTests.cs) |
| **Integration tests** | Every project whose **name contains `IntegrationTests`** | [`IRunIntegrationTests`](Automation.Fallout.Components/Components/IRunIntegrationTests.cs) |
| **Artifacts** | `artifacts/` at the repo root, with `test-results/`, `coverage-report/` and `packages/` beneath it | [`IHasArtifacts`](Automation.Fallout.Components/Parameters/IHasArtifacts.cs) |
| **Scratch space** | `.tmp/` for Velopack output, Blazor publishes and downloaded tools | `IHasArtifacts`, `IVelopack` |
| **Release branch** | `main`. It selects the production feed, and it gates tagging and announcing | [`IPackage`](Automation.Fallout.Components/Components/IPackage.cs), [`ITagRelease`](Automation.Fallout.Components/Components/ITagRelease.cs) |
| **Tags** | Annotated, named `v{Major.Minor.Patch}`, never overwritten — an existing tag pointing elsewhere is a warning, not a force-push | [`IGitTagging`](Automation.Fallout.Components/Components/IGitTagging.cs) |
| **Changelog** | `CHANGELOG.md` at the repo root, written from Git history | [`IUpdateChangelog`](Automation.Fallout.Components/Components/IUpdateChangelog.cs) |
| **Server vs local** | `IsServerBuild` decides whether releases tag and push. Local runs need explicit `--force-tag-release` | [`IDoTag`](Automation.Fallout.Components/Parameters/IDoTag.cs) |
| **Tool provisioning** | Gitleaks and the Codecov uploader are downloaded on demand into the temp directory if not already on `PATH` — no manual install step | [`IScanForSecrets`](Automation.Fallout.Components/Components/IScanForSecrets.cs), [`IGenerateCoverageReport`](Automation.Fallout.Components/Components/IGenerateCoverageReport.cs) |

Follow the conventions and a repo needs no configuration at all. Deviate, and you state the
deviation in one line.

### The target graph

Implementing the full set produces this order. Each edge comes from the component that owns the
downstream target, so omitting a component removes its edges rather than breaking the chain.

```
ShowVersion ─(DependentFor)─▶ Clean ──▶ Restore ──▶ Compile ──┬──▶ UnitTests ────────┐
                                                              └──▶ IntegrationTests ─┴──▶ CoverageReport
                                                                                              │
                                                  ScanForSecrets ─(runs after CoverageReport)─┤
                                                                                              ▼
                                                                                            Test
                                                                                              │
                                              UpdateChangelog ─(after Test)─────────────┐     │
                                                                                        ▼     ▼
                                                                                       Package
                                                                                          │
                                                                        ┌─────────────────┴──────────────────┐
                                                                        ▼                                    ▼
                                                              ReleasePackage                        ReleaseVelopack
                                                                        └────── triggers ──▶ TagRelease ◀────┘
                                                                                  (server builds, main only)
```

`Announce` is deliberately **not** wired into this chain — no component triggers it. Invoke it
explicitly, or add a `Triggers` edge in your own build class once you have implemented
`PerformAnnouncement`.

---

## 🎛 Overriding a convention

Three levels, in increasing order of scope.

**1. Change a value** — explicit interface implementation in `Build.cs`. Compile-time checked:

```csharp
int IHasTests.MinCoverageThreshold => 80;
bool IHasTests.BreakBuildOnWarnings => false;
bool ITestExecution.UseMicrosoftTestingPlatform => true;
string IHasVelopack.VelopackProjectName => "MyApp.Wpf";
```

**2. Change a value for one run** — every `[Parameter]` is settable from the command line, from
`.fallout/parameters.json`, or from the environment:

```bash
fallout Test --min-coverage-threshold 90 --configuration Release
```

**3. Change a behavior** — implement the narrower interface that owns the logic and override its
method. The target, its dependencies and its conditions stay exactly as they were:

```csharp
class Build : TestBuild
{
    void ITestExecution.RunTests(string projectNameFilter)
    {
        // your own discovery and execution; the rest of the graph is unchanged
    }
}
```

To add something genuinely new, write a component of your own — it is just an interface:

```csharp
public interface IMyLint : IFalloutBuild
{
    AbsolutePath Src => RootDirectory / "src";

    Target MyLint => t => t
        .Description("Run custom lint checks")
        .Executes(() => DotNet($"format {Src} --verify-no-changes"));
}

class Build : TestBuild, IMyLint { /* ... */ }
```

---

## 🧩 Component catalog

**Lifecycle**

| Component | Target | Does |
|---|---|---|
| [`IShowVersion`](Automation.Fallout.Components/Components/IShowVersion.cs) | `ShowVersion` | Logs the resolved GitVersion ahead of everything else |
| [`IClean`](Automation.Fallout.Components/Components/IClean.cs) | `Clean` | `dotnet clean`, then recreates the artifact directories |
| [`IRestore`](Automation.Fallout.Components/Components/IRestore.cs) | `Restore` | `dotnet restore` for the solution |
| [`ICompile`](Automation.Fallout.Components/Components/ICompile.cs) | `Compile` | `dotnet build`, stamping GitVersion, warnings-as-errors per `BreakBuildOnWarnings` |

**Quality**

| Component | Target | Does |
|---|---|---|
| [`ITestExecution`](Automation.Fallout.Components/Components/ITestExecution.cs) | — | Shared execution logic for VSTest and Microsoft Testing Platform. The override point for test running |
| [`IRunUnitTests`](Automation.Fallout.Components/Components/IRunUnitTests.cs) | `UnitTests` | Runs projects matching `UnitTests` |
| [`IRunIntegrationTests`](Automation.Fallout.Components/Components/IRunIntegrationTests.cs) | `IntegrationTests` | Runs projects matching `IntegrationTests` |
| [`IGenerateCoverageReport`](Automation.Fallout.Components/Components/IGenerateCoverageReport.cs) | `CoverageReport` | Merges Cobertura output via ReportGenerator, **fails the build below `MinCoverageThreshold`**, optionally uploads to Codecov |
| [`IScanForSecrets`](Automation.Fallout.Components/Components/IScanForSecrets.cs) | `ScanForSecrets` | Gitleaks over the working tree, auto-provisioning the tool. See the note in [Secret scanning](#-secret-scanning) |
| [`ITest`](Automation.Fallout.Components/Components/ITest.cs) | `Test` | The aggregate gate — depends on coverage and secret scanning, executes nothing itself |

**Release**

| Component | Target | Does |
|---|---|---|
| [`IUpdateChangelog`](Automation.Fallout.Components/Components/IUpdateChangelog.cs) | `UpdateChangelog` | Rewrites `CHANGELOG.md` from Git history |
| [`IPackage`](Automation.Fallout.Components/Components/IPackage.cs) | `Package` | `dotnet pack` at the GitVersion version into `artifacts/packages/`. **Produces only — never pushes** |
| [`IVelopack`](Automation.Fallout.Components/Components/IVelopack.cs) | `ReleaseVelopack` | Builds and publishes Velopack installers, with code signing and Azure blob upload |
| [`IGitTagging`](Automation.Fallout.Components/Components/IGitTagging.cs) | — | Shared Git identity and tag logic. The override point for tagging |
| [`ITagRelease`](Automation.Fallout.Components/Components/ITagRelease.cs) | `TagRelease` | Creates and pushes `v{version}` on `main`, on server builds or with `--force-tag-release` |
| [`IAnnounceRelease`](Automation.Fallout.Components/Components/IAnnounceRelease.cs) | `Announce` | Announcement hook; the default implementation only logs. Not auto-triggered |

**Platform-specific** — pick the set matching your [CI platform](#-ci-platform):

| Component | Platform | Does |
|---|---|---|
| [`AzurePipelinesBuild`](Automation.Fallout.Components/DefaultBuilds/AzurePipelinesBuild.cs) | Azure DevOps | Base class exposing `TF_BUILD`, build id and build number |
| [`GitHubActionsBuild`](Automation.Fallout.Components/DefaultBuilds/GitHubActionsBuild.cs) | GitHub | Base class exposing `GITHUB_ACTIONS`, run id and run number |
| [`IPackageAzureDevOps`](Automation.Fallout.Components/Components/IPackageAzureDevOps.cs) | Azure DevOps | Pushes to Azure Artifacts, choosing production or prerelease feed from the branch |
| [`IPackageGitHub`](Automation.Fallout.Components/Components/IPackageGitHub.cs) | GitHub | Pushes to GitHub Packages for `GitHubOwner` using `GitHubToken` |
| [`IPackageMultiPlatform`](Automation.Fallout.Components/Components/IPackageMultiPlatform.cs) | Both | One `ReleasePackage` target that routes to whichever platform is running the build. Use when one repo is built by both CI systems |
| [`ICreateGitHubRelease`](Automation.Fallout.Components/Components/ICreateGitHubRelease.cs) | GitHub | Creates a GitHub release from the tag, with milestone notes and assets |
| [`IPublishBlazorWasm`](Automation.Fallout.Components/Components/IPublishBlazorWasm.cs) | GitHub | Publishes Blazor WASM and deploys `wwwroot` to a static-site repository |

---

## 🔧 Parameter reference

Every parameter below is an interface member with a default, overridable in `Build.cs`, on the
command line, in `.fallout/parameters.json`, or via environment variable.

| Parameter | Default | Owner |
|---|---|---|
| `Solution` | Auto-detected | `IHasSolution` |
| `Configuration` | `Debug` locally, `Release` on a server | `IHasConfiguration` |
| `BreakBuildOnWarnings` | `true` | `IHasTests` |
| `BreakBuildOnSecretLeaks` | `true` — gates whether the scan **runs** | `IHasTests` |
| `MinCoverageThreshold` | `0` (no gate until you set one) | `IHasTests` |
| `UploadToCodecov` | `false` | `IHasTests` |
| `CodecovToken` | `CODECOV_TOKEN` environment variable | `IHasTests` |
| `GitleaksVersion` | `8.18.1` | `IScanForSecrets` |
| `ForceTagRelease` | `false` | `IDoTag` |
| `ArtifactsDirectoryParam` | `<root>/artifacts` | `IHasArtifacts` |
| `ProductionFeedId` / `PrereleaseFeedId` | AFTR feed GUIDs | `IHasAzureDevOpsFeeds` |
| `GitHubOwner` / `GitHubToken` | Required — throws if absent (token falls back to `GITHUB_TOKEN`) | `IHasGitHubPackages` |
| `PublishTarget` | `Auto` — detects the CI platform; pushes nothing on a local run | `IPackageMultiPlatform` |
| `VelopackProjectName`, `VelopackChannel`, `KeepMaxReleases`, … | See the interface | `IHasVelopack` |

> The defaults above are the ones in code. `BreakBuildOnWarnings` defaults to **true** and
> `MinCoverageThreshold` to **0** — a fresh build is strict about warnings and silent about
> coverage until you choose a threshold.

---

## 📦 Starting compositions

`DefaultBuilds/` holds ready-made compositions. Treat them as **starting points**: either inherit
one directly, or let `autofallout setup` emit the equivalent interface list into your `Build.cs` so
the composition is visible and editable in your repo.

| Composition | Adds | Use case |
|---|---|---|
| `CompileBuild` | Version, Clean, Restore, Compile, secret scan | Simple libraries |
| `TestBuild` | + unit/integration tests, coverage gate | Libraries with tests |
| `PackageBuild` | + changelog, NuGet package, tag, announce | Libraries published to a feed |
| `VelopackBuild` | + Velopack installer, tag, announce | Desktop apps with auto-update |
| `PackageAndVelopackBuild` | + both package and installer | Ships as a library *and* an app |

> The `DefaultBuilds` classes and the scaffolder's interface lists are maintained separately and do
> not currently match — `DefaultBuilds/TestBuild.cs` implements the packaging and Velopack
> components too, while a scaffolded `TestBuild` gets only the test set. Prefer the scaffolded
> composition, and read the class before inheriting it.

---

## 🚀 Bootstrap: the `autofallout` tool

[`Automation.Fallout.Builder`](Automation.Fallout.Builder/README.md) is a global .NET tool that
gets a repository from nothing to a working build.

```bash
dotnet tool install --global Automation.Fallout.Builder
cd MyProject
autofallout setup
fallout            # run the build
```

> **Note:** The command has been renamed twice: `aftrnuke` → `aftrfallout` → `autofallout`. Update
> any scripts or pipelines still invoking an older name, and uninstall the old tool
> (`dotnet tool uninstall -g Automation.Fallout.Builder`) so a stale shim does not linger on `PATH`.

`setup` asks which CI platform you target, which composition you want, and your quality-gate
preferences, then:

- Installs/updates the Fallout CLI (`Fallout.Cli`, exposing `fallout`) and runs `fallout :setup`
- Creates `build/_build.csproj` targeting **net10.0**
- References `Automation.Fallout.Components` and `Fallout.Common`, writing versions into
  `Directory.Packages.props` when the repo uses central package management
- Adds `PackageDownload` entries for GitVersion.Tool 6.8.2 and ReportGenerator 5.5.11
- Copies `.gitleaks.toml`, `nuget.config`, `GitVersion.yml` and the CI definition
- Generates `Build.cs` for the chosen composition and platform
- Updates `.gitignore`, and removes a legacy root `Configuration.cs` if present

`autofallout migrate` repairs a repository that was renamed from Nuke to Fallout but no longer
compiles. It is non-interactive and safe in a pipeline; `--dry-run` reports without writing.
See the [Builder README](Automation.Fallout.Builder/README.md) for the full option set.

### 🔀 CI platform

Setup asks up front whether the repository builds on **Azure DevOps** or **GitHub Actions**, because
the two differ in more than a pipeline file:

| | Azure DevOps | GitHub Actions |
|---|---|---|
| Base class | `AzurePipelinesBuild` | `GitHubActionsBuild` |
| Packaging component | `IPackageAzureDevOps` | `IPackageGitHub` |
| Destination | Azure Artifacts feeds | GitHub Packages |
| Credentials | Feed IDs, `az` API key | `GitHubOwner`, `GitHubToken` |
| CI definition | `azure-pipelines.yml` | `.github/workflows/build.yml` |
| `nuget.config` | AFTR feeds + nuget.org | nuget.org |
| Extra components | — | `ICreateGitHubRelease` when the build tags |

Everything else is shared, so a build differs only where it must. `migrate` takes the same choice as
`--platform GitHubActions|AzureDevOps` (default `AzureDevOps`).

The choice is not permanently binding: a repository built by *both* systems swaps its packaging
component for [`IPackageMultiPlatform`](#one-repository-both-ci-systems) and keeps everything else.

> **`setup` and `migrate` deliberately do not offer the dual-platform option.** It stays a
> by-hand change: the scaffolder asks one platform question and generates one platform's wiring.
> A repository that needs both is an exception, and exceptions are wired and maintained by hand
> rather than pushed through the generator.

### Manual setup

```bash
dotnet tool install -g Fallout.Cli
fallout :setup
```

Add the package to `build/_build.csproj`, then write `Build.cs`:

```csharp
using Fallout.Common;
using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.DefaultBuilds;

class Build : TestBuild
{
    public static int Main() => Execute<Build>(x => ((ITest)x).Test);
}
```

### Generated layout

```
YourProject/
├── .fallout/
│   ├── parameters.json            # Build parameters (solution, secrets)
│   └── build.schema.json          # Schema for editor autocomplete
├── build/
│   ├── Build.cs                   # Your composition
│   └── _build.csproj              # net10.0
├── .gitleaks.toml
├── nuget.config
├── GitVersion.yml
├── azure-pipelines.yml            # or .github/workflows/build.yml
├── build.cmd / build.ps1 / build.sh
```

---

## 🚢 CI/CD integration

The pipeline definition stays deliberately thin — install the SDK, restore tools, call the build:

```yaml
trigger:
  - main

pool:
  vmImage: 'windows-latest'

steps:
- checkout: self
  fetchDepth: 0                    # GitVersion needs full history

- task: UseDotNet@2
  displayName: 'Install .NET SDK'
  inputs:
    version: '10.x'

- script: dotnet tool restore
  displayName: 'Restore local tools'

- script: .\build.cmd
  displayName: 'Run Fallout build'
```

Because the logic is in C#, the same command reproduces a CI failure locally. `IsServerBuild` is the
only behavioral difference, and it is confined to tagging and pushing.

---

## 🧪 Testing

`ITestExecution` supports both VSTest and the Microsoft Testing Platform (MTP — TUnit and similar).
Default is VSTest via `dotnet test` with `XPlat Code Coverage`. For MTP projects:

```csharp
class Build : TestBuild
{
    bool ITestExecution.UseMicrosoftTestingPlatform => true;

    public static int Main() => Execute<Build>(x => ((ITest)x).Test);
}
```

**Why it matters:** on the .NET 10 SDK, `dotnet test` no longer supports the VSTest protocol for
MTP-based test apps. In MTP mode the test binary is executed directly, which also gives per-project
and per-TFM TRX reports, Cobertura coverage on server builds, and independent execution of each
target framework. Projects must be built first — MTP mode runs the binary from the output directory.

### This repository's own tests

The test project uses TUnit, so `dotnet test` fails here with
`Testing with VSTest target is no longer supported`. Run the binary directly:

```bash
dotnet build Automation.Fallout.Components.sln
./Automation.Fallout.Builder.UnitTests/bin/Debug/net8.0/Automation.Fallout.Builder.UnitTests.exe
```

---

## 📊 Code coverage

`IGenerateCoverageReport` merges every `coverage.cobertura.xml` under `artifacts/test-results/` with
ReportGenerator, writing HTML, Cobertura and JSON summary output to `artifacts/coverage-report/`. It
reads line coverage back from `Summary.json` and **throws when it is below `MinCoverageThreshold`**,
which is what makes the threshold a real gate. With no coverage files present it logs a warning and
skips rather than failing. Set `UploadToCodecov` to publish the merged report; the uploader is
downloaded on demand.

## 🔐 Secret scanning

`IScanForSecrets` runs Gitleaks over the working tree, downloading the pinned version
(`GitleaksVersion`, default 8.18.1) into the temp directory when it is not already on `PATH` — no
manual install step.

> **Read this before relying on it as a gate.** `BreakBuildOnSecretLeaks` controls whether the scan
> **runs**, not whether findings fail the build: the target is gated by `OnlyWhenDynamic`, and
> Gitleaks is invoked with `--exit-code 0`, so findings are logged and the build continues. Treat the
> current behavior as *reporting*, and check the log. Removing `--exit-code 0` in
> [`IScanForSecrets`](Automation.Fallout.Components/Components/IScanForSecrets.cs) is what turns it
> into an enforcing gate.

## 📦 Packaging and releases

`IPackage` only *produces* packages — pushing lives in a platform component because the destination
differs:

- **`IPackageAzureDevOps`** — pushes to Azure Artifacts, selecting the production feed on `main` and
  the prerelease feed otherwise, with `az` as the API key and duplicate pushes skipped.
- **`IPackageGitHub`** — pushes to GitHub Packages for `GitHubOwner` using `GitHubToken`, skipping
  the push on local runs unless `--force-tag-release` is passed.

Both trigger `TagRelease` on server builds. Tagging is conservative by design: an existing tag that
points somewhere other than `HEAD` produces a warning, never a force-push.

### One repository, both CI systems

Those two components each declare a target named `ReleasePackage`, so they cannot be implemented
side by side — two default interface members with the same name are ambiguous, and target discovery
would find two targets called `ReleasePackage`. That is why setup picks exactly one.

When a repository is genuinely built by both systems — an Azure DevOps master mirrored to GitHub,
for instance — implement **`IPackageMultiPlatform`** instead of either one. It composes the
target-free push interfaces (`IPushPackagesAzureDevOps`, `IPushPackagesGitHub`) and owns the single
`ReleasePackage` target, routing at runtime:

```csharp
class Build : AzurePipelinesBuild, ITest, IPackageMultiPlatform, ITagRelease
{
    public static int Main() => Execute<Build>(x => ((IPackageMultiPlatform)x).ReleasePackage);
}
```

| Running on | Pushes to | Tags the release |
|---|---|---|
| Azure DevOps (`TF_BUILD`) | Azure Artifacts | yes |
| GitHub Actions (`GITHUB_ACTIONS`) | GitHub Packages | no |
| A developer machine | nothing | no |

**Only Azure DevOps tags.** A mirrored GitHub build that also tagged would create a competing
`v{version}` at a different commit, and a mirror that pushes tags without forcing would then start
failing. Override `TagsReleases` to move that ownership.

Force a destination with `--publish-target AzureDevOps|GitHub|Both|None`; `None` runs the target
without pushing, which is how to exercise it locally.

`IVelopack` builds Windows installers, handling runtime bundling, code signing and Azure blob
upload, keeping `KeepMaxReleases` (default 3) releases per channel.

---

## 📋 NuGet configuration

Multi-source configuration with package source mapping:

- **AFTR Production** / **AFTR Prerelease** — Azure Artifacts
- **nuget.org** — public packages

Routing: `Automation.*`, `FuelTaxAutomation.*`, `PMFuelTax*.*` → AFTR feeds; everything else,
**including `Fallout.*`**, → nuget.org. See [`nuget.config`](nuget.config).

## 🔧 Requirements

**To run the builder tool:** .NET SDK 8.0+

**To run a generated build:**
- .NET SDK 10.0 — Fallout 11.x ships net10.0 assets only
- Git with real history — GitVersion fails on a shallow clone or an empty repository

**Provisioned for you:** `Fallout.Cli`, GitVersion.Tool 6.8.2 and ReportGenerator 5.5.11 (as
`PackageDownload`), plus Gitleaks and the Codecov uploader downloaded on demand.

---

## 🔄 Migration from NUKE

The API surface is largely identical — most of the work is renaming.

**Packages:** `Nuke.Common` → `Fallout.Common`, `Nuke.Build` → `Fallout.Build`, `Nuke.GlobalTool` →
`Fallout.Cli`, `Automation.Nuke.Components` → `Automation.Fallout.Components`,
`Automation.Nuke.Builder` → `Automation.Fallout.Builder`.

**Namespaces:** `Nuke.Common.*` → `Fallout.Common.*`, and `Nuke.Common.ProjectModel` →
**`Fallout.Solutions`**.

> ⚠️ **`Fallout.Solutions` is the one non-obvious rename.** Fallout 11.0 inlined the vendored
> solution parser and realigned the namespace to the assembly name. It is *not*
> `Fallout.Common.ProjectModel` — that namespace does not exist in 11.x. This is the most common
> migration error, and the automated migration tool gets it wrong.

**Types:** `INukeBuild` → `IFalloutBuild`, `NukeBuild` → `FalloutBuild`.

**Conventions:** `.nuke/` → `.fallout/`, `nuke` → `fallout`, `nuke :add-package` →
`fallout :add-package`.

**Target framework:** set `<TargetFramework>net10.0</TargetFramework>`. A build project left on
`net8.0` restores without error but resolves zero compile assets, producing a confusing cascade of
`CS0234`.

Fallout publishes `dotnet tool install -g Fallout.Migrate` for the rewrites; review its output.
`autofallout migrate` repairs what it leaves behind.

---

## 🐛 Troubleshooting

**`CS0234: 'Common' does not exist in the namespace 'Fallout'`**
Build project is not targeting `net10.0`.

**`CS0234: 'ProjectModel' does not exist in the namespace 'Fallout.Common'`**
Replace `using Fallout.Common.ProjectModel;` with `using Fallout.Solutions;`.

**`Could not find commit information`**
No commits, or a shallow clone. GitVersion needs real history — set `fetchDepth: 0` on CI checkout.

**`Missing package reference/download` for GitVersion.Tool**
Add `<PackageDownload Include="GitVersion.Tool" Version="[6.8.2]" />` to `build/_build.csproj`.

**Tests are not discovered**
Test projects are matched by name. A project must contain `UnitTests` or `IntegrationTests` in its
name to be picked up.

**Coverage report is empty or skipped**
No `coverage.cobertura.xml` was produced under `artifacts/test-results/`. For MTP projects, set
`UseMicrosoftTestingPlatform => true`.

**`Testing with VSTest target is no longer supported`**
An MTP test project on the .NET 10 SDK. Set `UseMicrosoftTestingPlatform => true`.

**Package not found**
Check package source mapping — `Fallout.*` resolves from nuget.org, not the AFTR feeds.

**"Fallout not found"**
`dotnet tool install -g Fallout.Cli`, and confirm global tools are on `PATH`.

---

## 🤝 Contributing

This package is consumed by 80+ repositories, so a change here is a change everywhere:

1. Preserve backward compatibility — adding a component is safe, changing an existing target's
   contract is not
2. Keep `Automation.Fallout.Components` and `Automation.Fallout.Builder` in sync; if a scaffolded
   composition changes, update `BuildFileGenerator` in the same change
3. Keep components single-purpose and composable — put shared logic in an overridable method rather
   than duplicating it into a target
4. Update [CHANGELOG.md](CHANGELOG.md)

## 📖 Further reading

- [Builder tool README](Automation.Fallout.Builder/README.md) — full `autofallout` documentation
- [Fallout](https://github.com/ChrisonSimtian/Fallout) — the underlying build system
- [GitVersion](https://gitversion.net/) — versioning configuration
- [CHANGELOG.md](CHANGELOG.md) — version history

## 📄 License

Apache License 2.0 — see [LICENSE](LICENSE).

## 👤 Authors

Luke Lanphear

## 🔗 Repository

https://dev.azure.com/AFTR/Automation/_git/Automation.Fallout.Components
