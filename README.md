# Purview.DotNetProjectSdk

A reusable MSBuild SDK NuGet package that delivers standardised .NET project defaults, code-style enforcement, test-framework wiring, and Central Package Management integration. Install it once per repo — every project beneath the repo root inherits everything automatically.

> [!NOTE]
> This SDK package imposes convention over configuration, enforcing certain styles and automations based on project file names, etc.

## What's included

| Feature | Detail |
| -- | -- |
| **Project type detection** | `IsCSharpProject`, `IsTestProject`, `IsSharedTestingProject`, `IsContainerProject`, `IsWebSdkProject`, `IsAspireHostProject`, … |
| **C# defaults** | `net10.0` TFM (overridable), `LangVersion=preview`, `Nullable=enable`, `ImplicitUsings=enable`, deterministic builds |
| **Code style** | `.editorconfig` baked into the package, applied via `EditorConfigFilePath`, and auto-bootstrapped to repo root if missing; `EnforceCodeStyleInBuild=true`, `EnableNETAnalyzers=true`, `AnalysisLevel=latest`, `AnalysisMode=All` |
| **NuGet packaging** | `PublishRepositoryUrl=true`, `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`, `EmbedUntrackedSources=true` for packable projects |
| **Repo bootstrap** | Missing repo-root `.editorconfig` and `global.json` are auto-copied/created by default (disable via `DisableAutoCopySdkFiles=true`) |
| **CI detection** | `ContinuousIntegrationBuild` set automatically when `CI`, `GITHUB_ACTIONS`, or `TF_BUILD` env vars are present |
| **SourceLink** | `Microsoft.SourceLink.GitHub` added to all packable projects (configurable via `SourceLinkPackageName`) |
| **Purview Telemetry** | `Purview.Telemetry.SourceGenerator` + `Microsoft.Extensions.Telemetry.Abstractions` added by default (opt-out) |
| **Assembly info** | Auto-generated `static class AssemblyInfo` with `RootNamespace`, `Version`, `Company`, etc., plus an embedded `Microsoft.CodeAnalysis.EmbeddedAttribute` (can be excluded via `PURVIEW_SDK_EXCLUDE_EMBEDDED`). |
| **InternalsVisibleTo** | Generated for all `TestType` variants and shared testing projects, using the resolved `$(AssemblyName)` so explicit, generated, and default naming are all handled |
| **Namespace management** | `NamespacePrefix.ProjectName` pattern with suffix stripping (`.Core`, `.Shared`, `.EF`, …) |
| **Testing framework** | `TestingFramework`: **TUnit** (default), `Xunit`, or `None` |
| **Mocking provider** | `SubstituteFramework`: **TUnitMocks** (default), `NSubstitute`, or `None` |
| **Test data provider** | `TestDataFramework`: **Bogus** (default) or `None` |
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

## Project naming guide

The SDK applies several conventions automatically based on the `.csproj` filename and `NamespacePrefix`.

### Defaults (no extra configuration)

By default (`EnableAssemblyNameGeneration=false`), `AssemblyName` follows standard .NET behaviour — it's just the `.csproj` filename. `RootNamespace` is always derived from `$(NamespacePrefix).$(ProjectName)`:

| `.csproj` filename | `AssemblyName` | `RootNamespace` | Detected as |
| -- | -- | -- | -- |
| `Api.csproj` | `Api` | `Acme.Api` | Source project |
| `Api.UnitTests.csproj` | `Api.UnitTests` | `Acme.Api` | `IsTestProject=true`, `TestingType=Unit` |
| `Api.IntegrationTests.csproj` | `Api.IntegrationTests` | `Acme.Api` | `IsTestProject=true`, `TestingType=Integration` |
| `SharedTestingFramework.csproj` | `SharedTestingFramework` | `Acme.SharedTestingFramework` | `IsSharedTestingProject=true` |

> **Note:** `InternalsVisibleTo` follows `$(AssemblyName)` — so for `Api.csproj` the SDK generates `Api.UnitTests`, `Api.IntegrationTests`, etc.

### With `EnableAssemblyNameGeneration=true`

When enabled, the SDK derives `AssemblyName` and `PackageId` from `$(PurviewLogicalProjectName)` — the full `$(NamespacePrefix).$(ProjectName)` with deduplication:

