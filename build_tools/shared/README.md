# Sharpy Build Tools - Shared Utilities

## Overview

Common utilities for Sharpy AI-powered development tools, providing a robust foundation for building tools that interact with AI backends like Claude Code and GitHub Copilot.

This shared module eliminates code duplication across tools by providing:
- **Backend abstraction** - Unified interface for AI model interactions
- **Rate limiting** - Detection, tracking, and handling of API rate limits
- **Configuration** - Base configuration with path resolution and serialization
- **Execution logging** - Structured JSONL logging for analysis and debugging
- **CLI building** - Consistent command construction for different backends

## Quick Start

```python
from build_tools.shared.backends import BackendConfig, ClaudeCodeBackend, ToolPermission

backend = ClaudeCodeBackend()

config = BackendConfig(
    allowed_tools={ToolPermission.READ, ToolPermission.WRITE},
)

response = await backend.execute(
    "Generate a function that calculates fibonacci numbers",
    config
)

print(f"Success: {response.success}")
print(f"Output: {response.output}")
```

## Modules

### Rate Limiting (`rate_limiting/`)

Detects rate limit errors, extracts wait times, and tracks rate limit state across backends.

**Key Components:**

- **`is_rate_limit_error(output, stderr)`** - Detects rate limit errors from output
- **`extract_rate_limit_wait_time(output, stderr)`** - Extracts wait time in seconds
- **`RateLimitState`** - Tracks rate limit state with exponential backoff

**Example:**

```python
from build_tools.shared.rate_limiting import (
    is_rate_limit_error,
    extract_rate_limit_wait_time,
    RateLimitState
)

# Check for rate limit
if is_rate_limit_error(output, stderr):
    wait_seconds = extract_rate_limit_wait_time(output, stderr)
    print(f"Rate limited! Wait {wait_seconds} seconds")

# Track state across multiple requests
state = RateLimitState()
state.record_request()

if error_occurred:
    state.record_error(wait_seconds=300)

if state.is_available():
    # Backend is ready for more requests
    pass
```

**Supported Error Patterns:**

- "rate limit exceeded"
- "too many requests"
- "quota exceeded"
- "try again in X minutes"
- "wait X seconds"
- "resets at Xam/pm"
- And 15+ more variations

### Backends (`backends/`)

Unified interface for executing prompts across different AI backends.

**Key Components:**

- **`BackendType`** - Enum of available backends (CLAUDE_CODE, COPILOT)
- **`ToolPermission`** - Enum of tool permissions (READ, WRITE, EDIT, BASH, GLOB, GREP)
- **`BackendConfig`** - Configuration for backend execution (timeout, tools, model)
- **`BackendResponse`** - Standardized response format
- **`Backend`** - Abstract base class for backend implementations
- **`ClaudeCodeBackend`** - Claude Code CLI backend
- **`CopilotBackend`** - GitHub Copilot CLI backend

**Custom Backends:**

```python
from build_tools.shared.backends import (
    Backend,
    BackendType,
    BackendResponse,
    BackendConfig,
)

class MyCustomBackend(Backend):
    @property
    def backend_type(self) -> BackendType:
        return BackendType.CLAUDE_CODE  # Or define your own

    async def execute(self, prompt: str, config: BackendConfig | None = None) -> BackendResponse:
        # Your implementation
        ...

    def is_available(self) -> bool:
        # Check if backend is ready
        return True
```

### Task Taxonomy (`model_selector.py`)

Task types and complexity levels that callers attach to a backend request.

**Key Components:**

- **`TaskType`** - Categories of tasks (CLASSIFICATION, CODE_GENERATION, etc.)
- **`TaskComplexity`** - Complexity levels (TRIVIAL, LOW, MEDIUM, HIGH, VERY_HIGH)

### Configuration (`config.py`)

Base configuration class with path resolution, JSON serialization, and directory management.

**Key Components:**

- **`BaseConfig`** - Base configuration class with common paths

**Basic Usage:**

