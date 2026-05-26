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
2. Click **Add rule** (or **Edit** if a rule for `main` already exists)
3. Set **Branch name pattern** to `main`
4. Enable **Require a pull request before merging** → set Required approvals to `1`
5. Enable **Require status checks to pass before merging**
6. Enable **Require branches to be up to date before merging**
7. In the status checks search box, add: `dotnet`, `angular`, `python`
8. Enable **Do not allow bypassing the above settings**
9. Click **Save changes**

> **Important:** The status check names must match the `jobs.<job-id>` keys in
> `ci.yml` exactly. They only appear in the autocomplete dropdown after at least
> one CI run has completed on any PR branch.

## Coverage Thresholds (NFR-021 / NFR-022)

| Layer | Threshold | Tool |
|---|---|---|
| .NET backend | ≥ 80% line coverage | coverlet (`XPlat Code Coverage`) |
| Angular frontend | ≥ 70% line coverage | vitest `--coverage` |

Coverage reports are uploaded as CI artifacts (`dotnet-test-results`,
`angular-test-results`) on every run. Threshold enforcement via
`reportgenerator` or a dedicated coverage gate step will be added in a
follow-up task (US-006 or equivalent).
