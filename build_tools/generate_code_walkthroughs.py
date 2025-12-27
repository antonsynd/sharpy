#!/usr/bin/env python3
"""
Generate internal code walkthrough documentation using AI CLI tools.

This script analyzes C# source files from Sharpy.Cli and Sharpy.Compiler projects,
using multiple parallel instances of an AI CLI tool (GitHub Copilot or Claude Code)
to generate markdown documentation that helps newcomers understand the codebase.

Supported CLI tools:
- GitHub Copilot CLI (copilot): Uses explicit tool permissions
- Claude Code CLI (claude): Uses explicit tool permissions

SECURITY MODEL:
- Read access: AI can read files in the repository (needed to analyze source)
- Write access: AI can create new files (needed to write documentation)
- NO shell/bash access: AI cannot execute arbitrary commands
- NO edit access: AI cannot modify existing files (only create new ones)
- NO delete access: AI cannot remove files

The AI tools are explicitly restricted to 'Read' and 'Write' operations only.
This prevents accidental or malicious file deletion, code modification, or
arbitrary command execution.

Working directory: Script runs from repository root for path resolution.
"""

import argparse
import asyncio
import subprocess
import sys
from pathlib import Path
from typing import List, Set, Optional
from dataclasses import dataclass, field


class RateLimitExceeded(Exception):
    """Exception raised when GitHub Copilot rate limit is exceeded."""

    pass


@dataclass
class Config:
    """Configuration for the walkthrough generator."""

    parallel_instances: int = 3
    timeout_between_batches: int = 60  # seconds
    copilot_timeout: int = 120  # seconds per file
    source_dirs: Optional[List[str]] = None
    output_dir: str = "docs/internal_walkthrough"
    cli_provider: str = "copilot"  # "copilot" or "claude"

    def __post_init__(self):
        if self.source_dirs is None:
            self.source_dirs = ["src/Sharpy.Cli", "src/Sharpy.Compiler"]
        if self.cli_provider not in ("copilot", "claude"):
            raise ValueError(
                f"Invalid CLI provider: {self.cli_provider}. Must be 'copilot' or 'claude'."
            )


def find_cs_files(base_dir: Path, source_dirs: List[str]) -> List[Path]:
    """Find all C# files in the specified source directories."""
    cs_files = []
    for source_dir in source_dirs:
        full_path = base_dir / source_dir
        if not full_path.exists():
            print(f"Warning: Directory {full_path} does not exist", file=sys.stderr)
            continue

        # Find all .cs files recursively
        for cs_file in full_path.rglob("*.cs"):
            # Skip obj and bin directories
            if any(part in ["obj", "bin"] for part in cs_file.parts):
                continue
            cs_files.append(cs_file)

    return sorted(cs_files)


def get_output_path(
    cs_file: Path, base_dir: Path, source_dirs: List[str], output_dir: Path
) -> Path:
    """
    Calculate the output markdown path for a given C# file.

    Preserves the directory structure relative to the source directory root.
    """
    # Find which source directory this file belongs to
    relative_path = cs_file.relative_to(base_dir)

    for source_dir in source_dirs:
        source_path = Path(source_dir)
        try:
            # Try to make path relative to this source directory
            rel_to_source = relative_path.relative_to(source_path)
            # Change extension to .md
            md_filename = rel_to_source.with_suffix(".md")
            return output_dir / source_path / md_filename
        except ValueError:
            # Not relative to this source directory, try next
            continue

    # Fallback: shouldn't happen if find_cs_files is correct
    return output_dir / relative_path.with_suffix(".md")


def find_existing_docs(output_dir: Path) -> Set[Path]:
    """Find all existing markdown documentation files."""
    if not output_dir.exists():
        return set()

    return set(output_dir.rglob("*.md"))


def _build_cli_command(cli_provider: str, prompt: str) -> List[str]:
    """
    Build the command line arguments for the specified CLI provider.

    SECURITY: Only 'Read' and 'Write' tools are allowed.
    - Read: Allows reading source files for analysis
    - Write: Allows creating new markdown documentation files

    NOT allowed (for safety):
    - Bash/shell: No arbitrary command execution
    - Edit: No modification of existing files
    - Delete: No file removal
    - Other tools that could have side effects

    Args:
        cli_provider: Either "copilot" or "claude"
        prompt: The prompt to send to the AI

    Returns:
        List of command arguments
    """
    if cli_provider == "copilot":
        # GitHub Copilot CLI: explicitly allow only read and write tools
        # This prevents shell access, file deletion, or editing existing files
        return [
            "/opt/homebrew/bin/copilot",
            "--prompt",
            prompt,
            "--allow-tool",
            "read",
            "--allow-tool",
            "write",
        ]
    elif cli_provider == "claude":
        # Claude Code CLI: explicitly allow only Read and Write tools
        # Do NOT use --dangerously-skip-permissions as it bypasses all safety checks
        # Only Read and Write are allowed - no Bash, Edit, or other tools
        return [
            "claude",
            "--print",
            "--allowedTools",
            "Read,Write",
            "--prompt",
            prompt,
        ]
    else:
        raise ValueError(f"Unknown CLI provider: {cli_provider}")


