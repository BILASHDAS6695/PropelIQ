# Task 004: Workflow Completion — Artifacts, Fail-Fast & Full Validation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-005 |
| **Epic** | EP-TECH |
| **Layer** | CI/CD / GitHub Actions |
| **Priority** | High |
| **Estimated Effort** | 1 hour |
| **Dependencies** | Task 001, Task 002, Task 003 |

## Objective

Complete the CI workflow by verifying all three jobs are correctly wired in the
single `ci.yml`, confirming the `test-results/` directory structure, validating
the full workflow YAML is syntactically correct, and performing a dry-run push to
confirm the pipeline executes end-to-end within 10 minutes.

## Implementation Steps

### 1. Verify the complete `ci.yml` structure

After Tasks 001–003, the complete `.github/workflows/ci.yml` should contain:

```
name: CI
on: { push: branches ["**"], pull_request: branches [main] }
concurrency: { group, cancel-in-progress: true }
jobs:
  dotnet:  (Task 001)
  angular: (Task 002)
  python:  (Task 003)
```

All three jobs run **in parallel** (no `needs:` dependency between them) so the
wall-clock time equals `max(dotnet_time, angular_time, python_time)` — well
within the 10-minute target.

### 2. Confirm `test-results/` is gitignored

The `test-results/` directory is created at workflow runtime inside the runner.
Ensure it is NOT committed to the repository. Add to root `.gitignore` if absent:

**File:** `.gitignore`

```
# CI test result artefacts (generated at runtime, uploaded as GitHub artifacts)
test-results/
```

### 3. Add `paths-ignore` to reduce noise (optional optimisation)

Prevent CI from triggering on documentation-only or config-only changes:

```yaml
on:
  push:
    branches: ["**"]
    paths-ignore:
      - "**.md"
      - ".propel/**"
      - ".github/instructions/**"
      - ".github/skills/**"
      - ".github/prompts/**"
  pull_request:
    branches: [main]
    paths-ignore:
      - "**.md"
      - ".propel/**"
```

> **Note:** Apply `paths-ignore` only if the team agrees it is safe. PRs that
> only update docs will show no CI status, which can affect branch protection
> `required status checks`. Omit if unsure.

### 4. Final complete `ci.yml` reference

**File:** `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: ["**"]
  pull_request:
    branches: [main]

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:

  dotnet:
    name: .NET Build & Test
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/packages.lock.json', '**/*.csproj') }}
          restore-keys: nuget-${{ runner.os }}-
      - run: dotnet restore src/HealthPlatform.sln
      - run: dotnet build src/HealthPlatform.sln --no-restore --configuration Release
      - run: >
          dotnet test src/HealthPlatform.sln
          --no-build --configuration Release
          --logger "trx;LogFileName=test-results.trx"
          --collect:"XPlat Code Coverage"
          --results-directory ./test-results/dotnet
      - if: always()
        uses: actions/upload-artifact@v4
        with:
          name: dotnet-test-results
          path: ./test-results/dotnet/
          retention-days: 7

  angular:
    name: Angular Lint, Test & Build
    runs-on: ubuntu-latest
    timeout-minutes: 10
    defaults:
      run:
        working-directory: src/health-platform-ui
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: "20"
      - uses: actions/cache@v4
        with:
          path: ~/.npm
          key: npm-${{ runner.os }}-${{ hashFiles('src/health-platform-ui/package-lock.json') }}
          restore-keys: npm-${{ runner.os }}-
      - run: npm ci
      - run: npm run lint
      - run: npm test -- --run --reporter=junit --outputFile=../../test-results/angular/junit.xml
        env:
          CI: "true"
      - run: npm run build:prod
      - if: always()
        uses: actions/upload-artifact@v4
        with:
          name: angular-test-results
          path: ./test-results/angular/
          retention-days: 7

  python:
    name: Python Lint, Type-Check & Test
    runs-on: ubuntu-latest
    timeout-minutes: 10
    defaults:
      run:
        working-directory: src/ai-service
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with:
          python-version: "3.11"
      - uses: actions/cache@v4
        with:
          path: ~/.cache/pip
          key: pip-${{ runner.os }}-${{ hashFiles('src/ai-service/requirements-dev.txt') }}
          restore-keys: pip-${{ runner.os }}-
      - run: pip install -r requirements-dev.txt
      - run: ruff check app/ tests/
      - run: mypy app/ --ignore-missing-imports
      - run: pytest tests/ --tb=short --junitxml=../../test-results/python/junit.xml
      - if: always()
        uses: actions/upload-artifact@v4
        with:
          name: python-test-results
          path: ./test-results/python/
          retention-days: 7
```

### 5. Validate YAML syntax

```bash
# Python-based YAML lint (no external tools needed)
python -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"
# Expected: no output (parse success)
```

### 6. Trigger the pipeline

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add GitHub Actions CI pipeline"
git push origin feat/us-005-github-actions-ci-pipeline
```

Navigate to `https://github.com/BILASHDAS6695/PropelIQ/actions` and confirm:
- All three jobs are visible and running in parallel
- Total wall-clock time < 10 minutes
- All artifacts appear after completion

## Acceptance Criteria

- [ ] `ci.yml` contains exactly 3 jobs: `dotnet`, `angular`, `python`
- [ ] All jobs run in parallel (no `needs:` between them)
- [ ] `test-results/` added to root `.gitignore`
- [ ] YAML parses without error
- [ ] Pipeline runs end-to-end on GitHub Actions within 10 minutes
- [ ] All 3 artifact uploads appear under the completed workflow run

## Verification

```bash
# YAML parse check
python -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml')); print('YAML OK')"

# Line count sanity check (should be ~80-100 lines)
(Get-Content .github/workflows/ci.yml).Count
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-005 AC-1 | Triggers on push + PR |
| US-005 AC-5 | Pipeline fails if any step fails |
| US-005 AC-6 | Test results as artifacts |
| US-005 AC-7 | Build time < 10 minutes |
| TR-032 | GitHub Actions CI/CD |