```python
from build_tools.shared.config import BaseConfig
from pathlib import Path

config = BaseConfig(project_root=Path("/path/to/sharpy"))

# Standard paths
print(config.build_tools_dir)  # /path/to/sharpy/build_tools
print(config.docs_dir)         # /path/to/sharpy/docs
print(config.src_dir)          # /path/to/sharpy/src

# Create directories
config.ensure_directories()
```

**Extending for Tool-Specific Config:**

```python
from dataclasses import dataclass
from pathlib import Path
from build_tools.shared.config import BaseConfig

@dataclass
class MyToolConfig(BaseConfig):
    """Configuration for my tool."""

    max_iterations: int = 10
    output_format: str = "json"

    @property
    def output_dir(self) -> Path:
        """Tool-specific output directory."""
        return self.build_tools_dir / "my_tool" / "output"

    def ensure_directories(self) -> None:
        """Create both base and tool-specific directories."""
        super().ensure_directories()
        self.output_dir.mkdir(parents=True, exist_ok=True)

# Use it
config = MyToolConfig(max_iterations=20)
config.ensure_directories()
```

**JSON Serialization:**

```python
# Save configuration
config = MyToolConfig(max_iterations=20, output_format="markdown")
config.save(Path("config.json"))

# Load configuration
loaded = MyToolConfig.load(Path("config.json"))
print(loaded.max_iterations)  # 20
```

### Execution Logging (`logging.py`)

Structured JSONL logging for tracking AI backend executions, prompts, responses, and events.

**Key Components:**

- **`LogEventType`** - Enum of event types
- **`LogEvent`** - Individual log event with timestamp
- **`ExecutionLogger`** - JSONL-based logger with query utilities

**Basic Logging:**

```python
from build_tools.shared.logging import ExecutionLogger, LogEventType
from pathlib import Path

logger = ExecutionLogger(Path("execution.log"))

# Log a prompt
event_id = logger.log_prompt(
    prompt="Generate a function",
    backend="claude",
    model="claude-sonnet-4-6",
)

# Log the response (correlated to prompt)
logger.log_response(
    event_id=event_id,
    response="def my_function():\n    pass",
    success=True,
    duration_seconds=2.5,
)
```

**Event Types:**

```python
# Backend execution
logger.log(LogEventType.PROMPT_SENT, {"prompt": "..."})
logger.log(LogEventType.RESPONSE_RECEIVED, {"response": "..."})

# Rate limiting
logger.log_rate_limit(
    backend="claude",
    wait_seconds=300,
    error_message="Rate limit exceeded",
)

# Backend switching
logger.log_backend_switch(
    from_backend="claude",
    to_backend="copilot",
    reason="Rate limited",
)

# Model selection
logger.log_model_selection(
    task_type="code_generation",
    complexity="medium",
    selected_model="claude-sonnet-4-6",
    reasoning="Standard code generation task",
)

# Task lifecycle
logger.log_task(
    event_type=LogEventType.TASK_START,
    task_id="task_001",
    description="Implement rate limiting",
)
```

**Querying Logs:**

```python
# Read all events
events = logger.read_log()
for event in events:
    print(f"{event.timestamp}: {event.event_type}")

# Filter by event type
prompts = logger.query_log(event_types=[LogEventType.PROMPT_SENT])

# Filter by time range
from datetime import datetime, timedelta
recent = logger.query_log(
    start_time=datetime.now() - timedelta(hours=1)
)

# Get correlated events
prompt_event_id = "abc-123"
related = logger.get_correlated_events(prompt_event_id)
# Returns both PROMPT_SENT and RESPONSE_RECEIVED events
```

**Log Format (JSONL):**

Each line in the log file is a JSON object:

```json
{"timestamp": "2026-01-14T10:30:00", "event_type": "prompt_sent", "event_id": "abc-123", "details": {"prompt": "...", "backend": "claude"}}
{"timestamp": "2026-01-14T10:30:02", "event_type": "response_received", "event_id": "abc-123", "details": {"success": true, "duration": 2.1}}
{"timestamp": "2026-01-14T10:30:05", "event_type": "rate_limit_hit", "details": {"backend": "claude", "wait_seconds": 300}}
```

