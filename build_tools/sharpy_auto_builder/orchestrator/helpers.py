"""
Helper utilities for the orchestrator.

Contains logging, configuration dumping, and other utility methods.
"""

import time
from datetime import datetime
from typing import TYPE_CHECKING, Any, Optional

if TYPE_CHECKING:
    from .core import Orchestrator


class OrchestratorHelpers:
    """Mixin class providing helper methods for the orchestrator."""

    def _log_execution(
        self: "Orchestrator",
        event_type: str,
        task_id: str,
        prompt: str | None = None,
        output: str | None = None,
        error: str | None = None,
        success: bool | None = None,
        backend: str | None = None,
        duration: float | None = None,
        extra: dict | None = None,
    ) -> None:
        """
        Append an execution event to the JSONL log file.

        Uses the shared ExecutionLogger for consistent logging format across
        all build tools. Maintains backward compatibility with existing callers.
        """
        # Build details dictionary
        details: dict[str, Any] = {"task_id": task_id}
        if prompt is not None:
            details["prompt"] = prompt
        if output is not None:
            details["output"] = output
        if error is not None:
            details["error"] = error
        if success is not None:
            details["success"] = success
        if backend is not None:
            details["backend"] = backend
        if duration is not None:
            details["duration_seconds"] = duration
        if extra:
            details.update(extra)

        # Log using shared ExecutionLogger
        self._execution_logger.log(event_type, details)

    def _log_step_start(
        self: "Orchestrator", step_name: str, task_id: str, details: str = ""
    ) -> None:
        """Log when a potentially long-running step begins."""
        import sys
        from pathlib import Path
        sys.path.insert(0, str(Path(__file__).parent.parent.parent))
        from shared.logging import LogEventType

        self._step_start_times[step_name] = time.time()
        timestamp = datetime.now().strftime("%H:%M:%S")
        detail_str = f" - {details}" if details else ""
        print(f"⏳ [{timestamp}] Starting: {step_name}{detail_str}")

        # Also log to JSONL using shared logger
        self._execution_logger.log(
            LogEventType.STEP_START,
            {"task_id": task_id, "step_name": step_name, "details": details},
        )

    def _log_step_end(
        self: "Orchestrator",
        step_name: str,
        task_id: str,
        success: bool = True,
        details: str = "",
    ) -> None:
        """Log when a step completes with duration."""
        import sys
        from pathlib import Path
        sys.path.insert(0, str(Path(__file__).parent.parent.parent))
        from shared.logging import LogEventType

        duration = None
        if step_name in self._step_start_times:
            duration = time.time() - self._step_start_times.pop(step_name)

        timestamp = datetime.now().strftime("%H:%M:%S")
        status = "✅" if success else "❌"
        duration_str = f" ({duration:.1f}s)" if duration else ""
        detail_str = f" - {details}" if details else ""
        print(
            f"{status} [{timestamp}] Completed: {step_name}{duration_str}{detail_str}"
        )

        # Also log to JSONL using shared logger
        self._execution_logger.log(
            LogEventType.STEP_END,
            {
                "task_id": task_id,
                "step_name": step_name,
                "success": success,
                "details": details,
                "duration_seconds": duration,
            },
        )

    def _dump_config(self: "Orchestrator") -> None:
        """Dump the current configuration at startup for debugging."""
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        config_dict = self.config.to_dict()

        print("\n" + "=" * 70)
        print(f"🔧 ORCHESTRATOR CONFIGURATION (as of {timestamp})")
        print("=" * 70)

        # Key settings summary
        print("\n📋 Key Settings:")
        print(f"  • Project root: {self.config.project_root}")
        print(f"  • Task list: {self.config.task_list_path}")
        print(f"  • Ground truth: {self.config.ground_truth_path}")

        print("\n🔍 Validation Settings:")
        print(f"  • Spec adherence check: {self.config.run_spec_adherence_check}")
        print(
            f"  • Verification after implementation: {self.config.run_verification_after_implementation}"
        )
        print(f"  • Hallucination defense: {self.config.run_hallucination_defense}")

        print("\n🔄 Retry/Fix Settings:")
        print(f"  • Max retries per task: {self.config.max_retries_per_task}")
        print(f"  • Max test fix attempts: {self.config.max_test_fix_attempts}")
        print(
            f"  • Max validation fix attempts: {self.config.max_validation_fix_attempts}"
        )
        print(
            f"  • Create follow-up on fix failure: {self.config.create_followup_task_on_fix_failure}"
        )

        print("\n⚙️ Execution Settings:")
        print(f"  • Auto commit: {self.config.auto_commit}")
        print(f"  • Create PR: {self.config.create_pr}")
        print(f"  • Test timeout: {self.config.test_timeout}s")
        print(f"  • Agent execution timeout: {self.config.agent_execution_timeout}s")
        print(f"  • Agent heartbeat interval: {self.config.agent_heartbeat_interval}s")
        print(
            f"  • Human approval for critical: {self.config.require_human_approval_for_critical}"
        )

        print("\n🤖 Backend Configuration:")
        for name, backend in self.config.backends.items():
            status = "✅ enabled" if backend.enabled else "❌ disabled"
            print(f"  • {name}: {status}")
            if backend.enabled:
                print(f"      Model: {backend.model}")
                print(f"      Max tokens: {backend.max_tokens}")
                print(
                    f"      Rate limit: {backend.rate_limit.max_requests_per_window}/hour"
                )
                print(f"      Cooldown: {backend.rate_limit.request_cooldown}s")

        print(f"\n  Backend priority: {' → '.join(self.config.backend_priority)}")

        print("\n" + "=" * 70 + "\n")

        # Also log to execution log as JSON
        self._log_execution(
            event_type="orchestrator_start",
            task_id="system",
            extra={"config": config_dict},
        )
