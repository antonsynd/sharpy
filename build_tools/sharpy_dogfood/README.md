# Sharpy Dogfooding Tool

A comprehensive testing tool that generates random/novel Sharpy code using AI, validates it against the language specification, compiles it, and verifies the output correctness.

## Overview

The dogfooding tool helps catch compiler bugs by:
1. **Generating** novel Sharpy code using Claude or GitHub Copilot
2. **Validating** the generated code against the language spec (phases 0.1.0-0.1.5)
3. **Compiling** the code using the Sharpy compiler
4. **Executing** the compiled code and capturing output
5. **Verifying** that the output matches expectations (via print statements)
6. **Reporting** any issues found to a dedicated directory

## Usage

```bash
# From the sharpy project root
cd build_tools

# Run with default settings (10 iterations)
python -m sharpy_dogfood

# Run with specific iteration count
python -m sharpy_dogfood --iterations 5

# Specify output directory
python -m sharpy_dogfood --output-dir ./my_dogfood_results

# Dry run to check configuration
python -m sharpy_dogfood --dry-run

# See all options
python -m sharpy_dogfood --help
```

## Output Structure

```
dogfood_output/
├── SUMMARY.md              # Human-readable summary report
├── runs.json               # Machine-readable run data
├── generated/              # Successfully generated/compiled code
└── issues/                 # Issues found during dogfooding
    └── 20260113_143022_compilation_failed_0001/
        ├── README.md       # Human-readable issue summary
        ├── metadata.json   # Issue metadata
        ├── source.spy      # Generated Sharpy code
        ├── generated.cs    # Generated C# (if available)
        ├── compiler_output.txt
        ├── actual_output.txt
        ├── expected_output.txt
        └── error.txt
```

## Issue Types

The tool detects several types of issues:

| Type | Description |
|------|-------------|
| `generation_failed` | AI failed to generate code |
| `validation_failed` | Generated code doesn't match Sharpy spec |
| `compilation_failed` | Sharpy compiler failed to compile the code |
| `execution_failed` | Compiled code crashed at runtime |
| `output_mismatch` | Output differs from expected |
| `timeout` | Operation exceeded time limit |

## Features Tested

The tool tests features from implementation phases 0.1.0 through 0.1.5:

- **0.1.0** - Lexer: tokens, keywords, operators, indentation
- **0.1.1** - Parser: AST, expressions, literals
- **0.1.2** - Code Generation: entry point, primitive types
- **0.1.3** - Variables: declarations, assignments, operators
- **0.1.4** - Control Flow: if/elif/else, for, while
- **0.1.5** - Functions: definitions, positional parameters, return

### Allowed Features (Strict)

✅ Variables: `x: int = 42`, `x = 42` (inference)
✅ Types: `int`, `str`, `bool`, `float` only
✅ Operators: `+`, `-`, `*`, `/`, `//`, `%`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `and`, `or`, `not`
✅ Control flow: `if/elif/else`, `while`, `for i in range(...)`
✅ Functions: `def name(param: type) -> return_type:`
✅ Built-ins: `print()`, `range()`

### Forbidden Features (Not Yet Implemented)

❌ f-strings, string concatenation
❌ Default parameters, keyword arguments
❌ Classes, structs, interfaces
❌ Lists, dicts, sets
❌ Imports, exceptions, lambdas
❌ Nullable types, type aliases

## Validation

Generated code goes through two validation stages:

1. **Quick Pre-validation** - Regex-based check for obviously forbidden features
2. **AI Spec Validation** - Detailed check against the language specification

Only code that passes both stages is compiled, ensuring we only test actual compiler bugs.

## Requirements

- Python 3.10+
- .NET 9 SDK (for the Sharpy compiler)
- One of:
  - Claude Code CLI (`claude`)
  - GitHub Copilot CLI (`copilot`)

## Configuration

The tool can be configured via command line arguments or by modifying `config.py`:

```python
@dataclass
class Config:
    max_iterations: int = 10
    generation_timeout: float = 180.0  # 3 minutes
    compilation_timeout: float = 60.0  # 1 minute
    execution_timeout: float = 30.0    # 30 seconds
```

## Rate Limiting

The tool includes sophisticated rate limiting to handle AI backend quotas:

- Tracks requests per time window
- Automatic backoff on rate limit errors
- Failover between backends (Claude → Copilot)
- Extracts wait times from error messages

## Design

The tool follows patterns established in the existing build tools:

- `backends.py` - AI backend abstraction with rate limiting
- `compiler.py` - Sharpy compiler interface
- `orchestrator.py` - Main coordination logic with pre-validation
- `prompts.py` - AI prompt templates (strict feature lists)
- `reporting.py` - Issue and summary reporting
- `cli.py` - Command-line interface

## Examples

### Running a Quick Test

```bash
python -m sharpy_dogfood --iterations 3 --dry-run
```

### Full Dogfooding Session

```bash
python -m sharpy_dogfood --iterations 50 --output-dir ./dogfood_$(date +%Y%m%d)
```

### Analyzing Issues

After running, check the issues directory:

```bash
# List all issues
ls -la dogfood_output/issues/

# Read an issue summary
cat dogfood_output/issues/*/README.md

# View the problematic Sharpy code
cat dogfood_output/issues/*/source.spy
```

## Contributing

When adding new features:

1. Add the feature to the appropriate phase in `FEATURE_FOCUSES` in `orchestrator.py`
2. Update the allowed/forbidden lists in `prompts.py`
3. Update the `_quick_prevalidate()` patterns in `orchestrator.py`
4. Add any new issue types to `reporting.py`
5. Update tests (when test infrastructure is added)
