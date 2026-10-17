namespace Automation.Fallout.Builder.Models;

/// <summary>
/// Known-good versions and paths used when migrating a Nuke repository to Fallout.
/// </summary>
public static class MigrationDefaults
{
    /// <summary>
    /// The Fallout.Common version that matches the current Automation.Fallout.Components package.
    /// A blind Nuke -> Fallout rename leaves the old Nuke.Common version behind (e.g. 10.1.0), which
    /// resolves an unrelated 10.x Fallout.Common from nuget.org and fails restore with NU1605.
    /// </summary>
    public const string FalloutCommonVersion = "10.4.0";

    /// <summary>
    /// The MeddlingIdiot.Fallout.Components version to pin when the project still references one of
    /// the old Automation.* component packages. Last resort only - the repository's own feeds
    /// and its existing pin both win over this, so it does not track routine releases.
    /// </summary>
    /// <remarks>
    /// Bump this only when the Fallout.Common line moves. This package carries a hard dependency on
    /// one exact <see cref="FalloutCommonVersion"/>, and a pair that disagrees fails restore with
    /// NU1605. 1.0.24 is the first release under the nuget.org id and builds against 10.4.0; the
    /// AFTR feed's 1.0.14-beta.2 that used to sit here does not exist under this id.
    /// </remarks>
    public const string ComponentsVersion = "1.0.24";

    /// <summary>
    /// The Fallout CLI package id. The tool ships under two ids: fallout.globaltool carries the
    /// publicly listed 10.x line, fallout.cli carries 10.3.41-10.3.47 and the unlisted 11.x line.
    /// Both expose the same 'fallout' shim, so nothing that invokes it needs to change.
    /// </summary>
    public const string GlobalToolPackageId = "fallout.globaltool";

    /// <summary>
    /// Version of the Fallout CLI written into .config/dotnet-tools.json in place of nuke.globaltool.
    /// Kept on <see cref="FalloutCommonVersion"/>'s line: fallout.cli has no 10.4.0 build, so pinning
    /// that id at this version fails 'dotnet tool restore'.
    /// </summary>
    public const string GlobalToolVersion = "10.4.0";

    public const string TargetFramework = "net10.0";

    /// <summary>
    /// Fallout.Common drags in NuGet.Packaging 6.14.3, which drops NuGet.Frameworks 6.14.3.1 beside
    /// the build host. SDK 10.0.400's MSBuild binds NuGet.Frameworks 7.9.0.0 when it evaluates
    /// [MSBuild]::GetTargetFrameworkIdentifier, and the app-local copy wins probing, so every target
    /// that parses a project dies with a manifest mismatch. Pinning NuGet.Packaging to the version
    /// whose NuGet.Frameworks matches <see cref="SdkVersion"/> keeps the two in step.
    /// </summary>
    public const string NuGetPackagingVersion = "7.9.0";

    /// <summary>
    /// The .NET SDK written into global.json. Must stay in step with <see cref="NuGetPackagingVersion"/>:
    /// a different feature band ships a different NuGet.Frameworks and re-opens the mismatch above.
    /// </summary>
    public const string SdkVersion = "10.0.400";

    public const string GitVersionToolVersion = "6.8.2";

    public const string ReportGeneratorVersion = "5.5.11";

    /// <summary>Legacy Nuke configuration directory, replaced by <see cref="FalloutConfigDirectory"/>.</summary>
    public const string NukeConfigDirectory = ".nuke";

    public const string FalloutConfigDirectory = ".fallout";
}
