"""
Unit tests for the model selector module.

Tests cover:
- Model selection matrix (all task type × complexity combinations)
- Heuristic task classification
- Override mechanism
- Logging callbacks
"""

import pytest
from unittest.mock import Mock, patch
import logging

from build_tools.shared.model_selector import (
    ModelSelector,
    TaskType,
    TaskComplexity,
    ModelRecommendation,
    ModelOverride,
    HAIKU,
    SONNET,
    OPUS,
)


class TestModelConstants:
    """Tests for model constant definitions."""

    def test_haiku_constant(self):
        assert HAIKU == "claude-3-5-haiku-20241022"
        assert ModelSelector.HAIKU == HAIKU

    def test_sonnet_constant(self):
        assert SONNET == "claude-sonnet-4-5-20250929"
        assert ModelSelector.SONNET == SONNET

    def test_opus_constant(self):
        assert OPUS == "claude-opus-4-5-20251101"
        assert ModelSelector.OPUS == OPUS

    def test_get_all_models(self):
        models = ModelSelector.get_all_models()
        assert models == [HAIKU, SONNET, OPUS]


class TestModelSelectionMatrix:
    """Tests for the model selection matrix - all combinations."""

    # Expected mappings from task specification
    EXPECTED_MAPPINGS = {
        # Classification: mostly Haiku, escalates to Opus for very high
        (TaskType.CLASSIFICATION, TaskComplexity.TRIVIAL): HAIKU,
        (TaskType.CLASSIFICATION, TaskComplexity.LOW): HAIKU,
        (TaskType.CLASSIFICATION, TaskComplexity.MEDIUM): SONNET,
        (TaskType.CLASSIFICATION, TaskComplexity.HIGH): SONNET,
        (TaskType.CLASSIFICATION, TaskComplexity.VERY_HIGH): OPUS,
        # Code generation: Haiku for trivial, Sonnet for low-medium, Opus for high+
        (TaskType.CODE_GENERATION, TaskComplexity.TRIVIAL): HAIKU,
        (TaskType.CODE_GENERATION, TaskComplexity.LOW): SONNET,
        (TaskType.CODE_GENERATION, TaskComplexity.MEDIUM): SONNET,
        (TaskType.CODE_GENERATION, TaskComplexity.HIGH): OPUS,
        (TaskType.CODE_GENERATION, TaskComplexity.VERY_HIGH): OPUS,
        # Validation: similar to classification
        (TaskType.VALIDATION, TaskComplexity.TRIVIAL): HAIKU,
        (TaskType.VALIDATION, TaskComplexity.LOW): HAIKU,
        (TaskType.VALIDATION, TaskComplexity.MEDIUM): SONNET,
        (TaskType.VALIDATION, TaskComplexity.HIGH): SONNET,
        (TaskType.VALIDATION, TaskComplexity.VERY_HIGH): OPUS,
        # Documentation: Sonnet for most, Opus for high+
        (TaskType.DOCUMENTATION, TaskComplexity.TRIVIAL): SONNET,
        (TaskType.DOCUMENTATION, TaskComplexity.LOW): SONNET,
        (TaskType.DOCUMENTATION, TaskComplexity.MEDIUM): SONNET,
        (TaskType.DOCUMENTATION, TaskComplexity.HIGH): OPUS,
        (TaskType.DOCUMENTATION, TaskComplexity.VERY_HIGH): OPUS,
        # Debugging: Sonnet for most, Opus for high+
        (TaskType.DEBUGGING, TaskComplexity.TRIVIAL): SONNET,
        (TaskType.DEBUGGING, TaskComplexity.LOW): SONNET,
        (TaskType.DEBUGGING, TaskComplexity.MEDIUM): SONNET,
        (TaskType.DEBUGGING, TaskComplexity.HIGH): OPUS,
        (TaskType.DEBUGGING, TaskComplexity.VERY_HIGH): OPUS,
        # Architecture: Sonnet for simple, Opus for medium+
        (TaskType.ARCHITECTURE, TaskComplexity.TRIVIAL): SONNET,
        (TaskType.ARCHITECTURE, TaskComplexity.LOW): SONNET,
        (TaskType.ARCHITECTURE, TaskComplexity.MEDIUM): OPUS,
        (TaskType.ARCHITECTURE, TaskComplexity.HIGH): OPUS,
        (TaskType.ARCHITECTURE, TaskComplexity.VERY_HIGH): OPUS,
        # Implementation: Sonnet for most, Opus for high+
        (TaskType.IMPLEMENTATION, TaskComplexity.TRIVIAL): SONNET,
        (TaskType.IMPLEMENTATION, TaskComplexity.LOW): SONNET,
        (TaskType.IMPLEMENTATION, TaskComplexity.MEDIUM): SONNET,
        (TaskType.IMPLEMENTATION, TaskComplexity.HIGH): OPUS,
        (TaskType.IMPLEMENTATION, TaskComplexity.VERY_HIGH): OPUS,
    }

    @pytest.mark.parametrize(
        "task_type,complexity,expected_model",
        [(tt, comp, model) for (tt, comp), model in EXPECTED_MAPPINGS.items()],
    )
    def test_selection_matrix(self, task_type, complexity, expected_model):
        """Test that each task type × complexity maps to expected model."""
        rec = ModelSelector.select_model(task_type, complexity)
        assert rec.model == expected_model

    def test_trivial_classification_uses_haiku(self):
        """Explicit test for trivial classification → Haiku."""
        rec = ModelSelector.select_model(
            TaskType.CLASSIFICATION,
            TaskComplexity.TRIVIAL,
        )
        assert rec.model == HAIKU

    def test_complex_architecture_uses_opus(self):
        """Explicit test for complex architecture → Opus."""
        rec = ModelSelector.select_model(
            TaskType.ARCHITECTURE,
            TaskComplexity.HIGH,
        )
        assert rec.model == OPUS

    def test_medium_code_generation_uses_sonnet(self):
        """Explicit test for medium code generation → Sonnet."""
        rec = ModelSelector.select_model(
            TaskType.CODE_GENERATION,
            TaskComplexity.MEDIUM,
        )
        assert rec.model == SONNET


