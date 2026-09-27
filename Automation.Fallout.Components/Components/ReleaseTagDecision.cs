namespace Automation.Fallout.Components.Components;

/// <summary>What to do about one copy of a release tag, local or on origin.</summary>
public enum TagStep
{
    /// <summary>No such tag yet: create it (locally) or push it (to origin).</summary>
    Write,

    /// <summary>The tag already points at HEAD. Nothing to do.</summary>
    AlreadyAtHead,

    /// <summary>The tag exists at another commit. Leave it alone and say so.</summary>
    RefuseConflict
}

/// <summary>The local and remote steps for tagging a release.</summary>
public readonly record struct ReleaseTagPlan(TagStep Local, TagStep Remote);

/// <summary>
/// The create-or-refuse rules <see cref="IGitTagging.PerformGitTagging"/> applies, as a
/// function of what git reported.
/// </summary>
/// <remarks>
/// A release tag is never moved: once v1.2.3 names a commit, pointing it anywhere else rewrites
/// history for everyone who fetched it. So an existing tag is either already right or refused.
/// </remarks>
public static class ReleaseTagDecision
{
    public static ReleaseTagPlan Decide(
        bool localExists, string? localSha,
        bool remoteExists, string? remoteSha,
        string? headSha) =>
        new(Step(localExists, localSha, headSha), Step(remoteExists, remoteSha, headSha));

    /// <summary>
    /// The commit sha from a <c>git ls-remote</c> line (<c>&lt;sha&gt;\t&lt;ref&gt;</c>), or
    /// <c>null</c> for a blank line, which is how ls-remote says the tag is not there.
    /// </summary>
    public static string? ParseLsRemoteSha(string? line) =>
        string.IsNullOrWhiteSpace(line) ? null : line.Split('\t')[0];

    private static TagStep Step(bool exists, string? sha, string? headSha) =>
        !exists ? TagStep.Write
        : sha == headSha ? TagStep.AlreadyAtHead
        : TagStep.RefuseConflict;
}
