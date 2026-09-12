using Automation.Fallout.Components.Components;

namespace Automation.Fallout.Components.UnitTests;

/// <summary>
/// The rules about when a build may publish to a public feed. Worth testing on their own
/// because the mistake they prevent cannot be undone: a version pushed to nuget.org can be
/// unlisted, but never deleted, and its number can never be used again.
/// </summary>
public class NuGetPublishDecisionTests
{
    private static NuGetPublishVerdict Evaluate(
        bool isServerBuild = true,
        bool forceTagRelease = false,
        bool isMainBranch = true,
        string? version = "1.2.3",
        bool publishPrereleases = false) =>
        NuGetPublishDecision.Evaluate(isServerBuild, forceTagRelease, isMainBranch, version, publishPrereleases);

    [Test]
    public async Task A_stable_version_on_main_in_CI_publishes()
    {
        var verdict = Evaluate();

        using (Assert.Multiple())
        {
            await Assert.That(verdict.ShouldPush).IsTrue();
            await Assert.That(verdict.Reason).Contains("1.2.3");
        }
    }

    [Test]
    public async Task A_developer_machine_publishes_nothing_by_accident()
    {
        var verdict = Evaluate(isServerBuild: false);

        using (Assert.Multiple())
        {
            await Assert.That(verdict.ShouldPush).IsFalse();
            await Assert.That(verdict.Reason).Contains("--force-tag-release");
        }
    }

    [Test]
    public async Task A_developer_machine_publishes_when_it_is_asked_to()
    {
        var verdict = Evaluate(isServerBuild: false, forceTagRelease: true);

        await Assert.That(verdict.ShouldPush).IsTrue();
    }

    [Test]
    public async Task A_prerelease_stays_off_the_public_feed()
    {
        // Otherwise every commit on every branch that happens to build ends up published.
        var verdict = Evaluate(version: "1.2.3-beta.4");

        using (Assert.Multiple())
        {
            await Assert.That(verdict.ShouldPush).IsFalse();
            await Assert.That(verdict.Reason).Contains("prerelease");
        }
    }

    [Test]
    public async Task A_prerelease_publishes_when_the_repository_asks_for_it()
    {
        var verdict = Evaluate(version: "1.2.3-beta.4", isMainBranch: false, publishPrereleases: true);

        await Assert.That(verdict.ShouldPush).IsTrue();
    }

    [Test]
    public async Task A_stable_version_off_main_does_not_publish()
    {
        // The version people get by default has to come off the release branch.
        var verdict = Evaluate(isMainBranch: false);

        using (Assert.Multiple())
        {
            await Assert.That(verdict.ShouldPush).IsFalse();
            await Assert.That(verdict.Reason).Contains("main branch");
        }
    }

    [Test]
    public async Task Allowing_prereleases_does_not_let_a_stable_version_out_of_a_branch()
    {
        // The switch is about prerelease versions, not about relaxing where releases come from.
        var verdict = Evaluate(isMainBranch: false, publishPrereleases: true);

        await Assert.That(verdict.ShouldPush).IsFalse();
    }

    [Test]
    public async Task A_missing_version_is_treated_as_stable()
    {
        // GitVersion unavailable falls back to a local version upstream; stable rules apply,
        // so it still cannot escape a feature branch.
        using (Assert.Multiple())
        {
            await Assert.That(Evaluate(version: null).ShouldPush).IsTrue();
            await Assert.That(Evaluate(version: null, isMainBranch: false).ShouldPush).IsFalse();
        }
    }
}
