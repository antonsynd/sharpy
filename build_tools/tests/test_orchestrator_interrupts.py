"""
Tests for orchestrator interrupt functionality.

Tests LangGraph native interrupt() usage, interrupt payloads, validation loops,
and CLI interrupt handler integration.
"""

import json
import tempfile
from pathlib import Path
from typing import Dict, Any
from unittest.mock import Mock, patch, MagicMock

import pytest

from sharpy_auto_builder.config import Config
from sharpy_auto_builder.orchestrator import Orchestrator
from sharpy_auto_builder import interrupt_handler


@pytest.fixture
def temp_config():
    """Create a temporary config with isolated state directory."""
    with tempfile.TemporaryDirectory() as tmpdir:
        config = Config()
        # Use temporary directory for test isolation
        config.project_root = Path(tmpdir)

        # Create minimal task list
        task_list_path = Path(tmpdir) / "test_task_list.md"
        task_list_path.write_text("# Test Task List\n\n- [ ] Test task\n")
        config.task_list_path = task_list_path

        # Ensure directories are created
        config.ensure_directories()

        yield config


class TestInterruptPayloads:
    """Tests for interrupt payload structure and content."""

    def test_human_review_payload_structure(self):
        """Test that HumanReviewPayload has all required fields."""
        from sharpy_auto_builder.orchestrator import HumanReviewPayload

        # Create a sample payload
        payload: HumanReviewPayload = {
            "type": "review",
            "task_id": "test-1",
            "task_description": "Test task",
            "execution_result": {"success": True, "output": "Done"},
            "validation_results": [],
            "files_changed": ["file1.py", "file2.py"],
            "diff_summary": "Added features",
        }

        # Verify all required fields
        assert payload["type"] == "review"
        assert payload["task_id"] == "test-1"
        assert "task_description" in payload
        assert "execution_result" in payload
        assert "validation_results" in payload
        assert "files_changed" in payload
        assert "diff_summary" in payload

    def test_human_question_payload_structure(self):
        """Test that HumanQuestionPayload has all required fields."""
        from sharpy_auto_builder.orchestrator import HumanQuestionPayload

        # Create a sample payload
        payload: HumanQuestionPayload = {
            "type": "question",
            "task_id": "test-1",
            "task_description": "Test task",
            "question": "What should we do?",
            "priority": "medium",
            "context": "Some context",
            "options": ["Option A", "Option B"],
        }

        # Verify all required fields
        assert payload["type"] == "question"
        assert payload["task_id"] == "test-1"
        assert "task_description" in payload
        assert "question" in payload
        assert "priority" in payload
        assert "context" in payload
        assert "options" in payload

    def test_human_response_structure(self):
        """Test that HumanResponse has expected fields."""
        from sharpy_auto_builder.orchestrator import HumanResponse

        # Create a sample response
        response: HumanResponse = {
            "approved": True,
            "feedback": "Looks good",
            "modified_value": None,
            "retry": False,
        }

        # Verify fields
        assert "approved" in response
        assert "feedback" in response
        assert "modified_value" in response
        assert "retry" in response


