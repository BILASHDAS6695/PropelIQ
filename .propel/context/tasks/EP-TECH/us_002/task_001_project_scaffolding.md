# Task 001: Angular Project Scaffolding & Tooling Configuration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-002 |
| **Epic** | EP-TECH |
| **Layer** | Frontend / Build |
| **Priority** | Critical |
| **Estimated Effort** | 2 hours |
| **Dependencies** | None (first frontend task) |

## Objective

Create the Angular 17+ project with standalone components architecture, TypeScript strict mode, ESLint + Prettier, environment files, and proxy configuration for local API development.

## Implementation Steps

### 1. Create Angular Project

```bash
ng new health-platform-web --standalone --style=scss --routing --skip-tests=false --ssr=false
cd health-platform-web
```

Key flags:
- `--standalone` — no NgModule-based architecture
- `--style=scss` — SCSS preprocessor
- `--routing` — includes app.routes.ts

### 2. Verify TypeScript Strict Mode

Confirm `tsconfig.json` has:

```json
{
  "compilerOptions": {
    "strict": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true
  }
}
```

### 3. Configure ESLint

```bash
ng add @angular-eslint/schematics
```

Add to `.eslintrc.json` (root overrides):

```json
{
  "overrides": [
    {
      "files": ["*.ts"],
      "extends": [
        "plugin:@angular-eslint/recommended",
        "plugin:@angular-eslint/template/process-inline-templates"
      ],
      "rules": {
        "@angular-eslint/directive-selector": ["error", { "type": "attribute", "prefix": "app", "style": "camelCase" }],
        "@angular-eslint/component-selector": ["error", { "type": "element", "prefix": "app", "style": "kebab-case" }]
      }
    },
    {
      "files": ["*.html"],
      "extends": ["plugin:@angular-eslint/template/recommended", "plugin:@angular-eslint/template/accessibility"]
    }
  ]
}
```

### 4. Configure Prettier

```bash
npm install --save-dev prettier eslint-config-prettier eslint-plugin-prettier
```

**File:** `.prettierrc`

```json
{
  "singleQuote": true,
  "trailingComma": "all",
  "printWidth": 100,
  "tabWidth": 2,
  "semi": true,
  "bracketSpacing": true
}
```

**File:** `.prettierignore`

```
dist/
coverage/
node_modules/
.angular/
```

Add prettier to ESLint extends: `"plugin:prettier/recommended"` (last in list).

### 5. Configure Environment Files

**File:** `src/environments/environment.ts`

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5013/api',
  appName: 'HealthPlatform',
};
```

**File:** `src/environments/environment.prod.ts`

```typescript
export const environment = {
  production: true,
  apiUrl: '/api',
  appName: 'HealthPlatform',
};
```

Update `angular.json` fileReplacements for production:

```json
"fileReplacements": [
  {
    "replace": "src/environments/environment.ts",
    "with": "src/environments/environment.prod.ts"
  }
]
```

### 6. Configure Dev Proxy

**File:** `proxy.conf.json`

```json
{
  "/api": {
    "target": "https://localhost:5013",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
```

Update `angular.json` serve options:

```json
"serve": {
  "options": {
    "proxyConfig": "proxy.conf.json"
  }
}
```

### 7. Add NPM Scripts

Update `package.json` scripts:

```json
{
  "scripts": {
    "start": "ng serve",
    "build": "ng build",
    "build:prod": "ng build --configuration production",
    "lint": "ng lint",
    "lint:fix": "ng lint --fix",
    "format": "prettier --write \"src/**/*.{ts,html,scss}\"",
    "format:check": "prettier --check \"src/**/*.{ts,html,scss}\""
  }
}
```

## Acceptance Criteria

- [ ] Angular 17+ project created with standalone component architecture
- [ ] TypeScript strict mode enabled (`strict: true`)
- [ ] ESLint configured with `@angular-eslint` rules
- [ ] Prettier configured with `.prettierrc`
- [ ] `src/environments/environment.ts` and `environment.prod.ts` exist with `apiUrl`
- [ ] `proxy.conf.json` proxies `/api` to backend
- [ ] `ng build --configuration production` succeeds with optimized bundle
- [ ] `ng lint` passes with zero errors

## Verification

```bash
ng build --configuration production
ng lint
npm run format:check
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-001 | Angular 17+ standalone |
| US-002 AC-1 | Standalone components architecture |
| US-002 AC-7 | Environment files |
| US-002 AC-8 | Production build optimized |
| US-002 AC-9 | ESLint + Prettier |
| US-002 AC-10 | Proxy config |