async def analyze_file_with_cli(
    cs_file: Path, output_path: Path, base_dir: Path, timeout: int, cli_provider: str
) -> bool:
    """
    Analyze a single C# file using the specified AI CLI tool.

    Args:
        cs_file: Path to the C# source file
        output_path: Path where the markdown documentation should be written
        base_dir: Base directory of the repository
        timeout: Timeout in seconds for the CLI command
        cli_provider: Either "copilot" or "claude"

    Returns:
        True if successful, False otherwise.
    """
    print(f"Analyzing: {cs_file}")

    try:
        # Create output directory if needed
        output_path.parent.mkdir(parents=True, exist_ok=True)

        # Create the prompt that asks the AI to analyze and write the file
        # Use relative paths for better readability
        relative_cs_file = cs_file.relative_to(base_dir)
        relative_output = output_path.relative_to(base_dir)

        prompt = f"""Read the C# source file '{relative_cs_file}' and create a comprehensive walkthrough document for a newcomer engineer joining the Sharpy compiler project.

The walkthrough should:

1. **Overview**: Start with a brief summary of what this file does and its role in the overall project
2. **Class/Type Structure**: Explain the main classes, interfaces, structs, or enums defined in the file
3. **Key Functions/Methods**: Walk through the important methods, explaining:
   - What each method does
   - Key parameters and return values
   - Important implementation details or algorithms
   - How it fits into the broader codebase
4. **Dependencies**: Note important dependencies on other parts of the codebase
5. **Patterns and Design Decisions**: Highlight any notable design patterns, architectural decisions, or coding conventions
6. **Debugging Tips**: Provide insights that would help someone debug issues in this code
7. **Contribution Guidelines**: Suggest what kinds of changes or contributions might be made to this file

Write the walkthrough as a well-structured markdown document to '{relative_output}' with:
- A header showing "# Walkthrough: {cs_file.name}" and "**Source File**: `{relative_cs_file}`" followed by a separator (---)
- Clear headings and subheadings
- Code snippets where helpful (use ```csharp blocks)
- Bullet points for lists
- Emphasis on readability for someone new to the codebase

Focus on providing intuition and understanding, not just restating what the code does line-by-line."""

        # Build the command for the specified CLI provider
        cmd = _build_cli_command(cli_provider, prompt)

        # Call the AI CLI in programmatic mode with write permissions
        # Change to the base directory so relative paths work
        # Using create_subprocess_exec (not shell) so the prompt argument is safely passed
        # without any shell interpretation - no escaping needed
        process = await asyncio.create_subprocess_exec(
            *cmd,
            cwd=str(base_dir),
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
        )

        try:
            stdout, stderr = await asyncio.wait_for(
                process.communicate(), timeout=timeout
            )

            if process.returncode != 0:
                stderr_text = stderr.decode("utf-8")
                stdout_text = stdout.decode("utf-8")

                # Check for rate limiting errors (works for both Copilot and Claude)
                rate_limit_indicators = [
                    "rate_limited",
                    "429",
                    "exceeded your Copilot token usage",
                    "rate limit",
                    "too many requests",
                ]
                combined_output = (stderr_text + stdout_text).lower()
                if any(
                    indicator.lower() in combined_output
                    for indicator in rate_limit_indicators
                ):
                    print(
                        "\n" + "=" * 60,
                        file=sys.stderr,
                    )
                    print(
                        "RATE LIMIT EXCEEDED",
                        file=sys.stderr,
                    )
                    print(
                        "=" * 60,
                        file=sys.stderr,
                    )
                    print(
                        f"Rate limit reached while processing {cs_file}",
                        file=sys.stderr,
                    )
                    print(
                        f"Error details: {stderr_text}",
                        file=sys.stderr,
                    )
                    print(
                        "\nStopping execution. You can restart the script later to resume.",
                        file=sys.stderr,
                    )
                    print(
                        "Already processed files will be skipped automatically.",
                        file=sys.stderr,
                    )
                    print(
                        "=" * 60 + "\n",
                        file=sys.stderr,
                    )
                    raise RateLimitExceeded("Rate limit exceeded")

                print(
                    f"Error analyzing {cs_file}: {stderr_text}",
                    file=sys.stderr,
                )
                return False

            # Check if the output file was created
            if output_path.exists():
                print(f"✓ Generated: {output_path}")
                return True
            else:
                print(
                    f"Warning: Copilot completed but output file not found: {output_path}",
                    file=sys.stderr,
                )
                # Print Copilot's response for debugging
                if stdout:
                    print(
                        f"Copilot output: {stdout.decode('utf-8')[:500]}",
                        file=sys.stderr,
                    )
                return False

        except asyncio.TimeoutError:
            print(f"Timeout analyzing {cs_file}", file=sys.stderr)
            process.kill()
            await process.wait()
            return False

    except Exception as e:
        print(f"Exception analyzing {cs_file}: {e}", file=sys.stderr)
        return False


