# Task 002: Angular CI Job (ESLint + Vitest + Production Build)

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

Add the Angular CI job to `.github/workflows/ci.yml`: install dependencies
(npm cache), ESLint lint, vitest unit tests, and production build.

## Implementation Steps

### 1. Add `angular` job to `.github/workflows/ci.yml`

Append the following job to the existing `jobs:` block:

```yaml
  angular:
    name: Angular Lint, Test & Build
    runs-on: ubuntu-latest
    timeout-minutes: 10
    defaults:
      run:
        working-directory: src/health-platform-ui

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node 20
        uses: actions/setup-node@v4
        with:
          node-version: "20"

      - name: Cache npm packages
        uses: actions/cache@v4
        with:
          path: ~/.npm
          key: npm-${{ runner.os }}-${{ hashFiles('src/health-platform-ui/package-lock.json') }}
          restore-keys: |
            npm-${{ runner.os }}-

      - name: Install dependencies
        run: npm ci

      - name: Lint
        run: npm run lint

      - name: Test
        run: npm test -- --run --reporter=junit --outputFile=../../test-results/angular/junit.xml
        env:
          CI: true

      - name: Build (production)
        run: npm run build:prod

      - name: Upload Angular test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: angular-test-results
          path: ./test-results/angular/
          retention-days: 7
```

### 2. Add vitest JUnit reporter

The `npm test` command in CI uses `--reporter=junit` to produce a machine-readable
test result file. Vitest supports this natively — no extra package needed.

Confirm the test script in `src/health-platform-ui/package.json` uses `ng test`
(which delegates to vitest via the Angular CLI builder):

```json
"test": "ng test"
```

The additional `-- --run --reporter=junit` flags are passed through to vitest by
the Angular CLI builder, causing a single non-watch test run with JUnit output.

### 3. Confirm `build:prod` script

The `package.json` already has:

```json
"build:prod": "ng build --configuration production"
```

No changes to `package.json` are required for this task.

## Acceptance Criteria

- [ ] `angular` job added to `ci.yml` with `working-directory: src/health-platform-ui`
- [ ] npm cache keyed on `package-lock.json` hash
- [ ] `npm run lint` step runs ESLint (fails workflow on lint errors)
- [ ] `npm test -- --run` runs vitest in non-watch CI mode
- [ ] JUnit XML written to `test-results/angular/junit.xml`
- [ ] `npm run build:prod` runs Angular production build
- [ ] Test results uploaded as `angular-test-results` artifact
- [ ] Job `timeout-minutes: 10`

## Verification

```bash
# Confirm vitest supports --run and --reporter flags
cd src/health-platform-ui
npx vitest --help | grep -E "\-\-run|\-\-reporter"

# Local lint check
npm run lint
# Expected: exit 0

# Local test run (CI mode)
npm test -- --run
# Expected: all tests pass, exit 0
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-005 AC-3 | Angular lint (ESLint), build (production), test (vitest) |
| US-005 AC-6 | Test results uploaded as artifact |
| US-005 AC-7 | Build time under 10 minutes |
| US-005 AC-8 | npm caching |
| TR-032 | GitHub Actions CI/CD |
| NFR-022 | Code coverage ≥ 70% (frontend) |
