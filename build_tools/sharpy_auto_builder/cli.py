"""
Command-line interface for Sharpy Auto Builder.
"""

import asyncio
import argparse
import json
import sys
from pathlib import Path
from datetime import datetime

from .config import Config
from .state import GroundTruth, parse_task_list
from .orchestrator import Orchestrator
from .human_loop import HumanLoopManager


def cmd_init(args):
    """Initialize the auto builder state."""
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

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
    config = Config()
    if args.project_root:
        config.project_root = Path(args.project_root)

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

    try:
        result = asyncio.run(orchestrator.run(max_tasks=args.max_tasks))

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
    task = ground_truth.get_task(args.task_id)

    if not task:
        print(f"Error: Task '{args.task_id}' not found")
        sys.exit(1)

    from .state import TaskStatus

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
    reset_parser.add_argument("task_id", help="ID of the task to reset")

    # skip command
    skip_parser = subparsers.add_parser("skip", help="Skip a task")
    skip_parser.add_argument("task_id", help="ID of the task to skip")
    skip_parser.add_argument("--reason", help="Reason for skipping", default=None)

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        sys.exit(1)

    commands = {
        "init": cmd_init,
        "status": cmd_status,
        "run": cmd_run,
        "report": cmd_report,
        "answer": cmd_answer,
        "review": cmd_review,
        "reset": cmd_reset,
        "skip": cmd_skip,
    }

    commands[args.command](args)


if __name__ == "__main__":
    main()
