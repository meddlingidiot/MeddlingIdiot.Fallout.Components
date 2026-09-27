# Changelog

All notable changes to this project will be documented in this file.

## [1.0.24] - 2026-09-11

### 📝 Other Changes

- Fix coverage ([e9295b5](../../commit/e9295b5))
- Publish to nuget.org under the MeddlingIdiot name ([a55eb77](../../commit/a55eb77))

## [1.0.23] - 2026-09-07

### 🐛 Bug Fixes

- fix(gitversion): align pull request branch mode with mainline ([43b6fe5](../../commit/43b6fe5))

## [1.0.22] - 2026-09-07

### 🐛 Bug Fixes

- fix(gitversion): add pull request branch configuration ([5658dbf](../../commit/5658dbf))

## [1.0.21] - 2026-09-03

### 🐛 Bug Fixes

- fix(ci): install .NET 8 runtime for net8.0 test projects ([a57c431](../../commit/a57c431))
- fix(ci): use windows-latest agent for Fallout pipeline ([3a08593](../../commit/3a08593))

## [1.0.20] - 2026-09-03

### 🐛 Bug Fixes

- fix(azure-pipelines): correct git argument binding and mask auth header ([d6477ba](../../commit/d6477ba))

## [1.0.19] - 2026-09-03

### 🐛 Bug Fixes

- fix(pipelines): make on-prem mirror failures diagnose themselves ([6733e24](../../commit/6733e24))

## [1.0.18] - 2026-09-03

### 🐛 Bug Fixes

- fix(pipelines): restore agent credential fallback in on-prem mirror ([769f614](../../commit/769f614))

## [1.0.17] - 2026-08-28

### 🔧 Chores

- chore(pipelines): skip on-prem mirror when agent cannot reach server ([8bfca0e](../../commit/8bfca0e))

## [1.0.16] - 2026-08-28

### 🔧 Chores

- chore(pipelines): add on-prem mirror pipeline ([af94bdb](../../commit/af94bdb))

## [1.0.15] - 2026-08-13

### 📝 Other Changes

- a fix for the GitHub side of the fence. Restored Github static site token. ([dd66ac4](../../commit/dd66ac4))

## [1.0.14] - 2026-08-13

### ✨ Features

- feat(builder): add NuGet.Packaging migration to build project migrator ([f40002e](../../commit/f40002e))

### 🐛 Bug Fixes

- fix(builder): align migration defaults with current Fallout package ids ([d5e9662](../../commit/d5e9662))
- fix(build): pin NuGet.Packaging to match SDK probing ([ca726ac](../../commit/ca726ac))

## [1.0.13] - 2026-08-11

### 🐛 Bug Fixes

- fix(ci): resolve blob SAS token from secrets variable group ([fe22158](../../commit/fe22158))

### 📝 Other Changes

- build: update tool and test package versions ([0471dbf](../../commit/0471dbf))

## [1.0.12] - 2026-08-10

### 📝 Other Changes

- Revert back to the installers container ([b870817](../../commit/b870817))

## [1.0.11] - 2026-08-10

### ✨ Features

- feat(velopack): choose default blob container by build host ([192c972](../../commit/192c972))

## [1.0.10] - 2026-08-07

### 🔧 Chores

- chore(build): bump Automation.Fallout.Components package version ([0916589](../../commit/0916589))

## [1.0.9] - 2026-08-07

### 🐛 Bug Fixes

- fix(azure-pipelines): resolve blob SAS token from variable group ([4fe10dd](../../commit/4fe10dd))

## [1.0.8] - 2026-08-07

### 📝 Other Changes

- ci(github-actions): simplify build runner configuration ([671a5d4](../../commit/671a5d4))

## [1.0.7] - 2026-08-06

### 📝 Other Changes

- ci(github-actions): grant workflows write permission for build job ([5673e2b](../../commit/5673e2b))

## [1.0.6] - 2026-08-06

### 📝 Other Changes

- ci(github-actions): run build on self-hosted Windows runner ([14d7ef8](../../commit/14d7ef8))

## [1.0.5] - 2026-08-05

### 🐛 Bug Fixes

- fix(velopack): resolve vpk on demand and fall back to dotnet tools ([f929732](../../commit/f929732))

## [1.0.4] - 2026-08-05

### 📝 Other Changes

- ci(pipeline): MultiPlatform deploy ([b8d5120](../../commit/b8d5120))

## [1.0.3] - 2026-08-05

### ✨ Features

- feat(builder): add shared Fallout banner for migrate command ([dd427d1](../../commit/dd427d1))
- feat(builder): add central package management support ([91a2e13](../../commit/91a2e13))
- feat(builder): add global tool parsing and safer Fallout install flow ([b5b54ea](../../commit/b5b54ea))

### 🐛 Bug Fixes

- fix(builder): rename global tool command to autofallout ([6ad71fb](../../commit/6ad71fb))
- fix(builder): support centrally managed package versions in migrator ([3b1a41d](../../commit/3b1a41d))
- fix(build): switch package release to Azure Pipelines ([489c42d](../../commit/489c42d))

### 🔧 Chores

- chore(gitignore): restore and clarify Fallout build ignores ([e4eaa71](../../commit/e4eaa71))
- chore(build): ignore fallout temp files in build ([7732f21](../../commit/7732f21))

### 📝 Other Changes

- Untrack ignored .fallout files and tidy .gitignore ([6a1d49c](../../commit/6a1d49c))
- Fix the build? ([4aadcf1](../../commit/4aadcf1))
- Merged GitHub and AzureDevOps into one codebase. Made it more symmetrical. And got package management somewhat working. Still needs work on migrate on an existing central package management leaves versions in the project file. ([afdae8a](../../commit/afdae8a))
- Fallout migrate command ([09b2994](../../commit/09b2994))

## [1.0.2] - 2026-08-02

### 📝 Other Changes

- Point Builder package metadata at the Fallout repo ([80a11cf](../../commit/80a11cf))

## [1.0.1] - 2026-08-02

### 📝 Other Changes

- Fix GitVersion injection under Fallout 11 / System.Text.Json ([4a3d42f](../../commit/4a3d42f))

## [1.0.0] - 2026-08-02

### 📝 Other Changes

- Need it commited so I can create a tag... ([8ed3101](../../commit/8ed3101))

