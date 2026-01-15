"""
Command-line interface for Sharpy Auto Builder.
"""

import asyncio
import argparse
import json
import sqlite3
import sys
from pathlib import Path
from datetime import datetime
from typing import Optional

from .config import Config
from .state import GroundTruth, parse_task_list
from .orchestrator import Orchestrator
from .human_loop import HumanLoopManager
from .interrupt_handler import display_interrupt, collect_response
from .memory import MemoryManager

# Import Command for interrupt resume
from langgraph.types import Command


async def run_with_interrupts(
    orchestrator: Orchestrator, thread_id: str, max_tasks: Optional[int] = None
) -> dict:
    """
    Run the orchestrator with interrupt handling for human-in-the-loop.

    This function handles LangGraph interrupts by:
    1. Invoking the graph and checking for __interrupt__ in the result
    2. If interrupted, displaying the payload and collecting user response
    3. Resuming with Command(resume=response)
    4. Continuing until completion or rate limit pause

    Args:
        orchestrator: The Orchestrator instance
        thread_id: Thread ID for checkpoint persistence
        max_tasks: Maximum number of tasks to process (optional)

    Returns:
        dict: Run statistics including tasks_processed, final_progress, backend_stats
    """
    config = {
        "configurable": {"thread_id": thread_id},
        "recursion_limit": 150,
    }

    # Check if we're resuming an existing session
    existing_state = orchestrator.app.get_state(config)
    is_resume = existing_state.values != {}

    if is_resume:
        print(f"🔄 Resuming existing session from checkpoint...")
        print(f"   Previous state found for thread: {thread_id}\n")
        input_data = None  # Resume from checkpoint
    else:
        print(f"🆕 Starting new session...")
        input_data = orchestrator._create_initial_state()

    tasks_processed = 0
    iteration = 0

    while True:
        iteration += 1

        # Invoke the graph (single step)
        try:
            result = await orchestrator.app.ainvoke(input_data, config)
        except Exception as e:
            print(f"[red]Error during graph execution: {e}[/red]")
            break

        # Check if we hit an interrupt
        if "__interrupt__" in result:
            interrupt_data = result["__interrupt__"][0]  # Get first interrupt
            interrupt_value = interrupt_data.value

            print(f"\n{'='*60}")
            print("⏸️  INTERRUPT - Human Input Required")
            print(f"{'='*60}\n")

            # Display the interrupt to the user
            display_interrupt(interrupt_value)

            # Collect response from user
            response = collect_response(interrupt_value)

            # Resume with the response
            print(f"\n{'='*60}")
            print("▶️  Resuming execution...")
            print(f"{'='*60}\n")

            input_data = Command(resume=response)
            continue

        # No interrupt - check for completion conditions
        next_action = result.get("next_action", "")

        # Check for completion
        if next_action == "complete":
            print(f"\n✅ Session complete!")
            break

        # Check for rate limit pause
        if next_action == "pause_rate_limited":
            print(f"\n⏸️  Session paused due to rate limiting")
            print(
                f"   Resume later with: ./auto_builder.sh run --thread-id {thread_id}"
            )
            break

        # Track task completion
        if next_action == "next_task":
            tasks_processed += 1
            print(f"\n📋 Tasks completed: {tasks_processed}")

            # Check max tasks limit
            if max_tasks and tasks_processed >= max_tasks:
                print(f"\n🎯 Reached maximum task limit ({max_tasks})")
                break

        # Continue with next iteration (no input needed, resume from checkpoint)
        input_data = None

        # Safety limit to prevent infinite loops
        if iteration > 1000:
            print(f"\n⚠️  Reached safety iteration limit (1000)")
            break

    # Get final state and stats
    final_state = orchestrator.app.get_state(config)

    return {
        "tasks_processed": tasks_processed,
        "final_progress": orchestrator.ground_truth.overall_progress,
        "backend_stats": orchestrator.backend_manager.get_status(),
    }


def list_sessions(config: Config) -> list[dict]:
    """
    Query checkpoint database for unique thread IDs and session info.

    Args:
        config: Configuration object with checkpoint_db_path

    Returns:
        list[dict]: List of sessions with thread_id, checkpoint_count, and last_updated
    """
    db_path = config.checkpoint_db_path
    if not db_path.exists():
        return []

    sessions = []
    try:
        conn = sqlite3.connect(str(db_path))
        cursor = conn.cursor()

        # Query for unique thread IDs with checkpoint count and most recent checkpoint
        cursor.execute(
            """
            SELECT thread_id, COUNT(*) as checkpoint_count, MAX(checkpoint_id) as last_checkpoint
            FROM checkpoints
            GROUP BY thread_id
            ORDER BY last_checkpoint DESC
            """
        )

        for row in cursor.fetchall():
            sessions.append(
                {
                    "thread_id": row[0],
                    "checkpoint_count": row[1],
                    "last_checkpoint_id": row[2],
                }
            )

        conn.close()
    except Exception as e:
        print(f"Warning: Could not query checkpoint database: {e}")

    return sessions


