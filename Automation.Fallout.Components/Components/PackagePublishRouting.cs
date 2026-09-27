using Automation.Fallout.Components.Parameters;

namespace Automation.Fallout.Components.Components;

/// <summary>
/// How <see cref="IPackageMultiPlatform"/> resolves <see cref="PackagePublishTarget.Auto"/>,
/// kept apart from the environment so it can be tested without faking CI variables.
/// </summary>
public static class PackagePublishRouting
{
    /// <param name="requested">The --publish-target value, <see cref="PackagePublishTarget.Auto"/> by default.</param>
    /// <param name="onGitHubActions">Whether GITHUB_ACTIONS is set.</param>
    /// <param name="onAzurePipelines">Whether TF_BUILD is set.</param>
    public static PackagePublishTarget Resolve(
        PackagePublishTarget requested,
        bool onGitHubActions,
        bool onAzurePipelines)
    {
        if (requested != PackagePublishTarget.Auto)
            return requested;

        if (onGitHubActions)
            return PackagePublishTarget.GitHub;

        if (onAzurePipelines)
            return PackagePublishTarget.AzureDevOps;

        // A developer machine: build the packages, push them nowhere.
        return PackagePublishTarget.None;
    }
}
