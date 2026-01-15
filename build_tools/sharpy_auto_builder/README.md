# Sharpy Auto Builder

Automated implementation of Sharpy compiler tasks using Claude Code or GitHub Copilot CLI, with LangGraph-powered orchestration, validation agents, and human-in-the-loop support.

## Recent Changes

### January 2026 - LangGraph Enhancements (Phases 1-4)

**Major architectural improvements** for production-ready automation:

#### Phase 1: Durable Persistence ✅
- **SqliteSaver checkpointer**: Replaces in-memory state with SQLite for true durability
- **Session resumption**: Resume from any checkpoint using `--thread-id`
- **Checkpoint management**: Automatic cleanup, statistics, and manual controls
- **Rate limit recovery**: Sessions pause on rate limits and can be resumed later

```bash
# Start a session (gets auto-generated thread ID)
./auto_builder.sh run --max-tasks 5

# Resume after interruption
./auto_builder.sh run --thread-id sharpy-build-20260114-153045

# List all saved sessions
./auto_builder.sh run --list-sessions

# View checkpoint statistics
./auto_builder.sh checkpoint-stats

# Clean up old checkpoints
./auto_builder.sh checkpoint-cleanup --thread-id <id> --keep 20
```

#### Phase 2: Native Interrupts ✅
- **LangGraph `interrupt()`**: Replaces file-based polling with native interrupts
- **Interactive CLI prompts**: Rich-formatted review requests and questions
- **Validation loops**: Invalid inputs trigger re-prompts automatically
- **Deprecation**: Old `HumanLoopManager` kept for backwards compatibility

```bash
# Run with interactive interrupts (default mode)
./auto_builder.sh run

# When interrupted, the CLI shows a rich prompt and waits for your response
```

#### Phase 3: Idempotent Tasks ✅
- **`@task` decorator**: Claude CLI and test executions are now idempotent
- **Graph replay safety**: Same inputs return cached results during replays
- **Fallback caching**: File-based cache for environments where @task doesn't work
- **Consistent results**: No duplicate API calls during checkpoint resumption

#### Phase 4: Long-Term Memory ✅
- **Pattern storage**: Successful implementations and errors stored for reuse
- **Semantic search**: Optional embedding-based search (OpenAI or local)
- **Context injection**: Past patterns automatically injected into prompts
- **CLI memory tools**: Search, view stats, and manage stored patterns

```bash
# Search for past implementation patterns
./auto_builder.sh memory search "parser implementation" --namespace implementation

# View memory statistics
./auto_builder.sh memory stats

# Clear specific namespace
./auto_builder.sh memory clear --namespace errors --confirm
```

**Configuration:**
```python
from sharpy_auto_builder import Config

config = Config(
    # Checkpoint settings
    checkpoint=CheckpointConfig(
        max_checkpoints_per_thread=100,
        cleanup_interval=50,
    ),

    # Memory settings
    memory=MemoryConfig(
        enabled=True,
        embedding_provider="openai",  # or "local" or None
        max_patterns_per_query=5,
    ),
)
```

### January 2026 - Test Timeout & Infinite Loop Detection

**Critical addition**: Tests now have a configurable timeout (default: 5 minutes) to detect infinite loops.

- **New config**: `test_timeout` (default: 300 seconds) - Maximum time allowed for test execution
- **Infinite loop detection**: If tests timeout, the system detects whether it's a pre-existing issue or introduced by the agent
- **Specialized fix prompts**: When an infinite loop is detected, the agent receives targeted guidance
- **Execution result tracking**: `ExecutionResult.timed_out` flag indicates timeout occurred

Configuration:
```python
Config(
    test_timeout=300.0,  # 5 minutes (adjust for your test suite)
)
```

## Documentation Navigation