| `.csproj` filename | `AssemblyName` | `RootNamespace` |
| -- | -- | -- |
| `Api.csproj` | `Acme.Api` | `Acme.Api` |
| `Api.UnitTests.csproj` | `Acme.Api.UnitTests` | `Acme.Api` |
| `Core.Infrastructure.csproj` | `Acme.Core.Infrastructure` | `Acme.Core.Infrastructure` |

Use short `.csproj` names in both modes — the SDK handles the prefixing:

```text
✅  Api.csproj                        → short name, SDK resolves the rest
❌  Acme.Api.csproj                   → redundant prefix, avoid
```

A build-time check (`PurviewProjectFileNameMismatch`) enforces that the `.csproj` filename matches its parent directory name, preventing inconsistent naming. Set `DisableProjectFileNamingConventionCheck=true` to opt out.

### Recommended structure: `src/` + `tests/`

For larger repos, separate source and test projects into `src/` and `tests/` folders:

```text
MyRepo/
├── Directory.Build.props          ← NamespacePrefix=Acme
├── Directory.Build.targets
├── Directory.Packages.props
├── global.json
├── src/
│   ├── Api/
│   │   └── Api.csproj
│   ├── Core/
│   │   └── Core.csproj
│   └── SourceGenerator/
│       └── SourceGenerator.csproj
├── tests/
│   ├── Api.UnitTests/
│   │   └── Api.UnitTests.csproj    → IsTestProject=true, TestingType=Unit
│   ├── Api.IntegrationTests/
│   │   └── Api.IntegrationTests.csproj
│   └── SharedTestingFramework/
│       └── SharedTestingFramework.csproj  → IsSharedTestingProject=true
└── package.json
```

### Flat structure: everything together

For smaller repos, source and test projects can live side-by-side:

```text
MyRepo/
├── Directory.Build.props
├── Directory.Build.targets
├── Directory.Packages.props
├── global.json
├── Api/
│   └── Api.csproj
├── Api.UnitTests/
│   └── Api.UnitTests.csproj
├── Core/
│   └── Core.csproj
├── Core.IntegrationTests/
│   └── Core.IntegrationTests.csproj
└── package.json
```

Both layouts work identically — the SDK detects test projects by name suffix, not folder location.

### Quick reference

```sh
# Create a source project
mkdir src/Api && cd src/Api
dotnet new classlib -n Api

# Create its unit tests
mkdir ../../tests/Api.UnitTests && cd ../../tests/Api.UnitTests
dotnet new classlib -n Api.UnitTests   # SDK wires TUnit automatically

# Or flat:
mkdir Api.UnitTests && cd Api.UnitTests
dotnet new classlib -n Api.UnitTests
```

---

## Template files

The `templates/` folder contains ready-to-copy starter files for new repos:

| File | Purpose |
| -- | -- |
| `Directory.Build.props` | Bootstrapper — copy to repo root and set `NamespacePrefix` |
| `Directory.Build.targets` | Bootstrapper — copy to repo root |
| `Directory.Packages.props` | All default package versions with `*` floating to latest |
| `global.json` | `msbuild-sdks` entry + `Microsoft.Testing.Platform` test runner |
| `.gitignore` | ASP.NET Core + VS + Rider + Node combined gitignore |
| `.gitattributes` | Line-ending normalisation for .cs, .json, .yml, etc. |
| `.config/dotnet-tools.json` | CSharpier tool manifest |

The package also ships bundled agent content under `.agents/**`. During build, the SDK copies it into the consuming repository's `.agents/` folder by default so compatible coding agents can discover repository-aware guidance automatically. The SDK also injects a `.gitignore` file into each second-level agent folder with the content `# Ignore all files\n*\n# Don't ignore directories, so Git can traverse them\n!*/\n# Keep this file\n!.gitignore`, so the copied folder is ignored by Git while keeping the folder structure discoverable.

---

## Configuration reference

Set any of these properties **before** the `<Import>` in your `Directory.Build.props`:

### Version detection

| Property | Default | Description |
| -- | -- | -- |
| `UsePackageJsonVersion` | `true` | `true` enables version detection, `false` disables it, and `Strict` requires version detection to succeed (build fails if no version source can be resolved). |
| `RootPackageJson` | *(auto-discovered)* | Explicit path to a `package.json`. Relative paths are resolved from the project directory. |
| `EnableVersionDetectionCache` | `true` | Enables local caching of auto-discovered package.json version results. |
| `VersionDetectionLogEnabled` | `false` | Emits a high-importance message showing the detected package version. Set to `true` to enable logging.

