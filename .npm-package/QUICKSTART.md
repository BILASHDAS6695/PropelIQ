# Quick Start: Publishing to GitHub Packages

## Step 1: Authenticate (One-time setup)

Create Personal Access Token at: https://github.com/settings/tokens

Configure npm:
```powershell
# Edit or create: %USERPROFILE%\.npmrc
# Add these lines:
@kanini-software:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=YOUR_GITHUB_TOKEN_HERE
```

## Step 2: Test Locally

```powershell
cd d:\Project\PropelIQ-Stub\PropelIQ-Stub-Copilot\.npm-package

# Create package tarball
npm pack

# Test installation globally
npm install -g .\kanini-software-propeliq-copilot-stub-1.0.0.tgz

# Test in a sample project
cd C:\temp\test-project
propeliq-init

# Verify files were copied:
# - .github/
# - .propel/
# - .vscode/
# - .env.example
```

## Step 3: Publish to GitHub Packages

```powershell
cd d:\Project\PropelIQ-Stub\PropelIQ-Stub-Copilot\.npm-package

# Ensure repository exists: kanini-software/PropelIQ-Stub-Copilot

# Publish
npm publish

# Package will be available at:
# https://github.com/kanini-software/PropelIQ-Stub-Copilot/packages
```

## Step 4: Test Installation from GitHub Packages

```powershell
# In a clean environment
npm install -g @kanini-software/propeliq-copilot-stub

# Verify version
npm list -g @kanini-software/propeliq-copilot-stub

# Test installer
cd C:\temp\another-test-project
propeliq-init
```

## Troubleshooting

### Authentication Failed
- Verify token has `write:packages` and `read:packages` scopes
- Check .npmrc file has correct token
- Try logging in: `npm login --scope=@kanini-software --registry=https://npm.pkg.github.com`

### Repository Not Found
- Ensure GitHub repository exists: `kanini-software/PropelIQ-Stub-Copilot`
- Verify you have write access to the repository
- Repository must be created before publishing

### Package Already Exists
- Update version in package.json before republishing
- Cannot republish same version number

## Version Updates

To publish a new version:
```powershell
cd d:\Project\PropelIQ-Stub\PropelIQ-Stub-Copilot\.npm-package

# Update version in package.json (e.g., 1.0.0 -> 1.0.1)

# Commit changes
git add .
git commit -m "Release v1.0.1"
git tag v1.0.1
git push origin main --tags

# Publish
npm publish
```
