# Task 003: Python CI Job (ruff + mypy + pytest)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-005 |
| **Epic** | EP-TECH |
| **Layer** | CI/CD / GitHub Actions |
| **Priority** | High |
| **Estimated Effort** | 1 hour |
| **Dependencies** | Task 001 |

## Objective

Add the Python CI job to `.github/workflows/ci.yml`: pip cache, ruff lint,
mypy type check, and pytest. Also add `ruff` and `mypy` to
`src/ai-service/requirements-dev.txt` so the same tool versions are used
locally and in CI.

## Implementation Steps

### 1. Update `requirements-dev.txt`

**File:** `src/ai-service/requirements-dev.txt`

Add `ruff` and `mypy` pinned versions:

```
-r requirements.txt
pytest==8.2.0
pytest-asyncio==0.23.6
httpx==0.27.0
ruff==0.4.4
mypy==1.10.0
```

### 2. Add `python` job to `.github/workflows/ci.yml`

Append the following job to the existing `jobs:` block:

```yaml
  python:
    name: Python Lint, Type-Check & Test
    runs-on: ubuntu-latest
    timeout-minutes: 10
    defaults:
      run:
        working-directory: src/ai-service

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Python 3.11
        uses: actions/setup-python@v5
        with:
          python-version: "3.11"

      - name: Cache pip packages
        uses: actions/cache@v4
        with:
          path: ~/.cache/pip
          key: pip-${{ runner.os }}-${{ hashFiles('src/ai-service/requirements-dev.txt') }}
          restore-keys: |
            pip-${{ runner.os }}-

      - name: Install dependencies
        run: pip install -r requirements-dev.txt

      - name: Lint (ruff)
        run: ruff check app/ tests/

      - name: Type check (mypy)
        run: mypy app/ --ignore-missing-imports

      - name: Test (pytest)
        run: >
          pytest tests/
          --tb=short
          --junitxml=../../test-results/python/junit.xml

      - name: Upload Python test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: python-test-results
          path: ./test-results/python/
          retention-days: 7
```

### 3. Add `ruff.toml` (optional but recommended)

A minimal ruff configuration at `src/ai-service/ruff.toml` keeps CI and local
runs consistent:

**File:** `src/ai-service/ruff.toml`

```toml
target-version = "py311"
line-length = 100

[lint]
select = ["E", "F", "I"]   # pycodestyle errors, pyflakes, isort
ignore = ["E501"]           # line-length handled by formatter, not linter
```

### 4. Add `mypy.ini` (optional but recommended)

**File:** `src/ai-service/mypy.ini`

```ini
[mypy]
python_version = 3.11
strict = false
ignore_missing_imports = true
exclude = tests/
```

## Acceptance Criteria

- [ ] `ruff==0.4.4` and `mypy==1.10.0` added to `requirements-dev.txt`
- [ ] `python` job added to `ci.yml` with `working-directory: src/ai-service`
- [ ] pip cache keyed on `requirements-dev.txt` hash
- [ ] `ruff check app/ tests/` runs and fails workflow on lint errors
- [ ] `mypy app/ --ignore-missing-imports` runs and fails workflow on type errors
- [ ] `pytest tests/` produces JUnit XML at `test-results/python/junit.xml`
- [ ] Test results uploaded as `python-test-results` artifact
- [ ] Job `timeout-minutes: 10`

## Verification

```bash
# Install dev deps locally
cd src/ai-service
pip install -r requirements-dev.txt

# Run ruff
ruff check app/ tests/
# Expected: exit 0 (no errors)

# Run mypy
mypy app/ --ignore-missing-imports
# Expected: Success: no issues found

# Run pytest
pytest tests/ --tb=short
# Expected: all tests pass
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-005 AC-4 | Python lint (ruff), type check (mypy) |
| US-005 AC-6 | Test results uploaded as artifact |
| US-005 AC-7 | Build time under 10 minutes |
| US-005 AC-8 | pip caching |
| TR-032 | GitHub Actions CI/CD |