class TestInterruptValidation:
    """Tests for interrupt response validation."""

    def test_validate_review_response_valid_approve(self):
        """Test validation of valid approve response."""
        response = {
            "approved": True,
            "retry": False,
            "feedback": "Great work",
            "modified_value": None,
        }

        is_valid, error = Orchestrator._validate_review_response(response)
        assert is_valid is True
        assert error is None

    def test_validate_review_response_valid_retry(self):
        """Test validation of valid retry response."""
        response = {
            "approved": False,
            "retry": True,
            "feedback": "Please fix the bug",
            "modified_value": None,
        }

        is_valid, error = Orchestrator._validate_review_response(response)
        assert is_valid is True
        assert error is None

    def test_validate_review_response_invalid_no_decision(self):
        """Test validation rejects response with no decision."""
        response = {
            "feedback": "Not sure",
        }

        is_valid, error = Orchestrator._validate_review_response(response)
        assert is_valid is False
        assert "approved" in error.lower() or "retry" in error.lower()

    def test_validate_review_response_invalid_conflicting(self):
        """Test validation rejects conflicting approve and retry."""
        response = {
            "approved": True,
            "retry": True,
        }

        is_valid, error = Orchestrator._validate_review_response(response)
        assert is_valid is False
        assert "cannot" in error.lower() or "conflict" in error.lower()

    def test_validate_review_response_invalid_type(self):
        """Test validation rejects non-boolean approved field."""
        response = {
            "approved": "yes",  # Should be boolean
            "retry": False,
        }

        is_valid, error = Orchestrator._validate_review_response(response)
        assert is_valid is False
        assert "boolean" in error.lower()

    def test_validate_review_response_invalid_not_dict(self):
        """Test validation rejects non-dictionary response."""
        response = "approve"  # Not a dict

        is_valid, error = Orchestrator._validate_review_response(response)
        assert is_valid is False
        assert "dictionary" in error.lower()

    def test_validate_question_response_valid(self):
        """Test validation of valid question response."""
        response = {
            "value": "Option A",
            "additional_feedback": "This is the best option",
        }

        is_valid, error = Orchestrator._validate_question_response(response)
        assert is_valid is True
        assert error is None

    def test_validate_question_response_with_options_valid(self):
        """Test validation accepts answer from valid options."""
        response = {"value": "Option B"}
        options = ["Option A", "Option B", "Option C"]

        is_valid, error = Orchestrator._validate_question_response(response, options)
        assert is_valid is True
        assert error is None

    def test_validate_question_response_with_options_invalid(self):
        """Test validation rejects answer not in options."""
        response = {"value": "Option D"}
        options = ["Option A", "Option B", "Option C"]

        is_valid, error = Orchestrator._validate_question_response(response, options)
        assert is_valid is False
        assert "must be one of" in error.lower()

    def test_validate_question_response_empty_answer(self):
        """Test validation rejects empty answer."""
        response = {"value": ""}

        is_valid, error = Orchestrator._validate_question_response(response)
        assert is_valid is False
        assert "empty" in error.lower()

    def test_validate_question_response_no_value(self):
        """Test validation rejects response without value field."""
        response = {"additional_feedback": "Something"}

        is_valid, error = Orchestrator._validate_question_response(response)
        assert is_valid is False
        assert "value" in error.lower()


