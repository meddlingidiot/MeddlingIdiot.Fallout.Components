namespace Automation.Fallout.Components.Components;

public enum VelopackSplashSource
{
    /// <summary>Pass no --splashImage; Velopack shows its own.</summary>
    VelopackDefault,

    /// <summary>The image named by VelopackSplashImagePath.</summary>
    Configured,

    /// <summary>The MeddlingIdiot logo embedded in this package.</summary>
    MeddlingIdiotDefault,
}

/// <param name="Path">Full path of the configured image; null for the other sources.</param>
/// <param name="Reason">One line for the build log.</param>
/// <param name="ConfiguredPathMissing">A path was configured but not found; log the reason as a warning.</param>
public sealed record VelopackSplashChoice(
    VelopackSplashSource Source, string? Path, string Reason, bool ConfiguredPathMissing = false);

/// <summary>
/// Which image Setup.exe shows while it installs.
/// </summary>
/// <remarks>
/// <para>
/// The MeddlingIdiot logo is the default only for installers published to MeddlingIdiot's own
/// storage account. This package is public, and the logo is not licensed to anyone else (see
/// Branding/LICENSE): a default that applied to every consumer would put MeddlingIdiot's
/// branding on other people's installers.
/// </para>
/// <para>
/// A configured path that does not exist is a warning rather than a silent skip, which is how
/// a mistyped VelopackIconPath behaves: a typo there just quietly ships the wrong icon.
/// </para>
/// </remarks>
public static class VelopackSplash
{
    public const string MeddlingIdiotBlobAccount = "meddlingidiotinstallers";

    /// <summary>VelopackSplashImagePath value that turns the splash override off.</summary>
    public const string None = "none";

    private const string ResourceName = "Automation.Fallout.Components.Branding.meddlingidiot-splash.png";

    public static VelopackSplashChoice Choose(string? configuredPath, string? azureBlobAccount, string rootDirectory)
    {
        var missingNote = "";

        if (string.Equals(configuredPath?.Trim(), None, StringComparison.OrdinalIgnoreCase))
        {
            return new(VelopackSplashSource.VelopackDefault, null,
                "VelopackSplashImagePath is 'none': using Velopack's default splash");
        }

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath, rootDirectory);
            if (File.Exists(fullPath))
            {
                return new(VelopackSplashSource.Configured, fullPath, $"Using splash image: {fullPath}");
            }

            missingNote = $"VelopackSplashImagePath '{configuredPath}' does not exist ({fullPath}); ";
        }

        if (string.Equals(azureBlobAccount, MeddlingIdiotBlobAccount, StringComparison.OrdinalIgnoreCase))
        {
            return new(VelopackSplashSource.MeddlingIdiotDefault, null,
                missingNote + "using the MeddlingIdiot splash", missingNote != "");
        }

        return new(VelopackSplashSource.VelopackDefault, null,
            missingNote + "using Velopack's default splash", missingNote != "");
    }

    /// <summary>Writes the embedded MeddlingIdiot logo to <paramref name="destination" />.</summary>
    public static void WriteMeddlingIdiotSplash(string destination)
    {
        using var resource = typeof(VelopackSplash).Assembly.GetManifestResourceStream(ResourceName)
                             ?? throw new InvalidOperationException(
                                 $"Embedded resource {ResourceName} is missing from the package.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var file = File.Create(destination);
        resource.CopyTo(file);
    }
}