def cmd_init(args):
    """Initialize the auto builder state."""
    # Load from config file if provided, otherwise use defaults
    if args.config:
        config_path = Path(args.config)
        if not config_path.exists():
            print(f"Error: Config file not found: {config_path}")
            sys.exit(1)
        config = Config.load(config_path)
        print(f"Loaded config from: {config_path}")
    else:
        config = Config()

    # Override with command-line arguments
    if args.project_root:
        config.project_root = Path(args.project_root)
    if args.task_list:
        config.task_list_path = Path(args.task_list)

    # Validate required fields
    if config.task_list_path is None:
        print(
            "Error: task_list_path is required. Provide via --task-list or in config.json"
        )
        sys.exit(1)

    if not config.task_list_path.exists():
        print(f"Error: Task list file not found: {config.task_list_path}")
        sys.exit(1)

    config.ensure_directories()

    # Parse task list and create ground truth
    task_list_content = config.task_list_path.read_text()
    ground_truth = parse_task_list(task_list_content)
    ground_truth.save(config.ground_truth_path)

    # Save config
    config.save()

    print(f"Initialized Sharpy Auto Builder")
    print(f"  Ground truth: {config.ground_truth_path}")
    print(f"  Config: {config.state_dir / 'config.json'}")
    print(f"  Phases: {len(ground_truth.phases)}")
    print(f"  Total tasks: {sum(len(p.tasks) for p in ground_truth.phases)}")


def cmd_status(args):
    """Show current status."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    if not config.ground_truth_path.exists():
        print("Error: Ground truth not found. Run 'init' first.")
        sys.exit(1)

    ground_truth = GroundTruth.load(config.ground_truth_path)
    human_loop = HumanLoopManager(
        config.questions_dir,
        config.answers_dir,
        config.human_review_dir,
    )

    print(f"\n{'='*60}")
    print("SHARPY AUTO BUILDER STATUS")
    print(f"{'='*60}")
    print(f"Overall Progress: {ground_truth.overall_progress:.1f}%")
    print(f"Current Task: {ground_truth.current_task_id or 'None'}")
    print(f"Total Attempts: {ground_truth.total_attempts}")
    print(f"Successes: {ground_truth.total_successes}")
    print(f"Failures: {ground_truth.total_failures}")
    print()

    print("PHASES:")
    for phase in ground_truth.phases:
        status_char = {
            "completed": "✓",
            "in_progress": "→",
            "failed": "✗",
            "pending": " ",
        }.get(phase.status.value, "?")

        completed = sum(1 for t in phase.tasks if t.status.value == "completed")
        total = len(phase.tasks)

        print(f"  [{status_char}] {phase.id}: {phase.name}")
        print(f"      Progress: {completed}/{total} tasks ({phase.progress:.0f}%)")

    print()
    print("HUMAN INTERACTIONS:")
    pending_q = human_loop.get_pending_questions()
    pending_r = human_loop.get_pending_reviews()
    print(f"  Pending Questions: {len(pending_q)}")
    print(f"  Pending Reviews: {len(pending_r)}")

    if pending_q:
        print()
        print("  Questions awaiting answers:")
        for q in pending_q[:5]:
            print(f"    - [{q.priority.value}] {q.id}: {q.question[:60]}...")

    if pending_r:
        print()
        print("  Reviews awaiting response:")
        for r in pending_r[:5]:
            print(f"    - {r.id}: {r.title}")

    print()
    print("BACKEND STATS:")
    for backend, stats in ground_truth.backend_stats.items():
        print(f"  {backend}: {stats['successes']}/{stats['attempts']} successful")


def cmd_run(args):
    """Run the auto builder."""
    # Load config from file if it exists, otherwise use defaults
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    config_path = config.state_dir / "config.json"
    if config_path.exists():
        config = Config.load(config_path)
        if args.project_root:
            config.project_root = Path(args.project_root)

    # Handle --list-sessions before anything else
    if args.list_sessions:
        sessions = list_sessions(config)
        if not sessions:
            print("No saved sessions found.")
            print(f"  (No checkpoint database at {config.checkpoint_db_path})")
        else:
            print(f"\n{'='*60}")
            print("SAVED SESSIONS")
            print(f"{'='*60}")
            for session in sessions:
                print(f"\n  Thread ID: {session['thread_id']}")
                print(f"    Checkpoints: {session['checkpoint_count']}")
                print(
                    f"    Resume with: ./auto_builder.sh run --thread-id {session['thread_id']}"
                )
            print()
        return

    if not config.ground_truth_path.exists():
        print("Error: Ground truth not found. Run 'init' first.")
        sys.exit(1)

    # Configure backends
    if args.backend:
        # Disable non-selected backends
        for backend_type in config.backends:
            if backend_type != args.backend:
                config.backends[backend_type].enabled = False

    if args.model:
        for backend in config.backends.values():
            backend.model = args.model

    # Configure validation
    config.run_spec_adherence_check = not args.skip_spec_check
    config.run_verification_after_implementation = not args.skip_verification
    config.run_hallucination_defense = not args.skip_hallucination_check
    config.require_human_approval_for_critical = not args.no_human_approval

    print(f"Starting Sharpy Auto Builder...")
    print(f"  Backend priority: {config.backend_priority}")
    print(f"  Max tasks: {args.max_tasks or 'unlimited'}")
    if args.thread_id:
        print(f"  Resuming thread: {args.thread_id}")
    print(
        f"  Spec check: {'enabled' if config.run_spec_adherence_check else 'disabled'}"
    )
    print(
        f"  Verification: {'enabled' if config.run_verification_after_implementation else 'disabled'}"
    )
    print(
        f"  Hallucination check: {'enabled' if config.run_hallucination_defense else 'disabled'}"
    )
    print()

    orchestrator = Orchestrator(config)

    # Generate thread ID if not provided
    thread_id = args.thread_id
    if thread_id is None:
        thread_id = f"sharpy-build-{datetime.now().strftime('%Y%m%d-%H%M%S')}"

    # Print thread ID and resume instructions
    print(f"\n{'='*60}")
    print(f"🔗 Thread ID: {thread_id}")
    print(f"{'='*60}")
    print(f"💡 To resume this session later, use:")
    print(f"   ./auto_builder.sh run --thread-id {thread_id}")
    print(f"{'='*60}\n")

    try:
        # Use the new interrupt-aware run function
        result = asyncio.run(
            run_with_interrupts(orchestrator, thread_id, max_tasks=args.max_tasks)
        )

        print()
        print(f"{'='*60}")
        print("RUN COMPLETE")
        print(f"{'='*60}")
        print(f"Tasks processed: {result['tasks_processed']}")
        print(f"Final progress: {result['final_progress']:.1f}%")

    except KeyboardInterrupt:
        print("\nInterrupted by user")
        sys.exit(1)


def cmd_report(args):
    """Generate a detailed report."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    if not config.ground_truth_path.exists():
        print("Error: Ground truth not found. Run 'init' first.")
        sys.exit(1)

    orchestrator = Orchestrator(config)
    report = orchestrator.generate_status_report()

    if args.output:
        output_path = Path(args.output)
        output_path.write_text(report)
        print(f"Report saved to: {output_path}")
    else:
        print(report)


