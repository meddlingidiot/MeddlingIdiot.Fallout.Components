using Automation.Fallout.Builder.Models;
using Spectre.Console;

namespace Automation.Fallout.Builder.Services;

/// <summary>
/// Works out which MeddlingIdiot.Fallout.Components and Fallout.Common versions the migration should pin.
/// </summary>
/// <remarks>
/// The versions in <see cref="MigrationDefaults"/> are whatever was current on the AFTR Azure DevOps
/// feed when this tool was built. Pinning them unconditionally hands an Azure version to every
/// repository no matter where it actually restores from - a GitHub Packages repository ends up
/// pinned to a version its feeds have never heard of and fails restore with NU1102. The repository's
/// own feeds are asked first, and what the project already pins is preferred over the shipped
/// constant.
/// </remarks>
public static class MigrationPackageVersionResolver
{
    /// <summary>
    /// The nuget.org package id. Deliberately not the Automation.Fallout.Components namespace the
    /// package's types live in: that id belongs to the AFTR feed's diverged fork.
    /// </summary>
    public const string ComponentsPackageId = "MeddlingIdiot.Fallout.Components";

    /// <summary>
    /// Ids a repository may still reference, renamed to <see cref="ComponentsPackageId"/> on migrate.
    /// Their versions are never carried over - none of them exists under the nuget.org id.
    /// </summary>
    public static readonly string[] LegacyComponentsPackageIds =
        ["Automation.Nuke.Components", "Automation.Fallout.Components"];
    public const string FalloutCommonPackageId = "Fallout.Common";

    public static async Task<MigrationPackageVersions> ResolveAsync(
        string repositoryRoot, string buildProjectPath, MigrationOptions options)
    {
        var projectXml = File.Exists(buildProjectPath)
            ? await File.ReadAllTextAsync(buildProjectPath)
            : string.Empty;

        var components = await ResolveOneAsync(
            ComponentsPackageId, options.ComponentsVersion, MigrationDefaults.ComponentsVersion,
            repositoryRoot, projectXml);

        // Fallout.Common is deliberately not looked up. Its 11.x releases are unlisted, so a feed
        // search only sees the 10.x line - picking the "newest" would downgrade a working pin into
        // the NU1605 failure MigrationDefaults exists to avoid. The same goes for whatever the
        // project already pins: a blind Nuke -> Fallout rename leaves Nuke.Common's version behind.
        var falloutCommon = options.ResolvedFalloutCommonVersion;

        return new MigrationPackageVersions(components, falloutCommon);
    }

    private static async Task<string> ResolveOneAsync(
        string packageId,
        string? explicitVersion,
        string shippedDefault,
        string repositoryRoot,
        string projectXml)
    {
        if (explicitVersion != null)
            return explicitVersion;

        var feedVersion = await NuGetVersionResolver.GetLatestStableVersionAsync(packageId, repositoryRoot);

        // Only meaningful once the reference is already the nuget.org package - a repository still on
        // one of the LegacyComponentsPackageIds has nothing pinned under this id.
        var existingPin = CentralPackageManagement.ReadPackageReferenceVersion(projectXml, packageId);

        var chosen = Choose(feedVersion, existingPin, shippedDefault);

        AnsiConsole.MarkupLine(chosen.Source switch
        {
            VersionSource.Feed => $"[grey]{packageId} {chosen.Version} (newest on this repository's feeds)[/]",
            VersionSource.ExistingPin => $"[grey]{packageId} {chosen.Version} (kept - the feeds could not be reached)[/]",
            _ => $"[yellow]{packageId} {chosen.Version} (the version this tool shipped with - the feeds could not be reached and the project pins nothing)[/]"
        });

        return chosen.Version;
    }

    /// <summary>
    /// Newest on the feeds, then whatever the project already pins, then the version this tool
    /// shipped with. Split out from the lookup so the precedence can be tested without a network.
    /// </summary>
    public static (string Version, VersionSource Source) Choose(
        string? feedVersion, string? existingPin, string shippedDefault)
    {
        if (feedVersion != null)
            return (feedVersion, VersionSource.Feed);

        if (existingPin != null)
            return (existingPin, VersionSource.ExistingPin);

        return (shippedDefault, VersionSource.ShippedDefault);
    }

    public enum VersionSource
    {
        Feed,
        ExistingPin,
        ShippedDefault
    }
}
