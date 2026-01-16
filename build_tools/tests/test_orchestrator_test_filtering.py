"""
Tests for orchestrator test component filtering logic.

Ensures that task IDs and phases correctly map to test filter components,
avoiding substring matching bugs (e.g., phase "0.1.10" should not match "0.1.1").
"""

import pytest

from sharpy_auto_builder.orchestrator.nodes.task_execution import TaskExecutionNodes


class MockOrchestrator(TaskExecutionNodes):
    """Mock orchestrator to test the _get_test_component method."""

    pass


class TestGetTestComponent:
    """Tests for _get_test_component method."""

    @pytest.fixture
    def orchestrator(self):
        """Create a mock orchestrator instance."""
        return MockOrchestrator()

    # Phase-based matching tests
    @pytest.mark.parametrize(
        "task_id,phase,expected_component",
        [
            # Lexer phase (0.1.0.*)
            ("0.1.0.L1", "0.1.0", "Lexer"),
            ("task-123", "0.1.0", "Lexer"),
            ("task-123", "0.1.0.L1", "Lexer"),
            # Parser phase (0.1.1.*)
            ("0.1.1.P1", "0.1.1", "Parser"),
            ("task-123", "0.1.1", "Parser"),
            ("task-123", "0.1.1.P5", "Parser"),
            # CodeGen phase (0.1.2.*)
            ("0.1.2.CG1", "0.1.2", "CodeGen"),
            ("task-123", "0.1.2", "CodeGen"),
            ("task-123", "0.1.2.CG3", "CodeGen"),
            # Unknown phase - should run all tests (empty component)
            ("task-123", "0.1.10", ""),
            ("0.1.10.CG1", "0.1.10", ""),
            ("task-123", "0.1.3", ""),
            ("task-123", "0.1.5", ""),
            ("task-123", "0.2.0", ""),
        ],
    )
    def test_phase_based_component_selection(
        self, orchestrator, task_id, phase, expected_component
    ):
        """Test that phases correctly map to test components."""
        task_data = {"id": task_id, "phase": phase}
        result = orchestrator._get_test_component(task_data)
        assert result == expected_component, (
            f"Phase '{phase}' should map to '{expected_component}' component, "
            f"got '{result}'"
        )

    # Task ID-based matching tests
    @pytest.mark.parametrize(
        "task_id,phase,expected_component",
        [
            # Lexer keyword in task ID
            ("analyze-lexer-tokens", "0.1.99", "Lexer"),
            ("lexer-refactor", "0.1.99", "Lexer"),
            ("LEXER-bug-fix", "0.1.99", "Lexer"),
            # Parser keyword in task ID
            ("parser-improvement", "0.1.99", "Parser"),
            ("fix-parser-bug", "0.1.99", "Parser"),
            ("PARSER-ast-node", "0.1.99", "Parser"),
            # CodeGen keyword in task ID
            ("codegen-emit-fix", "0.1.99", "CodeGen"),
            ("improve-codegen", "0.1.99", "CodeGen"),
            ("CODEGEN-roslyn", "0.1.99", "CodeGen"),
            # Semantic keyword in task ID
            ("semantic-analysis-fix", "0.1.99", "Semantic"),
            ("type-semantic-check", "0.1.99", "Semantic"),
            ("SEMANTIC-resolver", "0.1.99", "Semantic"),
        ],
    )
    def test_task_id_keyword_component_selection(
        self, orchestrator, task_id, phase, expected_component
    ):
        """Test that task IDs with keywords correctly map to test components."""
        task_data = {"id": task_id, "phase": phase}
        result = orchestrator._get_test_component(task_data)
        assert result == expected_component, (
            f"Task ID '{task_id}' should map to '{expected_component}' component, "
            f"got '{result}'"
        )

    # Critical regression test for substring matching bug
    def test_phase_0110_does_not_match_parser(self, orchestrator):
        """
        Regression test: Phase '0.1.10' should NOT match '0.1.1' (Parser).

        This was a bug where substring matching caused '0.1.1' in '0.1.10' to be True,
        incorrectly filtering 0.1.10 tasks to only run Parser tests.
        """
        # All 0.1.10 phase variants should NOT return "Parser"
        test_cases = [
            {"id": "0.1.10.CG1", "phase": "0.1.10"},
            {"id": "0.1.10.CG2", "phase": "0.1.10"},
            {"id": "task-abc", "phase": "0.1.10"},
            {"id": "random-task", "phase": "0.1.10.subphase"},
        ]

        for task_data in test_cases:
            result = orchestrator._get_test_component(task_data)
            assert result != "Parser", (
                f"Phase '{task_data['phase']}' incorrectly matched 'Parser'. "
                f"This indicates a substring matching bug where '0.1.1' in '0.1.10' "
                f"was evaluated as True."
            )
            # These should return empty string (run all tests) since no specific
            # component is defined for 0.1.10
            assert result == "", (
                f"Phase '{task_data['phase']}' should return empty string "
                f"(run all tests), got '{result}'"
            )

    def test_exact_phase_match_lexer(self, orchestrator):
        """Test exact phase match for Lexer (0.1.0)."""
        # Should match
        assert (
            orchestrator._get_test_component({"id": "x", "phase": "0.1.0"}) == "Lexer"
        )
        assert (
            orchestrator._get_test_component({"id": "x", "phase": "0.1.0.L1"})
            == "Lexer"
        )

        # Should NOT match (0.1.00 is not 0.1.0)
        assert (
            orchestrator._get_test_component({"id": "x", "phase": "0.1.00"}) != "Lexer"
        )

    def test_exact_phase_match_codegen(self, orchestrator):
        """Test exact phase match for CodeGen (0.1.2)."""
        # Should match
        assert (
            orchestrator._get_test_component({"id": "x", "phase": "0.1.2"}) == "CodeGen"
        )
        assert (
            orchestrator._get_test_component({"id": "x", "phase": "0.1.2.CG5"})
            == "CodeGen"
        )

        # Should NOT match (0.1.20, 0.1.21, etc. are not 0.1.2)
        assert (
            orchestrator._get_test_component({"id": "x", "phase": "0.1.20"})
            != "CodeGen"
        )
        assert (
            orchestrator._get_test_component({"id": "x", "phase": "0.1.21"})
            != "CodeGen"
        )
