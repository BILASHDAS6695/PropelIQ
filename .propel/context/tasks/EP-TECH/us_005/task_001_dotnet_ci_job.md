# Task 001: GitHub Actions Workflow Skeleton & .NET CI Job

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-005 |
| **Epic** | EP-TECH |
| **Layer** | CI/CD / GitHub Actions |
| **Priority** | Critical |
| **Estimated Effort** | 1 hour |
| **Dependencies** | None (first task) |

## Objective

Create the GitHub Actions CI workflow file with correct triggers and implement
the .NET job: restore, build, test (xUnit + coverlet), and NuGet cache.

## Implementation Steps

### 1. Create the Workflow File

**File:** `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: ["**"]
  pull_request:
    branches: [main]

# Cancel any in-progress run for the same ref to save minutes
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  dotnet:
    name: .NET Build & Test
    runs-on: ubuntu-latest
    timeout-minutes: 10

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/packages.lock.json', '**/*.csproj') }}
          restore-keys: |
            nuget-${{ runner.os }}-

      - name: Restore
        run: dotnet restore src/HealthPlatform.sln

      - name: Build
        run: >
          dotnet build src/HealthPlatform.sln
          --no-restore
          --configuration Release

      - name: Test
        run: >
          dotnet test src/HealthPlatform.sln
          --no-build
          --configuration Release
          --logger "trx;LogFileName=test-results.trx"
          --collect:"XPlat Code Coverage"
          --results-directory ./test-results/dotnet

      - name: Upload .NET test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: dotnet-test-results
          path: ./test-results/dotnet/
          retention-days: 7
```

### 2. Verify `.github/workflows/` directory exists

The `.github/` directory at repo root should already exist (it holds `prompts/`,
`skills/`, `instructions/`). The `workflows/` subdirectory is new.

### 3. Confirm `HealthPlatform.sln` path

The solution file lives at `src/HealthPlatform.sln`. All dotnet CLI commands in
the workflow use `src/HealthPlatform.sln` as the target — never a specific
`.csproj` — so every project (including Tests) is included automatically.

## Acceptance Criteria

- [ ] `.github/workflows/ci.yml` exists with `on: push` (all branches) and `on: pull_request` to `main`
- [ ] `concurrency` block cancels duplicate runs
- [ ] `dotnet` job: restore → build → test, all `--no-restore` / `--no-build` chained
- [ ] NuGet cache keyed on `*.csproj` + `packages.lock.json` hashes
- [ ] Test results written to `./test-results/dotnet/` and uploaded as artifact
- [ ] Job `timeout-minutes: 10`

## Verification

```bash
# Lint the workflow YAML locally (optional — requires actionlint)
actionlint .github/workflows/ci.yml

# Push a branch and confirm the workflow appears in GitHub Actions tab
git push origin HEAD
# Navigate to: https://github.com/BILASHDAS6695/PropelIQ/actions
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-005 AC-1 | Triggers on push + PR to main |
| US-005 AC-2 | .NET build, test |
| US-005 AC-6 | Test results uploaded as artifacts |
| US-005 AC-7 | Build time under 10 minutes |
| US-005 AC-8 | NuGet caching |
| TR-032 | GitHub Actions CI/CD |
