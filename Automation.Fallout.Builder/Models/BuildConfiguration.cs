namespace Automation.Fallout.Builder.Models;

public class BuildConfiguration
{
    public string BuildType { get; set; } = string.Empty;
    public BuildPlatform Platform { get; set; } = BuildPlatform.AzureDevOps;
    public bool BreakBuildOnWarnings { get; set; } = true;
    public int MinCodeCoverage { get; set; } = 0;
    public bool EnableCodeCoverage { get; set; } = false;
    public string? VelopackProjectName { get; set; }
    public string? VelopackIconPath { get; set; }
}
