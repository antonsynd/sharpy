"""
Main orchestrator for the dogfooding process.

Coordinates code generation, validation, compilation, and verification.
"""

import asyncio
import random
import re
import sys
from dataclasses import dataclass
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Optional

from .config import Config
from .backends import BackendManager, ExecutionResult as AIResult
from .compiler import SharpyCompiler, TempSourceFile, verify_compiler_available
from .prompts import (
    get_spec_context,
    get_code_generation_prompt,
    get_spec_validation_prompt,
    extract_expected_output,
    extract_code_block,
)
from .reporting import Issue, IssueType, IssueReporter, SummaryReporter


class IterationStatus(Enum):
    """Status of a dogfooding iteration."""

    SUCCESS = "success"  # Test passed
    FAILED = "failed"  # Test failed - compiler bug
    SKIPPED = "skipped"  # Skipped - generated code uses unsupported features


@dataclass
class IterationResult:
    """Result of a single dogfooding iteration."""

    status: IterationStatus
    issue_dir: Optional[Path] = None
    skip_reason: Optional[str] = None


def _outputs_equivalent(expected: str, actual: str, rel_tol: float = 1e-9) -> bool:
    """
    Compare two outputs line by line, allowing floating-point tolerance.

    Returns True if outputs are equivalent considering:
    - Exact string matches
    - Floating-point numbers within relative tolerance
    """
    expected_lines = expected.strip().split("\n")
    actual_lines = actual.strip().split("\n")

    if len(expected_lines) != len(actual_lines):
        return False

    float_pattern = re.compile(r"^-?\d+\.?\d*$")

    for exp_line, act_line in zip(expected_lines, actual_lines):
        exp_line = exp_line.strip()
        act_line = act_line.strip()

        if exp_line == act_line:
            continue

        # Try float comparison
        if float_pattern.match(exp_line) and float_pattern.match(act_line):
            try:
                exp_val = float(exp_line)
                act_val = float(act_line)
                # Use relative tolerance for comparison
                if exp_val == 0:
                    if abs(act_val) > rel_tol:
                        return False
                elif abs((exp_val - act_val) / exp_val) > rel_tol:
                    return False
                continue
            except ValueError:
                pass

        # Not equal and not matching floats
        return False

    return True


def _has_multi_arg_print(line: str) -> bool:
    """
    Check if a line contains a multi-argument print call.

    Correctly handles nested parentheses, so:
    - print(add(5, 5))  -> False (single argument: function call result)
    - print(a, b)       -> True (multiple arguments)
    - print(func(a, b), c) -> True (two arguments)
    - print("hello")    -> False (single argument)
    """
    # Find print( in the line
    match = re.search(r"\bprint\s*\(", line)
    if not match:
        return False

    start_idx = match.end()  # Position right after 'print('

    # Parse to find top-level commas (not inside nested parens/brackets/braces)
    depth = 1  # We're already inside the print(
    in_string = None
    i = start_idx

    while i < len(line) and depth > 0:
        char = line[i]

        # Handle string literals
        if in_string:
            if char == in_string and (i == 0 or line[i - 1] != "\\"):
                in_string = None
        elif char in "\"'":
            in_string = char
        elif char in "([{":
            depth += 1
        elif char in ")]}":
            depth -= 1
        elif char == "," and depth == 1:
            # Found a top-level comma inside print()
            return True

        i += 1

    return False


# Feature focuses for code generation - matched to phases 0.1.0-0.1.5
# Each focus tests specific compiler functionality
FEATURE_FOCUSES = [
    "integer_variables",  # x: int = 42
    "float_variables",  # y: float = 3.14
    "bool_variables",  # flag: bool = True
    "arithmetic_operators",  # +, -, *, /, //, %
    "comparison_operators",  # ==, !=, <, <=, >, >=
    "logical_operators",  # and, or, not
    "augmented_assignment",  # +=, -=, *=, /=
    "if_else_simple",  # basic if/else
    "if_elif_else",  # if/elif/else chains
    "while_loop",  # while with counter
    "for_range_single",  # for i in range(n)
    "for_range_start_end",  # for i in range(start, end)
    "for_range_with_step",  # for i in range(start, end, step)
    "break_continue",  # break/continue in loops
    "simple_function",  # def with parameters, return
    "function_with_print",  # function that prints values
    "function_calling_function",  # one function calls another
    "nested_if_in_loop",  # if inside for/while
    "loop_in_function",  # for/while inside function
]

