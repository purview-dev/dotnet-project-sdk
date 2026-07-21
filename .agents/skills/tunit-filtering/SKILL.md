---
name: tunit-filtering
description: "Use when running, filtering, or troubleshooting TUnit tests in this repository with Microsoft.Testing.Platform; includes --treenode-filter patterns, -- separator usage, and harness-focused validation flows."
---

# TUnit Filtering (Repo-specific)

Use this skill for fast and correct test execution in this repo.

This summary is aligned with TUnit filtering docs and repo conventions.

## Defaults

- Prefer `just test` for full-suite runs.
- Test runner is Microsoft.Testing.Platform (MTP), not VSTest.
- Use `--treenode-filter` for narrowing tests (do not use `--filter`).

## `--treenode-filter`

Use `--treenode-filter` to select tests by the tree path:

`/<Assembly>/<Namespace>/<Class name>/<Test name>`

TUnit supports filtering by:

- Assembly
- Namespace
- Class name
- Test name

## Tree segments

Each `/.../` segment matches one level in the test tree:

- 1st segment: Assembly
- 2nd segment: Namespace
- 3rd segment: Class
- 4th segment: Test name

Examples:

- `/*/*/LoginTests/*` → all tests in class `LoginTests`
- `/*/*/*/AcceptCookiesTest` → a single test by name
- `/*/MyProject.Tests.Integration/*/*` → tests in a namespace

## Operators and filter options

### `*` wildcard

Matches any value in a segment, or part of a value.

Examples:

- `LoginTests*`
- `/*/*/MyProject.Tests.Api*/*`

### `=` equality

Matches an exact property value.

Example:

- `[Category=Smoke]`

### `!=` not equal

Excludes a property value.

Example:

- `[Category!=Slow]`

### `&` AND

Combines multiple conditions in the same segment or property group.

Examples:

- `/*/*/(ClassA*)&(Smoke)/*`
- `/*/*/*/*[(Category=Smoke)&(Priority=High)]`

### `|` OR

Matches either condition, inside a single parenthesized group.

Examples:

- `/*/*/(LoginTests)|(SignupTests)/*`
- `/**[(Category=Smoke)|(Priority=High)]`

### `**` match-all

Matches any path depth, but it must appear at the end of the path.

Examples:

- `/**`
- `/MyAssembly/**`

## Property filtering

You can filter on custom properties in the last segment using `[...]`.

Examples:

- `/*/*/*/*[Category=Smoke]`
- `/*/*/*/*[Owner=*Team-Backend*]`
- `/*/*/*/*[Category!=Slow]`

## Important rules

- Only one property group `[...]` is allowed per path segment.
- If you need multiple property conditions, combine them inside the same brackets with `&` or `|`.
- Separate brackets like `[Category=Smoke]|[Priority=High]` are not valid.
- `**` must be at the end; `/**/Path` is not allowed.

## Common examples

- All tests: `/*/*/*/*`
- All smoke tests: `/*/*/*/*[Category=Smoke]`
- High-priority smoke tests: `/*/*/*/*[(Category=Smoke)&(Priority=High)]`
- Integration tests with priority: `/*/MyProject.Tests.Integration/*/*[Priority=Critical]`

## Note on `dotnet test`

TUnit does not use the usual VSTest `--filter` syntax. Use `--treenode-filter` instead.

Preferred:

- `dotnet test --treenode-filter "..."`

Compatibility note for older SDKs:

- `dotnet test -- --treenode-filter "..."`

## Repo filter quick starts

Use path-like tree-node filters for common flows in this repository:

- `/*/*/*/*/` — all tests (repo default)
- `/*/*/*/ProjectClassificationTests/*` — class-scoped
- `/*/*/*/*/InternalsVisibleTo*` — name pattern

## Zero-tests troubleshooting

If zero tests run:

1. Verify `--treenode-filter` is used (not `--filter`).
2. Try `dotnet test --treenode-filter "..."` first; if needed for your SDK version, use `dotnet test -- --treenode-filter "..."`.
3. Broaden filter first (`/*/*/*/*/`), then narrow.
4. Confirm target project contains `[Test]` methods.
5. Confirm property predicates are in one bracket group (e.g. `[(A=1)&(B=2)]`).

## Harness-aware validation flow

For SDK behavior that depends on MSBuild evaluation/import order:

1. Prefer integration tests under `src/tests/DotNetProjectSdk.IntegrationTests/`.
2. Use `ProjectHarness` (`Harness/ProjectHarness.cs`) to create throwaway projects.
3. Assert using:
   - `GetPropertyAsync` / `GetPropertiesAsync` for property evaluation
   - `GetItemIdentitiesAsync` for item projections
   - `BuildAsync(restore: true)` when package restore/build behavior is under test
4. Keep test setup minimal and deterministic:
   - set pre-import properties when behavior depends on `Sdk.props` import timing
   - avoid unrelated package references unless required by the scenario

## See also

- `../tunit-test-runner/SKILL.md`
- `../dotnet-tunit/SKILL.md`
- `../../../AGENTS.md`

## Available skills in this repository

Use this catalog to compose the right workflow:

- `changesets-prerelease` — prerelease bump and changeset/changelog quality.
- `dotnet-tunit` — TUnit test-authoring conventions and assertion style.
- `tunit-test-runner` — deep TUnit/MTP execution and troubleshooting guidance.
- `tunit-filtering` (this file) — concise `--treenode-filter` syntax and examples for this repo.
- `git-conventional-commits` — commit hygiene and commit message conventions.
- `lefthook-integration` — local Git hook strategy and setup guidance.

Paths:

- `../changesets-prerelease/SKILL.md`
- `../dotnet-tunit/SKILL.md`
- `../tunit-test-runner/SKILL.md`
- `../tunit-filtering/SKILL.md`
- `../git-conventional-commits/SKILL.md`
- `../lefthook-integration/SKILL.md`
