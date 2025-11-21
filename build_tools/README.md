# Build Tools

This directory contains build automation and documentation generation tools for the Sharpy project.

## Scripts

### generate_code_walkthroughs.py

Automatically generates comprehensive code walkthrough documentation for C# source files using GitHub Copilot CLI.

**Purpose**: Creates markdown documentation that helps newcomer engineers understand the codebase structure, design patterns, and how to effectively contribute and debug.

**Features**:
- ✅ Parallel processing with configurable instances (default: 3)
- ✅ Rate limiting protection with configurable timeouts
- ✅ Smart resumption - skips already documented files
- ✅ Preserves directory structure in output
- ✅ Uses Copilot CLI with `--allow-tool 'write'` permission for markdown file creation

**Requirements**:
- GitHub CLI (`gh`) installed and authenticated
- GitHub Copilot CLI enabled
- Python 3.8+

**Usage**:
```bash
# Basic usage with defaults
./build_tools/generate_code_walkthroughs.py

# Custom configuration
./build_tools/generate_code_walkthroughs.py --parallel 5 --timeout 90

# See all options
./build_tools/generate_code_walkthroughs.py --help
```

**Output**: Generated markdown files are placed in `docs/internal_walkthrough/` with the same directory structure as the source files.

**Permissions**: The script uses Copilot CLI's programmatic mode with `--allow-tool 'write'` permission, which allows Copilot to create documentation files without manual approval for each file. Copilot has:
- **Read access**: Can read C# source files in the repository
- **Write access**: Can create/modify markdown files in the output directory
- **Working directory**: Runs from repository root for proper path resolution

**Rate Limiting**: Configurable delay between batches (default: 60 seconds) to avoid hitting GitHub Copilot API rate limits.

## Other Tools

### bin/build_sharpy

Custom build script for the Sharpy compiler. See the main repository README for usage details.
