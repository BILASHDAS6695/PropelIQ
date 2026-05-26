# Task 005: Branch Protection Rules Documentation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-005 |
| **Epic** | EP-TECH |
| **Layer** | CI/CD / Repository Configuration |
| **Priority** | Medium |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 004 (pipeline must be passing before protection is applied) |

## Objective

Document and apply GitHub branch protection rules for the `main` branch that
require the CI pipeline to pass before any PR can be merged. This enforces
US-005 AC-1 / AC-5 at the repository policy level.

## Implementation Steps

### 1. Apply Branch Protection via GitHub UI

Navigate to:
`https://github.com/BILASHDAS6695/PropelIQ/settings/branches`

Click **Add rule** (or **Edit** if a rule for `main` already exists) and
configure:

| Setting | Value |
|---------|-------|
| Branch name pattern | `main` |
| Require a pull request before merging | ✅ Enabled |
| — Required approvals | `1` |
| Require status checks to pass before merging | ✅ Enabled |
| — Require branches to be up to date before merging | ✅ Enabled |
| Required status checks | `dotnet` · `angular` · `python` |
| Restrict who can push to matching branches | (optional — owner only for now) |
| Do not allow bypassing the above settings | ✅ Enabled |

> **Important:** The status check names (`dotnet`, `angular`, `python`) must
> match the `jobs.<job-id>` keys in `ci.yml` exactly. They will only appear in
> the autocomplete dropdown after at least one CI run has completed on a PR.

### 2. Create `.github/branch-protection.md`

Document the rules so future contributors understand the required checks:

**File:** `.github/branch-protection.md`

```markdown
# Branch Protection Rules — `main`

## Required Status Checks

All three CI jobs must pass before a PR can be merged into `main`:

| Check name | Workflow | Description |
|---|---|---|
| `dotnet` | `ci.yml` | .NET restore, build, xUnit tests |
| `angular` | `ci.yml` | ESLint lint, vitest tests, production build |
| `python` | `ci.yml` | ruff lint, mypy type-check, pytest |

## How to Configure (GitHub UI)

1. Navigate to **Settings → Branches → Branch protection rules**
2. Add rule for pattern `main`
3. Enable **Require status checks to pass before merging**
4. Search for and add: `dotnet`, `angular`, `python`
5. Enable **Require branches to be up to date**
6. Enable **Do not allow bypassing the above settings**

## Coverage Thresholds (NFR-021 / NFR-022)

| Layer | Threshold | Tool |
|---|---|---|
| .NET backend | ≥ 80% line coverage | coverlet (`XPlat Code Coverage`) |
| Angular frontend | ≥ 70% line coverage | vitest `--coverage` |

Coverage reports are uploaded as CI artifacts (`dotnet-test-results`,
`angular-test-results`) on every run. Threshold enforcement via
`reportgenerator` or a dedicated coverage gate step will be added in a
follow-up task (US-006 or equivalent).
```

### 3. Verify Status Check Names Appear After First CI Run

After the CI pipeline runs at least once on a PR branch:

1. Go to `Settings → Branches → Edit rule for main`
2. In the **Status checks** search box, type `dotnet`
3. Confirm `dotnet`, `angular`, and `python` appear as suggestions
4. Select all three

### 4. (Optional) Add `CODEOWNERS`

**File:** `.github/CODEOWNERS`

```
# Global owners — all PRs require review from at least one owner
* @BILASHDAS6695
```

## Acceptance Criteria

- [ ] `.github/branch-protection.md` documents the three required checks
- [ ] Branch protection rule applied on GitHub for `main` (UI step)
- [ ] `dotnet`, `angular`, `python` listed as required status checks
- [ ] PRs to `main` require at least 1 approval
- [ ] Coverage threshold targets documented (NFR-021 / NFR-022)

## Verification

```bash
# Confirm branch-protection.md is committed
Test-Path .github/branch-protection.md

# Attempt a direct push to main — expect rejection
git checkout main
git commit --allow-empty -m "test: direct push should be blocked"
git push origin main
# Expected: remote: error: GH006 Protected branch update failed
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-005 AC-9 | Branch protection rules documented |
| US-005 AC-5 | Pipeline fails prevent merge |
| NFR-021 | .NET coverage ≥ 80% (target documented) |
| NFR-022 | Angular coverage ≥ 70% (target documented) |
| TR-032 | GitHub Actions CI/CD |
