# Sharpy Build Tools - Shared Utilities

## Overview

Common utilities for Sharpy AI-powered development tools, providing a robust foundation for building tools that interact with AI backends like Claude Code and GitHub Copilot.

This shared module eliminates code duplication across tools by providing:
- **Backend abstraction** - Unified interface for AI model interactions with automatic failover
- **Rate limiting** - Detection, tracking, and handling of API rate limits
- **Model selection** - Intelligent routing of tasks to cost-effective models
- **Configuration** - Base configuration with path resolution and serialization
- **Execution logging** - Structured JSONL logging for analysis and debugging
- **CLI building** - Consistent command construction for different backends

## Quick Start

```python
from build_tools.shared.backends import BackendManager, BackendConfig, ToolPermission
from build_tools.shared.model_selector import TaskType, TaskComplexity

# Create a backend manager with automatic failover
manager = BackendManager()

# Execute a prompt with automatic model selection
config = BackendConfig(
    task_type=TaskType.CODE_GENERATION,
    task_complexity=TaskComplexity.MEDIUM,
    allowed_tools={ToolPermission.READ, ToolPermission.WRITE},
)

response, backend_used = await manager.execute(
    "Generate a function that calculates fibonacci numbers",
    config
)

print(f"Used backend: {backend_used}")
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

Unified interface for executing prompts across different AI backends with automatic failover and rate limit handling.

**Key Components:**

- **`BackendType`** - Enum of available backends (CLAUDE_CODE, COPILOT)
- **`ToolPermission`** - Enum of tool permissions (READ, WRITE, EDIT, BASH, GLOB, GREP)
- **`BackendConfig`** - Configuration for backend execution (timeout, tools, model)
- **`BackendResponse`** - Standardized response format
- **`Backend`** - Abstract base class for backend implementations
- **`ClaudeCodeBackend`** - Claude Code CLI backend
- **`CopilotBackend`** - GitHub Copilot CLI backend
- **`BackendManager`** - Manages multiple backends with failover

**Basic Backend Usage:**

```python
from build_tools.shared.backends import (
    BackendManager,
    BackendConfig,
    ToolPermission,
    BackendType,
)

# Create manager with default configuration
manager = BackendManager()

# Simple execution
response, backend = await manager.execute("Write a hello world function")

# With explicit configuration
config = BackendConfig(
    timeout_seconds=300,
    allowed_tools={ToolPermission.READ, ToolPermission.WRITE},
    model="claude-3-5-haiku-20241022",
)
response, backend = await manager.execute(prompt, config)
```

**Automatic Failover:**

```python
from build_tools.shared.backends import (
    BackendManager,
    BackendManagerConfig,
    BackendType,
)

# Configure primary backend with fallbacks
config = BackendManagerConfig(
    primary_backend=BackendType.CLAUDE_CODE,
    fallback_backends=[BackendType.COPILOT],
    auto_failover=True,
    max_requests_per_window=50,
)

manager = BackendManager(config)

# Automatically fails over if primary is rate limited
response, backend = await manager.execute(prompt)
print(f"Executed using: {backend}")  # May be fallback if primary unavailable
```

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

# Register with manager
manager = BackendManager()
manager.register_backend(MyCustomBackend())
```

**Monitoring Backend Status:**

```python
# Get available backends
available = manager.get_available_backends()
print(f"Available backends: {available}")

# Get detailed status
status = manager.get_backend_status()
for backend_type, info in status.items():
    print(f"{backend_type}:")
    print(f"  Available: {info['available']}")
    print(f"  Requests in window: {info['requests_in_window']}")
    print(f"  Consecutive errors: {info['consecutive_errors']}")
```

### Model Selection (`model_selector.py`)

Intelligent routing of tasks to cost-effective models based on task type and complexity.

**Key Components:**

- **`TaskType`** - Categories of tasks (CLASSIFICATION, CODE_GENERATION, etc.)
- **`TaskComplexity`** - Complexity levels (TRIVIAL, LOW, MEDIUM, HIGH, VERY_HIGH)
- **`ModelRecommendation`** - Recommended model with reasoning
- **`ModelSelector`** - Selects appropriate model for tasks

**Model Constants:**

```python
from build_tools.shared.model_selector import HAIKU, SONNET, OPUS

# Model identifiers
HAIKU  = "claude-3-5-haiku-20241022"    # $0.80/$4.00 per 1M tokens
SONNET = "claude-sonnet-4-5-20250929"   # $3.00/$15.00 per 1M tokens
OPUS   = "claude-opus-4-5-20251101"     # $15.00/$75.00 per 1M tokens
```

