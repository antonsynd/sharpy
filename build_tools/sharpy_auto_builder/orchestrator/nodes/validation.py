"""
Validation nodes for the orchestrator.

Contains nodes related to spec adherence, verification, hallucination defense,
and addressing validation issues.
"""

import re
from typing import TYPE_CHECKING

from ..types import OrchestratorState
from ...agents import AgentRole, get_agent_prompt

if TYPE_CHECKING:
    from ..core import Orchestrator


class ValidationNodes:
    """Mixin class providing validation node implementations."""

    async def _validate_spec_adherence_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Run spec adherence validation."""
        if not self.config.run_spec_adherence_check:
            return state

        task_data = state["current_task"]
        self._log_step_start("validate_spec_adherence", task_data["id"])

        prompt = get_agent_prompt(
            AgentRole.SPEC_ADHERENCE,
            task_title=task_data["title"],
            files=task_data["files"],
        )

        # Log the prompt being sent
        self._log_execution(
            event_type="validation_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={"validation_type": "spec_adherence"},
        )

        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
        )

        # Log the response
        self._log_execution(
            event_type="validation_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"validation_type": "spec_adherence"},
        )

        validation_result = {
            "agent": "spec-adherence",
            "status": "passed" if result.success else "warnings",
            "findings": [],
            "raw_output": result.output,
            "execution_error": result.error if not result.success else None,
        }

        validation_results = state.get("validation_results", [])
        validation_results.append(validation_result)

        # Check for actionable issues
        actionable_issues = self._extract_actionable_issues(validation_results)
        critical_issues = [i for i in actionable_issues if i["severity"] == "critical"]

        # Determine next action
        validation_fix_attempt = state.get("validation_fix_attempt", 0)
        max_attempts = getattr(self.config, "max_validation_fix_attempts", 2)

        if critical_issues and validation_fix_attempt < max_attempts:
            next_action = "address_issues"
            messages = [
                "Spec adherence check found critical issues",
                f"  {len(critical_issues)} critical issue(s) to address",
            ]
        elif critical_issues and task_data.get("is_critical"):
            next_action = "human_review"
            messages = [
                "Spec adherence check found unresolved critical issues",
                "Escalating to human review",
            ]
        else:
            next_action = "verification"
            messages = ["Spec adherence check completed"]

        self._log_step_end("validate_spec_adherence", task_data["id"], result.success)
        return {
            **state,
            "validation_results": validation_results,
            "next_action": next_action,
            "messages": messages,
        }

    async def _validate_verification_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Run verification expert validation."""
        if not self.config.run_verification_after_implementation:
            return {
                **state,
                "next_action": (
                    "commit"
                    if not self.config.run_hallucination_defense
                    else "hallucination_check"
                ),
            }

        task_data = state["current_task"]
        self._log_step_start("validate_verification", task_data["id"])

        prompt = get_agent_prompt(
            AgentRole.VERIFICATION_EXPERT,
            task_title=task_data["title"],
            files=task_data["files"],
            component=task_data["phase"],
        )

        # Log the prompt being sent
        self._log_execution(
            event_type="validation_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={"validation_type": "verification_expert"},
        )

        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
        )

        # Log the response
        self._log_execution(
            event_type="validation_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"validation_type": "verification_expert"},
        )

        validation_result = {
            "agent": "verification-expert",
            "status": "passed" if result.success else "warnings",
            "findings": [],
            "raw_output": result.output,
            "execution_error": result.error if not result.success else None,
        }

        validation_results = state.get("validation_results", [])
        validation_results.append(validation_result)

        # Check for actionable issues
        actionable_issues = self._extract_actionable_issues(validation_results)
        critical_or_high_issues = [
            i for i in actionable_issues if i["severity"] in ("critical", "high")
        ]

        # Determine next action
        validation_fix_attempt = state.get("validation_fix_attempt", 0)
        max_attempts = getattr(self.config, "max_validation_fix_attempts", 2)

        if critical_or_high_issues and validation_fix_attempt < max_attempts:
            next_action = "address_issues"
            messages = [
                "Verification found issues requiring fixes",
                f"  {len(critical_or_high_issues)} issue(s) to address",
            ]
        elif critical_or_high_issues and task_data.get("is_critical"):
            next_action = "human_review"
            messages = [
                "Verification found unresolved issues on critical task",
                "Escalating to human review",
            ]
        elif self.config.run_hallucination_defense:
            next_action = "hallucination_check"
            messages = ["Verification check completed"]
        else:
            next_action = "commit"
            messages = ["Verification check completed"]

        self._log_step_end("validate_verification", task_data["id"], result.success)
        return {
            **state,
            "validation_results": validation_results,
            "next_action": next_action,
            "messages": messages,
        }

    async def _check_hallucinations_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Check for potential hallucinations in the implementation."""
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})
        self._log_step_start("check_hallucinations", task_data["id"])

        # Extract claims from implementation output with smart truncation
        full_output = execution_result.get("output", "")
        if len(full_output) > 2000:
            truncated = full_output[:2000]
            last_sentence_end = max(
                truncated.rfind(". "),
                truncated.rfind(".\n"),
                truncated.rfind("! "),
                truncated.rfind("!\n"),
                truncated.rfind("? "),
                truncated.rfind("?\n"),
            )
            if last_sentence_end > 1000:
                claims = truncated[: last_sentence_end + 1]
            else:
                claims = truncated
            claims += "\n\n[... content truncated for validation, see full output for remaining claims]"
        else:
            claims = full_output

        prompt = get_agent_prompt(
            AgentRole.HALLUCINATION_DEFENSE,
            claims=claims,
        )

        # Log the prompt being sent
        self._log_execution(
            event_type="validation_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={"validation_type": "hallucination_defense"},
        )

        result = await self.backend_manager.execute_with_failover(prompt)

        # Log the response
        self._log_execution(
            event_type="validation_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"validation_type": "hallucination_defense"},
        )

        validation_result = {
            "agent": "hallucination-defense",
            "status": "passed" if result.success else "warnings",
            "findings": [],
            "raw_output": result.output,
            "execution_error": result.error if not result.success else None,
        }

        validation_results = state.get("validation_results", [])
        validation_results.append(validation_result)

        # Check if human review needed
        has_hallucinations = "INCORRECT" in result.output.upper()

        # Check for actionable issues
        actionable_issues = self._extract_actionable_issues(validation_results)

        if (
            actionable_issues
            and state.get("validation_fix_attempt", 0)
            < self.config.max_validation_fix_attempts
        ):
            next_action = "address_issues"
        elif has_hallucinations and task_data.get("is_critical"):
            next_action = "human_review"
        else:
            next_action = "commit"

        self._log_step_end(
            "check_hallucinations", task_data["id"], not has_hallucinations
        )
        return {
            **state,
            "validation_results": validation_results,
            "next_action": next_action,
            "messages": ["Hallucination check completed"],
        }

    async def _address_validation_issues_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Have the agent address critical/high priority validation issues."""
        task_data = state["current_task"]
        validation_fix_attempt = state.get("validation_fix_attempt", 0) + 1
        max_attempts = getattr(self.config, "max_validation_fix_attempts", 2)
        self._log_step_start(
            "address_validation_issues",
            task_data["id"],
            f"attempt {validation_fix_attempt}/{max_attempts}",
        )

        if validation_fix_attempt > max_attempts:
            # Max attempts reached - create follow-up task if configured
            if self.config.create_followup_task_on_fix_failure:
                await self._create_validation_fix_followup_task(state)

            return {
                **state,
                "validation_fix_attempt": validation_fix_attempt,
                "next_action": (
                    "human_review" if task_data.get("is_critical") else "error"
                ),
                "error_message": f"Failed to address validation issues after {max_attempts} attempts",
                "messages": [
                    f"Max validation fix attempts ({max_attempts}) reached",
                    (
                        "Escalating to human review"
                        if task_data.get("is_critical")
                        else "Proceeding with unresolved issues"
                    ),
                    (
                        "Created follow-up task for validation issues"
                        if self.config.create_followup_task_on_fix_failure
                        else ""
                    ),
                ],
            }

        validation_results = state.get("validation_results", [])
        actionable_issues = self._extract_actionable_issues(validation_results)

        # Build detailed prompt for addressing issues
        issues_summary = "\n".join(
            [
                f"- [{issue['severity'].upper()}] ({issue['agent']}): {issue['description']}"
                for issue in actionable_issues[:5]
            ]
        )

        # Collect raw outputs for context
        validation_context = "\n\n---\n\n".join(
            [
                f"## {vr['agent']} Report\n{vr.get('raw_output', 'No output')[:1500]}"
                for vr in validation_results
                if vr.get("status") != "passed"
            ]
        )

        prompt = f"""You previously implemented a task but validation checks found critical issues that need to be addressed.

## Task
{task_data['title']}
{task_data.get('description', '')}

## Actionable Issues Found
{issues_summary}

## Validation Reports
{validation_context[:4000]}

## Fix Attempt
This is validation fix attempt {validation_fix_attempt} of {max_attempts}.

## Instructions
1. Carefully review each issue identified above
2. For spec deviations: Update implementation to match the specification
3. For behavior failures: Fix the code to produce correct behavior
4. For factual errors: Correct any incorrect assumptions or implementations
5. Do NOT modify test expected values to make tests pass - fix the implementation
6. Run tests after your fixes to verify correctness

Focus on addressing the specific issues identified. Do not re-implement the entire task.

{f"Previous fix attempt did not resolve all issues. Please try a different approach." if validation_fix_attempt > 1 else ""}
"""

        # Log the prompt
        self._log_execution(
            event_type="validation_fix_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={
                "validation_fix_attempt": validation_fix_attempt,
                "max_attempts": max_attempts,
                "issues_count": len(actionable_issues),
            },
        )

        # Execute fix via backend
        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data.get("files", [])},
        )

        # Log the response
        self._log_execution(
            event_type="validation_fix_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"validation_fix_attempt": validation_fix_attempt},
        )

        messages = [
            f"Validation fix attempt {validation_fix_attempt}/{max_attempts} {'succeeded' if result.success else 'failed'}"
        ]
        if not result.success and result.error:
            error_preview = (
                result.error[:200] if len(result.error) > 200 else result.error
            )
            messages.append(f"  Error: {error_preview}")

        self._log_step_end("address_validation_issues", task_data["id"], result.success)
        return {
            **state,
            "validation_fix_attempt": validation_fix_attempt,
            "validation_results": [],  # Clear to force re-validation
            "next_action": "test" if result.success else "error",
            "messages": messages,
        }

    def _extract_actionable_issues(
        self: "Orchestrator", validation_results: list[dict]
    ) -> list[dict]:
        """
        Extract actionable issues from validation results.

        Looks for patterns indicating critical/high priority issues that should
        be addressed by the agent before proceeding.

        Returns a list of issues with structure:
        {
            "agent": str,
            "severity": "critical" | "high" | "medium" | "low",
            "description": str,
            "recommendation": str (optional)
        }
        """
        actionable_issues = []

        for vr in validation_results:
            raw_output = vr.get("raw_output", "") or ""
            agent = vr.get("agent", "unknown")
            status = vr.get("status", "")

            # Check for execution errors first
            execution_error = vr.get("execution_error")
            if execution_error:
                actionable_issues.append(
                    {
                        "agent": agent,
                        "severity": "critical",
                        "description": f"Validation execution failed: {execution_error[:300]}",
                        "pattern_matched": "execution_error",
                    }
                )
                continue

            # Pattern 0: Detect permission denied errors
            permission_patterns = [
                r"Permission denied",
                r"could not request permission from user",
                r"✘.*Permission denied",
            ]
            for pattern in permission_patterns:
                if re.search(pattern, raw_output, re.IGNORECASE):
                    actionable_issues.append(
                        {
                            "agent": agent,
                            "severity": "high",
                            "description": f"Agent encountered permission issues - commands could not be executed.",
                            "pattern_matched": "permission_denied",
                        }
                    )
                    break

            # Skip if validation passed cleanly
            if status == "passed":
                continue

            # Pattern 1: Look for explicit severity markers
            severity_patterns = [
                (r"\*\*(?:Impact|Severity)\*\*:\s*(?:High|Critical)", "high"),
                (r"(?:Impact|Severity):\s*(?:High|Critical)", "high"),
                (r"\[(?:HIGH|CRITICAL)\]", "high"),
                (r"❌.*(?:deviation|error|incorrect|broken|failed)", "high"),
                (r"- \[ \].*(?:Section|Requirement)", "medium"),
            ]

            for pattern, severity in severity_patterns:
                matches = re.findall(pattern, raw_output, re.IGNORECASE)
                if matches:
                    for match in re.finditer(pattern, raw_output, re.IGNORECASE):
                        start = max(0, match.start() - 100)
                        end = min(len(raw_output), match.end() + 200)
                        context = raw_output[start:end].strip()

                        actionable_issues.append(
                            {
                                "agent": agent,
                                "severity": severity,
                                "description": context[:300],
                                "pattern_matched": pattern,
                            }
                        )

            # Pattern 2: Look for "Deviations" section with content
            deviations_match = re.search(
                r"### Deviations\s*\n(.*?)(?=###|\Z)",
                raw_output,
                re.DOTALL | re.IGNORECASE,
            )
            if deviations_match:
                deviations_content = deviations_match.group(1).strip()
                if deviations_content and not re.match(
                    r"^(None|N/A|-|No deviations)?\s*$",
                    deviations_content,
                    re.IGNORECASE,
                ):
                    actionable_issues.append(
                        {
                            "agent": agent,
                            "severity": "high",
                            "description": f"Spec deviations found: {deviations_content[:300]}",
                            "pattern_matched": "deviations_section",
                        }
                    )

            # Pattern 3: Look for "Recommendations" section with actionable items
            recommendations_match = re.search(
                r"### (?:Recommendations|Suggestions)\s*\n(.*?)(?=###|\Z)",
                raw_output,
                re.DOTALL | re.IGNORECASE,
            )
            if recommendations_match:
                rec_content = recommendations_match.group(1).strip()
                if re.search(
                    r"\b(fix|update|change|modify|remove|add|implement|correct)\b",
                    rec_content,
                    re.IGNORECASE,
                ):
                    actionable_issues.append(
                        {
                            "agent": agent,
                            "severity": "medium",
                            "description": f"Actionable recommendations: {rec_content[:300]}",
                            "pattern_matched": "recommendations_section",
                        }
                    )

            # Pattern 4: Look for failed behavior checks
            behavior_failures = re.findall(
                r"- \[ \].*?(?:has deviation|failed|incorrect|broken).*",
                raw_output,
                re.IGNORECASE,
            )
            for failure in behavior_failures:
                actionable_issues.append(
                    {
                        "agent": agent,
                        "severity": "high",
                        "description": failure[:200],
                        "pattern_matched": "behavior_failure",
                    }
                )

            # Pattern 5: INCORRECT marker from hallucination defense
            if "INCORRECT" in raw_output.upper():
                incorrect_context = re.search(
                    r"INCORRECT[:\s]*(.*?)(?:\n\n|\Z)",
                    raw_output,
                    re.IGNORECASE | re.DOTALL,
                )
                if incorrect_context:
                    actionable_issues.append(
                        {
                            "agent": agent,
                            "severity": "critical",
                            "description": f"Factual incorrectness: {incorrect_context.group(1)[:200]}",
                            "pattern_matched": "incorrect_marker",
                        }
                    )

        # Filter to only critical and high severity issues
        return [
            issue
            for issue in actionable_issues
            if issue["severity"] in ("critical", "high")
        ]
