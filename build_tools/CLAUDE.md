# build_tools

Python 3.9+ automation tools for the Sharpy compiler. **Not C#** — uses `pytest`, not `dotnet test`.

## Commands

```bash
pytest build_tools/tests/ -v                    # Run all tests
pytest build_tools/tests/test_rate_limiting.py   # Run specific test
python -m build_tools dogfood run --iterations 10  # Dogfood the compiler
python -m build_tools build run --max-tasks 5      # Auto-builder
./build_tools/generate_code_walkthroughs.py        # Generate docs
```

> **PYTHONPATH**: Must include the repo root for `build_tools.shared.*` imports. CI sets this automatically.

## Structure

| Directory | Purpose |
|-----------|---------|
| `sharpy_auto_builder/` | LangGraph-based task orchestration with SQLite persistence |
| `sharpy_dogfood/` | Random code generation → compile → execute → bug reporting |
| `shared/` | Backend abstraction (Claude/Copilot), rate limiting, model selection, logging |
| `tests/` | pytest tests (`asyncio_mode = auto`) |
| `generate_code_walkthroughs.py` | AI-powered C# documentation generator |
| `cli.py` / `__main__.py` | Unified CLI entry point |

## Key Patterns

- **Backend abstraction** — `shared/backends/` provides a single interface for Claude Code + GitHub Copilot CLIs with automatic failover on rate limits
- **Durable persistence** — Auto-builder uses SQLite checkpoints (`.claude/.sharpy_auto_builder/checkpoints/`); resume with `--thread-id`
- **Cost-aware model routing** — `shared/model_selector.py` routes tasks to Haiku/Sonnet/Opus by complexity
- **Two-stage dogfood validation** — Regex pre-check for forbidden features, then compiler validation (saves API calls)

## CI

`.github/workflows/python-build-tools.yml` — Python 3.12, runs `pytest build_tools/tests/ -v` on pushes to `mainline` and PRs.
