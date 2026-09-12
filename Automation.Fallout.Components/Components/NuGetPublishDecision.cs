namespace Automation.Fallout.Components.Components;

/// <summary>Whether a publish should happen, and the sentence to log either way.</summary>
public readonly record struct NuGetPublishVerdict(bool ShouldPush, string Reason);

/// <summary>
/// The rules about when a build may publish to a public feed, as arithmetic over primitives.
/// </summary>
/// <remarks>
/// Pulled out of <see cref="IPushPackagesNuGet"/> so it can be tested without a build object,
/// a CI environment or a feed. The stakes are why: a push to nuget.org is permanent. A version
/// can be unlisted, but it cannot be deleted and its number can never be reused — so "did this
/// build have any business publishing" is the one piece of this component that has to be right
/// before it runs, not after.
/// </remarks>
public static class NuGetPublishDecision
{
    /// <param name="isServerBuild">Whether CI is running the build.</param>
    /// <param name="forceTagRelease">The explicit local override, <c>--force-tag-release</c>.</param>
    /// <param name="isMainBranch">Whether the build is on the release branch.</param>
    /// <param name="fullSemVer">The version being packed. A <c>-</c> makes it a prerelease.</param>
    /// <param name="publishPrereleases">Whether prerelease versions may be published.</param>
    public static NuGetPublishVerdict Evaluate(
        bool isServerBuild,
        bool forceTagRelease,
        bool isMainBranch,
        string? fullSemVer,
        bool publishPrereleases)
    {
        if (!isServerBuild && !forceTagRelease)
            return new NuGetPublishVerdict(false,
                "not a server build - use --force-tag-release to publish from a developer machine");

        // A version with a pre-release label is a build of something still moving. Publishing
        // those by default would fill a public feed with every commit on every branch.
        var isPrerelease = fullSemVer?.Contains('-') == true;

        if (isPrerelease && !publishPrereleases)
            return new NuGetPublishVerdict(false,
                $"{fullSemVer} is a prerelease - set PublishPrereleases to publish these");

        // A stable version is the one people get by default, forever. It comes off the release
        // branch or it does not go.
        if (!isPrerelease && !isMainBranch)
            return new NuGetPublishVerdict(false,
                $"{fullSemVer ?? "this version"} is a stable release and this is not the main branch");

        return new NuGetPublishVerdict(true, $"publishing {fullSemVer ?? "packages"}");
    }
}
