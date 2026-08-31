# release-prep (generic agent spec)

## Goal

Prepare a safe prerelease-ready update with minimal churn and clear validation.

## Workflow

1. Read current version from `package.json` and inspect pending `.changeset/*.md` files.
2. Ensure release notes describe user-visible changes succinctly.
3. Run repository validation commands:
   - `just lint-check`
   - `just build`
   - `just test`
4. If tests fail due to filtering invocation, use MTP-compatible `--treenode-filter` semantics.
5. Summarize changed files, validation output, and any follow-up actions.

## Constraints

- Keep edits narrowly scoped to release prep.
- Do not rewrite unrelated docs/code.
- Follow existing conventional-commit and changesets conventions in this repository.

## Available skills in this repository

When relevant, explicitly use the matching skill guidance:

- `changesets-prerelease` — prerelease bump and changeset/changelog quality.
- `dotnet-tunit` — TUnit test-authoring conventions and assertion style.
- `tunit-test-runner` — deep TUnit/MTP execution and troubleshooting guidance.
- `tunit-filtering` — concise `--treenode-filter` syntax and examples for this repo.
- `git-conventional-commits` — commit hygiene and commit message conventions.
- `lefthook-integration` — local Git hook strategy and setup guidance.

Paths:

- `../skills/changesets-prerelease/SKILL.md`
- `../skills/dotnet-tunit/SKILL.md`
- `../skills/tunit-test-runner/SKILL.md`
- `../skills/tunit-filtering/SKILL.md`
- `../skills/git-conventional-commits/SKILL.md`
- `../skills/lefthook-integration/SKILL.md`
