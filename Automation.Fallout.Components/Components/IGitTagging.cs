using Automation.Fallout.Components.Parameters;
using Fallout.Common;
using Fallout.Common.Tooling;

namespace Automation.Fallout.Components.Components;

/// <summary>
/// Base interface for Git tagging operations with shared logic.
/// Inherit from this to create custom Git tagging strategies.
/// </summary>
public interface IGitTagging : IFalloutBuild, IHasGitVersion
{
    /// <summary>
    /// Shared Git helper to run commands and return captured stdout.
    /// Override this to customize how Git commands are executed.
    /// </summary>
    string? RunGitSingle(string args)
    {
        var p = ProcessTasks.StartProcess("git", args);
        p.AssertWaitForExit();
        return p.Output.Select(x => x.Text).LastOrDefault()?.Trim();
    }

    /// <summary>
    /// Configure Git identity for automated commits/tags.
    /// Override this to customize Git identity configuration.
    /// </summary>
    void ConfigureGitIdentity(string userName = "Azure DevOps Pipeline", string userEmail = "pipeline@azure.com")
    {
        ProcessTasks.StartProcess("git", $"config --local user.name {userName}")
            .AssertZeroExitCode();
        ProcessTasks.StartProcess("git", $"config --local user.email {userEmail}")
            .AssertZeroExitCode();
    }

    /// <summary>
    /// Core tagging logic. Override this to customize the entire tagging workflow.
    /// </summary>
    void PerformGitTagging()
    {
        ConfigureGitIdentity();

        var version = GitVersion.MajorMinorPatch;
        var tag = $"v{version}";
        Serilog.Log.Information("Tagging release with version {Version}", version);

        // check local tag
        var localCheck = ProcessTasks.StartProcess("git", $"rev-parse -q --verify refs/tags/{tag}");
        localCheck.AssertWaitForExit();
        var localExists = localCheck.ExitCode == 0;
        string? localSha = null;
        if (localExists)
            localSha = RunGitSingle($"rev-list -n 1 {tag}");

        // check remote tag (origin)
        var remoteProc = ProcessTasks.StartProcess("git", $"ls-remote --tags origin refs/tags/{tag}");
        remoteProc.AssertWaitForExit();
        var remoteSha = ReleaseTagDecision.ParseLsRemoteSha(remoteProc.Output.Select(x => x.Text).LastOrDefault());

        // current HEAD
        var headSha = RunGitSingle("rev-parse HEAD");

        var plan = ReleaseTagDecision.Decide(localExists, localSha, remoteSha != null, remoteSha, headSha);

        if (plan.Local != TagStep.Write)
        {
            if (plan.Local == TagStep.AlreadyAtHead)
            {
                Serilog.Log.Information("Local tag {Tag} already exists and points at HEAD. Skipping creation.",
                    tag);
            }
            else
            {
                Serilog.Log.Warning(
                    "Local tag {Tag} already exists and points at {localSha}. Refusing to overwrite.", tag,
                    localSha);
            }
        }
        else
        {
            // create annotated tag
            Serilog.Log.Information("Creating local tag {Tag}", tag);
            ProcessTasks.StartProcess("git", $"tag -a {tag} -m \"Release {tag}\"")
                .AssertZeroExitCode();
        }

        // decide whether to push
        if (plan.Remote != TagStep.Write)
        {
            if (plan.Remote == TagStep.AlreadyAtHead)
            {
                Serilog.Log.Information("Remote tag {Tag} already exists and matches HEAD. Skipping push.",
                    tag);
            }
            else
            {
                Serilog.Log.Warning(
                    "Remote tag {Tag} already exists and points at {RemoteSha}. Refusing to push. Resolve manually.",
                    tag, remoteSha);
            }
        }
        else
        {
            Serilog.Log.Information("Pushing tag {Tag} to origin", tag);
            var result = ProcessTasks.StartProcess("git", $"push --quiet origin {tag}");
            result.WaitForExit();
            if (result.ExitCode != 0)
            {
                throw new Exception($"Failed to push tag {tag}");
            }

            Serilog.Log.Information("Tag {Tag} pushed", tag);
        }
    }
}
