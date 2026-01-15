# Sharpy Auto Builder Architecture

This document provides detailed technical architecture documentation for the Sharpy Auto Builder, focusing on the LangGraph-based orchestration system and its key components.

## Table of Contents

- [Overview](#overview)
- [Core Technologies](#core-technologies)
- [System Components](#system-components)
- [LangGraph State Machine](#langgraph-state-machine)
- [Durable Persistence (Phase 1)](#durable-persistence-phase-1)
- [Native Interrupts (Phase 2)](#native-interrupts-phase-2)
- [Idempotent Tasks (Phase 3)](#idempotent-tasks-phase-3)
- [Long-Term Memory (Phase 4)](#long-term-memory-phase-4)
- [State Management](#state-management)
- [Error Handling & Recovery](#error-handling--recovery)
- [Performance & Scalability](#performance--scalability)

## Overview

The Sharpy Auto Builder is a production-ready automation system that orchestrates the implementation of compiler tasks using AI coding assistants (Claude Code and GitHub Copilot CLI). It's built on LangGraph, a framework for creating stateful, multi-actor applications with LLMs.

### Key Design Goals

1. **Durability**: Survive crashes, rate limits, and restarts
2. **Resumability**: Pick up exactly where you left off
3. **Idempotency**: Safe graph replays without duplicate work
4. **Learning**: Improve over time by storing successful patterns
5. **Human Oversight**: Interactive review and decision-making
6. **Reliability**: Comprehensive error handling and recovery

## Core Technologies

### LangGraph

LangGraph provides the foundational state machine framework:

- **StateGraph**: Directed graph of nodes (functions) and edges (transitions)
- **Checkpointing**: Automatic state persistence via `SqliteSaver`
- **Interrupts**: `interrupt()` function for human-in-the-loop
- **Tasks**: `@task` decorator for idempotent operations
- **Memory Store**: Long-term pattern storage with semantic search

**Version**: Requires `langgraph>=0.2.50`, `langgraph-checkpoint-sqlite>=2.0.0`

### SQLite

Used for durable persistence:

- **Checkpoints**: `state/orchestrator_checkpoints.db` (all graph state)
- **Memory Patterns**: `state/memory_store.db` (implementation patterns)
- **Thread-safe**: `check_same_thread=False` for async operations

### Python Async/Await

Async operations for:
- CLI subprocess execution (`asyncio.create_subprocess_exec`)
- Graph streaming (`app.astream()`)
- Interrupt handling (`run_with_interrupts()`)

## System Components

### 1. Orchestrator (`orchestrator.py`)

The heart of the system - a 3300+ line LangGraph state machine.

**Key Responsibilities:**
- Build and compile the state graph
- Execute nodes (task selection, implementation, validation, etc.)
- Manage checkpoints and resumption
- Handle interrupts for human review
- Store patterns in memory
- Route state transitions based on results

**Critical Methods:**
```python
class Orchestrator:
    def __init__(self, config: Config):
        # Initialize checkpointer (SqliteSaver)
        # Initialize memory store (InMemoryStore)
        # Build and compile graph

    def _build_graph(self) -> StateGraph:
        # Define all nodes and edges

    async def run(self, max_tasks: int, thread_id: str) -> dict:
        # Execute graph with checkpointing

    # Node methods (25+)
    async def _select_task_node(self, state) -> OrchestratorState
    async def _execute_implementation_node(self, state) -> OrchestratorState
    async def _request_human_review_node(self, state) -> OrchestratorState
    # ... etc

    # Routing methods (10+)
    def _route_after_tests(self, state) -> str
    def _route_after_validation(self, state) -> str
    # ... etc
```

### 2. CLI (`cli.py`)

Command-line interface with interrupt handling.

**Key Responsibilities:**
- Parse commands and arguments
- Run interrupt loop (`run_with_interrupts()`)
- Display interrupt payloads (rich formatting)
- Collect human responses
- Manage checkpoint and memory commands

**Critical Functions:**
```python
async def run_with_interrupts(orchestrator, thread_id, max_tasks):
    """Main interrupt loop."""
    while True:
        result = await orchestrator.app.ainvoke(...)

        if "__interrupt__" in result:
            # Display interrupt
            # Collect response
            # Resume with Command(resume=response)

        if result.next_action in ["complete", "pause_rate_limited"]:
            break
```

### 3. Tasks (`tasks.py`)

Idempotent task wrappers using `@task` decorator.

**Key Responsibilities:**
- Wrap CLI executions (Claude Code, Copilot, pytest)
- Compute input hashes for caching
- Provide fallback file-based caching
- Return structured `TaskExecutionResult`

**Critical Functions:**
```python
@task
async def execute_claude_cli(
    prompt: str,
    tools: list[str],
    model: str,
    timeout: float,
    task_id: str,
    attempt: int,
) -> TaskExecutionResult:
    """Execute Claude Code CLI with idempotent semantics."""
    # Compute input hash
    # Execute via subprocess
    # Return TaskExecutionResult

@task
async def run_tests(
    test_command: str,
    working_dir: Path,
    timeout: float,
    task_id: str,
    attempt: int,
) -> TaskExecutionResult:
    """Run tests with idempotent semantics."""
    # Execute test command
    # Detect timeout (infinite loops)
    # Return TaskExecutionResult
```

### 4. Memory (`memory.py`)

Long-term pattern storage and retrieval.

**Key Responsibilities:**
- Store implementation patterns (successful solutions)
- Store error patterns (failures to avoid)
- Search patterns (semantic or exact match)
- Generate context strings for prompts

**Critical Classes:**
```python
@dataclass
class Pattern:
    """Represents a stored pattern."""
    id: str
    namespace: tuple[str, ...]
    task_type: str
    description: str
    solution: str
    files: list[str]
    tags: list[str]
    success: bool
    metadata: dict[str, Any]

class MemoryManager:
    """Manages pattern storage and retrieval."""
    NS_IMPLEMENTATION = ("sharpy", "implementation_patterns")
    NS_ERRORS = ("sharpy", "error_patterns")

    def store_implementation_pattern(...) -> str
    def store_error_pattern(...) -> str
    def search_patterns(...) -> list[Pattern]
    def get_implementation_context(...) -> str
    def get_error_avoidance_context(...) -> str
```

### 5. Interrupt Handler (`interrupt_handler.py`)

CLI display and response collection for interrupts.

**Key Responsibilities:**
- Display review requests (rich formatting)
- Display questions (rich formatting)
- Collect user responses
- Validate responses

**Critical Functions:**
```python
def display_interrupt(interrupt_data: dict):
    """Display interrupt with rich formatting."""
    if interrupt_data["type"] == "review":
        _display_review_request(interrupt_data)
    elif interrupt_data["type"] == "question":
        _display_question(interrupt_data)

def collect_response(interrupt_data: dict) -> dict:
    """Collect user response."""
    if interrupt_data["type"] == "review":
        return _collect_review_response()
    elif interrupt_data["type"] == "question":
        return _collect_question_response(interrupt_data)
```

### 6. Config (`config.py`)

Centralized configuration with nested dataclasses.

**Key Components:**
```python
@dataclass
class CheckpointConfig:
    """Checkpoint persistence settings."""
    max_checkpoints_per_thread: int = 100
    cleanup_interval: int = 50
    # ...

@dataclass
class MemoryConfig:
    """Memory store settings."""
    enabled: bool = True
    embedding_provider: Optional[Literal["openai", "local"]] = None
    max_patterns_per_query: int = 5
    # ...

@dataclass
class Config:
    """Main configuration."""
    checkpoint: CheckpointConfig
    memory: MemoryConfig
    backends: dict[BackendType, BackendConfig]
    # ...
```

## LangGraph State Machine

### State Definition

```python
class OrchestratorState(TypedDict):
    """LangGraph state type."""
    current_task: Optional[dict]
    ground_truth_path: str
    execution_attempt: int
    fix_attempt: int
    validation_fix_attempt: int
    last_execution_result: Optional[dict]
    baseline_test_passed: Optional[bool]
    baseline_test_output: Optional[str]
    validation_results: list[dict]
    awaiting_human_input: bool  # Legacy field
    human_question_id: Optional[str]  # Legacy field
    human_review_id: Optional[str]  # Legacy field
    human_response: Optional[dict]
    response_analysis: Optional[dict]
    auto_decision: Optional[dict]
    next_action: str
    error_message: Optional[str]
    messages: list[str]
```

### Graph Structure

The graph has **15 nodes** and **conditional routing**:

```
           ┌──────────────┐
           │ select_task  │
           └──────┬───────┘
                  │
         ┌────────┴────────┐
         │                 │
         ▼                 ▼
   ┌──────────┐      ┌──────────┐
   │   plan   │      │ complete │
   │  impl    │      │  (END)   │
   └────┬─────┘      └──────────┘
        │
        ▼
   ┌──────────┐
   │ baseline │
   │  tests   │
   └────┬─────┘
        │
        ▼
   ┌──────────┐
   │ execute  │
   │   impl   │
   └────┬─────┘
        │
        ▼
   ┌──────────┐
   │ analyze  │
   │ response │
   └────┬─────┘
        │
   ┌────┴────┐
   │         │
   ▼         ▼
┌──────┐  ┌──────┐
│ auto │  │ run  │
│decide│  │tests │
└──┬───┘  └───┬──┘
   │          │
   │     ┌────┴────┐
   │     │         │
   │     ▼         ▼
   │  ┌──────┐  ┌──────┐
   │  │ pass │  │ fix  │
   │  └──┬───┘  └───┬──┘
   │     │          │
   │     ▼          │
   │  ┌──────┐     │
   │  │valid │     │
   │  │ spec │     │
   │  └──┬───┘     │
   │     │         │
   │     ▼         │
   │  ┌──────┐    │
   │  │valid │    │
   │  │verify│    │
   │  └──┬───┘    │
   │     │        │
   │     ▼        │
   │  ┌──────┐   │
   │  │check │   │
   │  │halluc│   │
   │  └──┬───┘   │
   │     │       │
   │     ▼       │
   │  ┌──────┐  │
   └─▶│update│◀─┘
      │ground│
      │truth │
      └──┬───┘
         │
         ▼
      ┌──────┐
      │commit│
      └──────┘
```

### Key Routing Logic

```python
def _route_after_tests(self, state):
    """Route after test execution."""
    if tests_passed:
        return "validate"  # → validate_spec_adherence
    elif agent_broke_tests:
        return "fix"  # → fix_test_failures
    elif preexisting_failure:
        return "preexisting_failure"  # → validate_spec_adherence
    else:
        return "error"  # → handle_error

def _route_after_human_response(self, state):
    """Route after human review."""
    response = state["human_response"]
    if response["approved"]:
        return "commit_changes"
    elif response["retry"]:
        return "execute_implementation"
    else:
        return "update_ground_truth"  # Skip
```

## Durable Persistence (Phase 1)

### Architecture

```
┌─────────────────────────────────────────────────────┐
│                   Orchestrator                      │
├─────────────────────────────────────────────────────┤
│                                                     │
│  self.checkpointer = SqliteSaver(db_connection)    │
│  self.app = graph.compile(checkpointer=...)        │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │         LangGraph State Machine             │   │
│  ├─────────────────────────────────────────────┤   │
│  │                                             │   │
│  │  Node1 → [checkpoint] → Node2 → [chkpt]    │   │
│  │                                             │   │
│  └──────────────────┬──────────────────────────┘   │
│                     │                              │
│                     ▼                              │
│        ┌────────────────────────┐                  │
│        │   SqliteSaver          │                  │
│        │  (LangGraph)           │                  │
│        └────────┬───────────────┘                  │
│                 │                                  │
└─────────────────┼──────────────────────────────────┘
                  │
                  ▼
      ┌───────────────────────────┐
      │ orchestrator_checkpoints  │
      │         .db               │
      ├───────────────────────────┤
      │ • thread_id              │
      │ • checkpoint_id          │
      │ • checkpoint_ns          │
      │ • parent_checkpoint_id   │
      │ • type                   │
      │ • checkpoint (BLOB)      │
      │ • metadata (BLOB)        │
      └───────────────────────────┘
```

### Implementation Details

**Initialization** (orchestrator.py:175-179):
```python
# Create SQLite connection with thread-safety disabled
self._db_connection = sqlite3.connect(
    str(self.config.checkpoint_db_path),
    check_same_thread=False  # Required for async
)

# Create checkpointer and initialize schema
self.checkpointer = SqliteSaver(self._db_connection)
self.checkpointer.setup()  # Creates tables
```

**Graph Compilation** (orchestrator.py:190):
```python
self.app = self.graph.compile(
    checkpointer=self.checkpointer,  # Enable persistence
    store=self.memory_store,         # Enable memory
)
```

**Thread ID Management** (orchestrator.py:3037-3050):
```python
# Generate unique thread ID if not provided
if thread_id is None:
    thread_id = f"sharpy-build-{datetime.now().strftime('%Y%m%d-%H%M%S')}"

self._current_thread_id = thread_id

# Print resume instructions
print(f"🔗 Thread ID: {thread_id}")
print(f"💡 To resume: ./auto_builder.sh run --thread-id {thread_id}")
```

**Resumption Logic** (orchestrator.py:3057-3067):
```python
config = {"configurable": {"thread_id": thread_id}}

# Check for existing state
existing_state = self.app.get_state(config)
is_resume = existing_state.values != {}

if is_resume:
    print("🔄 Resuming existing session...")
    initial_state = None  # Use checkpoint state
else:
    print("🆕 Starting new session...")
    initial_state = self._create_initial_state()
```

**Checkpoint Cleanup** (orchestrator.py:3184-3224):
```python
def _cleanup_thread_checkpoints(self, thread_id: str):
    """Remove old checkpoints beyond max_checkpoints_per_thread."""
    max_checkpoints = self.config.checkpoint.max_checkpoints_per_thread

    # Get all checkpoints for thread, ordered by ID descending
    cursor.execute("""
        SELECT checkpoint_id, checkpoint_ns
        FROM checkpoints
        WHERE thread_id = ?
        ORDER BY checkpoint_id DESC
    """, (thread_id,))

    checkpoints = cursor.fetchall()

    # Keep only the most recent N checkpoints
    if len(checkpoints) > max_checkpoints:
        to_delete = checkpoints[max_checkpoints:]
        for checkpoint_id, checkpoint_ns in to_delete:
            cursor.execute("""
                DELETE FROM checkpoints
                WHERE thread_id = ? AND checkpoint_id = ? AND checkpoint_ns = ?
            """, (thread_id, checkpoint_id, checkpoint_ns))
```

### Benefits

1. **Crash Recovery**: Process crashes don't lose work
2. **Rate Limit Handling**: Pause execution, resume after cooldown
3. **Long-Running Tasks**: Stop and restart without losing progress
4. **Debugging**: Inspect exact state at any checkpoint
5. **Rollback**: Restart from earlier checkpoint if needed

## Native Interrupts (Phase 2)

### Architecture

```
┌────────────────────────────────────────────────────────┐
│                     Graph Execution                     │
├────────────────────────────────────────────────────────┤
│                                                        │
│  Node1 → Node2 → _request_human_review_node           │
│                         │                              │
│                         │ interrupt(review_payload)    │
│                         │                              │
│                         ▼                              │
│                  ╔══════════════╗                      │
│                  ║   PAUSED     ║                      │
│                  ╚═══════╤══════╝                      │
│                          │                             │
└──────────────────────────┼─────────────────────────────┘
                           │
                           │ __interrupt__ in result
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│              CLI Interrupt Handler                      │
├────────────────────────────────────────────────────────┤
│                                                        │
│  1. display_interrupt(interrupt_data)                  │
│     └─ Rich formatting                                 │
│     └─ Show task, files, validation results            │
│                                                        │
│  2. collect_response(interrupt_data)                   │
│     └─ Prompt user for decision                        │
│     └─ Validate response                               │
│                                                        │
│  3. Command(resume=response)                           │
│     └─ Resume graph execution                          │
│                                                        │
└────────────────────────────────────────────────────────┘
                           │
                           │ Resume with response
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                  Graph Execution                        │
├────────────────────────────────────────────────────────┤
│                                                        │
│  _request_human_review_node (continued)                │
│     │ human_response = <value from CLI>                │
│     │                                                  │
│     └─→ {**state, "human_response": response}         │
│                                                        │
│  → Next node based on routing                          │
│                                                        │
└────────────────────────────────────────────────────────┘
```

### Implementation Details

**Interrupt Payloads** (orchestrator.py:67-98):
```python
class HumanReviewPayload(TypedDict):
    """Payload for review interrupt."""
    type: Literal["review"]
    task_id: str
    task_description: str
    execution_result: dict
    validation_results: list[dict]
    files_changed: list[str]
    diff_summary: str

class HumanResponse(TypedDict):
    """Response from human."""
    approved: bool
    feedback: Optional[str]
    modified_value: Optional[str]
    retry: bool
```

**Review Node with Interrupt** (orchestrator.py:2570-2656):
```python
async def _request_human_review_node(self, state):
    """Request human review using native interrupt."""

    # Gather review data (idempotent - no side effects)
    files_changed = self._get_files_changed()  # Git status
    diff_summary = self._get_diff_summary()    # Git diff

    # Build payload
    review_payload: HumanReviewPayload = {
        "type": "review",
        "task_id": task_data["id"],
        "task_description": task_data["description"],
        "execution_result": execution_result,
        "validation_results": validation_results,
        "files_changed": files_changed,
        "diff_summary": diff_summary,
    }

    # Interrupt here - execution pauses
    human_response: HumanResponse = interrupt(review_payload)

    # After resumption, process response
    self._log_execution("human_review_response", task_id, ...)

    # Route based on response
    if human_response["approved"]:
        next_action = "commit_changes"
    elif human_response["retry"]:
        next_action = "execute_implementation"
    else:
        next_action = "update_ground_truth"

    return {
        **state,
        "human_response": human_response,
        "next_action": next_action,
    }
```

**Validation Loop** (orchestrator.py:2443-2500):
```python
def _interrupt_with_validation(
    self,
    payload: dict,
    validator: callable,
    max_attempts: int = 3,
) -> dict:
    """Interrupt with validation loop."""
    attempt = 0

    while attempt < max_attempts:
        attempt += 1

        # Interrupt
        response = interrupt(payload)

        # Validate
        is_valid, error_message = validator(response)

        if is_valid:
            return response  # Success

        # Invalid - update payload and retry
        payload["validation_error"] = error_message
        payload["attempt"] = attempt

        self._log_execution(
            "invalid_interrupt_response",
            task_id=payload.get("task_id"),
            extra={"attempt": attempt, "error": error_message}
        )

    # Max attempts exceeded
    raise RuntimeError(f"Max validation attempts ({max_attempts}) exceeded")
```

**CLI Interrupt Loop** (cli.py:25-135):
```python
async def run_with_interrupts(orchestrator, thread_id, max_tasks):
    """Run orchestrator with interrupt handling."""
    config = {"configurable": {"thread_id": thread_id}}

    # Check if resuming
    existing_state = orchestrator.app.get_state(config)
    is_resume = existing_state.values != {}

    input_data = None if is_resume else orchestrator._create_initial_state()

    while True:
        # Invoke graph (may interrupt)
        result = await orchestrator.app.ainvoke(input_data, config)

        # Check for interrupt
        if "__interrupt__" in result:
            interrupt_data = result["__interrupt__"][0]

            # Display interrupt
            display_interrupt(interrupt_data)

            # Collect response
            response = collect_response(interrupt_data)

            # Resume with response
            input_data = Command(resume=response)
            continue  # Next iteration resumes

        # Check for completion
        next_action = result.get("next_action", "")
        if next_action in ["complete", "pause_rate_limited"]:
            break

        # Update input for next iteration
        input_data = None  # Use state from graph
```

### Benefits

1. **Simpler Code**: No file watching, no polling loops
2. **Type Safety**: Structured payloads and responses
3. **Validation**: Automatic re-prompting on invalid input
4. **State Persistence**: Interrupts are checkpointed automatically
5. **Rich UI**: Interactive CLI with formatted prompts

## Idempotent Tasks (Phase 3)

### Architecture

```
┌────────────────────────────────────────────────────────┐
│                  Orchestrator Node                      │
├────────────────────────────────────────────────────────┤
│                                                        │
│  async def _execute_implementation_node(self, state):  │
│      # Call task-wrapped function                      │
│      result = await execute_claude_cli(                │
│          prompt=prompt,                                │
│          task_id=task_id,                              │
│          attempt=attempt,  # Cache key component       │
│      )                                                 │
│                                                        │
└──────────────────────┬─────────────────────────────────┘
                       │
                       ▼
┌────────────────────────────────────────────────────────┐
│           @task Decorated Function                      │
├────────────────────────────────────────────────────────┤
│                                                        │
│  @task                                                 │
│  async def execute_claude_cli(...) -> TaskResult:     │
│      # LangGraph checks cache first                    │
│      # If same inputs seen before, return cached       │
│      # Otherwise, execute                              │
│                                                        │
│      input_hash = _compute_input_hash(...)             │
│                                                        │
│      # Execute subprocess                              │
│      process = await asyncio.create_subprocess_exec... │
│      stdout, stderr = await process.communicate()      │
│                                                        │
│      return TaskExecutionResult(...)                   │
│                                                        │
└──────────────────────┬─────────────────────────────────┘
                       │
                       ├─────────────────┐
                       │                 │
                       ▼                 ▼
         ┌──────────────────┐   ┌───────────────┐
         │  LangGraph       │   │   Fallback    │
         │  @task Cache     │   │  File Cache   │
         │  (in-memory)     │   │ (.task_cache/)│
         └──────────────────┘   └───────────────┘
```

### Implementation Details

**Task Decorator Usage** (tasks.py:121-228):
```python
@task
async def execute_claude_cli(
    prompt: str,
    tools: Optional[List[str]] = None,
    model: Optional[str] = None,
    timeout: float = 600.0,
    working_dir: Optional[Path] = None,
    task_id: str = "unknown",
    attempt: int = 1,  # Part of cache key!
    use_fallback_idempotency: bool = True,
) -> TaskExecutionResult:
    """
    Execute Claude CLI with idempotent semantics.

    The @task decorator ensures same inputs → same outputs.
    Different attempt numbers create different cache keys.
    """
    start_time = time.time()

    # Compute hash for fallback cache
    input_hash = _compute_input_hash(
        prompt, tools, model, working_dir, task_id, attempt
    )

    # Build command
    cmd = ["claude", "--print", "--allowedTools", ",".join(tools)]
    if model:
        cmd.extend(["--model", model])

    # Execute
    try:
        process = await asyncio.create_subprocess_exec(
            *cmd,
            stdin=asyncio.subprocess.PIPE,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            cwd=working_dir or Path.cwd(),
        )

        stdout, stderr = await asyncio.wait_for(
            process.communicate(input=prompt.encode()),
            timeout=timeout,
        )

        duration = time.time() - start_time

        return TaskExecutionResult(
            success=process.returncode == 0,
            output=stdout.decode(),
            error=stderr.decode() if stderr else None,
            backend="claude_code",
            model=model,
            duration_seconds=duration,
            exit_code=process.returncode,
            input_hash=input_hash,
        )

    except asyncio.TimeoutError:
        # Handle timeout...
```

**Fallback Idempotency** (tasks.py:578-653):
```python
class TaskIdempotencyFallback:
    """File-based fallback when @task doesn't work."""

    def __init__(self, cache_dir: Optional[Path] = None):
        if cache_dir is None:
            cache_dir = Path.cwd() / ".task_cache"
        self.cache_dir = cache_dir
        self.cache_dir.mkdir(parents=True, exist_ok=True)

    def _marker_path(self, input_hash: str) -> Path:
        """Get cache file path."""
        return self.cache_dir / f"{input_hash}.json"

    def get_cached(self, input_hash: str) -> Optional[TaskExecutionResult]:
        """Get cached result."""
        marker_path = self._marker_path(input_hash)

        if not marker_path.exists():
            return None

        try:
            with open(marker_path, 'r') as f:
                data = json.load(f)
            return TaskExecutionResult.from_dict(data)
        except Exception:
            return None

    def cache_result(self, input_hash: str, result: TaskExecutionResult):
        """Cache result."""
        marker_path = self._marker_path(input_hash)

        try:
            with open(marker_path, 'w') as f:
                json.dump(result.to_dict(), f)
        except Exception as e:
            logger.warning(f"Failed to cache result: {e}")
```

**Orchestrator Integration** (orchestrator.py:594-659):
```python
async def _execute_with_task_failover(
    self,
    prompt: str,
    task_id: str,
    attempt: int,
    timeout: float,
) -> TaskExecutionResult:
    """
    Execute with task failover and fallback idempotency.
    """
    # Get fallback tracker
    tracker = _get_fallback_tracker()

    # Try each backend in priority order
    for backend_type in self.config.backend_priority:
        backend_config = self.config.backends[backend_type]

        # Determine task function
        if backend_type == "claude_code":
            task_func = execute_claude_cli
            tools = ["Read", "Write", "Edit", "Bash"]
            model = backend_config.model
        elif backend_type == "copilot":
            task_func = execute_copilot_cli
            tools = ["read", "write", "edit", "bash"]
            model = None
        else:
            continue

        # Execute task function
        # @task provides LangGraph caching
        # Fallback tracker provides file-based caching
        result = await task_func(
            prompt=prompt,
            tools=tools,
            model=model,
            timeout=timeout,
            working_dir=self.config.project_root,
            task_id=task_id,
            attempt=attempt,  # Different attempts = different cache keys
            use_fallback_idempotency=True,
        )

        # Check for success or rate limiting
        if result.success or not _is_rate_limited(result.output, result.error):
            return result

    # All backends failed
    return TaskExecutionResult(
        success=False,
        error="All backends exhausted",
        backend="none",
    )
```

### Benefits

1. **Graph Replay Safety**: Checkpoints can be replayed without duplicate work
2. **Consistent Results**: Same inputs always produce same outputs
3. **Performance**: Cached results skip expensive CLI calls
4. **Reliability**: Fallback cache works even if @task fails
5. **Debugging**: Input hashes identify duplicate work

## Long-Term Memory (Phase 4)

### Architecture

```
┌────────────────────────────────────────────────────────┐
│                     Orchestrator                        │
├────────────────────────────────────────────────────────┤
│                                                        │
│  self.memory_store = InMemoryStore(index=embeddings)   │
│  self.memory_manager = MemoryManager(store, config)    │
│  self.app = graph.compile(store=self.memory_store)     │
│                                                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │  _execute_implementation_node:                   │  │
│  │                                                  │  │
│  │  # Inject context from past patterns            │  │
│  │  impl_context = memory_manager.                 │  │
│  │      get_implementation_context(task_desc)      │  │
│  │  error_context = memory_manager.                │  │
│  │      get_error_avoidance_context(task_desc)     │  │
│  │                                                  │  │
│  │  prompt += impl_context + error_context         │  │
│  └──────────────────────────────────────────────────┘  │
│                                                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │  _update_ground_truth_node:                      │  │
│  │                                                  │  │
│  │  if success:                                     │  │
│  │      memory_manager.store_implementation_pattern│  │
│  │  elif error:                                     │  │
│  │      memory_manager.store_error_pattern         │  │
│  └──────────────────────────────────────────────────┘  │
│                                                        │
└──────────────────────┬─────────────────────────────────┘
                       │
                       ▼
┌────────────────────────────────────────────────────────┐
│              Memory Manager                             │
├────────────────────────────────────────────────────────┤
│                                                        │
│  Namespaces:                                           │
│  • ("sharpy", "implementation_patterns")               │
│  • ("sharpy", "error_patterns")                        │
│  • ("sharpy", "codebase_knowledge")                    │
│  • ("sharpy", "spec_patterns")                         │
│                                                        │
│  Methods:                                              │
│  • store_implementation_pattern()                      │
│  • store_error_pattern()                               │
│  • search_patterns(query, namespace)                   │
│  • get_implementation_context()                        │
│  • get_error_avoidance_context()                       │
│                                                        │
└──────────────────────┬─────────────────────────────────┘
                       │
                       ▼
┌────────────────────────────────────────────────────────┐
│           InMemoryStore (LangGraph)                     │
├────────────────────────────────────────────────────────┤
│                                                        │
│  Storage:                                              │
│  • Patterns stored by namespace                        │
│  • In-memory for speed (can be swapped for SQLite)    │
│                                                        │
│  Search:                                               │
│  • Semantic search (if embeddings configured)          │
│  • Exact key matching (if no embeddings)               │
│                                                        │
│  Embeddings (optional):                                │
│  • OpenAI: text-embedding-3-small                      │
│  • Local: sentence-transformers (all-MiniLM-L6-v2)    │
│                                                        │
└────────────────────────────────────────────────────────┘
```

### Implementation Details

**Memory Store Creation** (orchestrator.py:349-396):
```python
def _create_memory_store(self) -> Optional[InMemoryStore]:
    """Create and configure memory store."""
    if not self.config.memory.enabled:
        return None

    # Create store
    store = InMemoryStore()

    # Configure embeddings if provider specified
    if self.config.memory.embedding_provider == "openai":
        try:
            from langchain_openai import OpenAIEmbeddings

            embeddings = OpenAIEmbeddings(
                model=self.config.memory.openai_embedding_model
            )
            store = InMemoryStore(index={"embed": embeddings})
            print(f"  Memory store: Enabled with OpenAI embeddings")
        except ImportError:
            print("  Memory store: Warning - langchain-openai not installed")

    elif self.config.memory.embedding_provider == "local":
        try:
            from langchain_community.embeddings import HuggingFaceEmbeddings

            embeddings = HuggingFaceEmbeddings(
                model_name=self.config.memory.local_embedding_model
            )
            store = InMemoryStore(index={"embed": embeddings})
            print(f"  Memory store: Enabled with local embeddings")
        except ImportError:
            print("  Memory store: Warning - sentence-transformers not installed")
    else:
        print("  Memory store: Enabled with exact key matching")

    return store
```

**Pattern Storage** (memory.py:139-196):
```python
def store_implementation_pattern(
    self,
    task_type: str,
    description: str,
    solution: str,
    files: Optional[list[str]] = None,
    tags: Optional[list[str]] = None,
    task_id: Optional[str] = None,
    metadata: Optional[dict[str, Any]] = None,
) -> str:
    """Store successful implementation pattern."""
    if not self.config.enabled or not self.store:
        return ""

    pattern_id = self._get_next_id("impl")

    pattern = Pattern(
        id=pattern_id,
        namespace=self.NS_IMPLEMENTATION,
        task_type=task_type,
        description=self._truncate_value(description),
        solution=self._truncate_value(solution),
        files=files or [],
        tags=tags or [],
        success=True,
        metadata=metadata or {},
    )

    if task_id:
        pattern.metadata["task_id"] = task_id

    try:
        self.store.put(
            namespace=self.NS_IMPLEMENTATION,
            key=pattern_id,
            value=pattern.to_dict(),
        )
        logger.info(f"Stored implementation pattern: {pattern_id}")
        return pattern_id
    except Exception as e:
        logger.error(f"Failed to store implementation pattern: {e}")
        return ""
```

**Pattern Search** (memory.py:296-332):
```python
def search_patterns(
    self,
    query: str,
    namespace: tuple[str, ...],
    limit: Optional[int] = None,
) -> list[Pattern]:
    """Search for patterns matching query."""
    if not self.config.enabled or not self.store:
        return []

    limit = limit or self.config.max_patterns_per_query

    try:
        # Use search method (works with or without embeddings)
        results = self.store.search(
            namespace,  # Positional argument
            query=query if query else "",
            limit=limit if limit else 100,
        )

        # Convert results to Pattern objects
        patterns = []
        for item in results:
            try:
                pattern = Pattern.from_store_item(item)
                patterns.append(pattern)
            except Exception as e:
                logger.warning(f"Failed to parse pattern: {e}")

        return patterns

    except Exception as e:
        logger.error(f"Pattern search failed: {e}")
        return []
```

**Context Generation** (memory.py:336-393):
```python
def get_implementation_context(self, task_description: str) -> str:
    """Get relevant implementation patterns as context."""
    patterns = self.search_patterns(
        query=task_description,
        namespace=self.NS_IMPLEMENTATION,
    )

    if not patterns:
        return ""

    context_parts = ["## Relevant Past Implementations\n"]
    for pattern in patterns:
        context_parts.append(f"### {pattern.task_type}")
        context_parts.append(f"**Description:** {pattern.description}")
        context_parts.append(f"**Solution:**\n{pattern.solution}")
        if pattern.files:
            context_parts.append(f"**Files:** {', '.join(pattern.files)}")
        context_parts.append("")

    return "\n".join(context_parts)

def get_error_avoidance_context(self, task_description: str) -> str:
    """Get warnings about past errors."""
    patterns = self.search_patterns(
        query=task_description,
        namespace=self.NS_ERRORS,
    )

    if not patterns:
        return ""

    context_parts = ["## Common Errors to Avoid\n"]
    for pattern in patterns:
        context_parts.append(f"### {pattern.task_type}")
        context_parts.append(f"**Error:** {pattern.description}")
        error_msg = pattern.metadata.get("error_message", "")
        if error_msg:
            context_parts.append(f"**Message:** {error_msg}")
        context_parts.append(f"**Solution:** {pattern.solution}")
        context_parts.append("")

    return "\n".join(context_parts)
```

**Context Injection** (orchestrator.py:794-805):
```python
# In _execute_implementation_node
if self.memory_manager and self.config.memory.enabled:
    task_desc = task_data["description"]

    # Get relevant implementation patterns
    impl_context = self.memory_manager.get_implementation_context(task_desc)
    if impl_context:
        prompt += f"\n\n{impl_context}"

    # Get error avoidance patterns
    error_context = self.memory_manager.get_error_avoidance_context(task_desc)
    if error_context:
        prompt += f"\n\n{error_context}"
```

**Pattern Storage After Completion** (orchestrator.py:2819-2852):
```python
# In _update_ground_truth_node
try:
    if execution.success and tests_ok:
        # Store successful implementation pattern
        solution = self._extract_solution_summary(execution_result)
        task_type = task_data.get("phase", "implementation")

        self.memory_manager.store_implementation_pattern(
            task_type=task_type,
            description=task_desc,
            solution=solution,
            files=files,
            tags=[task_type, "success"],
            task_id=task_data["id"],
            metadata={
                "backend": execution.backend,
                "tests_passed": execution.tests_passed,
                "attempt_number": execution.attempt_number,
            },
        )

    elif execution.error_message:
        # Store error pattern for future avoidance
        error_type = self._categorize_error(execution.error_message)
        solution = self._extract_solution_summary(execution_result)

        self.memory_manager.store_error_pattern(
            error_type=error_type,
            description=task_desc,
            error_message=execution.error_message,
            solution=solution if solution else "No solution found",
            files=files,
            task_id=task_data["id"],
        )

except Exception as e:
    # Don't fail task on memory errors
    print(f"Warning: Failed to store pattern in memory: {e}")
```

### Benefits

1. **Learning**: System improves over time
2. **Context**: Relevant past solutions injected into prompts
3. **Error Avoidance**: Past mistakes documented and avoided
4. **Semantic Search**: Find similar patterns even with different wording
5. **Scalability**: Patterns grow with usage

## State Management

### Ground Truth

The ground truth (`state/ground_truth.json`) is the source of truth for task state:

```python
@dataclass
class GroundTruth:
    """Complete project state."""
    task_list_path: Path
    phases: list[Phase]
    overall_progress: float
    total_tasks: int
    completed_tasks: int
    failed_tasks: int
    pending_tasks: int

    def get_task_by_id(self, task_id: str) -> Optional[Task]:
        """Find task by ID."""

    def update_task_status(self, task_id: str, status: TaskStatus):
        """Update task status."""

    def save(self, path: Path):
        """Persist to disk."""
```

### Checkpoint State

LangGraph checkpoints store the `OrchestratorState`:

- **Persisted**: Every node transition
- **Thread-specific**: Isolated by thread_id
- **Resumable**: Full state restoration
- **Queryable**: Can inspect any checkpoint

### Memory State

Patterns stored in memory namespaces:

- **Namespaces**: Separate implementation, errors, codebase, spec
- **Searchable**: Semantic or exact match
- **Persistent**: Survives restarts (when using persistent store)
- **Manageable**: CLI commands for search, stats, clear

## Error Handling & Recovery

### Rate Limiting

**Detection** (orchestrator.py:2873-2876):
```python
is_rate_limited = (
    "exhausted" in error_msg.lower() or
    "rate limit" in error_msg.lower()
)
```

**Recovery** (orchestrator.py:2878-2900):
```python
if is_rate_limited:
    pause_hours = self.config.rate_limit_pause_hours
    resume_time = datetime.now() + timedelta(hours=pause_hours)

    print(f"\n{'='*60}")
    print(f"⏸️  SESSION PAUSED - Rate limit reached")
    print(f"{'='*60}")
    print(f"\nSession checkpointed. Resume after {pause_hours} hours.")
    print(f"Estimated resume time: {resume_time}")
    print(f"\n📌 Session saved with thread ID: {self._current_thread_id}")
    print(f"\n▶️  To resume: ./auto_builder.sh run --thread-id {self._current_thread_id}")

    return {
        **state,
        "next_action": "pause_rate_limited",  # Routes to END
        "messages": [f"Session paused due to rate limiting."]
    }
```

### Crashes & Interruptions

**Automatic Recovery**:
1. Checkpoint saved before crash
2. Resume with `--thread-id`
3. Graph continues from last checkpoint
4. No duplicate work (thanks to @task)

**Manual Recovery**:
```bash
# List available sessions
./auto_builder.sh run --list-sessions

# Resume specific session
./auto_builder.sh run --thread-id sharpy-build-20260114-153045
```

### Invalid States

**Validation**:
- Type checking (TypedDict)
- Required fields (TypedDict)
- Interrupt response validation
- State transitions (LangGraph routing)

**Handling**:
- Log error
- Return to safe state
- Prompt for correction (interrupts)
- Fail gracefully

## Performance & Scalability

### Checkpoint Database

**Size Management**:
- Automatic cleanup every N checkpoints
- Configurable retention per thread
- Manual cleanup commands

**Performance**:
- SQLite with indexes (LangGraph handles this)
- Async operations (check_same_thread=False)
- Minimal overhead (<100ms per checkpoint)

### Memory Patterns

**Size Management**:
- Truncate long values (max_pattern_length)
- Limit total patterns (max_patterns_stored)
- Namespace-specific cleanup

**Performance**:
- In-memory for speed (InMemoryStore)
- Embedding search cached
- Configurable result limits

### Task Caching

**Size Management**:
- File-based cache in `.task_cache/`
- One file per unique input hash
- Safe to delete (regenerates on need)

**Performance**:
- Instant cache hits
- No network calls
- Parallel-safe (unique hashes)

### Recommended Limits

| Setting | Recommended | Notes |
|---------|-------------|-------|
| max_checkpoints_per_thread | 100 | ~10MB per thread |
| max_patterns_stored | 10000 | ~50MB with truncation |
| max_pattern_length | 1000 | Truncate long solutions |
| cleanup_interval | 50 | Run every 50 checkpoints |

### Scaling Considerations

1. **Multiple Sessions**: Each thread is independent
2. **Parallel Execution**: Not supported (sequential by design)
3. **Large Codebases**: Memory patterns help with context
4. **Long-Running**: Checkpoints enable multi-day execution
5. **Disk Space**: Monitor checkpoint and pattern database sizes

---

## References

- [LangGraph Documentation](https://docs.langchain.com/oss/python/langgraph/)
- [LangGraph Persistence](https://docs.langchain.com/oss/python/langgraph/persistence)
- [LangGraph Interrupts](https://docs.langchain.com/oss/python/langgraph/interrupts)
- [LangGraph Durable Execution](https://docs.langchain.com/oss/python/langgraph/durable-execution)
- [LangGraph Memory](https://docs.langchain.com/oss/python/langgraph/add-memory)
- [Implementation Plans](../../docs/implementation_planning/)