def cmd_answer(args):
    """Submit an answer to a pending question."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    answer_data = {
        "answer": args.answer,
        "notes": args.notes or "",
    }

    answer_file = config.answers_dir / f"{args.question_id}.json"
    with open(answer_file, "w") as f:
        json.dump(answer_data, f, indent=2)

    print(f"Answer saved: {answer_file}")


def cmd_review(args):
    """Submit a review response."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    if args.status not in ["approved", "rejected", "needs_changes"]:
        print(
            f"Error: Invalid status '{args.status}'. Must be: approved, rejected, needs_changes"
        )
        sys.exit(1)

    response_data = {
        "status": args.status,
        "notes": args.notes or "",
    }

    response_file = config.human_review_dir / f"{args.review_id}_response.json"
    with open(response_file, "w") as f:
        json.dump(response_data, f, indent=2)

    print(f"Review response saved: {response_file}")


def cmd_reset(args):
    """Reset a task to pending status."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    if not config.ground_truth_path.exists():
        print("Error: Ground truth not found. Run 'init' first.")
        sys.exit(1)

    ground_truth = GroundTruth.load(config.ground_truth_path)

    from .state import TaskStatus

    # If --from is specified, reset a range of tasks
    if args.from_task:
        # Collect all task IDs in order
        all_tasks = []
        for phase in ground_truth.phases:
            for task in phase.tasks:
                all_tasks.append(task)

        # Find the indices of the from and to tasks
        from_idx = None
        to_idx = None
        for idx, task in enumerate(all_tasks):
            if task.id == args.from_task:
                from_idx = idx
            if task.id == args.task_id:
                to_idx = idx

        if from_idx is None:
            print(f"Error: Task '{args.from_task}' not found")
            sys.exit(1)
        if to_idx is None:
            print(f"Error: Task '{args.task_id}' not found")
            sys.exit(1)

        # Ensure from_idx <= to_idx
        if from_idx > to_idx:
            from_idx, to_idx = to_idx, from_idx

        # Reset all tasks in the range
        tasks_to_reset = all_tasks[from_idx : to_idx + 1]
        for task in tasks_to_reset:
            task.status = TaskStatus.PENDING
            task.executions = []
            task.human_question = None
            task.human_answer = None

        ground_truth.save(config.ground_truth_path)
        print(
            f"Reset {len(tasks_to_reset)} tasks from {args.from_task} to {args.task_id}:"
        )
        for task in tasks_to_reset:
            print(f"  - {task.id}: {task.title}")
    else:
        # Single task reset
        task = ground_truth.get_task(args.task_id)

        if not task:
            print(f"Error: Task '{args.task_id}' not found")
            sys.exit(1)

        task.status = TaskStatus.PENDING
        task.executions = []
        task.human_question = None
        task.human_answer = None

        ground_truth.save(config.ground_truth_path)
        print(f"Task {args.task_id} reset to pending")


def cmd_skip(args):
    """Skip a task."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    if not config.ground_truth_path.exists():
        print("Error: Ground truth not found. Run 'init' first.")
        sys.exit(1)

    ground_truth = GroundTruth.load(config.ground_truth_path)
    task = ground_truth.get_task(args.task_id)

    if not task:
        print(f"Error: Task '{args.task_id}' not found")
        sys.exit(1)

    from .state import TaskStatus

    task.status = TaskStatus.SKIPPED
    task.notes.append(f"Skipped by user: {args.reason or 'No reason given'}")

    ground_truth.save(config.ground_truth_path)
    print(f"Task {args.task_id} marked as skipped")