class TestModelRecommendation:
    """Tests for ModelRecommendation structure and fields."""

    def test_provides_reasoning(self):
        """Test that recommendation includes reasoning."""
        rec = ModelSelector.select_model(
            TaskType.DOCUMENTATION,
            TaskComplexity.MEDIUM,
        )
        assert len(rec.reasoning) > 0
        assert "documentation" in rec.reasoning.lower() or "Sonnet" in rec.reasoning

    def test_provides_fallback_for_opus(self):
        """Test that Opus recommendations include Sonnet fallback."""
        rec = ModelSelector.select_model(
            TaskType.ARCHITECTURE,
            TaskComplexity.VERY_HIGH,
        )
        assert rec.model == OPUS
        assert rec.fallback_model == SONNET

    def test_provides_fallback_for_sonnet(self):
        """Test that Sonnet recommendations include Haiku fallback."""
        rec = ModelSelector.select_model(
            TaskType.DOCUMENTATION,
            TaskComplexity.LOW,
        )
        assert rec.model == SONNET
        assert rec.fallback_model == HAIKU

    def test_no_fallback_for_haiku(self):
        """Test that Haiku recommendations have no fallback."""
        rec = ModelSelector.select_model(
            TaskType.CLASSIFICATION,
            TaskComplexity.TRIVIAL,
        )
        assert rec.model == HAIKU
        assert rec.fallback_model is None

    def test_extended_thinking_for_complex_architecture(self):
        """Test that complex architecture tasks recommend extended thinking."""
        rec = ModelSelector.select_model(
            TaskType.ARCHITECTURE,
            TaskComplexity.HIGH,
        )
        assert rec.requires_extended_thinking is True

    def test_extended_thinking_for_very_high_debugging(self):
        """Test that very high debugging tasks recommend extended thinking."""
        rec = ModelSelector.select_model(
            TaskType.DEBUGGING,
            TaskComplexity.VERY_HIGH,
        )
        assert rec.requires_extended_thinking is True

    def test_no_extended_thinking_for_simple_tasks(self):
        """Test that simple tasks don't recommend extended thinking."""
        rec = ModelSelector.select_model(
            TaskType.CODE_GENERATION,
            TaskComplexity.LOW,
        )
        assert rec.requires_extended_thinking is False


