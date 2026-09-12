using Automation.Fallout.Components.Parameters;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tools.DotNet;

namespace Automation.Fallout.Components.Components;

/// <summary>
/// The public-feed push, deliberately carrying no <see cref="Target"/>.
/// See <see cref="IPushPackagesAzureDevOps"/> for why the push is separated from the target —
/// the same reason applies here, and it is what lets a repository publish to its private feed
/// and to nuget.org from one build.
///
/// Override <see cref="PushPackagesToNuGet"/> to change how packages reach the feed.
/// </summary>
public interface IPushPackagesNuGet : IPackage, IHasNuGetOrg
{
    /// <summary>
    /// Whether prerelease versions may be published. Off by default: a public feed should
    /// carry releases, not every commit that happened to build.
    /// </summary>
    bool PublishPrereleases => false;

    /// <summary>
    /// Whether this build may publish, and why. Override to change the rules; the reasoning
    /// itself lives in <see cref="NuGetPublishDecision"/>, where it can be tested.
    /// </summary>
    NuGetPublishVerdict NuGetVerdict => NuGetPublishDecision.Evaluate(
        IsServerBuild,
        ForceTagRelease,
        IsMainBranch,
        GitVersion.FullSemVer,
        PublishPrereleases);

    void PushPackagesToNuGet()
    {
        var verdict = NuGetVerdict;
        if (!verdict.ShouldPush)
        {
            Serilog.Log.Information("Skipping public NuGet push - {Reason}.", verdict.Reason);
            return;
        }

        var packages = PackagesToPush;
        if (packages.Count == 0)
        {
            // Worth saying out loud rather than succeeding silently: a release target that
            // pushed nothing reads as a successful release until somebody looks for the package.
            Serilog.Log.Warning("No packages found to publish to {Source}.", NuGetSource);
            return;
        }

        Serilog.Log.Information(
            "Publishing {Count} package(s) to {Source}{Owner} - {Reason}.",
            packages.Count,
            NuGetSource,
            NuGetOwner is { Length: > 0 } owner ? $" as {owner}" : string.Empty,
            verdict.Reason);

        foreach (var package in packages)
        {
            Serilog.Log.Information("Pushing {Package}", package.Name);

            DotNetTasks.DotNetNuGetPush(s => s
                .SetTargetPath(package)
                .SetSource(NuGetSource)
                .SetApiKey(NuGetApiKey)
                // A re-run of a release must not fail the build on a version that already
                // landed. The feed keeps the first push; nothing is overwritten either way.
                .SetSkipDuplicate(true));
        }

        // Symbols travel with the package: dotnet nuget push sends the matching .snupkg from
        // the same directory on its own, so there is nothing to glob for and nothing to do
        // here beyond saying whether it happened.
        var symbols = PackagePublishDirectory.GlobFiles("**/*.snupkg").Count;
        Serilog.Log.Information(
            symbols > 0
                ? "{Count} symbol package(s) went with them."
                : "No symbol packages alongside - SourceLink will not step into this release.",
            symbols);
    }
}
