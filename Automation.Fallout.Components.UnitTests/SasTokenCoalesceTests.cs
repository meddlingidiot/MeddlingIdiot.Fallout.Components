using Automation.Fallout.Components.Parameters;

namespace Automation.Fallout.Components.UnitTests;

/// <summary>
/// Picking the SAS token from the several places CI definitions put it. Getting this wrong
/// does not fail the build: the Velopack upload skips with a warning, so it has to be right here.
/// </summary>
public class SasTokenCoalesceTests
{
    [Test]
    public async Task The_first_real_value_wins()
    {
        await Assert.That(IHasVelopack.Coalesce("first", "second")).IsEqualTo("first");
    }

    [Test]
    public async Task Missing_and_blank_sources_are_passed_over()
    {
        // An unmapped Azure DevOps variable arrives as "", not null, and would shadow a later source.
        await Assert.That(IHasVelopack.Coalesce(null, "", "  ", "token")).IsEqualTo("token");
    }

    [Test]
    public async Task An_unexpanded_Azure_DevOps_macro_is_not_a_token()
    {
        await Assert.That(IHasVelopack.Coalesce("$(AzureBlobToken)", "token")).IsEqualTo("token");
    }

    [Test]
    public async Task A_value_that_merely_starts_like_a_macro_is_kept()
    {
        await Assert.That(IHasVelopack.Coalesce("$(not closed", "token")).IsEqualTo("$(not closed");
    }

    [Test]
    public async Task The_parameter_wins_over_the_environment()
    {
        var token = IHasVelopack.ResolveSasToken("from-parameter", Env(("AZURE_BLOB_SAS_TOKEN", "from-env")));

        await Assert.That(token).IsEqualTo("from-parameter");
    }

    [Test]
    public async Task AZURE_BLOB_SAS_TOKEN_wins_over_the_legacy_names()
    {
        var token = IHasVelopack.ResolveSasToken(null, Env(
            ("AZURE_BLOB_SAS_TOKEN", "current"),
            ("AZURE_BLOB_SAS_TOKEN_LOCAL", "legacy"),
            ("AZUREBLOBSASTOKEN", "older")));

        await Assert.That(token).IsEqualTo("current");
    }

    /// <summary>
    /// Most consumer workflows still map AZURE_BLOB_SAS_TOKEN_LOCAL or AZUREBLOBSASTOKEN. Dropping
    /// them would not fail those builds - the upload would skip and installers would stop shipping.
    /// </summary>
    [Test]
    [Arguments("AZURE_BLOB_SAS_TOKEN_LOCAL")]
    [Arguments("AZUREBLOBSASTOKEN")]
    public async Task A_legacy_name_is_still_read(string legacy)
    {
        var token = IHasVelopack.ResolveSasToken("$(AZURE_BLOB_SAS_TOKEN)", Env((legacy, "legacy")));

        await Assert.That(token).IsEqualTo("legacy");
    }

    [Test]
    public async Task No_token_anywhere_resolves_to_empty()
    {
        await Assert.That(IHasVelopack.ResolveSasToken(null, Env())).IsEqualTo("");
    }

    private static Func<string, string?> Env(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(v => v.Name == name).Value;

    [Test]
    public async Task Nothing_usable_resolves_to_empty_rather_than_null()
    {
        using (Assert.Multiple())
        {
            await Assert.That(IHasVelopack.Coalesce(null, "", "$(Unset)")).IsEqualTo("");
            await Assert.That(IHasVelopack.Coalesce()).IsEqualTo("");
        }
    }
}
