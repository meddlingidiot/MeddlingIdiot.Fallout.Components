using System.Xml.Linq;
using Automation.Fallout.Builder.Models;

namespace Automation.Fallout.Builder.Services;

public class BuildProjectMigrationResult
{
    public string Xml { get; set; } = string.Empty;

    /// <summary>Human readable description of every edit applied to the project file.</summary>
    public List<string> Notes { get; set; } = new();
}

/// <summary>
/// Repairs a build/_build.csproj that was renamed from Nuke to Fallout.
/// </summary>
public static class BuildProjectMigrator
{
    /// <summary>Pins the versions this tool shipped with. Prefer the overload taking resolved versions.</summary>
    public static BuildProjectMigrationResult Migrate(string projectXml, MigrationOptions options) =>
        Migrate(projectXml, MigrationPackageVersions.FromOptions(options));

    /// <param name="centrallyManaged">
    /// Whether a Directory.Packages.props governs this project. When it does the versions belong in
    /// that file and a Version on the PackageReference fails restore with NU1008, so the references
    /// are written bare and any version already on them is stripped. PackageDownload is untouched
    /// either way - central package management does not cover it.
    /// </param>
    public static BuildProjectMigrationResult Migrate(string projectXml, MigrationPackageVersions versions,
        bool centrallyManaged = false)
    {
        var doc = XDocument.Parse(projectXml);
        var root = doc.Root ?? throw new InvalidOperationException("The build project file has no root element.");
        var notes = new List<string>();

        RenameNukeProperties(root, notes);
        UpgradeTargetFramework(root, notes);
        MigratePackageReferences(root, versions, centrallyManaged, notes);
        UpsertPackageDownload(root, "GitVersion.Tool", MigrationDefaults.GitVersionToolVersion, notes);
        UpsertPackageDownload(root, "ReportGenerator", MigrationDefaults.ReportGeneratorVersion, notes);

        return new BuildProjectMigrationResult { Xml = Serialize(doc), Notes = notes };
    }

    /// <summary>NukeRootDirectory, NukeScriptDirectory, NukeTelemetryVersion, ...</summary>
    private static void RenameNukeProperties(XElement root, List<string> notes)
    {
        var properties = ByLocalName(root, "PropertyGroup")
            .SelectMany(g => g.Elements())
            .Where(e => e.Name.LocalName.StartsWith("Nuke", StringComparison.Ordinal))
            .ToList();

        foreach (var property in properties)
        {
            var oldName = property.Name.LocalName;
            var newName = string.Concat("Fallout", oldName.AsSpan("Nuke".Length));
            property.Name = property.Name.Namespace.GetName(newName);
            notes.Add($"Renamed <{oldName}> to <{newName}>");
        }
    }

    private static void UpgradeTargetFramework(XElement root, List<string> notes)
    {
        var targetFramework = ByLocalName(root, "TargetFramework").FirstOrDefault();
        if (targetFramework == null || targetFramework.Value == MigrationDefaults.TargetFramework)
            return;

        notes.Add($"Upgraded TargetFramework from {targetFramework.Value} to {MigrationDefaults.TargetFramework}");
        targetFramework.Value = MigrationDefaults.TargetFramework;
    }

