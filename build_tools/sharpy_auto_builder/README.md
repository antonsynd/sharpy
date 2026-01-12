# Sharpy Auto Builder

Automated implementation of Sharpy compiler tasks using GitHub Copilot CLI or Claude Code, with validation agents to ensure spec adherence and quality.

## Overview

The Sharpy Auto Builder orchestrates the implementation of tasks from the implementation plan (`docs/implementation_planning/task_list_0.1.0_to_0.1.5.md`) using AI coding assistants. It provides:

- **Multi-backend support**: Primary support for Claude Code, with limited GitHub Copilot CLI fallback
- **Rate limiting**: Automatic handling of API rate limits with failover
- **Validation agents**: Spec adherence, verification, and hallucination defense
- **Human-in-the-loop**: Critical questions and review requests for humans
- **Ground truth tracking**: Persistent state of all tasks and progress
- **LangGraph orchestration**: Robust state machine for task execution

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Sharpy Auto Builder                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐   │
│  │   CLI       │───▶│ Orchestrator │───▶│ Backend Manager │   │
│  │             │    │  (LangGraph) │    │                 │   │
│  └─────────────┘    └──────────────┘    └─────────────────┘   │
│                            │                    │               │
│                            │              ┌─────┴─────┐         │
│                            │              │           │         │
│                            ▼              ▼           ▼         │
│                     ┌──────────────┐ ┌─────────┐ ┌─────────┐   │
│                     │ Human Loop   │ │ Claude  │ │ Copilot │   │
│                     │ Manager      │ │ Code    │ │ CLI     │   │
│                     └──────────────┘ └─────────┘ └─────────┘   │
│                            │                                    │
│                     ┌──────┴──────┐                            │
│                     │             │                            │
│                     ▼             ▼                            │
│              ┌──────────┐  ┌──────────┐                        │
│              │Questions │  │ Reviews  │                        │
│              │Directory │  │Directory │                        │
│              └──────────┘  └──────────┘                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

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
./auto_builder.sh init
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
./auto_builder.sh init
```

### `status`

Show current progress, pending tasks, and human interactions.

```bash
./auto_builder.sh status
```

### `run`

Execute the orchestrator to process tasks.

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
├── auto_builder.sh          # Shell wrapper script
└── sharpy_auto_builder/
    ├── __init__.py
    ├── config.py            # Configuration management
    ├── state.py             # Ground truth state management
    ├── agents.py            # Agent definitions and prompts
    ├── backends.py          # Backend implementations (Claude Code, Copilot)
    ├── human_loop.py        # Human-in-the-loop support
    ├── orchestrator.py      # LangGraph orchestration (1200+ lines)
    ├── cli.py               # Command-line interface
    ├── run.py               # Main entry point
    ├── requirements.txt     # Python dependencies
    └── state/               # Runtime state (created on init)
        ├── config.json
        ├── ground_truth.json
        ├── execution_log.jsonl  # Full prompt/response logs
        ├── questions/       # Pending questions
        ├── answers/         # Human answers
        └── human_review/    # Review requests
```

## Configuration Options

Key settings in `config.py`:

| Setting | Default | Description |
|---------|---------|-------------|
| `max_retries_per_task` | 3 | Max execution attempts per task |
| `max_test_fix_attempts` | 3 | Max attempts to fix broken tests |
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

## Tips

1. **Start small**: Use `--max-tasks 1` to test the system
2. **Monitor progress**: Run `status` frequently
3. **Review carefully**: Don't approve reviews without reading them
4. **Use specific backends**: If one backend is rate-limited, use `--backend` to switch
5. **Reset on failure**: Use `reset` to retry failed tasks
6. **Check logs**: Use `./auto_builder.sh logs` for detailed prompt/response history
7. **Skip validation for speed**: Use `--skip-spec-check --skip-verification` when iterating quickly
8. **Review execution log**: The `state/execution_log.jsonl` contains full prompts and responses for debugging

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

### GitHub Copilot CLI limitations

The `gh copilot` CLI is designed for interactive shell command suggestions, not arbitrary code implementation. It's disabled by default. To enable it for experimentation:

```bash
./auto_builder.sh run --backend copilot
```

Note: Results will be limited. For full code implementation, use Claude Code.

## Development

To extend or modify:

1. **New agent type**: Add to `agents.py`
2. **New backend**: Implement `Backend` interface in `backends.py`
3. **New workflow step**: Add node to LangGraph in `orchestrator.py`
4. **New CLI command**: Add to `cli.py`
