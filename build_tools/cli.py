#!/usr/bin/env python3
"""
Unified CLI for Sharpy Build Tools.

This provides a single entry point for all AI-powered development tools:
- walkthrough: Generate code documentation
- dogfood: Test the compiler with generated code
- build: Automated task implementation

Usage:
    python -m build_tools <command> [options]
    python -m build_tools --help

Examples:
    # Generate code walkthroughs
    python -m build_tools walkthrough generate --parallel 3

    # Run dogfood iterations
    python -m build_tools dogfood run --iterations 10

    # Initialize auto builder
    python -m build_tools build init --task-list tasks.md

    # Show status across all tools
    python -m build_tools status
"""

import sys
import asyncio
from pathlib import Path

import click


# Import stdlib doc generator (stdlib-only deps, always available)
try:
    from build_tools.generate_stdlib_docs import generate as stdlib_generate
except ImportError:
    from .generate_stdlib_docs import generate as stdlib_generate

# Import heavier tool-specific CLI functions (optional deps like langgraph)
_heavy_imports_available = True
try:
    try:
        from build_tools.generate_code_walkthroughs import (
            Config as WalkthroughConfig,
            main_async as walkthrough_main,
        )
        from build_tools.sharpy_dogfood.cli import (
            main as dogfood_main_async,
            Config as DogfoodConfig,
        )
        from build_tools.sharpy_auto_builder.cli import (
            cmd_init as builder_init,
            cmd_status as builder_status,
            cmd_run as builder_run,
            cmd_report as builder_report,
            cmd_answer as builder_answer,
            cmd_review as builder_review,
            cmd_reset as builder_reset,
            cmd_skip as builder_skip,
            cmd_logs as builder_logs,
        )
        from build_tools.sharpy_auto_builder.config import Config as BuilderConfig
    except ImportError:
        from .generate_code_walkthroughs import (
            Config as WalkthroughConfig,
            main_async as walkthrough_main,
        )
        from .sharpy_dogfood.cli import (
            main as dogfood_main_async,
            Config as DogfoodConfig,
        )
        from .sharpy_auto_builder.cli import (
            cmd_init as builder_init,
            cmd_status as builder_status,
            cmd_run as builder_run,
            cmd_report as builder_report,
            cmd_answer as builder_answer,
            cmd_review as builder_review,
            cmd_reset as builder_reset,
            cmd_skip as builder_skip,
            cmd_logs as builder_logs,
        )
        from .sharpy_auto_builder.config import Config as BuilderConfig
except ImportError as e:
    _heavy_imports_available = False
    print(f"Warning: some build tools unavailable ({e})", file=sys.stderr)
    print("The 'stdlib' command is still available.", file=sys.stderr)


@click.group()
@click.version_option(version="1.0.0", prog_name="build_tools")
def cli():
    """Sharpy Build Tools - AI-powered development utilities."""
    pass


# ==============================================================================
# Walkthrough Commands
# ==============================================================================


@cli.group()
def walkthrough():
    """Generate code walkthrough documentation."""
    pass


