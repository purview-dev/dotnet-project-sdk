# Purview.DotNetProjectSdk

A reusable MSBuild SDK NuGet package that delivers standardised .NET project defaults, code-style enforcement, test-framework wiring, and Central Package Management integration. Install it once per repo — every project beneath the repo root inherits everything automatically.

## What's included

| Feature | Detail |
|---|---|
| **Project type detection** | `IsCSharpProject`, `IsTestProject`, `IsSharedTestingProject`, `IsContainerProject`, `IsWebSdkProject`, `IsAspireHostProject`, … |
| **C# defaults** | `net10.0` TFM (overridable), `LangVersion=preview`, `Nullable=enable`, `ImplicitUsings=enable`, deterministic builds |
| **Code style** | `.editorconfig` baked into the package and applied via `EditorConfigFilePath`; `EnforceCodeStyleInBuild=true` |
| **CI detection** | `ContinuousIntegrationBuild` set automatically when `CI`, `GITHUB_ACTIONS`, or `TF_BUILD` env vars are present |
| **SourceLink** | `Microsoft.SourceLink.GitHub` added to all packable projects (configurable via `SourceLinkPackageName`) |
| **Purview Telemetry** | `Purview.Telemetry.SourceGenerator` + `Microsoft.Extensions.Telemetry.Abstractions` added by default (opt-out) |
| **Assembly info** | Auto-generated `static class AssemblyInfo` with `RootNamespace`, `Version`, `Company`, etc. |
| **InternalsVisibleTo** | Generated for all defined `TestType` variants automatically |
| **Namespace management** | `NamespacePrefix.ProjectName` pattern with suffix stripping (`.Core`, `.Shared`, `.EF`, …) |
| **Test framework** | **TUnit** by default; switch to **XUnit v3** with one property |
| **Testing extras** | NSubstitute, Bogus, `Microsoft.Testing.Platform.MSBuild`, Aspire.Hosting — all wired up |
| **CPM** | `ManagePackageVersionsCentrally=true` — versions live in your `Directory.Packages.props` |
| **Container projects** | AOT, Linux Docker defaults when a `Dockerfile` is present |

---

## Quick start

### 1. Add the SDK to `global.json`

```json
{
  "sdk": { "version": "10.0.202", "rollForward": "latestMinor" },
  "msbuild-sdks": {
    "Purview.DotNetProjectSdk": "1.0.0"
  }
}
```

### 2. Create `Directory.Build.props` at repo root

```xml
<Project>
  <PropertyGroup>
    <!-- Required: sets the root namespace prefix for all projects -->
    <NamespacePrefix>YourCompany</NamespacePrefix>
  </PropertyGroup>

  <Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.props" />
</Project>
```

### 3. Create `Directory.Build.targets` at repo root

```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.targets" />
</Project>
```

### 4. Copy `Directory.Packages.props` to repo root

Copy `templates/Directory.Packages.props` from this package to your repo root. All package versions default to `*` (latest at restore). Pin any package by replacing `*` with a specific version.

> **Note:** `ManagePackageVersionsCentrally=true` is set by the SDK. You **must** have a `Directory.Packages.props` at your repo root for CPM to work, even if it only contains the packages the SDK adds automatically.

---

## Template files

The `templates/` folder contains ready-to-copy starter files for new repos:

| File | Purpose |
|---|---|
| `Directory.Build.props` | Bootstrapper — copy to repo root and set `NamespacePrefix` |
| `Directory.Build.targets` | Bootstrapper — copy to repo root |
| `Directory.Packages.props` | All default package versions with `*` floating to latest |
| `global.json` | SDK pin + `msbuild-sdks` entry |
| `.editorconfig` | Full C# code-style rules (IDE-discoverable copy) |
| `.gitignore` | ASP.NET Core + VS + Rider + Node combined gitignore |
| `.gitattributes` | Line-ending normalisation for .cs, .json, .yml, etc. |
| `.config/dotnet-tools.json` | CSharpier + dotnet-inspect pre-configured |

---

## Configuration reference

Set any of these properties **before** the `<Import>` in your `Directory.Build.props`:

### General

