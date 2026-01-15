# Quick Start Guide

Get up and running with Sharpy Auto Builder in 5 minutes.

## Installation

```bash
cd build_tools

# Make script executable
chmod +x auto_builder.sh

# First run creates virtual environment and installs dependencies
./auto_builder.sh --help
```

## Your First Run

### 1. Initialize with a Task List

```bash
./auto_builder.sh init --task-list docs/implementation_planning/task_list_example.md
```

This parses your task list markdown file and creates the ground truth state.

### 2. Check Status

```bash
./auto_builder.sh status
```

You'll see:
- Overall progress
- Next pending task
- Any pending human questions/reviews

### 3. Run Some Tasks

```bash
# Start with just 1 task to test
./auto_builder.sh run --max-tasks 1
```

**What happens**:
1. System generates a thread ID (e.g., `sharpy-build-20260114-153045`)
2. Selects first pending task
3. Creates implementation plan
4. Runs baseline tests
5. Executes implementation (using Claude Code or Copilot)
6. Runs tests
7. Validates with spec adherence agent
8. May interrupt for your review
9. Commits changes and updates ground truth

### 4. Handle Interrupts

When the system needs your input, you'll see a **rich-formatted prompt**:

```
╔══════════════════════════════════════════╗
║   Human Review Required                  ║
╚══════════════════════════════════════════╝

Task ID: 0.1.0.3
Description: Implement basic lexer tokenization

Execution Status: ✓ Success

Files Changed (3):
  • src/Sharpy.Compiler/Lexer.cs
  • tests/Sharpy.Tests/LexerTests.cs
  • docs/implementation_walkthrough/Lexer.md

? Approve this implementation? (y/n/r for retry):
```

**Your options**:
- `y` (yes) - Approve and continue
- `n` (no) - Skip this task
- `r` (retry) - Re-run implementation

### 5. Resume After Interruption

If the session stops (crash, rate limit, or Ctrl+C):

```bash
# List available sessions
./auto_builder.sh run --list-sessions

# Resume specific session
./auto_builder.sh run --thread-id sharpy-build-20260114-153045
```

**The system picks up exactly where it left off** - no duplicate work!

## Common Workflows

### Fast Iteration (Skip Validation)

When iterating quickly on a feature:

```bash
./auto_builder.sh run \
  --max-tasks 5 \
  --skip-spec-check \
  --skip-verification \
  --skip-hallucination-check \
  --no-human-approval
```

This runs **much faster** but with less safety. Good for experimentation.

### Production Run (Full Validation)

For production-quality implementation:

```bash
./auto_builder.sh run --max-tasks 10
```

This runs **all validation agents** and requires human approval for critical tasks.

### Dealing with Rate Limits

When you hit a rate limit:

```
⏸️  SESSION PAUSED - Rate limit reached

Session checkpointed. Resume after 1 hours.
Estimated resume time: 2026-01-14 16:30:45

📌 Session saved with thread ID: sharpy-build-20260114-153045

▶️  To resume: ./auto_builder.sh run --thread-id sharpy-build-20260114-153045
```

**Wait the recommended time**, then resume:

```bash
./auto_builder.sh run --thread-id sharpy-build-20260114-153045
```

### Using Memory Patterns

After running several tasks, the system learns patterns:

```bash
# Search for implementation patterns
./auto_builder.sh memory search "parser" --namespace implementation

# See what errors were encountered
./auto_builder.sh memory search "type error" --namespace errors

# View memory statistics
./auto_builder.sh memory stats
```

These patterns are **automatically injected into prompts** for future tasks.

### Viewing Logs

Check what the AI actually did:

```bash
# View last 10 log entries
./auto_builder.sh logs --last 10

# View logs for specific task
./auto_builder.sh logs --task-id 0.1.0.3

# View only AI responses
./auto_builder.sh logs --event-type agent_response

# Include full prompts
./auto_builder.sh logs --show-prompt --last 5
```

### Checkpoint Management

Clean up old checkpoints to save disk space:

```bash
# View checkpoint statistics
./auto_builder.sh checkpoint-stats

# Clean up all threads, keep 20 most recent per thread
./auto_builder.sh checkpoint-cleanup --keep 20

# Preview what would be deleted
./auto_builder.sh checkpoint-cleanup --dry-run

# Clean specific thread
./auto_builder.sh checkpoint-cleanup --thread-id sharpy-build-20260114-153045 --keep 5
```

## Task List Format

Your task list should be a markdown file like this:

