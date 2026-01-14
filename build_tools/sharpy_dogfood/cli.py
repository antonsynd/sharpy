#!/usr/bin/env python3
"""
CLI for the Sharpy Dogfooding Tool.

Usage:
    python -m sharpy_dogfood [options]
    python -m sharpy_dogfood --iterations 5
    python -m sharpy_dogfood --output-dir ./my_output
"""

import argparse
import asyncio
import sys
from pathlib import Path

from .config import Config
from .orchestrator import DogfoodOrchestrator


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(
        description="Sharpy Dogfooding Tool - Generate, validate, compile and verify Sharpy code",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
    # Run 10 iterations (default)
    python -m sharpy_dogfood

    # Run 5 iterations
    python -m sharpy_dogfood --iterations 5

    # Specify output directory
    python -m sharpy_dogfood --output-dir ./dogfood_results

    # Use specific project root
    python -m sharpy_dogfood --project-root /path/to/sharpy
""",
    )

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

    return parser.parse_args()


async def main(args: argparse.Namespace | None = None) -> int:
    """Main entry point."""
    if args is None:
        args = parse_args()

    # Build configuration
    config = Config()

    if args.project_root:
        config.project_root = args.project_root.resolve()

    if args.output_dir:
        config.output_dir = args.output_dir
        config.issues_dir = args.output_dir / "issues"
        config.generated_dir = args.output_dir / "generated"

    config.max_iterations = args.iterations
    config.generation_timeout = args.generation_timeout
    config.compilation_timeout = args.compilation_timeout
    config.execution_timeout = args.execution_timeout

    # Ensure directories exist
    config.ensure_dirs()

    # Print configuration
    print("Sharpy Dogfooding Tool", file=sys.stderr)
    print("=" * 40, file=sys.stderr)
    print(f"Project root: {config.project_root}", file=sys.stderr)
    print(f"Output dir: {config.output_dir}", file=sys.stderr)
    print(f"Issues dir: {config.issues_dir}", file=sys.stderr)
    print(f"Iterations: {config.max_iterations}", file=sys.stderr)
    print(
        f"Timeouts: gen={config.generation_timeout}s, compile={config.compilation_timeout}s, exec={config.execution_timeout}s",
        file=sys.stderr,
    )
    print("=" * 40, file=sys.stderr)

    if args.dry_run:
        print("\nDry run - checking configuration...", file=sys.stderr)
        orchestrator = DogfoodOrchestrator(config)
        if await orchestrator.initialize():
            print("✓ Configuration is valid", file=sys.stderr)
            return 0
        else:
            print("✗ Configuration check failed", file=sys.stderr)
            return 1

    # Run the dogfooding process
    orchestrator = DogfoodOrchestrator(config)
    return await orchestrator.run(args.iterations)


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
        sys.exit(1)


if __name__ == "__main__":
    cli_main()
