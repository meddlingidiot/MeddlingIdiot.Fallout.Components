using Fallout.Common;

namespace Automation.Fallout.Components.Parameters;

public interface IHasVelopack: IFalloutBuild
{
    [Parameter] string VelopackProjectName => TryGetValue(() => VelopackProjectName);
    [Parameter] string VelopackPackTitle => TryGetValue(() => VelopackPackTitle) ??
                                            "";
    [Parameter] string VelopackIconPath => TryGetValue(() => VelopackIconPath) ??
                                           "";
    // Image Setup.exe shows while installing (png/jpg/gif; relative to the repo root). Unset
    // means the MeddlingIdiot logo for builds publishing to meddlingidiotinstallers, Velopack's
    // own splash otherwise; "none" forces Velopack's. See VelopackSplash.
    [Parameter] string VelopackSplashImagePath => TryGetValue(() => VelopackSplashImagePath) ??
                                                  "";
    [Parameter] string VelopackChannel => TryGetValue(() => VelopackChannel) ?? 
                                          "win";
    [Parameter] string VelopackBlobContainer => TryGetValue(() => VelopackBlobContainer) ?? 
                                                "installers";
    [Parameter] string AzureBlobAccount => TryGetValue(() => AzureBlobAccount) ?? 
                                           "staftrinstallers";
    [Parameter] string AzureBlobEndpoint => TryGetValue(() => AzureBlobEndpoint) ?? 
                                            "https://staftrinstallers.blob.core.windows.net";
    // The SAS token the Velopack targets use: --azure-blob-sas-token, or the AZURE_BLOB_SAS_TOKEN
    // environment variable, which is the secret's name in both the MeddlingIdiot GitHub org and
    // AFTR's Azure DevOps 'Secrets' group. There is deliberately no default: a SAS token is a live
    // credential and does not belong in source. When it is missing the Velopack targets log a
    // warning and skip the download/upload rather than failing the build.
    [Parameter, Secret] string AzureBlobSasToken =>
        ResolveSasToken(TryGetValue(() => AzureBlobSasToken), Environment.GetEnvironmentVariable);

    /// <summary>
    /// Names older CI definitions still map the token to. They are read so a repository that has
    /// not been updated keeps publishing - dropping them would not fail its build, it would skip
    /// the upload with a warning and stop shipping installers - but each use logs a warning
    /// naming the variable to rename.
    /// </summary>
    internal static readonly string[] LegacySasTokenVariables = ["AZURE_BLOB_SAS_TOKEN_LOCAL", "AZUREBLOBSASTOKEN"];

    internal static string ResolveSasToken(string? parameter, Func<string, string?> getEnvironmentVariable)
    {
        var token = Coalesce(parameter, getEnvironmentVariable("AZURE_BLOB_SAS_TOKEN"));
        if (token.Length > 0)
            return token;

        foreach (var legacy in LegacySasTokenVariables)
        {
            token = Coalesce(getEnvironmentVariable(legacy));
            if (token.Length == 0)
                continue;

            Serilog.Log.Warning("The blob SAS token came from {Legacy}; map it to AZURE_BLOB_SAS_TOKEN instead", legacy);
            return token;
        }

        return "";
    }

    /// <summary>
    /// The value, or "" when it is blank or an unexpanded Azure DevOps macro. Plain ?? is not
    /// enough here: an undefined Azure DevOps variable arrives as the literal string "$(Name)"
    /// and an unmapped one as "", and both are non-null.
    /// </summary>
    internal static string Coalesce(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            // An unexpanded Azure DevOps macro, e.g. "$(AZURE_BLOB_SAS_TOKEN)" when the variable
            // group has no such variable. Treat it as absent rather than handing it to vpk as a token.
            if (value.StartsWith("$(", StringComparison.Ordinal) && value.EndsWith(")", StringComparison.Ordinal))
                continue;

            return value;
        }

        return "";
    }
    [Parameter] string AzureBlobTimeout => TryGetValue(() => AzureBlobTimeout) ?? 
                                           "30";
    [Parameter] int KeepMaxReleases => TryGetValue<int?>(() => KeepMaxReleases) ?? 3;
}