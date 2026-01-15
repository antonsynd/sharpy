# Build Tools

This directory contains build automation and documentation generation tools for the Sharpy project.

## Overview

The build tools support the development workflow for the Sharpy compiler with:

1. **Automated Implementation**: AI-powered task execution with validation
2. **Code Walkthroughs**: Automated documentation generation
3. **Shared Utilities**: Common logging and configuration
4. **Dogfooding**: Self-improvement tools

## Directory Structure

```
build_tools/
├── sharpy_auto_builder/     # AI-powered automated implementation system
│   ├── README.md            # Usage guide
│   ├── ARCHITECTURE.md      # Technical architecture
│   ├── CONTRIBUTING.md      # Contributor guidelines
│   └── ...                  # Implementation files
├── sharpy_dogfood/          # Self-improvement and meta-tools
├── shared/                  # Shared utilities (logging, config)
├── auto_builder.sh          # Main entry point for auto builder
├── generate_code_walkthroughs.py  # Documentation generator
└── README.md                # This file
```

## Tools

### Sharpy Auto Builder

**Primary automation system** for implementing compiler tasks.

**Key Features**:
- **LangGraph orchestration** with durable persistence (SQLite checkpoints)
- **Multi-backend support** (Claude Code, GitHub Copilot CLI)
- **Native interrupts** for human-in-the-loop review
- **Idempotent tasks** (safe graph replays via `@task` decorator)
- **Long-term memory** (stores successful patterns for reuse)
- **Validation agents** (spec adherence, verification, hallucination defense)
- **Rate limit handling** with automatic failover
- **Session resumption** from any checkpoint

**Quick Start**:
```bash
# Initialize with task list
./auto_builder.sh init --task-list docs/implementation_planning/your_tasks.md

# Run a few tasks
./auto_builder.sh run --max-tasks 3

# Resume from checkpoint (after crash, rate limit, etc.)
./auto_builder.sh run --thread-id sharpy-build-20260114-153045

# View status
./auto_builder.sh status

# Search past implementation patterns
./auto_builder.sh memory search "parser" --namespace implementation
```

**Documentation**:
- [README.md](sharpy_auto_builder/README.md) - Complete usage guide
- [ARCHITECTURE.md](sharpy_auto_builder/ARCHITECTURE.md) - Technical architecture
- [CONTRIBUTING.md](sharpy_auto_builder/CONTRIBUTING.md) - Contributor guide

**Recent Enhancements** (January 2026):
- Phase 1: Durable persistence with SqliteSaver
- Phase 2: Native interrupts for human review
- Phase 3: Idempotent tasks with `@task` decorator
- Phase 4: Long-term memory for pattern storage

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

### Sharpy Dogfood

**Self-improvement tools** for the build tools themselves.

Uses the auto builder to improve the auto builder. See [sharpy_dogfood/README.md](sharpy_dogfood/README.md) for details.

### Shared Utilities

**Common infrastructure** used across all build tools:

- **Logging**: `ExecutionLogger` with consistent JSONL format
- **Configuration**: Shared config base classes
- **Constants**: Project-wide constants

See [shared/README.md](shared/README.md) for details.

## Getting Started

### For Users

If you want to **use the auto builder** to implement tasks:

1. Read [sharpy_auto_builder/QUICK_START.md](sharpy_auto_builder/QUICK_START.md)
2. Run `./auto_builder.sh --help`

### For Contributors

If you want to **extend or modify** the auto builder:

1. Read [sharpy_auto_builder/ARCHITECTURE.md](sharpy_auto_builder/ARCHITECTURE.md)
2. Read [sharpy_auto_builder/CONTRIBUTING.md](sharpy_auto_builder/CONTRIBUTING.md)
3. Run tests: `pytest build_tools/tests/`

## Key Concepts

### Durable Execution

All execution state is persisted to SQLite. You can:
- Stop execution at any time (Ctrl+C)
- Resume from any checkpoint
- Survive crashes and rate limits
- Restart after hours or days

### Human-in-the-Loop

The system uses LangGraph's native `interrupt()` for human interaction:
- Rich-formatted CLI prompts
- Validation with automatic re-prompts
- Full context for decision-making
- No file polling needed

### Learning from Experience

