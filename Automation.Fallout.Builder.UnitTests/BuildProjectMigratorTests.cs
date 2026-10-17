using Automation.Fallout.Builder.Models;
using Automation.Fallout.Builder.Services;

namespace Automation.Fallout.Builder.UnitTests;

public class BuildProjectMigratorTests
{
    /// <summary>
    /// A _build.csproj exactly as a blind Nuke -> Fallout rename leaves it: the package ids are
    /// right but the versions are still the Nuke ones.
    /// </summary>
    private const string BlindlyRenamedProject = """
                                                 <Project Sdk="Microsoft.NET.Sdk">
                                                   <PropertyGroup>
                                                     <OutputType>Exe</OutputType>
                                                     <TargetFramework>net8.0</TargetFramework>
                                                     <FalloutRootDirectory>..</FalloutRootDirectory>
                                                   </PropertyGroup>
                                                   <ItemGroup>
                                                     <PackageReference Include="Automation.Fallout.Components" Version="1.0.47" />
                                                     <PackageReference Include="Fallout.Common" Version="10.1.0" />
                                                   </ItemGroup>
                                                 </Project>
                                                 """;

    private const string UntouchedNukeProject = """
                                                <Project Sdk="Microsoft.NET.Sdk">
                                                  <PropertyGroup>
                                                    <TargetFramework>net8.0</TargetFramework>
                                                    <NukeRootDirectory>..</NukeRootDirectory>
                                                    <NukeScriptDirectory>..</NukeScriptDirectory>
                                                    <NukeTelemetryVersion>1</NukeTelemetryVersion>
                                                  </PropertyGroup>
                                                  <ItemGroup>
                                                    <PackageReference Include="Automation.Nuke.Components" Version="1.0.47" />
                                                    <PackageReference Include="Nuke.Common" Version="10.1.0" />
                                                  </ItemGroup>
                                                </Project>
                                                """;

    [Test]
    public async Task Migrate_CorrectsTheFalloutCommonVersionLeftBehindByTheRename()
    {
        var result = BuildProjectMigrator.Migrate(BlindlyRenamedProject, new MigrationOptions());

        using (Assert.Multiple())
        {
            await Assert.That(result.Xml)
                .Contains($"""Include="Fallout.Common" Version="{MigrationDefaults.FalloutCommonVersion}" """.TrimEnd());
            await Assert.That(result.Xml).DoesNotContain("""Version="10.1.0" """.TrimEnd());
            await Assert.That(result.Notes).Contains(n => n.Contains("Corrected Fallout.Common from 10.1.0"));
        }
    }

    [Test]
    public async Task Migrate_RenamesNukeMsBuildProperties()
    {
        var result = BuildProjectMigrator.Migrate(UntouchedNukeProject, new MigrationOptions());

        using (Assert.Multiple())
        {
            await Assert.That(result.Xml).Contains("<FalloutRootDirectory>");
            await Assert.That(result.Xml).Contains("<FalloutScriptDirectory>");
            await Assert.That(result.Xml).Contains("<FalloutTelemetryVersion>");
            await Assert.That(result.Xml).DoesNotContain("<Nuke");
        }
    }

    [Test]
    public async Task Migrate_ReplacesNukePackagesWithFalloutPackages()
    {
        var result = BuildProjectMigrator.Migrate(UntouchedNukeProject, new MigrationOptions());

        using (Assert.Multiple())
        {
            await Assert.That(result.Xml).DoesNotContain("Nuke.Common");
            await Assert.That(result.Xml).DoesNotContain("Automation.Nuke.Components");
            await Assert.That(result.Xml).Contains("MeddlingIdiot.Fallout.Components");
            await Assert.That(result.Xml).Contains("Fallout.Common");
        }
    }

    [Test]
    public async Task Migrate_UpgradesTargetFrameworkAndAddsPackageDownloads()
    {
        var result = BuildProjectMigrator.Migrate(BlindlyRenamedProject, new MigrationOptions());

        using (Assert.Multiple())
        {
            await Assert.That(result.Xml).Contains($"<TargetFramework>{MigrationDefaults.TargetFramework}</TargetFramework>");
            await Assert.That(result.Xml).Contains($"""Include="GitVersion.Tool" Version="[{MigrationDefaults.GitVersionToolVersion}]" """.TrimEnd());
            await Assert.That(result.Xml).Contains($"""Include="ReportGenerator" Version="[{MigrationDefaults.ReportGeneratorVersion}]" """.TrimEnd());
        }
    }

    /// <summary>
    /// Fallout.Common's transitive NuGet.Packaging 6.14.3 puts NuGet.Frameworks 6.14.3.1 beside the
    /// build host and SDK 10.0.400's MSBuild binds 7.9.0.0, so every target that parses a project
    /// fails. The migration has to override it, not just rename Nuke packages.
    /// </summary>
    [Test]
    public async Task Migrate_PinsNuGetPackagingToMatchTheSdk()
    {
        var result = BuildProjectMigrator.Migrate(BlindlyRenamedProject, new MigrationOptions());

        await Assert.That(result.Xml)
            .Contains($"""Include="NuGet.Packaging" Version="{MigrationDefaults.NuGetPackagingVersion}" """.TrimEnd());
    }