When `UsePackageJsonVersion=true` (the default) or `UsePackageJsonVersion=Strict`, the SDK:

1. **Explicit path** — if `RootPackageJson` is set, reads that file directly.
2. **Auto-discovery** — otherwise, walks up from the project directory looking for a `.git` marker to locate the repo root, then reads `package.json` from there.

The extracted `version` field is applied to both `Version` and `PackageVersion`. A build error is raised if the file can't be found or contains no `version` field. With `UsePackageJsonVersion=Strict`, the build also fails when no package.json source can be discovered (for example, no explicit `RootPackageJson` and no discoverable `.git` marker).

Version detection logging is disabled by default. Set `VersionDetectionLogEnabled` to `true` to emit a high-importance message showing the detected package version.

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
| -- | -- | -- |
| `NamespacePrefix` | *(required)* | Root namespace prefix, e.g. `Acme`. Results in `Acme.MyProject`. |
| `DisableNamespacePrefixCheck` | `false` | Set to `true` to suppress the build error for missing `NamespacePrefix`. |
| `TargetFramework` | `net10.0` | Override the default TFM per-project or globally. Defaults to `netstandard2.0` for projects declaring `IsRoslynComponent=true`. |
| `IsRoslynComponent` | `false` | When explicitly `true`, applies source-generator defaults for a single `netstandard2.0` target, analyzer rules, SourceLink, generated-file output, dependency output, symbol packaging, telemetry exclusion, and package build output. |
| `PackProjectReferencedSourceGenerators` | `true` | Automatically packs analyzer `ProjectReference` outputs and their runtime dependencies under `analyzers/dotnet/cs/`. Set to `false` to opt out; set `Pack="false"` on an individual reference to exclude only that generator. |
| `SourceLinkPackageName` | `Microsoft.SourceLink.GitHub` | SourceLink provider. Set to `Microsoft.SourceLink.AzureDevOps.Git` for ADO repos. |
| `DisableSourceLink` | `false` | Set to `true` to stop the SDK from adding the configured SourceLink package automatically. |
| `EnableAssemblyNameGeneration` | `false` | When `true`, the SDK derives `AssemblyName` (and `PackageId`) from `$(PurviewLogicalProjectName)` — i.e. `$(NamespacePrefix).$(ProjectName)` with deduplication logic. When `false` (default), standard .NET behaviour applies (`$(MSBuildProjectName)`). Explicit `<AssemblyName>` in a `.csproj` always takes precedence. |
| `DisableProjectFileNamingConventionCheck` | `false` | Set to `true` to disable the validation that requires `MyProject\MyProject.csproj` naming alignment. |
| `DisableGenerateAssemblyInfoClass` | `false` | Set to `true` to disable the generated `AssemblyInfo` helper source. |
| `AutoIncludeUsings` | `true` | Controls SDK-added global usings for `NamespacePrefix` and `RootNamespace`. |

### Repo bootstrap

| Property | Default | Description |
| -- | -- | -- |
| `DisableAutoCopySdkFiles` | `false` | Master switch that disables repo-level SDK file bootstrapping. |
| `BootstrapEditorConfigToRepoRoot` | `true` | Copies the SDK `.editorconfig` to the repository root when missing. |
| `RepositoryEditorConfigFilePath` | *(auto-detected)* | Override the destination path for the bootstrapped `.editorconfig`. |
| `BootstrapGlobalJsonToRepoRoot` | `true` | Creates a `global.json` at the repository root when missing. |
| `RepositoryGlobalJsonFilePath` | *(auto-detected)* | Override the destination path for the bootstrapped `global.json`. |
| `PurviewDotNetProjectSdkVersionForGlobalJson` | *(auto-detected or `1.0.0` fallback)* | Version written to the `msbuild-sdks.Purview.DotNetProjectSdk` entry in a bootstrapped `global.json`. |

### Agent folder

| Property | Default | Description |
| -- | -- | -- |
| `PurviewAutoSdkPack` | `true` | When `true`, automatically packs the `Sdk/` folder contents into the NuGet package with the correct root-level paths. Disable this for MSBuild SDK projects. |
| `EnableAgentFolderInPackage` | `true` | Copies the bundled `.agents/**` folder from the SDK NuGet package into the consuming repository's `.agents/` folder (or `$(AgentPackDestinationFolder)/`) before build. |
| `AgentPackDestinationFolder` | `.agents` | Repo-relative destination folder that receives the copied agent folder contents when `EnableAgentFolderInPackage` is `true`. |

