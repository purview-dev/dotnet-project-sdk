---
name: dotnet-tunit
description: Use .NET with TUnit-specific test conventions.
category: dotnet
roles:
  - dotnet
  - dotnet-tunit
  - coding
tags:
  - dotnet
  - csharp
  - tunit
  - tests
---

# dotnet-tunit Skill

Use this skill for .NET repositories that use TUnit.

## Rules

- Use `TUnit.Core`.
- Use `TUnit.Assertions`.
- Test methods must have `[Test]`.
- Assertion calls must be awaited.
- Prefer `await Assert.That(actual).IsEqualTo(expected);`.
- Do not use xUnit, NUnit, MSTest, or FluentAssertions unless explicitly requested.
- Run the relevant `dotnet test` command before completion.

## Available skills in this repository

Use this catalog to compose the right workflow:

- `changesets-prerelease` — prerelease bump and changeset/changelog quality.
- `dotnet-tunit` (this file) — TUnit test-authoring conventions and assertion style.
- `tunit-test-runner` — deep TUnit/MTP execution and troubleshooting guidance.
- `tunit-filtering` — concise `--treenode-filter` syntax and examples for this repo.
- `git-conventional-commits` — commit hygiene and commit message conventions.
- `lefthook-integration` — local Git hook strategy and setup guidance.

Paths:

- `../changesets-prerelease/SKILL.md`
- `../dotnet-tunit/SKILL.md`
- `../tunit-test-runner/SKILL.md`
- `../tunit-filtering/SKILL.md`
- `../git-conventional-commits/SKILL.md`
- `../lefthook-integration/SKILL.md`