    private static void MigratePackageReferences(XElement root, MigrationPackageVersions versions,
        bool centrallyManaged, List<string> notes)
    {
        var references = ByLocalName(root, "PackageReference").ToList();

        // Automation.Nuke.Components / Automation.Fallout.Components -> MeddlingIdiot.Fallout.Components.
        // Leaving an old id in place would add the new package beside it and compile every component
        // twice. No old version exists under the nuget.org id, so it always has to be re-pinned.
        var componentsId = MigrationPackageVersionResolver.ComponentsPackageId;
        foreach (var reference in references.Where(r => MigrationPackageVersionResolver.LegacyComponentsPackageIds
                     .Contains(Include(r), StringComparer.OrdinalIgnoreCase)))
        {
            notes.Add($"Renamed package {Include(reference)} to {componentsId}");
            SetInclude(reference, componentsId);
            reference.Attribute("Version")?.Remove();
        }

        // Anything else still on a Nuke package is dead weight - Fallout.Common replaces Nuke.Common.
        foreach (var reference in references.Where(r => Include(r).StartsWith("Nuke.", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            notes.Add($"Removed package reference {Include(reference)}");
            reference.Remove();
        }

        UpsertPackageReference(root, "Fallout.Common", versions.FalloutCommon, centrallyManaged, notes);
        UpsertPackageReference(root, componentsId, versions.Components, centrallyManaged, notes);

        // Not a Fallout package: this overrides the NuGet.Packaging that Fallout.Common transitively
        // pulls so the build host's NuGet.Frameworks matches the SDK's. See MigrationDefaults.
        UpsertPackageReference(root, "NuGet.Packaging", MigrationDefaults.NuGetPackagingVersion, centrallyManaged, notes);
    }

    private static void UpsertPackageReference(XElement root, string packageId, string version, bool centrallyManaged,
        List<string> notes)
    {
        var existing = ByLocalName(root, "PackageReference")
            .FirstOrDefault(r => string.Equals(Include(r), packageId, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            var group = ByLocalName(root, "ItemGroup").FirstOrDefault(g => g.Elements().Any(e => e.Name.LocalName == "PackageReference"));
            if (group == null)
            {
                group = new XElement(root.Name.Namespace.GetName("ItemGroup"));
                root.Add(group);
            }

            var reference = new XElement(root.Name.Namespace.GetName("PackageReference"),
                new XAttribute("Include", packageId));

            if (!centrallyManaged)
            {
                reference.SetAttributeValue("Version", version);
            }

            group.Add(reference);

            notes.Add(centrallyManaged
                ? $"Added package {packageId} (version pinned in {CentralPackageManagement.PropsFileName})"
                : $"Added package {packageId} {version}");
            return;
        }

        var currentVersion = existing.Attribute("Version");

        if (centrallyManaged)
        {
            if (currentVersion == null)
                return;

            currentVersion.Remove();
            notes.Add($"Moved the {packageId} version to {CentralPackageManagement.PropsFileName}");
            return;
        }

        // Snapshotted before the write: SetAttributeValue mutates this same attribute, so reading it
        // afterwards would report the new version as the old one.
        var previousVersion = currentVersion?.Value;
        if (previousVersion == version)
            return;

        existing.SetAttributeValue("Version", version);
        notes.Add(previousVersion == null
            ? $"Pinned {packageId} to {version}"
            : $"Corrected {packageId} from {previousVersion} to {version}");
    }

    private static void UpsertPackageDownload(XElement root, string packageId, string version, List<string> notes)
    {
        var pinned = $"[{version}]";
        var existing = ByLocalName(root, "PackageDownload")
            .FirstOrDefault(p => string.Equals(Include(p), packageId, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            var group = ByLocalName(root, "ItemGroup").FirstOrDefault(g => g.Elements().Any(e => e.Name.LocalName == "PackageDownload"));
            if (group == null)
            {
                group = new XElement(root.Name.Namespace.GetName("ItemGroup"));
                root.Add(group);
            }

            group.Add(new XElement(root.Name.Namespace.GetName("PackageDownload"),
                new XAttribute("Include", packageId),
                new XAttribute("Version", pinned)));

            notes.Add($"Added PackageDownload {packageId} {version}");
            return;
        }

        if (existing.Attribute("Version")?.Value == pinned)
            return;

        existing.SetAttributeValue("Version", pinned);
        notes.Add($"Updated PackageDownload {packageId} to {version}");
    }

    private static IEnumerable<XElement> ByLocalName(XContainer container, string localName) =>
        container.Descendants().Where(e => e.Name.LocalName == localName);

    private static string Include(XElement element) => element.Attribute("Include")?.Value ?? string.Empty;

    private static void SetInclude(XElement element, string value) => element.SetAttributeValue("Include", value);

    private static string Serialize(XDocument doc)
    {
        var declaration = doc.Declaration != null ? doc.Declaration + Environment.NewLine : string.Empty;
        return declaration + doc + Environment.NewLine;
    }
}
