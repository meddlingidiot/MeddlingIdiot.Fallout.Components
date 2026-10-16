using Fallout.Common;

namespace Automation.Fallout.Components.Components;

/// <summary>
/// Orchestrates all testing steps: unit tests, integration tests, and coverage reporting.
///
/// To customize individual steps, implement the specific interfaces:
/// - <see cref="ITestExecution"/> - Override RunTests() to change how all tests execute
/// - <see cref="IRunUnitTests"/> - Replace just unit test execution
/// - <see cref="IRunIntegrationTests"/> - Replace just integration test execution
/// - <see cref="IGenerateCoverageReport"/> - Replace just coverage reporting
///
/// Example: To change test execution for all tests, implement ITestExecution:
/// <code>
/// class Build : FalloutBuild, ITest, ITestExecution
/// {
///     void ITestExecution.RunTests(string filter)
///     {
///         // Custom test execution logic for both unit and integration tests
///     }
/// }
/// </code>
/// </summary>
public interface ITest : IRunUnitTests, IRunIntegrationTests, IGenerateCoverageReport
{
    /// <summary>
    /// Main test target that runs all tests and generates coverage report.
    /// </summary>
    Target Test => t => t
        .DependsOn<IGenerateCoverageReport>(x => x.CoverageReport)
        .Description("Run all tests with coverage")
        .Executes(() => { });
}