def cmd_logs(args):
    """View execution logs."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    log_path = config.execution_log_path
    if not log_path.exists():
        print("No execution logs found yet. Run 'run' to generate logs.")
        sys.exit(0)

    import json

    # Read all log entries
    entries = []
    with open(log_path, "r") as f:
        for line in f:
            line = line.strip()
            if line:
                entries.append(json.loads(line))

    # Filter by task_id if specified
    if args.task_id:
        entries = [e for e in entries if e.get("task_id") == args.task_id]

    # Filter by event type if specified
    if args.event_type:
        entries = [e for e in entries if e.get("event_type") == args.event_type]

    # Limit entries
    if args.last:
        entries = entries[-args.last :]

    if not entries:
        print("No matching log entries found.")
        sys.exit(0)

    # Display entries
    for entry in entries:
        timestamp = entry.get("timestamp", "?")
        event_type = entry.get("event_type", "?")
        task_id = entry.get("task_id", "?")

        print(f"\n{'='*80}")
        print(f"[{timestamp}] {event_type} - Task: {task_id}")
        print(f"{'='*80}")

        if entry.get("success") is not None:
            print(f"Success: {entry['success']}")
        if entry.get("backend"):
            print(f"Backend: {entry['backend']}")
        if entry.get("duration_seconds"):
            print(f"Duration: {entry['duration_seconds']:.2f}s")
        if entry.get("attempt"):
            print(f"Attempt: {entry['attempt']}")

        if args.show_prompt and entry.get("prompt"):
            print(f"\n--- PROMPT ---")
            prompt = entry["prompt"]
            if args.truncate and len(prompt) > args.truncate:
                print(
                    prompt[: args.truncate]
                    + f"\n... [truncated, {len(prompt)} chars total]"
                )
            else:
                print(prompt)

        if entry.get("output"):
            print(f"\n--- OUTPUT ---")
            output = entry["output"]
            if args.truncate and len(output) > args.truncate:
                print(
                    output[: args.truncate]
                    + f"\n... [truncated, {len(output)} chars total]"
                )
            else:
                print(output)

        if entry.get("error"):
            print(f"\n--- ERROR ---")
            print(entry["error"])


def cmd_checkpoint_stats(args):
    """Show checkpoint storage statistics."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    db_path = config.checkpoint_db_path
    if not db_path.exists():
        print("No checkpoint database found.")
        print(f"  (Expected at {db_path})")
        print("  Run the auto builder first to create checkpoints.")
        return

    # Get stats using the orchestrator
    orchestrator = Orchestrator(config)
    stats = orchestrator.get_checkpoint_stats()

    if "error" in stats:
        print(f"Error getting checkpoint stats: {stats['error']}")
        sys.exit(1)

    print(f"\n{'='*60}")
    print("CHECKPOINT STATISTICS")
    print(f"{'='*60}")
    print(f"\nDatabase: {db_path}")
    print(f"Database size: {stats['db_size_mb']} MB ({stats['db_size_bytes']} bytes)")
    print(f"\nTotal checkpoints: {stats['total_checkpoints']}")
    print(f"Unique threads: {stats['unique_threads']}")
    print(f"Max checkpoints per thread: {stats['max_checkpoints_per_thread']}")
    print(f"Cleanup interval: {stats['cleanup_interval']}")

    if stats["thread_stats"]:
        print(f"\nCheckpoints per thread:")
        for thread in stats["thread_stats"][:10]:  # Show top 10
            print(f"  {thread['thread_id']}: {thread['checkpoint_count']}")
        if len(stats["thread_stats"]) > 10:
            print(f"  ... and {len(stats['thread_stats']) - 10} more threads")

    print()


