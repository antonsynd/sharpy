# Overseer Improvements for Auto-Builder

## Problem Statement

Analysis of task 0.1.5.8 logs reveals a critical gap in orchestration: when an agent asks questions instead of doing work, the system proceeds as if the task was completed successfully.

### Example: Task 0.1.5.8 "(Optional) Function Overloading"

**Agent Response:**
```
Would you like me to:
1. Update the task status in `ground_truth.json` to mark 0.1.5.8 as "deferred"?
2. Proceed to task 0.1.5.9 (integration tests) to complete phase 0.1.5?
3. Create a placeholder `OverloadResolver.cs` with TODO comments documenting the planned implementation?
```

**What happened:**
1. Agent asked questions, did no implementation work
2. Orchestrator saw `result.success=True` (API call succeeded) → proceeded to testing
3. Tests passed (no changes were made, so nothing broke)
4. Validation passed (validators correctly noted the feature was "deferred")
5. Changes committed (empty commit of no actual work)
6. Task marked complete

**Expected behavior:**
- System should detect the question-asking pattern
- Either auto-decide (for optional tasks) or escalate to human
- Only proceed when actual work is done OR explicit deferral is recorded

---

## Proposed Solutions

### 1. Response Quality Analyzer

Add a new component that analyzes agent responses before proceeding:

```python
# build_tools/sharpy_auto_builder/response_analyzer.py

class ResponseAnalysis:
    """Analysis result for an agent response."""

    class ResponseType(Enum):
        IMPLEMENTATION = "implementation"  # Agent did work
        QUESTION = "question"              # Agent is asking questions
        DEFERRAL = "deferral"              # Agent recommends deferring
        CLARIFICATION = "clarification"    # Agent needs clarification
        ERROR = "error"                    # Agent hit an error
        EMPTY = "empty"                    # No meaningful response

    response_type: ResponseType
    confidence: float  # 0.0 - 1.0
    questions: list[str]  # Extracted questions
    proposed_actions: list[str]  # Actions agent is proposing
    work_indicators: list[str]  # Evidence of actual work done

class ResponseAnalyzer:
    """Analyzes agent responses to detect questions vs. work done."""

    # Patterns indicating questions/requests for input
    QUESTION_PATTERNS = [
        r"Would you like me to",
        r"Should I",
        r"Do you want me to",
        r"Would you prefer",
        r"Please (confirm|let me know|tell me)",
        r"Which option",
        r"\?\s*$",  # Ends with question mark
        r"1\.\s+.*\n\s*2\.\s+.*",  # Numbered options
    ]

    # Patterns indicating actual work was done
    WORK_PATTERNS = [
        r"Created? (file|directory|class|function|test)",
        r"Modified? (file|class|function)",
        r"Added? (method|property|test|import)",
        r"Implemented? ",
        r"Fixed? ",
        r"Updated? ",
        r"Here's the (implementation|code|solution)",
        r"The (test|implementation|fix) is (complete|done)",
    ]

    def analyze(self, response: str, task: Task) -> ResponseAnalysis:
        """Analyze an agent response."""
        ...
```

### 2. Auto-Decision Engine for Optional Tasks

Add logic to automatically decide on optional tasks:

```python
class AutoDecisionEngine:
    """Makes automatic decisions for well-defined scenarios."""

    def should_auto_decide(self, task: Task, response: ResponseAnalysis) -> bool:
        """Check if we can auto-decide without human input."""
        # Auto-decide if:
        # 1. Task is marked optional AND
        # 2. Agent suggests deferral AND
        # 3. No critical dependencies on this task

        if "(Optional)" in task.title or task.metadata.get("optional"):
            if response.response_type == ResponseAnalysis.ResponseType.DEFERRAL:
                return not self._has_critical_dependents(task)
        return False

    def make_decision(self, task: Task, response: ResponseAnalysis) -> Decision:
        """Make an automatic decision."""
        if response.response_type == ResponseAnalysis.ResponseType.DEFERRAL:
            return Decision(
                action="defer",
                reason="Optional task deferred per agent recommendation",
                auto_decided=True,
            )
        # ... other decision types
```

### 3. Orchestrator Integration

Modify `_execute_implementation_node` to check response quality:

```python
async def _execute_implementation_node(self, state: OrchestratorState) -> OrchestratorState:
    # ... existing code to execute agent ...

    # NEW: Analyze the response
    analysis = self.response_analyzer.analyze(result.output, task_data)

    if analysis.response_type == ResponseAnalysis.ResponseType.QUESTION:
        # Agent is asking questions - don't proceed blindly

        if self.auto_decision_engine.should_auto_decide(task_data, analysis):
            # Auto-decide for optional tasks
            decision = self.auto_decision_engine.make_decision(task_data, analysis)
            return {
                **state,
                "next_action": "auto_decide",
                "auto_decision": decision.to_dict(),
                "messages": [f"Auto-decided: {decision.action} - {decision.reason}"],
            }
        else:
            # Create human question
            question = HumanQuestion(
                id=f"q-{task_data['id']}-{datetime.now().timestamp()}",
                task_id=task_data["id"],
                question=f"Agent is asking for guidance on task {task_data['id']}",
                context=result.output,
                priority=QuestionPriority.HIGH,
                options=analysis.proposed_actions,
            )
            await self.human_loop.submit_question(question)

            return {
                **state,
                "next_action": "wait_human",
                "human_question_id": question.id,
                "messages": ["Agent asked questions - awaiting human decision"],
            }

    # Existing logic for when work was actually done...
```

