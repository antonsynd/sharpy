#!/usr/bin/env python3
"""
CLI for the Sharpy Dogfooding Tool.

Usage:
    python -m sharpy_dogfood [run] [options]     # Run dogfood iterations
    python -m sharpy_dogfood convert [options]   # Convert outputs to integration tests
"""

import argparse
import asyncio
import sys
from pathlib import Path

from .config import Config
from .orchestrator import DogfoodOrchestrator
from .convert import convert_dogfood_to_test, convert_all_dogfood_outputs


def create_parser() -> argparse.ArgumentParser:
    """Create the argument parser with subcommands."""
    parser = argparse.ArgumentParser(
        description="Sharpy Dogfooding Tool - Generate, validate, compile and verify Sharpy code",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
    # Run 10 iterations (default)
    python -m sharpy_dogfood

    # Run 5 iterations
    python -m sharpy_dogfood run --iterations 5

    # Convert all successful outputs to integration tests
    python -m sharpy_dogfood convert --all

    # Convert a specific dogfood output
    python -m sharpy_dogfood convert 20260117_001155_success_simple_class_0001

    # Convert with custom test name and category
    python -m sharpy_dogfood convert <dir> --name my_test --category classes
""",
    )

    subparsers = parser.add_subparsers(dest="command", help="Available commands")

    # Run subcommand (also the default)
    run_parser = subparsers.add_parser("run", help="Run dogfood iterations")
    _add_run_arguments(run_parser)

    # Convert subcommand
    convert_parser = subparsers.add_parser(
        "convert", help="Convert dogfood outputs to integration tests"
    )
    _add_convert_arguments(convert_parser)

    # Add run arguments to main parser for backward compatibility (no subcommand)
    _add_run_arguments(parser)

    return parser


def _add_run_arguments(parser: argparse.ArgumentParser) -> None:
    """Add arguments for the run command."""
    parser.add_argument(
        "-n",
        "--iterations",
        type=int,
        default=10,
        help="Number of iterations to run (default: 10)",
    )

    parser.add_argument(
        "-o",
        "--output-dir",
        type=Path,
        help="Output directory for results and issues (default: dogfood_output)",
    )

    parser.add_argument(
        "--project-root",
        type=Path,
        help="Path to the Sharpy project root (default: auto-detected)",
    )

    parser.add_argument(
        "--generation-timeout",
        type=float,
        default=180.0,
        help="Timeout for code generation in seconds (default: 180)",
    )

    parser.add_argument(
        "--compilation-timeout",
        type=float,
        default=60.0,
        help="Timeout for compilation in seconds (default: 60)",
    )

    parser.add_argument(
        "--execution-timeout",
        type=float,
        default=30.0,
        help="Timeout for execution in seconds (default: 30)",
    )

    parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Enable verbose output",
    )

    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Check configuration without running iterations",
    )

    parser.add_argument(
        "--auto-convert",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="Auto-convert unique successful tests to integration test fixtures (default: enabled)",
    )

    parser.add_argument(
        "--backend",
        type=str,
        choices=["claude", "copilot"],
        default=None,
        help="AI backend to use (default: claude with copilot failover)",
    )


def _add_convert_arguments(parser: argparse.ArgumentParser) -> None:
    """Add arguments for the convert command."""
    parser.add_argument(
        "directory",
        nargs="?",
        help="Specific dogfood output directory to convert (e.g., 20260117_success_...)",
    )

    parser.add_argument(
        "--all",
        "-a",
        action="store_true",
        dest="convert_all",
        help="Convert all dogfood outputs",
    )

    parser.add_argument(
        "--include-failures",
        action="store_true",
        help="Also convert failed compilations as error tests",
    )

    parser.add_argument(
        "--name",
        type=str,
        help="Override test name (without extension)",
    )

    parser.add_argument(
        "--category",
        type=str,
        help="Override test category (subfolder in TestFixtures)",
    )

    parser.add_argument(
        "--force",
        "-f",
        action="store_true",
        help="Overwrite existing test files",
    )

    parser.add_argument(
        "--dogfood-dir",
        type=Path,
        help="Path to dogfood_output directory (default: auto-detected)",
    )

    parser.add_argument(
        "--test-fixtures-dir",
        type=Path,
        help="Path to TestFixtures directory (default: auto-detected)",
    )

    parser.add_argument(
        "--project-root",
        type=Path,
        help="Path to the Sharpy project root (default: auto-detected)",
    )


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = create_parser()
    args = parser.parse_args()

    # Handle case where no subcommand is given (backward compatibility)
    if args.command is None:
        # Check if any run-specific args were given
        if hasattr(args, "iterations"):
            args.command = "run"
        else:
            # Show help if no command and no args
            parser.print_help()
            sys.exit(0)

    return args


async def run_dogfood(args: argparse.Namespace) -> int:
    """Run the dogfooding process."""
    # Build configuration
    config = Config()

    if hasattr(args, "project_root") and args.project_root:
        config.project_root = args.project_root.resolve()

    if hasattr(args, "output_dir") and args.output_dir:
        config.output_dir = args.output_dir
        config.issues_dir = args.output_dir / "issues"
        config.successes_dir = args.output_dir / "successes"

    config.max_iterations = args.iterations
    config.generation_timeout = args.generation_timeout
    config.compilation_timeout = args.compilation_timeout
    config.execution_timeout = args.execution_timeout

    backend_choice = getattr(args, "backend", None)
    if backend_choice:
        for name, backend_cfg in config.backends.items():
            backend_cfg.enabled = name == backend_choice

    # Ensure directories exist
    config.ensure_dirs()

    # Print configuration
    print("Sharpy Dogfooding Tool", file=sys.stderr)
    print("=" * 40, file=sys.stderr)
    print(f"Project root: {config.project_root}", file=sys.stderr)
    print(f"Output dir: {config.output_dir}", file=sys.stderr)
    print(f"Issues dir: {config.issues_dir}", file=sys.stderr)
    print(f"Successes dir: {config.successes_dir}", file=sys.stderr)
    print(f"Iterations: {config.max_iterations}", file=sys.stderr)
    print(
        f"Timeouts: gen={config.generation_timeout}s, compile={config.compilation_timeout}s, exec={config.execution_timeout}s",
        file=sys.stderr,
    )
    print("=" * 40, file=sys.stderr)

    if backend_choice:
        print(f"Backend: {backend_choice}", file=sys.stderr)
    else:
        print("Backend: claude (with copilot failover)", file=sys.stderr)
    auto_convert = getattr(args, "auto_convert", True)
    print(f"Auto-convert: {'enabled' if auto_convert else 'disabled'}", file=sys.stderr)
    print("=" * 40, file=sys.stderr)

    if args.dry_run:
        print("\nDry run - checking configuration...", file=sys.stderr)
        orchestrator = DogfoodOrchestrator(config, auto_convert=auto_convert)
        if await orchestrator.initialize():
            print("✓ Configuration is valid", file=sys.stderr)
            return 0
        else:
            print("✗ Configuration check failed", file=sys.stderr)
            return 1

    # Run the dogfooding process
    orchestrator = DogfoodOrchestrator(config, auto_convert=auto_convert)
    return await orchestrator.run(args.iterations)


def run_convert(args: argparse.Namespace) -> int:
    """Convert dogfood outputs to integration tests."""
    # Determine paths
    config = Config()
    if hasattr(args, "project_root") and args.project_root:
        config.project_root = args.project_root.resolve()

    dogfood_dir = (
        args.dogfood_dir if args.dogfood_dir else config.project_root / "dogfood_output"
    )
    test_fixtures_dir = (
        args.test_fixtures_dir
        if args.test_fixtures_dir
        else config.project_root / "src/Sharpy.Compiler.Tests/Integration/TestFixtures"
    )

    # Validate paths
    if not dogfood_dir.exists():
        print(
            f"Error: Dogfood output directory not found: {dogfood_dir}", file=sys.stderr
        )
        return 1

    if not test_fixtures_dir.exists():
        print(f"Creating TestFixtures directory: {test_fixtures_dir}", file=sys.stderr)
        test_fixtures_dir.mkdir(parents=True, exist_ok=True)

    print(f"Dogfood directory: {dogfood_dir}", file=sys.stderr)
    print(f"TestFixtures directory: {test_fixtures_dir}", file=sys.stderr)
    print("=" * 40, file=sys.stderr)

    if args.convert_all:
        # Convert all outputs
        converted, failed = convert_all_dogfood_outputs(
            dogfood_dir,
            test_fixtures_dir,
            include_failures=args.include_failures,
            force=args.force,
        )
        print(f"\nConverted: {converted}, Failed: {failed}", file=sys.stderr)
        return 0 if failed == 0 else 1

    elif args.directory:
        # Convert a specific directory
        # Try to find it in successes or issues
        specific_dir = None
        for subdir in ["successes", "issues"]:
            candidate = dogfood_dir / subdir / args.directory
            if candidate.exists():
                specific_dir = candidate
                break

        # Also try the raw path
        if not specific_dir:
            candidate = Path(args.directory)
            if candidate.exists():
                specific_dir = candidate

        if not specific_dir:
            print(f"Error: Directory not found: {args.directory}", file=sys.stderr)
            print(
                f"Searched in: {dogfood_dir}/successes/, {dogfood_dir}/issues/",
                file=sys.stderr,
            )
            return 1

        result = convert_dogfood_to_test(
            specific_dir,
            test_fixtures_dir,
            category=args.category,
            test_name=args.name,
            force=args.force,
        )
        return 0 if result else 1

    else:
        print(
            "Error: Specify a directory or use --all to convert all outputs",
            file=sys.stderr,
        )
        return 1


async def main(args: argparse.Namespace | None = None) -> int:
    """Main entry point."""
    if args is None:
        args = parse_args()

    if args.command == "convert":
        return run_convert(args)
    else:
        return await run_dogfood(args)


def cli_main() -> None:
    """Entry point for the CLI."""
    try:
        exit_code = asyncio.run(main())
        sys.exit(exit_code)
    except KeyboardInterrupt:
        print("\nInterrupted by user", file=sys.stderr)
        sys.exit(130)
    except Exception as e:
        print(f"Fatal error: {e}", file=sys.stderr)
        import traceback

        traceback.print_exc(file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    cli_main()