To disable bundled agent folder copying in a consuming repo, set the opt-out property before importing the SDK:

```xml
<PropertyGroup>
  <EnableAgentFolderInPackage>false</EnableAgentFolderInPackage>
</PropertyGroup>
```

When a project is packable, the SDK treats any content under `Sdk/` as a pack target with these rules:

| Source path | Package path |
| -- | -- |
| `Sdk/.agents/**` | `.agents/**` |
| `Sdk/.github/**` | `.github/**` |
| `Sdk/build/**` | `build/**` |
| `Sdk/buildTransitive/**` | `buildTransitive/**` |
| `Sdk/buildMultiTargeting/**` | `buildMultiTargeting/**` |
| `Sdk/*.md`, `Sdk/*.png`, `Sdk/*.jpg`, etc. | package root |
| everything else under `Sdk/` | `Sdk/` |

The SDK automatically adds a `.gitignore` file into each second-level folder under `Sdk/.agents` with the content `# Ignore all files\n*\n# Don't ignore directories, so Git can traverse them\n!*/\n# Keep this file\n!.gitignore`. This ensures the copied folder structure remains discoverable in consuming repositories while the content itself is ignored by Git.

### Telemetry

| Property | Default | Description |
| -- | -- | -- |
| `ExcludePurviewTelemetry` | `false` | Set to `true` to exclude `Purview.Telemetry.SourceGenerator` from all projects. |
| `ExcludeMSTelemetryExtension` | `false` | Set to `true` to exclude `Microsoft.Extensions.Telemetry.Abstractions`. Note, when `ExcludePurviewTelemetry` is `false` this is excluded anyway. |

### Testing

| Property | Default | Description |
| -- | -- | -- |
| `TestingFramework` | `TUnit` | Testing framework. Supported values: `TUnit`, `Xunit`, `None`. |
| `SubstituteFramework` | `TUnitMocks` | Mocking provider. Supported values: `TUnitMocks`, `NSubstitute`, `None`. |
| `TestDataFramework` | `Bogus` | Test data provider. Supported values: `Bogus`, `None`. |
| `ProjectSdkTestFramework` | `TUnit` | Legacy alias for `TestingFramework`. |
| `DisableAutoInternalsVisibleTo` | `false` | Set to `true` to disable automatic `InternalsVisibleTo` generation for test types and shared testing projects. |

### Compiler-visible SDK properties

The SDK now exports its properties via `CompilerVisibleProperty`, so analyzers and source generators can read them through `build_property.<PropertyName>`.

