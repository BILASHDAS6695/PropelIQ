# @kanini-software/propeliq-copilot-stub

GitHub Copilot configuration stub for PropelIQ projects by KANINI Software Solutions. This package provides a complete setup for AI-assisted development with GitHub Copilot, including prompts, skills, instructions, and project configuration.

**PROPRIETARY SOFTWARE** - This software is the property of KANINI Software Solutions. See LICENSE file for terms and conditions.

## Installation

### Prerequisites

1. **Authenticate with GitHub Packages**
   
   Create a Personal Access Token (PAT) with `read:packages` scope:
   - Go to GitHub Settings > Developer settings > Personal access tokens > Tokens (classic)
   - Generate new token with `read:packages` permission
   - Copy the token

2. **Configure npm for GitHub Packages**
   
   Create or edit `~/.npmrc` (Windows: `%USERPROFILE%\.npmrc`):
   ```
   @kanini-software:registry=https://npm.pkg.github.com
   //npm.pkg.github.com/:_authToken=YOUR_GITHUB_TOKEN
   ```
   
   Replace `YOUR_GITHUB_TOKEN` with your PAT.

### Install Package

```bash
npm install -g @kanini-software/propeliq-copilot-stub
```

Or as a dev dependency in your project:

```bash
npm install --save-dev @kanini-software/propeliq-copilot-stub
```

## Usage

Navigate to your project root directory and run:

```bash
npx propeliq-init
```

Or if installed globally:

```bash
propeliq-init
```

The installer will copy configuration files to your current directory (where you run the command).

This will install the following to your project root:

- `.github/` - GitHub Copilot prompts, instructions, skills, and agents
- `.propel/` - PropelIQ templates, rules, prompts, and orchestrators
- `.vscode/` - VS Code tasks and MCP configuration
- `.env.example` - Environment variable template

**Note:** Existing files and folders will be skipped to preserve your customizations.

## What Gets Installed

### `.github/` Structure
- **prompts/** - Reusable AI prompts for various development tasks
- **skills/** - Domain-specific knowledge and coding standards
- **instructions/** - Development guidelines and best practices
- **agents/** - Custom GitHub Copilot agents
- **hooks/** - Copilot hooks configuration
- **copilot-instructions.md** - Main Copilot configuration

### `.propel/` Structure
- **prompts/** - Workflow prompts for change management and analysis
- **templates/** - Document templates for specs, plans, and artifacts
- **rules/** - Custom validation and enforcement rules
- **orchestrators/** - Workflow orchestration definitions
- **learnings/** - Captured patterns and findings
- **hooks/** - PropelIQ hooks
- **instructions.md** - Core workflow instructions
- **id-scheme-registry.md** - ID scheme registry for artifacts
- **project-config.json** - Project configuration
- **requirements.txt** - Python dependencies for skills

### `.vscode/` Structure
- **mcp.json** - Model Context Protocol configuration
- **tasks.json** - VS Code task definitions

## Post-Installation Steps

1. **Review Environment Variables**
   ```bash
   cp .env.example .env
   # Edit .env with your project-specific values
   ```

2. **Configure Context7 API Key**
   
   Context7 provides AI-powered code search and semantic understanding. To enable it:
   
   - Get your API key from [Context7](https://context7.ai)
   - Open `.vscode/mcp.json`
   - Update the `context7` server configuration:
     ```json
     "context7": {
       "command": "npx",
       "args": ["-y", "@context7/mcp-server"],
       "env": {
         "CONTEXT7_API_KEY": "your-api-key-here"
       }
     }
     ```
   - Replace `"your-api-key-here"` with your actual API key
   - Restart VS Code to apply changes

3. **Configure MCP Servers**
   
   Model Context Protocol (MCP) servers extend AI assistant capabilities:
   
   - Open `.vscode/mcp.json`
   - Review enabled servers:
     - `propel-iq` - PropelIQ template and workflow management
     - `context7` - Semantic code search
     - `azure-mcp` - Azure resource management
     - `sequential-thinking` - Step-by-step reasoning
   - Enable/disable servers as needed by adding/removing entries
   - Configure server-specific settings (API keys, endpoints, etc.)
   - Restart VS Code after configuration changes
   
   **Common MCP Server Configurations:**
   
   ```json
   {
     "mcpServers": {
       "propel-iq": {
         "command": "npx",
         "args": ["-y", "@propeliq/mcp-server"]
       },
       "context7": {
         "command": "npx",
         "args": ["-y", "@context7/mcp-server"],
         "env": {
           "CONTEXT7_API_KEY": "your-key"
         }
       },
       "azure-mcp": {
         "command": "npx",
         "args": ["-y", "@azure/mcp-server"],
         "env": {
           "AZURE_SUBSCRIPTION_ID": "your-subscription-id"
         }
       }
     }
   }
   ```

4. **Customize Copilot Instructions**
   - Edit `.github/copilot-instructions.md` for your project
   - Review and modify `.github/instructions/*.instructions.md` as needed

5. **Configure PropelIQ**
   - Update `.propel/project-config.json` with your project details
   - Review `.propel/instructions.md` for workflow guidance

6. **Install Python Dependencies (if using skills)**
   ```bash
   pip install -r .propel/requirements.txt
   ```

## Features

### AI-Assisted Development
- 40+ pre-configured prompts for common development tasks
- Specialized agents for specs, testing, and architecture
- Comprehensive coding standards and guidelines

### Change Management
- Structured change request workflows
- Impact analysis automation
- Traceability enforcement

### Quality Assurance
- Test plan generation
- Code review automation
- Edge case analysis

### Documentation
- Automatic spec generation
- UML and PlantUML support
- Figma integration for design specs

## Updating Configuration

The installer skips existing files to preserve your customizations. To update specific components:

### Option 1: Selective Update

1. **Backup your customizations**
   ```bash
   cp -r .github .github.backup
   cp -r .propel .propel.backup
   ```

2. **Remove what you want to update**
   ```bash
   # Example: Update only .github folder
   rm -rf .github
   ```

3. **Re-run installer from your project root**
   ```bash
   propeliq-init
   ```

4. **Merge customizations** from your backup if needed

### Option 2: Fresh Install

Remove all installed components and reinstall:
```bash
rm -rf .github .propel .vscode .env.example
propeliq-init
```

## Troubleshooting

### Files Already Exist
The installer automatically skips existing files and folders to preserve your customizations. If you want to reinstall specific items, remove or rename them first.

### Wrong Installation Directory
Always run `propeliq-init` from your project root directory. The installer copies files to the current working directory (where you execute the command).

### Permission Errors
Ensure you have write permissions in the target directory.

### Missing Dependencies
Some features require Python packages. Install them with:
```bash
pip install -r .propel/requirements.txt
```

## Customization

All installed files are meant to be customized for your project. The installer creates a baseline configuration that you should adapt to your specific needs.

Key files to customize:
- `.github/copilot-instructions.md` - Main Copilot behavior
- `.github/instructions/*.instructions.md` - Coding standards for your stack
- `.propel/project-config.json` - Project-specific settings
- `.propel/instructions.md` - Workflow preferences

## Support

For licensing, support, or questions:
- Email: support@kanini.com
- Website: https://www.kanini.com/propeliq
- Documentation: Review installed files in `.github/` and `.propel/` folders

## License

Proprietary - Copyright (c) 2026 KANINI Software Solutions. All rights reserved.

This software is the property of KANINI Software Solutions and is protected by copyright law. Unauthorized copying, distribution, modification, or use of this software is strictly prohibited without prior written permission from KANINI Software Solutions.

See LICENSE file for complete terms and conditions.
