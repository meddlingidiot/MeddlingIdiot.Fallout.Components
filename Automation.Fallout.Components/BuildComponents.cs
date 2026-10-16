namespace Automation.Fallout.Components;

// Keep in mind that this pipeline will be shared across 80+ projects
// and by multiple developers. It is using Nuke build v10. I want to
// make it easy to setup on a new project. Preferably as easy as
// the nuke :setup process. 


// I am wanting to simplify the installation process of getting a project
// setup to do the build. I am going to move the Automation.Fallout.Components 
// to a NuGet Package, but how do i do a nuke :setup then automate adding the 
// nuget package, and all other dependencies like GitVersion to get setup.
// Plus I have some simple things like asking what the "goal" of this build
// is which should install one of the DefaultBuilds (CompileBuild, TestBuild,
// PackageBuild, etc). I think the goal is to make a global tool like nuke is
// to build automate the installation so you could do "aftrnuke : setup" and 
// it would prompt for which Default build would you like to use, It would 
// dynamically load the builds based on which ones exist (code generation).
// Then it would generate the build script based on the selected Default build.
// It could ask some other questions: Do you want to fail the build on warnings? 
// Do you want to fail the build on code coverage? What is the min Code coverage
// (int, percentage)? Do you want to fail the bulid on secrets leaked? 

// Create it in a project called Automation.Fallout.Builder or something like that.
// Follow the pattern of Nuke's :setup process. Use Spectre Console for prompts.
// Evaluate the _build project for dependencies and install them. Also examine 
// Automation.Fallout.Components for DefaultBuilds and Parameters for configuration
// for the questions asked parameters.

// Done! This created a .config/dotnet-tools.json manifest file that your build
// server will automatically recognize. When the build server runs dotnet tool
// restore, it will install GitVersion.Tool automatically.

// upgrade build project to net10.0
// if needed run dotnet tool update -g Fallout.GlobalTool
// dotnet tool install GitVersion.Tool --version 6.8.2 --local
// run nuke :add-package GitVersion.Tool --version 6.8.2
// run nuke :add-package ReportGenerator --version 5.5.11
// Delete Configuration.cs in root of _build project
// include a nuget.config file
// include a GitVersion.yml file
// DON'T include a dotnet-tools.json file (use the one you build)
// Add package to _build project   <PackageReference Include="SharpZipLib" Version="1.4.2" />
// TODO
// dotnet tool install GitReleaseManager.Tool --local