@walkthrough.command(name="generate")
@click.option(
    "--parallel",
    "-p",
    default=3,
    type=int,
    help="Number of parallel instances (default: 3)",
)
@click.option(
    "--timeout",
    "-t",
    default=2,
    type=int,
    help="Timeout between batches in seconds (default: 2)",
)
@click.option(
    "--copilot-timeout",
    default=300,
    type=int,
    help="Timeout for Copilot commands in seconds (default: 300)",
)
@click.option(
    "--source-dirs",
    "-s",
    multiple=True,
    default=["src/Sharpy.Compiler", "src/Sharpy.Cli"],
    help="Source directories to analyze (default: Compiler and CLI)",
)
@click.option(
    "--output-dir",
    "-o",
    type=click.Path(path_type=Path),
    default="docs/implementation_walkthrough",
    help="Output directory for documentation (default: docs/implementation_walkthrough)",
)
@click.option(
    "--cli",
    type=click.Choice(["auto", "copilot", "claude"], case_sensitive=False),
    default="auto",
    help="CLI provider: auto (Claude→Copilot), copilot, or claude (default: auto)",
)
@click.option(
    "--force",
    "-f",
    is_flag=True,
    help="Force regeneration of all documentation, even if up-to-date",
)
def walkthrough_generate(
    parallel: int,
    timeout: int,
    copilot_timeout: int,
    source_dirs: tuple,
    output_dir: Path,
    cli: str,
    force: bool,
):
    """Generate walkthrough documentation for source files."""
    # Validate arguments
    if parallel < 1:
        click.echo("Error: --parallel must be >= 1", err=True)
        sys.exit(1)

    if timeout < 0:
        click.echo("Error: --timeout must be >= 0", err=True)
        sys.exit(1)

    # Create config
    config = WalkthroughConfig(
        parallel_instances=parallel,
        timeout_between_batches=timeout,
        copilot_timeout=copilot_timeout,
        source_dirs=list(source_dirs),
        output_dir=output_dir,
        cli_provider=cli,
        force_regenerate=force,
    )

    # Run async main
    try:
        asyncio.run(walkthrough_main(config))
    except KeyboardInterrupt:
        click.echo("\n\nInterrupted by user", err=True)
        sys.exit(1)


# ==============================================================================
# Dogfood Commands
# ==============================================================================


@cli.group()
def dogfood():
    """Dogfood the Sharpy compiler with generated code."""
    pass


@dogfood.command(name="run")
@click.option(
    "--iterations",
    "-n",
    default=10,
    type=int,
    help="Number of iterations to run (default: 10)",
)
@click.option(
    "--output-dir",
    "-o",
    type=click.Path(path_type=Path),
    help="Output directory for results and issues (default: dogfood_output)",
)
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to the Sharpy project root (default: auto-detected)",
)
@click.option(
    "--generation-timeout",
    default=180.0,
    type=float,
    help="Timeout for code generation in seconds (default: 180)",
)
@click.option(
    "--compilation-timeout",
    default=60.0,
    type=float,
    help="Timeout for compilation in seconds (default: 60)",
)
@click.option(
    "--execution-timeout",
    default=30.0,
    type=float,
    help="Timeout for execution in seconds (default: 30)",
)
@click.option(
    "--verbose",
    "-v",
    is_flag=True,
    help="Enable verbose output",
)
@click.option(
    "--dry-run",
    is_flag=True,
    help="Check configuration without running iterations",
)
def dogfood_run(
    iterations: int,
    output_dir: Path | None,
    project_root: Path | None,
    generation_timeout: float,
    compilation_timeout: float,
    execution_timeout: float,
    verbose: bool,
    dry_run: bool,
):
    """Run dogfood iterations to test the compiler."""
    # Build configuration
    config = DogfoodConfig()

    if project_root:
        config.project_root = project_root.resolve()

    if output_dir:
        config.output_dir = output_dir
        config.issues_dir = output_dir / "issues"
        config.generated_dir = output_dir / "generated"

    config.max_iterations = iterations
    config.generation_timeout = generation_timeout
    config.compilation_timeout = compilation_timeout
    config.execution_timeout = execution_timeout

    # Ensure directories exist
    config.ensure_dirs()

    # Print configuration
    click.echo("Sharpy Dogfooding Tool", err=True)
    click.echo("=" * 40, err=True)
    click.echo(f"Project root: {config.project_root}", err=True)
    click.echo(f"Output dir: {config.output_dir}", err=True)
    click.echo(f"Issues dir: {config.issues_dir}", err=True)
    click.echo(f"Iterations: {config.max_iterations}", err=True)
    click.echo(
        f"Timeouts: gen={config.generation_timeout}s, compile={config.compilation_timeout}s, exec={config.execution_timeout}s",
        err=True,
    )
    click.echo("=" * 40, err=True)

    # Create a mock args object for compatibility with existing code
    import argparse

    args = argparse.Namespace()
    args.iterations = iterations
    args.output_dir = output_dir
    args.project_root = project_root
    args.generation_timeout = generation_timeout
    args.compilation_timeout = compilation_timeout
    args.execution_timeout = execution_timeout
    args.verbose = verbose
    args.dry_run = dry_run

    # Run async main
    try:
        exit_code = asyncio.run(dogfood_main_async(args))
        sys.exit(exit_code)
    except KeyboardInterrupt:
        click.echo("\nInterrupted by user", err=True)
        sys.exit(130)
    except Exception as e:
        click.echo(f"Fatal error: {e}", err=True)
        sys.exit(1)


