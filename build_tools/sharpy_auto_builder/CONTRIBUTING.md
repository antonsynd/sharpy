# Contributing to Sharpy Auto Builder

Thank you for your interest in contributing to the Sharpy Auto Builder! This document provides guidelines and best practices for contributors.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Code Style](#code-style)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Architecture Guidelines](#architecture-guidelines)
- [Common Tasks](#common-tasks)

## Getting Started

### Prerequisites

- Python 3.9+
- Git
- At least one of:
  - Claude Code CLI (`claude`)
  - GitHub Copilot CLI (`copilot`)

### Development Setup

1. **Clone the repository**:
   ```bash
   cd build_tools/sharpy_auto_builder
   ```

2. **Create virtual environment**:
   ```bash
   python3 -m venv .venv
   source .venv/bin/activate  # On Windows: .venv\Scripts\activate
   ```

3. **Install dependencies**:
   ```bash
   pip install -r requirements.txt
   pip install -r requirements-dev.txt  # Testing and development tools
   ```

4. **Run tests to verify setup**:
   ```bash
   pytest build_tools/tests/
   ```

## Code Style

### Python Style Guide

We follow PEP 8 with these specific conventions:

#### Type Annotations

**Use modern Python type hints** (3.9+ syntax):

```python
# ✅ Good: Modern syntax
def process_tasks(tasks: list[str]) -> dict[str, Any]:
    results: dict[str, int] = {}
    return results

# ❌ Bad: Old typing module syntax
from typing import List, Dict, Any
def process_tasks(tasks: List[str]) -> Dict[str, Any]:
    results: Dict[str, int] = {}
    return results
```

#### Dataclasses

Use `@dataclass` for structured data:

```python
from dataclasses import dataclass, field

@dataclass
class TaskResult:
    """Result from task execution."""
    success: bool
    output: str
    error: Optional[str] = None
    metadata: dict[str, Any] = field(default_factory=dict)
```

#### Async/Await

Use `async`/`await` for I/O operations:

```python
# ✅ Good: Async subprocess
async def execute_cli(command: list[str]) -> str:
    process = await asyncio.create_subprocess_exec(
        *command,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
    )
    stdout, stderr = await process.communicate()
    return stdout.decode()

# ❌ Bad: Blocking subprocess
def execute_cli(command: list[str]) -> str:
    import subprocess
    result = subprocess.run(command, capture_output=True)
    return result.stdout.decode()
```

### Documentation

#### Docstrings

Use Google-style docstrings:

```python
def store_pattern(
    self,
    task_type: str,
    description: str,
    solution: str,
) -> str:
    """
    Store a successful implementation pattern.

    Args:
        task_type: Type of task (e.g., "component_creation")
        description: Description of what was implemented
        solution: The implementation code or approach

    Returns:
        Pattern ID (empty string if memory disabled)

    Raises:
        ValueError: If task_type is invalid
    """
```

#### Comments

```python
# ✅ Good: Explain why, not what
# Use exponential backoff to avoid overwhelming the API
backoff_time = base_delay * (2 ** attempt)

# ❌ Bad: Obvious comments
# Set backoff time
backoff_time = base_delay * (2 ** attempt)
```

### Project Structure

```
sharpy_auto_builder/
├── __init__.py           # Package exports only
├── config.py             # Configuration (no business logic)
├── state.py              # State management (dataclasses)
├── orchestrator.py       # LangGraph state machine (main logic)
├── tasks.py              # @task decorated functions
├── memory.py             # Memory pattern management
├── cli.py                # CLI interface
└── ...                   # Other modules
```

## Testing

### Test Organization

```
build_tools/tests/
├── test_orchestrator_persistence.py   # Phase 1: Checkpoints
├── test_orchestrator_interrupts.py    # Phase 2: Interrupts
├── test_tasks.py                      # Phase 3: Idempotent tasks
├── test_memory.py                     # Phase 4: Memory patterns
└── ...                                # Other test files
```

### Writing Tests

#### Unit Tests

Test individual functions in isolation:

```python
import pytest
from sharpy_auto_builder.tasks import _compute_input_hash

def test_compute_input_hash_same_inputs():
    """Same inputs should produce same hash."""
    hash1 = _compute_input_hash("prompt", model="sonnet")
    hash2 = _compute_input_hash("prompt", model="sonnet")
    assert hash1 == hash2

def test_compute_input_hash_different_inputs():
    """Different inputs should produce different hashes."""
    hash1 = _compute_input_hash("prompt1")
    hash2 = _compute_input_hash("prompt2")
    assert hash1 != hash2
```

#### Integration Tests

Test multiple components together:

```python
import pytest
from pathlib import Path
from sharpy_auto_builder import Orchestrator, Config

@pytest.fixture
def orchestrator(tmp_path):
    """Create orchestrator with temporary state dir."""
    config = Config(
        project_root=Path.cwd(),
        state_dir=tmp_path / "state",
        task_list_path=tmp_path / "tasks.md",
    )
    config.ensure_directories()
    return Orchestrator(config)

def test_checkpoint_creation(orchestrator):
    """Test that checkpoints are created."""
    checkpoint_db = orchestrator.config.checkpoint_db_path
    assert checkpoint_db.exists()
    assert checkpoint_db.stat().st_size > 0
```

#### Async Tests

Use `pytest-asyncio` for async tests:

```python
import pytest

@pytest.mark.asyncio
async def test_execute_claude_cli():
    """Test Claude CLI execution."""
    from sharpy_auto_builder.tasks import execute_claude_cli

    result = await execute_claude_cli(
        prompt="print('hello')",
        tools=["Bash"],
        task_id="test",
        attempt=1,
    )

    assert result.success or result.error is not None  # Either success or error
    assert result.input_hash  # Hash should be computed
```

### Running Tests

```bash
# Run all tests
pytest

# Run specific test file
pytest build_tools/tests/test_tasks.py

# Run specific test
pytest build_tools/tests/test_tasks.py::test_compute_input_hash_same_inputs

# Run with coverage
pytest --cov=sharpy_auto_builder --cov-report=html

# Run with verbose output
pytest -v

# Run with print statements visible
pytest -s
```

## Submitting Changes

### Branch Naming

- `feature/description` - New features
- `fix/description` - Bug fixes
- `docs/description` - Documentation updates
- `refactor/description` - Code refactoring

### Commit Messages

Follow conventional commits:

```
type(scope): subject

body (optional)

footer (optional)
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`

Examples:
```
feat(memory): add semantic search with embeddings

Implements OpenAI and local embedding support for memory patterns.
Uses InMemoryStore with embedding index.

Related: #123

---

fix(orchestrator): handle rate limit during checkpoint

Rate limit errors now properly pause execution and save checkpoint
for resumption.

---

docs(readme): update with Phase 4 memory features

Added documentation for memory CLI commands and pattern storage.
```

### Pull Request Process

1. **Create branch** from `mainline`:
   ```bash
   git checkout mainline
   git pull
   git checkout -b feature/my-feature
   ```

2. **Make changes**:
   - Write code following style guidelines
   - Add/update tests
   - Update documentation

3. **Test locally**:
   ```bash
   pytest
   # Ensure all tests pass
   ```

4. **Commit changes**:
   ```bash
   git add .
   git commit -m "feat(scope): description"
   ```

5. **Push to GitHub**:
   ```bash
   git push origin feature/my-feature
   ```

6. **Create Pull Request**:
   - Target `mainline` branch
   - Fill out PR template
   - Link related issues
   - Request review

7. **Address review feedback**:
   - Make requested changes
   - Push updates
   - Re-request review

## Architecture Guidelines

### LangGraph Nodes

**Rules for node functions**:

1. **Must be idempotent** - Safe to re-run on checkpoint replay
2. **No side effects before interrupt** - Only read operations
3. **Return new state dict** - Don't mutate input state
4. **Handle errors gracefully** - Wrap in try/except

```python
# ✅ Good: Idempotent node
async def _my_node(self, state: OrchestratorState) -> OrchestratorState:
    task_data = state["current_task"]  # Read only

    # Idempotent operations
    files = self._get_files_changed()  # Git status (read-only)

    # Interrupt (pause point)
    response = interrupt({"type": "review", "files": files})

    # State update after interrupt
    return {
        **state,
        "human_response": response,
        "next_action": "commit_changes",
    }

# ❌ Bad: Side effects before interrupt
async def _bad_node(self, state: OrchestratorState) -> OrchestratorState:
    self._send_email()  # Side effect! Will re-run on replay
    response = interrupt({"type": "review"})
    return {**state, "response": response}
```

### Memory Pattern Storage

**Always wrap in try/except** - Don't fail tasks on memory errors:

```python
# ✅ Good: Graceful failure
try:
    self.memory_manager.store_implementation_pattern(
        task_type="lexer",
        description=task_desc,
        solution=summary,
    )
except Exception as e:
    print(f"Warning: Failed to store pattern: {e}")
    # Continue execution - don't fail task

# ❌ Bad: Uncaught exception fails task
self.memory_manager.store_implementation_pattern(...)  # Can raise
```

### Task Decorator Usage

**Use `@task` for all side-effecting operations**:

```python
# ✅ Good: Task-wrapped
@task
async def execute_cli(prompt: str, attempt: int) -> Result:
    # Side-effecting operation (subprocess)
    process = await asyncio.create_subprocess_exec(...)
    return Result(...)

# ❌ Bad: Direct side effects
async def execute_cli(prompt: str) -> Result:
    # No @task - will re-run on checkpoint replay
    process = await asyncio.create_subprocess_exec(...)
    return Result(...)
```

### State Management

**Always return new state dict**:

```python
# ✅ Good: New dict
async def _node(self, state: OrchestratorState) -> OrchestratorState:
    return {
        **state,  # Spread existing state
        "new_field": "value",
        "updated_field": state["updated_field"] + 1,
    }

# ❌ Bad: Mutating state
async def _node(self, state: OrchestratorState) -> OrchestratorState:
    state["new_field"] = "value"  # Mutation!
    return state
```

## Common Tasks

### Adding a New CLI Command

1. **Add parser** in `cli.py`:
   ```python
   # In main() function
   your_parser = subparsers.add_parser(
       "your-command",
       help="Your command description"
   )
   your_parser.add_argument("--option", help="Option help")
   ```

2. **Add command function**:
   ```python
   def cmd_your_command(args):
       """Your command implementation."""
       config = Config()
       # Load from config file if exists
       config_path = config.state_dir / "config.json"
       if config_path.exists():
           config = Config.load(config_path)

       # Your logic here
       print("Command executed")
   ```

3. **Register command**:
   ```python
   commands = {
       # ... existing commands
       "your-command": cmd_your_command,
   }
   ```

4. **Add tests**:
   ```python
   # test_cli.py
   def test_cmd_your_command(tmp_path):
       """Test your command."""
       # Setup
       # Execute
       # Assert
   ```

### Adding a New LangGraph Node

1. **Add node method** to `Orchestrator`:
   ```python
   async def _your_node(self, state: OrchestratorState) -> OrchestratorState:
       """Your node description."""
       task_data = state["current_task"]

       # Your logic (must be idempotent)

       return {
           **state,
           "next_action": "next_step",
           "messages": ["Node completed"],
       }
   ```

2. **Register in graph** (`_build_graph`):
   ```python
   graph.add_node("your_node", self._your_node)
   ```

3. **Add routing**:
   ```python
   # From previous node
   graph.add_edge("previous_node", "your_node")

   # To next node (conditional or direct)
   graph.add_conditional_edges(
       "your_node",
       self._route_after_your_node,
       {
           "success": "next_node",
           "error": "handle_error",
       }
   )
   ```

4. **Add routing method** (if conditional):
   ```python
   def _route_after_your_node(self, state: OrchestratorState) -> str:
       """Route after your node."""
       if state.get("success"):
           return "success"
       else:
           return "error"
   ```

5. **Add tests**:
   ```python
   # test_orchestrator_*.py
   @pytest.mark.asyncio
   async def test_your_node(orchestrator):
       """Test your node."""
       state = {...}  # Initial state
       result = await orchestrator._your_node(state)
       assert result["next_action"] == "next_step"
   ```

### Adding a New Backend

1. **Add backend type** to `BackendType` enum:
   ```python
   # backends.py
   class BackendType(str, Enum):
       YOUR_BACKEND = "your_backend"
   ```

2. **Implement `Backend` interface**:
   ```python
   class YourBackend(Backend):
       """Your backend implementation."""

       def __init__(self, config: BackendConfig):
           super().__init__(config)

       async def execute(
           self,
           prompt: str,
           tools: list[str],
       ) -> ExecutionResult:
           """Execute prompt using your backend."""
           # Your implementation
           return ExecutionResult(
               success=True,
               output="...",
               backend="your_backend",
           )
   ```

3. **Add configuration**:
   ```python
   # config.py
   backends: dict[BackendType, BackendConfig] = field(
       default_factory=lambda: {
           # ... existing backends
           "your_backend": BackendConfig(
               name="your_backend",
               enabled=True,
               # ... other config
           ),
       }
   )
   ```

4. **Register in `BackendManager`**:
   ```python
   # backends.py BackendManager.__init__
   if "your_backend" in config.backends:
       backend_config = config.backends["your_backend"]
       self.backends["your_backend"] = YourBackend(backend_config)
   ```

5. **Add tests**:
   ```python
   # test_backends.py
   @pytest.mark.asyncio
   async def test_your_backend():
       """Test your backend."""
       # Setup
       # Execute
       # Assert
   ```

### Adding Memory Namespace

1. **Add namespace constant** to `MemoryManager`:
   ```python
   # memory.py
   class MemoryManager:
       NS_YOUR_NAMESPACE = ("sharpy", "your_namespace")
   ```

2. **Add storage method**:
   ```python
   def store_your_pattern(
       self,
       field1: str,
       field2: str,
   ) -> str:
       """Store your pattern type."""
       if not self.config.enabled or not self.store:
           return ""

       pattern_id = self._get_next_id("your_prefix")

       pattern = Pattern(
           id=pattern_id,
           namespace=self.NS_YOUR_NAMESPACE,
           # ... other fields
       )

       try:
           self.store.put(
               namespace=self.NS_YOUR_NAMESPACE,
               key=pattern_id,
               value=pattern.to_dict(),
           )
           return pattern_id
       except Exception as e:
           logger.error(f"Failed to store pattern: {e}")
           return ""
   ```

3. **Add CLI support** (if needed):
   ```python
   # cli.py - update namespace_map
   namespace_map = {
       # ... existing
       "your_namespace": MemoryManager.NS_YOUR_NAMESPACE,
   }
   ```

## Questions or Issues?

- **Documentation**: See [README.md](README.md) and [ARCHITECTURE.md](ARCHITECTURE.md)
- **Issues**: Open an issue on GitHub
- **Questions**: Ask in pull request comments

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.
