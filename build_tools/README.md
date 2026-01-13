# Build Tools

This directory contains build automation and documentation generation tools for the Sharpy project.

## Scripts

### generate_code_walkthroughs.py

Generates comprehensive code walkthrough documentation for C# source files using AI CLI tools (Claude Code or GitHub Copilot).

**Purpose**: Creates markdown documentation that helps newcomer engineers understand the codebase structure, design patterns, and how to contribute and debug effectively.

**Features**:
- Automatic backend failover: tries Claude Code first, falls back to GitHub Copilot if rate-limited
- Parallel processing with configurable instances (default: 3)
- Incremental updates: only regenerates docs when source files change
- Rate limit detection with automatic wait time extraction
- Execution logging to JSONL for debugging
- Rich context injection (compiler pipeline stage, related specs, dependencies)

**Requirements**:
- Python 3.8+
- At least one of:
  - Claude Code CLI (`claude`) installed
  - GitHub Copilot CLI (`copilot`) installed at `/opt/homebrew/bin/copilot`

**Usage**:
```bash
# Recommended: auto mode with failover (Claude → Copilot)
./build_tools/generate_code_walkthroughs.py

# Use a specific backend
./build_tools/generate_code_walkthroughs.py --cli claude
./build_tools/generate_code_walkthroughs.py --cli copilot

# Force regenerate all docs (ignore timestamps)
./build_tools/generate_code_walkthroughs.py --force

# Custom parallelism and timing
./build_tools/generate_code_walkthroughs.py --parallel 5 --timeout 90 --copilot-timeout 180

# Process specific directories
./build_tools/generate_code_walkthroughs.py --source-dirs src/Sharpy.Compiler
```

**Output**: Generated markdown files are placed in `docs/implementation_walkthrough/` preserving the source directory structure.

**Security Model**: Both CLI tools are restricted to `Read` and `Write` operations only—no shell access, no editing existing files, no file deletion.

**Rate Limiting**: The script detects rate limit errors, extracts wait times from error messages, and can automatically fail over to another backend. A configurable delay between batches (default: 60s) helps avoid hitting limits.

## Other Tools

### bin/build_sharpy

Custom build script for the Sharpy compiler. See the main repository README for usage details.
