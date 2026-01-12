"""
Response Quality Analyzer for Sharpy Auto Builder.

Analyzes agent responses to detect whether actual work was done
vs. questions being asked vs. deferral recommendations.
"""

import re
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional


class ResponseType(str, Enum):
    """Types of agent responses."""

    IMPLEMENTATION = "implementation"  # Agent did actual work
    QUESTION = "question"  # Agent is asking questions
    DEFERRAL = "deferral"  # Agent recommends deferring the task
    CLARIFICATION = "clarification"  # Agent needs more info
    ERROR = "error"  # Agent encountered an error
    EMPTY = "empty"  # No meaningful response


@dataclass
class ResponseAnalysis:
    """Analysis result for an agent response."""

    response_type: ResponseType
    confidence: float  # 0.0 - 1.0
    questions: list[str] = field(default_factory=list)
    proposed_actions: list[str] = field(default_factory=list)
    work_indicators: list[str] = field(default_factory=list)
    deferral_indicators: list[str] = field(default_factory=list)
    raw_response: str = ""
    reasoning: str = ""

    def to_dict(self) -> dict:
        return {
            "response_type": self.response_type.value,
            "confidence": self.confidence,
            "questions": self.questions,
            "proposed_actions": self.proposed_actions,
            "work_indicators": self.work_indicators,
            "deferral_indicators": self.deferral_indicators,
            "reasoning": self.reasoning,
        }