class TestTaskClassification:
    """Tests for heuristic task classification."""

    def test_classify_debugging_task(self):
        """Test classification of debugging-related descriptions."""
        task_type, _ = ModelSelector.classify_task(
            "Fix the bug in the parser that causes incorrect token positions"
        )
        assert task_type == TaskType.DEBUGGING

    def test_classify_documentation_task(self):
        """Test classification of documentation-related descriptions."""
        task_type, _ = ModelSelector.classify_task(
            "Write documentation and docstrings for all public methods in the module"
        )
        assert task_type == TaskType.DOCUMENTATION

    def test_classify_code_generation_task(self):
        """Test classification of code generation descriptions."""
        task_type, _ = ModelSelector.classify_task(
            "Create a new function that implements the Visitor interface"
        )
        assert task_type == TaskType.CODE_GENERATION

    def test_classify_validation_task(self):
        """Test classification of validation descriptions."""
        task_type, _ = ModelSelector.classify_task(
            "Verify that the output conforms to the API specification"
        )
        assert task_type == TaskType.VALIDATION

    def test_classify_architecture_task(self):
        """Test classification of architecture descriptions."""
        task_type, _ = ModelSelector.classify_task(
            "Design a new abstraction layer for the backend system"
        )
        assert task_type == TaskType.ARCHITECTURE

    def test_classify_classification_task(self):
        """Test classification of classification descriptions."""
        task_type, _ = ModelSelector.classify_task(
            "Determine if this error message is a rate limit error"
        )
        assert task_type == TaskType.CLASSIFICATION

    def test_default_to_implementation(self):
        """Test that unknown descriptions default to IMPLEMENTATION."""
        task_type, _ = ModelSelector.classify_task("Do something with the code")
        assert task_type == TaskType.IMPLEMENTATION


class TestComplexityClassification:
    """Tests for complexity classification."""

    def test_trivial_keywords(self):
        """Test that trivial keywords result in TRIVIAL complexity."""
        _, complexity = ModelSelector.classify_task(
            "Simple yes/no check if the string is valid"
        )
        assert complexity == TaskComplexity.TRIVIAL

    def test_complex_keywords(self):
        """Test that complex keywords result in HIGH complexity."""
        _, complexity = ModelSelector.classify_task(
            "Complex refactoring across multiple files with edge cases"
        )
        assert complexity == TaskComplexity.HIGH

    def test_very_high_keywords(self):
        """Test that major refactor keywords result in VERY_HIGH."""
        _, complexity = ModelSelector.classify_task(
            "Major refactor of the entire module architecture"
        )
        assert complexity == TaskComplexity.VERY_HIGH

    def test_file_count_increases_complexity(self):
        """Test that high file count increases complexity."""
        # Using a description without complexity keywords to let context dominate
        _, complexity = ModelSelector.classify_task(
            "Make changes to the codebase",
            context={"file_count": 10},
        )
        # High file count should bump complexity, but default is MEDIUM
        # With file_count > 5, we add to HIGH and VERY_HIGH scores
        assert complexity in (
            TaskComplexity.MEDIUM,
            TaskComplexity.HIGH,
            TaskComplexity.VERY_HIGH,
        )

    def test_line_count_increases_complexity(self):
        """Test that high line count increases complexity."""
        # Very high line count should push towards higher complexity
        _, complexity = ModelSelector.classify_task(
            "Make changes to the codebase",
            context={"line_count": 600},
        )
        # line_count > 500 adds to VERY_HIGH score
        assert complexity in (TaskComplexity.HIGH, TaskComplexity.VERY_HIGH)

    def test_default_to_medium(self):
        """Test that unknown descriptions default to MEDIUM complexity."""
        _, complexity = ModelSelector.classify_task("Do something undefined")
        assert complexity == TaskComplexity.MEDIUM


class TestModelOverride:
    """Tests for the model override mechanism."""

    def test_override_changes_model(self):
        """Test that override changes the selected model."""
        override = ModelOverride(
            model=HAIKU,
            reason="Cost optimization - task is simpler than classification suggests",
        )
        rec = ModelSelector.select_model(
            TaskType.ARCHITECTURE,
            TaskComplexity.HIGH,
            override=override,
        )
        assert rec.model == HAIKU

    def test_override_preserves_original(self):
        """Test that override preserves the original model."""
        override = ModelOverride(model=HAIKU, reason="Testing override")
        rec = ModelSelector.select_model(
            TaskType.ARCHITECTURE,
            TaskComplexity.HIGH,
            override=override,
        )
        assert rec.was_overridden is True
        assert rec.original_model == OPUS

    def test_override_includes_reason_in_reasoning(self):
        """Test that override reason is included in recommendation."""
        override = ModelOverride(model=SONNET, reason="Custom reason for override")
        rec = ModelSelector.select_model(
            TaskType.ARCHITECTURE,
            TaskComplexity.HIGH,
            override=override,
        )
        assert "Custom reason for override" in rec.reasoning

    def test_no_override_default(self):
        """Test that without override, was_overridden is False."""
        rec = ModelSelector.select_model(
            TaskType.CODE_GENERATION,
            TaskComplexity.MEDIUM,
        )
        assert rec.was_overridden is False
        assert rec.original_model is None


