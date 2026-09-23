using System.Net;
using Automation.Fallout.Components.Components;
using Octokit;

namespace Automation.Fallout.Components.UnitTests;

/// <summary>
/// A second run of the same commit must skip a release that already exists, but any other
/// rejection from GitHub still has to fail the build: a release that silently never gets made
/// is worse than a red run.
/// </summary>
public class CreateGitHubReleaseTests
{
    private static ApiValidationException Rejection(string body) => new(new FakeResponse(body));

    // Octokit's own Response is internal; ApiException only needs the status and the JSON body.
    private sealed class FakeResponse(string body) : IResponse
    {
        public object Body => body;
        public IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>();
        public ApiInfo ApiInfo => null!;
        public HttpStatusCode StatusCode => (HttpStatusCode)422;
        public string ContentType => "application/json";
    }

    [Test]
    public async Task A_release_that_already_exists_is_recognised()
    {
        var ex = Rejection("""
            {"message":"Validation Failed",
             "errors":[{"resource":"Release","code":"already_exists","field":"tag_name"}]}
            """);

        await Assert.That(ICreateGitHubRelease.IsAlreadyExists(ex)).IsTrue();
    }

    [Test]
    public async Task Any_other_validation_failure_still_fails_the_build()
    {
        var ex = Rejection("""
            {"message":"Validation Failed",
             "errors":[{"resource":"Release","code":"invalid","field":"target_commitish"}]}
            """);

        await Assert.That(ICreateGitHubRelease.IsAlreadyExists(ex)).IsFalse();
    }

    [Test]
    public async Task A_rejection_without_details_is_not_treated_as_a_duplicate()
    {
        var ex = Rejection("""{"message":"Validation Failed"}""");

        await Assert.That(ICreateGitHubRelease.IsAlreadyExists(ex)).IsFalse();
    }
}