# ==============================================================================
# Auto Builder Commands
# ==============================================================================


@cli.group(name="build")
def build():
    """Automated task implementation tool."""
    pass


@build.command(name="init")
@click.option(
    "--task-list",
    "-t",
    type=click.Path(exists=True, path_type=Path),
    required=True,
    help="Path to task list file",
)
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
@click.option(
    "--config",
    "-c",
    type=click.Path(exists=True, path_type=Path),
    help="Path to config file",
)
def build_init(task_list: Path, project_root: Path | None, config: Path | None):
    """Initialize the auto builder with a task list."""

    # Create args namespace for compatibility
    import argparse

    args = argparse.Namespace()
    args.task_list = task_list
    args.project_root = project_root
    args.config = config

    try:
        builder_init(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


@build.command(name="status")
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
def build_status(project_root: Path | None):
    """Show current auto builder status."""

    import argparse

    args = argparse.Namespace()
    args.project_root = project_root

    try:
        builder_status(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


@build.command(name="run")
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
@click.option(
    "--max-tasks",
    "-n",
    default=5,
    type=int,
    help="Maximum number of tasks to process (default: 5)",
)
@click.option(
    "--skip-tests",
    is_flag=True,
    help="Skip running tests after each task",
)
@click.option(
    "--auto-decide",
    is_flag=True,
    help="Automatically decide on ambiguous questions",
)
@click.option(
    "--agents",
    multiple=True,
    help="Enable specific agents (planner, implementer, etc.)",
)
@click.option(
    "--backend",
    type=click.Choice(["claude", "copilot"]),
    help="Use specific backend only",
)
@click.option(
    "--model",
    help="Override model for all backends",
)
@click.option(
    "--skip-spec-check",
    is_flag=True,
    help="Skip spec adherence check",
)
@click.option(
    "--skip-verification",
    is_flag=True,
    help="Skip verification after implementation",
)
@click.option(
    "--skip-hallucination-check",
    is_flag=True,
    help="Skip hallucination defense check",
)
@click.option(
    "--no-human-approval",
    is_flag=True,
    help="Don't require human approval for critical tasks",
)
@click.option(
    "--thread-id",
    help="Thread ID to resume a previous session",
)
@click.option(
    "--list-sessions",
    is_flag=True,
    help="List saved sessions instead of running",
)
def build_run(
    project_root: Path | None,
    max_tasks: int,
    skip_tests: bool,
    auto_decide: bool,
    agents: tuple,
    backend: str | None,
    model: str | None,
    skip_spec_check: bool,
    skip_verification: bool,
    skip_hallucination_check: bool,
    no_human_approval: bool,
    thread_id: str | None,
    list_sessions: bool,
):
    """Run auto builder to process tasks."""

    import argparse

    args = argparse.Namespace()
    args.project_root = project_root
    args.max_tasks = max_tasks
    args.skip_tests = skip_tests
    args.auto_decide = auto_decide
    args.agents = list(agents) if agents else None
    args.backend = backend
    args.model = model
    args.skip_spec_check = skip_spec_check
    args.skip_verification = skip_verification
    args.skip_hallucination_check = skip_hallucination_check
    args.no_human_approval = no_human_approval
    args.thread_id = thread_id
    args.list_sessions = list_sessions

    try:
        builder_run(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


@build.command(name="report")
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
@click.option(
    "--output",
    "-o",
    type=click.Path(path_type=Path),
    help="Output file for report (default: print to stdout)",
)
def build_report(project_root: Path | None, output: Path | None):
    """Generate a detailed report of progress and results."""

    import argparse

    args = argparse.Namespace()
    args.project_root = project_root
    args.output = output

    try:
        builder_report(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


@build.command(name="answer")
@click.argument("question_id")
@click.argument("answer")
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
def build_answer(question_id: str, answer: str, project_root: Path | None):
    """Answer a pending question from the auto builder."""

    import argparse

    args = argparse.Namespace()
    args.question_id = question_id
    args.answer = answer
    args.project_root = project_root

    try:
        builder_answer(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


@build.command(name="review")
@click.argument("task_id")
@click.argument("decision", type=click.Choice(["approve", "reject"]))
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
@click.option(
    "--feedback",
    "-f",
    help="Feedback for the review",
)
def build_review(
    task_id: str, decision: str, project_root: Path | None, feedback: str | None
):
    """Review and approve/reject a task implementation."""

    import argparse

    args = argparse.Namespace()
    args.task_id = task_id
    args.decision = decision
    args.project_root = project_root
    args.feedback = feedback

    try:
        builder_review(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


@build.command(name="reset")
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
@click.option(
    "--confirm",
    is_flag=True,
    help="Confirm reset without prompting",
)
def build_reset(project_root: Path | None, confirm: bool):
    """Reset the auto builder state."""

    import argparse

    args = argparse.Namespace()
    args.project_root = project_root
    args.confirm = confirm

    try:
        builder_reset(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


@build.command(name="skip")
@click.argument("task_id")
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
@click.option(
    "--reason",
    "-r",
    help="Reason for skipping the task",
)
def build_skip(task_id: str, project_root: Path | None, reason: str | None):
    """Skip a task in the auto builder."""

    import argparse

    args = argparse.Namespace()
    args.task_id = task_id
    args.reason = reason
    args.project_root = project_root

    try:
        builder_skip(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


@build.command(name="logs")
@click.option(
    "--project-root",
    type=click.Path(path_type=Path),
    help="Path to project root (default: current directory)",
)
@click.option(
    "--task-id",
    help="Filter by task ID",
)
@click.option(
    "--event-type",
    help="Filter by event type (agent_prompt, agent_response, test_run)",
)
@click.option(
    "--last",
    "-n",
    type=int,
    help="Show only last N entries",
)
@click.option(
    "--show-prompt",
    is_flag=True,
    help="Include full prompts in output",
)
@click.option(
    "--truncate",
    type=int,
    help="Truncate output/prompt to N characters",
)
def build_logs(
    project_root: Path | None,
    task_id: str | None,
    event_type: str | None,
    last: int | None,
    show_prompt: bool,
    truncate: int | None,
):
    """View execution logs from the auto builder."""

    import argparse

    args = argparse.Namespace()
    args.project_root = project_root
    args.task_id = task_id
    args.event_type = event_type
    args.last = last
    args.show_prompt = show_prompt
    args.truncate = truncate

    try:
        builder_logs(args)
    except SystemExit:
        raise
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


# ==============================================================================
# Stdlib Documentation Commands
# ==============================================================================


@cli.group()
def stdlib():
    """Generate standard library API documentation."""
    pass


@stdlib.command(name="generate")
@click.option(
    "--source-dir",
    "-s",
    type=click.Path(exists=True, path_type=Path),
    default="src/Sharpy.Core",
    help="Path to Sharpy.Core source directory (default: src/Sharpy.Core)",
)
@click.option(
    "--output-dir",
    "-o",
    type=click.Path(path_type=Path),
    default="docs/stdlib",
    help="Output directory for generated docs (default: docs/stdlib)",
)
@click.option(
    "--stdlib-dir",
    type=click.Path(exists=True, path_type=Path),
    default=None,
    help="Path to Sharpy.Stdlib source directory (default: auto-detected from source-dir sibling)",
)
@click.option(
    "--force",
    "-f",
    is_flag=True,
    help="Overwrite existing files (default: skip existing)",
)
@click.option(
    "--verbose",
    "-v",
    is_flag=True,
    help="Print progress information",
)
def stdlib_gen(source_dir: Path, output_dir: Path, stdlib_dir: Path | None, force: bool, verbose: bool):
    """Generate stdlib API reference pages from Sharpy.Core and Sharpy.Stdlib C# source."""
    if stdlib_dir is None:
        auto = source_dir.resolve().parent / "Sharpy.Stdlib"
        if auto.exists():
            stdlib_dir = auto
    try:
        generated = stdlib_generate(
            source_dir=source_dir.resolve(),
            output_dir=output_dir.resolve(),
            force=force,
            verbose=verbose,
            stdlib_dir=stdlib_dir.resolve() if stdlib_dir else None,
        )
        if not verbose:
            click.echo(f"Generated {len(generated)} files in {output_dir}")
    except Exception as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


# ==============================================================================
# Global Status Command
# ==============================================================================


@cli.command()
def status():
    """Show status of all build tools."""
    click.echo("\n" + "=" * 60)
    click.echo("SHARPY BUILD TOOLS - STATUS")
    click.echo("=" * 60 + "\n")

    # Check walkthrough generator
    click.echo("📚 Code Walkthrough Generator:")
    walkthrough_dir = Path("docs/implementation_walkthrough")
    if walkthrough_dir.exists():
        doc_count = len(list(walkthrough_dir.glob("*.md")))
        click.echo(f"   ✓ Output directory exists: {walkthrough_dir}")
        click.echo(f"   ✓ Documentation files: {doc_count}")
    else:
        click.echo(f"   ⚠ Output directory not found: {walkthrough_dir}")
    click.echo()

    # Check dogfood tool
    click.echo("🐕 Dogfood Tool:")
    dogfood_dir = Path("dogfood_output")
    if dogfood_dir.exists():
        generated_dir = dogfood_dir / "generated"
        issues_dir = dogfood_dir / "issues"
        generated_count = (
            len(list(generated_dir.glob("*.spy"))) if generated_dir.exists() else 0
        )
        issue_count = len(list(issues_dir.glob("*.json"))) if issues_dir.exists() else 0
        click.echo(f"   ✓ Output directory exists: {dogfood_dir}")
        click.echo(f"   ✓ Generated files: {generated_count}")
        click.echo(f"   ✓ Issue reports: {issue_count}")
    else:
        click.echo(f"   ⚠ Output directory not found: {dogfood_dir}")
    click.echo()

    # Check auto builder
    click.echo("🔨 Auto Builder:")
    builder_config = BuilderConfig()
    if builder_config.ground_truth_path.exists():
        from .sharpy_auto_builder.state import GroundTruth

        ground_truth = GroundTruth.load(builder_config.ground_truth_path)
        click.echo(f"   ✓ Initialized with ground truth")
        click.echo(f"   ✓ Overall progress: {ground_truth.overall_progress:.1f}%")
        click.echo(f"   ✓ Total attempts: {ground_truth.total_attempts}")
        click.echo(f"   ✓ Successes: {ground_truth.total_successes}")
        click.echo(f"   ✓ Failures: {ground_truth.total_failures}")
        if ground_truth.current_task_id:
            click.echo(f"   → Current task: {ground_truth.current_task_id}")
    else:
        click.echo(f"   ⚠ Not initialized (run 'build_tools build init' first)")
    click.echo()

    # Check shared modules
    click.echo("🔧 Shared Modules:")
    try:
        from .shared import backends, rate_limiting, model_selector, config, logging

        click.echo("   ✓ Backends module available")
        click.echo("   ✓ Rate limiting module available")
        click.echo("   ✓ Model selector module available")
        click.echo("   ✓ Configuration module available")
        click.echo("   ✓ Logging module available")
    except ImportError as e:
        click.echo(f"   ✗ Error importing shared modules: {e}")

    click.echo("\n" + "=" * 60 + "\n")


def main():
    """Main entry point."""
    cli()


if __name__ == "__main__":
    main()
