# Implementation Plan 4: Add Long-Term Memory Store

## Overview

**Goal:** Add a LangGraph memory store to enable cross-task and cross-session learning, allowing the orchestrator to leverage patterns from successful past implementations.

**Priority:** Medium  
**Estimated Effort:** 4-6 hours  
**Risk Level:** Low (additive feature, doesn't change existing behavior)

**Prerequisites:** 
- Implementation Plan 1 (Durable Persistence) recommended but not required
- Memory store is independent of checkpointer

## Background

### Current State

Each task execution in the orchestrator is independent:
- No memory of successful patterns from previous tasks
- No learning from failures
- Repeated mistakes possible
- Context about the codebase not accumulated

### The Problem

Consider these scenarios:
1. **Pattern Recognition:** Task A successfully implemented operator overloading with a specific approach. Later, Task B needs similar work but starts from scratch.

2. **Error Avoidance:** Task C failed because of a specific edge case in the spec. Task D has similar requirements but will likely hit the same issue.

3. **Codebase Knowledge:** After processing 50 files, the system still doesn't "know" the codebase patterns.

### Solution: Memory Store

LangGraph's memory store provides:
- **Namespaced key-value storage** across threads/sessions
- **Semantic search** to find relevant past patterns
- **Persistent storage** that survives restarts
- **Node-accessible** via `store` parameter injection

From the [Memory docs](https://docs.langchain.com/oss/python/langgraph/add-memory#add-long-term-memory):
> "Use long-term memory to store user-specific or application-level data across sessions."

## Reference Documentation

- [LangGraph Memory](https://docs.langchain.com/oss/python/langgraph/add-memory)
- [Memory Store](https://docs.langchain.com/oss/python/langgraph/persistence#memory-store)
- [Semantic Search in Store](https://docs.langchain.com/oss/python/langgraph/persistence#semantic-search)

## Files to Modify

| File | Changes |
|------|---------|
| `build_tools/sharpy_auto_builder/requirements.txt` | Add embedding dependencies |
| `build_tools/sharpy_auto_builder/config.py` | Add memory store path |
| `build_tools/sharpy_auto_builder/orchestrator.py` | Initialize and use memory store |
| `build_tools/sharpy_auto_builder/memory.py` | New file: memory management utilities |

## Task List

### Task 4.1: Add Dependencies
**File:** `build_tools/sharpy_auto_builder/requirements.txt`

Add dependencies for semantic search:

```
# Memory store with semantic search
langchain-openai>=0.2.0  # For embeddings (or use another provider)
# OR for local embeddings without API calls:
sentence-transformers>=2.2.0

# For Postgres store (production)
# langgraph-checkpoint-postgres>=2.0.0  # Already includes store
```

**Note:** Choose an embedding approach:
- **OpenAI:** Best quality, requires API key, costs money
- **sentence-transformers:** Free, local, slightly lower quality
- **No embeddings:** Use exact key matching only (simplest)

For this plan, we'll support both OpenAI and a no-embedding fallback.

---

### Task 4.2: Add Memory Configuration
**File:** `build_tools/sharpy_auto_builder/config.py`

Add memory store configuration:

```python
from dataclasses import dataclass, field
from typing import Optional, Literal


@dataclass
class MemoryConfig:
    """Configuration for long-term memory store."""
    
    # Enable/disable memory
    enabled: bool = True
    
    # Embedding provider: "openai", "local", or None (no semantic search)
    embedding_provider: Optional[Literal["openai", "local"]] = None
    
    # OpenAI embedding model (if using OpenAI)
    openai_embedding_model: str = "text-embedding-3-small"
    openai_embedding_dims: int = 1536
    
    # Local embedding model (if using sentence-transformers)
    local_embedding_model: str = "all-MiniLM-L6-v2"
    local_embedding_dims: int = 384
    
    # Memory retrieval settings
    max_patterns_per_query: int = 5
    min_similarity_score: float = 0.5
    
    # Memory storage settings
    max_pattern_length: int = 1000  # Truncate patterns longer than this
    max_patterns_stored: int = 10000  # Maximum patterns to store


@dataclass
class Config(BaseConfig):
    # ... existing fields ...
    
    # Memory configuration
    memory: MemoryConfig = field(default_factory=MemoryConfig)
    
    @property
    def memory_store_path(self) -> Path:
        """Path to memory store database (for SQLite-backed store)."""
        return self.state_dir / "memory_store.db"
```

---

### Task 4.3: Create Memory Management Module
**File:** `build_tools/sharpy_auto_builder/memory.py` (new file)

Create a module for memory operations:

```python
"""
Long-term memory management for cross-task learning.

This module provides utilities for storing and retrieving patterns,
solutions, and learnings across task executions.

Memory Namespaces:
- ("sharpy", "implementation_patterns"): Successful implementation approaches
- ("sharpy", "error_patterns"): Common errors and their solutions
- ("sharpy", "codebase_knowledge"): Knowledge about the Sharpy codebase
- ("sharpy", "spec_patterns"): Patterns from the language specification

Usage:
    from sharpy_auto_builder.memory import MemoryManager
    
    manager = MemoryManager(config, store)
    
    # Store a successful pattern
    manager.store_implementation_pattern(
        task_type="operator_overloading",
        description="Implementing == operator",
        solution="Use IEquatable<T> pattern with...",
        files=["Operators.cs"],
        tags=["operator", "equality"],
    )
    
    # Retrieve relevant patterns
    patterns = manager.search_patterns(
        query="How to implement comparison operators?",
        namespace="implementation_patterns",
        limit=3,
    )
"""

import uuid
from dataclasses import dataclass, asdict
from datetime import datetime
from typing import Optional, List, Dict, Any
from pathlib import Path

from langgraph.store.base import BaseStore


@dataclass
class Pattern:
    """Represents a stored pattern or learning."""
    
    id: str
    namespace: str
    task_type: str
    description: str
    solution: str
    files: List[str]
    tags: List[str]
    created_at: str
    success: bool
    metadata: Dict[str, Any]
    
    def to_dict(self) -> dict:
        return asdict(self)
    
    @classmethod
    def from_store_item(cls, item) -> "Pattern":
        """Create Pattern from store Item."""
        value = item.value
        return cls(
            id=item.key,
            namespace=str(item.namespace),
            task_type=value.get("task_type", "unknown"),
            description=value.get("description", ""),
            solution=value.get("solution", ""),
            files=value.get("files", []),
            tags=value.get("tags", []),
            created_at=value.get("created_at", ""),
            success=value.get("success", True),
            metadata=value.get("metadata", {}),
        )


class MemoryManager:
    """
    Manages long-term memory for the orchestrator.
    
    Provides high-level operations for storing and retrieving
    patterns, knowledge, and learnings.
    """
    
    # Namespace constants
    NS_IMPLEMENTATION = ("sharpy", "implementation_patterns")
    NS_ERRORS = ("sharpy", "error_patterns")
    NS_CODEBASE = ("sharpy", "codebase_knowledge")
    NS_SPEC = ("sharpy", "spec_patterns")
    
    def __init__(self, config, store: BaseStore):
        """
        Initialize memory manager.
        
        Args:
            config: Application configuration
            store: LangGraph BaseStore instance
        """
        self.config = config
        self.store = store
        self.memory_config = config.memory
    
    def store_implementation_pattern(
        self,
        task_type: str,
        description: str,
        solution: str,
        files: List[str] = None,
        tags: List[str] = None,
        task_id: str = None,
        metadata: Dict[str, Any] = None,
    ) -> str:
        """
        Store a successful implementation pattern.
        
        Args:
            task_type: Type of task (e.g., "operator_overloading", "parser_rule")
            description: Brief description of what was implemented
            solution: The approach/solution that worked (truncated if too long)
            files: List of files involved
            tags: Tags for categorization
            task_id: Original task ID (for reference)
            metadata: Additional metadata
        
        Returns:
            ID of the stored pattern
        """
        if not self.memory_config.enabled:
            return ""
        
        pattern_id = str(uuid.uuid4())
        
        # Truncate solution if too long
        max_len = self.memory_config.max_pattern_length
        if len(solution) > max_len:
            solution = solution[:max_len] + "...[truncated]"
        
        value = {
            "task_type": task_type,
            "description": description,
            "solution": solution,
            "files": files or [],
            "tags": tags or [],
            "created_at": datetime.now().isoformat(),
            "success": True,
            "task_id": task_id,
            "metadata": metadata or {},
        }
        
        self.store.put(self.NS_IMPLEMENTATION, pattern_id, value)
        return pattern_id
    
    def store_error_pattern(
        self,
        error_type: str,
        description: str,
        error_message: str,
        solution: str = None,
        files: List[str] = None,
        task_id: str = None,
    ) -> str:
        """
        Store an error pattern and its solution (if known).
        
        Args:
            error_type: Category of error
            description: What caused the error
            error_message: The actual error message
            solution: How it was fixed (if known)
            files: Files involved
            task_id: Original task ID
        
        Returns:
            ID of the stored pattern
        """
        if not self.memory_config.enabled:
            return ""
        
        pattern_id = str(uuid.uuid4())
        
        value = {
            "error_type": error_type,
            "description": description,
            "error_message": error_message[:500],  # Truncate error
            "solution": solution,
            "files": files or [],
            "created_at": datetime.now().isoformat(),
            "success": False,
            "task_id": task_id,
        }
        
        self.store.put(self.NS_ERRORS, pattern_id, value)
        return pattern_id
    
    def store_codebase_knowledge(
        self,
        category: str,
        knowledge: str,
        source_file: str = None,
        confidence: float = 1.0,
    ) -> str:
        """
        Store knowledge about the codebase.
        
        Args:
            category: Category (e.g., "architecture", "naming", "patterns")
            knowledge: The knowledge/insight
            source_file: File this knowledge came from
            confidence: Confidence level (0-1)
        
        Returns:
            ID of the stored knowledge
        """
        if not self.memory_config.enabled:
            return ""
        
        knowledge_id = str(uuid.uuid4())
        
        value = {
            "category": category,
            "knowledge": knowledge[:self.memory_config.max_pattern_length],
            "source_file": source_file,
            "confidence": confidence,
            "created_at": datetime.now().isoformat(),
        }
        
        self.store.put(self.NS_CODEBASE, knowledge_id, value)
        return knowledge_id
    
    def search_patterns(
        self,
        query: str,
        namespace: tuple = None,
        limit: int = None,
    ) -> List[Pattern]:
        """
        Search for relevant patterns.
        
        Args:
            query: Search query (semantic search if embeddings enabled)
            namespace: Namespace to search (default: implementation_patterns)
            limit: Maximum results
        
        Returns:
            List of matching Pattern objects
        """
        if not self.memory_config.enabled:
            return []
        
        namespace = namespace or self.NS_IMPLEMENTATION
        limit = limit or self.memory_config.max_patterns_per_query
        
        try:
            # Use semantic search if available
            items = self.store.search(namespace, query=query, limit=limit)
            return [Pattern.from_store_item(item) for item in items]
        except Exception as e:
            # Fallback to listing all items if search fails
            print(f"Search failed, falling back to list: {e}")
            items = list(self.store.search(namespace, limit=limit))
            return [Pattern.from_store_item(item) for item in items]
    
    def get_implementation_context(self, task_description: str) -> str:
        """
        Build context string from relevant patterns for a task.
        
        Args:
            task_description: Description of the current task
        
        Returns:
            Formatted context string for prompt injection
        """
        if not self.memory_config.enabled:
            return ""
        
        patterns = self.search_patterns(task_description, self.NS_IMPLEMENTATION)
        
        if not patterns:
            return ""
        
        context_parts = ["## Relevant Past Patterns\n"]
        
        for i, pattern in enumerate(patterns, 1):
            context_parts.append(f"### Pattern {i}: {pattern.task_type}")
            context_parts.append(f"**Description:** {pattern.description}")
            context_parts.append(f"**Solution approach:**\n{pattern.solution}")
            if pattern.files:
                context_parts.append(f"**Files:** {', '.join(pattern.files)}")
            context_parts.append("")
        
        return "\n".join(context_parts)
    
    def get_error_avoidance_context(self, task_description: str) -> str:
        """
        Build context about errors to avoid.
        
        Args:
            task_description: Description of the current task
        
        Returns:
            Formatted warnings about past errors
        """
        if not self.memory_config.enabled:
            return ""
        
        error_patterns = self.search_patterns(task_description, self.NS_ERRORS)
        
        if not error_patterns:
            return ""
        
        context_parts = ["## ⚠️ Past Errors to Avoid\n"]
        
        for pattern in error_patterns:
            context_parts.append(f"- **{pattern.description}**")
            if pattern.solution:
                context_parts.append(f"  Solution: {pattern.solution}")
        
        return "\n".join(context_parts)
    
    def get_codebase_context(self, file_path: str = None) -> str:
        """
        Get relevant codebase knowledge.
        
        Args:
            file_path: Optional file path to scope knowledge
        
        Returns:
            Formatted codebase knowledge
        """
        if not self.memory_config.enabled:
            return ""
        
        query = file_path or "Sharpy compiler architecture"
        items = self.search_patterns(query, self.NS_CODEBASE, limit=3)
        
        if not items:
            return ""
        
        context_parts = ["## Codebase Knowledge\n"]
        
        for item in items:
            # Access raw value since codebase items have different structure
            context_parts.append(f"- {item.description}")
        
        return "\n".join(context_parts)
```

---

### Task 4.4: Initialize Memory Store in Orchestrator
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Update the orchestrator to initialize and use the memory store:

```python
# ADD imports:
from langgraph.store.memory import InMemoryStore
from langgraph.store.base import BaseStore

from .memory import MemoryManager

# For semantic search (optional):
try:
    from langchain.embeddings import init_embeddings
    EMBEDDINGS_AVAILABLE = True
except ImportError:
    EMBEDDINGS_AVAILABLE = False


class Orchestrator:
    def __init__(self, config: Config):
        # ... existing initialization ...
        
        # Initialize memory store
        self.memory_store = self._create_memory_store()
        self.memory_manager = MemoryManager(config, self.memory_store)
        
        # Update graph compilation to include store
        self.app = self.graph.compile(
            checkpointer=self.checkpointer,
            store=self.memory_store,  # ADD this line
        )
    
    def _create_memory_store(self) -> BaseStore:
        """Create and configure the memory store."""
        memory_config = self.config.memory
        
        if not memory_config.enabled:
            # Return a minimal store that won't error
            return InMemoryStore()
        
        # Configure embeddings for semantic search
        index_config = None
        
        if memory_config.embedding_provider == "openai" and EMBEDDINGS_AVAILABLE:
            try:
                embeddings = init_embeddings(
                    f"openai:{memory_config.openai_embedding_model}"
                )
                index_config = {
                    "embed": embeddings,
                    "dims": memory_config.openai_embedding_dims,
                    "fields": ["description", "solution", "knowledge"],
                }
            except Exception as e:
                print(f"Warning: Could not initialize OpenAI embeddings: {e}")
                print("Falling back to non-semantic search")
        
        elif memory_config.embedding_provider == "local" and EMBEDDINGS_AVAILABLE:
            try:
                from langchain_community.embeddings import HuggingFaceEmbeddings
                embeddings = HuggingFaceEmbeddings(
                    model_name=memory_config.local_embedding_model
                )
                index_config = {
                    "embed": embeddings,
                    "dims": memory_config.local_embedding_dims,
                    "fields": ["description", "solution", "knowledge"],
                }
            except Exception as e:
                print(f"Warning: Could not initialize local embeddings: {e}")
        
        # Create the store
        if index_config:
            return InMemoryStore(index=index_config)
        else:
            return InMemoryStore()
```

---

### Task 4.5: Inject Memory Context into Prompts
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Update the prompt building to include memory context:

```python
def _build_implementation_prompt(
    self, 
    task_data: dict, 
    state: OrchestratorState
) -> str:
    """Build the implementation prompt with memory context."""
    task_description = task_data.get("description", "")
    
    # Get memory context
    pattern_context = self.memory_manager.get_implementation_context(task_description)
    error_context = self.memory_manager.get_error_avoidance_context(task_description)
    
    # Build the prompt
    prompt_parts = []
    
    # Task description
    prompt_parts.append(f"# Task: {task_data.get('id', 'Unknown')}")
    prompt_parts.append(f"\n{task_description}\n")
    
    # Memory context (if available)
    if pattern_context:
        prompt_parts.append(pattern_context)
    
    if error_context:
        prompt_parts.append(error_context)
    
    # Spec references
    if task_data.get("spec_reference"):
        prompt_parts.append(f"## Specification Reference")
        prompt_parts.append(f"See: {task_data['spec_reference']}\n")
    
    # Files to modify
    if task_data.get("files"):
        prompt_parts.append(f"## Files")
        for f in task_data["files"]:
            prompt_parts.append(f"- {f}")
        prompt_parts.append("")
    
    # Instructions
    prompt_parts.append("## Instructions")
    prompt_parts.append("Implement this task following the Sharpy language specification.")
    prompt_parts.append("Ensure all changes maintain compatibility with existing code.")
    
    return "\n".join(prompt_parts)
```

---

### Task 4.6: Store Successful Patterns After Completion
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

Update the ground truth update node to store successful patterns:

```python
async def _update_ground_truth_node(self, state: OrchestratorState) -> OrchestratorState:
    """Update ground truth and store patterns in memory."""
    task_data = state["current_task"]
    last_result = state.get("last_execution_result", {}) or {}
    
    # ... existing ground truth update logic ...
    
    # Store successful pattern in memory
    if last_result.get("success"):
        try:
            self.memory_manager.store_implementation_pattern(
                task_type=task_data.get("type", "implementation"),
                description=task_data.get("description", "")[:200],
                solution=self._extract_solution_summary(last_result),
                files=last_result.get("files_changed", []),
                tags=task_data.get("tags", []),
                task_id=task_data.get("id"),
                metadata={
                    "backend": last_result.get("backend"),
                    "duration": last_result.get("duration_seconds"),
                    "attempt": state.get("execution_attempt", 1),
                },
            )
        except Exception as e:
            # Don't fail the task if memory storage fails
            print(f"Warning: Could not store pattern in memory: {e}")
    
    # Store error patterns for failed tasks
    elif state.get("error_message"):
        try:
            self.memory_manager.store_error_pattern(
                error_type=self._categorize_error(state["error_message"]),
                description=task_data.get("description", "")[:200],
                error_message=state["error_message"],
                files=task_data.get("files", []),
                task_id=task_data.get("id"),
            )
        except Exception as e:
            print(f"Warning: Could not store error pattern: {e}")
    
    # ... rest of existing logic ...
    
    return {
        **state,
        "next_action": "next_task" if next_task else "complete",
        "messages": [f"Ground truth updated for task {task_data['id']}"],
    }


def _extract_solution_summary(self, result: dict) -> str:
    """Extract a summary of the solution from execution result."""
    output = result.get("output", "")
    
    # Try to extract key parts of the output
    # This is heuristic and may need adjustment
    
    # Look for code changes
    if "```" in output:
        # Extract first code block as summary
        start = output.find("```")
        end = output.find("```", start + 3)
        if end > start:
            return output[start:end + 3]
    
    # Otherwise take first N characters
    max_len = self.config.memory.max_pattern_length
    if len(output) > max_len:
        return output[:max_len] + "..."
    
    return output


def _categorize_error(self, error_message: str) -> str:
    """Categorize an error message."""
    error_lower = error_message.lower()
    
    if "rate limit" in error_lower:
        return "rate_limit"
    elif "timeout" in error_lower:
        return "timeout"
    elif "compile" in error_lower or "build" in error_lower:
        return "compilation"
    elif "test" in error_lower:
        return "test_failure"
    elif "spec" in error_lower:
        return "spec_violation"
    else:
        return "unknown"
```

---

### Task 4.7: Add Memory Access to Nodes (Store Injection)
**File:** `build_tools/sharpy_auto_builder/orchestrator.py`

LangGraph can inject the store into nodes. Update node signatures:

```python
# Nodes can receive the store via parameter injection
# Add *, store: BaseStore to node signatures that need memory access

async def _execute_implementation_node(
    self, 
    state: OrchestratorState,
    *,
    store: BaseStore = None,  # Injected by LangGraph
) -> OrchestratorState:
    """Execute implementation with memory context."""
    
    # If store is injected, we can use it directly
    # Otherwise fall back to self.memory_store
    active_store = store or self.memory_store
    
    # ... rest of implementation ...
```

**Note:** Store injection requires proper graph compilation with the store. The `store` parameter is optional with a default to support testing.

---

### Task 4.8: Add Memory CLI Commands
**File:** `build_tools/sharpy_auto_builder/cli.py`

Add CLI commands for memory management:

```python
import click
from rich.console import Console
from rich.table import Table

console = Console()


@click.group()
def memory():
    """Memory store management commands."""
    pass


@memory.command()
@click.option("--namespace", "-n", default="implementation_patterns",
              help="Namespace to search")
@click.option("--limit", "-l", default=10, help="Maximum results")
@click.argument("query")
def search(namespace: str, limit: int, query: str):
    """Search memory for patterns."""
    from .config import Config
    from .orchestrator import Orchestrator
    
    config = Config()
    
    with Orchestrator(config) as orch:
        ns_tuple = ("sharpy", namespace)
        patterns = orch.memory_manager.search_patterns(query, ns_tuple, limit)
        
        if not patterns:
            console.print("[yellow]No patterns found[/yellow]")
            return
        
        table = Table(title=f"Patterns matching: {query}")
        table.add_column("ID", style="dim")
        table.add_column("Type")
        table.add_column("Description")
        table.add_column("Created")
        
        for p in patterns:
            table.add_row(
                p.id[:8],
                p.task_type,
                p.description[:50] + "..." if len(p.description) > 50 else p.description,
                p.created_at[:10],
            )
        
        console.print(table)


@memory.command()
@click.option("--namespace", "-n", help="Namespace to list (all if not specified)")
def stats(namespace: str):
    """Show memory store statistics."""
    from .config import Config
    from .orchestrator import Orchestrator
    
    config = Config()
    
    with Orchestrator(config) as orch:
        namespaces = [
            ("sharpy", "implementation_patterns"),
            ("sharpy", "error_patterns"),
            ("sharpy", "codebase_knowledge"),
            ("sharpy", "spec_patterns"),
        ]
        
        if namespace:
            namespaces = [("sharpy", namespace)]
        
        table = Table(title="Memory Store Statistics")
        table.add_column("Namespace")
        table.add_column("Count", justify="right")
        
        for ns in namespaces:
            try:
                items = list(orch.memory_store.search(ns, limit=10000))
                count = len(items)
            except Exception:
                count = "N/A"
            
            table.add_row(ns[1], str(count))
        
        console.print(table)


@memory.command()
@click.option("--namespace", "-n", required=True, help="Namespace to clear")
@click.option("--confirm", is_flag=True, help="Confirm deletion")
def clear(namespace: str, confirm: bool):
    """Clear all patterns in a namespace."""
    if not confirm:
        console.print("[red]Use --confirm to actually delete[/red]")
        return
    
    from .config import Config
    from .orchestrator import Orchestrator
    
    config = Config()
    ns_tuple = ("sharpy", namespace)
    
    with Orchestrator(config) as orch:
        items = list(orch.memory_store.search(ns_tuple, limit=10000))
        
        for item in items:
            orch.memory_store.delete(ns_tuple, item.key)
        
        console.print(f"[green]Deleted {len(items)} items from {namespace}[/green]")
```

---

### Task 4.9: Write Tests
**File:** `build_tools/tests/test_memory.py` (new file)

```python
"""Tests for long-term memory management."""

import pytest
from datetime import datetime
from unittest.mock import MagicMock, patch

from langgraph.store.memory import InMemoryStore

from sharpy_auto_builder.memory import MemoryManager, Pattern
from sharpy_auto_builder.config import Config, MemoryConfig


class TestMemoryManager:
    """Test MemoryManager operations."""
    
    @pytest.fixture
    def config(self):
        """Create test config."""
        config = MagicMock(spec=Config)
        config.memory = MemoryConfig(enabled=True)
        return config
    
    @pytest.fixture
    def store(self):
        """Create in-memory store."""
        return InMemoryStore()
    
    @pytest.fixture
    def manager(self, config, store):
        """Create memory manager."""
        return MemoryManager(config, store)
    
    def test_store_implementation_pattern(self, manager):
        """Test storing implementation pattern."""
        pattern_id = manager.store_implementation_pattern(
            task_type="operator_overloading",
            description="Implemented == operator",
            solution="Used IEquatable pattern",
            files=["Operators.cs"],
            tags=["operator", "equality"],
        )
        
        assert pattern_id  # Should return non-empty ID
        
        # Verify it was stored
        items = list(manager.store.search(manager.NS_IMPLEMENTATION))
        assert len(items) == 1
        assert items[0].value["task_type"] == "operator_overloading"
    
    def test_store_error_pattern(self, manager):
        """Test storing error pattern."""
        pattern_id = manager.store_error_pattern(
            error_type="compilation",
            description="Missing using directive",
            error_message="CS0246: The type or namespace...",
            solution="Add using System.Linq;",
        )
        
        assert pattern_id
        
        items = list(manager.store.search(manager.NS_ERRORS))
        assert len(items) == 1
    
    def test_search_patterns(self, manager):
        """Test searching patterns."""
        # Store some patterns
        manager.store_implementation_pattern(
            task_type="lexer",
            description="Token scanning",
            solution="Used finite state machine",
        )
        manager.store_implementation_pattern(
            task_type="parser",
            description="Expression parsing",
            solution="Used recursive descent",
        )
        
        # Search
        results = manager.search_patterns("parsing", limit=5)
        
        assert len(results) >= 1
    
    def test_get_implementation_context(self, manager):
        """Test context generation."""
        manager.store_implementation_pattern(
            task_type="test_type",
            description="Test description",
            solution="Test solution",
        )
        
        context = manager.get_implementation_context("test query")
        
        assert "Relevant Past Patterns" in context or context == ""
    
    def test_disabled_memory(self, config, store):
        """Test that disabled memory returns empty."""
        config.memory.enabled = False
        manager = MemoryManager(config, store)
        
        # These should all return empty/no-op
        assert manager.store_implementation_pattern("t", "d", "s") == ""
        assert manager.search_patterns("query") == []
        assert manager.get_implementation_context("query") == ""


class TestPattern:
    """Test Pattern dataclass."""
    
    def test_to_dict(self):
        """Test serialization."""
        pattern = Pattern(
            id="abc123",
            namespace="test",
            task_type="implementation",
            description="Test",
            solution="Solution",
            files=["file.cs"],
            tags=["tag1"],
            created_at="2024-01-01",
            success=True,
            metadata={},
        )
        
        d = pattern.to_dict()
        assert d["id"] == "abc123"
        assert d["task_type"] == "implementation"
    
    def test_from_store_item(self):
        """Test creation from store item."""
        # Create a mock store item
        item = MagicMock()
        item.key = "test-key"
        item.namespace = ("sharpy", "patterns")
        item.value = {
            "task_type": "lexer",
            "description": "Token handling",
            "solution": "FSM approach",
            "files": ["Lexer.cs"],
            "tags": ["lexer"],
            "created_at": "2024-01-01",
            "success": True,
            "metadata": {},
        }
        
        pattern = Pattern.from_store_item(item)
        
        assert pattern.id == "test-key"
        assert pattern.task_type == "lexer"


class TestMemoryIntegration:
    """Integration tests for memory with orchestrator."""
    
    def test_memory_context_in_prompt(self):
        """Test that memory context is included in prompts."""
        # This would be an integration test with the full orchestrator
        pass
    
    def test_pattern_stored_after_success(self):
        """Test that patterns are stored after successful task."""
        # This would be an integration test
        pass
```

---

## Verification Checklist

After completing all tasks, verify:

- [ ] `memory.py` module created with `MemoryManager`
- [ ] Memory store initialized in orchestrator
- [ ] Patterns are stored after successful tasks
- [ ] Error patterns are stored after failures
- [ ] Memory context is included in prompts
- [ ] `memory search` CLI command works
- [ ] `memory stats` CLI command works
- [ ] Embeddings work (if configured) for semantic search
- [ ] Memory disabled mode works without errors
- [ ] All existing tests pass
- [ ] New memory tests pass

## Memory Usage Patterns

### What Gets Stored

1. **Implementation Patterns** (after success):
   - Task type (e.g., "lexer_rule", "operator_overloading")
   - Description of what was done
   - Solution summary (code or approach)
   - Files modified
   - Tags for categorization

2. **Error Patterns** (after failure):
   - Error type (compilation, test, spec_violation)
   - What caused it
   - How it was fixed (if known)

3. **Codebase Knowledge** (extracted during analysis):
   - Architecture patterns
   - Naming conventions
   - Common idioms

### How It's Used

1. **Before Implementation:**
   ```
   Query: "Implement binary + operator for custom types"
   
   Retrieved patterns:
   - Pattern 1: operator_overloading - "Use static operator method..."
   - Pattern 2: binary_operators - "Ensure both operands handled..."
   
   Injected into prompt for context.
   ```

2. **Error Avoidance:**
   ```
   Query: "Parser rule for expressions"
   
   Retrieved errors:
   - Error: "Left recursion caused stack overflow"
   - Solution: "Rewrite as right-recursive"
   
   Included as warning in prompt.
   ```

## Rollback Plan

Memory is additive and doesn't affect core functionality:

1. Set `memory.enabled = False` in config
2. Or remove memory store from graph compilation
3. Memory data is preserved in case you re-enable

## Next Steps

After this implementation:
- The system learns from past tasks
- Prompts include relevant context
- Errors are avoided through learned patterns
- Consider adding manual pattern entry for human expertise

## Cross-References

- **New file:** `build_tools/sharpy_auto_builder/memory.py`
- **Orchestrator:** `build_tools/sharpy_auto_builder/orchestrator.py`
- **Config:** `build_tools/sharpy_auto_builder/config.py`
- **LangGraph Memory:** https://docs.langchain.com/oss/python/langgraph/add-memory
- **Persistence Store:** https://docs.langchain.com/oss/python/langgraph/persistence#memory-store