### CLI Command Building (`cli_builder.py`)

Utilities for building CLI commands for different backend types with proper argument formatting.

**Key Components:**

- **`CLICommand`** - Represents a CLI command (args, stdin, cwd)
- **`CLIBuilder`** - Builds commands for different backends

**Building Claude Commands:**

```python
from build_tools.shared.cli_builder import CLIBuilder
from build_tools.shared.backends import ToolPermission

tools = {ToolPermission.READ, ToolPermission.WRITE}
cmd = CLIBuilder.build_claude_command(
    prompt="Generate a function",
    tools=tools,
    model="claude-sonnet-4-6",
    print_mode=True,
)

print(cmd.args)   # ["claude", "--print", "--allowedTools", "Read,Write", "--model", "..."]
print(cmd.stdin)  # "Generate a function"
```

**Building Copilot Commands:**

```python
cmd = CLIBuilder.build_copilot_command(
    prompt="Fix this bug",
    tools={ToolPermission.READ, ToolPermission.EDIT},
)

print(cmd.args)  # ["/opt/homebrew/bin/copilot", "--prompt", "Fix this bug", "--allow-tool", "read", ...]
```

**Generic Builder:**

```python
from build_tools.shared.backends import BackendType

# Automatically selects appropriate builder
cmd = CLIBuilder.build_command(
    backend_type=BackendType.CLAUDE_CODE,
    prompt="Write a test",
    tools={ToolPermission.READ},
)
```

## API Reference

### Rate Limiting

#### `is_rate_limit_error(output: str, stderr: str = "") -> bool`
Detect if output indicates a rate limit error.

#### `extract_rate_limit_wait_time(output: str, stderr: str = "") -> Optional[int]`
Extract wait time in seconds from rate limit error message.

#### `RateLimitState`
Track rate limit state for a backend.

**Methods:**
- `record_request()` - Record a request
- `record_error(wait_seconds: Optional[int])` - Record an error
- `record_success()` - Record a success
- `is_available() -> bool` - Check if backend is available
- `should_wait() -> bool` - Check if we should wait before retrying
- `requests_in_window(window_seconds: int) -> int` - Count recent requests
- `get_backoff_delay() -> float` - Get current backoff delay

### Backends

#### `BackendType` (Enum)
- `CLAUDE_CODE` - Claude Code CLI
- `COPILOT` - GitHub Copilot CLI

#### `ToolPermission` (Enum)
- `READ` - Read files
- `WRITE` - Write files
- `EDIT` - Edit files
- `BASH` - Execute bash commands
- `GLOB` - File pattern matching
- `GREP` - Text search

#### `BackendConfig`
Configuration for backend execution.

**Fields:**
- `timeout_seconds: int = 300`
- `allowed_tools: set[ToolPermission] = {READ}`
- `model: Optional[str] = None`
- `max_retries: int = 3`
- `task_type: Optional[TaskType] = None`
- `task_complexity: Optional[TaskComplexity] = None`

#### `BackendResponse`
Standardized response from any backend.

**Fields:**
- `success: bool`
- `output: str`
- `stderr: str = ""`
- `exit_code: int = 0`
- `duration_seconds: float = 0.0`
- `rate_limited: bool = False`
- `error_message: Optional[str] = None`

### Task Taxonomy

#### `TaskType` (Enum)
Task categories: `CLASSIFICATION`, `CODE_GENERATION`, `VALIDATION`, `DOCUMENTATION`, `DEBUGGING`, `ARCHITECTURE`, `IMPLEMENTATION`

#### `TaskComplexity` (Enum)
Complexity levels: `TRIVIAL`, `LOW`, `MEDIUM`, `HIGH`, `VERY_HIGH`

### Configuration

#### `BaseConfig`
Base configuration with path resolution.

**Fields:**
- `project_root: Path`

**Properties:**
- `build_tools_dir: Path`
- `docs_dir: Path`
- `src_dir: Path`

