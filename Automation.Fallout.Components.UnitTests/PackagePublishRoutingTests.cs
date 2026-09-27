using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.Parameters;

namespace Automation.Fallout.Components.UnitTests;

/// <summary>
/// Where a mirrored repository's packages go. The same commit builds on both CI systems, and
/// each run must push only to its own feed.
/// </summary>
public class PackagePublishRoutingTests
{
    [Test]
    [Arguments(true, false, PackagePublishTarget.GitHub)]
    [Arguments(false, true, PackagePublishTarget.AzureDevOps)]
    public async Task Auto_follows_the_CI_platform_running_the_build(
        bool onGitHubActions, bool onAzurePipelines, PackagePublishTarget expected)
    {
        var target = PackagePublishRouting.Resolve(PackagePublishTarget.Auto, onGitHubActions, onAzurePipelines);

        await Assert.That(target).IsEqualTo(expected);
    }

    [Test]
    public async Task Auto_on_a_developer_machine_pushes_nowhere()
    {
        var target = PackagePublishRouting.Resolve(PackagePublishTarget.Auto, false, false);

        await Assert.That(target).IsEqualTo(PackagePublishTarget.None);
    }

    [Test]
    public async Task GitHub_wins_when_both_platforms_look_present()
    {
        var target = PackagePublishRouting.Resolve(PackagePublishTarget.Auto, true, true);

        await Assert.That(target).IsEqualTo(PackagePublishTarget.GitHub);
    }

    [Test]
    [Arguments(PackagePublishTarget.AzureDevOps)]
    [Arguments(PackagePublishTarget.GitHub)]
    [Arguments(PackagePublishTarget.Both)]
    [Arguments(PackagePublishTarget.None)]
    public async Task An_explicit_target_is_used_whatever_the_environment(PackagePublishTarget requested)
    {
        using (Assert.Multiple())
        {
            await Assert.That(PackagePublishRouting.Resolve(requested, true, false)).IsEqualTo(requested);
            await Assert.That(PackagePublishRouting.Resolve(requested, false, true)).IsEqualTo(requested);
            await Assert.That(PackagePublishRouting.Resolve(requested, false, false)).IsEqualTo(requested);
        }
    }
}
