using Automation.Fallout.Components.Parameters;
using Fallout.Common;
using Fallout.Common.CI.GitHubActions;
using Fallout.Common.Git;
using Fallout.Common.Tools.GitHub;
using Octokit;
using System.IO;
using Fallout.Common.IO;

namespace Automation.Fallout.Components.Components;

/// <summary>
/// Creates a GitHub release and comments on milestone issues after a tag is pushed.
/// Requires <see cref="ITagRelease"/> to be implemented on the build.
/// </summary>
public interface ICreateGitHubRelease : IFalloutBuild, IHasGitVersion, IHasGitHubPackages, IHasArtifacts
{
    [GitRepository]
    GitRepository GitRepository => TryGetValue(() => GitRepository);

    /// <summary>
    /// The milestone title to look up issues for. Defaults to the current version tag (e.g. "v1.2.3").
    /// Override this to use a different milestone naming convention.
    /// </summary>
    string MilestoneTitle => $"v{GitVersion.MajorMinorPatch}";

    Target CreateGitHubRelease => _ => _
        .TriggeredBy<ITagRelease>(x => x.TagRelease)
        .OnlyWhenStatic(() => GitRepository.IsOnMainOrMasterBranch())
        .OnlyWhenStatic(() => GitHubActions.Instance != null)
        .Executes(async () =>
        {
            var client = new GitHubClient(new ProductHeaderValue("Automation.Fallout.Components"))
            {
                Credentials = new Credentials(GitHubToken)
            };

            var owner = GitRepository.GetGitHubOwner();
            var repoName = GitRepository.GetGitHubName();

            // GitHub occasionally delivers one push twice, starting two identical runs. Both tag
            // the same version, and the second used to fail here trying to create a release the
            // first had already made — a red build for a release that had in fact gone out.
            if (await ReleaseExists(client, owner, repoName, MilestoneTitle))
            {
                Serilog.Log.Information(
                    "GitHub release {Tag} already exists, presumably from another run of this commit; skipping",
                    MilestoneTitle);
                return;
            }

            var milestones = await client.Issue.Milestone.GetAllForRepository(owner, repoName);
            var milestone = milestones.FirstOrDefault(m => m.Title == MilestoneTitle);
            var issues = milestone != null
                ? await client.Issue.GetAllForRepository(owner, repoName,
                    new RepositoryIssueRequest { Milestone = milestone.Number.ToString(), State = ItemStateFilter.All })
                : [];

            var releaseNotes = issues.Count > 0
                ? "## Issues\n\n" + string.Join("\n", issues.Select(i => $"- #{i.Number} {i.Title}"))
                : string.Empty;

            Release release;
            try
            {
                release = await client.Repository.Release.Create(
                    owner,
                    repoName,
                    new NewRelease(MilestoneTitle)
                    {
                        Name = MilestoneTitle,
                        Body = releaseNotes,
                        Draft = false,
                        Prerelease = false,
                    });
            }
            catch (ApiValidationException ex) when (IsAlreadyExists(ex))
            {
                // The check above lost a race: the other run created it in the meantime. It will
                // upload the assets and comment on the issues, so this one has nothing left to do.
                Serilog.Log.Information(
                    "GitHub release {Tag} was created by another run while this one was preparing; skipping",
                    MilestoneTitle);
                return;
            }

            Serilog.Log.Information("GitHub release created: {Url}", release.HtmlUrl);

            var packages = PackagePublishDirectory.GlobFiles("**/*.nupkg");
            foreach (var package in packages)
            {
                await using var stream = File.OpenRead(package);
                var assetUpload = new ReleaseAssetUpload
                {
                    FileName = Path.GetFileName(package),
                    ContentType = "application/octet-stream",
                    RawData = stream
                };
                await client.Repository.Release.UploadAsset(release, assetUpload);
                Serilog.Log.Information("Uploaded release asset: {Package}", package.Name);
            }

            foreach (var issue in issues)
                await client.Issue.Comment.Create(owner, repoName, issue.Number, $"Released in [{MilestoneTitle}]({release.HtmlUrl})! 🎉");
        });

    private static async Task<bool> ReleaseExists(GitHubClient client, string owner, string repoName, string tag)
    {
        try
        {
            await client.Repository.Release.Get(owner, repoName, tag);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// GitHub answers a duplicate release with 422 and an "already_exists" error on the tag.
    /// Any other validation failure is a real problem and must still fail the build.
    /// </summary>
    public static bool IsAlreadyExists(ApiValidationException ex) =>
        ex.ApiError?.Errors?.Any(e => string.Equals(e.Code, "already_exists", StringComparison.OrdinalIgnoreCase)) == true;
}