| Property | Default | Description |
|---|---|---|
| `NamespacePrefix` | *(required)* | Root namespace prefix, e.g. `Acme`. Results in `Acme.MyProject`. |
| `DisableNamespacePrefixCheck` | `false` | Set to `true` to suppress the build error for missing `NamespacePrefix`. |
| `TargetFramework` | `net10.0` | Override the default TFM per-project or globally. |
| `SourceLinkPackageName` | `Microsoft.SourceLink.GitHub` | SourceLink provider. Set to `Microsoft.SourceLink.AzureDevOps.Git` for ADO repos. |

### Telemetry

| Property | Default | Description |
|---|---|---|
| `ExcludePurviewTelemetry` | `false` | Set to `true` to remove `Purview.Telemetry.SourceGenerator` from all projects. |
| `ExcludeMSTelemetryExtension` | `false` | Set to `true` to remove `Microsoft.Extensions.Telemetry.Abstractions`. |

### Testing

| Property | Default | Description |
|---|---|---|
| `ProjectSdkTestFramework` | `TUnit` | Testing framework. Set to `XUnit` to switch to xunit v3. |
| `DisableAutoInternalsVisibleTo` | `false` | Set to `true` to disable automatic `InternalsVisibleTo` generation for test types and shared testing projects. |

#### Example: switch a repo to XUnit

```xml
<Project>
  <PropertyGroup>
    <NamespacePrefix>Acme</NamespacePrefix>
    <ProjectSdkTestFramework>XUnit</ProjectSdkTestFramework>
  </PropertyGroup>

  <Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.props" />
</Project>
```

---

## Test project naming conventions

Test projects are automatically detected by their suffix. Supported patterns:

```
MyProject.UnitTests       → IsTestProject=true, TestingType=Unit
MyProject.IntegrationTests→ IsTestProject=true, TestingType=Integration
MyProject.E2ETests        → IsTestProject=true, TestingType=E2E
```

Any suffix from the full list is recognised: `Unit`, `Integration`, `E2E`, `EndToEnd`, `Acceptance`, `Functional`, `Performance`, `Load`, `Smoke`, `Stress`, `Regression`, `Security`, `Chaos`, `Scenario`, `System`, `Threat`, `BlackBox`, `WhiteBox`, `Accessibility`, `Interactive`, `Environment`.

### Shared testing projects

Projects named `SharedTestingFramework`, `SharedTestingInfrastructure`, `SharedTestingInfra`, `SharedTestingUtilities`, `SharedTestingLibrary`, `SharedTestingLib`, or `SharedTestingHelpers` are treated as shared testing helpers — they get test package references but not the test runner or coverage settings.

---

## InternalsVisibleTo

The SDK automatically generates `[assembly: InternalsVisibleTo("MyProject.UnitTests")]` (and all other TestType variants) for every non-test project. This allows test projects to access internal members. No manual attributes required.

Additionally, all SharedTesting projects (like `SharedTestingFramework`, `SharedTestingInfrastructure`, etc.) are also granted access to internals, so shared testing infrastructure has full visibility into the projects being tested.

### Disabling automatic InternalsVisibleTo

To disable automatic InternalsVisibleTo generation, set `DisableAutoInternalsVisibleTo=true` in your project or `Directory.Build.props`:

```xml
<PropertyGroup>
  <DisableAutoInternalsVisibleTo>true</DisableAutoInternalsVisibleTo>
</PropertyGroup>
```

---

## Namespace stripping

Certain suffixes are automatically stripped from `RootNamespace` to avoid awkward namespace names like `Acme.MyProject.Core.Something`:

Stripped suffixes: `Core`, `EF`, `Shared`, `ClientShared`, `ServiceDefaults`, and all shared testing project names.

---

## Central Package Management

The SDK sets `ManagePackageVersionsCentrally=true`. The `templates/Directory.Packages.props` file contains `PackageVersion` entries for all packages the SDK auto-adds — all set to `Version="*"` (floating to latest).

To pin a package:

```xml
<PackageVersion Include="TUnit" Version="1.45.29" />
```

To add project-specific packages, just append `PackageVersion` entries to your `Directory.Packages.props`.

---

## Building the SDK

```sh
dotnet build src/Purview.DotNetProjectSdk/Purview.DotNetProjectSdk.csproj
dotnet pack src/Purview.DotNetProjectSdk/Purview.DotNetProjectSdk.csproj -o ./artifacts
```

## License

MIT
