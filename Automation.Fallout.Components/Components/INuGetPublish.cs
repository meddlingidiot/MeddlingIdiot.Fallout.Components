using Fallout.Common;
using Fallout.Common.Utilities; // supplies the fluent .When(...) extension

namespace Automation.Fallout.Components.Components;

/// <summary>
/// Publishes the packaged output to a public NuGet feed, as a step that can be added to a
/// pipeline that already publishes somewhere else.
/// </summary>
/// <remarks>
/// <para>
/// Its target is <see cref="PublishNuGet"/> rather than ReleasePackage, on purpose: the
/// private-feed components own that name, and a repository that publishes to both wants both
/// steps rather than a choice between them. Implement this alongside
/// <see cref="IPackageGitHub"/> or <see cref="IPackageAzureDevOps"/> and the two coexist.
/// </para>
/// <code>
/// class Build : GitHubActionsBuild, ITest, IPackageGitHub, INuGetPublish, ITagRelease
/// {
///     public static int Main() => Execute&lt;Build&gt;(x => ((INuGetPublish)x).PublishNuGet);
///
///     string IHasNuGetOrg.NuGetOwner => "themeddlingidiot";
/// }
/// </code>
/// <para>
/// The key comes from <c>--nuget-api-key</c> or <c>NUGET_API_KEY</c> and belongs in CI
/// secrets. Nothing publishes from a developer machine without <c>--force-tag-release</c>,
/// and nothing publishes a stable version off the main branch — see
/// <see cref="NuGetPublishDecision"/> for the rules and why they are that way round.
/// </para>
/// </remarks>
public interface INuGetPublish : IPushPackagesNuGet
{
    /// <summary>
    /// Whether this target owns release tagging. Off by default: a repository that also
    /// publishes to a private feed tags from that target, and two targets tagging the same
    /// commit is how a build ends up racing itself for the v{version} tag. Turn it on where
    /// this is the only publish step.
    /// </summary>
    bool TagsReleasesFromNuGet => false;

    Target PublishNuGet => t => t
        .DependsOn<IPackage>(x => x.Package)
        .When(TagsReleasesFromNuGet && (IsServerBuild || ForceTagRelease), _ => _
            .Triggers<ITagRelease>(x => x.TagRelease))
        .Description("Publish NuGet packages to the public feed")
        .Executes(() => PushPackagesToNuGet());
}