### 4. Enhanced State Tracking

Add new task states for better tracking:

```python
class TaskStatus(str, Enum):
    PENDING = "pending"
    IN_PROGRESS = "in_progress"
    AWAITING_DECISION = "awaiting_decision"  # NEW: Agent asked questions
    DEFERRED = "deferred"                     # NEW: Explicitly deferred
    BLOCKED = "blocked"                       # NEW: Blocked by something
    COMPLETED = "completed"
    FAILED = "failed"
```

### 5. Validation Improvements

Make validators detect "no work done" scenarios:

```python
def _extract_actionable_issues(self, validation_results: list[dict]) -> list[dict]:
    # ... existing patterns ...

    # NEW: Pattern for detecting deferral without explicit decision
    for vr in validation_results:
        raw_output = vr.get("raw_output", "") or ""

        # Check for indicators that work wasn't done
        if any(pattern in raw_output.lower() for pattern in [
            "not implemented",
            "does not exist",
            "marked as optional",
            "intentionally deferred",
            "no implementation work has been done",
        ]):
            # Check if there was an explicit decision recorded
            if not self._has_deferral_decision(state):
                actionable_issues.append({
                    "agent": vr.get("agent"),
                    "severity": "high",
                    "description": "Task appears to have no implementation but no explicit deferral decision was made",
                    "pattern_matched": "no_work_no_decision",
                })
```

---

## Implementation Plan

### Phase 1: Response Analysis (Priority: HIGH)
1. Create `ResponseAnalyzer` class
2. Add pattern matching for questions vs. work
3. Integrate into `_execute_implementation_node`
4. Log analysis results for debugging

### Phase 2: Auto-Decision Engine (Priority: HIGH)
1. Create `AutoDecisionEngine` class
2. Define auto-decidable scenarios
3. Add decision logging/audit trail
4. Create new graph node for auto-decisions

### Phase 3: State Tracking (Priority: MEDIUM)
1. Add new `TaskStatus` values
2. Update ground truth schema
3. Add transition validation
4. Create CLI commands for state queries

### Phase 4: Validation Improvements (Priority: MEDIUM)
1. Add "no work done" detection patterns
2. Require explicit deferral decisions
3. Block commits when issues detected

### Phase 5: Human Loop Improvements (Priority: LOW)
1. Better question formatting
2. Default timeout for optional tasks
3. Batch question handling

---

## Testing Strategy

1. **Unit tests** for `ResponseAnalyzer` patterns
2. **Integration tests** with mock agent responses
3. **Replay tests** using task 0.1.5.8 logs as test case
4. **Manual testing** with intentionally vague prompts

---

## Metrics to Track

- Questions asked vs. auto-decided vs. human-answered
- False positive rate (detected as question when it wasn't)
- False negative rate (missed questions)
- Time to resolution for human questions
- Task completion rate before/after changes

---

## Risk Mitigation

1. **Conservative defaults**: When uncertain, escalate to human
2. **Audit logging**: All auto-decisions logged with rationale
3. **Override capability**: Human can always override auto-decisions
4. **Gradual rollout**: Start with just detection, then auto-decision

---

## Appendix: Task 0.1.5.8 Log Analysis

Full log shows the issue clearly:

```
[2026-01-12T11:28:03.419687] agent_response - Task: 0.1.5.8
Success: True  # <-- This is the API success, not work done
...
OUTPUT:
Would you like me to:
1. Update the task status in `ground_truth.json` to mark 0.1.5.8 as "deferred"?
2. Proceed to task 0.1.5.9 (integration tests) to complete phase 0.1.5?
3. Create a placeholder `OverloadResolver.cs` with TODO comments...

[2026-01-12T11:28:06.334738] test_run - Task: 0.1.5.8
Success: True  # <-- Tests pass because nothing changed

[2026-01-12T11:29:41.854338] validation_response - Task: 0.1.5.8
"The file `src/Sharpy.Compiler/Semantic/OverloadResolver.cs` specified in the task does not exist"
"Task 0.1.5.8 "(Optional) Function Overloading" has been explicitly deferred"
# <-- Validators correctly noted no work, but this didn't block commit

[2026-01-12T11:32:23.091082] commit_success - Task: 0.1.5.8
# <-- Empty commit made
```

The fix is to add the "Overseer" analysis layer between agent response and test execution.
