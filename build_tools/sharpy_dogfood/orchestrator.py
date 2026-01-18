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
from .compiler import (
    SharpyCompiler,
    TempSourceFile,
    TempProjectDir,
    verify_compiler_available,
)
from .prompts import (
    get_spec_context,
    get_code_generation_prompt,
    get_multifile_generation_prompt,
    get_spec_validation_prompt,
    get_regeneration_prompt,
    extract_expected_output,
    extract_code_block,
    extract_multifile_code,
    extract_expected_output_from_multifile,
    load_test_fixtures,
    format_fixtures_for_prompt,
)
from .reporting import (
    Issue,
    IssueType,
    IssueReporter,
    Skip,
    SkipReporter,
    SummaryReporter,
    Success,
    SuccessReporter,
)


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
    success_dir: Optional[Path] = None
    skip_dir: Optional[Path] = None
    skip_reason: Optional[str] = None


@dataclass
class GenerationResult:
    """Result of the generate-and-validate process with retry loop."""

    success: bool
    code: Optional[str] = None
    expected_output: Optional[str] = None
    skip_reason: Optional[str] = None
    backend_used: Optional[str] = None
    generation_duration: Optional[float] = None
    rate_limited: bool = False
    attempts: int = 1
    validation_output: Optional[str] = None  # AI validation output (for debugging)


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


async def _verify_expected_with_python(
    code: str, expected_output: str, timeout: float = 5.0
) -> tuple[bool, Optional[str], Optional[str]]:
    """
    Verify expected output by running the code as Python.

    Since Sharpy is syntactically similar to Python for basic features,
    we can use Python to verify the expected output is correct.

    Returns:
        (is_valid, python_output, error_message)
        - is_valid: True if Python output matches expected, or Python execution failed
        - python_output: The actual output from Python (if successful)
        - error_message: Error details if Python execution failed
    """
    import tempfile
    import os

    # Convert Sharpy to Python (minimal transformations needed for basic code)
    python_code = _sharpy_to_python(code)

    # Create a temporary file for the Python code
    with tempfile.NamedTemporaryFile(mode="w", suffix=".py", delete=False) as f:
        f.write(python_code)
        temp_path = f.name

    try:
        process = await asyncio.create_subprocess_exec(
            sys.executable,
            temp_path,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
        )

        try:
            stdout, stderr = await asyncio.wait_for(
                process.communicate(), timeout=timeout
            )
        except asyncio.TimeoutError:
            try:
                process.kill()
                await process.wait()
            except Exception:
                pass
            return True, None, "Python execution timed out (not a validation failure)"

        if process.returncode != 0:
            # Python failed to run the code - this might be due to Sharpy-specific
            # syntax that doesn't translate, so we don't treat it as validation failure
            return True, None, f"Python execution failed: {stderr.decode().strip()}"

        python_output = stdout.decode().strip()
        expected_stripped = expected_output.strip()

        # Compare outputs
        if _outputs_equivalent(expected_stripped, python_output):
            return True, python_output, None
        else:
            # Expected output doesn't match Python output - likely an AI mistake
            return (
                False,
                python_output,
                f"Expected output doesn't match Python: got '{python_output}', expected '{expected_stripped}'",
            )
    finally:
        try:
            os.unlink(temp_path)
        except Exception:
            pass


def _sharpy_to_python(sharpy_code: str) -> str:
    """
    Convert Sharpy code to Python for verification.

    For basic features (phases 0.1.0-0.1.5), Sharpy is syntactically
    identical to Python, so minimal transformation is needed.
    Note: Classes, structs, enums, interfaces (0.1.6-0.1.10) may need
    additional conversion or skipping for Python verification.
    """
    lines = []
    for line in sharpy_code.split("\n"):
        # Skip lines that are pure comments starting with EXPECTED OUTPUT
        stripped = line.strip()
        if stripped.upper().startswith(
            "# EXPECTED OUTPUT"
        ) or stripped.upper().startswith("#EXPECTED OUTPUT"):
            # Start of expected output block - skip rest of comments
            break
        if stripped.upper().startswith("# EXPECTED:"):
            break

        # Remove type annotations from variable declarations: x: int = 5 -> x = 5
        # But preserve in function signatures for Python's type hints
        if ":" in line and "=" in line and "def " not in line:
            # Simple variable declaration with type annotation
            match = re.match(r"^(\s*)(\w+)\s*:\s*\w+\s*=(.*)$", line)
            if match:
                indent, name, value = match.groups()
                line = f"{indent}{name} ={value}"

        # Remove 'const' keyword (not valid Python)
        line = re.sub(r"\bconst\s+", "", line)

        lines.append(line)

    return "\n".join(lines)


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


