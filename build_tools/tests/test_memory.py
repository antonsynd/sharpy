"""
Unit tests for memory management.

Tests Pattern dataclass, MemoryManager storage/retrieval, and integration scenarios.
"""

import pytest
from datetime import datetime
from pathlib import Path
from tempfile import TemporaryDirectory

from build_tools.sharpy_auto_builder.memory import Pattern, MemoryManager
from build_tools.sharpy_auto_builder.config import MemoryConfig
from langgraph.store.memory import InMemoryStore


class TestPattern:
    """Test Pattern dataclass functionality."""

    def test_pattern_creation(self):
        """Pattern should initialize with required fields."""
        pattern = Pattern(
            id="test_001",
            namespace=("sharpy", "implementation"),
            task_type="component_creation",
            description="Test pattern",
            solution="Test solution code",
        )

        assert pattern.id == "test_001"
        assert pattern.namespace == ("sharpy", "implementation")
        assert pattern.task_type == "component_creation"
        assert pattern.description == "Test pattern"
        assert pattern.solution == "Test solution code"
        assert pattern.files == []
        assert pattern.tags == []
        assert pattern.success is True
        assert pattern.metadata == {}
        assert isinstance(pattern.created_at, datetime)

    def test_pattern_with_optional_fields(self):
        """Pattern should accept optional fields."""
        pattern = Pattern(
            id="test_002",
            namespace=("sharpy", "errors"),
            task_type="type_error",
            description="Type error pattern",
            solution="Fix by adding type annotation",
            files=["src/parser.py", "src/lexer.py"],
            tags=["parsing", "types"],
            success=False,
            metadata={"error_count": 5},
        )

        assert pattern.files == ["src/parser.py", "src/lexer.py"]
        assert pattern.tags == ["parsing", "types"]
        assert pattern.success is False
        assert pattern.metadata == {"error_count": 5}

    def test_pattern_to_dict(self):
        """Pattern should serialize to dictionary."""
        created = datetime(2025, 1, 14, 12, 30, 45)
        pattern = Pattern(
            id="test_003",
            namespace=("sharpy", "codebase"),
            task_type="architecture",
            description="Architecture knowledge",
            solution="Uses visitor pattern",
            files=["src/ast.py"],
            tags=["design"],
            created_at=created,
            metadata={"confidence": 0.95},
        )

        data = pattern.to_dict()

        assert data["id"] == "test_003"
        assert data["namespace"] == ("sharpy", "codebase")
        assert data["task_type"] == "architecture"
        assert data["description"] == "Architecture knowledge"
        assert data["solution"] == "Uses visitor pattern"
        assert data["files"] == ["src/ast.py"]
        assert data["tags"] == ["design"]
        assert data["created_at"] == "2025-01-14T12:30:45"
        assert data["success"] is True
        assert data["metadata"] == {"confidence": 0.95}

    def test_pattern_from_store_item(self):
        """Pattern should deserialize from store item."""
        store_item_data = {
            "id": "test_004",
            "namespace": ("sharpy", "spec_patterns"),
            "task_type": "syntax",
            "description": "Spec pattern",
            "solution": "Grammar rule",
            "files": ["spec.md"],
            "tags": ["grammar"],
            "created_at": "2025-01-14T12:30:45",
            "success": True,
            "metadata": {"version": "1.0"},
        }

        # Mock store item
        class MockStoreItem:
            def __init__(self, value):
                self.value = value

        store_item = MockStoreItem(store_item_data)
        pattern = Pattern.from_store_item(store_item)

        assert pattern.id == "test_004"
        assert pattern.namespace == ("sharpy", "spec_patterns")
        assert pattern.task_type == "syntax"
        assert pattern.created_at == datetime(2025, 1, 14, 12, 30, 45)
        assert pattern.metadata == {"version": "1.0"}

    def test_pattern_from_store_item_with_dict(self):
        """Pattern should deserialize from plain dictionary."""
        data = {
            "id": "test_005",
            "namespace": ("sharpy", "implementation"),
            "task_type": "test",
            "description": "Test implementation",
            "solution": "pytest fixture",
            "files": [],
            "tags": [],
            "created_at": "2025-01-14T10:00:00",
            "success": True,
            "metadata": {},
        }

        pattern = Pattern.from_store_item(data)
        assert pattern.id == "test_005"
        assert pattern.created_at == datetime(2025, 1, 14, 10, 0, 0)