**Manual Model Selection:**

```python
from build_tools.shared.model_selector import (
    ModelSelector,
    TaskType,
    TaskComplexity,
)

# Select model for a specific task
recommendation = ModelSelector.select_model(
    task_type=TaskType.CODE_GENERATION,
    complexity=TaskComplexity.MEDIUM,
)

print(f"Recommended model: {recommendation.model}")
print(f"Reasoning: {recommendation.reasoning}")
print(f"Fallback: {recommendation.fallback_model}")
print(f"Extended thinking: {recommendation.requires_extended_thinking}")
```

**Automatic Task Classification:**

```python
# Classify task from description
task_type, complexity = ModelSelector.classify_task(
    "Fix this intermittent test failure in the parser"
)
# Returns: (TaskType.DEBUGGING, TaskComplexity.HIGH)

# Or use convenience method
recommendation = ModelSelector.select_model_for_description(
    "Generate comprehensive documentation for the Lexer class"
)
# Automatically classifies and selects appropriate model
```

**Model Selection Guidelines:**

| Task Type       | Trivial | Low    | Medium | High   | Very High |
|----------------|---------|--------|--------|--------|-----------|
| Classification | Haiku   | Haiku  | Sonnet | Sonnet | Opus      |
| Code Generation| Haiku   | Sonnet | Sonnet | Opus   | Opus      |
| Validation     | Haiku   | Haiku  | Sonnet | Sonnet | Opus      |
| Documentation  | Sonnet  | Sonnet | Sonnet | Opus   | Opus      |
| Debugging      | Sonnet  | Sonnet | Sonnet | Opus   | Opus      |
| Architecture   | Sonnet  | Sonnet | Opus   | Opus   | Opus      |
| Implementation | Sonnet  | Sonnet | Sonnet | Opus   | Opus      |

**Use Haiku for:**
- Simple yes/no decisions ("Is this a rate limit error?")
- Pattern matching and extraction
- Straightforward classification tasks

**Use Sonnet for:**
- Most code generation following established patterns
- Documentation generation
- Bug fixes with clear reproduction steps
- Test writing
- Straightforward refactoring

**Use Opus for:**
- Architecture and design decisions
- Complex debugging with unclear root cause
- Novel feature implementation
- Multi-file refactoring requiring design tradeoffs
- Tasks requiring extended reasoning

**Integration with BackendManager:**

```python
from build_tools.shared.backends import BackendManager, BackendConfig
from build_tools.shared.model_selector import TaskType, TaskComplexity

manager = BackendManager()

# Model is automatically selected based on task type/complexity
config = BackendConfig(
    task_type=TaskType.DEBUGGING,
    task_complexity=TaskComplexity.HIGH,
    allowed_tools={ToolPermission.READ, ToolPermission.EDIT},
)

# BackendManager will select appropriate model (Opus for HIGH complexity debugging)
response, backend = await manager.execute(prompt, config)
```

**Model Override:**

```python
from build_tools.shared.model_selector import ModelOverride

# Explicitly override model selection
override = ModelOverride(
    model="claude-opus-4-5-20251101",
    reason="This specific bug requires extended reasoning"
)

recommendation = ModelSelector.select_model(
    task_type=TaskType.DEBUGGING,
    complexity=TaskComplexity.MEDIUM,
    override=override,
)
# Uses Opus instead of Sonnet
```

**Cost Tracking Callback:**

```python
def track_costs(recommendation: ModelRecommendation, description: str):
    """Log model selection for cost analysis."""
    print(f"Task: {description}")
    print(f"Model: {recommendation.model}")
    print(f"Estimated cost tier: {get_cost_tier(recommendation.model)}")

ModelSelector.set_selection_callback(track_costs)

# Now all selections will be logged
recommendation = ModelSelector.select_model_for_description(
    "Generate tests for the semantic analyzer"
)
```

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
    model="claude-sonnet-4-5-20250929",
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
    selected_model="claude-sonnet-4-5-20250929",
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
    model="claude-sonnet-4-5-20250929",
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

## Complete Example: Building a Tool

Here's a complete example of building a tool that uses all shared utilities:

