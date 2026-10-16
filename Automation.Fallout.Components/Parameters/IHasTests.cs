using Fallout.Common;

namespace Automation.Fallout.Components.Parameters;

public interface IHasTests : IFalloutBuild
{
    [Parameter("Break build on warnings - Default is 'true'")]
    bool BreakBuildOnWarnings => TryGetValue<bool?>(() => BreakBuildOnWarnings) ?? true;

    [Parameter] int MinCoverageThreshold => TryGetValue<int?>(() => MinCoverageThreshold) ?? 0;

    [Parameter("Upload coverage report to Codecov - Default is 'false'. Enable for public repositories.")]
    bool UploadToCodecov => TryGetValue<bool?>(() => UploadToCodecov) ?? false;

    [Parameter, Secret]
    string? CodecovToken => TryGetValue(() => CodecovToken) ?? Environment.GetEnvironmentVariable("CODECOV_TOKEN");
}