def cmd_checkpoint_cleanup(args):
    """Clean up old checkpoints."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    db_path = config.checkpoint_db_path
    if not db_path.exists():
        print("No checkpoint database found.")
        return

    try:
        conn = sqlite3.connect(str(db_path))
        cursor = conn.cursor()

        # Get current counts
        cursor.execute("SELECT COUNT(*) FROM checkpoints")
        total_before = cursor.fetchone()[0]

        if args.thread_id:
            # Clean up specific thread
            cursor.execute(
                "SELECT COUNT(*) FROM checkpoints WHERE thread_id = ?",
                (args.thread_id,),
            )
            thread_count = cursor.fetchone()[0]

            if thread_count == 0:
                print(f"No checkpoints found for thread: {args.thread_id}")
                conn.close()
                return

            # Get checkpoints to keep
            keep = args.keep or 10
            cursor.execute(
                """
                SELECT checkpoint_id, checkpoint_ns
                FROM checkpoints
                WHERE thread_id = ?
                ORDER BY checkpoint_id DESC
                """,
                (args.thread_id,),
            )
            all_checkpoints = cursor.fetchall()

            to_delete = all_checkpoints[keep:]

            if not to_delete:
                print(
                    f"Thread {args.thread_id} has {len(all_checkpoints)} checkpoints, keeping all (--keep={keep})"
                )
                conn.close()
                return

            if args.dry_run:
                print(
                    f"DRY RUN: Would delete {len(to_delete)} checkpoints from thread {args.thread_id}"
                )
                print(f"  (Keeping newest {keep} of {len(all_checkpoints)})")
            else:
                for checkpoint_id, checkpoint_ns in to_delete:
                    cursor.execute(
                        "DELETE FROM checkpoints WHERE checkpoint_id = ? AND checkpoint_ns = ?",
                        (checkpoint_id, checkpoint_ns),
                    )
                conn.commit()
                print(
                    f"Deleted {len(to_delete)} checkpoints from thread {args.thread_id}"
                )
                print(f"  (Kept newest {keep} of {len(all_checkpoints)})")
        else:
            # Clean up all threads
            keep = args.keep or 10

            # Get all threads
            cursor.execute("SELECT DISTINCT thread_id FROM checkpoints")
            threads = [row[0] for row in cursor.fetchall()]

            total_deleted = 0
            for thread_id in threads:
                cursor.execute(
                    """
                    SELECT checkpoint_id, checkpoint_ns
                    FROM checkpoints
                    WHERE thread_id = ?
                    ORDER BY checkpoint_id DESC
                    """,
                    (thread_id,),
                )
                all_checkpoints = cursor.fetchall()
                to_delete = all_checkpoints[keep:]

                if to_delete:
                    if args.dry_run:
                        print(
                            f"  DRY RUN: Would delete {len(to_delete)} from {thread_id}"
                        )
                    else:
                        for checkpoint_id, checkpoint_ns in to_delete:
                            cursor.execute(
                                "DELETE FROM checkpoints WHERE checkpoint_id = ? AND checkpoint_ns = ?",
                                (checkpoint_id, checkpoint_ns),
                            )
                        total_deleted += len(to_delete)

            if args.dry_run:
                cursor.execute("SELECT COUNT(*) FROM checkpoints")
                would_keep = sum(
                    min(
                        keep,
                        len(
                            [
                                c
                                for c in cursor.execute(
                                    "SELECT checkpoint_id FROM checkpoints WHERE thread_id = ?",
                                    (t,),
                                ).fetchall()
                            ]
                        ),
                    )
                    for t in threads
                )
                print(
                    f"\nDRY RUN: Would delete approximately {total_before - would_keep} checkpoints total"
                )
            else:
                conn.commit()
                cursor.execute("SELECT COUNT(*) FROM checkpoints")
                total_after = cursor.fetchone()[0]
                print(
                    f"Deleted {total_deleted} checkpoints ({total_before} -> {total_after})"
                )

        conn.close()

    except Exception as e:
        print(f"Error during cleanup: {e}")
        sys.exit(1)


def cmd_memory_search(args):
    """Search for patterns in memory store."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    if not config.memory.enabled:
        print("Memory store is disabled in configuration.")
        sys.exit(1)

    db_path = config.memory_store_path
    if not db_path.exists():
        print("No memory store database found.")
        print(f"  (Expected at {db_path})")
        print("  Run the auto builder first to create patterns.")
        return

    # Create memory manager
    from langgraph.store.memory import InMemoryStore

    try:
        store = InMemoryStore()
        memory_manager = MemoryManager(store, config.memory)
    except Exception as e:
        print(f"Error initializing memory manager: {e}")
        sys.exit(1)

    # Map namespace names to tuples
    namespace_map = {
        "implementation": MemoryManager.NS_IMPLEMENTATION,
        "errors": MemoryManager.NS_ERRORS,
        "codebase": MemoryManager.NS_CODEBASE,
        "spec": MemoryManager.NS_SPEC,
    }

    namespace = None
    if args.namespace:
        if args.namespace not in namespace_map:
            print(
                f"Error: Invalid namespace '{args.namespace}'. Must be one of: {', '.join(namespace_map.keys())}"
            )
            sys.exit(1)
        namespace = namespace_map[args.namespace]

    # Search patterns
    try:
        if namespace:
            patterns = memory_manager.search_patterns(
                query=args.query, namespace=namespace, limit=args.limit
            )
        else:
            # Search across all namespaces
            all_patterns = []
            for ns in namespace_map.values():
                patterns = memory_manager.search_patterns(
                    query=args.query, namespace=ns, limit=args.limit
                )
                all_patterns.extend(patterns)
            patterns = all_patterns[: args.limit]

        if not patterns:
            print(f'No patterns found matching query: "{args.query}"')
            if namespace:
                print(f"  (searched in namespace: {args.namespace})")
            else:
                print(f"  (searched across all namespaces)")
            return

        print(f"\n{'='*60}")
        print(f"MEMORY SEARCH RESULTS: {len(patterns)} patterns found")
        print(f"{'='*60}\n")

        for i, pattern in enumerate(patterns, 1):
            print(f"[{i}] {pattern.id}")
            print(f"    Namespace: {'/'.join(pattern.namespace)}")
            print(f"    Type: {pattern.task_type}")
            print(f"    Description: {pattern.description}")
            print(f"    Created: {pattern.created_at.strftime('%Y-%m-%d %H:%M:%S')}")
            if pattern.files:
                print(f"    Files: {', '.join(pattern.files[:3])}")
                if len(pattern.files) > 3:
                    print(f"           ... and {len(pattern.files) - 3} more")
            if pattern.tags:
                print(f"    Tags: {', '.join(pattern.tags)}")
            print(f"    Solution preview: {pattern.solution[:100]}...")
            print()

    except Exception as e:
        print(f"Error searching patterns: {e}")
        sys.exit(1)


