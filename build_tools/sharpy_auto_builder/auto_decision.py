"""
Auto-Decision Engine for Sharpy Auto Builder.

Makes automatic decisions for well-defined scenarios where human input
isn't strictly necessary, such as deferring optional tasks.
"""

import re
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from typing import Optional, Any

from .response_analyzer import ResponseAnalysis, ResponseType


class DecisionType(str, Enum):
    """Types of automatic decisions."""

    DEFER = "defer"  # Defer the task to later
    PROCEED = "proceed"  # Proceed with default/recommended action
    SKIP = "skip"  # Skip the task entirely
    ESCALATE = "escalate"  # Escalate to human review


@dataclass
class AutoDecision:
    """An automatic decision made by the system."""

    decision_type: DecisionType
    task_id: str
    reason: str
    auto_decided: bool = True
    confidence: float = 0.0
    timestamp: str = field(default_factory=lambda: datetime.now().isoformat())
    selected_option: Optional[str] = None  # Which of agent's proposed options was chosen
    metadata: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {
            "decision_type": self.decision_type.value,
            "task_id": self.task_id,
            "reason": self.reason,
            "auto_decided": self.auto_decided,
            "confidence": self.confidence,
            "timestamp": self.timestamp,
            "selected_option": self.selected_option,
            "metadata": self.metadata,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "AutoDecision":
        return cls(
            decision_type=DecisionType(data["decision_type"]),
            task_id=data["task_id"],
            reason=data["reason"],
            auto_decided=data.get("auto_decided", True),
            confidence=data.get("confidence", 0.0),
            timestamp=data.get("timestamp", datetime.now().isoformat()),
            selected_option=data.get("selected_option"),
            metadata=data.get("metadata", {}),
        )


class AutoDecisionEngine:
    """
    Makes automatic decisions for well-defined scenarios.

    This engine helps the orchestrator handle cases where:
    1. Tasks are marked optional and agent recommends deferring
    2. Agent asks questions that have clear default answers
    3. Tasks can be safely skipped based on context
    """

    # Keywords that indicate a task is optional
    OPTIONAL_INDICATORS = [
        r"\(optional\)",
        r"\[optional\]",
        r"optional:?\s*true",
        r"not required",
        r"nice to have",
        r"future enhancement",
        r"stretch goal",
    ]

    # Keywords that indicate deferral in agent's proposed actions
    DEFERRAL_ACTION_INDICATORS = [
        r"defer",
        r"postpone",
        r"delay",
        r"later phase",
        r"future",
        r"skip",
        r"mark\s+as\s+deferred",
    ]

    def __init__(
        self,
        auto_defer_optional: bool = True,
        require_high_confidence: bool = True,
        min_confidence: float = 0.7,
    ):
        """
        Initialize the auto-decision engine.

        Args:
            auto_defer_optional: Whether to automatically defer optional tasks
            require_high_confidence: Whether to require high confidence for auto-decisions
            min_confidence: Minimum confidence threshold for making decisions
        """
        self.auto_defer_optional = auto_defer_optional
        self.require_high_confidence = require_high_confidence
        self.min_confidence = min_confidence

    def should_auto_decide(
        self,
        task: dict,
        response_analysis: ResponseAnalysis,
    ) -> bool:
        """
        Check if we can make an automatic decision without human input.

        Args:
            task: Task dictionary
            response_analysis: Analysis of the agent's response

        Returns:
            True if auto-decision is appropriate
        """
        # Don't auto-decide if disabled
        if not self.auto_defer_optional:
            return False

        # Check confidence threshold
        if self.require_high_confidence:
            if response_analysis.confidence < self.min_confidence:
                return False

        # Case 1: Optional task with deferral recommendation
        if self._is_optional_task(task):
            if response_analysis.response_type in (
                ResponseType.DEFERRAL,
                ResponseType.QUESTION,
            ):
                # Check if agent's proposed actions include deferral
                if self._has_deferral_option(response_analysis):
                    return True

        # Case 2: Question response with clear default
        if response_analysis.response_type == ResponseType.QUESTION:
            if self._has_clear_default_option(task, response_analysis):
                return True

        return False

    def make_decision(
        self,
        task: dict,
        response_analysis: ResponseAnalysis,
    ) -> AutoDecision:
        """
        Make an automatic decision.

        Args:
            task: Task dictionary
            response_analysis: Analysis of the agent's response

        Returns:
            AutoDecision with the chosen action
        """
        task_id = task.get("id", "unknown")
        task_title = task.get("title", "")

        # Case 1: Optional task with deferral
        if self._is_optional_task(task):
            deferral_option = self._find_deferral_option(response_analysis)
            if deferral_option:
                return AutoDecision(
                    decision_type=DecisionType.DEFER,
                    task_id=task_id,
                    reason=f"Optional task '{task_title}' auto-deferred per agent recommendation",
                    confidence=response_analysis.confidence,
                    selected_option=deferral_option,
                    metadata={
                        "task_is_optional": True,
                        "response_type": response_analysis.response_type.value,
                        "deferral_indicators": response_analysis.deferral_indicators,
                    },
                )

        # Case 2: Question with default option
        default_option = self._find_default_option(task, response_analysis)
        if default_option:
            return AutoDecision(
                decision_type=DecisionType.PROCEED,
                task_id=task_id,
                reason=f"Auto-selected default option for task '{task_title}'",
                confidence=0.6,  # Lower confidence for default selection
                selected_option=default_option,
                metadata={
                    "response_type": response_analysis.response_type.value,
                    "available_options": response_analysis.proposed_actions,
                },
            )

        # Default: Escalate to human
        return AutoDecision(
            decision_type=DecisionType.ESCALATE,
            task_id=task_id,
            reason=f"No clear auto-decision for task '{task_title}' - escalating to human",
            auto_decided=False,
            confidence=0.0,
            metadata={
                "response_type": response_analysis.response_type.value,
                "questions": response_analysis.questions,
                "proposed_actions": response_analysis.proposed_actions,
            },
        )

    def _is_optional_task(self, task: dict) -> bool:
        """Check if a task is marked as optional."""
        title = task.get("title", "").lower()
        description = task.get("description", "").lower()

        # Check explicit metadata
        if task.get("optional", False):
            return True
        if task.get("is_optional", False):
            return True
        if task.get("metadata", {}).get("optional", False):
            return True

        # Check title and description for optional indicators
        combined = f"{title} {description}"
        for pattern in self.OPTIONAL_INDICATORS:
            if re.search(pattern, combined, re.IGNORECASE):
                return True

        return False

    def _has_deferral_option(self, analysis: ResponseAnalysis) -> bool:
        """Check if agent's response includes a deferral option."""
        # Check proposed actions
        for action in analysis.proposed_actions:
            for pattern in self.DEFERRAL_ACTION_INDICATORS:
                if re.search(pattern, action, re.IGNORECASE):
                    return True

        # Check deferral indicators from analysis
        if analysis.deferral_indicators:
            return True

        # Check response type
        if analysis.response_type == ResponseType.DEFERRAL:
            return True

        return False

    def _find_deferral_option(self, analysis: ResponseAnalysis) -> Optional[str]:
        """Find and return the specific deferral option from proposed actions."""
        for action in analysis.proposed_actions:
            for pattern in self.DEFERRAL_ACTION_INDICATORS:
                if re.search(pattern, action, re.IGNORECASE):
                    return action
        return None

    def _has_clear_default_option(
        self, task: dict, analysis: ResponseAnalysis
    ) -> bool:
        """Check if there's a clear default option among proposed actions."""
        # For now, only support deferral as auto-decidable
        # Future: Could support other clear defaults based on task type
        return self._is_optional_task(task) and self._has_deferral_option(analysis)

    def _find_default_option(
        self, task: dict, analysis: ResponseAnalysis
    ) -> Optional[str]:
        """Find the default option to select."""
        # For optional tasks, default is deferral
        if self._is_optional_task(task):
            return self._find_deferral_option(analysis)

        # Future: Add other default selection logic
        return None


# Test cases
if __name__ == "__main__":
    from .response_analyzer import ResponseAnalyzer

    analyzer = ResponseAnalyzer()
    engine = AutoDecisionEngine()

    # Test case: Optional task with deferral question
    task = {
        "id": "0.1.5.8",
        "title": "(Optional) Function Overloading",
        "description": "Optional feature for function overloading",
    }

    response = """Would you like me to:
1. Update the task status in `ground_truth.json` to mark 0.1.5.8 as "deferred"?
2. Proceed to task 0.1.5.9 (integration tests) to complete phase 0.1.5?
3. Create a placeholder `OverloadResolver.cs` with TODO comments?
"""

    analysis = analyzer.analyze(response, task["title"])
    print(f"Response type: {analysis.response_type}")
    print(f"Should auto-decide: {engine.should_auto_decide(task, analysis)}")

    if engine.should_auto_decide(task, analysis):
        decision = engine.make_decision(task, analysis)
        print(f"Decision: {decision.decision_type}")
        print(f"Reason: {decision.reason}")
        print(f"Selected option: {decision.selected_option}")
