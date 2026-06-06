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
| **Testing extras** | NSubstitute, Bogus, `Microsoft.Testing.Platform.MSBuild` wiring and coverage defaults |
| **Version detection** | Reads `version` from `package.json` and applies it to `Version` and `PackageVersion` automatically; falls back to `0.0.1` |
| **CPM** | `ManagePackageVersionsCentrally=true` — versions live in your `Directory.Packages.props` |

---

## Quick start

### 1. Add the SDK to `global.json`

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  },
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
<Project>
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
| `global.json` | `msbuild-sdks` entry + `Microsoft.Testing.Platform` test runner |
| `.gitignore` | ASP.NET Core + VS + Rider + Node combined gitignore |
| `.gitattributes` | Line-ending normalisation for .cs, .json, .yml, etc. |
| `.config/dotnet-tools.json` | CSharpier tool manifest |

---

## Configuration reference

Set any of these properties **before** the `<Import>` in your `Directory.Build.props`:

### Version detection

| Property | Default | Description |
|---|---|---|
| `UsePackageJsonVersion` | `true` | Set to `false` to disable reading `Version`/`PackageVersion` from `package.json`. |
| `RootPackageJson` | *(auto-discovered)* | Explicit path to a `package.json`. Relative paths are resolved from the project directory. |

When `UsePackageJsonVersion=true` (the default) the SDK:

1. **Explicit path** — if `RootPackageJson` is set, reads that file directly.
2. **Auto-discovery** — otherwise, walks up from the project directory looking for a `.git` marker to locate the repo root, then reads `package.json` from there.

The extracted `version` field is applied to both `Version` and `PackageVersion`. A build error is raised if the file can't be found or contains no `version` field.

> **Important — set before the import:** Both `UsePackageJsonVersion` and `RootPackageJson` must be set **before** the `<Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.props" />` line in your `Directory.Build.props`. The version logic runs during that import and cannot see properties set afterwards (e.g. in individual `.csproj` files).
>
> ```xml
> <Project>
>   <PropertyGroup>
>     <NamespacePrefix>Acme</NamespacePrefix>
>     <!-- Set here, before the import -->
>     <RootPackageJson>$(MSBuildThisFileDirectory)package.json</RootPackageJson>
>   </PropertyGroup>
>
>   <Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.props" />
> </Project>
> ```

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

### Compiler-visible SDK properties

The SDK now exports its properties via `CompilerVisibleProperty`, so analyzers and source generators can read them through `build_property.<PropertyName>`.

| Property | Description |
|---|---|
| `UsePackageJsonVersion` | Whether version detection from `package.json` is active. |
| `RootPackageJson` | Resolved path to the `package.json` used for version detection. |
| `RepoRoot` | Repo root directory found via `.git` auto-discovery. |
| `Version` | Package/assembly version, sourced from `package.json` when detection is enabled. |
| `PackageVersion` | NuGet package version, sourced from `package.json` when detection is enabled. |
| `NamespacePrefix` | Required namespace prefix used to derive `RootNamespace`. |
| `DisableNamespacePrefixCheck` | Disables the build error for missing `NamespacePrefix`. |
| `ProjectSdkTestFramework` | Selected test framework (`TUnit` by default, `XUnit` opt-in). |
| `SourceLinkPackageName` | SourceLink package ID added by the SDK. |
| `ExcludePurviewTelemetry` | Opt-out for `Purview.Telemetry.SourceGenerator`. |
| `ExcludeMSTelemetryExtension` | Opt-out for `Microsoft.Extensions.Telemetry.Abstractions`. |
| `DisableGenerateAssemblyInfoClass` | Disables generated `AssemblyInfo` helper source. |
| `DisableAutoInternalsVisibleTo` | Disables automatic `InternalsVisibleTo` generation. |
| `AutoIncludeUsings` | Controls SDK-added global usings. |
| `IsCSharpProject` | True when the project is a `.csproj`. |
| `IsTestProject` | True when project name ends with a supported test suffix. |
| `IsSharedTestingProject` | True for known shared testing helper project names. |
| `TestingType` | Detected test category suffix from project name. |
| `TargetProjectName` | Inferred target project name for test projects. |
| `IsContainerProject` | True when Dockerfile markers indicate container defaults. |
| `IsSdkProject` | True when an SDK value is detected from project/import declaration. |
| `SdkProjectName` | Detected SDK name (e.g. `Microsoft.NET.Sdk.Web`). |
| `IsWebProject` | Marker used in SDK web-project behavior. |
| `IsWebSdkProject` | True when `SdkProjectName` is `Microsoft.NET.Sdk.Web`. |
| `IsWorkerSdkProject` | True when `SdkProjectName` is `Microsoft.NET.Sdk.Worker`. |
| `IsAspireHostProject` | True when SDK starts with `Aspire.Sdk.Host`. |
| `EditorConfigFilePath` | Path to the SDK-provided `.editorconfig` that is injected into `@(EditorConfigFiles)`. |
| `CurrentYear` | Current year used in generated assembly metadata. |
| `AutoGeneratedAssemblyInfoFile` | Relative path to generated AssemblyInfo source file. |

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
dotnet build src/DotNetProjectSdk.slnx -c Release
dotnet test src/DotNetProjectSdk.slnx -c Release
dotnet pack src/src/DotNetProjectSdk/DotNetProjectSdk.csproj -o ./artifacts
```

## License

MIT
