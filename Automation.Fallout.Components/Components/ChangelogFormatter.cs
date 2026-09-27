using System.Text;
using System.Text.RegularExpressions;

namespace Automation.Fallout.Components.Components;

/// <summary>The changelog heading a commit is listed under.</summary>
public enum CommitKind
{
    Breaking,
    Feature,
    Fix,
    Refactor,
    Chore,
    Other
}

/// <summary>
/// Turns <c>git log --pretty=format:"%s|%h|%an"</c> lines into a changelog section.
/// </summary>
/// <remarks>
/// Pulled out of <see cref="IUpdateChangelog"/> so the conventional-commit rules can be tested
/// without a repository. The component still owns asking git; this owns what the answer means.
/// </remarks>
public static class ChangelogFormatter
{
    // Rendered in this order, which is the order a reader wants: what breaks them first.
    private static readonly (CommitKind Kind, string Heading)[] Sections =
    [
        (CommitKind.Breaking, "### ⚠ BREAKING CHANGES"),
        (CommitKind.Feature, "### ✨ Features"),
        (CommitKind.Fix, "### 🐛 Bug Fixes"),
        (CommitKind.Refactor, "### ♻️ Refactoring"),
        (CommitKind.Chore, "### 🔧 Chores"),
        (CommitKind.Other, "### 📝 Other Changes"),
    ];

    /// <summary>The <c>## …</c> line that opens a version's section.</summary>
    public static string Header(string versionLabel, bool isReleased, string releaseDate = "") =>
        isReleased
            ? $"## [{versionLabel}] - {releaseDate}"
            : $"## [Unreleased] - {versionLabel}";

    /// <summary>Which heading a commit subject belongs under.</summary>
    public static CommitKind Classify(string message)
    {
        // Check for breaking change indicator (! after type)
        // Examples: feat!:, fix!:, feat(scope)!:, fix(api)!:
        if (message.Contains("BREAKING", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(message, @"^(feat|fix|refactor|chore)(\([^)]*\))?!:", RegexOptions.IgnoreCase))
            return CommitKind.Breaking;

        // Match: feat:, feat(scope):, feature:, feature(scope):
        if (Regex.IsMatch(message, @"^feat(ure)?(\([^)]*\))?:", RegexOptions.IgnoreCase))
            return CommitKind.Feature;

        // Match: fix:, fix(scope):, bugfix:, bugfix(scope):
        if (Regex.IsMatch(message, @"^(fix|bugfix)(\([^)]*\))?:", RegexOptions.IgnoreCase))
            return CommitKind.Fix;

        if (Regex.IsMatch(message, @"^chore(\([^)]*\))?:", RegexOptions.IgnoreCase))
            return CommitKind.Chore;

        if (Regex.IsMatch(message, @"^refactor(\([^)]*\))?:", RegexOptions.IgnoreCase))
            return CommitKind.Refactor;

        return CommitKind.Other;
    }

    /// <summary>
    /// Appends the categorised commit lists for one version. Lines without a hash are skipped;
    /// no usable lines at all says so rather than leaving an empty heading.
    /// </summary>
    public static void AppendCommits(StringBuilder changelog, IEnumerable<string> gitLogLines)
    {
        var grouped = new Dictionary<CommitKind, List<string>>();

        foreach (var line in gitLogLines.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var parts = line.Split('|');
            if (parts.Length < 2) continue;

            var message = parts[0].Trim();
            var hash = parts[1].Trim();
            var kind = Classify(message);

            if (!grouped.TryGetValue(kind, out var items))
                grouped[kind] = items = [];

            items.Add($"- {message} ([{hash}](../../commit/{hash}))");
        }

        if (grouped.Count == 0)
        {
            changelog.AppendLine("No changes recorded.");
            changelog.AppendLine();
            return;
        }

        foreach (var (kind, heading) in Sections)
        {
            if (!grouped.TryGetValue(kind, out var items)) continue;

            changelog.AppendLine(heading);
            changelog.AppendLine();
            foreach (var item in items)
                changelog.AppendLine(item);
            changelog.AppendLine();
        }
    }
}
