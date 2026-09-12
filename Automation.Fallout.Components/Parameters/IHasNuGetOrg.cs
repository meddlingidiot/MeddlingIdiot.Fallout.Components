using Fallout.Common;

namespace Automation.Fallout.Components.Parameters;

/// <summary>
/// Credentials and destination for publishing to a public NuGet feed.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately carries no account default. A shared build library that bakes in one team's
/// destination is how a valid credential ends up pointed at somebody else's feed — which is
/// exactly what <see cref="IHasVelopack.AzureBlobAccount"/> defaulting to another division's
/// storage account cost us, and it presented as an authentication failure rather than as the
/// misrouting it was.
/// </para>
/// </remarks>
public interface IHasNuGetOrg : IFalloutBuild
{
    [Parameter("API key for the public NuGet feed. Falls back to the NUGET_API_KEY environment variable.")]
    string NuGetApiKey => TryGetValue(() => NuGetApiKey)
                          ?? Environment.GetEnvironmentVariable("NUGET_API_KEY")
                          ?? throw new Exception(
                              "NuGetApiKey parameter or NUGET_API_KEY environment variable is required");

    [Parameter("Public NuGet feed to publish to. Defaults to nuget.org.")]
    string NuGetSource => TryGetValue(() => NuGetSource) ?? "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// The account the key belongs to, for the build log only.
    /// </summary>
    /// <remarks>
    /// nuget.org authenticates with the API key alone — there is nowhere to send a user name,
    /// and no way for a build to check that a key belongs to the account it expected. This
    /// exists so the log says who a release was published as, which is the difference between
    /// noticing a wrong key in the output and noticing it on the package page.
    /// </remarks>
    [Parameter("Account the NuGet API key belongs to. Logged, never transmitted.")]
    string? NuGetOwner => TryGetValue(() => NuGetOwner);
}