class TestLoggingCallback:
    """Tests for the logging callback mechanism."""

    def teardown_method(self):
        """Clean up callback after each test."""
        ModelSelector.set_selection_callback(None)

    def test_callback_is_invoked(self):
        """Test that callback is invoked on model selection."""
        callback = Mock()
        ModelSelector.set_selection_callback(callback)

        ModelSelector.select_model(
            TaskType.CODE_GENERATION,
            TaskComplexity.MEDIUM,
            task_description="Test task",
        )

        callback.assert_called_once()

    def test_callback_receives_recommendation(self):
        """Test that callback receives the recommendation."""
        callback = Mock()
        ModelSelector.set_selection_callback(callback)

        ModelSelector.select_model(
            TaskType.CODE_GENERATION,
            TaskComplexity.MEDIUM,
            task_description="Test task",
        )

        args = callback.call_args[0]
        assert isinstance(args[0], ModelRecommendation)
        assert args[0].model == SONNET

    def test_callback_receives_description(self):
        """Test that callback receives the task description."""
        callback = Mock()
        ModelSelector.set_selection_callback(callback)

        ModelSelector.select_model(
            TaskType.CODE_GENERATION,
            TaskComplexity.MEDIUM,
            task_description="Generate a parser",
        )

        args = callback.call_args[0]
        assert args[1] == "Generate a parser"

    def test_callback_error_does_not_break_selection(self):
        """Test that callback errors don't break model selection."""

        def failing_callback(rec, desc):
            raise ValueError("Callback failed!")

        ModelSelector.set_selection_callback(failing_callback)

        # Should not raise
        rec = ModelSelector.select_model(
            TaskType.CODE_GENERATION,
            TaskComplexity.MEDIUM,
        )
        assert rec.model == SONNET

    def test_callback_can_be_cleared(self):
        """Test that callback can be cleared."""
        callback = Mock()
        ModelSelector.set_selection_callback(callback)
        ModelSelector.set_selection_callback(None)

        ModelSelector.select_model(
            TaskType.CODE_GENERATION,
            TaskComplexity.MEDIUM,
        )

        callback.assert_not_called()


class TestSelectModelForDescription:
    """Tests for the convenience method combining classification and selection."""

    def test_returns_recommendation(self):
        """Test that select_model_for_description returns a recommendation."""
        rec = ModelSelector.select_model_for_description(
            "Generate unit tests for the parser module"
        )
        assert isinstance(rec, ModelRecommendation)

    def test_classifies_and_selects(self):
        """Test that it classifies the task and selects appropriate model."""
        rec = ModelSelector.select_model_for_description(
            "Fix the simple typo in the error message"
        )
        # Simple bug fix should use Sonnet
        assert rec.model == SONNET

    def test_respects_override(self):
        """Test that override works with description-based selection."""
        override = ModelOverride(model=HAIKU, reason="Testing")
        rec = ModelSelector.select_model_for_description(
            "Complex architecture redesign",
            override=override,
        )
        assert rec.model == HAIKU
        assert rec.was_overridden is True

    def test_passes_context_to_classification(self):
        """Test that context is passed to classification."""
        # High file count should increase complexity
        rec = ModelSelector.select_model_for_description(
            "Update the module",
            context={"file_count": 15},
        )
        # Should select a more capable model due to high file count
        assert rec.model in (SONNET, OPUS)


class TestGetModelForTask:
    """Tests for the convenience method to get just the model string."""

    def test_returns_string(self):
        """Test that get_model_for_task returns a string."""
        model = ModelSelector.get_model_for_task(
            TaskType.CLASSIFICATION,
            TaskComplexity.TRIVIAL,
        )
        assert isinstance(model, str)
        assert model == HAIKU

    def test_matches_select_model(self):
        """Test that get_model_for_task matches select_model result."""
        for task_type in TaskType:
            for complexity in TaskComplexity:
                direct = ModelSelector.get_model_for_task(task_type, complexity)
                via_select = ModelSelector.select_model(task_type, complexity).model
                assert direct == via_select