```python
from pathlib import Path
from dataclasses import dataclass
from build_tools.shared.config import BaseConfig
from build_tools.shared.backends import (
    BackendManager,
    BackendConfig,
    ToolPermission,
)
from build_tools.shared.model_selector import TaskType, TaskComplexity
from build_tools.shared.logging import ExecutionLogger, LogEventType

@dataclass
class MyToolConfig(BaseConfig):
    """Configuration for my tool."""
    max_retries: int = 3
    output_dir: Path = None

    def __post_init__(self):
        if self.output_dir is None:
            self.output_dir = self.build_tools_dir / "my_tool" / "output"

class MyTool:
    """Example tool using shared utilities."""

    def __init__(self, config: MyToolConfig):
        self.config = config
        self.backend_manager = BackendManager()
        self.logger = ExecutionLogger(
            config.build_tools_dir / "my_tool" / "execution.log"
        )

        # Ensure directories exist
        self.config.ensure_directories()
        self.config.output_dir.mkdir(parents=True, exist_ok=True)

    async def process_task(self, description: str, code_context: str):
        """Process a task with automatic model selection and error handling."""

        # Log task start
        task_id = self.logger.log_task(
            event_type=LogEventType.TASK_START,
            task_id=description[:50],
            description=description,
        )

        # Build prompt
        prompt = f"{description}\n\nContext:\n{code_context}"

        # Configure backend with automatic model selection
        config = BackendConfig(
            task_type=TaskType.CODE_GENERATION,
            task_complexity=TaskComplexity.MEDIUM,
            allowed_tools={ToolPermission.READ, ToolPermission.WRITE},
            timeout_seconds=300,
        )

        # Execute with automatic failover
        try:
            response, backend_used = await self.backend_manager.execute(
                prompt, config
            )

            if response.success:
                # Process successful response
                output_file = self.config.output_dir / f"{task_id}.py"
                output_file.write_text(response.output)

                self.logger.log_task(
                    event_type=LogEventType.TASK_COMPLETE,
                    task_id=task_id,
                    description=description,
                    details={"backend": str(backend_used), "duration": response.duration_seconds},
                )

                return output_file
            else:
                # Handle error
                self.logger.log(
                    LogEventType.ERROR,
                    {
                        "task_id": task_id,
                        "error": response.error_message,
                        "backend": str(backend_used),
                    },
                )
                return None

        except Exception as e:
            self.logger.log(LogEventType.ERROR, {"task_id": task_id, "exception": str(e)})
            raise

# Use the tool
async def main():
    config = MyToolConfig(project_root=Path("/path/to/sharpy"))
    tool = MyTool(config)

    result = await tool.process_task(
        description="Generate a parser for JSON files",
        code_context="# existing code here",
    )

    print(f"Generated: {result}")

if __name__ == "__main__":
    import asyncio
    asyncio.run(main())
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

#### `BackendManager`
Manages multiple backends with automatic failover.

**Methods:**
- `register_backend(backend: Backend)` - Register a backend
- `unregister_backend(backend_type: BackendType)` - Unregister a backend
- `async execute(prompt: str, config: Optional[BackendConfig], preferred_backend: Optional[BackendType]) -> tuple[BackendResponse, BackendType]` - Execute with failover
- `get_available_backends() -> list[BackendType]` - Get available backends
- `get_backend_status() -> dict` - Get detailed status
- `reset_backend_state(backend_type: BackendType)` - Reset rate limit state

### Model Selection

#### `TaskType` (Enum)
Task categories: `CLASSIFICATION`, `CODE_GENERATION`, `VALIDATION`, `DOCUMENTATION`, `DEBUGGING`, `ARCHITECTURE`, `IMPLEMENTATION`

#### `TaskComplexity` (Enum)
Complexity levels: `TRIVIAL`, `LOW`, `MEDIUM`, `HIGH`, `VERY_HIGH`

#### `ModelSelector`
Intelligent model selection.

**Methods:**
- `select_model(task_type: TaskType, complexity: TaskComplexity, context: Optional[dict], override: Optional[ModelOverride]) -> ModelRecommendation` - Select model
- `classify_task(description: str, context: Optional[dict]) -> tuple[TaskType, TaskComplexity]` - Classify task from description
- `select_model_for_description(description: str, context: Optional[dict], override: Optional[ModelOverride]) -> ModelRecommendation` - Convenience method
- `set_selection_callback(callback: Callable)` - Set callback for logging

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
python -m pytest build_tools/tests/test_model_selector.py
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
6. **Cost Awareness** - Intelligent model selection to minimize API costs
7. **Reliability** - Automatic failover and rate limit handling

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