# Feature focuses for code generation - matched to phases 0.1.0-0.1.10
# Each focus tests specific compiler functionality
FEATURE_FOCUSES = [
    # Phase 0.1.3: Variables & Expressions
    "integer_variables",  # x: int = 42
    "float_variables",  # y: float = 3.14
    "bool_variables",  # flag: bool = True
    "arithmetic_operators",  # +, -, *, /, //, %, **
    "comparison_operators",  # ==, !=, <, <=, >, >=
    "logical_operators",  # and, or, not
    "augmented_assignment",  # +=, -=, *=, /=
    # Phase 0.1.4: Control Flow
    "if_else_simple",  # basic if/else
    "if_elif_else",  # if/elif/else chains
    "while_loop",  # while with counter
    "for_range_single",  # for i in range(n)
    "for_range_start_end",  # for i in range(start, end)
    "for_range_with_step",  # for i in range(start, end, step)
    "break_continue",  # break/continue in loops
    # Phase 0.1.5: Functions
    "simple_function",  # def with parameters, return
    "function_with_print",  # function that prints values
    "function_calling_function",  # one function calls another
    "function_default_params",  # def foo(x: int, y: int = 5)
    "function_keyword_args",  # foo(x=10, y=20)
    # Phase 0.1.6: Classes
    "simple_class",  # class with fields
    "class_with_init",  # class with __init__
    "class_instance_methods",  # instance methods with self
    "class_static_methods",  # static methods (no self)
    "class_field_access",  # obj.field, self.field
    # Phase 0.1.7: Inheritance & Interfaces
    "class_inheritance",  # class Child(Parent)
    "super_init_call",  # super().__init__()
    "abstract_class",  # @abstract class
    "virtual_override",  # @virtual and @override methods
    "interface_definition",  # interface with method signatures
    "interface_implementation",  # class implements interface
    "access_modifiers",  # @private, @protected
    # Phase 0.1.8: Structs & Enums
    "struct_definition",  # struct with fields
    "enum_definition",  # enum with values
    "enum_usage",  # using enum values
    # Phase 0.1.9: Type System
    "nullable_types",  # T? syntax
    "null_coalescing",  # ?? operator
    "null_conditional",  # ?. operator
    "type_narrowing",  # if x is not None
    "type_alias",  # type UserId = int
    "generic_class",  # class Box[T]
    "generic_function",  # def foo[T](x: T) -> T
    # Phase 0.1.10: Module System
    "import_statement",  # import module
    "from_import",  # from module import item
    # Combinations
    "nested_if_in_loop",  # if inside for/while
    "loop_in_function",  # for/while inside function
    "class_with_loop",  # class with method using loop
    "inheritance_with_override",  # override methods with logic
]

# Bias toward simpler tests initially - complex tests often hit unimplemented features
COMPLEXITY_LEVELS = ["simple", "simple", "simple", "medium", "medium", "complex"]