    [Test]
    public async Task Migrate_HonoursExplicitVersionPins()
    {
        var options = new MigrationOptions
        {
            FalloutCommonVersion = "11.0.17",
            ComponentsVersion = "1.0.1"
        };

        var result = BuildProjectMigrator.Migrate(BlindlyRenamedProject, options);

        using (Assert.Multiple())
        {
            await Assert.That(result.Xml).Contains("""Include="Fallout.Common" Version="11.0.17" """.TrimEnd());
            await Assert.That(result.Xml).Contains("""Include="MeddlingIdiot.Fallout.Components" Version="1.0.1" """.TrimEnd());
        }
    }

    [Test]
    public async Task Migrate_IsIdempotent()
    {
        var options = new MigrationOptions();

        var first = BuildProjectMigrator.Migrate(BlindlyRenamedProject, options);
        var second = BuildProjectMigrator.Migrate(first.Xml, options);

        using (Assert.Multiple())
        {
            await Assert.That(second.Notes).IsEmpty();
            await Assert.That(second.Xml).IsEqualTo(first.Xml);
        }
    }

    /// <summary>
    /// A repository on central package management keeps its versions in Directory.Packages.props. A
    /// Version left on the PackageReference is not a style problem - restore fails with NU1008.
    /// </summary>
    [Test]
    public async Task Migrate_LeavesVersionsOffThePackageReferencesWhenVersionsAreManagedCentrally()
    {
        var result = BuildProjectMigrator.Migrate(BlindlyRenamedProject, new MigrationPackageVersions("1.0.2", "11.0.18"),
            centrallyManaged: true);

        using (Assert.Multiple())
        {
            await Assert.That(result.Xml).Contains("""<PackageReference Include="Fallout.Common" />""");
            await Assert.That(result.Xml).Contains("""<PackageReference Include="MeddlingIdiot.Fallout.Components" />""");
            await Assert.That(result.Notes).Contains(n => n.Contains("Moved the Fallout.Common version"));
        }
    }

    /// <summary>
    /// The exact shape autofallout migrate was handed by MeddlingIdiot.AppManager: already correctly
    /// versionless, and the migration used to stamp inline versions back onto it.
    /// </summary>
    [Test]
    public async Task Migrate_LeavesAnAlreadyCentrallyManagedProjectAlone()
    {
        const string centrallyManagedProject = """
                                               <Project Sdk="Microsoft.NET.Sdk">
                                                 <PropertyGroup>
                                                   <TargetFramework>net10.0</TargetFramework>
                                                 </PropertyGroup>
                                                 <ItemGroup>
                                                   <PackageReference Include="Fallout.Common" />
                                                   <PackageReference Include="MeddlingIdiot.Fallout.Components" />
                                                 </ItemGroup>
                                               </Project>
                                               """;

        var result = BuildProjectMigrator.Migrate(centrallyManagedProject,
            new MigrationPackageVersions("1.0.2", "11.0.18"), centrallyManaged: true);

        await Assert.That(result.Xml).DoesNotContain("""Include="Fallout.Common" Version=""");
        await Assert.That(result.Xml).DoesNotContain("""Include="MeddlingIdiot.Fallout.Components" Version=""");
    }

    /// <summary>
    /// Automation.Fallout.Components is the AFTR feed's id, not this package's. Adding the nuget.org
    /// id beside it would compile every component twice, and its version does not exist on nuget.org.
    /// </summary>
    [Test]
    public async Task Migrate_RenamesTheAutomationPackageIdToTheNuGetOrgId()
    {
        var result = BuildProjectMigrator.Migrate(BlindlyRenamedProject, new MigrationOptions());

        using (Assert.Multiple())
        {
            await Assert.That(result.Xml).DoesNotContain("""Include="Automation.Fallout.Components""");
            await Assert.That(result.Xml)
                .Contains($"""Include="MeddlingIdiot.Fallout.Components" Version="{MigrationDefaults.ComponentsVersion}" """.TrimEnd());
            await Assert.That(result.Xml).DoesNotContain("1.0.47");
            await Assert.That(result.Notes)
                .Contains("Renamed package Automation.Fallout.Components to MeddlingIdiot.Fallout.Components");
        }
    }

    /// <summary>
    /// Central package management covers PackageReference only, so the tool downloads keep the
    /// versions they pin.
    /// </summary>
    [Test]
    public async Task Migrate_KeepsPackageDownloadVersionsWhenVersionsAreManagedCentrally()
    {
        var result = BuildProjectMigrator.Migrate(BlindlyRenamedProject, new MigrationPackageVersions("1.0.2", "11.0.18"),
            centrallyManaged: true);

        using (Assert.Multiple())
        {
            await Assert.That(result.Xml)
                .Contains($"""Include="GitVersion.Tool" Version="[{MigrationDefaults.GitVersionToolVersion}]" """.TrimEnd());
            await Assert.That(result.Xml)
                .Contains($"""Include="ReportGenerator" Version="[{MigrationDefaults.ReportGeneratorVersion}]" """.TrimEnd());
        }
    }

    [Test]
    public async Task Migrate_IsIdempotentWhenVersionsAreManagedCentrally()
    {
        var versions = new MigrationPackageVersions("1.0.2", "11.0.18");

        var first = BuildProjectMigrator.Migrate(BlindlyRenamedProject, versions, centrallyManaged: true);
        var second = BuildProjectMigrator.Migrate(first.Xml, versions, centrallyManaged: true);

        using (Assert.Multiple())
        {
            await Assert.That(second.Notes).IsEmpty();
            await Assert.That(second.Xml).IsEqualTo(first.Xml);
        }
    }
}
