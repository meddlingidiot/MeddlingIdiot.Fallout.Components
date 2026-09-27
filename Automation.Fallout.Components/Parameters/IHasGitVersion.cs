using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fallout.Common;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.GitVersion;

namespace Automation.Fallout.Components.Parameters;

public interface IHasGitVersion : IFalloutBuild
{
    // NOTE: Fallout's [GitVersion] attribute cannot be used with GitVersion 6.x.
    // Fallout's GitVersion model types CommitsSinceVersionSource and WeightedPreReleaseNumber
    // as string, but the tool emits them as JSON numbers. Under NUKE this was coerced silently
    // by Newtonsoft; Fallout 11 moved to System.Text.Json, which throws instead. We invoke the
    // tool ourselves and deserialize into the same GitVersion type with a lenient converter,
    // so the public shape of this property is unchanged. Revert to [GitVersion] once fixed upstream.
    GitVersion GitVersion => GitVersionResolver.Resolve(RootDirectory);
}

internal static class GitVersionResolver
{
    private static readonly object Gate = new();
    private static GitVersion? _cached;

    internal static GitVersion Resolve(string rootDirectory)
    {
        lock (Gate)
        {
            return _cached ??= Query(rootDirectory);
        }
    }

    private static GitVersion Query(string rootDirectory)
    {
        var toolPath = NuGetToolPathResolver.GetPackageExecutable(
            "GitVersion.Tool",
            "gitversion.dll",
            framework: "net8.0");

        return Parse(Run("dotnet", $"\"{toolPath}\" /nocache", rootDirectory));
    }

    /// <summary>GitVersion's console output, diagnostics and all, as a <see cref="GitVersion"/>.</summary>
    internal static GitVersion Parse(string output)
    {
        var json = ExtractJsonObject(output);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new NumberTolerantStringConverter());

        return JsonSerializer.Deserialize<GitVersion>(json, options)
               ?? throw new Exception($"GitVersion returned no version information:\n{output}");
    }

    private static string Run(string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
                            ?? throw new Exception($"Could not start '{fileName} {arguments}'");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception(
                $"GitVersion exited with code {process.ExitCode}.\n{stdout}\n{stderr}");
        }

        return stdout;
    }

    // GitVersion can emit diagnostic lines around the JSON payload; take the object only.
    private static string ExtractJsonObject(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new Exception($"Could not find JSON object in GitVersion output:\n{output}");
        }

        return output[start..(end + 1)];
    }
}

/// <summary>
/// Reads JSON numbers and booleans into string properties, matching the coercion Newtonsoft
/// performed before Fallout migrated to System.Text.Json.
/// </summary>
internal sealed class NumberTolerantStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt64(out var number)
                ? number.ToString()
                : reader.GetDouble().ToString(),
            JsonTokenType.True or JsonTokenType.False => reader.GetBoolean().ToString(),
            JsonTokenType.Null => null,
            _ => reader.GetString()
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