```markdown
# Project Tasks

## Phase 1: Lexer

### Task 1.0.0.1: Basic Tokenization

**Description**: Implement basic token recognition for keywords, identifiers, and literals.

**Files**:
- `src/Sharpy.Compiler/Lexer.cs`
- `tests/Sharpy.Tests/LexerTests.cs`

**Acceptance**:
- Recognizes keywords (if, else, while, etc.)
- Handles identifiers and literals
- Passes all lexer tests

---

### Task 1.0.0.2: String Literals

**Description**: Add support for string literal tokenization with escape sequences.

**Files**:
- `src/Sharpy.Compiler/Lexer.cs`
- `tests/Sharpy.Tests/LexerTests.cs`

**Acceptance**:
- Handles double-quoted strings
- Supports escape sequences (\n, \t, etc.)
- Passes string literal tests

---

## Phase 2: Parser

...
```

## Troubleshooting

### "Backend not available"

All backends are rate-limited. Options:

1. **Wait**: Check status for cooldown time
2. **Switch backends**: `--backend copilot` if Claude is rate-limited
3. **Resume later**: Session is checkpointed, resume when ready

### "Ground truth not found"

You need to initialize first:

```bash
./auto_builder.sh init --task-list docs/implementation_planning/your_tasks.md
```

### "Task blocked"

A task has dependencies that aren't completed:

```bash
# Check status to see what's blocking
./auto_builder.sh status

# Skip or complete blocking tasks first
./auto_builder.sh skip 0.1.0.1 --reason "Blocked by external issue"
```

### Tests Keep Failing

The system will:
1. Give the AI 3 attempts to fix
2. Create a follow-up task if all attempts fail
3. Continue with other tasks

Check logs to see what was tried:

```bash
./auto_builder.sh logs --event-type fix_response --task-id 0.1.0.3
```

### Tests Timing Out

If tests timeout (default: 5 minutes):

```bash
# View timeout events
./auto_builder.sh logs --event-type test_run | grep "timed_out"

# Increase timeout in config if needed
# Edit state/config.json and set test_timeout: 600.0 (10 minutes)
```

## Configuration

Customize behavior by editing `state/config.json`:

```json
{
  "max_retries_per_task": 3,
  "test_timeout": 300.0,
  "require_human_approval_for_critical": true,
  "checkpoint": {
    "max_checkpoints_per_thread": 100,
    "cleanup_interval": 50
  },
  "memory": {
    "enabled": true,
    "embedding_provider": null,
    "max_patterns_per_query": 5
  }
}
```

**Key settings**:
- `max_retries_per_task`: Attempts before giving up
- `test_timeout`: Maximum seconds for tests (catches infinite loops)
- `require_human_approval_for_critical`: Auto-approve or always ask
- `checkpoint.max_checkpoints_per_thread`: How many checkpoints to keep
- `memory.enabled`: Enable/disable pattern storage

## Tips for Success

1. **Start small**: Run 1-2 tasks first to verify setup
2. **Review carefully**: Don't blindly approve - read the changes
3. **Use checkpoints**: Don't worry about crashes - resume anytime
4. **Monitor memory**: Successful patterns help future tasks
5. **Check logs**: Full prompt/response history is in execution_log.jsonl
6. **Skip when blocked**: Use `./auto_builder.sh skip` for external blockers
7. **Reset on failure**: Use `./auto_builder.sh reset` to retry failed tasks

## Next Steps

- **Read the full README**: [README.md](README.md)
- **Understand the architecture**: [ARCHITECTURE.md](ARCHITECTURE.md)
- **Contribute**: [CONTRIBUTING.md](CONTRIBUTING.md)
- **Check implementation plans**: `docs/implementation_planning/`

## Common Commands Reference

```bash
# Setup
./auto_builder.sh init --task-list <path>

# Execution
./auto_builder.sh run --max-tasks 5
./auto_builder.sh run --thread-id <id>  # Resume
./auto_builder.sh run --list-sessions    # List sessions

# Monitoring
./auto_builder.sh status
./auto_builder.sh logs --last 10
./auto_builder.sh report -o report.md

# Task Management
./auto_builder.sh reset <task-id>
./auto_builder.sh skip <task-id> --reason "..."

# Checkpoints
./auto_builder.sh checkpoint-stats
./auto_builder.sh checkpoint-cleanup --keep 20

# Memory
./auto_builder.sh memory search <query>
./auto_builder.sh memory stats
./auto_builder.sh memory clear --namespace <name> --confirm

# Human Interaction
./auto_builder.sh answer <question-id> "answer"
./auto_builder.sh review <review-id> approved
```

## Getting Help

- Run `./auto_builder.sh --help` for command help
- Check `./auto_builder.sh <command> --help` for specific commands
- Read the detailed [README.md](README.md)
- View [ARCHITECTURE.md](ARCHITECTURE.md) for technical details