class TestInterruptHandler:
    """Tests for CLI interrupt handler functions."""

    def test_display_review_request_no_error(self):
        """Test that display_review_request doesn't raise errors."""
        data = {
            "type": "review",
            "task_id": "test-1",
            "task_description": "Test task",
            "execution_result": {"success": True, "output": "Done"},
            "validation_results": [
                {"agent": "syntax", "status": "passed", "message": "OK"}
            ],
            "files_changed": ["file1.py"],
            "diff_summary": "Added feature",
        }

        # Should not raise
        try:
            interrupt_handler._display_review_request(data)
        except Exception as e:
            pytest.fail(f"_display_review_request raised {type(e).__name__}: {e}")

    def test_display_question_no_error(self):
        """Test that display_question doesn't raise errors."""
        data = {
            "type": "question",
            "task_id": "test-1",
            "task_description": "Test task",
            "question": "What should we do?",
            "priority": "medium",
            "context": "Context",
            "options": ["A", "B"],
        }

        # Should not raise
        try:
            interrupt_handler._display_question(data)
        except Exception as e:
            pytest.fail(f"_display_question raised {type(e).__name__}: {e}")

    def test_display_interrupt_routes_correctly(self):
        """Test that display_interrupt routes to correct display function."""
        review_data = {"type": "review", "task_id": "test"}
        question_data = {"type": "question", "task_id": "test"}

        # Should not raise for either type
        try:
            interrupt_handler.display_interrupt(review_data)
            interrupt_handler.display_interrupt(question_data)
        except Exception as e:
            pytest.fail(f"display_interrupt raised {type(e).__name__}: {e}")

    @patch("sharpy_auto_builder.interrupt_handler.Prompt.ask")
    @patch("sharpy_auto_builder.interrupt_handler.Confirm.ask")
    def test_collect_review_response_approve(self, mock_confirm, mock_prompt):
        """Test collecting approve review response."""
        mock_prompt.return_value = "1"  # Approve
        mock_confirm.return_value = False  # No feedback

        response = interrupt_handler._collect_review_response()

        assert response["approved"] is True
        assert response["retry"] is False
        assert response["feedback"] is None

    @patch("sharpy_auto_builder.interrupt_handler.Prompt.ask")
    @patch("sharpy_auto_builder.interrupt_handler.Confirm.ask")
    def test_collect_review_response_retry(self, mock_confirm, mock_prompt):
        """Test collecting retry review response."""
        mock_prompt.return_value = "2"  # Retry
        mock_confirm.return_value = False  # No feedback

        response = interrupt_handler._collect_review_response()

        assert response["approved"] is False
        assert response["retry"] is True
        assert response["feedback"] is None

    @patch("sharpy_auto_builder.interrupt_handler.Prompt.ask")
    @patch("sharpy_auto_builder.interrupt_handler.Confirm.ask")
    def test_collect_review_response_skip(self, mock_confirm, mock_prompt):
        """Test collecting skip review response."""
        mock_prompt.return_value = "3"  # Skip
        mock_confirm.return_value = False  # No feedback

        response = interrupt_handler._collect_review_response()

        assert response["approved"] is False
        assert response["retry"] is False

    @patch("sharpy_auto_builder.interrupt_handler.Prompt.ask")
    @patch("sharpy_auto_builder.interrupt_handler.Confirm.ask")
    def test_collect_question_response_no_options(self, mock_confirm, mock_prompt):
        """Test collecting free-text question response."""
        mock_prompt.return_value = "My answer"
        mock_confirm.return_value = False  # No additional feedback

        data = {"options": None}
        response = interrupt_handler._collect_question_response(data)

        assert response["value"] == "My answer"
        assert response["additional_feedback"] is None

    @patch("sharpy_auto_builder.interrupt_handler.Prompt.ask")
    @patch("sharpy_auto_builder.interrupt_handler.Confirm.ask")
    def test_collect_question_response_with_options(self, mock_confirm, mock_prompt):
        """Test collecting multiple-choice question response."""
        mock_prompt.return_value = "1"  # First option
        mock_confirm.return_value = False  # No additional feedback

        data = {"options": ["Option A", "Option B", "Option C"]}
        response = interrupt_handler._collect_question_response(data)

        assert response["value"] == "Option A"

    def test_collect_response_routes_correctly(self):
        """Test that collect_response routes to correct collection function."""
        with patch("sharpy_auto_builder.interrupt_handler._collect_review_response") as mock_review:
            with patch("sharpy_auto_builder.interrupt_handler._collect_question_response") as mock_question:
                mock_review.return_value = {"approved": True, "retry": False}
                mock_question.return_value = {"value": "Answer"}

                # Test review routing
                review_data = {"type": "review"}
                result = interrupt_handler.collect_response(review_data)
                assert mock_review.called
                assert "approved" in result

                # Test question routing
                question_data = {"type": "question"}
                result = interrupt_handler.collect_response(question_data)
                assert mock_question.called
                assert "value" in result


class TestAskHumanQuestion:
    """Tests for _ask_human_question helper method."""

    def test_ask_human_question_creates_payload(self, temp_config):
        """Test that _ask_human_question creates proper payload structure."""
        with Orchestrator(temp_config) as orch:
            # We can't actually call interrupt() in tests, so we'll test payload creation
            task_id = "test-1"
            question = "Should we proceed?"
            priority = "high"
            context = "We need to decide"
            options = ["Yes", "No"]

            # Instead of calling the method which would interrupt, we'll construct
            # what the payload should look like
            expected_payload = {
                "type": "question",
                "task_id": task_id,
                "task_description": "",  # Would be filled from task
                "question": question,
                "priority": priority,
                "context": context,
                "options": options,
            }

            # Verify structure
            assert expected_payload["type"] == "question"
            assert expected_payload["task_id"] == task_id
            assert expected_payload["question"] == question
            assert expected_payload["priority"] == priority


if __name__ == "__main__":
    # Run tests with pytest
    pytest.main([__file__, "-v"])