async def process_batch(
    files_batch: List[tuple], config: Config, base_dir: Path
) -> int:
    """
    Process a batch of files in parallel.

    Returns the number of successfully processed files.
    Raises RateLimitExceeded if rate limit is hit.
    """
    tasks = [
        analyze_file_with_cli(
            cs_file, output_path, base_dir, config.copilot_timeout, config.cli_provider
        )
        for cs_file, output_path in files_batch
    ]

    # Use return_exceptions=False so RateLimitExceeded propagates
    results = await asyncio.gather(*tasks, return_exceptions=True)

    # Check if any task raised RateLimitExceeded
    for result in results:
        if isinstance(result, RateLimitExceeded):
            raise result

    # Count successful results (True values, not exceptions)
    return sum(1 for r in results if r is True)


async def main_async(config: Config):
    """Main async processing function."""
    # Find the repository root (where sharpy.sln is located)
    script_dir = Path(__file__).parent.parent
    base_dir = script_dir

    # Ensure source_dirs is set
    if config.source_dirs is None:
        config.source_dirs = ["src/Sharpy.Cli", "src/Sharpy.Compiler"]

    print(f"Repository root: {base_dir}")
    print(f"CLI provider: {config.cli_provider}")
    print(f"Source directories: {config.source_dirs}")
    print(f"Output directory: {config.output_dir}")
    print(f"Parallel instances: {config.parallel_instances}")
    print(f"Timeout between batches: {config.timeout_between_batches}s")
    print()

    # Find all C# files
    cs_files = find_cs_files(base_dir, config.source_dirs)
    print(f"Found {len(cs_files)} C# files")

    if not cs_files:
        print("No C# files found to process")
        return

    # Calculate output paths and check for existing docs
    output_dir = base_dir / config.output_dir
    existing_docs = find_existing_docs(output_dir)

    # Filter out files that already have documentation
    files_to_process = []
    for cs_file in cs_files:
        output_path = get_output_path(cs_file, base_dir, config.source_dirs, output_dir)
        if output_path in existing_docs:
            print(f"Skipping (already exists): {cs_file}")
        else:
            files_to_process.append((cs_file, output_path))

    print(
        f"\nProcessing {len(files_to_process)} new files (skipped {len(cs_files) - len(files_to_process)} existing)"
    )
    print()

    if not files_to_process:
        print("All files already have documentation!")
        return

    # Process in batches
    total_processed = 0
    total_batches = (
        len(files_to_process) + config.parallel_instances - 1
    ) // config.parallel_instances

    try:
        for batch_num in range(0, len(files_to_process), config.parallel_instances):
            batch = files_to_process[batch_num : batch_num + config.parallel_instances]
            current_batch_num = batch_num // config.parallel_instances + 1

            print(f"\n{'='*60}")
            print(f"Batch {current_batch_num}/{total_batches} ({len(batch)} files)")
            print(f"{'='*60}\n")

            success_count = await process_batch(batch, config, base_dir)
            total_processed += success_count

            print(
                f"\nBatch {current_batch_num} complete: {success_count}/{len(batch)} successful"
            )

            # Wait between batches to avoid rate limiting (except for the last batch)
            if batch_num + config.parallel_instances < len(files_to_process):
                print(
                    f"Waiting {config.timeout_between_batches}s to avoid rate limiting..."
                )
                await asyncio.sleep(config.timeout_between_batches)
    except RateLimitExceeded:
        # Rate limit was hit, stop processing
        print(
            f"\nStopped after processing {total_processed} files due to rate limiting.",
            file=sys.stderr,
        )
        print(
            f"Run the script again later to continue. Already processed files will be skipped.",
            file=sys.stderr,
        )
        sys.exit(1)

    print(f"\n{'='*60}")
    print(
        f"Complete! Processed {total_processed}/{len(files_to_process)} files successfully"
    )
    print(f"Documentation output: {output_dir}")
    print(f"{'='*60}")


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Generate code walkthrough documentation using AI CLI tools.\n\n"
        "Supports GitHub Copilot CLI and Claude Code CLI.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Use default settings with GitHub Copilot (3 parallel instances, 60s timeout)
  %(prog)s

  # Use Claude Code CLI instead of GitHub Copilot
  %(prog)s --cli claude

  # Use 5 parallel instances with 90s timeout between batches
  %(prog)s --parallel 5 --timeout 90

  # Process only Sharpy.Compiler with custom output directory
  %(prog)s --source-dirs src/Sharpy.Compiler --output-dir docs/compiler_walkthrough

  # Increase timeout for CLI to 3 minutes per file
  %(prog)s --copilot-timeout 180

