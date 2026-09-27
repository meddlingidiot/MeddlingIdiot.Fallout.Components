using System.Text;
using Automation.Fallout.Components.Components;

namespace Automation.Fallout.Components.UnitTests;

/// <summary>
/// How commit subjects land under changelog headings. The changelog is regenerated from the
/// whole history on every release, so a rule change here rewrites every past section too.
/// </summary>
public class ChangelogFormatterTests
{
    private static string Render(params string[] gitLogLines)
    {
        var changelog = new StringBuilder();
        ChangelogFormatter.AppendCommits(changelog, gitLogLines);
        return changelog.ToString().ReplaceLineEndings("\n");
    }

    [Test]
    [Arguments("feat: add a thing", CommitKind.Feature)]
    [Arguments("feat(builder): add a thing", CommitKind.Feature)]
    [Arguments("feature: add a thing", CommitKind.Feature)]
    [Arguments("Feat: shouted", CommitKind.Feature)]
    [Arguments("fix: mend it", CommitKind.Fix)]
    [Arguments("bugfix(ci): mend it", CommitKind.Fix)]
    [Arguments("refactor: move it", CommitKind.Refactor)]
    [Arguments("chore(pipelines): tidy", CommitKind.Chore)]
    [Arguments("Fix coverage", CommitKind.Other)]
    public async Task Conventional_commit_prefixes_pick_the_heading(string message, CommitKind expected)
    {
        await Assert.That(ChangelogFormatter.Classify(message)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("feat!: drop net8.0")]
    [Arguments("fix(api)!: rename the parameter")]
    [Arguments("chore: BREAKING remove the old target")]
    public async Task Anything_marked_breaking_is_listed_as_breaking(string message)
    {
        // Checked before the type, so a breaking fix is not filed away among ordinary fixes.
        await Assert.That(ChangelogFormatter.Classify(message)).IsEqualTo(CommitKind.Breaking);
    }

    [Test]
    public async Task A_word_that_only_starts_like_a_type_is_not_that_type()
    {
        // The colon is part of the prefix: "fixup" and "features" are not conventional commits.
        using (Assert.Multiple())
        {
            await Assert.That(ChangelogFormatter.Classify("fixup: squash me")).IsEqualTo(CommitKind.Other);
            await Assert.That(ChangelogFormatter.Classify("features: list")).IsEqualTo(CommitKind.Other);
        }
    }

    [Test]
    public async Task A_released_version_is_headed_with_its_date()
    {
        using (Assert.Multiple())
        {
            await Assert.That(ChangelogFormatter.Header("1.0.24", isReleased: true, "2026-09-11"))
                .IsEqualTo("## [1.0.24] - 2026-09-11");
            await Assert.That(ChangelogFormatter.Header("1.0.25-beta.1", isReleased: false))
                .IsEqualTo("## [Unreleased] - 1.0.25-beta.1");
        }
    }

    [Test]
    public async Task Sections_come_out_breaking_first_whatever_order_the_commits_came_in()
    {
        var rendered = Render(
            "chore: tidy|c1|Ann",
            "Fix coverage|o1|Ann",
            "fix: mend|f1|Ann",
            "feat!: drop net8.0|b1|Ann",
            "refactor: move|r1|Ann",
            "feat: add|a1|Ann");

        await Assert.That(rendered).IsEqualTo(
            """
            ### ⚠ BREAKING CHANGES

            - feat!: drop net8.0 ([b1](../../commit/b1))

            ### ✨ Features

            - feat: add ([a1](../../commit/a1))

            ### 🐛 Bug Fixes

            - fix: mend ([f1](../../commit/f1))

            ### ♻️ Refactoring

            - refactor: move ([r1](../../commit/r1))

            ### 🔧 Chores

            - chore: tidy ([c1](../../commit/c1))

            ### 📝 Other Changes

            - Fix coverage ([o1](../../commit/o1))


            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public async Task Commits_keep_their_git_log_order_within_a_heading()
    {
        var rendered = Render("fix: second|f2|Ann", "fix: first|f1|Ann");

        await Assert.That(rendered.IndexOf("f2", StringComparison.Ordinal))
            .IsLessThan(rendered.IndexOf("f1", StringComparison.Ordinal));
    }

    [Test]
    public async Task Headings_with_no_commits_are_left_out()
    {
        var rendered = Render("fix: mend|f1|Ann");

        using (Assert.Multiple())
        {
            await Assert.That(rendered).Contains("### 🐛 Bug Fixes");
            await Assert.That(rendered).DoesNotContain("### ✨ Features");
            await Assert.That(rendered).DoesNotContain("### 📝 Other Changes");
        }
    }

    [Test]
    public async Task A_version_with_no_commits_says_so()
    {
        await Assert.That(Render()).IsEqualTo("No changes recorded.\n\n");
    }

    [Test]
    public async Task Blank_lines_and_lines_without_a_hash_are_skipped()
    {
        using (Assert.Multiple())
        {
            await Assert.That(Render("", "   ", "no hash here")).IsEqualTo("No changes recorded.\n\n");
            await Assert.That(Render("  feat: padded | a1 | Ann  ")).Contains("- feat: padded ([a1](../../commit/a1))");
        }
    }
}
