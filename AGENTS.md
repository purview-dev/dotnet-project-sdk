# AGENTS.md

Guidance for AI coding agents working in this repository.

## Customization source of truth

Use `.agents/` as the default location for **generic agentic information** shared across agent runtimes.

- `.agents/skills/` — canonical, cross-agent skills
- `.agents/agents/` — canonical, cross-agent agent workflow specs
- `.agents/prompts/` — canonical, cross-agent prompt specs

Use `.github/` customization files only as VS Code/Copilot wrappers or registration points when required by tooling.

## Purpose

This repo builds and tests `Purview.DotNetProjectSdk`, a reusable MSBuild SDK package plus analyzer and tests.

For full product behavior and configuration, read [`README.md`](./README.md). Keep edits minimal, targeted, and convention-driven.

## Repository map

- `src/src/DotNetProjectSdk/` — packable MSBuild SDK package (`Purview.DotNetProjectSdk`)
- `src/src/DotNetProjectSdk.Analyzers/` — Roslyn analyzer/source-generator assembly
- `src/tests/DotNetProjectSdk.Analyzers.UnitTests/` — analyzer-focused unit tests
- `src/tests/DotNetProjectSdk.IntegrationTests/` — integration harness validating SDK behavior
- `src/DotNetProjectSdk.slnx` — solution entry point

## Canonical commands

Prefer `just` tasks:

- `just restore`
- `just build`
- `just test`
- `just lint-check`
- `just lint-fix`
- `just pack`

`dotnet` fallback uses `src/DotNetProjectSdk.slnx` and `Release`.

## Testing rules (important)

This repo uses **Microsoft.Testing.Platform** (`global.json`) and **TUnit** conventions.

- Prefer `just test` first.
- If filtering tests, use `--treenode-filter` (not `--filter`).
- When passing test-runner options, keep the `--` separator with `dotnet test`.

For filtering syntax and troubleshooting, see:

- [`tunit-test-runner` skill](./.agents/skills/tunit-test-runner/SKILL.md)
- [`dotnet-tunit` skill](./.agents/skills/dotnet-tunit/SKILL.md)
- [`tunit-filtering` skill](./.agents/skills/tunit-filtering/SKILL.md)

### Integration harness for complex validation

Use `src/tests/DotNetProjectSdk.IntegrationTests/Harness/ProjectHarness.cs` when validating behavior that depends on MSBuild evaluation, import order, or generated project state.

- Create throwaway projects with `ProjectHarness.For(...).BuildAsync()` (or `CreateAsync`/`CreateWithContentAsync`).
- Prefer harness evaluation helpers over brittle log parsing:
  - `GetPropertyAsync` / `GetPropertiesAsync`
  - `GetItemIdentitiesAsync` / `GetProjectReferencesAsync`
  - `GetPreprocessProjectAsync` for evaluated project inspection
- Use `BuildAsync(restore: true)` when package restore/build behavior is part of the scenario.
- Keep scenarios minimal and deterministic; isolate one behavior per test.
- For import-time behavior, set required pre-import properties in harness setup (before `Sdk.props` import).

Supporting files:

- `src/tests/DotNetProjectSdk.IntegrationTests/Harness/ProjectHarness.Builder.cs`
- `src/tests/DotNetProjectSdk.IntegrationTests/TestHelpers.cs`

## Conventions to preserve

- Keep project naming aligned with repo conventions in [`README.md`](./README.md#project-naming-guide).
- Respect Central Package Management in `Directory.Packages.props`.
- Avoid unrelated refactors or formatting-only churn unless requested.
- Follow existing style and keep changes small and testable.

## Release and commit workflow

- Versioning is driven by `package.json` + Changesets.
- Use existing Changesets and commit conventions:
  - [`changesets-prerelease` skill](./.agents/skills/changesets-prerelease/SKILL.md)
  - [`git-conventional-commits` skill](./.agents/skills/git-conventional-commits/SKILL.md)
  - [`lefthook-integration` skill](./.agents/skills/lefthook-integration/SKILL.md)
- Generic release agent workflow source: `./.agents/agents/release-prep.md`

## Practical guardrails for agents

- Validate changes with targeted tests first, then broader suite as needed.
- When touching test behavior, verify with MTP/TUnit-compatible invocation.
- Prefer linking users to existing docs over duplicating long explanations in chat.
- Keep generic guidance in `.agents/`; keep `.github/` copies thin and referential.
