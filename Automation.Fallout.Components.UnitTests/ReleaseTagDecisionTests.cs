using Automation.Fallout.Components.Components;

namespace Automation.Fallout.Components.UnitTests;

/// <summary>
/// When a release build creates, skips or refuses its v{version} tag. A tag that moves after
/// people have fetched it is worse than a missing one, so the only safe answer to a tag at the
/// wrong commit is to leave it and say so.
/// </summary>
public class ReleaseTagDecisionTests
{
    private const string Head = "aaaa111";
    private const string Elsewhere = "bbbb222";

    [Test]
    public async Task A_new_version_is_tagged_and_pushed()
    {
        var plan = ReleaseTagDecision.Decide(false, null, false, null, Head);

        await Assert.That(plan).IsEqualTo(new ReleaseTagPlan(TagStep.Write, TagStep.Write));
    }

    [Test]
    public async Task A_rerun_of_the_same_commit_does_nothing()
    {
        var plan = ReleaseTagDecision.Decide(true, Head, true, Head, Head);

        await Assert.That(plan).IsEqualTo(new ReleaseTagPlan(TagStep.AlreadyAtHead, TagStep.AlreadyAtHead));
    }

    [Test]
    public async Task A_tag_created_locally_but_never_pushed_is_pushed_now()
    {
        // The previous run died between tag and push.
        var plan = ReleaseTagDecision.Decide(true, Head, false, null, Head);

        await Assert.That(plan).IsEqualTo(new ReleaseTagPlan(TagStep.AlreadyAtHead, TagStep.Write));
    }

    [Test]
    public async Task A_tag_on_origin_at_another_commit_is_refused()
    {
        // Two commits built the same version number; the first one to tag keeps it.
        var plan = ReleaseTagDecision.Decide(false, null, true, Elsewhere, Head);

        await Assert.That(plan.Remote).IsEqualTo(TagStep.RefuseConflict);
    }

    [Test]
    public async Task A_local_tag_at_another_commit_is_not_overwritten()
    {
        var plan = ReleaseTagDecision.Decide(true, Elsewhere, true, Elsewhere, Head);

        await Assert.That(plan).IsEqualTo(new ReleaseTagPlan(TagStep.RefuseConflict, TagStep.RefuseConflict));
    }

    [Test]
    public async Task A_tag_whose_commit_could_not_be_read_counts_as_a_conflict()
    {
        // Existence and sha come from two git calls. If the second comes back empty, the tag
        // might be anywhere, so it is not assumed to be at HEAD.
        var plan = ReleaseTagDecision.Decide(true, null, false, null, Head);

        await Assert.That(plan.Local).IsEqualTo(TagStep.RefuseConflict);
    }

    [Test]
    public async Task The_sha_is_the_first_column_of_an_ls_remote_line()
    {
        using (Assert.Multiple())
        {
            await Assert.That(ReleaseTagDecision.ParseLsRemoteSha($"{Elsewhere}\trefs/tags/v1.2.3")).IsEqualTo(Elsewhere);
            await Assert.That(ReleaseTagDecision.ParseLsRemoteSha("")).IsNull();
            await Assert.That(ReleaseTagDecision.ParseLsRemoteSha("   ")).IsNull();
            await Assert.That(ReleaseTagDecision.ParseLsRemoteSha(null)).IsNull();
        }
    }
}
