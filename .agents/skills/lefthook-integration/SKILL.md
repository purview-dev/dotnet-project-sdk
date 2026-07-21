---
name: lefthook-integration
description: Lefthook Git hook integration.
---

# lefthook-integration Skill

Use this skill for repo-local Git hook setup.

## Rules

- Prefer small, fast checks in pre-commit.
- Put slower checks in pre-push.
- Do not block commits with network-dependent checks.
- Keep commands cross-platform where possible.
- Document how to install and run hooks locally.

## Available skills in this repository

Use this catalog to compose the right workflow:

- `changesets-prerelease` — prerelease bump and changeset/changelog quality.
- `dotnet-tunit` — TUnit test-authoring conventions and assertion style.
- `tunit-test-runner` — deep TUnit/MTP execution and troubleshooting guidance.
- `tunit-filtering` — concise `--treenode-filter` syntax and examples for this repo.
- `git-conventional-commits` — commit hygiene and commit message conventions.
- `lefthook-integration` (this file) — local Git hook strategy and setup guidance.

Paths:

- `../changesets-prerelease/SKILL.md`
- `../dotnet-tunit/SKILL.md`
- `../tunit-test-runner/SKILL.md`
- `../tunit-filtering/SKILL.md`
- `../git-conventional-commits/SKILL.md`
- `../lefthook-integration/SKILL.md`
