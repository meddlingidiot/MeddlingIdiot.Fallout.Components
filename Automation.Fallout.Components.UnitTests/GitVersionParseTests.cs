using Automation.Fallout.Components.Parameters;

namespace Automation.Fallout.Components.UnitTests;

/// <summary>
/// Reading GitVersion 6.x output. Fallout's own [GitVersion] attribute throws on it, because the
/// tool writes some string-typed fields as JSON numbers; these pin the workaround in place.
/// </summary>
public class GitVersionParseTests
{
    private const string Json =
        """
        {
          "Major": 1,
          "Minor": 2,
          "Patch": 3,
          "MajorMinorPatch": "1.2.3",
          "FullSemVer": "1.2.3-beta.4",
          "BranchName": "main",
          "CommitsSinceVersionSource": 7,
          "WeightedPreReleaseNumber": 30004,
          "PreReleaseNumber": 4
        }
        """;

    [Test]
    public async Task Numbers_in_string_fields_are_read_as_their_text()
    {
        var version = GitVersionResolver.Parse(Json);

        using (Assert.Multiple())
        {
            await Assert.That(version.CommitsSinceVersionSource).IsEqualTo("7");
            await Assert.That(version.WeightedPreReleaseNumber).IsEqualTo("30004");
            await Assert.That(version.FullSemVer).IsEqualTo("1.2.3-beta.4");
            await Assert.That(version.MajorMinorPatch).IsEqualTo("1.2.3");
            await Assert.That(version.BranchName).IsEqualTo("main");
        }
    }

    [Test]
    public async Task Diagnostic_lines_around_the_json_are_ignored()
    {
        var output = $"INFO [26/09/26 12:00:00] Working directory: C:\\repo\n{Json}\nINFO done\n";

        await Assert.That(GitVersionResolver.Parse(output).FullSemVer).IsEqualTo("1.2.3-beta.4");
    }

    [Test]
    public async Task Output_with_no_json_fails_with_the_output_in_the_message()
    {
        await Assert.That(() => GitVersionResolver.Parse("fatal: not a git repository"))
            .Throws<Exception>()
            .WithMessageContaining("fatal: not a git repository");
    }
}
