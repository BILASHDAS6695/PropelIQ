# Publishing @kanini-software/propeliq-copilot-stub

This folder contains the npm package configuration for publishing PropelIQ-Stub-Copilot to GitHub Packages.

**PROPRIETARY SOFTWARE** - Copyright (c) 2026 KANINI Software Solutions. All rights reserved.

## Prerequisites

1. **GitHub Personal Access Token**
   - Go to GitHub Settings > Developer settings > Personal access tokens
   - Generate new token (classic) with these scopes:
     - `write:packages` - To publish packages
     - `read:packages` - To download packages
     - `delete:packages` - To delete package versions (optional)
   - Copy and save the token securely

2. **Authenticate with GitHub Packages**
   
   Create or edit `~/.npmrc` (Windows: `%USERPROFILE%\.npmrc`):
   ```
   @kanini-software:registry=https://npm.pkg.github.com
   //npm.pkg.github.com/:_authToken=YOUR_GITHUB_TOKEN
   ```
   
   Or authenticate via npm CLI:
   ```bash
   npm login --scope=@kanini-software --registry=https://npm.pkg.github.com
   # Username: your-github-username
   # Password: your-github-token (not your GitHub password!)
   # Email: your-email@example.com
   ```

3. **GitHub Repository**
   - Repository must exist: `kanini-software/PropelIQ-Stub-Copilot`
   - You must have write access to the repository

## Package Structure

```
.npm-package/
├── package.json          # npm package manifest
├── index.js             # Package entry point
├── .npmignore           # Files to exclude from npm package
├── README.md            # Package README (shown on npm)
└── bin/
    └── init.js          # CLI installation script
```

## Publishing to GitHub Packages

### 1. Update Version

Edit `package.json` and increment the version:
```json
{
  "version": "1.0.1"
}
```

### 2. Test Locally

Test the package installation locally:
```bash
cd .npm-package
npm pack
npm install -g kanini-software-propeliq-copilot-stub-1.0.0.tgz
cd /path/to/test/project
propeliq-init
```

Verify all files copy correctly and the installer works as expected.

### 3. Authenticate with GitHub

Ensure you're authenticated (see Prerequisites section above):
```bash
npm login --scope=@kanini-software --registry=https://npm.pkg.github.com
```

### 4. Publish to GitHub Packages

From the `.npm-package` directory:
```bash
cd .npm-package

# Publish the package
npm publish

# The package will be published to:
# https://github.com/kanini-software/PropelIQ-Stub-Copilot/packages
```

**Note:** The `publishConfig` in package.json automatically directs npm to GitHub Packages registry.

### 5. Verify Publication

1. Go to `https://github.com/orgs/kanini-software/packages`
2. Find `propeliq-copilot-stub` package
3. Verify version and metadata
4. Test installation from GitHub Packages:
   ```bash
   npm install -g @kanini-software/propeliq-copilot-stub
   propeliq-init
   ```

## What Gets Packaged

The package includes:
- `.github/` - All Copilot configuration
- `.propel/` - All PropelIQ templates and workflows
- `.vscode/` - VS Code configuration
- `.env.example` - Environment template

## Package Installation

Users must first authenticate with GitHub Packages (see Prerequisites), then install with:

```bash
npm install -g @kanini-software/propeliq-copilot-stub
propeliq-init
```

Or use directly with npx (still requires authentication):
```bash
npx @kanini-software/propeliq-copilot-stub
```

## Update Checklist

Before publishing:
- [ ] Update version in package.json
- [ ] Update README.md with latest features
- [ ] Test installation locally with `npm pack`
- [ ] Verify all required files are in package.json "files" array
- [ ] Review .npmignore to ensure no sensitive data is included
- [ ] Authenticate with GitHub Packages
- [ ] Ensure GitHub repository exists and you have write access
- [ ] Commit changes to git
- [ ] Create git tag for version: `git tag v1.0.0`
- [ ] Push tag: `git push origin v1.0.0`
- [ ] Publish to GitHub Packages: `npm publish`
- [ ] Verify package appears in GitHub Packages UI
- [ ] Test installation from GitHub Packages registry