**Methods:**
- `ensure_directories()` - Create directories
- `to_dict() -> dict` - Serialize to dict
- `from_dict(data: dict) -> BaseConfig` - Deserialize from dict
- `save(path: Path)` - Save to JSON
- `load(path: Path) -> BaseConfig` - Load from JSON

### Logging

#### `LogEventType` (Enum)
Event types: `PROMPT_SENT`, `RESPONSE_RECEIVED`, `RATE_LIMIT_HIT`, `BACKEND_SWITCH`, `ERROR`, `TASK_START`, `TASK_COMPLETE`, `STEP_START`, `STEP_END`, `MODEL_SELECTED`, `GENERATE`, `SKIP`, `REGENERATE`

#### `ExecutionLogger`
JSONL-based execution logger.

**Methods:**
- `log(event_type: LogEventType, details: dict) -> str` - Log event
- `log_prompt(prompt: str, backend: str, model: Optional[str]) -> str` - Log prompt
- `log_response(event_id: str, response: str, success: bool, duration_seconds: float)` - Log response
- `log_rate_limit(backend: str, wait_seconds: int, error_message: str)` - Log rate limit
- `log_backend_switch(from_backend: str, to_backend: str, reason: str)` - Log backend switch
- `log_model_selection(task_type: str, complexity: str, selected_model: str, reasoning: str)` - Log model selection
- `log_task(event_type: LogEventType, task_id: str, description: str, details: Optional[dict])` - Log task event
- `read_log() -> list[LogEvent]` - Read all events
- `query_log(event_types: Optional[list], start_time: Optional[datetime], end_time: Optional[datetime], task_id: Optional[str]) -> list[LogEvent]` - Query events
- `get_correlated_events(event_id: str) -> list[LogEvent]` - Get related events

### CLI Building

#### `CLIBuilder`
Build CLI commands for backends.

**Methods:**
- `build_claude_command(prompt: str, tools: set[ToolPermission], model: Optional[str], print_mode: bool) -> CLICommand` - Build Claude command
- `build_copilot_command(prompt: str, tools: set[ToolPermission]) -> CLICommand` - Build Copilot command
- `build_command(backend_type: BackendType, prompt: str, tools: set[ToolPermission], model: Optional[str]) -> CLICommand` - Generic builder

## Testing

All shared utilities include comprehensive unit tests:

```bash
# Run all shared module tests
cd /path/to/sharpy
python -m pytest build_tools/tests/ -v

# Run specific module tests
python -m pytest build_tools/tests/test_rate_limiting.py
python -m pytest build_tools/tests/test_backends.py
python -m pytest build_tools/tests/test_config.py
python -m pytest build_tools/tests/test_logging.py
python -m pytest build_tools/tests/test_cli_builder.py
```

## Design Principles

1. **Reusability** - Eliminate code duplication across tools
2. **Type Safety** - Full type hints throughout
3. **Testability** - Comprehensive unit tests for all modules
4. **Extensibility** - Easy to add new backends, models, or event types
5. **Observability** - Structured logging for debugging and analysis
6. **Reliability** - Rate limit handling

## Future Enhancements

Planned additions to the shared module:

- **Direct Claude API Backend** - For lower latency and cost on simple tasks
- **Cost Tracking** - Automatic token usage and cost tracking with optimization suggestions
- **Caching Layer** - Response caching for repeated similar prompts
- **Batch Processing** - Execute multiple prompts efficiently
- **Streaming Support** - Stream responses for long-running operations

## Contributing

When adding to the shared module:

1. Follow existing patterns and conventions
2. Add comprehensive docstrings
3. Include type hints for all public APIs
4. Write unit tests with >90% coverage
5. Update this README with new functionality
6. Ensure backwards compatibility

## Questions or Issues?

Refer to existing tool implementations for usage examples:
- `build_tools/generate_code_walkthroughs.py`
- `build_tools/sharpy_auto_builder/`
- `build_tools/sharpy_dogfood/`

Or check the task list at `docs/implementation_planning/build_tools_refactoring_tasks.md`.