Successful implementations are stored as patterns:
- Semantic search finds similar past work
- Patterns auto-injected into prompts
- Errors documented to avoid repetition
- System improves over time

### Validation & Quality

Three validation agents ensure quality:
- **Spec Adherence**: Matches language specification
- **Verification**: Independent correctness check
- **Hallucination Defense**: Fact-checks .NET/Python claims

## Architecture Overview

```
┌────────────────────────────────────────────────────────────┐
│                   Build Tools Ecosystem                     │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  ┌──────────────────────────────────────────────────────┐  │
│  │            Sharpy Auto Builder                       │  │
│  │  (LangGraph + SQLite + Memory + Interrupts)         │  │
│  └────────────────────┬─────────────────────────────────┘  │
│                       │                                    │
│                       ├──────────────┬──────────────────┐  │
│                       │              │                  │  │
│                       ▼              ▼                  ▼  │
│           ┌───────────────┐  ┌──────────┐  ┌──────────────┐│
│           │  Claude Code  │  │ Copilot  │  │  Validation  ││
│           │     CLI       │  │   CLI    │  │   Agents     ││
│           └───────────────┘  └──────────┘  └──────────────┘│
│                                                            │
│  ┌──────────────────────────────────────────────────────┐  │
│  │            Shared Utilities                          │  │
│  │  (Logging, Config, Constants)                       │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                            │
│  ┌──────────────────────────────────────────────────────┐  │
│  │     Code Walkthrough Generator                       │  │
│  │  (Documentation via AI)                              │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

## Common Workflows

### Automated Implementation

```bash
# Initialize
./auto_builder.sh init --task-list docs/implementation_planning/tasks.md

# Run tasks
./auto_builder.sh run --max-tasks 5

# If interrupted (crash, rate limit):
./auto_builder.sh run --thread-id sharpy-build-20260114-153045
```

### Generate Documentation

```bash
# Generate code walkthroughs
./build_tools/generate_code_walkthroughs.py

# Force regenerate all
./build_tools/generate_code_walkthroughs.py --force
```

### View Progress

```bash
# Check overall status
./auto_builder.sh status

# View execution logs
./auto_builder.sh logs --last 20

# Generate detailed report
./auto_builder.sh report -o status_report.md
```

### Manage Memory

```bash
# Search past implementations
./auto_builder.sh memory search "lexer tokenization"

# View statistics
./auto_builder.sh memory stats

# Clear error patterns
./auto_builder.sh memory clear --namespace errors --confirm
```

## Requirements

- **Python**: 3.9 or higher
- **Git**: For version control operations
- **AI CLI Tools**: At least one of:
  - Claude Code CLI (`claude`) - Recommended
  - GitHub Copilot CLI (`copilot`) - Limited support

## Installation

```bash
# Clone repository
cd build_tools

# Make scripts executable
chmod +x auto_builder.sh
chmod +x generate_code_walkthroughs.py

# First run creates venv and installs dependencies
./auto_builder.sh --help
```

## Testing

```bash
# Run all tests
pytest build_tools/tests/

# Run specific test suite
pytest build_tools/tests/test_orchestrator_persistence.py

# Run with coverage
pytest --cov=sharpy_auto_builder --cov-report=html
```

## Troubleshooting

### Rate Limits

The system handles rate limits automatically:
1. Detects rate limit errors
2. Saves checkpoint
3. Prints resume instructions
4. You resume later with `--thread-id`

### Crashes

Checkpoints save state automatically:
1. Find your thread ID in console output
2. Resume with `./auto_builder.sh run --thread-id <id>`
3. System continues from last checkpoint

### Memory Issues

If pattern storage grows too large:
```bash
# View size
./auto_builder.sh memory stats

# Clear old patterns
./auto_builder.sh memory clear --namespace implementation --confirm
```

### Checkpoint Database Size

If checkpoints consume too much disk:
```bash
# View size
./auto_builder.sh checkpoint-stats

# Clean up old checkpoints
./auto_builder.sh checkpoint-cleanup --keep 20
```

## Contributing

See [sharpy_auto_builder/CONTRIBUTING.md](sharpy_auto_builder/CONTRIBUTING.md) for:
- Development setup
- Code style guide
- Testing guidelines
- Pull request process

## License

Same as the Sharpy project.