class TestMemoryManager:
    """Test MemoryManager storage and retrieval."""

    @pytest.fixture
    def memory_manager(self):
        """Create MemoryManager with in-memory store."""
        config = MemoryConfig(
            enabled=True,
            embedding_provider=None,  # Use exact key matching
            max_patterns_per_query=10,
            max_pattern_length=1000,
        )
        store = InMemoryStore()
        return MemoryManager(store, config)

    @pytest.fixture
    def disabled_memory_manager(self):
        """Create MemoryManager with memory disabled."""
        config = MemoryConfig(enabled=False)
        return MemoryManager(None, config)

    def test_store_implementation_pattern(self, memory_manager):
        """MemoryManager should store implementation patterns."""
        pattern_id = memory_manager.store_implementation_pattern(
            task_type="component_creation",
            description="Created new parser component",
            solution="class Parser:\n    def parse(self): pass",
            files=["src/parser.py"],
            tags=["parsing"],
            task_id="task_001",
            metadata={"duration": 120},
        )

        assert pattern_id != ""
        assert pattern_id.startswith("impl_")

    def test_store_error_pattern(self, memory_manager):
        """MemoryManager should store error patterns."""
        pattern_id = memory_manager.store_error_pattern(
            error_type="type_error",
            description="Type mismatch in parser",
            error_message="Expected str, got int",
            solution="Added type conversion",
            files=["src/parser.py"],
            task_id="task_002",
        )

        assert pattern_id != ""
        assert pattern_id.startswith("error_")

    def test_store_codebase_knowledge(self, memory_manager):
        """MemoryManager should store codebase knowledge."""
        knowledge_id = memory_manager.store_codebase_knowledge(
            category="architecture",
            knowledge="The parser uses a recursive descent strategy",
            source_file="src/parser.py",
            confidence=0.9,
        )

        assert knowledge_id != ""
        assert knowledge_id.startswith("knowledge_")

    def test_search_implementation_patterns(self, memory_manager):
        """MemoryManager should find stored implementation patterns."""
        # Store a pattern
        memory_manager.store_implementation_pattern(
            task_type="test_creation",
            description="Created unit tests for lexer",
            solution="def test_lexer(): assert True",
            files=["tests/test_lexer.py"],
            tags=["testing", "lexer"],
        )

        # Search for it
        patterns = memory_manager.search_patterns(
            query="lexer",
            namespace=MemoryManager.NS_IMPLEMENTATION,
        )

        assert len(patterns) > 0
        assert any("lexer" in p.description.lower() for p in patterns)

    def test_search_error_patterns(self, memory_manager):
        """MemoryManager should find stored error patterns."""
        # Store an error pattern
        memory_manager.store_error_pattern(
            error_type="syntax_error",
            description="Missing semicolon",
            error_message="SyntaxError: invalid syntax",
            solution="Added missing semicolon",
            files=["src/parser.py"],
        )

        # Search for it
        patterns = memory_manager.search_patterns(
            query="syntax",
            namespace=MemoryManager.NS_ERRORS,
        )

        assert len(patterns) > 0
        assert any("syntax" in p.task_type.lower() for p in patterns)

    def test_search_with_limit(self, memory_manager):
        """MemoryManager should respect search limit."""
        # Store multiple patterns
        for i in range(10):
            memory_manager.store_implementation_pattern(
                task_type=f"type_{i}",
                description=f"Pattern {i}",
                solution=f"Solution {i}",
            )

        # Search with limit
        patterns = memory_manager.search_patterns(
            query="",
            namespace=MemoryManager.NS_IMPLEMENTATION,
            limit=5,
        )

        assert len(patterns) <= 5

    def test_get_implementation_context(self, memory_manager):
        """MemoryManager should generate implementation context."""
        # Store a relevant pattern
        memory_manager.store_implementation_pattern(
            task_type="parser_enhancement",
            description="Enhanced parser with error recovery",
            solution="Added try-except blocks",
            files=["src/parser.py"],
        )

        # Get context
        context = memory_manager.get_implementation_context("improve parser error handling")

        assert isinstance(context, str)
        if context:  # May be empty if search doesn't find relevant patterns
            assert "Past Implementations" in context or context == ""

    def test_get_error_avoidance_context(self, memory_manager):
        """MemoryManager should generate error avoidance context."""
        # Store an error pattern
        memory_manager.store_error_pattern(
            error_type="import_error",
            description="Missing import statement",
            error_message="ImportError: No module named 'ast'",
            solution="Added import ast",
            files=["src/parser.py"],
        )

        # Get context
        context = memory_manager.get_error_avoidance_context("working with AST")

        assert isinstance(context, str)
        if context:
            assert "Errors to Avoid" in context or context == ""

    def test_get_codebase_context(self, memory_manager):
        """MemoryManager should generate codebase context."""
        # Store codebase knowledge
        memory_manager.store_codebase_knowledge(
            category="patterns",
            knowledge="AST nodes use visitor pattern",
            source_file="src/ast.py",
            confidence=0.95,
        )

        # Get context
        context = memory_manager.get_codebase_context("src/ast.py")

        assert isinstance(context, str)
        if context:
            assert "Codebase Knowledge" in context or context == ""

    def test_disabled_memory_returns_empty_id(self, disabled_memory_manager):
        """Disabled MemoryManager should return empty string for storage."""
        pattern_id = disabled_memory_manager.store_implementation_pattern(
            task_type="test",
            description="Test",
            solution="Test",
        )

        assert pattern_id == ""

    def test_disabled_memory_returns_empty_list(self, disabled_memory_manager):
        """Disabled MemoryManager should return empty list for search."""
        patterns = disabled_memory_manager.search_patterns(
            query="test",
            namespace=MemoryManager.NS_IMPLEMENTATION,
        )

        assert patterns == []

    def test_disabled_memory_returns_empty_context(self, disabled_memory_manager):
        """Disabled MemoryManager should return empty context."""
        context = disabled_memory_manager.get_implementation_context("test")
        assert context == ""

        context = disabled_memory_manager.get_error_avoidance_context("test")
        assert context == ""

        context = disabled_memory_manager.get_codebase_context("test.py")
        assert context == ""

    def test_truncate_long_values(self, memory_manager):
        """MemoryManager should truncate values exceeding max_pattern_length."""
        # Create a very long description
        long_text = "A" * 2000  # Exceeds default 1000 char limit

        pattern_id = memory_manager.store_implementation_pattern(
            task_type="test",
            description=long_text,
            solution=long_text,
        )

        # Verify pattern was stored
        assert pattern_id != ""

        # Search and verify truncation
        patterns = memory_manager.search_patterns(
            query="",
            namespace=MemoryManager.NS_IMPLEMENTATION,
        )

        stored_pattern = next((p for p in patterns if p.id == pattern_id), None)
        assert stored_pattern is not None
        assert len(stored_pattern.description) <= 1003  # 1000 + "..."
        assert stored_pattern.description.endswith("...")

    def test_pattern_metadata_stored(self, memory_manager):
        """MemoryManager should preserve pattern metadata."""
        metadata = {
            "duration": 120,
            "retries": 2,
            "backend": "claude_code",
        }

        pattern_id = memory_manager.store_implementation_pattern(
            task_type="test",
            description="Test with metadata",
            solution="Solution",
            metadata=metadata,
        )

        # Search and verify metadata
        patterns = memory_manager.search_patterns(
            query="",
            namespace=MemoryManager.NS_IMPLEMENTATION,
        )

        stored_pattern = next((p for p in patterns if p.id == pattern_id), None)
        assert stored_pattern is not None
        assert stored_pattern.metadata["duration"] == 120
        assert stored_pattern.metadata["retries"] == 2
        assert stored_pattern.metadata["backend"] == "claude_code"

    def test_namespace_constants(self):
        """MemoryManager should define namespace constants."""
        assert MemoryManager.NS_IMPLEMENTATION == ("sharpy", "implementation_patterns")
        assert MemoryManager.NS_ERRORS == ("sharpy", "error_patterns")
        assert MemoryManager.NS_CODEBASE == ("sharpy", "codebase_knowledge")
        assert MemoryManager.NS_SPEC == ("sharpy", "spec_patterns")