| Property | Description |
| -- | -- |
| `UsePackageJsonVersion` | Whether version detection from `package.json` is active. |
| `RootPackageJson` | Resolved path to the `package.json` used for version detection. |
| `RepoRoot` | Repo root directory found via `.git` auto-discovery. |
| `Version` | Package/assembly version, sourced from `package.json` when detection is enabled. |
| `PackageVersion` | NuGet package version, sourced from `package.json` when detection is enabled. |
| `NamespacePrefix` | Required namespace prefix used to derive `RootNamespace`. |
| `DisableNamespacePrefixCheck` | Disables the build error for missing `NamespacePrefix`. |
| `ProjectSdkTestFramework` | Legacy alias for `TestingFramework`. |
| `TestingFramework` | Selected testing framework (`TUnit`, `Xunit`, or `None`). |
| `SubstituteFramework` | Selected mocking provider (`TUnitMocks`, `NSubstitute`, or `None`). |
| `TestDataFramework` | Selected test data provider (`Bogus` or `None`). |
| `SourceLinkPackageName` | SourceLink package ID added by the SDK. |
| `ExcludePurviewTelemetry` | Opt-out for `Purview.Telemetry.SourceGenerator`. |
| `ExcludeMSTelemetryExtension` | Opt-out for `Microsoft.Extensions.Telemetry.Abstractions`. |
| `DisableGenerateAssemblyInfoClass` | Disables generated `AssemblyInfo` helper source. |
| `EnableAssemblyNameGeneration` | When `true`, the SDK derives `AssemblyName` from the logical project name. |
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
| `IsWebProject` | Marker used in SDK web-project behaviour. |
| `IsWebSdkProject` | True when `SdkProjectName` is `Microsoft.NET.Sdk.Web`. |
| `IsWorkerSdkProject` | True when `SdkProjectName` is `Microsoft.NET.Sdk.Worker`. |
| `IsAspireHostProject` | True when SDK starts with `Aspire.Sdk.Host`. |
| `EditorConfigFilePath` | Path to the SDK-provided `.editorconfig` that is injected into `@(EditorConfigFiles)`. |
| `RepositoryEditorConfigFilePath` | Destination path for bootstrapping a physical repo-level `.editorconfig` (defaults to git repo root; falls back to `Directory.Build.props` directory). |
| `BootstrapEditorConfigToRepoRoot` | When `true` (default), copies the SDK `.editorconfig` to `RepositoryEditorConfigFilePath` if missing. |
| `RepositoryGlobalJsonFilePath` | Destination path for bootstrapping a physical repo-level `global.json` (defaults to git repo root; falls back to `Directory.Build.props` directory). |
| `BootstrapGlobalJsonToRepoRoot` | When `true` (default), creates `global.json` at `RepositoryGlobalJsonFilePath` if missing. |
| `PurviewDotNetProjectSdkVersionForGlobalJson` | Version used for `msbuild-sdks.Purview.DotNetProjectSdk` when bootstrapping `global.json` (auto-detected from SDK package path, fallback `1.0.0`). |
| `DisableAutoCopySdkFiles` | When `true`, disables SDK auto-copy/bootstrap for repo files (`.editorconfig`, `global.json`). |
| `PurviewAutoSdkPack` | When `true`, automatically packs the `Sdk/` folder contents into the NuGet package with the correct root-level paths. |
| `CurrentYear` | Current year used in generated assembly metadata. |
| `AutoGeneratedAssemblyInfoFile` | Relative path to generated AssemblyInfo source file. |

#### Example: switch a repo to Xunit + NSubstitute and disable Bogus

```xml
<Project>
  <PropertyGroup>
    <NamespacePrefix>Acme</NamespacePrefix>
    <TestingFramework>Xunit</TestingFramework>
    <SubstituteFramework>NSubstitute</SubstituteFramework>
    <TestDataFramework>None</TestDataFramework>
  </PropertyGroup>

  <Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.props" />
</Project>
```

---

## Test project naming conventions

Test projects are automatically detected by their suffix. Supported patterns:

```text
MyProject.UnitTests       → IsTestProject=true, TestingType=Unit
MyProject.IntegrationTests→ IsTestProject=true, TestingType=Integration
MyProject.E2ETests        → IsTestProject=true, TestingType=E2E
```

Any suffix from the full list is recognised: `Unit`, `Integration`, `E2E`, `EndToEnd`, `Acceptance`, `Functional`, `Performance`, `Load`, `Smoke`, `Stress`, `Regression`, `Security`, `Chaos`, `Scenario`, `System`, `Threat`, `BlackBox`, `WhiteBox`, `Accessibility`, `Interactive`, `Environment`.

### Shared testing projects

Projects named `SharedTestingFramework`, `SharedTestingInfrastructure`, `SharedTestingInfra`, `SharedTestingUtilities`, `SharedTestingLibrary`, `SharedTestingLib`, or `SharedTestingHelpers` are treated as shared testing helpers — they get test package references but not the test runner or coverage settings.

---

## InternalsVisibleTo

The SDK automatically generates `[assembly: InternalsVisibleTo("…")]` attributes for every non-test C# project. The friend assembly name is derived from the source project's resolved `$(AssemblyName)`, so all naming modes are handled correctly:

- **Explicit `<AssemblyName>`** — if a project sets `<AssemblyName>Custom.Assembly</AssemblyName>`, the generated attributes use `Custom.Assembly.UnitTests`, `Custom.Assembly.IntegrationTests`, etc.
- **`EnableAssemblyNameGeneration=true`** — the SDK-derived fully-qualified name is used (e.g. `Acme.MyProject.UnitTests`).
- **Default** — standard .NET behaviour: `$(MSBuildProjectName)` (e.g. `MyProject.UnitTests`).

Two categories of friend assemblies are generated:

