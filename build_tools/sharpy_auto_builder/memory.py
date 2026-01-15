"""
Memory management for Sharpy Auto Builder.

Provides pattern storage and retrieval using LangGraph memory store.
Supports semantic search with optional embeddings or exact key matching.
"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Literal, Optional
from pathlib import Path
import logging

from langgraph.store.base import BaseStore

logger = logging.getLogger(__name__)


@dataclass
class Pattern:
    """
    Represents a stored pattern (implementation, error, or knowledge).

    Patterns are stored in namespaces to separate different types of information.
    """

    # Unique identifier
    id: str

    # Namespace tuple (e.g., ("sharpy", "implementation_patterns"))
    namespace: tuple[str, ...]

    # Pattern type (e.g., "component_creation", "test_fix")
    task_type: str

    # Description of the pattern
    description: str

    # Solution/code/knowledge content
    solution: str

    # Related files
    files: list[str] = field(default_factory=list)

    # Tags for categorization
    tags: list[str] = field(default_factory=list)

    # When created
    created_at: datetime = field(default_factory=datetime.now)

    # Success indicator (for implementation patterns)
    success: bool = True

    # Additional metadata
    metadata: dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        """Convert pattern to dictionary for storage."""
        return {
            "id": self.id,
            "namespace": self.namespace,
            "task_type": self.task_type,
            "description": self.description,
            "solution": self.solution,
            "files": self.files,
            "tags": self.tags,
            "created_at": self.created_at.isoformat(),
            "success": self.success,
            "metadata": self.metadata,
        }

    @classmethod
    def from_store_item(cls, item: Any) -> "Pattern":
        """Create pattern from store item."""
        value = item.value if hasattr(item, "value") else item

        # Handle created_at deserialization
        created_at = value.get("created_at", datetime.now().isoformat())
        if isinstance(created_at, str):
            created_at = datetime.fromisoformat(created_at)

        return cls(
            id=value.get("id", ""),
            namespace=tuple(value.get("namespace", ())),
            task_type=value.get("task_type", ""),
            description=value.get("description", ""),
            solution=value.get("solution", ""),
            files=value.get("files", []),
            tags=value.get("tags", []),
            created_at=created_at,
            success=value.get("success", True),
            metadata=value.get("metadata", {}),
        )


class MemoryManager:
    """
    Manages pattern storage and retrieval using LangGraph memory store.

    Supports three types of patterns:
    - Implementation patterns: Successful code implementations
    - Error patterns: Common errors and their solutions
    - Codebase knowledge: Understanding of the codebase structure
    """

    # Namespace constants
    NS_IMPLEMENTATION = ("sharpy", "implementation_patterns")
    NS_ERRORS = ("sharpy", "error_patterns")
    NS_CODEBASE = ("sharpy", "codebase_knowledge")
    NS_SPEC = ("sharpy", "spec_patterns")

    def __init__(self, store: Optional[BaseStore], config: Any):
        """
        Initialize memory manager.

        Args:
            store: LangGraph memory store (None if disabled)
            config: MemoryConfig instance
        """
        self.store = store
        self.config = config
        self._pattern_counter = 0

    def _get_next_id(self, prefix: str) -> str:
        """Generate unique pattern ID."""
        self._pattern_counter += 1
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        return f"{prefix}_{timestamp}_{self._pattern_counter}"

    def _truncate_value(self, value: str) -> str:
        """Truncate long values to max_pattern_length."""
        max_len = self.config.max_pattern_length
        if len(value) > max_len:
            logger.debug(f"Truncating value from {len(value)} to {max_len} chars")
            return value[:max_len] + "..."
        return value

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
        """
        Store a successful implementation pattern.

        Args:
            task_type: Type of task (e.g., "component_creation", "test_fix")
            description: Description of what was implemented
            solution: The implementation code or approach
            files: List of files modified
            tags: Tags for categorization
            task_id: Optional task ID for linking
            metadata: Additional metadata

        Returns:
            Pattern ID (empty string if memory disabled)
        """
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

    def store_error_pattern(
        self,
        error_type: str,
        description: str,
        error_message: str,
        solution: str,
        files: Optional[list[str]] = None,
        task_id: Optional[str] = None,
    ) -> str:
        """
        Store an error pattern and its solution.

        Args:
            error_type: Type of error (e.g., "type_error", "test_failure")
            description: Description of the error
            error_message: The error message
            solution: How it was fixed
            files: Files involved
            task_id: Optional task ID

        Returns:
            Pattern ID (empty string if memory disabled)
        """
        if not self.config.enabled or not self.store:
            return ""

        pattern_id = self._get_next_id("error")

        pattern = Pattern(
            id=pattern_id,
            namespace=self.NS_ERRORS,
            task_type=error_type,
            description=self._truncate_value(description),
            solution=self._truncate_value(solution),
            files=files or [],
            tags=[error_type],
            metadata={"error_message": self._truncate_value(error_message)},
        )

        if task_id:
            pattern.metadata["task_id"] = task_id

        try:
            self.store.put(
                namespace=self.NS_ERRORS,
                key=pattern_id,
                value=pattern.to_dict(),
            )
            logger.info(f"Stored error pattern: {pattern_id}")
            return pattern_id
        except Exception as e:
            logger.error(f"Failed to store error pattern: {e}")
            return ""

    def store_codebase_knowledge(
        self,
        category: str,
        knowledge: str,
        source_file: Optional[str] = None,
        confidence: float = 1.0,
    ) -> str:
        """
        Store codebase knowledge.

        Args:
            category: Knowledge category (e.g., "architecture", "patterns")
            knowledge: The knowledge content
            source_file: Optional source file
            confidence: Confidence score (0.0-1.0)

        Returns:
            Knowledge ID (empty string if memory disabled)
        """
        if not self.config.enabled or not self.store:
            return ""

        knowledge_id = self._get_next_id("knowledge")

        pattern = Pattern(
            id=knowledge_id,
            namespace=self.NS_CODEBASE,
            task_type=category,
            description=category,
            solution=self._truncate_value(knowledge),
            files=[source_file] if source_file else [],
            tags=[category],
            metadata={"confidence": confidence},
        )

        try:
            self.store.put(
                namespace=self.NS_CODEBASE,
                key=knowledge_id,
                value=pattern.to_dict(),
            )
            logger.info(f"Stored codebase knowledge: {knowledge_id}")
            return knowledge_id
        except Exception as e:
            logger.error(f"Failed to store codebase knowledge: {e}")
            return ""

    def search_patterns(
        self,
        query: str,
        namespace: tuple[str, ...],
        limit: Optional[int] = None,
    ) -> list[Pattern]:
        """
        Search for patterns matching a query.

        Args:
            query: Search query
            namespace: Namespace to search in
            limit: Maximum results (uses config default if None)

        Returns:
            List of matching patterns (empty if memory disabled)
        """
        if not self.config.enabled or not self.store:
            return []

        limit = limit or self.config.max_patterns_per_query

        try:
            # Use search method for both semantic and non-semantic cases
            # InMemoryStore.search works even without embeddings
            # search() signature: search(namespace_prefix, /, query, limit=10, filter=None)
            results = self.store.search(
                namespace,  # positional argument
                query=query if query else "",  # Empty query returns all items
                limit=limit if limit else 100,  # Provide a default limit
            )

            patterns = [Pattern.from_store_item(item) for item in results]
            logger.debug(f"Found {len(patterns)} patterns for query: {query}")
            return patterns[:limit] if limit else patterns

        except Exception as e:
            logger.error(f"Failed to search patterns: {e}")
            return []

    def get_implementation_context(self, task_description: str) -> str:
        """
        Get relevant implementation patterns as context.

        Args:
            task_description: Description of current task

        Returns:
            Formatted context string with relevant patterns
        """
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
        """
        Get warnings about past errors to avoid.

        Args:
            task_description: Description of current task

        Returns:
            Formatted context string with error warnings
        """
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

    def get_codebase_context(self, file_path: str) -> str:
        """
        Get relevant codebase knowledge.

        Args:
            file_path: File path to get context for

        Returns:
            Formatted context string with codebase knowledge
        """
        patterns = self.search_patterns(
            query=file_path,
            namespace=self.NS_CODEBASE,
        )

        if not patterns:
            return ""

        context_parts = ["## Codebase Knowledge\n"]
        for pattern in patterns:
            confidence = pattern.metadata.get("confidence", 1.0)
            context_parts.append(f"### {pattern.task_type} (confidence: {confidence:.2f})")
            context_parts.append(pattern.solution)
            context_parts.append("")

        return "\n".join(context_parts)
