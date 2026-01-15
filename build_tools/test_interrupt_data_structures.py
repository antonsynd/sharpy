#!/usr/bin/env python3
"""Test script to verify interrupt data structures."""
import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))


def test_interrupt_data_structures():
    """Test that interrupt TypedDicts can be instantiated."""
    print("Testing interrupt data structures...\n")

    # Import the TypedDicts from orchestrator
    from sharpy_auto_builder.orchestrator import (
        HumanQuestionPayload,
        HumanReviewPayload,
        HumanResponse,
    )

    print("✓ Imported HumanQuestionPayload, HumanReviewPayload, HumanResponse")

    # Test 1: Create HumanQuestionPayload
    question_payload: HumanQuestionPayload = {
        "type": "question",
        "task_id": "task-123",
        "task_description": "Implement feature X",
        "question": "Which approach should we use?",
        "priority": "high",
        "context": "We have two options for implementing this feature",
        "options": ["Option A", "Option B", "Option C"],
    }
    assert question_payload["type"] == "question"
    assert question_payload["task_id"] == "task-123"
    assert question_payload["options"] is not None
    print("✓ HumanQuestionPayload can be instantiated")

    # Test 2: Create HumanReviewPayload
    review_payload: HumanReviewPayload = {
        "type": "review",
        "task_id": "task-456",
        "task_description": "Fix bug in parser",
        "execution_result": {"success": True, "output": "Implementation complete"},
        "validation_results": [{"status": "pass", "message": "All checks passed"}],
        "files_changed": ["src/parser.py", "tests/test_parser.py"],
        "diff_summary": "Modified parser logic and added tests",
    }
    assert review_payload["type"] == "review"
    assert review_payload["task_id"] == "task-456"
    assert len(review_payload["files_changed"]) == 2
    print("✓ HumanReviewPayload can be instantiated")

    # Test 3: Create HumanResponse
    response: HumanResponse = {
        "approved": True,
        "feedback": "Looks good!",
        "modified_value": None,
        "retry": False,
    }
    assert response["approved"] is True
    assert response["retry"] is False
    print("✓ HumanResponse can be instantiated")

    # Test 4: Create response with modifications
    response_with_mods: HumanResponse = {
        "approved": False,
        "feedback": "Please change X to Y",
        "modified_value": "new_value",
        "retry": True,
    }
    assert response_with_mods["approved"] is False
    assert response_with_mods["modified_value"] == "new_value"
    assert response_with_mods["retry"] is True
    print("✓ HumanResponse with modifications can be instantiated")

    # Test 5: Verify optional fields work
    question_no_options: HumanQuestionPayload = {
        "type": "question",
        "task_id": "task-789",
        "task_description": "Test task",
        "question": "Free form question?",
        "priority": "low",
        "context": "Some context",
        "options": None,  # No predefined options
    }
    assert question_no_options["options"] is None
    print("✓ Optional fields (None values) work correctly")

    print("\n✓ All interrupt data structure tests passed!")


if __name__ == "__main__":
    test_interrupt_data_structures()