def cmd_memory_stats(args):
    """Show memory store statistics."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    if not config.memory.enabled:
        print("Memory store is disabled in configuration.")
        sys.exit(1)

    db_path = config.memory_store_path
    if not db_path.exists():
        print("No memory store database found.")
        print(f"  (Expected at {db_path})")
        return

    # Create memory manager
    from langgraph.store.memory import InMemoryStore

    try:
        store = InMemoryStore()
        memory_manager = MemoryManager(store, config.memory)
    except Exception as e:
        print(f"Error initializing memory manager: {e}")
        sys.exit(1)

    # Get stats for each namespace
    namespace_map = {
        "implementation": MemoryManager.NS_IMPLEMENTATION,
        "errors": MemoryManager.NS_ERRORS,
        "codebase": MemoryManager.NS_CODEBASE,
        "spec": MemoryManager.NS_SPEC,
    }

    print(f"\n{'='*60}")
    print("MEMORY STORE STATISTICS")
    print(f"{'='*60}")
    print(f"\nDatabase: {db_path}")
    if db_path.exists():
        size_bytes = db_path.stat().st_size
        size_mb = size_bytes / (1024 * 1024)
        print(f"Database size: {size_mb:.2f} MB ({size_bytes} bytes)")

    print(f"\nConfiguration:")
    print(f"  Enabled: {config.memory.enabled}")
    print(
        f"  Embedding provider: {config.memory.embedding_provider or 'None (exact key matching)'}"
    )
    print(f"  Max patterns per query: {config.memory.max_patterns_per_query}")
    print(f"  Max pattern length: {config.memory.max_pattern_length}")

    # Filter by namespace if specified
    if args.namespace:
        if args.namespace not in namespace_map:
            print(
                f"Error: Invalid namespace '{args.namespace}'. Must be one of: {', '.join(namespace_map.keys())}"
            )
            sys.exit(1)
        namespaces_to_query = {args.namespace: namespace_map[args.namespace]}
    else:
        namespaces_to_query = namespace_map

    print(f"\nPatterns by namespace:")
    total_patterns = 0
    for name, ns in namespaces_to_query.items():
        try:
            # List all patterns in this namespace
            patterns = memory_manager.search_patterns(
                query="", namespace=ns, limit=10000
            )
            count = len(patterns)
            total_patterns += count
            print(f"  {name}: {count} patterns")

            # Show breakdown by task_type
            if count > 0:
                task_types = {}
                for p in patterns:
                    task_types[p.task_type] = task_types.get(p.task_type, 0) + 1
                print(
                    f"     Types: {', '.join(f'{k}({v})' for k, v in sorted(task_types.items())[:5])}"
                )
                if len(task_types) > 5:
                    print(f"            ... and {len(task_types) - 5} more types")
        except Exception as e:
            print(f"  {name}: Error - {e}")

    print(f"\nTotal patterns: {total_patterns}")
    print()


def cmd_memory_clear(args):
    """Clear patterns from memory store."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

    if not config.memory.enabled:
        print("Memory store is disabled in configuration.")
        sys.exit(1)

    db_path = config.memory_store_path
    if not db_path.exists():
        print("No memory store database found.")
        return

    # Map namespace names to tuples
    namespace_map = {
        "implementation": MemoryManager.NS_IMPLEMENTATION,
        "errors": MemoryManager.NS_ERRORS,
        "codebase": MemoryManager.NS_CODEBASE,
        "spec": MemoryManager.NS_SPEC,
    }

    namespace = None
    if args.namespace:
        if args.namespace not in namespace_map:
            print(
                f"Error: Invalid namespace '{args.namespace}'. Must be one of: {', '.join(namespace_map.keys())}"
            )
            sys.exit(1)
        namespace = namespace_map[args.namespace]

    # Require confirmation
    if not args.confirm:
        if namespace:
            print(f"This will delete all patterns from namespace '{args.namespace}'.")
        else:
            print("This will delete ALL patterns from the memory store.")
        print("Add --confirm to proceed.")
        return

    # Create memory manager
    from langgraph.store.memory import InMemoryStore

    try:
        store = InMemoryStore()
        memory_manager = MemoryManager(store, config.memory)
    except Exception as e:
        print(f"Error initializing memory manager: {e}")
        sys.exit(1)

    # Delete patterns
    try:
        if namespace:
            # Delete from specific namespace
            patterns = memory_manager.search_patterns(
                query="", namespace=namespace, limit=10000
            )
            count = len(patterns)

            for pattern in patterns:
                try:
                    store.delete(namespace=namespace, key=pattern.id)
                except Exception as e:
                    print(f"Warning: Failed to delete pattern {pattern.id}: {e}")

            print(f"Deleted {count} patterns from namespace '{args.namespace}'")
        else:
            # Delete from all namespaces
            total_deleted = 0
            for name, ns in namespace_map.items():
                patterns = memory_manager.search_patterns(
                    query="", namespace=ns, limit=10000
                )
                for pattern in patterns:
                    try:
                        store.delete(namespace=ns, key=pattern.id)
                        total_deleted += 1
                    except Exception as e:
                        print(f"Warning: Failed to delete pattern {pattern.id}: {e}")
                print(f"  Deleted {len(patterns)} patterns from {name}")

            print(f"\nTotal deleted: {total_deleted} patterns")

    except Exception as e:
        print(f"Error clearing patterns: {e}")
        sys.exit(1)