class DogfoodOrchestrator:
    """Orchestrates the dogfooding process."""

    def __init__(self, config: Config):
        self.config = config
        self.backend_manager = BackendManager(config)
        self.compiler = SharpyCompiler(config.project_root, config.sharpy_cli_project)
        self.issue_reporter = IssueReporter(config.issues_dir)
        self.success_reporter = SuccessReporter(config.successes_dir)
        self.skip_reporter = SkipReporter(config.skips_dir)
        self.summary_reporter = SummaryReporter(config.output_dir)
        self.spec_context: Optional[str] = None
        self.example_snippets: list[str] = []
        self.test_fixtures: dict[str, list[tuple[str, str]]] = {}
        self.fixtures_prompt_section: str = ""

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

        # Load existing test fixtures to avoid duplicating tests
        self._load_test_fixtures()
        total_fixtures = sum(len(tests) for tests in self.test_fixtures.values())
        print(
            f"Loaded {total_fixtures} test fixtures from {len(self.test_fixtures)} categories",
            file=sys.stderr,
        )

        return True

    def _load_test_fixtures(self) -> None:
        """Load existing integration test fixtures to show the AI what already exists."""
        fixtures_dir = self.config.test_fixtures_dir
        if not fixtures_dir.exists():
            print(f"Test fixtures directory not found: {fixtures_dir}", file=sys.stderr)
            return

        self.test_fixtures = load_test_fixtures(fixtures_dir)
        self.fixtures_prompt_section = format_fixtures_for_prompt(
            self.test_fixtures,
            max_examples_per_category=2,
            max_total_chars=3000,
        )

    def _load_example_snippets(self) -> None:
        """Load example Sharpy snippets from the snippets directory.

        Filters to only include snippets that use features from phases 0.1.0-0.1.10.
        Excludes snippets with v0.1.11+ features like collections, comprehensions, etc.
        """
        snippets_dir = self.config.snippets_dir
        if not snippets_dir.exists():
            return

        # Features that indicate code is beyond phases 0.1.0-0.1.10
        # (collections, comprehensions, exceptions, lambdas, .NET interop)
        forbidden_patterns = [
            "lambda",
            "try:",
            "except:",
            "raise ",
            'f"',  # f-strings (v0.1.11)
            "f'",  # f-strings (v0.1.11)
            "= [",  # list literals (v0.1.11)
            "= {",  # dict/set literals (v0.1.11)
            ": list[",  # list type (v0.1.11)
            ": dict[",  # dict type (v0.1.11)
            ": set[",  # set type (v0.1.11)
            "-> list[",  # list return type (v0.1.11)
            "-> dict[",  # dict return type (v0.1.11)
            "Optional[",  # Use T? instead
            "from system",  # .NET interop (v0.1.12)
            "from System",  # .NET interop (v0.1.12)
            " for ",  # comprehensions (approximate check)
        ]

        for spy_file in snippets_dir.glob("*.spy"):
            try:
                content = spy_file.read_text()
                # Only include smaller snippets without forbidden features
                if len(content) < 500:
                    has_forbidden = any(
                        pattern in content for pattern in forbidden_patterns
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

        # Step 1 & 2: Generate and validate code (with retry loop on validation failure)
        print("\n[1/4] Generating code...", file=sys.stderr)
        gen_result = await self._generate_and_validate_code(feature_focus, complexity)

        if not gen_result.success:
            # Check if it's a rate limit issue (not a real failure)
            if gen_result.rate_limited:
                print(
                    "  Generation failed due to rate limiting (not a bug)",
                    file=sys.stderr,
                )
                return IterationResult(
                    IterationStatus.SKIPPED,
                    skip_reason=gen_result.skip_reason,
                )

            # If it was a validation failure after retries, skip but save for inspection
            if gen_result.attempts > 1:
                print(
                    f"  Code generation failed after {gen_result.attempts} attempts",
                    file=sys.stderr,
                )
                # Save the skip for inspection if we have generated code
                skip_dir = None
                if gen_result.code:
                    skip = Skip(
                        timestamp=timestamp,
                        skip_reason=gen_result.skip_reason or "Validation failed",
                        generated_code=gen_result.code,
                        expected_output=gen_result.expected_output,
                        feature_focus=feature_focus,
                        complexity=complexity,
                        backend_used=gen_result.backend_used,
                        generation_duration=gen_result.generation_duration,
                        validation_output=gen_result.validation_output,
                    )
                    skip_dir = self.skip_reporter.report(skip)
                    print(
                        f"  Skip saved for inspection: {skip_dir.name}", file=sys.stderr
                    )
                return IterationResult(
                    IterationStatus.SKIPPED,
                    skip_dir=skip_dir,
                    skip_reason=gen_result.skip_reason,
                )

            # First attempt generation failure - report as issue
            issue = Issue(
                issue_type=IssueType.GENERATION_FAILED,
                timestamp=timestamp,
                generated_code="",
                error_message=gen_result.skip_reason or "No code generated",
                feature_focus=feature_focus,
                complexity=complexity,
                backend_used=gen_result.backend_used,
                generation_duration=gen_result.generation_duration,
            )
            issue_dir = self.issue_reporter.report(issue)
            return IterationResult(IterationStatus.FAILED, issue_dir)

        code = gen_result.code
        expected_output = gen_result.expected_output
        if gen_result.attempts > 1:
            print(
                f"  Code validated successfully after {gen_result.attempts} attempts",
                file=sys.stderr,
            )

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
                    feature_focus=feature_focus,
                    complexity=complexity,
                    backend_used=gen_result.backend_used,
                    generation_duration=gen_result.generation_duration,
                    execution_duration=run_result.duration_seconds,
                )
                issue_dir = self.issue_reporter.report(issue)
                print(f"  Issue reported: {issue_dir.name}", file=sys.stderr)
                return IterationResult(IterationStatus.FAILED, issue_dir)

        actual_output = run_result.output.strip()
        execution_duration = run_result.duration_seconds
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

            if expected_normalized == actual_normalized:
                # Exact match - print status (this was the missing case!)
                print("  ✓ Output matches expected (exact)", file=sys.stderr)
            elif _outputs_equivalent(expected_normalized, actual_normalized):
                # Float-tolerant match
                print(
                    "  ✓ Output matches expected (with float tolerance)",
                    file=sys.stderr,
                )
            else:
                # Fall back to AI-assisted comparison for fuzzy matching
                verify_result = await self._verify_output(
                    code, expected_output, actual_output
                )
                if "MISMATCH" in verify_result.output.upper():
                    print("  ✗ Output mismatch detected", file=sys.stderr)
                    issue = Issue(
                        issue_type=IssueType.OUTPUT_MISMATCH,
                        timestamp=timestamp,
                        generated_code=code,
                        expected_output=expected_output,
                        actual_output=actual_output,
                        feature_focus=feature_focus,
                        complexity=complexity,
                        backend_used=gen_result.backend_used,
                        generation_duration=gen_result.generation_duration,
                        execution_duration=run_result.duration_seconds,
                    )
                    issue_dir = self.issue_reporter.report(issue)
                    return IterationResult(IterationStatus.FAILED, issue_dir)
                else:
                    print("  ✓ Output matches expected (AI verified)", file=sys.stderr)
        else:
            print(
                "  No expected output specified, skipping verification", file=sys.stderr
            )

        # Report successful iteration
        success_dir = None
        if expected_output:
            success = Success(
                timestamp=timestamp,
                generated_code=code,
                expected_output=expected_output,
                actual_output=actual_output,
                feature_focus=feature_focus,
                complexity=complexity,
                backend_used=gen_result.backend_used,
                generation_duration=gen_result.generation_duration,
                execution_duration=execution_duration,
            )
            success_dir = self.success_reporter.report(success)
            print(f"  Success saved: {success_dir.name}", file=sys.stderr)

        print("\n✓ Iteration completed successfully!", file=sys.stderr)
        return IterationResult(IterationStatus.SUCCESS, success_dir=success_dir)

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
            existing_fixtures_section=self.fixtures_prompt_section,
        )
        return await self.backend_manager.execute(
            prompt, timeout=self.config.generation_timeout
        )

    async def _regenerate_code(
        self,
        feature_focus: str,
        complexity: str,
        previous_code: str,
        validation_error: str,
        attempt: int,
    ) -> AIResult:
        """Regenerate Sharpy code with feedback from validation failure."""
        prompt = get_regeneration_prompt(
            self.spec_context,
            feature_focus=feature_focus,
            complexity=complexity,
            previous_code=previous_code,
            validation_error=validation_error,
            attempt=attempt,
            max_attempts=self.config.max_regeneration_attempts,
            example_snippets=(
                random.sample(self.example_snippets, min(2, len(self.example_snippets)))
                if self.example_snippets
                else None
            ),
            existing_fixtures_section=self.fixtures_prompt_section,
        )
        return await self.backend_manager.execute(
            prompt, timeout=self.config.generation_timeout
        )

    async def _generate_and_validate_code(
        self, feature_focus: str, complexity: str
    ) -> GenerationResult:
        """Generate and validate code with retry loop on validation failure.

        Implements a feedback loop similar to sharpy_auto_builder's fix_test_failures_node.
        If code fails pre-validation or AI spec validation, retries up to
        max_regeneration_attempts times with feedback about the validation error.

        Returns:
            GenerationResult with code if successful, or skip_reason if all attempts failed.
        """
        max_attempts = self.config.max_regeneration_attempts
        last_code: Optional[str] = None
        last_error: Optional[str] = None
        total_duration = 0.0
        backend_used: Optional[str] = None

        for attempt in range(1, max_attempts + 1):
            # Generate or regenerate code
            if attempt == 1:
                # First attempt: generate fresh code
                gen_result = await self._generate_code(feature_focus, complexity)
            else:
                # Subsequent attempts: regenerate with feedback
                print(
                    f"  Regenerating code (attempt {attempt}/{max_attempts})...",
                    file=sys.stderr,
                )
                gen_result = await self._regenerate_code(
                    feature_focus=feature_focus,
                    complexity=complexity,
                    previous_code=last_code,
                    validation_error=last_error,
                    attempt=attempt,
                )

            total_duration += gen_result.duration_seconds or 0.0
            backend_used = gen_result.backend

            # Check for rate limiting
            if not gen_result.success or not gen_result.output:
                is_rate_limited = gen_result.rate_limited or (
                    gen_result.error
                    and any(
                        x in gen_result.error.lower()
                        for x in ["rate limit", "rate-limit", "unavailable", "429"]
                    )
                )
                if is_rate_limited:
                    return GenerationResult(
                        success=False,
                        skip_reason=f"Rate limited: {gen_result.error or 'All backends unavailable'}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        rate_limited=True,
                        attempts=attempt,
                    )
                # Non-rate-limit generation failure - report as issue
                return GenerationResult(
                    success=False,
                    skip_reason=gen_result.error or "No code generated",
                    backend_used=backend_used,
                    generation_duration=total_duration,
                    attempts=attempt,
                )

            code = extract_code_block(gen_result.output) or gen_result.output
            last_code = code
            print(f"  Generated {len(code)} chars of code", file=sys.stderr)

            # Step 1.5: Quick pre-validation (programmatic check for forbidden features)
            prevalidation_error = self._quick_prevalidate(code)
            if prevalidation_error:
                print(
                    f"  Pre-validation failed: {prevalidation_error}", file=sys.stderr
                )
                last_error = f"Pre-validation error: {prevalidation_error}"
                if attempt < max_attempts:
                    print(
                        f"  Will retry with feedback ({attempt}/{max_attempts})...",
                        file=sys.stderr,
                    )
                    continue
                else:
                    return GenerationResult(
                        success=False,
                        code=code,
                        expected_output=extract_expected_output(code),
                        skip_reason=f"Pre-validation failed after {attempt} attempts: {prevalidation_error}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                    )

            # Step 1.6: Verify expected output using Python
            expected_output = extract_expected_output(code)
            if expected_output:
                is_valid, python_output, verify_error = (
                    await _verify_expected_with_python(code, expected_output)
                )
                if not is_valid:
                    print(
                        f"  Expected output verification failed: {verify_error}",
                        file=sys.stderr,
                    )
                    last_error = f"Expected output verification error: {verify_error}. Python says output should be: {python_output}"
                    if attempt < max_attempts:
                        print(
                            f"  Will retry with feedback ({attempt}/{max_attempts})...",
                            file=sys.stderr,
                        )
                        continue
                    else:
                        return GenerationResult(
                            success=False,
                            code=code,
                            expected_output=expected_output,
                            skip_reason=f"Invalid expected output after {attempt} attempts (Python says: {python_output})",
                            backend_used=backend_used,
                            generation_duration=total_duration,
                            attempts=attempt,
                        )
                elif python_output is not None:
                    print(
                        f"  Expected output verified with Python: {python_output[:50]}...",
                        file=sys.stderr,
                    )

            # Step 2: Validate against spec (AI-based detailed check)
            print("\n[2/4] Validating against spec...", file=sys.stderr)
            val_result = await self._validate_code(code)
            if not val_result.success:
                print(
                    f"  Validation backend error: {val_result.error}", file=sys.stderr
                )
                last_error = f"Validation backend error: {val_result.error}"
                if attempt < max_attempts:
                    print(
                        f"  Will retry with feedback ({attempt}/{max_attempts})...",
                        file=sys.stderr,
                    )
                    continue
                else:
                    return GenerationResult(
                        success=False,
                        code=code,
                        expected_output=expected_output,
                        skip_reason=f"Validation backend error after {attempt} attempts: {val_result.error}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                        validation_output=val_result.error,
                    )

            validation_output = val_result.output
            if "INVALID" in validation_output.upper():
                print(f"  Code is invalid per spec", file=sys.stderr)
                # Extract the specific reason from validation output
                last_error = f"Code invalid per spec: {validation_output[:500]}"
                if attempt < max_attempts:
                    print(
                        f"  Will retry with feedback ({attempt}/{max_attempts})...",
                        file=sys.stderr,
                    )
                    continue
                else:
                    return GenerationResult(
                        success=False,
                        code=code,
                        expected_output=expected_output,
                        skip_reason=f"Code invalid per spec after {attempt} attempts",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                        validation_output=validation_output,
                    )

            # Success!
            print("  Code validated successfully", file=sys.stderr)
            return GenerationResult(
                success=True,
                code=code,
                expected_output=expected_output,
                backend_used=backend_used,
                generation_duration=total_duration,
                attempts=attempt,
            )

        # Should not reach here, but just in case
        return GenerationResult(
            success=False,
            code=last_code,
            expected_output=extract_expected_output(last_code) if last_code else None,
            skip_reason="Generation failed after all retry attempts",
            backend_used=backend_used,
            generation_duration=total_duration,
            attempts=max_attempts,
        )

    async def _generate_multifile_code(
        self, feature_focus: str, complexity: str
    ) -> AIResult:
        """Generate multi-file Sharpy code using AI."""
        prompt = get_multifile_generation_prompt(
            self.spec_context,
            feature_focus=feature_focus,
            complexity=complexity,
            example_snippets=(
                random.sample(self.example_snippets, min(3, len(self.example_snippets)))
                if self.example_snippets
                else None
            ),
            existing_fixtures_section=self.fixtures_prompt_section,
        )
        return await self.backend_manager.execute(
            prompt, timeout=self.config.generation_timeout
        )

    async def run_multifile_iteration(
        self,
        iteration: int,
        feature_focus: str,
        complexity: str,
    ) -> IterationResult:
        """Run a single multi-file dogfooding iteration.

        Similar to run_iteration but for multi-file projects with imports.

        Returns:
            IterationResult with status (SUCCESS, FAILED, or SKIPPED) and optional issue_dir
        """
        print(f"\n{'='*60}", file=sys.stderr)
        print(
            f"Iteration {iteration} [MULTI-FILE]: {feature_focus} ({complexity})",
            file=sys.stderr,
        )
        print(f"{'='*60}", file=sys.stderr)

        start_time = datetime.now()
        timestamp = start_time.isoformat()

        # Step 1: Generate multi-file code
        print("\n[1/4] Generating multi-file project...", file=sys.stderr)
        gen_result = await self._generate_multifile_code(feature_focus, complexity)

        if not gen_result.success or not gen_result.output:
            # Check if it's a rate limit issue
            is_rate_limited = gen_result.rate_limited or (
                gen_result.error
                and any(
                    x in gen_result.error.lower()
                    for x in ["rate limit", "rate-limit", "unavailable", "429"]
                )
            )

            if is_rate_limited:
                print(
                    "  Generation failed due to rate limiting (not a bug)",
                    file=sys.stderr,
                )
                return IterationResult(
                    IterationStatus.SKIPPED,
                    skip_reason=f"Rate limited: {gen_result.error or 'All backends unavailable'}",
                )

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

        # Parse multi-file response
        files = extract_multifile_code(gen_result.output)
        if not files:
            print("  Failed to parse multi-file response", file=sys.stderr)
            # Save raw output for debugging prompt issues
            skip = Skip(
                timestamp=timestamp,
                skip_reason="Failed to parse multi-file response from AI",
                generated_code=gen_result.output,  # Raw output for debugging
                feature_focus=feature_focus,
                complexity=complexity,
                backend_used=gen_result.backend,
                generation_duration=gen_result.duration_seconds,
            )
            skip_dir = self.skip_reporter.report(skip)
            print(f"  Skip saved for inspection: {skip_dir.name}", file=sys.stderr)
            return IterationResult(
                IterationStatus.SKIPPED,
                skip_dir=skip_dir,
                skip_reason="Failed to parse multi-file response from AI",
            )

        print(f"  Generated {len(files)} files:", file=sys.stderr)
        for filename in files:
            print(f"    - {filename}", file=sys.stderr)

        # Extract expected output from main.spy
        expected_output = extract_expected_output_from_multifile(files)

        # Step 1.5: Quick pre-validation for each file
        for filename, code in files.items():
            prevalidation_error = self._quick_prevalidate(code)
            if prevalidation_error:
                print(
                    f"  Pre-validation failed for {filename}: {prevalidation_error}",
                    file=sys.stderr,
                )
                # Save for inspection
                skip = Skip(
                    timestamp=timestamp,
                    skip_reason=f"Unsupported feature in {filename}: {prevalidation_error}",
                    generated_code=files.get("main.spy", ""),
                    expected_output=expected_output,
                    feature_focus=feature_focus,
                    complexity=complexity,
                    backend_used=gen_result.backend,
                    generation_duration=gen_result.duration_seconds,
                    source_files=files,
                )
                skip_dir = self.skip_reporter.report(skip)
                print(f"  Skip saved for inspection: {skip_dir.name}", file=sys.stderr)
                return IterationResult(
                    IterationStatus.SKIPPED,
                    skip_dir=skip_dir,
                    skip_reason=f"Unsupported feature in {filename}: {prevalidation_error}",
                )

        # Step 2: Validate each file against spec
        print("\n[2/4] Validating against spec...", file=sys.stderr)
        for filename, code in files.items():
            val_result = await self._validate_code(code)
            if not val_result.success:
                print(
                    f"  Validation failed for {filename}: {val_result.error}",
                    file=sys.stderr,
                )
                # Save for inspection
                skip = Skip(
                    timestamp=timestamp,
                    skip_reason=f"Validation backend error for {filename}",
                    generated_code=files.get("main.spy", ""),
                    expected_output=expected_output,
                    feature_focus=feature_focus,
                    complexity=complexity,
                    backend_used=gen_result.backend,
                    generation_duration=gen_result.duration_seconds,
                    source_files=files,
                    validation_output=val_result.error,
                )
                skip_dir = self.skip_reporter.report(skip)
                print(f"  Skip saved for inspection: {skip_dir.name}", file=sys.stderr)
                return IterationResult(
                    IterationStatus.SKIPPED,
                    skip_dir=skip_dir,
                    skip_reason=f"Validation backend error for {filename}",
                )

            if "INVALID" in val_result.output.upper():
                print(f"  {filename} is invalid per spec, skipping", file=sys.stderr)
                # Save for inspection
                skip = Skip(
                    timestamp=timestamp,
                    skip_reason=f"{filename} invalid per spec",
                    generated_code=files.get("main.spy", ""),
                    expected_output=expected_output,
                    feature_focus=feature_focus,
                    complexity=complexity,
                    backend_used=gen_result.backend,
                    generation_duration=gen_result.duration_seconds,
                    source_files=files,
                    validation_output=val_result.output,
                )
                skip_dir = self.skip_reporter.report(skip)
                print(f"  Skip saved for inspection: {skip_dir.name}", file=sys.stderr)
                return IterationResult(
                    IterationStatus.SKIPPED,
                    skip_dir=skip_dir,
                    skip_reason=f"{filename} invalid per spec",
                )

        print("  All files validated successfully", file=sys.stderr)

        # Step 3: Compile and run multi-file project
        print("\n[3/4] Compiling and running project...", file=sys.stderr)
        with TempProjectDir(files) as project_dir:
            run_result = await self.compiler.run_project(
                project_dir, timeout=self.config.execution_timeout
            )

            if not run_result.success:
                issue_type = (
                    IssueType.TIMEOUT
                    if run_result.timed_out
                    else IssueType.COMPILATION_FAILED
                )
                if run_result.exit_code != 0 and not run_result.timed_out:
                    if "error" in (run_result.error or "").lower():
                        if "CS" in (run_result.error or ""):
                            issue_type = IssueType.COMPILATION_FAILED
                        else:
                            issue_type = IssueType.EXECUTION_FAILED

                print(
                    f"  ✗ {issue_type.value}: {run_result.error[:200] if run_result.error else 'Unknown error'}",
                    file=sys.stderr,
                )

                issue = Issue(
                    issue_type=issue_type,
                    timestamp=timestamp,
                    generated_code=files.get("main.spy", ""),
                    source_files=files,
                    expected_output=expected_output,
                    error_message=run_result.error,
                    compiler_output=run_result.output,
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
        execution_duration = run_result.duration_seconds
        print(
            f"  Execution successful, got {len(actual_output)} chars output",
            file=sys.stderr,
        )

        # Step 4: Verify output
        print("\n[4/4] Verifying output...", file=sys.stderr)
        if expected_output:
            expected_normalized = expected_output.strip()
            actual_normalized = actual_output.strip()

            if expected_normalized == actual_normalized:
                # Exact match - print status
                print("  ✓ Output matches expected (exact)", file=sys.stderr)
            elif _outputs_equivalent(expected_normalized, actual_normalized):
                # Float-tolerant match
                print(
                    "  ✓ Output matches expected (with float tolerance)",
                    file=sys.stderr,
                )
            else:
                verify_result = await self._verify_output(
                    files.get("main.spy", ""), expected_output, actual_output
                )
                if "MISMATCH" in verify_result.output.upper():
                    print("  ✗ Output mismatch detected", file=sys.stderr)
                    issue = Issue(
                        issue_type=IssueType.OUTPUT_MISMATCH,
                        timestamp=timestamp,
                        generated_code=files.get("main.spy", ""),
                        source_files=files,
                        expected_output=expected_output,
                        actual_output=actual_output,
                        feature_focus=feature_focus,
                        complexity=complexity,
                        backend_used=gen_result.backend,
                        generation_duration=gen_result.duration_seconds,
                        execution_duration=run_result.duration_seconds,
                    )
                    issue_dir = self.issue_reporter.report(issue)
                    return IterationResult(IterationStatus.FAILED, issue_dir)
                else:
                    print("  ✓ Output matches expected (AI verified)", file=sys.stderr)
        else:
            print(
                "  No expected output specified, skipping verification", file=sys.stderr
            )

        # Report successful iteration
        success_dir = None
        if expected_output:
            success = Success(
                timestamp=timestamp,
                generated_code=files.get("main.spy", ""),
                source_files=files,
                expected_output=expected_output,
                actual_output=actual_output,
                feature_focus=feature_focus,
                complexity=complexity,
                backend_used=gen_result.backend,
                generation_duration=gen_result.duration_seconds,
                execution_duration=execution_duration,
            )
            success_dir = self.success_reporter.report(success)
            print(f"  Success saved: {success_dir.name}", file=sys.stderr)

        print("\n✓ Multi-file iteration completed successfully!", file=sys.stderr)
        return IterationResult(IterationStatus.SUCCESS, success_dir=success_dir)

    def _quick_prevalidate(self, code: str) -> Optional[str]:
        """Quick programmatic check for forbidden features.

        Returns None if code passes, or an error message if it fails.
        This catches obvious issues before expensive AI validation.

        Validates against phases 0.1.0-0.1.10 (excludes v0.1.11+ features).
        """
        import re

        # Patterns that indicate features beyond phases 0.1.0-0.1.10
        # Note: Classes, structs, interfaces, enums, imports, decorators,
        # nullable types, default params, keyword args ARE allowed now
        forbidden_checks = [
            # String features not yet supported
            (r'f"[^"]*\{', "f-string interpolation (v0.1.11)"),
            (r"f'[^']*\{", "f-string interpolation (v0.1.11)"),
            # Collections (v0.1.11)
            (r":\s*list\[", "list type annotation (v0.1.11)"),
            (r":\s*dict\[", "dict type annotation (v0.1.11)"),
            (r":\s*set\[", "set type annotation (v0.1.11)"),
            (r":\s*Optional\[", "Optional type - use T? instead"),
            (r"\[\s*\]", "empty list literal (v0.1.11)"),
            (r"\{\s*\}", "empty dict/set literal (v0.1.11)"),
            (r"\[\s*\w+.*for\s+\w+\s+in", "list comprehension (v0.1.11)"),
            (r"\{[^}]*for\s+\w+\s+in", "dict/set comprehension (v0.1.11)"),
            # Exception handling (v0.1.13)
            (r"\btry\s*:", "try block (v0.1.13)"),
            (r"\bexcept\s*", "except block (v0.1.13)"),
            (r"\braise\s+", "raise statement (v0.1.13)"),
            # Lambdas (v0.1.14)
            (r"\blambda\s*[^:]*:", "lambda expression (v0.1.14)"),
            # Async/await (deferred)
            (r"\basync\s+def", "async function (deferred)"),
            (r"\bawait\s+", "await expression (deferred)"),
            # Context managers (deferred)
            (r"\bwith\s+", "with statement (deferred)"),
            # Tuple unpacking
            (r"\w+\s*,\s*\w+\s*=", "tuple unpacking (not supported)"),
            # Ternary expression
            (r"\bx\s+if\s+.+\s+else\s+", "ternary expression (not supported)"),
            # .NET interop imports (v0.1.12)
            (r"\bfrom\s+system\s+import", ".NET interop import (v0.1.12)"),
            (r"\bfrom\s+System\s+import", ".NET interop import (v0.1.12)"),
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

        # Multi-file iteration settings
        # Run ~20% of iterations as multi-file tests
        multifile_probability = 0.2
        multifile_features = ["module_imports", "cross_module_classes", "module_utils"]

        for i in range(1, max_iterations + 1):
            # Decide if this should be a multi-file iteration
            is_multifile = random.random() < multifile_probability

            if is_multifile:
                # Multi-file iteration
                feature_focus = random.choice(multifile_features)
                complexity = random.choice(
                    ["medium", "complex"]
                )  # Multi-file is at least medium
            else:
                # Regular single-file iteration
                feature_focus = random.choice(FEATURE_FOCUSES)
                complexity = random.choice(COMPLEXITY_LEVELS)

            start_time = datetime.now()
            try:
                if is_multifile:
                    result = await self.run_multifile_iteration(
                        i, feature_focus, complexity
                    )
                else:
                    result = await self.run_iteration(i, feature_focus, complexity)
                duration = (datetime.now() - start_time).total_seconds()

                if result.status == IterationStatus.SUCCESS:
                    successful += 1
                    self.summary_reporter.add_run(
                        i,
                        feature_focus,
                        complexity,
                        success=True,
                        success_dir=result.success_dir,
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
                        skip_dir=result.skip_dir,
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