# Bias toward simpler tests initially - complex tests often hit unimplemented features
COMPLEXITY_LEVELS = ["simple", "simple", "medium"]  # 2/3 simple, 1/3 medium


class DogfoodOrchestrator:
    """Orchestrates the dogfooding process."""

    def __init__(self, config: Config):
        self.config = config
        self.backend_manager = BackendManager(config)
        self.compiler = SharpyCompiler(config.project_root, config.sharpy_cli_project)
        self.issue_reporter = IssueReporter(config.issues_dir)
        self.summary_reporter = SummaryReporter(config.output_dir)
        self.spec_context: Optional[str] = None
        self.example_snippets: list[str] = []

    async def initialize(self) -> bool:
        """Initialize the orchestrator and verify dependencies."""
        print("Initializing dogfooding tool...", file=sys.stderr)

        # Check AI backends
        availability = self.backend_manager.check_availability()
        available_backends = [k for k, v in availability.items() if v]
        if not available_backends:
            print("ERROR: No AI backends available", file=sys.stderr)
            return False
        print(f"Available backends: {', '.join(available_backends)}", file=sys.stderr)

        # Check compiler
        print("Checking Sharpy compiler...", file=sys.stderr)
        if not await verify_compiler_available(self.config.sharpy_cli_project):
            print("ERROR: Sharpy compiler not available", file=sys.stderr)
            return False
        print("Sharpy compiler is available", file=sys.stderr)

        # Load specification context
        print("Loading specification context...", file=sys.stderr)
        self.spec_context = get_spec_context(
            self.config.spec_dir, self.config.phases_file
        )
        print(f"Loaded {len(self.spec_context)} chars of spec context", file=sys.stderr)

        # Load example snippets
        self._load_example_snippets()
        print(f"Loaded {len(self.example_snippets)} example snippets", file=sys.stderr)

        return True

    def _load_example_snippets(self) -> None:
        """Load example Sharpy snippets from the snippets directory.

        Filters to only include snippets that use features from phases 0.1.0-0.1.5.
        """
        snippets_dir = self.config.snippets_dir
        if not snippets_dir.exists():
            return

        # Features that indicate code is beyond phases 0.1.0-0.1.5
        forbidden_patterns = [
            "class ",
            "struct ",
            "interface ",
            "import ",
            "from ",
            "lambda",
            "try:",
            "except:",
            "raise ",
            'f"',
            "f'",
            "= [",
            "= {",
            ": list",
            ": dict",
            ": set",
            "-> list",
            "-> dict",
            "Optional[",
            "?",  # nullable types
            "@",  # decorators
        ]

        for spy_file in snippets_dir.glob("*.spy"):
            try:
                content = spy_file.read_text()
                # Only include smaller snippets without forbidden features
                if len(content) < 400:
                    content_lower = content.lower()
                    has_forbidden = any(
                        pattern.lower() in content_lower
                        for pattern in forbidden_patterns
                    )
                    if not has_forbidden:
                        self.example_snippets.append(content)
            except Exception:
                continue

    async def run_iteration(
        self,
        iteration: int,
        feature_focus: str,
        complexity: str,
    ) -> IterationResult:
        """Run a single dogfooding iteration.

        Returns:
            IterationResult with status (SUCCESS, FAILED, or SKIPPED) and optional issue_dir
        """
        print(f"\n{'='*60}", file=sys.stderr)
        print(f"Iteration {iteration}: {feature_focus} ({complexity})", file=sys.stderr)
        print(f"{'='*60}", file=sys.stderr)

        start_time = datetime.now()
        timestamp = start_time.isoformat()

        # Step 1: Generate code
        print("\n[1/4] Generating code...", file=sys.stderr)
        gen_result = await self._generate_code(feature_focus, complexity)
        if not gen_result.success or not gen_result.output:
            issue = Issue(
                issue_type=IssueType.GENERATION_FAILED,
                timestamp=timestamp,
                generated_code="",
                error_message=gen_result.error or "No code generated",
                feature_focus=feature_focus,
                complexity=complexity,
                backend_used=gen_result.backend,
                generation_duration=gen_result.duration_seconds,
            )
            issue_dir = self.issue_reporter.report(issue)
            return IterationResult(IterationStatus.FAILED, issue_dir)

        code = extract_code_block(gen_result.output) or gen_result.output
        expected_output = extract_expected_output(code)
        print(f"  Generated {len(code)} chars of code", file=sys.stderr)

        # Step 1.5: Quick pre-validation (programmatic check for forbidden features)
        prevalidation_error = self._quick_prevalidate(code)
        if prevalidation_error:
            print(f"  Pre-validation failed: {prevalidation_error}", file=sys.stderr)
            print(
                "  Skipping (generated code uses features beyond phases 0.1.0-0.1.5)",
                file=sys.stderr,
            )
            return IterationResult(
                IterationStatus.SKIPPED,
                skip_reason=f"Unsupported feature: {prevalidation_error}",
            )

        # Step 2: Validate against spec (AI-based detailed check)
        print("\n[2/4] Validating against spec...", file=sys.stderr)
        val_result = await self._validate_code(code)
        if not val_result.success:
            print(f"  Validation failed: {val_result.error}", file=sys.stderr)
            # Skip this iteration rather than reporting as issue
            # Invalid generated code is not a compiler bug
            return IterationResult(
                IterationStatus.SKIPPED,
                skip_reason=f"Validation backend error: {val_result.error}",
            )

        validation_output = val_result.output
        if "INVALID" in validation_output.upper():
            print(f"  Code is invalid per spec, skipping", file=sys.stderr)
            return IterationResult(
                IterationStatus.SKIPPED,
                skip_reason="Generated code invalid per spec",
            )

        print("  Code validated successfully", file=sys.stderr)

        # Step 3: Compile and run
        print("\n[3/4] Compiling and running...", file=sys.stderr)
        with TempSourceFile(code) as source_path:
            run_result = await self.compiler.run_file(
                source_path, timeout=self.config.execution_timeout
            )

            if not run_result.success:
                # Try to get generated C# for debugging
                cs_result = await self.compiler.emit_cs(source_path, timeout=30.0)
                generated_cs = cs_result.generated_cs if cs_result.success else None

                issue_type = (
                    IssueType.TIMEOUT
                    if run_result.timed_out
                    else IssueType.COMPILATION_FAILED
                )
                if run_result.exit_code != 0 and not run_result.timed_out:
                    # Check if it's a runtime error vs compilation error
                    if "error" in (run_result.error or "").lower():
                        if "CS" in (run_result.error or ""):
                            issue_type = IssueType.COMPILATION_FAILED
                        else:
                            issue_type = IssueType.EXECUTION_FAILED

                # Print failure information
                print(
                    f"  ✗ {issue_type.value}: {run_result.error[:200] if run_result.error else 'Unknown error'}",
                    file=sys.stderr,
                )

                issue = Issue(
                    issue_type=issue_type,
                    timestamp=timestamp,
                    generated_code=code,
                    expected_output=expected_output,
                    error_message=run_result.error,
                    compiler_output=run_result.output,
                    generated_cs=generated_cs,
                    validation_result=validation_output,
                    feature_focus=feature_focus,
                    complexity=complexity,
                    backend_used=gen_result.backend,
                    generation_duration=gen_result.duration_seconds,
                    execution_duration=run_result.duration_seconds,
                )
                issue_dir = self.issue_reporter.report(issue)
                print(f"  Issue reported: {issue_dir.name}", file=sys.stderr)
                return IterationResult(IterationStatus.FAILED, issue_dir)

        actual_output = run_result.output.strip()
        print(
            f"  Execution successful, got {len(actual_output)} chars output",
            file=sys.stderr,
        )

        # Step 4: Verify output
        print("\n[4/4] Verifying output...", file=sys.stderr)
        if expected_output:
            # Normalize outputs for comparison
            expected_normalized = expected_output.strip()
            actual_normalized = actual_output.strip()

            if expected_normalized != actual_normalized:
                # First, try programmatic float-tolerant comparison
                if _outputs_equivalent(expected_normalized, actual_normalized):
                    print("  Outputs match (with float tolerance)", file=sys.stderr)
                else:
                    # Fall back to AI-assisted comparison for fuzzy matching
                    verify_result = await self._verify_output(
                        code, expected_output, actual_output
                    )
                    if "MISMATCH" in verify_result.output.upper():
                        issue = Issue(
                            issue_type=IssueType.OUTPUT_MISMATCH,
                            timestamp=timestamp,
                            generated_code=code,
                            expected_output=expected_output,
                            actual_output=actual_output,
                            validation_result=validation_output,
                            feature_focus=feature_focus,
                            complexity=complexity,
                            backend_used=gen_result.backend,
                            generation_duration=gen_result.duration_seconds,
                            execution_duration=run_result.duration_seconds,
                        )
                        issue_dir = self.issue_reporter.report(issue)
                        return IterationResult(IterationStatus.FAILED, issue_dir)
                    else:
                        print("  Outputs match (AI verified)", file=sys.stderr)
        else:
            print(
                "  No expected output specified, skipping verification", file=sys.stderr
            )

        print("\n✓ Iteration completed successfully!", file=sys.stderr)
        return IterationResult(IterationStatus.SUCCESS)

    async def _generate_code(self, feature_focus: str, complexity: str) -> AIResult:
        """Generate Sharpy code using AI."""
        prompt = get_code_generation_prompt(
            self.spec_context,
            feature_focus=feature_focus,
            complexity=complexity,
            example_snippets=(
                random.sample(self.example_snippets, min(3, len(self.example_snippets)))
                if self.example_snippets
                else None
            ),
        )
        return await self.backend_manager.execute(
            prompt, timeout=self.config.generation_timeout
        )

    def _quick_prevalidate(self, code: str) -> Optional[str]:
        """Quick programmatic check for forbidden features.

        Returns None if code passes, or an error message if it fails.
        This catches obvious issues before expensive AI validation.
        """
        import re

        # Patterns that indicate features beyond phases 0.1.0-0.1.5
        forbidden_checks = [
            (r'f"[^"]*\{', "f-string interpolation"),
            (r"f'[^']*\{", "f-string interpolation"),
            # Note: multi-argument print is checked separately with _has_multi_arg_print()
            (r"def\s+\w+\s*\([^)]*=\s*[^,)]+", "default parameter value"),
            (
                r"\w+\s*=\s*\w+\s*\(",
                None,
            ),  # skip - this is just a function call assignment
            (r"\(\s*\w+\s*=\s*", "keyword argument"),
            (r"\bclass\s+\w+", "class definition"),
            (r"\bstruct\s+\w+", "struct definition"),
            (r"\binterface\s+\w+", "interface definition"),
            (r"\bimport\s+", "import statement"),
            (r"\bfrom\s+\w+\s+import", "from import statement"),
            (r"\blambda\s*[^:]*:", "lambda expression"),
            (r"\btry\s*:", "try block"),
            (r"\bexcept\s*", "except block"),
            (r"\braise\s+", "raise statement"),
            (r"\bwith\s+", "with statement"),
            (r"\basync\s+def", "async function"),
            (r"\bawait\s+", "await expression"),
            (r":\s*list\[", "list type annotation"),
            (r":\s*dict\[", "dict type annotation"),
            (r":\s*set\[", "set type annotation"),
            (r":\s*\w+\?", "nullable type annotation"),
            (r"->\s*\w+\?", "nullable return type"),
            (r":\s*Optional\[", "Optional type"),
            (r"\[\s*\]", "empty list literal"),
            (r"\{\s*\}", "empty dict/set literal"),
            (r"\[\s*\w+.*for\s+\w+\s+in", "list comprehension"),
            (r"\{[^}]*for\s+\w+\s+in", "dict/set comprehension"),
            (r"^\s*@\w+", "decorator"),
            (r"\bx\s+if\s+.+\s+else\s+", "ternary expression"),
            (r"\w+\s*,\s*\w+\s*=", "tuple unpacking"),
        ]

        lines = code.split("\n")
        for i, line in enumerate(lines, 1):
            # Skip comments
            stripped = line.split("#")[0].strip()
            if not stripped:
                continue

            # Special check for multi-argument print (needs proper paren parsing)
            if _has_multi_arg_print(stripped):
                return f"Line {i}: multi-argument print (use multiple print() calls) - '{stripped[:50]}...'"

            for pattern, description in forbidden_checks:
                if description is None:
                    continue
                if re.search(pattern, stripped):
                    # Special case: skip "keyword argument" false positives for comparisons
                    if description == "keyword argument":
                        # Check if it's actually a comparison or assignment in a function call
                        if re.search(r"\w+\s*==\s*", stripped):
                            continue
                        # Skip if it's a regular assignment (no parenthesis before =)
                        if not re.search(r"\([^)]*\w+\s*=\s*[^=]", stripped):
                            continue
                    return f"Line {i}: {description} - '{stripped[:50]}...'"

        return None

    async def _validate_code(self, code: str) -> AIResult:
        """Validate code against the Sharpy spec."""
        prompt = get_spec_validation_prompt(code, self.spec_context)
        return await self.backend_manager.execute(
            prompt, timeout=60.0  # Validation should be quick
        )

    async def _verify_output(self, code: str, expected: str, actual: str) -> AIResult:
        """Verify output using AI for fuzzy comparison."""
        from .prompts import get_output_verification_prompt

        prompt = get_output_verification_prompt(code, expected, actual)
        return await self.backend_manager.execute(
            prompt, timeout=30.0  # Quick comparison
        )

    async def run(self, iterations: Optional[int] = None) -> int:
        """Run the full dogfooding process."""
        max_iterations = iterations or self.config.max_iterations

        if not await self.initialize():
            return 1

        print(
            f"\nStarting dogfooding with {max_iterations} iterations...",
            file=sys.stderr,
        )
        print(f"Output directory: {self.config.output_dir}", file=sys.stderr)

        successful = 0
        failed = 0
        skipped = 0

        for i in range(1, max_iterations + 1):
            # Randomize feature focus and complexity
            feature_focus = random.choice(FEATURE_FOCUSES)
            complexity = random.choice(COMPLEXITY_LEVELS)

            start_time = datetime.now()
            try:
                result = await self.run_iteration(i, feature_focus, complexity)
                duration = (datetime.now() - start_time).total_seconds()

                if result.status == IterationStatus.SUCCESS:
                    successful += 1
                    self.summary_reporter.add_run(
                        i,
                        feature_focus,
                        complexity,
                        success=True,
                        duration=duration,
                    )
                elif result.status == IterationStatus.SKIPPED:
                    skipped += 1
                    self.summary_reporter.add_run(
                        i,
                        feature_focus,
                        complexity,
                        success=False,
                        issue_type=IssueType.SKIPPED,
                        duration=duration,
                        skip_reason=result.skip_reason,
                    )
                else:  # FAILED
                    failed += 1
                    issue_type = None
                    if result.issue_dir:
                        # Extract issue type from the directory name
                        for it in IssueType:
                            if it.value in result.issue_dir.name:
                                issue_type = it
                                break
                    self.summary_reporter.add_run(
                        i,
                        feature_focus,
                        complexity,
                        success=False,
                        issue_type=issue_type,
                        issue_dir=result.issue_dir,
                        duration=duration,
                    )

            except Exception as e:
                import traceback

                print(f"ERROR in iteration {i}: {e}", file=sys.stderr)
                traceback.print_exc(file=sys.stderr)
                failed += 1
                self.summary_reporter.add_run(
                    i,
                    feature_focus,
                    complexity,
                    success=False,
                    issue_type=IssueType.EXECUTION_FAILED,
                    duration=(datetime.now() - start_time).total_seconds(),
                    skip_reason=f"Unexpected error: {str(e)}",
                )

            # Save summary after each iteration
            self.summary_reporter.save()

        # Final summary
        print(f"\n{'='*60}", file=sys.stderr)
        print("DOGFOODING COMPLETE", file=sys.stderr)
        print(f"{'='*60}", file=sys.stderr)
        print(f"Successful: {successful}/{max_iterations}", file=sys.stderr)
        print(f"Failed: {failed}/{max_iterations}", file=sys.stderr)
        print(f"Skipped: {skipped}/{max_iterations}", file=sys.stderr)
        print(f"\nIssues saved to: {self.config.issues_dir}", file=sys.stderr)
        print(f"Summary: {self.config.output_dir / 'SUMMARY.md'}", file=sys.stderr)

        # Return success if no actual failures (skips don't count)
        return 0 if failed == 0 else 1