- **New to Auto Builder?** → Start with [QUICK_START.md](QUICK_START.md)
- **Need reference docs?** → This README (you're here!)
- **Want technical details?** → See [ARCHITECTURE.md](ARCHITECTURE.md)
- **Planning to contribute?** → Read [CONTRIBUTING.md](CONTRIBUTING.md)

## Overview

The Sharpy Auto Builder is a production-ready automation system that orchestrates the implementation of compiler tasks using AI coding assistants. Built on LangGraph with comprehensive durability and learning features.

### Core Capabilities

- **Durable Persistence**: SQLite-backed checkpoints survive crashes and rate limits
- **Session Resumption**: Resume from any checkpoint with `--thread-id`
- **Multi-backend Support**: Claude Code (primary), GitHub Copilot CLI (fallback)
- **Native Interrupts**: Interactive human review with rich CLI prompts
- **Idempotent Execution**: `@task` decorator ensures safe graph replays
- **Long-Term Memory**: Stores successful patterns for reuse
- **Validation Agents**: Spec adherence, verification, and hallucination defense
- **Rate Limit Handling**: Automatic detection, pause, and resumption
- **LangGraph State Machine**: Robust orchestration with 15+ nodes

## Architecture

### High-Level Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                        Sharpy Auto Builder                           │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────┐    ┌──────────────────┐    ┌──────────────────┐   │
│  │   CLI       │───▶│   Orchestrator   │───▶│ Backend Manager  │   │
│  │ (Interrupt  │    │   (LangGraph     │    │  (Task-wrapped   │   │
│  │  Handler)   │    │   State Machine) │    │   CLI calls)     │   │
│  └─────────────┘    └──────────────────┘    └──────────────────┘   │
│         │                    │                        │              │
│         │                    │                  ┌─────┴─────┐        │
│         │                    │                  │           │        │
│         │                    ▼                  ▼           ▼        │
│         │            ┌──────────────┐    ┌─────────┐ ┌─────────┐    │
│         └───────────▶│ Native       │    │ Claude  │ │ Copilot │    │
│                      │ Interrupts   │    │ Code    │ │ CLI     │    │
│                      └──────────────┘    └─────────┘ └─────────┘    │
│                             │                                        │
│                             │                                        │
│  ┌──────────────────────────┴───────────────────────────┐           │
│  │                                                       │           │
│  ▼                    ▼                    ▼             ▼           │
│ ┌────────────┐  ┌──────────┐  ┌─────────────┐  ┌──────────────┐    │
│ │ SQLite     │  │ Memory   │  │ Checkpoints │  │ Task Cache   │    │
│ │ Checkpoint │  │ Store    │  │ (Durable    │  │ (Fallback    │    │
│ │ Database   │  │(Patterns)│  │  State)     │  │ Idempotency) │    │
│ └────────────┘  └──────────┘  └─────────────┘  └──────────────┘    │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

### LangGraph State Machine (Detailed)

The orchestrator uses a sophisticated LangGraph state machine with the following key components:

**Core Nodes:**
- `select_task` - Choose next pending task
- `plan_implementation` - Generate implementation plan
- `run_baseline_tests` - Establish test baseline (idempotent via @task)
- `execute_implementation` - Run implementation (idempotent via @task)
- `analyze_response` - Overseer: Analyze AI response quality
- `handle_auto_decision` - Overseer: Make automatic decisions
- `run_tests` - Verify implementation (idempotent via @task)
- `fix_test_failures` - Fix broken tests
- `validate_spec_adherence` - Check spec compliance
- `validate_verification` - Independent verification
- `check_hallucinations` - Fact-check claims
- `address_validation_issues` - Fix validation problems
- `request_human_review` - Native interrupt for human review
- `update_ground_truth` - Persist results + store memory patterns
- `commit_changes` - Git commit
- `handle_error` - Error handling + rate limit detection

**Key Features:**
- **Checkpointing**: Every state transition is persisted to SQLite
- **Interrupts**: `request_human_review` uses LangGraph's native `interrupt()`
- **Idempotency**: CLI calls wrapped with `@task` decorator for replay safety
- **Memory**: Successful patterns stored in memory store for context injection
- **Routing**: Conditional edges based on state (tests pass/fail, validation results, etc.)

## Quick Start

### 1. Install Dependencies

```bash
cd build_tools
chmod +x auto_builder.sh

# The script will create a virtual environment and install dependencies
./auto_builder.sh --help
```

Or manually:

```bash
cd build_tools/sharpy_auto_builder
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

### 2. Initialize

```bash
# Initialize with a task list (required)
./auto_builder.sh init --task-list docs/implementation_planning/task_list_0.1.0_to_0.1.5.md

# Or load from a config file
./auto_builder.sh init --config my_config.json
```

This parses the task list and creates the ground truth state file.

### 3. Check Status

```bash
./auto_builder.sh status
```

### 4. Run Implementation

```bash
# Run a few tasks
./auto_builder.sh run --max-tasks 5

# Run with specific backend
./auto_builder.sh run --backend claude_code --max-tasks 3

# Run with a specific model (default: claude-sonnet-4-5-20250929)
./auto_builder.sh run --model claude-sonnet-4-5-20250929

# Skip validation steps for faster iteration
./auto_builder.sh run --skip-spec-check --skip-verification --skip-hallucination-check
```

## Commands

### `init`

Initialize the auto builder by parsing the task list and creating ground truth.

```bash
./auto_builder.sh init [OPTIONS]

Options:
  --config, -c PATH       Load settings from a config.json file
  --task-list, -t PATH    Path to task list markdown file (required if not in config)
```

Examples:
```bash
# Initialize with task list
./auto_builder.sh init --task-list docs/implementation_planning/task_list_remediation.md

# Initialize from config file
./auto_builder.sh init --config ./my_config.json

# Config file with task-list override
./auto_builder.sh init --config ./my_config.json --task-list ./different_tasks.md
```

### `status`

Show current progress, pending tasks, and human interactions.

```bash
./auto_builder.sh status
```

### `run`

Execute the orchestrator to process tasks. Supports session resumption and automatic checkpoint creation.

```bash
./auto_builder.sh run [OPTIONS]

Options:
  --max-tasks N           Maximum tasks to process
  --backend BACKEND       Use only this backend (claude_code or copilot)
  --model MODEL           Model to use (default: claude-sonnet-4-5-20250929)
  --skip-spec-check       Skip spec adherence validation
  --skip-verification     Skip verification validation
  --skip-hallucination-check  Skip hallucination defense
  --no-human-approval     Don't require human approval for critical tasks
  --thread-id ID          Resume a previous session (from checkpoint)
  --list-sessions         List all saved sessions and exit
```

Examples:
```bash
# Start new session (auto-generates thread ID)
./auto_builder.sh run --max-tasks 3

# Resume from checkpoint (after rate limit, crash, or manual stop)
./auto_builder.sh run --thread-id sharpy-build-20260114-153045

# List all available sessions to resume
./auto_builder.sh run --list-sessions

# Fast iteration (skip validation)
./auto_builder.sh run --skip-spec-check --skip-verification --max-tasks 1
```

### `report`

Generate a detailed markdown status report.

```bash
./auto_builder.sh report -o status_report.md
```

### `answer`

Submit an answer to a pending question.

```bash
./auto_builder.sh answer QUESTION_ID "Your answer here" --notes "Additional context"
```

### `review`

Submit a review response for pending reviews.

```bash
./auto_builder.sh review REVIEW_ID approved --notes "Looks good"
./auto_builder.sh review REVIEW_ID rejected --notes "See issue with X"
./auto_builder.sh review REVIEW_ID needs_changes --notes "Fix Y first"
```

### `reset`

Reset a task back to pending status.

```bash
./auto_builder.sh reset 0.1.0.3
```

### `skip`

Skip a task.

```bash
./auto_builder.sh skip 0.1.0.3 --reason "Blocked by external dependency"
```

### `logs`

View execution logs (prompts, responses, test results).

```bash
# View last 10 log entries
./auto_builder.sh logs --last 10

# View logs for a specific task
./auto_builder.sh logs --task-id 0.1.0.3

# Filter by event type
./auto_builder.sh logs --event-type agent_response

# Include full prompts and truncate output
./auto_builder.sh logs --show-prompt --truncate 500
```

Event types: `agent_prompt`, `agent_response`, `test_run`, `baseline_test_run`, `fix_prompt`, `fix_response`, `followup_task_created`

### `checkpoint-stats`

Show checkpoint storage statistics.

```bash
./auto_builder.sh checkpoint-stats
```

Displays:
- Database size and location
- Total checkpoints stored
- Number of unique threads
- Per-thread checkpoint counts
- Cleanup configuration

### `checkpoint-cleanup`

Clean up old checkpoints to save disk space.

```bash
./auto_builder.sh checkpoint-cleanup [OPTIONS]

Options:
  --thread-id ID    Clean up only this thread's checkpoints
  --keep N          Number of checkpoints to keep per thread (default: 10)
  --dry-run         Show what would be deleted without deleting
```

Examples:
```bash
# Clean all threads, keeping 20 most recent checkpoints each
./auto_builder.sh checkpoint-cleanup --keep 20

# Preview cleanup for specific thread
./auto_builder.sh checkpoint-cleanup --thread-id sharpy-build-20260114-153045 --dry-run

# Clean specific thread, keep last 5 checkpoints
./auto_builder.sh checkpoint-cleanup --thread-id sharpy-build-20260114-153045 --keep 5
```

### `memory search`

Search for patterns in the memory store.

```bash
./auto_builder.sh memory search [OPTIONS] QUERY

Options:
  --namespace, -n NAMESPACE   Limit search to: implementation, errors, codebase, spec
  --limit, -l N              Maximum results to return (default: 10)
```

Examples:
```bash
# Search all patterns for "parser"
./auto_builder.sh memory search "parser implementation"

# Search only implementation patterns
./auto_builder.sh memory search "lexer" --namespace implementation

# Get top 20 error patterns about type checking
./auto_builder.sh memory search "type error" --namespace errors --limit 20
```

### `memory stats`

Show memory store statistics.

```bash
./auto_builder.sh memory stats [OPTIONS]

Options:
  --namespace, -n NAMESPACE   Show stats for specific namespace only
```

Displays:
- Database size and location
- Pattern counts by namespace
- Embedding configuration
- Storage limits

### `memory clear`

Clear patterns from the memory store.

```bash
./auto_builder.sh memory clear [OPTIONS]

Options:
  --namespace, -n NAMESPACE   Clear only this namespace
  --confirm                  Confirm deletion (required)
```

Examples:
```bash
# Clear all error patterns (requires --confirm)
./auto_builder.sh memory clear --namespace errors --confirm

# Clear entire memory store (requires --confirm)
./auto_builder.sh memory clear --confirm
```

## Human-in-the-Loop

### Questions

When the system encounters something that needs human input, it creates a question file in `state/questions/`. Each question has:

- A JSON file with the question data
- A markdown file for human-readable format

To answer:

1. Check pending questions: `./auto_builder.sh status`
2. Read the question markdown file
3. Create an answer file in `state/answers/` with the same ID

Answer file format:
```json
{
  "answer": "Your answer here",
  "notes": "Optional explanation"
}
```

Or use the CLI:
```bash
./auto_builder.sh answer q_20250111_abc123 "Use approach B" --notes "Better performance"
```

### Reviews

When a critical task needs human review, it creates a review request in `state/human_review/`. Each review has:

- A JSON file with full details
- A markdown file for human-readable format

To respond:

1. Check pending reviews: `./auto_builder.sh status`
2. Read the review markdown file
3. Create a response file: `{review_id}_response.json`

Response file format:
```json
{
  "status": "approved",  // or "rejected" or "needs_changes"
  "notes": "Your review notes"
}
```

Or use the CLI:
```bash
./auto_builder.sh review r_20250111_def456 approved --notes "LGTM"
```

## Validation Agents

The system uses three validation agents (matching `.github/agents/`):

### Spec Adherence Agent

Verifies implementation matches the language specification.

- Compares implementation against `docs/language_specification/`
- Reports compliant items and deviations
- Provides spec citations

### Verification Expert

Independent verification of correctness.

- Runs tests and reports results
- Validates behavior against expected outcomes
- Checks for regressions

### Hallucination Defense

Fact-checks claims about .NET, Roslyn, and Python.

- Verifies .NET API behavior
- Checks C# 9.0 compatibility (Unity constraint)
- Validates Python semantic claims

## Rate Limiting

The system handles rate limiting automatically:

1. Tracks requests per time window
2. Implements cooldown between requests
3. Exponential backoff on errors
4. Automatic failover to alternate backend

Configure in `config.py`:

```python
RateLimitConfig(
    max_requests_per_window=50,  # requests per window
    window_seconds=3600,          # 1 hour window
    request_cooldown=3.0,         # seconds between requests
    max_consecutive_errors=3,     # errors before failover
    backoff_multiplier=2.0,       # exponential backoff
    max_backoff=300.0,            # max 5 minute backoff
)
```

## Execution Workflow

The orchestrator runs a sophisticated workflow for each task:

```
select_task → plan_implementation → run_baseline_tests → execute_implementation
     ↑                                                          ↓
     │                                                    run_tests
     │                                                          ↓
     │    ┌──────────────────────────────────────────────────────┤
     │    │                                                      │
     │    ▼                                                      ▼
     │  validate (if tests pass)              fix_test_failures (if agent broke tests)
     │    │                                           │
     │    ▼                                           │ (max 3 attempts)
     │  human_review (if needed)                      │
     │    │                                           ▼
     └────┴───────────── update_ground_truth ◄───────┘
```

### Baseline Test Tracking

Before executing a task, the system runs tests to establish a baseline:
- If tests **already fail**: Implementation proceeds, and post-implementation failures are attributed to pre-existing issues (not the agent's fault)
- If tests **pass**: Any subsequent failures are treated as regressions the agent must fix

### Test Fix Workflow

When an agent breaks tests (tests passed before, fail after):

1. Agent gets up to **3 attempts** to fix the issue (configurable via `max_test_fix_attempts`)
2. Prompt includes the test failure output and instructions to fix **implementation, not tests**
3. If all fix attempts fail, a **follow-up task** is automatically created
4. The original task is marked with a note linking to the follow-up

### Execution Logging

All prompts, responses, and test results are logged to `state/execution_log.jsonl`:

```jsonl
{"timestamp": "2025-01-11T...", "event_type": "agent_prompt", "task_id": "0.1.0.3", "prompt": "..."}
{"timestamp": "2025-01-11T...", "event_type": "agent_response", "task_id": "0.1.0.3", "output": "...", "success": true}
{"timestamp": "2025-01-11T...", "event_type": "test_run", "task_id": "0.1.0.3", "success": false}
```

Use `./auto_builder.sh logs` to view and filter these logs.

## Ground Truth State

The ground truth file (`state/ground_truth.json`) tracks:

- All phases and tasks
- Task status (pending, in_progress, completed, failed, etc.)
- Execution attempts and results
- Validation results
- Human questions and answers
- Overall statistics

## File Structure

```
build_tools/
├── auto_builder.sh               # Shell wrapper script
└── sharpy_auto_builder/
    ├── __init__.py               # Package exports
    ├── config.py                 # Configuration management
    ├── state.py                  # Ground truth state management
    ├── agents.py                 # Agent definitions and prompts
    ├── backends.py               # Backend implementations (Claude Code, Copilot)
    ├── tasks.py                  # @task-wrapped idempotent functions (NEW)
    ├── memory.py                 # Memory pattern storage (NEW)
    ├── interrupt_handler.py      # CLI interrupt display (NEW)
    ├── human_loop.py             # Legacy file-based human loop (DEPRECATED)
    ├── orchestrator.py           # LangGraph state machine (3300+ lines)
    ├── response_analyzer.py      # Overseer: Response quality analysis
    ├── auto_decision.py          # Overseer: Automatic decision making
    ├── cli.py                    # Command-line interface (1400+ lines)
    ├── run.py                    # Main entry point
    ├── requirements.txt          # Python dependencies
    ├── README.md                 # This file
    ├── ARCHITECTURE.md           # Technical architecture documentation (NEW)
    ├── CONTRIBUTING.md           # Contributor guidelines (NEW)
    ├── QUICK_START.md            # Quick start guide (NEW)
    └── state/                    # Runtime state (created on init)
        ├── config.json
        ├── ground_truth.json
        ├── execution_log.jsonl              # Full prompt/response logs
        ├── orchestrator_checkpoints.db      # SQLite checkpoints (NEW)
        ├── memory_store.db                  # Pattern storage (NEW)
        ├── .task_cache/                     # Fallback idempotency cache (NEW)
        ├── questions/                       # Pending questions (LEGACY)
        ├── answers/                         # Human answers (LEGACY)
        └── human_review/                    # Review requests (LEGACY)
```

## Configuration Options

Key settings in `config.py`:

| Setting | Default | Description |
|---------|---------|-------------|
| `max_retries_per_task` | 3 | Max execution attempts per task |
| `max_test_fix_attempts` | 3 | Max attempts to fix broken tests |
| `test_timeout` | 300s | Timeout for test execution (catches infinite loops) |
| `create_followup_task_on_fix_failure` | true | Create follow-up task when test fixes fail |
| `require_human_approval_for_critical` | true | Require human review for critical tasks |
| `human_wait_timeout` | 3600s | Timeout waiting for human input |
| `run_spec_adherence_check` | true | Run spec validation agent |
| `run_verification_after_implementation` | true | Run verification agent |
| `run_hallucination_defense` | true | Run hallucination check agent |

## Integration with Agents

The auto builder uses the same agent definitions as `.github/agents/`:

| Role | Purpose |
|------|---------|
| Implementer | Full implementation of tasks |
| Lexer Expert | Tokenization code |
| Parser Expert | AST construction |
| Semantic Expert | Type checking |
| CodeGen Expert | C# emission |
| Test Expert | xUnit tests |
| Spec Adherence | Verify spec compliance |
| Verification Expert | Test verification |
| Hallucination Defense | Fact checking |

## Documentation

- **[QUICK_START.md](QUICK_START.md)** - Get started in 5 minutes
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Deep dive into system architecture
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Guidelines for contributors
- **[Implementation Plans](../../docs/implementation_planning/)** - Detailed feature specifications

## Tips

1. **Start small**: Use `--max-tasks 1` to test the system
2. **Monitor progress**: Run `status` frequently
3. **Use checkpoints**: Don't worry about crashes - resume with `--thread-id`
4. **Review carefully**: Don't approve reviews without reading them
5. **Leverage memory**: Past patterns are auto-injected into prompts
6. **Switch backends**: If one is rate-limited, use `--backend` to switch
7. **Reset on failure**: Use `reset` to retry failed tasks
8. **Check logs**: Use `./auto_builder.sh logs` for detailed history
9. **Skip validation for speed**: Use `--skip-*` flags when iterating
10. **Clean up**: Run `checkpoint-cleanup` and `memory clear` periodically

## Troubleshooting

### "Backend not available"

All backends are rate-limited. Wait or check `status` for cooldown times.

### "Ground truth not found"

Run `init` first to create the ground truth file.

### "Task blocked"

A task has dependencies that aren't completed. Check `status` for blocking tasks.

### Rate limit errors

The system handles these automatically with backoff. If persistent, wait 15-30 minutes.

### Agent keeps breaking tests

The system tracks baseline test state and will:
1. Give the agent up to 3 fix attempts
2. Create a follow-up task if fixes fail
3. Not blame the agent for pre-existing test failures

Check `./auto_builder.sh logs --event-type fix_response` to see what fixes were attempted.

### Tests timing out / Infinite loops

If tests timeout (default: 5 minutes), the system will:
1. Kill the test process
2. Detect if the timeout is pre-existing or agent-introduced
3. Provide the agent with specialized infinite loop debugging guidance
4. Track `timed_out: true` in execution logs

To adjust the timeout for slower test suites, modify `test_timeout` in config:
```python
Config(test_timeout=600.0)  # 10 minutes
```

Check `./auto_builder.sh logs --event-type test_run` and look for `timed_out: true` entries.

### GitHub Copilot CLI limitations

The `gh copilot` CLI is designed for interactive shell command suggestions, not arbitrary code implementation. It's disabled by default. To enable it for experimentation:

```bash
./auto_builder.sh run --backend copilot
```

Note: Results will be limited. For full code implementation, use Claude Code.

## Development & Contributing

### Architecture Deep Dive

The Sharpy Auto Builder is built on several key architectural principles:

#### 1. **Durable State Management (Phase 1)**

All execution state is persisted to SQLite using LangGraph's `SqliteSaver`:

- **Checkpoint Database**: `state/orchestrator_checkpoints.db`
- **Automatic Checkpointing**: Every node transition creates a checkpoint
- **Thread-based Sessions**: Each run has a unique thread ID
- **Resumption**: Restart from any checkpoint, preserving full state
- **Cleanup**: Automatic removal of old checkpoints (configurable)

**Implementation**: `orchestrator.py` lines 175-179, 3276-3294

#### 2. **Native Interrupts (Phase 2)**

Human-in-the-loop interactions use LangGraph's `interrupt()` function:

- **interrupt()**: Pauses graph execution, returns control to CLI
- **Rich Prompts**: Interactive CLI with formatted review requests
- **Validation**: Invalid responses trigger re-prompts automatically
- **Command(resume=...)**: Resumes execution with user's response

**Implementation**: `orchestrator.py` lines 2443-2570, `interrupt_handler.py`

#### 3. **Idempotent Execution (Phase 3)**

All side-effecting operations use the `@task` decorator:

- **@task Decorator**: LangGraph ensures same inputs → same outputs
- **Graph Replays**: Checkpoints can be replayed without duplicate API calls
- **Fallback Cache**: File-based cache when `@task` doesn't work
- **Input Hashing**: SHA256 hash of all parameters for cache keys

**Implementation**: `tasks.py` (execute_claude_cli, execute_copilot_cli, run_tests)

#### 4. **Long-Term Memory (Phase 4)**

Successful patterns are stored for future reuse:

- **Pattern Storage**: Implementation and error patterns in namespaces
- **Semantic Search**: Optional embeddings (OpenAI or local)
- **Context Injection**: Past patterns added to implementation prompts
- **Learning**: System improves over time by reusing what works

**Implementation**: `memory.py`, `orchestrator.py` lines 794-805, 2819-2852

### Code Organization

```
sharpy_auto_builder/
├── __init__.py              # Package exports
├── config.py                # Configuration (CheckpointConfig, MemoryConfig, etc.)
├── state.py                 # Ground truth state management (Task, Phase, etc.)
├── agents.py                # Agent role definitions and prompts
├── backends.py              # Backend implementations (rate limiting, execution)
├── tasks.py                 # @task-wrapped idempotent functions (NEW)
├── memory.py                # Memory store pattern management (NEW)
├── interrupt_handler.py     # CLI interrupt display and collection (NEW)
├── human_loop.py            # Legacy file-based human loop (DEPRECATED)
├── orchestrator.py          # LangGraph state machine (3300+ lines)
│   ├── __init__             # Initialize checkpointer, memory store
│   ├── _build_graph         # Build LangGraph state machine
│   ├── _create_memory_store # Configure InMemoryStore with embeddings
│   ├── Node methods         # _select_task_node, _execute_implementation_node, etc.
│   ├── Routing methods      # _route_after_tests, _route_after_validation, etc.
│   ├── Memory methods       # _extract_solution_summary, _categorize_error
│   ├── Interrupt methods    # _request_human_review_node, _ask_human_question
│   └── Checkpoint methods   # _setup_checkpoint_cleanup, get_checkpoint_stats
├── response_analyzer.py     # Overseer: Analyze AI response quality
├── auto_decision.py         # Overseer: Make automatic decisions
├── cli.py                   # Command-line interface (1400+ lines)
│   ├── run_with_interrupts  # Main interrupt loop
│   ├── cmd_*                # Command implementations
│   └── main                 # Argument parsing
└── run.py                   # Entry point

state/                       # Runtime state (created on init)
├── config.json              # Active configuration
├── ground_truth.json        # Task state and history
├── execution_log.jsonl      # Full prompt/response logs
├── orchestrator_checkpoints.db   # SQLite checkpoints (NEW)
├── memory_store.db          # Pattern storage (NEW)
├── .task_cache/             # Fallback idempotency cache (NEW)
├── questions/               # Pending questions
├── answers/                 # Human answers
└── human_review/            # Review requests
```

### Adding New Features

#### Add a New Agent Type

1. Add to `AgentRole` enum in `agents.py`
2. Add to `AGENT_CONFIGS` with prompt template
3. Update `get_specialist_for_task()` logic

```python
# agents.py
class AgentRole(str, Enum):
    YOUR_NEW_AGENT = "your_new_agent"

AGENT_CONFIGS = {
    AgentRole.YOUR_NEW_AGENT: {
        "name": "Your New Agent",
        "role": "Description of role",
        "tools": ["Read", "Write"],
        "prompt_template": "Template string..."
    }
}
```

#### Add a New Backend

1. Implement `Backend` interface in `backends.py`
2. Add to `BackendType` enum
3. Add backend configuration in `config.py`
4. Update `BackendManager.__init__()` to register it

```python
# backends.py
class YourBackend(Backend):
    def __init__(self, config: BackendConfig):
        super().__init__(config)

    async def execute(self, prompt: str, tools: list[str]) -> ExecutionResult:
        # Your implementation
        pass
```

#### Add a New LangGraph Node

1. Add node method to `Orchestrator` class: `_your_node(state) -> OrchestratorState`
2. Register in `_build_graph()`: `graph.add_node("your_node", self._your_node)`
3. Add routing logic (edges) to/from your node
4. Update state typing if needed

```python
# orchestrator.py
async def _your_new_node(self, state: OrchestratorState) -> OrchestratorState:
    """Your node description."""
    task_data = state["current_task"]

    # Your logic here

    return {
        **state,
        "next_action": "next_step",
        "messages": ["Node completed"]
    }

def _build_graph(self):
    # ...
    graph.add_node("your_node", self._your_new_node)
    graph.add_edge("previous_node", "your_node")
    graph.add_edge("your_node", "next_node")
```

#### Add a New CLI Command

1. Add command parser in `cli.py` `main()` function
2. Add command implementation function: `def cmd_your_command(args):`
3. Register in `commands` dictionary

```python
# cli.py
def cmd_your_command(args):
    """Your command description."""
    config = Config()
    # Your implementation
    print("Command executed")

def main():
    # Add parser
    your_parser = subparsers.add_parser("your-command", help="Help text")
    your_parser.add_argument("--option", help="Option help")

    # Register command
    commands = {
        # ...
        "your-command": cmd_your_command,
    }
```

### Testing

The system includes comprehensive test suites:

```bash
# Run all tests
pytest build_tools/tests/

# Run specific test suites
pytest build_tools/tests/test_orchestrator_persistence.py   # Phase 1
pytest build_tools/tests/test_orchestrator_interrupts.py    # Phase 2
pytest build_tools/tests/test_tasks.py                      # Phase 3
pytest build_tools/tests/test_memory.py                     # Phase 4

# Run with coverage
pytest --cov=sharpy_auto_builder --cov-report=html
```

### Key Design Patterns

#### 1. **Idempotent State Updates**

All node functions must be idempotent (safe to replay):

```python
async def _my_node(self, state: OrchestratorState) -> OrchestratorState:
    # ✅ GOOD: Read-only operations before interrupt
    task_data = state["current_task"]
    files = self._get_files_changed()  # Idempotent

    # ✅ GOOD: Interrupt pauses here
    response = interrupt({"type": "review", "files": files})

    # ✅ GOOD: State updates after interrupt
    return {**state, "human_response": response}
```

```python
# ❌ BAD: Side effects before interrupt
async def _bad_node(self, state: OrchestratorState) -> OrchestratorState:
    self._send_notification()  # Side effect!
    response = interrupt({"type": "review"})
    return {**state, "response": response}
```

#### 2. **Memory Pattern Storage**

Always wrap pattern storage in try/except (don't fail tasks on memory errors):

```python
try:
    self.memory_manager.store_implementation_pattern(
        task_type="component_creation",
        description=task_desc,
        solution=summary,
        files=files,
        tags=["success"],
        task_id=task_id,
    )
except Exception as e:
    print(f"Warning: Failed to store pattern: {e}")
    # Continue execution
```

#### 3. **Interrupt Validation**

Use validation functions to ensure correct responses:

```python
def validate_review_response(response: dict) -> tuple[bool, Optional[str]]:
    if not isinstance(response, dict):
        return False, "Response must be a dictionary"

    if "approved" not in response and "retry" not in response:
        return False, "Response must include 'approved' or 'retry'"

    return True, None

# Use with interrupt_with_validation
response = self._interrupt_with_validation(
    payload=review_payload,
    validator=validate_review_response,
    max_attempts=3
)
```

### Performance Considerations

1. **Checkpoint Size**: Large states increase checkpoint database size
   - Configure `max_checkpoints_per_thread` appropriately
   - Run cleanup periodically

2. **Memory Patterns**: Patterns accumulate over time
   - Use `max_patterns_stored` limit
   - Truncate long values with `max_pattern_length`
   - Clear old namespaces periodically

3. **Task Caching**: Fallback cache files can accumulate
   - Located in `.task_cache/`
   - Safe to delete (will regenerate on next run)

4. **Embeddings**: Semantic search adds overhead
   - Use `embedding_provider: None` for exact matching
   - Local embeddings (`sentence-transformers`) are slower than OpenAI
   - Consider disabling for small pattern sets

### Debugging Tips

1. **Check execution logs**: `./auto_builder.sh logs --last 20`
2. **Inspect checkpoints**: `./auto_builder.sh checkpoint-stats`
3. **View stored patterns**: `./auto_builder.sh memory search ""`
4. **Examine ground truth**: `cat state/ground_truth.json | jq .`
5. **Enable verbose logging**: Set `logging.basicConfig(level=logging.DEBUG)`

### Contributing Guidelines

1. **Follow existing patterns**: Use `@task` for side effects, wrap memory in try/except
2. **Write tests**: Add tests to appropriate test suite (test_*.py)
3. **Update documentation**: Update this README with new features
4. **Type annotations**: Use modern Python type hints (dict[str, Any] not Dict[str, Any])
5. **Error handling**: Be defensive - external APIs can fail
6. **Idempotency**: Ensure nodes are replay-safe (critical for checkpoints)

### Common Pitfalls

❌ **Don't:** Perform side effects before `interrupt()`
❌ **Don't:** Fail tasks when memory storage errors occur
❌ **Don't:** Use old Dict/List types (use dict/list)
❌ **Don't:** Forget to handle rate limits in new backends
❌ **Don't:** Modify state without returning new dict from node

✅ **Do:** Use `@task` for all CLI executions
✅ **Do:** Wrap memory calls in try/except
✅ **Do:** Return new state dict from nodes: `{**state, "key": value}`
✅ **Do:** Check for existing checkpoints before starting new sessions
✅ **Do:** Test with `--max-tasks 1` before full runs
