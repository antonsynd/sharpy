"""
Core orchestrator module.

This module contains the main Orchestrator class that composes all node
implementations from the nodes subpackage.
"""

import sys
from datetime import datetime
from pathlib import Path
from typing import Optional

from langgraph.store.memory import InMemoryStore
from langgraph.checkpoint.sqlite.aio import AsyncSqliteSaver

from .types import OrchestratorState, create_initial_state
from .graph import build_graph
from .helpers import OrchestratorHelpers
from .followup import FollowupTaskMixin
from .nodes.task_execution import TaskExecutionNodes
from .nodes.validation import ValidationNodes
from .nodes.human_interaction import HumanInteractionNodes
from .nodes.overseer import OverseerNodes
from .nodes.state_management import StateManagementNodes

from ..config import Config
from ..memory import MemoryManager
from ..state import GroundTruth, parse_task_list
from ..backends import BackendManager
from ..human_loop import HumanLoopManager
from ..response_analyzer import ResponseAnalyzer
from ..auto_decision import AutoDecisionEngine
from ..tasks import execute_claude_cli, execute_copilot_cli, _get_fallback_tracker

# Import shared logging utilities
sys.path.insert(0, str(Path(__file__).parent.parent.parent))
from shared.logging import ExecutionLogger


class Orchestrator(
    OrchestratorHelpers,
    FollowupTaskMixin,
    TaskExecutionNodes,
    ValidationNodes,
    HumanInteractionNodes,
    OverseerNodes,
    StateManagementNodes,
):
    """
    Main orchestrator for automated task implementation.

    This class coordinates task execution, validation, and human-in-the-loop
    interactions. It uses LangGraph for state machine management and checkpointing.

    The class is composed of multiple mixins that provide node implementations:
    - TaskExecutionNodes: Task selection, planning, execution, testing, fixing
    - ValidationNodes: Spec adherence, verification, hallucination defense
    - HumanInteractionNodes: Human review requests and question handling
    - OverseerNodes: Response analysis and auto-decision making
    - StateManagementNodes: Ground truth updates, commits, error handling
    - FollowupTaskMixin: Follow-up task creation for failures
    - OrchestratorHelpers: Logging and configuration utilities
    """

    def __init__(self, config: Config):
        self.config = config
        self.config.ensure_directories()

        # Track step start times for duration logging
        self._step_start_times: dict[str, float] = {}

        # Initialize shared execution logger
        self._execution_logger = ExecutionLogger(config.execution_log_path)

        # Initialize components
        self.backend_manager = BackendManager(config)
        self.human_loop = HumanLoopManager(
            config.questions_dir,
            config.answers_dir,
            config.human_review_dir,
        )

        # Initialize response analyzer and auto-decision engine (Overseer components)
        self.response_analyzer = ResponseAnalyzer(
            min_confidence=getattr(config, "response_analysis_confidence", 0.6)
        )
        self.auto_decision_engine = AutoDecisionEngine(
            auto_defer_optional=getattr(config, "auto_defer_optional", True),
            require_high_confidence=True,
            min_confidence=getattr(config, "auto_decision_confidence", 0.7),
        )

        # Load or initialize ground truth
        self.ground_truth = self._load_or_create_ground_truth()

        # Checkpointer will be initialized async in setup()
        self.checkpointer = None
        self._checkpointer_context = None

        # Setup checkpoint cleanup tracking
        self._setup_checkpoint_cleanup()

        # Initialize memory store and manager
        self.memory_store = self._create_memory_store()
        self.memory_manager = MemoryManager(self.memory_store, self.config.memory)

        # Build the graph (app will be compiled in setup())
        self.graph = build_graph(self)
        self.app = None  # Will be set in async setup()

    async def setup(self) -> None:
        """Async setup - must be called before using the orchestrator."""
        import aiosqlite

        # Initialize async SQLite checkpointer
        self._async_db_conn = await aiosqlite.connect(
            str(self.config.checkpoint_db_path)
        )
        # Monkey-patch is_alive method that langgraph expects
        self._async_db_conn.is_alive = lambda: True

        self.checkpointer = AsyncSqliteSaver(self._async_db_conn)

        # Compile the graph with the checkpointer
        self.app = self.graph.compile(
            checkpointer=self.checkpointer, store=self.memory_store
        )

    def _load_or_create_ground_truth(self) -> GroundTruth:
        """Load existing ground truth or create from task list."""
        if self.config.ground_truth_path.exists():
            return GroundTruth.load(self.config.ground_truth_path)

        # Parse task list
        task_list_content = self.config.task_list_path.read_text()
        ground_truth = parse_task_list(task_list_content)
        ground_truth.save(self.config.ground_truth_path)
        return ground_truth

    def _create_memory_store(self) -> Optional[InMemoryStore]:
        """
        Create and configure LangGraph memory store.

        Returns:
            InMemoryStore if memory enabled, None otherwise
        """
        if not self.config.memory.enabled:
            return None

        # Create in-memory store (can be swapped for persistent store later)
        store = InMemoryStore()

        # Configure embeddings if provider specified
        if self.config.memory.embedding_provider == "openai":
            try:
                from langchain_openai import OpenAIEmbeddings

                embeddings = OpenAIEmbeddings(
                    model=self.config.memory.openai_embedding_model
                )
                store = InMemoryStore(index={"embed": embeddings})
                print(
                    f"  Memory store: Enabled with OpenAI embeddings ({self.config.memory.openai_embedding_model})"
                )
            except ImportError:
                print(
                    "  Memory store: Warning - langchain-openai not installed, using exact key matching"
                )
        elif self.config.memory.embedding_provider == "local":
            try:
                from langchain_community.embeddings import HuggingFaceEmbeddings

                embeddings = HuggingFaceEmbeddings(
                    model_name=self.config.memory.local_embedding_model
                )
                store = InMemoryStore(index={"embed": embeddings})
                print(
                    f"  Memory store: Enabled with local embeddings ({self.config.memory.local_embedding_model})"
                )
            except ImportError:
                print(
                    "  Memory store: Warning - sentence-transformers not installed, using exact key matching"
                )
        else:
            print("  Memory store: Enabled with exact key matching (no embeddings)")

        return store

    def _setup_checkpoint_cleanup(self) -> None:
        """Initialize checkpoint cleanup tracking."""
        self._checkpoint_count = 0
        self._cleanup_interval = self.config.checkpoint.cleanup_interval

    def _maybe_cleanup_checkpoints(self, thread_id: str) -> None:
        """Run checkpoint cleanup periodically based on cleanup_interval."""
        self._checkpoint_count += 1
        if self._checkpoint_count >= self._cleanup_interval:
            self._cleanup_thread_checkpoints(thread_id)
            self._checkpoint_count = 0

    def _cleanup_thread_checkpoints(self, thread_id: str) -> None:
        """
        Remove old checkpoints beyond max_checkpoints_per_thread.

        Keeps the most recent N checkpoints per thread, where N is
        config.checkpoint.max_checkpoints_per_thread.
        """
        try:
            max_checkpoints = self.config.checkpoint.max_checkpoints_per_thread

            # Query checkpoints for this thread, ordered by timestamp descending
            cursor = self._db_connection.cursor()
            cursor.execute(
                """
                SELECT checkpoint_id, checkpoint_ns
                FROM checkpoints
                WHERE thread_id = ?
                ORDER BY checkpoint_id DESC
                """,
                (thread_id,),
            )
            checkpoints = cursor.fetchall()

            # If we have more than max_checkpoints, delete the oldest ones
            if len(checkpoints) > max_checkpoints:
                checkpoints_to_delete = checkpoints[max_checkpoints:]
                for checkpoint_id, checkpoint_ns in checkpoints_to_delete:
                    cursor.execute(
                        "DELETE FROM checkpoints WHERE checkpoint_id = ? AND checkpoint_ns = ?",
                        (checkpoint_id, checkpoint_ns),
                    )

                self._db_connection.commit()
                deleted_count = len(checkpoints_to_delete)
                print(
                    f"🧹 Cleaned up {deleted_count} old checkpoints for thread {thread_id}"
                )

        except Exception as e:
            # Don't fail the whole operation if cleanup fails
            print(f"⚠️ Checkpoint cleanup failed: {e}")

    # =========================================================================
    # Task Execution Helpers
    # =========================================================================

    async def _execute_with_task_failover(
        self,
        prompt: str,
        task_id: str,
        attempt: int,
        timeout: float,
    ):
        """
        Execute a task using task functions with failover across backends.

        This method provides:
        1. Fallback idempotency checking (file-based cache)
        2. Backend failover (Claude Code -> Copilot)
        3. Integration with @task decorator for LangGraph idempotency

        Args:
            prompt: The prompt to execute
            task_id: Task identifier for tracking
            attempt: Attempt number
            timeout: Execution timeout in seconds

        Returns:
            TaskExecutionResult: Result from task execution
        """
        # Get fallback idempotency tracker
        tracker = _get_fallback_tracker()

        # Try each backend in priority order
        result = None
        for backend_type in self.config.backend_priority:
            # Check if backend is available
            if backend_type not in self.config.backends:
                continue

            backend_config = self.config.backends[backend_type]

            # Determine which task function to use and build kwargs
            base_kwargs = {
                "prompt": prompt,
                "timeout": timeout,
                "working_dir": self.config.project_root,
                "task_id": task_id,
                "attempt": attempt,
                "use_fallback_idempotency": True,
            }

            if backend_type == "claude_code":
                task_func = execute_claude_cli
                base_kwargs["tools"] = ["Read", "Write", "Edit", "Bash"]
                base_kwargs["model"] = backend_config.model
            elif backend_type == "copilot":
                task_func = execute_copilot_cli
                base_kwargs["tools"] = ["read", "write", "edit", "bash"]
                # Copilot doesn't support model selection - don't pass model kwarg
            else:
                continue

            # Execute task function
            result = await task_func(**base_kwargs)

            # Cache result in fallback tracker
            tracker.cache_result(result.input_hash, result)

            # Return on success
            if result.success:
                return result

            # Continue to next backend on failure (unless CLI not found)
            if result.error and "not found" in result.error.lower():
                continue

        # If all backends failed, return the last result
        return result

    # =========================================================================
    # Public API
    # =========================================================================

    async def run(
        self, max_tasks: Optional[int] = None, thread_id: Optional[str] = None
    ) -> dict:
        """
        Run the orchestrator to process tasks.

        Args:
            max_tasks: Maximum number of tasks to process in this run
            thread_id: Thread ID for checkpoint persistence. If not provided, a new one will be generated.

        Returns:
            dict: Run statistics including tasks_processed, final_progress, backend_stats
        """
        # Dump config at startup for debugging
        self._dump_config()

        # Generate thread ID if not provided
        if thread_id is None:
            thread_id = f"sharpy-build-{datetime.now().strftime('%Y%m%d-%H%M%S')}"

        # Store current thread ID
        self._current_thread_id = thread_id

        # Print thread ID and resume instructions
        print(f"\n{'='*60}")
        print(f"🔗 Thread ID: {thread_id}")
        print(f"{'='*60}")
        print(f"💡 To resume this session later, use:")
        print(f"   ./auto_builder.sh run --thread-id {thread_id}")
        print(f"{'='*60}\n")

        config = {
            "configurable": {"thread_id": thread_id},
            "recursion_limit": 150,
        }

        # Check if we're resuming an existing session
        existing_state = await self.app.aget_state(config)
        is_resume = existing_state.values != {}

        if is_resume:
            print(f"🔄 Resuming existing session from checkpoint...")
            print(f"   Previous state found for thread: {thread_id}\n")
            initial_state = None
        else:
            print(f"🆕 Starting new session...")
            initial_state = create_initial_state(str(self.config.ground_truth_path))

        tasks_processed = 0

        # Run the graph
        if initial_state is not None:
            stream_input = initial_state
        else:
            stream_input = None

        async for event in self.app.astream(stream_input, config):
            # Log progress
            for node_name, node_state in event.items():
                if node_state.get("messages"):
                    for msg in node_state["messages"]:
                        print(f"[{node_name}] {msg}")

            # Check if we've hit the task limit
            if max_tasks and tasks_processed >= max_tasks:
                break

            # Check if task completed
            if event.get("update_ground_truth", {}).get("next_action") == "next_task":
                tasks_processed += 1

        # Get final state
        final_state = await self.app.aget_state(config)

        return {
            "tasks_processed": tasks_processed,
            "final_progress": self.ground_truth.overall_progress,
            "backend_stats": self.backend_manager.get_status(),
        }

    def get_status(self) -> dict:
        """Get current orchestrator status."""
        return {
            "ground_truth_progress": self.ground_truth.overall_progress,
            "current_task": self.ground_truth.current_task_id,
            "phases": [
                {
                    "id": p.id,
                    "name": p.name,
                    "status": p.status.value,
                    "progress": p.progress,
                }
                for p in self.ground_truth.phases
            ],
            "backend_status": self.backend_manager.get_status(),
            "pending_human_questions": len(self.human_loop.get_pending_questions()),
            "pending_human_reviews": len(self.human_loop.get_pending_reviews()),
        }

    def generate_status_report(self) -> str:
        """Generate a comprehensive status report."""
        status = self.get_status()

        lines = [
            "# Sharpy Auto Builder Status Report",
            "",
            f"**Generated:** {datetime.now().isoformat()}",
            f"**Overall Progress:** {status['ground_truth_progress']:.1f}%",
            f"**Current Task:** {status['current_task'] or 'None'}",
            "",
            "## Phases",
            "",
        ]

        for phase in status["phases"]:
            status_emoji = {
                "completed": "✅",
                "in_progress": "🔄",
                "failed": "❌",
                "pending": "⏳",
            }.get(phase["status"], "❓")
            lines.append(
                f"- {status_emoji} **{phase['id']}**: {phase['name']} ({phase['progress']:.0f}%)"
            )

        lines.extend(
            [
                "",
                "## Backend Status",
                "",
            ]
        )

        for backend, bstatus in status["backend_status"].items():
            avail = "✅" if bstatus["available"] else "⏳"
            lines.append(
                f"- **{backend}**: {avail} (wait: {bstatus['wait_time']:.1f}s, errors: {bstatus['consecutive_errors']})"
            )

        lines.extend(
            [
                "",
                "## Human Interactions",
                "",
                f"- Pending Questions: {status['pending_human_questions']}",
                f"- Pending Reviews: {status['pending_human_reviews']}",
            ]
        )

        return "\n".join(lines)

    def get_checkpoint_stats(self) -> dict:
        """
        Get statistics about checkpoint storage.

        Returns:
            dict: Statistics including checkpoint counts, thread info, and database size
        """
        try:
            cursor = self._db_connection.cursor()

            # Get total checkpoint count
            cursor.execute("SELECT COUNT(*) FROM checkpoints")
            total_checkpoints = cursor.fetchone()[0]

            # Get unique thread count
            cursor.execute("SELECT COUNT(DISTINCT thread_id) FROM checkpoints")
            unique_threads = cursor.fetchone()[0]

            # Get checkpoints per thread
            cursor.execute(
                """
                SELECT thread_id, COUNT(*) as count
                FROM checkpoints
                GROUP BY thread_id
                ORDER BY count DESC
                """
            )
            thread_stats = [
                {"thread_id": row[0], "checkpoint_count": row[1]}
                for row in cursor.fetchall()
            ]

            # Get database file size
            import os

            db_size = os.path.getsize(self.config.checkpoint_db_path)

            return {
                "total_checkpoints": total_checkpoints,
                "unique_threads": unique_threads,
                "db_size_bytes": db_size,
                "db_size_mb": round(db_size / (1024 * 1024), 2),
                "thread_stats": thread_stats,
                "max_checkpoints_per_thread": self.config.checkpoint.max_checkpoints_per_thread,
                "cleanup_interval": self.config.checkpoint.cleanup_interval,
            }

        except Exception as e:
            return {"error": str(e)}

    async def aclose(self) -> None:
        """Close the async database connection."""
        if hasattr(self, "_async_db_conn") and self._async_db_conn:
            try:
                await self._async_db_conn.close()
            except Exception:
                pass

    def close(self) -> None:
        """Close the database connection (sync wrapper for cleanup)."""
        pass

    def __del__(self) -> None:
        """Cleanup when object is destroyed."""
        pass

    def __enter__(self) -> "Orchestrator":
        """Context manager entry."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        """Context manager exit."""
        pass

    async def __aenter__(self) -> "Orchestrator":
        """Async context manager entry."""
        await self.setup()
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb) -> None:
        """Async context manager exit - close database connection."""
        await self.aclose()
