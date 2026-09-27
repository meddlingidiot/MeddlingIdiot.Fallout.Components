using Automation.Fallout.Components.Parameters;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using System.Text;

namespace Automation.Fallout.Components.Components;

public interface IUpdateChangelog : IFalloutBuild, IHasGitVersion, IHasArtifacts
{
    [Parameter("Azure DevOps Personal Access Token (optional, for future enhancements)")]
    string DevOpsGitPatToken => TryGetValue(() => DevOpsGitPatToken) ??
        Environment.GetEnvironmentVariable("DEVOPSGITPATTOKEN") ?? string.Empty;

    AbsolutePath ChangelogPath => RootDirectory / "CHANGELOG.md";

    Target UpdateChangelog => t => t
        .After<ITest>(x => x.Test)
        .Description("Update changelog from git history")
        .Executes(() =>
        {
            var version = GitVersion.MajorMinorPatch;
            var semVer = GitVersion.FullSemVer;
            var currentTag = $"v{version}";

            Serilog.Log.Information("Generating changelog for version {Version}", semVer);

            // Get list of all tags
            var tagsProcess = ProcessTasks.StartProcess("git", "tag --sort=-version:refname", workingDirectory: RootDirectory);
            tagsProcess.WaitForExit();
            var tags = tagsProcess.Output
                .Select(x => x.Text.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith("v"))
                .ToList();

            Serilog.Log.Information("Found {Count} existing tags", tags.Count);

            var changelog = new StringBuilder();
            changelog.AppendLine("# Changelog");
            changelog.AppendLine();
            changelog.AppendLine($"All notable changes to this project will be documented in this file.");
            changelog.AppendLine();

            // Generate changelog for current version (if not tagged yet)
            string previousTag = tags.FirstOrDefault() ?? string.Empty;

            if (!tags.Contains(currentTag))
            {
                Serilog.Log.Information("Generating changelog for unreleased version {CurrentTag}", currentTag);
                GenerateChangelogSection(changelog, currentTag, previousTag, semVer, isReleased: false);
            }
            else
            {
                Serilog.Log.Information("Version {CurrentTag} already tagged, skipping unreleased section", currentTag);
            }

            // Generate changelog for all existing tags
            for (int i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                var prevTag = i + 1 < tags.Count ? tags[i + 1] : string.Empty;

                // Get tag date
                var dateProcess = ProcessTasks.StartProcess("git", $"log -1 --format=%aI {tag}", workingDirectory: RootDirectory);
                dateProcess.WaitForExit();
                var tagDate = dateProcess.Output.FirstOrDefault().Text.Trim() ?? DateTime.Now.ToString("yyyy-MM-dd");

                if (DateTime.TryParse(tagDate, out var parsedDate))
                {
                    tagDate = parsedDate.ToString("yyyy-MM-dd");
                }

                GenerateChangelogSection(changelog, tag, prevTag, tag.TrimStart('v'), isReleased: true, releaseDate: tagDate);
            }

            // Write changelog to file
            File.WriteAllText(ChangelogPath, changelog.ToString());
            Serilog.Log.Information("Changelog written to {ChangelogPath}", ChangelogPath);

            // Copy to artifacts directory
            ArtifactsDirectory.CreateDirectory();
            var artifactChangelog = ArtifactsDirectory / "CHANGELOG.md";
            ChangelogPath.Copy(artifactChangelog, ExistsPolicy.MergeAndOverwriteIfNewer);
            Serilog.Log.Information("Changelog copied to artifacts: {ArtifactPath}", artifactChangelog);
        });

    private void GenerateChangelogSection(StringBuilder changelog, string tag, string previousTag, string versionLabel, bool isReleased, string releaseDate = "")
    {
        changelog.AppendLine(ChangelogFormatter.Header(versionLabel, isReleased, releaseDate));
        changelog.AppendLine();

        // Get commits between tags
        string gitLogArgs;
        if (string.IsNullOrEmpty(previousTag))
        {
            gitLogArgs = $"log {tag} --pretty=format:\"%s|%h|%an\" --no-merges";
        }
        else
        {
            // For unreleased versions, use HEAD instead of the tag that doesn't exist yet
            var targetRef = isReleased ? tag : "HEAD";
            gitLogArgs = $"log {previousTag}..{targetRef} --pretty=format:\"%s|%h|%an\" --no-merges";
        }

        var logProcess = ProcessTasks.StartProcess("git", gitLogArgs, workingDirectory: RootDirectory);
        logProcess.WaitForExit();

        ChangelogFormatter.AppendCommits(changelog, logProcess.Output.Select(x => x.Text));
    }
}