class ResponseAnalyzer:
    """Analyzes agent responses to detect questions vs. actual work done."""

    # Patterns indicating questions/requests for human input
    QUESTION_PATTERNS = [
        (r"Would you like me to[:\s]", 0.9),
        (r"Should I\s+", 0.8),
        (r"Do you want me to\s+", 0.85),
        (r"Would you prefer\s+", 0.8),
        (r"Please (confirm|let me know|tell me|advise)", 0.7),
        (r"Which (option|approach|method)\s+", 0.75),
        (r"What (would|should) you like", 0.8),
        (r"How would you like me to", 0.85),
        (r"Could you (clarify|confirm|specify)", 0.7),
        # Numbered options pattern (like "1. ... 2. ... 3. ...")
        (r"^\s*1\.\s+.+\n\s*2\.\s+.+", 0.7),
    ]

    # Patterns indicating actual work was performed
    WORK_PATTERNS = [
        (r"I('ve|'ve| have) (created|implemented|added|modified|fixed|updated)", 0.9),
        (r"Created (file|directory|class|function|test|method)", 0.85),
        (r"Modified (file|class|function|method)", 0.85),
        (r"Added (method|property|test|import|class|function)", 0.85),
        (r"Implemented (the|a|an)?\s*\w+", 0.8),
        (r"Fixed (the|a|an)?\s*(bug|issue|error|problem)", 0.85),
        (r"Updated (the|a|an)?\s*\w+", 0.75),
        (r"Here'?s the (implementation|code|solution|fix)", 0.8),
        (r"The (test|implementation|fix|feature) (is|has been) (complete|done)", 0.9),
        (r"Successfully (created|updated|modified|implemented)", 0.9),
        (r"All (\d+\s+)?tests? (pass|passed|passing)", 0.8),
        (r"Changes? (made|committed|saved)", 0.75),
        (r"```\w+\n.*?```", 0.5),  # Code blocks suggest work was done
    ]

    # Patterns indicating deferral recommendation
    DEFERRAL_PATTERNS = [
        (r"(recommend|suggest|advise)\s+(deferr?ing|postponing|delaying)", 0.9),
        (r"mark\s+(this\s+)?(task\s+)?as\s+[\"']?defer", 0.9),
        (r"should (be )?(defer|postpone|delay)", 0.85),
        (r"(optional|not required|not critical)", 0.6),
        (r"defer(red)?\s+to\s+(later|future|next)", 0.85),
        (r"out of scope for", 0.7),
        (r"not (necessary|needed|required) (for|at) this", 0.7),
    ]

    # Patterns indicating errors or blockers
    ERROR_PATTERNS = [
        (r"(cannot|can't|unable to) (proceed|continue|complete)", 0.85),
        (r"blocked by", 0.9),
        (r"missing (dependency|prerequisite|requirement)", 0.8),
        (r"error(s)? (encountered|occurred|found)", 0.8),
        (r"failed to", 0.7),
    ]

    def __init__(self, min_confidence: float = 0.6):
        """
        Initialize the analyzer.

        Args:
            min_confidence: Minimum confidence threshold for pattern matching
        """
        self.min_confidence = min_confidence

    def analyze(self, response: str, task_title: str = "") -> ResponseAnalysis:
        """
        Analyze an agent response.

        Args:
            response: The agent's response text
            task_title: The task title (used for context)

        Returns:
            ResponseAnalysis with detected type and confidence
        """
        if not response or not response.strip():
            return ResponseAnalysis(
                response_type=ResponseType.EMPTY,
                confidence=1.0,
                raw_response=response,
                reasoning="Response is empty or whitespace only",
            )

        # Check each pattern type and collect scores
        question_score, question_matches = self._score_patterns(
            response, self.QUESTION_PATTERNS
        )
        work_score, work_matches = self._score_patterns(response, self.WORK_PATTERNS)
        deferral_score, deferral_matches = self._score_patterns(
            response, self.DEFERRAL_PATTERNS
        )
        error_score, error_matches = self._score_patterns(response, self.ERROR_PATTERNS)

        # Extract specific questions if found
        questions = self._extract_questions(response)
        proposed_actions = self._extract_proposed_actions(response)

        # Determine response type based on scores
        # Priority: ERROR > QUESTION > DEFERRAL > IMPLEMENTATION > EMPTY

        reasoning_parts = []

        # If response ends with a question and has low work indicators, it's likely a question
        ends_with_question = response.strip().endswith("?")
        has_numbered_options = bool(
            re.search(r"^\s*1\.\s+.+\n\s*2\.\s+.+", response, re.MULTILINE)
        )

        if ends_with_question:
            question_score = max(question_score, 0.6)
            reasoning_parts.append("Response ends with a question mark")

        if has_numbered_options and questions:
            question_score = max(question_score, 0.8)
            reasoning_parts.append("Response contains numbered options")

        # Error takes precedence
        if error_score >= self.min_confidence and error_score > work_score:
            return ResponseAnalysis(
                response_type=ResponseType.ERROR,
                confidence=error_score,
                work_indicators=work_matches,
                raw_response=response,
                reasoning=f"Error patterns detected: {error_matches}",
            )

        # Question detection - key issue from 0.1.5.8
        if question_score >= self.min_confidence:
            # If there's also significant work, it's implementation with follow-up questions
            if work_score >= 0.7:
                reasoning_parts.append(
                    f"Work score ({work_score:.2f}) indicates implementation was done"
                )
                return ResponseAnalysis(
                    response_type=ResponseType.IMPLEMENTATION,
                    confidence=work_score,
                    questions=questions,
                    proposed_actions=proposed_actions,
                    work_indicators=work_matches,
                    raw_response=response,
                    reasoning="; ".join(reasoning_parts)
                    or "Work patterns detected alongside questions",
                )

            # Pure question response (like 0.1.5.8)
            reasoning_parts.append(f"Question patterns detected: {question_matches}")
            return ResponseAnalysis(
                response_type=ResponseType.QUESTION,
                confidence=question_score,
                questions=questions,
                proposed_actions=proposed_actions,
                raw_response=response,
                reasoning="; ".join(reasoning_parts),
            )

        # Deferral recommendation
        if deferral_score >= self.min_confidence:
            # Check if this is a recommendation or actual deferral with no work
            if work_score < 0.5:
                return ResponseAnalysis(
                    response_type=ResponseType.DEFERRAL,
                    confidence=deferral_score,
                    deferral_indicators=deferral_matches,
                    questions=questions,
                    proposed_actions=proposed_actions,
                    raw_response=response,
                    reasoning=f"Deferral patterns detected: {deferral_matches}",
                )

        # If we have work indicators, it's an implementation
        if work_score >= self.min_confidence:
            return ResponseAnalysis(
                response_type=ResponseType.IMPLEMENTATION,
                confidence=work_score,
                work_indicators=work_matches,
                raw_response=response,
                reasoning=f"Work patterns detected: {work_matches}",
            )

        # Default: Unclear/needs clarification
        return ResponseAnalysis(
            response_type=ResponseType.CLARIFICATION,
            confidence=0.5,
            questions=questions,
            proposed_actions=proposed_actions,
            work_indicators=work_matches,
            raw_response=response,
            reasoning="No clear pattern detected; response type unclear",
        )

    def _score_patterns(
        self, text: str, patterns: list[tuple[str, float]]
    ) -> tuple[float, list[str]]:
        """
        Score text against a list of patterns.

        Returns the max score and list of matched patterns.
        """
        max_score = 0.0
        matches = []

        for pattern, weight in patterns:
            if re.search(pattern, text, re.IGNORECASE | re.MULTILINE):
                max_score = max(max_score, weight)
                matches.append(pattern)

        return max_score, matches

    def _extract_questions(self, text: str) -> list[str]:
        """Extract explicit questions from the response."""
        questions = []

        # Look for sentences ending with ?
        for match in re.finditer(r"[^.!?\n]+\?", text):
            question = match.group().strip()
            if len(question) > 10:  # Filter out very short matches
                questions.append(question)

        # Look for "Would you like" style questions
        for match in re.finditer(r"Would you like[^.!?\n]+[.!?]?", text, re.IGNORECASE):
            q = match.group().strip()
            if q not in questions:
                questions.append(q)

        return questions[:5]  # Limit to 5 questions

    def _extract_proposed_actions(self, text: str) -> list[str]:
        """Extract numbered options/proposed actions from the response."""
        actions = []

        # Pattern for numbered lists: "1. Something\n2. Something else"
        numbered_pattern = r"^\s*(\d+)[.)]\s+(.+?)(?=\n\s*\d+[.)]|\n\n|\Z)"
        for match in re.finditer(numbered_pattern, text, re.MULTILINE | re.DOTALL):
            num, action = match.groups()
            action = action.strip()
            if action and len(action) > 5:
                actions.append(f"{num}. {action[:200]}")  # Truncate long actions

        return actions[:5]  # Limit to 5 actions

    def is_question_response(self, response: str, task_title: str = "") -> bool:
        """
        Quick check if a response is primarily asking questions.

        This is a convenience method for common use case.
        """
        analysis = self.analyze(response, task_title)
        return analysis.response_type == ResponseType.QUESTION

    def has_work_done(self, response: str, task_title: str = "") -> bool:
        """
        Quick check if a response indicates actual work was done.
        """
        analysis = self.analyze(response, task_title)
        return analysis.response_type == ResponseType.IMPLEMENTATION