1. **TestType variants** — one `InternalsVisibleTo` per defined `TestType` (`Unit`, `Integration`, `Architecture`, `Contract`, `Functional`, …), formatted as `$(AssemblyName).{TestType}Tests`.
2. **SharedTesting projects** — one per known shared testing project name (`SharedTestingFramework`, `SharedTestingInfrastructure`, etc.). When `EnableAssemblyNameGeneration=true` and a `NamespacePrefix` is set, these are prefixed (e.g. `Acme.SharedTestingFramework`); otherwise the raw name is used.

### Disabling automatic InternalsVisibleTo

To disable automatic InternalsVisibleTo generation, set `DisableAutoInternalsVisibleTo=true` in your project or `Directory.Build.props`:

```xml
<PropertyGroup>
  <DisableAutoInternalsVisibleTo>true</DisableAutoInternalsVisibleTo>
</PropertyGroup>
```

---

## EmbeddedAttribute generation

When `GenerateAssemblyInfoClassTarget` writes the SDK-generated `AssemblyInfo` source, it also emits:

```csharp
namespace Microsoft.CodeAnalysis
{
    sealed partial class EmbeddedAttribute : System.Attribute { }
}
```

This block is guarded by:

```csharp
#if !PURVIEW_SDK_EXCLUDE_EMBEDDED
```

`AssemblyInfo` is emitted with `[Microsoft.CodeAnalysis.Embedded]`, so the project must have a matching `Microsoft.CodeAnalysis.EmbeddedAttribute` type available at compile time. The SDK emits that attribute to satisfy the reference and to keep generated metadata/source-generator-facing symbols marked as embedded.

Define `PURVIEW_SDK_EXCLUDE_EMBEDDED` only when your build already provides `Microsoft.CodeAnalysis.EmbeddedAttribute` from another source; otherwise compilation will fail because the attribute used by generated `AssemblyInfo` cannot be resolved.

---

## Namespace stripping

Certain suffixes are automatically stripped from `RootNamespace` to avoid awkward namespace names like `Acme.MyProject.Core.Something`:

Stripped suffixes: `Core`, `EF`, `Shared`, `ClientShared`, `ServiceDefaults`, and all shared testing project names.

### Extensions namespace rule (`PDS0002`)

When a file is placed under a project-root `Extensions/` folder, the analyzer intentionally treats
that folder as a namespace reset point.

- Scope: only files where the first project-relative segment is exactly `Extensions`
- Expected namespace: derived from subfolders under `Extensions/` (file name is ignored)
- `RootNamespace` is deliberately ignored for these files

Examples:

| Project-relative file path | Expected namespace |
| -- | -- |
| `Extensions/System/StringExtensions.cs` | `System` |
| `Extensions/Microsoft/Extensions/Configuration/ConfigurationExtensions.cs` | `Microsoft.Extensions.Configuration` |
| `Extensions/TopLevel.cs` | *(global namespace)* |

To avoid conflicting guidance, `IDE0130` is suppressed for files in this root `Extensions/` scope.
Outside this scope, normal `IDE0130` behaviour remains unchanged.

---

## Assembly name generation

By default (`EnableAssemblyNameGeneration=false`), the SDK follows standard .NET behaviour: `AssemblyName` is `$(MSBuildProjectName)`. Set `EnableAssemblyNameGeneration=true` (in `Directory.Build.props` or individual `.csproj`) to have the SDK derive `AssemblyName` from `$(PurviewLogicalProjectName)`:

```xml
<PropertyGroup>
  <NamespacePrefix>Acme</NamespacePrefix>
  <EnableAssemblyNameGeneration>true</EnableAssemblyNameGeneration>
</PropertyGroup>
```

With this enabled:

| Project name | `NamespacePrefix` | Resolved `AssemblyName` |
| -- | -- | -- |
| `Api` | `Acme` | `Acme.Api` |
| `Acme.Api` | `Acme` | `Acme.Api` (no double-prefix) |
| `Core.Infrastructure` | `Acme` | `Acme.Core.Infrastructure` |
| `Acme` | `Acme` | `Acme` |

`PackageId` follows `AssemblyName` (with namespace-remove patterns applied). An explicit `<AssemblyName>` in a `.csproj` always takes precedence over generation.

> **Note:** `RootNamespace` is derived from `$(PurviewLogicalProjectName)` regardless of this setting — it always reflects `$(NamespacePrefix).$(ProjectName)` with suffix stripping applied. `EnableAssemblyNameGeneration` only controls whether `AssemblyName`/`PackageId` follow suit.

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
