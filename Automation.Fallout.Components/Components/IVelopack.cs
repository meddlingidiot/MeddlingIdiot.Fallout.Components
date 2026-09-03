using System.Linq;
using System.Runtime.InteropServices;
using Automation.Fallout.Components.DefaultBuilds;
using Automation.Fallout.Components.Parameters;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Utilities;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

namespace Automation.Fallout.Components.Components;

public interface IVelopack : IFalloutBuild, IHasSolution, IHasConfiguration, IHasGitVersion, IHasVelopack, IHasArtifacts,
    IHasCodeSigning,
    IDoTag
{
    [Parameter("Version of the Velopack CLI (vpk) to install. Defaults to the newest release.")]
    string? VelopackCliVersion => TryGetValue(() => VelopackCliVersion);

    /// <summary>
    /// Installs the Velopack CLI if it is missing and returns a usable path to it.
    ///
    /// Every target that shells out to vpk must go through this. The install used to live in the
    /// body of <see cref="PreVelopack"/>, behind its "no SAS token" early return, so a build
    /// without AzureBlobSasToken skipped the install and then failed in BuildVelopack with
    /// "Could not find 'vpk'".
    /// </summary>
    string ResolveVelopackCli()
    {
        var existing = FindVelopackCli();
        if (existing != null)
            return existing;

        Serilog.Log.Information("Velopack CLI (vpk) not found. Installing...");

        var versionArgument = string.IsNullOrWhiteSpace(VelopackCliVersion)
            ? string.Empty
            : $" --version {VelopackCliVersion}";

        ProcessTasks.StartProcess("dotnet", $"tool update -g vpk{versionArgument}")
            .AssertZeroExitCode();

        return FindVelopackCli()
               ?? throw new Exception(
                   "Velopack CLI (vpk) is still not resolvable after 'dotnet tool update -g vpk'. " +
                   $"Looked on PATH and in {DotNetToolsDirectory}.");
    }

    /// <summary>The dotnet global tool directory, which is not always on PATH for a running process.</summary>
    AbsolutePath DotNetToolsDirectory =>
        (AbsolutePath)(Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        / ".dotnet" / "tools";

    private string? FindVelopackCli()
    {
        // 1. Already on PATH - the normal case on a developer machine.
        try
        {
            var onPath = ToolPathResolver.GetPathExecutable("vpk");
            if (!string.IsNullOrWhiteSpace(onPath))
                return onPath;
        }
        catch
        {
            // GetPathExecutable asserts rather than returning null; fall through to the tools directory.
        }

        // 2. The global tool directory. A tool installed during this build lands here, but PATH was
        //    captured when the process started, so step 1 cannot see it on a fresh agent.
        var executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "vpk.exe" : "vpk";
        var installed = DotNetToolsDirectory / executable;

        return installed.FileExists() ? installed : null;
    }

    Target PreVelopack => t => t
        .DependsOn<ITest>(x => x.Test)
        .DependsOn<IUpdateChangelog>()
        .Description("Download existing Velopack releases")
        .Executes(() =>
        {
            if (string.IsNullOrEmpty(AzureBlobSasTokenLocal))
            {
                Serilog.Log.Warning("AzureBlobSasToken not provided, skipping download of existing releases");
                return;
            }

            var velopackProject = Solution.GetProject(VelopackProjectName);
            if (string.IsNullOrEmpty(velopackProject?.Name) || string.IsNullOrEmpty(VelopackProjectName))
            {
                Serilog.Log.Warning("VelopackProjectName not provided, skipping building...");
                Assert.False(true, "VelopackProjectName not provided");
                return;
            }

            var version = GitVersion?.FullSemVer ?? "0.0.0-local";
            var releaseType = version.Contains("beta") ? "Prerelease" : "Stable";
            var channel = $"{VelopackProjectName}-{releaseType}-{VelopackChannel}";
            var downloadDir = VelopackPublishDirectory;

            Serilog.Log.Information("Downloading existing releases from Azure Blob Storage...");
            Serilog.Log.Information("Channel: {Channel}", channel);
            Serilog.Log.Information("Download Directory: {Directory}", downloadDir);

            var vpkToolPath = ResolveVelopackCli();

            downloadDir.CreateDirectory();

            var vpkArgs = $"download az " +
                          $"--container {VelopackBlobContainer} " +
                          $"--account {AzureBlobAccount} " +
                          $"--sas \"{AzureBlobSasTokenLocal}\" " +
                          $"--channel {channel} " +
                          $"--outputDir \"{downloadDir}\"";
            
            if (releaseType == "Prerelease")
            {
                vpkArgs += $" --delta none";
            }
            
            if (!string.IsNullOrEmpty(AzureBlobEndpoint))
            {
                vpkArgs += $" --endpoint {AzureBlobEndpoint}";
            }

            if (!string.IsNullOrEmpty(AzureBlobTimeout))
            {
                vpkArgs += $" --timeout {AzureBlobTimeout}";
            }

            Serilog.Log.Information("Running: vpk {Args}", vpkArgs.Replace(AzureBlobSasTokenLocal, "***"));

            ProcessTasks.StartProcess(vpkToolPath, vpkArgs, workingDirectory: RootDirectory)
                .WaitForExit();
        });

    Target BuildVelopack => t => t
        .DependsOn(PreVelopack)
        .Description("Build Velopack release package")
        .Executes(() =>
        {
            var velopackProject = Solution.GetProject(VelopackProjectName);
            if (string.IsNullOrEmpty(velopackProject?.Name) || string.IsNullOrEmpty(VelopackProjectName))
            {
                Serilog.Log.Warning("VelopackProjectName not provided, skipping building...");
                Assert.False(true, "VelopackProjectName not provided");
                return;
            }
            
            var version = GitVersion?.FullSemVer.Replace('+', '-') ?? "0.0.0-local";
            var isMainBranch = GitVersion?.BranchName.Equals("main", StringComparison.OrdinalIgnoreCase) ?? false;
            var releaseType = isMainBranch ? "Stable" : "Prerelease";
            var channel = $"{VelopackProjectName}-{releaseType}-{VelopackChannel}";
            var netRuntime = $"win-x64";
            
            var targetFrameworks = velopackProject.GetTargetFrameworks();
            var useNetRuntime = targetFrameworks.Any(x => x.StartsWith("net10.0", StringComparison.OrdinalIgnoreCase));

            Serilog.Log.Information("Building Velopack package for {Project} version {Version}", VelopackProjectName,
                version);
            Serilog.Log.Information("Release Type: {ReleaseType}", releaseType);
            Serilog.Log.Information("Channel: {Channel}", channel);

            // Publish the application to temp directory (not artifacts)
            var publishDirectory = RootDirectory / ".tmp" / "velopack-build";
            publishDirectory.CreateOrCleanDirectory();
            Serilog.Log.Information("Publishing application to {Directory}", publishDirectory);

            DotNetPublish(s => s
                .SetProject(velopackProject)
                .SetConfiguration(Configuration)
                .SetOutput(publishDirectory)
                .SetRuntime("win-x64")
                .SetSelfContained(true)
                .SetProperty("PublishSingleFile", "false")
                .SetAssemblyVersion(GitVersion?.AssemblySemVer)
                .SetFileVersion(GitVersion?.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion?.InformationalVersion));

            // Find the main executable
            var executables = Directory.GetFiles(publishDirectory, "*.exe", SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(executables, $"No .exe files found in publish directory: {publishDirectory}");

            var mainExe = executables.FirstOrDefault(e =>
                              Path.GetFileName(e).Equals($"{VelopackProjectName}.exe",
                                  StringComparison.OrdinalIgnoreCase))
                          ?? executables.First();
            var mainExeName = Path.GetFileName(mainExe);

            Serilog.Log.Information("Main executable: {MainExe}", mainExeName);

            // Build Velopack package
            var vpkArgs = $"pack " +
                          $"--packId {VelopackProjectName} " +
                          $"--packVersion {version} " +
                          $"--packDir \"{publishDirectory}\" " +
                          $"--mainExe {mainExeName} " +
                          $"--channel {channel} " +
                          //(useNetRuntime ? $"--runtime {netRuntime} " : "") +
                          $"--outputDir \"{VelopackPublishDirectory}\"";

            //Serilog.Log.Information("SslComUsername: {SslComUsername}}", SslComUsername);
            // Add signing parameters if configured
            //if (!string.IsNullOrEmpty(SslComUsername) && !string.IsNullOrEmpty(SslComPassword))
            //{
            //    vpkArgs += $" --signParams \"/fd sha256 /tr http://timestamp.ssl.com /td sha256 /n \\\"{CodeSigningCertificateName}\\\"\"";
            //    Serilog.Log.Information("Code signing enabled");
            //}
            
            if (!string.IsNullOrEmpty(VelopackIconPath) && File.Exists(VelopackIconPath))
            {
                vpkArgs += $" --icon \"{VelopackIconPath}\"";
                Serilog.Log.Information("Using icon: {VelopackIconPath}", VelopackIconPath);
            }

            var vpkToolPath = ResolveVelopackCli();

            Serilog.Log.Information("Creating Velopack package...");
            Serilog.Log.Information("Running: vpk {Args}", vpkArgs);

            var process = ProcessTasks.StartProcess(vpkToolPath, vpkArgs, workingDirectory: RootDirectory, logOutput: true);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var output = string.Join(Environment.NewLine, process.Output.Select(o => o.Text));
                Serilog.Log.Error("Velopack command output: {Output}", output);

                if (output.Contains("equal or greater to the current version"))
                {
                    Serilog.Log.Warning("Version {Version} already exists in channel. Skipping package creation.",
                        version);
                }
                else
                {
                    Serilog.Log.Error("Velopack pack failed with exit code {ExitCode}", process.ExitCode);
                    throw new Exception($"Velopack pack failed with exit code {process.ExitCode}: {output}");
                }
            }
            else
            {
                Serilog.Log.Information("Velopack package built successfully");
            }

            // List created files
            var createdFiles = Directory.GetFiles(VelopackPublishDirectory, "*", SearchOption.TopDirectoryOnly);
            foreach (var file in createdFiles)
            {
                Serilog.Log.Information("  - {File}", Path.GetFileName(file));
            }
        });

    Target ReleaseVelopack => t => t
        .DependsOn(BuildVelopack)
        .When(IsServerBuild || ForceTagRelease, _ => _
            .Triggers<ITagRelease>(x => x.TagRelease))
        .Description("Upload Velopack releases")
        .Executes(() =>
        {
            if (string.IsNullOrEmpty(AzureBlobSasTokenLocal))
            {
                Serilog.Log.Warning("AzureBlobSasToken not provided, skipping upload");
                return;
            }
            var velopackProject = Solution.GetProject(VelopackProjectName);
            if (string.IsNullOrEmpty(velopackProject?.Name) || string.IsNullOrEmpty(VelopackProjectName))
            {
                Serilog.Log.Warning("VelopackProjectName not provided, skipping building...");
                Assert.False(true, "VelopackProjectName not provided");
                return;
            }
            
            var isMainBranch = GitVersion?.BranchName.Equals("main", StringComparison.OrdinalIgnoreCase) ?? false;
            var releaseType = isMainBranch ? "Stable" : "Prerelease";
            var channel = $"{VelopackProjectName}-{releaseType}-{VelopackChannel}";
            var keepMaxReleases = KeepMaxReleases; // PreRelease could keep fewer, but 1 was not enough

            var assetsFile = VelopackPublishDirectory / $"assets.{channel}.json";
            if (!assetsFile.Exists())
            {
                Serilog.Log.Warning("No assets file found for channel '{Channel}', skipping upload (pack was likely skipped because this version already exists)", channel);
                return;
            }

            Serilog.Log.Information("Uploading Velopack releases to Azure Blob Storage...");
            Serilog.Log.Information("Channel: {Channel}", channel);
            Serilog.Log.Information("Keep Max Releases: {KeepMaxReleases}", keepMaxReleases);
            Serilog.Log.Information("Source Directory: {Directory}", VelopackPublishDirectory);

            var vpkArgs = $"upload az " +
                          $"--outputDir \"{VelopackPublishDirectory}\" " +
                          $"--container {VelopackBlobContainer} " +
                          $"--account {AzureBlobAccount} " +
                          $"--sas \"{AzureBlobSasTokenLocal}\" " +
                          $"--channel {channel} " +
                          $"--keepMaxReleases {keepMaxReleases}";

            if (!string.IsNullOrEmpty(AzureBlobEndpoint))
            {
                vpkArgs += $" --endpoint {AzureBlobEndpoint}";
            }

            if (!string.IsNullOrEmpty(AzureBlobTimeout))
            {
                vpkArgs += $" --timeout {AzureBlobTimeout}";
            }

            var vpkToolPath = ResolveVelopackCli();

            Serilog.Log.Information("Running: vpk {Args}", vpkArgs.Replace(AzureBlobSasTokenLocal, "***"));

            ProcessTasks.StartProcess(vpkToolPath, vpkArgs, workingDirectory: RootDirectory)
                .AssertZeroExitCode();

            Serilog.Log.Information("Successfully uploaded Velopack releases");
            Serilog.Log.Information("Update URL: {Url}/{Container}/{Channel}", AzureBlobEndpoint, VelopackBlobContainer,
                channel);
        });
}