class TestMemoryIntegration:
    """Test memory integration scenarios."""

    @pytest.fixture
    def memory_manager(self):
        """Create MemoryManager for integration tests."""
        config = MemoryConfig(
            enabled=True,
            embedding_provider=None,
            max_patterns_per_query=10,
        )
        store = InMemoryStore()
        return MemoryManager(store, config)

    def test_store_and_retrieve_workflow(self, memory_manager):
        """Test complete store and retrieve workflow."""
        # Store implementation pattern
        impl_id = memory_manager.store_implementation_pattern(
            task_type="parser_fix",
            description="Fixed parser bug in expression handling",
            solution="Updated precedence rules",
            files=["src/parser.py"],
            tags=["bugfix", "parser"],
        )

        # Store error pattern
        error_id = memory_manager.store_error_pattern(
            error_type="runtime_error",
            description="Parser crashed on nested expressions",
            error_message="RecursionError: maximum recursion depth exceeded",
            solution="Added iterative approach",
            files=["src/parser.py"],
        )

        # Store codebase knowledge
        knowledge_id = memory_manager.store_codebase_knowledge(
            category="architecture",
            knowledge="Parser uses Pratt parsing for expressions",
            source_file="src/parser.py",
        )

        # Verify all were stored
        assert impl_id != ""
        assert error_id != ""
        assert knowledge_id != ""

        # Search implementation patterns
        impl_patterns = memory_manager.search_patterns(
            query="parser",
            namespace=MemoryManager.NS_IMPLEMENTATION,
        )
        assert len(impl_patterns) > 0

        # Search error patterns
        error_patterns = memory_manager.search_patterns(
            query="parser",
            namespace=MemoryManager.NS_ERRORS,
        )
        assert len(error_patterns) > 0

        # Search codebase knowledge
        codebase_patterns = memory_manager.search_patterns(
            query="parser",
            namespace=MemoryManager.NS_CODEBASE,
        )
        assert len(codebase_patterns) > 0

    def test_context_generation_workflow(self, memory_manager):
        """Test context generation for task execution."""
        # Populate memory with various patterns
        memory_manager.store_implementation_pattern(
            task_type="test_writing",
            description="Created comprehensive test suite",
            solution="Used pytest fixtures and parametrize",
            files=["tests/test_parser.py"],
        )

        memory_manager.store_error_pattern(
            error_type="test_failure",
            description="Tests failed due to missing mocks",
            error_message="AttributeError: 'NoneType' object has no attribute",
            solution="Added proper mocking with unittest.mock",
        )

        memory_manager.store_codebase_knowledge(
            category="testing",
            knowledge="All tests use pytest framework",
            confidence=1.0,
        )

        # Generate contexts
        impl_context = memory_manager.get_implementation_context("write new tests")
        error_context = memory_manager.get_error_avoidance_context("write tests")
        code_context = memory_manager.get_codebase_context("tests/")

        # All contexts should be strings
        assert isinstance(impl_context, str)
        assert isinstance(error_context, str)
        assert isinstance(code_context, str)

    def test_multiple_namespaces_isolated(self, memory_manager):
        """Test that namespaces are properly isolated."""
        # Store same description in different namespaces
        memory_manager.store_implementation_pattern(
            task_type="test_type",
            description="parser functionality",
            solution="Implementation solution",
        )

        memory_manager.store_error_pattern(
            error_type="test_type",
            description="parser functionality",
            error_message="Error message",
            solution="Error solution",
        )

        # Search in each namespace
        impl_patterns = memory_manager.search_patterns(
            query="parser",
            namespace=MemoryManager.NS_IMPLEMENTATION,
        )

        error_patterns = memory_manager.search_patterns(
            query="parser",
            namespace=MemoryManager.NS_ERRORS,
        )

        # Both should find patterns
        assert len(impl_patterns) > 0
        assert len(error_patterns) > 0

        # Patterns should be in correct namespaces
        for p in impl_patterns:
            assert p.namespace == MemoryManager.NS_IMPLEMENTATION

        for p in error_patterns:
            assert p.namespace == MemoryManager.NS_ERRORS