# Test cases for development
if __name__ == "__main__":
    analyzer = ResponseAnalyzer()

    # Test case 1: Question response (from 0.1.5.8)
    question_response = """Would you like me to:
1. Update the task status in `ground_truth.json` to mark 0.1.5.8 as "deferred"?
2. Proceed to task 0.1.5.9 (integration tests) to complete phase 0.1.5?
3. Create a placeholder `OverloadResolver.cs` with TODO comments documenting the planned implementation?
"""
    result = analyzer.analyze(question_response, "(Optional) Function Overloading")
    print(f"Question response: {result.response_type} ({result.confidence:.2f})")
    print(f"  Questions: {result.questions}")
    print(f"  Actions: {result.proposed_actions}")

    # Test case 2: Implementation response
    impl_response = """I've implemented the function code generation feature.

Created `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` with the following changes:
- Added GenerateFunction method
- Implemented parameter mapping
- Added default value handling

All 54 tests pass. The implementation follows the spec exactly.
"""
    result = analyzer.analyze(impl_response, "Implement Function Code Generation")
    print(f"\nImpl response: {result.response_type} ({result.confidence:.2f})")
    print(f"  Work indicators: {result.work_indicators}")

    # Test case 3: Deferral recommendation
    defer_response = """This task is marked as optional and implementing it now would add complexity.

I recommend deferring this task to a later phase. The core functionality works without it,
and we can revisit function overloading once the basic function support is stable.
"""
    result = analyzer.analyze(defer_response, "(Optional) Function Overloading")
    print(f"\nDeferral response: {result.response_type} ({result.confidence:.2f})")
    print(f"  Deferral indicators: {result.deferral_indicators}")
