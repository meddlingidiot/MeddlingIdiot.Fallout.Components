using System.Text.RegularExpressions;

namespace Automation.Fallout.Builder.Services;

/// <summary>
/// Strips secret scanning out of a build class. IScanForSecrets and the BreakBuildOnSecretLeaks
/// parameter that gated it were removed from the components, so a build class still naming either
/// fails with CS0246 on the interface or CS0539 on the explicit override.
/// </summary>
public static class SecretScanningMigrator
{
    /// <summary>The Gitleaks configuration files setup used to drop at the repository root.</summary>
    public static readonly string[] ConfigFiles = { ".gitleaks.toml", "gitleaks.toml" };

    /// <summary>
    /// A line that uses the removed API rather than just listing the interface: an explicit member
    /// such as <c>bool IHasTests.BreakBuildOnSecretLeaks => false;</c>, or a chained call such as
    /// <c>.DependsOn&lt;IScanForSecrets&gt;(x =&gt; x.ScanForSecrets)</c>. The comment lines directly
    /// above it go with it - they describe the line being removed - and so does the blank line
    /// above them when it would otherwise be doubled up or left dangling before the closing brace.
    /// </summary>
    private static readonly Regex RemovedApiLine = new(
        @"(?<blank>^[ \t]*\r?\n)?(?:^[ \t]*//[^\r\n]*\r?\n)*^[^\r\n]*(?:\bIScanForSecrets[ \t]*[.>]|\bIHasTests[ \t]*\.[ \t]*BreakBuildOnSecretLeaks\b)[^\r\n]*(?:\r?\n|$)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>IScanForSecrets as the first interface after the colon, with the comma that follows it.</summary>
    private static readonly Regex FirstBaseListEntry = new(@"(?<=:\s*)\bIScanForSecrets\b\s*,[ \t]*", RegexOptions.Compiled);

    /// <summary>IScanForSecrets anywhere else in a base list, with the comma that precedes it.</summary>
    private static readonly Regex BaseListEntry = new(@"[ \t]*,\s*\bIScanForSecrets\b", RegexOptions.Compiled);

    /// <summary>
    /// What follows a removed line that makes the blank line above it redundant: another blank line,
    /// which already separates what is left, or the closing brace, which needs no separating.
    /// </summary>
    private static readonly Regex NoSeparatorNeeded = new(@"\G(?:[ \t]*\r?\n|\s*\})", RegexOptions.Compiled);

    /// <summary>What is still left naming secret scanning once the rewrite has run, so it can be flagged.</summary>
    private static readonly Regex Leftover = new(@"\b(?:IScanForSecrets|ScanForSecrets|BreakBuildOnSecretLeaks|GitleaksVersion|GitleaksToolPath)\b",
        RegexOptions.Compiled);

    public static string Rewrite(string source, out IReadOnlyList<string> notes)
    {
        var changes = new List<string>();
        notes = changes;

        var removedLines = false;
        var rewritten = RemovedApiLine.Replace(source, match =>
        {
            removedLines = true;
            var blank = match.Groups["blank"];
            return blank.Success && !NoSeparatorNeeded.IsMatch(source, match.Index + match.Length) ? blank.Value : string.Empty;
        });

        if (removedLines)
        {
            changes.Add("Removed the BreakBuildOnSecretLeaks override and other IScanForSecrets members - secret scanning is no longer part of Fallout");
        }

        var withoutInterface = BaseListEntry.Replace(FirstBaseListEntry.Replace(rewritten, string.Empty), string.Empty);
        if (!string.Equals(withoutInterface, rewritten, StringComparison.Ordinal))
        {
            changes.Add("Removed IScanForSecrets from the build class - secret scanning is no longer part of Fallout");
        }

        return withoutInterface;
    }

    /// <summary>
    /// The secret scanning references <see cref="Rewrite"/> could not remove on its own, such as a
    /// member body spread over several lines. Each one still fails to compile.
    /// </summary>
    public static IReadOnlyList<string> FindLeftovers(string source) =>
        Leftover.Matches(source).Select(match => match.Value).Distinct(StringComparer.Ordinal).ToList();
}