def main():
    parser = argparse.ArgumentParser(
        prog="sharpy-auto-builder",
        description="Automated implementation of Sharpy compiler tasks",
    )
    parser.add_argument(
        "--project-root",
        help="Path to Sharpy project root",
        default=None,
    )

    subparsers = parser.add_subparsers(dest="command", help="Commands")

    # init command
    init_parser = subparsers.add_parser("init", help="Initialize the auto builder")
    init_parser.add_argument(
        "--config",
        "-c",
        help="Path to config.json file to load settings from",
        default=None,
    )
    init_parser.add_argument(
        "--task-list",
        "-t",
        help="Path to task list markdown file (required if not in config)",
        default=None,
    )

    # status command
    status_parser = subparsers.add_parser("status", help="Show current status")

    # run command
    run_parser = subparsers.add_parser("run", help="Run the auto builder")
    run_parser.add_argument(
        "--max-tasks",
        type=int,
        help="Maximum number of tasks to process",
        default=None,
    )
    run_parser.add_argument(
        "--backend",
        choices=["claude_code", "copilot"],
        help="Use only this backend",
        default=None,
    )
    run_parser.add_argument(
        "--model",
        help="Model to use (default: claude-sonnet-4-5-20250929)",
        default=None,
    )
    run_parser.add_argument(
        "--skip-spec-check",
        action="store_true",
        help="Skip spec adherence validation",
    )
    run_parser.add_argument(
        "--skip-verification",
        action="store_true",
        help="Skip verification validation",
    )
    run_parser.add_argument(
        "--skip-hallucination-check",
        action="store_true",
        help="Skip hallucination defense check",
    )
    run_parser.add_argument(
        "--no-human-approval",
        action="store_true",
        help="Don't require human approval for critical tasks",
    )
    run_parser.add_argument(
        "--thread-id",
        help="Thread ID to resume a previous session",
        default=None,
    )
    run_parser.add_argument(
        "--list-sessions",
        action="store_true",
        help="List saved sessions instead of running",
    )

    # report command
    report_parser = subparsers.add_parser("report", help="Generate status report")
    report_parser.add_argument(
        "--output",
        "-o",
        help="Output file path",
        default=None,
    )

    # answer command
    answer_parser = subparsers.add_parser(
        "answer", help="Submit an answer to a question"
    )
    answer_parser.add_argument("question_id", help="ID of the question to answer")
    answer_parser.add_argument("answer", help="The answer")
    answer_parser.add_argument("--notes", help="Additional notes", default=None)

    # review command
    review_parser = subparsers.add_parser("review", help="Submit a review response")
    review_parser.add_argument("review_id", help="ID of the review request")
    review_parser.add_argument(
        "status",
        choices=["approved", "rejected", "needs_changes"],
        help="Review status",
    )
    review_parser.add_argument("--notes", help="Review notes", default=None)

    # reset command
    reset_parser = subparsers.add_parser("reset", help="Reset a task to pending")
    reset_parser.add_argument(
        "task_id", help="ID of the task to reset (end of range if --from is specified)"
    )
    reset_parser.add_argument(
        "--from",
        dest="from_task",
        help="Reset all tasks from this ID to task_id (inclusive)",
        default=None,
    )

    # skip command
    skip_parser = subparsers.add_parser("skip", help="Skip a task")
    skip_parser.add_argument("task_id", help="ID of the task to skip")
    skip_parser.add_argument("--reason", help="Reason for skipping", default=None)

    # logs command
    logs_parser = subparsers.add_parser("logs", help="View execution logs")
    logs_parser.add_argument("--task-id", help="Filter by task ID", default=None)
    logs_parser.add_argument(
        "--event-type",
        help="Filter by event type (agent_prompt, agent_response, test_run)",
        default=None,
    )
    logs_parser.add_argument(
        "--last", type=int, help="Show only last N entries", default=None
    )
    logs_parser.add_argument(
        "--show-prompt", action="store_true", help="Include full prompts in output"
    )
    logs_parser.add_argument(
        "--truncate",
        type=int,
        help="Truncate output/prompt to N characters (default: no truncation)",
        default=None,
    )

    # checkpoint-stats command
    checkpoint_stats_parser = subparsers.add_parser(
        "checkpoint-stats", help="Show checkpoint storage statistics"
    )

    # checkpoint-cleanup command
    checkpoint_cleanup_parser = subparsers.add_parser(
        "checkpoint-cleanup", help="Clean up old checkpoints"
    )
    checkpoint_cleanup_parser.add_argument(
        "--thread-id",
        help="Clean up only this thread's checkpoints",
        default=None,
    )
    checkpoint_cleanup_parser.add_argument(
        "--keep",
        type=int,
        help="Number of checkpoints to keep per thread (default: 10)",
        default=None,
    )
    checkpoint_cleanup_parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show what would be deleted without actually deleting",
    )

    # memory command group
    memory_parser = subparsers.add_parser(
        "memory", help="Memory store commands for pattern management"
    )
    memory_subparsers = memory_parser.add_subparsers(
        dest="memory_command", help="Memory commands"
    )

    # memory search command
    memory_search_parser = memory_subparsers.add_parser(
        "search", help="Search for patterns in memory store"
    )
    memory_search_parser.add_argument("query", help="Search query string")
    memory_search_parser.add_argument(
        "--namespace",
        "-n",
        choices=["implementation", "errors", "codebase", "spec"],
        help="Limit search to specific namespace (default: search all)",
        default=None,
    )
    memory_search_parser.add_argument(
        "--limit",
        "-l",
        type=int,
        help="Maximum number of results to return (default: 10)",
        default=10,
    )

    # memory stats command
    memory_stats_parser = memory_subparsers.add_parser(
        "stats", help="Show memory store statistics"
    )
    memory_stats_parser.add_argument(
        "--namespace",
        "-n",
        choices=["implementation", "errors", "codebase", "spec"],
        help="Show stats for specific namespace only",
        default=None,
    )

    # memory clear command
    memory_clear_parser = memory_subparsers.add_parser(
        "clear", help="Clear patterns from memory store"
    )
    memory_clear_parser.add_argument(
        "--namespace",
        "-n",
        choices=["implementation", "errors", "codebase", "spec"],
        help="Clear only this namespace (default: clear all)",
        default=None,
    )
    memory_clear_parser.add_argument(
        "--confirm",
        action="store_true",
        help="Confirm deletion (required to actually delete)",
    )

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        sys.exit(1)

    # Handle memory subcommands
    if args.command == "memory":
        if not hasattr(args, "memory_command") or args.memory_command is None:
            memory_parser.print_help()
            sys.exit(1)

        memory_commands = {
            "search": cmd_memory_search,
            "stats": cmd_memory_stats,
            "clear": cmd_memory_clear,
        }
        memory_commands[args.memory_command](args)
        return

    commands = {
        "init": cmd_init,
        "status": cmd_status,
        "run": cmd_run,
        "report": cmd_report,
        "answer": cmd_answer,
        "review": cmd_review,
        "reset": cmd_reset,
        "skip": cmd_skip,
        "logs": cmd_logs,
        "checkpoint-stats": cmd_checkpoint_stats,
        "checkpoint-cleanup": cmd_checkpoint_cleanup,
    }

    commands[args.command](args)


if __name__ == "__main__":
    main()
