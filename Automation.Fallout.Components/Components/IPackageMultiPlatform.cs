using Automation.Fallout.Components.Parameters;
using Fallout.Common;
using Fallout.Common.Utilities; // supplies the fluent .When(...) extension

namespace Automation.Fallout.Components.Components;

/// <summary>
/// A single ReleasePackage target that pushes to whichever feed belongs to the CI platform
/// currently running the build. Use this instead of <see cref="IPackageAzureDevOps"/> or
/// <see cref="IPackageGitHub"/> when one repository is built by both systems - for example a
/// repository whose Azure DevOps master is mirrored to GitHub.
///
/// Those two components each declare their own ReleasePackage target and therefore cannot be
/// implemented side by side. This component implements neither of them: it composes the
/// target-free push interfaces and owns the only ReleasePackage target.
///
/// Wired by hand. 'autofallout setup' and 'autofallout migrate' scaffold a single platform on
/// purpose and will not generate this composition - a repository built by both CI systems is an
/// exception, maintained deliberately rather than generated.
///
/// <code>
/// class Build : AzurePipelinesBuild, ITest, IPackageMultiPlatform, ITagRelease
/// {
///     public static int Main() => Execute&lt;Build&gt;(x => ((IPackageMultiPlatform)x).ReleasePackage);
/// }
/// </code>
/// </summary>
public interface IPackageMultiPlatform : IPushPackagesAzureDevOps, IPushPackagesGitHub
{
    [Parameter("Where to push packages: Auto (detect the CI platform), AzureDevOps, GitHub, Both or None. Default is 'Auto'.")]
    PackagePublishTarget PublishTarget =>
        TryGetValue<PackagePublishTarget?>(() => PublishTarget) ?? PackagePublishTarget.Auto;

    /// <summary>
    /// Detected the same way <see cref="DefaultBuilds.AzurePipelinesBuild"/> does. Named to avoid
    /// colliding with that class's own IsAzurePipelines helper when both are in play.
    /// </summary>
    bool RunningOnAzurePipelines => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));

    /// <summary>Detected the same way <see cref="DefaultBuilds.GitHubActionsBuild"/> does.</summary>
    bool RunningOnGitHubActions => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    /// <summary>
    /// The destination after resolving <see cref="PackagePublishTarget.Auto"/>. A local run with
    /// no CI platform detected resolves to <see cref="PackagePublishTarget.None"/>, so running
    /// ReleasePackage on a developer machine builds packages without pushing them anywhere.
    /// </summary>
    PackagePublishTarget ResolvedPublishTarget =>
        PackagePublishRouting.Resolve(PublishTarget, RunningOnGitHubActions, RunningOnAzurePipelines);

    /// <summary>
    /// Whether this platform owns release tagging. Only Azure DevOps tags, so a mirrored GitHub
    /// build cannot create a competing v{version} tag at a different commit - which would break
    /// a mirror that pushes tags without forcing. Override to move tag ownership.
    /// </summary>
    bool TagsReleases => RunningOnAzurePipelines;

    Target ReleasePackage => t => t
        .DependsOn<IPackage>(x => x.Package)
        .When(TagsReleases && (IsServerBuild || ForceTagRelease), _ => _
            .Triggers<ITagRelease>(x => x.TagRelease))
        .Description("Deploy NuGet packages to the current CI platform's feed")
        .Executes(() => PublishPackages());

    /// <summary>
    /// Dispatches to the push for the resolved target. Override to change the routing itself.
    ///
    /// Each branch is entered only for its own platform on purpose: GitHubOwner and GitHubToken
    /// throw when unset (see <see cref="IHasGitHubPackages"/>), so evaluating them during an
    /// Azure DevOps run would fail the build.
    /// </summary>
    void PublishPackages()
    {
        var target = ResolvedPublishTarget;
        Serilog.Log.Information("Package publish target: {Target} (requested {Requested})", target, PublishTarget);

        switch (target)
        {
            case PackagePublishTarget.AzureDevOps:
                PushPackagesToAzureDevOps();
                break;

            case PackagePublishTarget.GitHub:
                PushPackagesToGitHub();
                break;

            case PackagePublishTarget.Both:
                PushPackagesToAzureDevOps();
                PushPackagesToGitHub();
                break;

            case PackagePublishTarget.None when PublishTarget == PackagePublishTarget.Auto:
                Serilog.Log.Information(
                    "No package push - no CI platform detected. Pass --publish-target to force a destination.");
                break;

            case PackagePublishTarget.None:
                Serilog.Log.Information("No package push - publishing is disabled by --publish-target None.");
                break;

            default:
                throw new Exception($"Unsupported publish target '{target}'.");
        }
    }
}