CLI Providers:
  copilot: Uses GitHub Copilot CLI with explicit tool permissions
  claude:  Uses Claude Code CLI with explicit tool permissions

Security Model:
  Both CLI tools are restricted to ONLY 'Read' and 'Write' operations:
  - Read: Can read source files for analysis
  - Write: Can create new markdown documentation files

  NOT allowed (for safety):
  - Bash/shell: No arbitrary command execution (no rm, mv, etc.)
  - Edit: Cannot modify existing files
  - Delete: Cannot remove files
  - Other tools with potential side effects

  This ensures the AI cannot accidentally or maliciously delete files,
  modify existing code, or execute dangerous shell commands.
        """,
    )

    parser.add_argument(
        "--parallel",
        "-p",
        type=int,
        default=3,
        help="Number of parallel Copilot instances (default: 3)",
    )

    parser.add_argument(
        "--timeout",
        "-t",
        type=int,
        default=60,
        help="Timeout in seconds between batches to avoid rate limiting (default: 60)",
    )

    parser.add_argument(
        "--copilot-timeout",
        type=int,
        default=300,
        help="Timeout in seconds for each Copilot analysis (default: 300)",
    )

    parser.add_argument(
        "--source-dirs",
        nargs="+",
        help="Source directories to process (default: src/Sharpy.Cli src/Sharpy.Compiler)",
    )

    parser.add_argument(
        "--output-dir",
        default="docs/internal_walkthrough",
        help="Output directory for markdown documentation (default: docs/internal_walkthrough)",
    )

    parser.add_argument(
        "--cli",
        choices=["copilot", "claude"],
        default="copilot",
        help="CLI provider to use: 'copilot' for GitHub Copilot CLI, 'claude' for Claude Code CLI (default: copilot)",
    )

    args = parser.parse_args()

    # Validate arguments
    if args.parallel < 2:
        print("Error: --parallel must be >= 2", file=sys.stderr)
        sys.exit(1)

    if args.timeout < 0:
        print("Error: --timeout must be >= 0", file=sys.stderr)
        sys.exit(1)

    # Check if the selected CLI tool is available
    if args.cli == "copilot":
        try:
            result = subprocess.run(
                ["gh", "--version"], capture_output=True, check=True
            )
            print(f"GitHub CLI version: {result.stdout.decode('utf-8').strip()}")
        except (subprocess.CalledProcessError, FileNotFoundError):
            print(
                "Error: GitHub CLI (gh) is not installed or not in PATH",
                file=sys.stderr,
            )
            print("Install it from: https://cli.github.com/", file=sys.stderr)
            sys.exit(1)
    elif args.cli == "claude":
        try:
            result = subprocess.run(
                ["claude", "--version"], capture_output=True, check=True
            )
            print(f"Claude Code CLI version: {result.stdout.decode('utf-8').strip()}")
        except (subprocess.CalledProcessError, FileNotFoundError):
            print(
                "Error: Claude Code CLI (claude) is not installed or not in PATH",
                file=sys.stderr,
            )
            print(
                "Install it from: https://docs.anthropic.com/en/docs/claude-code",
                file=sys.stderr,
            )
            sys.exit(1)

    # Create config
    config = Config(
        parallel_instances=args.parallel,
        timeout_between_batches=args.timeout,
        copilot_timeout=args.copilot_timeout,
        source_dirs=args.source_dirs,
        output_dir=args.output_dir,
        cli_provider=args.cli,
    )

    # Run async main
    try:
        asyncio.run(main_async(config))
    except KeyboardInterrupt:
        print("\n\nInterrupted by user", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
