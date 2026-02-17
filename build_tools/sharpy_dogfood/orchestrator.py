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
    get_code_generation_prompt,
    get_multifile_generation_prompt,
    get_multifile_regeneration_prompt,
    get_regeneration_prompt,
    get_test_uniqueness_prompt,
    extract_expected_output,
    extract_code_block,
    extract_multifile_code,
    extract_expected_output_from_multifile,
    load_test_fixtures,
    format_fixtures_for_prompt,
)
from .convert import convert_dogfood_to_test, get_category_from_feature
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


_INTERNAL_COMPILER_ERROR_PATTERN = re.compile(r"SPY09\d{2}")


def _is_internal_compiler_error(error_message: str) -> bool:
    """Check if an error message contains an internal compiler error (SPY09xx).

    SPY09xx errors are infrastructure/compiler-level errors, not user code errors.
    In dogfood context, these should not be retried since the generated code may
    be perfectly valid.
    """
    return bool(_INTERNAL_COMPILER_ERROR_PATTERN.search(error_message))


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
    is_internal_compiler_error: bool = False


@dataclass
class MultifileGenerationResult:
    """Result of the multi-file generate-and-validate process with retry loop."""

    success: bool
    files: Optional[dict[str, str]] = None
    expected_output: Optional[str] = None
    skip_reason: Optional[str] = None
    backend_used: Optional[str] = None
    generation_duration: Optional[float] = None
    rate_limited: bool = False
    attempts: int = 1
    is_internal_compiler_error: bool = False


_NUMBER_PATTERN = re.compile(r"-?\d+\.?\d*")
_PURE_NUMBER_PATTERN = re.compile(r"^-?\d+\.?\d*$")


def _numbers_close(a: str, b: str, rel_tol: float) -> bool:
    """Compare two numeric strings with tolerance, respecting decimal point presence.

    Returns True only if:
    - Both have a decimal point (or both don't), AND
    - The numeric values are within relative tolerance.

    "22.0" vs "22" → False (decimal point presence differs).
    "3.14" vs "3.140000000000001" → True (float tolerance, both have ".").
    """
    a_has_dot = "." in a
    b_has_dot = "." in b
    if a_has_dot != b_has_dot:
        return False  # Decimal presence differs — format mismatch
    try:
        a_val = float(a)
        b_val = float(b)
        if a_val == 0:
            return abs(b_val) <= rel_tol
        return abs((a_val - b_val) / a_val) <= rel_tol
    except ValueError:
        return False


def _outputs_equivalent(expected: str, actual: str, rel_tol: float = 1e-9) -> bool:
    """
    Compare two outputs line by line, allowing floating-point tolerance.

    Returns True if outputs are equivalent considering:
    - Exact string matches
    - Pure-number lines: floating-point tolerance (but decimal point presence must match)
    - Embedded numbers: extract all numeric tokens, verify non-numeric text is identical,
      then compare numeric tokens with tolerance (decimal point presence must match)
    """
    expected_lines = expected.strip().split("\n")
    actual_lines = actual.strip().split("\n")

    if len(expected_lines) != len(actual_lines):
        return False

    for exp_line, act_line in zip(expected_lines, actual_lines):
        exp_line = exp_line.strip()
        act_line = act_line.strip()

        if exp_line == act_line:
            continue

        # Try pure-number comparison first
        if _PURE_NUMBER_PATTERN.match(exp_line) and _PURE_NUMBER_PATTERN.match(act_line):
            if _numbers_close(exp_line, act_line, rel_tol):
                continue
            return False

        # Try embedded number comparison
        exp_nums = _NUMBER_PATTERN.findall(exp_line)
        act_nums = _NUMBER_PATTERN.findall(act_line)

        if exp_nums and act_nums and len(exp_nums) == len(act_nums):
            # Verify non-numeric text is identical
            exp_text = _NUMBER_PATTERN.sub("\x00", exp_line)
            act_text = _NUMBER_PATTERN.sub("\x00", act_line)
            if exp_text == act_text:
                # Compare each numeric token
                all_close = all(
                    _numbers_close(e, a, rel_tol)
                    for e, a in zip(exp_nums, act_nums)
                )
                if all_close:
                    continue

        # Not equal and not matching via any strategy
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

    # Skip verification for Sharpy-specific features that Python can't run
    sharpy_only_features = [
        "struct ",
        "interface ",
        "@abstract",
        "@virtual",
        "@override",
        "@final",
        "@private",
        "@protected",
        "@internal",
        "enum ",
        "Some(",
        "None()",
        "Ok(",
        "Err(",
        "maybe ",
        "int?",
        "str?",
        "float?",
        "bool?",
    ]
    if any(feature in code for feature in sharpy_only_features):
        return True, None, "Sharpy-specific features - skipping Python verification"

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
    has_main = "def main(" in sharpy_code or "def main():" in sharpy_code

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

    # Add main() call at the end if main function exists
    # Python doesn't auto-invoke main() like Sharpy does
    if has_main:
        lines.append("")
        lines.append("main()")

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


def _replace_expected_output_in_code(code: str, new_output: str) -> str:
    """Replace the expected output comment block in code with new output.

    Finds the '# EXPECTED OUTPUT:' (or '# Expected output:') comment block
    and replaces the commented output lines with the new output.
    """
    # Match the expected output header and all subsequent comment lines
    pattern = r"#\s*(?:EXPECTED|Expected)\s*(?:OUTPUT|output):?\s*\n(?:#\s*.*\n?)+"
    replacement_lines = [f"# EXPECTED OUTPUT:"]
    for line in new_output.strip().split("\n"):
        replacement_lines.append(f"# {line}")
    replacement = "\n".join(replacement_lines) + "\n"

    result = re.sub(pattern, replacement, code)
    return result


# Feature focuses for code generation - matched to phases 0.1.0-0.1.18
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
    # Phase 0.1.11: F-Strings & Collections
    "f_string_basic",  # f"Hello {name}"
    "f_string_expressions",  # f"Result: {x + y}"
    "list_literal",  # [1, 2, 3]
    "dict_literal",  # {"key": value}
    "set_literal",  # {1, 2, 3}
    "list_comprehension",  # [x * 2 for x in range(10)]
    "dict_comprehension",  # {k: v for k, v in items}
    "set_comprehension",  # {x for x in items}
    "collection_iteration",  # for item in collection
    "collection_methods",  # .add(), .remove(), len()
    # Phase 0.1.12: .NET Interop
    "dotnet_import",  # from system import Console
    "dotnet_type_usage",  # using .NET types
    # Phase 0.1.13: Exception Handling
    "try_except_basic",  # try/except
    "try_except_finally",  # try/except/finally
    "try_except_else",  # try/except/else/finally
    "raise_exception",  # raise ValueError()
    # Phase 0.1.14: Lambda Expressions
    "lambda_basic",  # lambda x: x * 2
    "lambda_multiarg",  # lambda a, b: a + b
    "higher_order_function",  # passing lambdas
    # Phase 0.1.15-0.1.18: Optional & Result Types
    "optional_type",  # T?, Some(x), None()
    "optional_unwrap",  # .unwrap(), .unwrap_or(), .map()
    "result_type",  # T !E, Ok(x), Err(e)
    "result_unwrap",  # .unwrap(), .unwrap_or(), .map()
    "maybe_expression",  # maybe expr — T | None → T?
    "try_expression",  # try expr — wraps in Result[T, E]
    "lambda_type_inference",  # lambda params inferred from context
    # Dunder Methods
    "dunder_str",  # __str__() method
    "dunder_eq_hash",  # __eq__() + __hash__() pair
    "dunder_bool",  # __bool__() for truthiness
    "dunder_len",  # __len__() + len() builtin
    "dunder_iter",  # __iter__() + __next__() iterator protocol
    "dunder_operators",  # __add__(), __sub__(), __mul__(), __div__(), __mod__(), plus bitwise
    "dunder_comparison",  # __lt__(), __le__(), __gt__(), __ge__(), __ne__()
    "dunder_unary",  # __neg__(), __pos__(), __invert__()
    # Additional Builtins
    "builtin_conversions",  # int(), float(), bool(), str()
    "builtin_aggregation",  # min(), max(), sum()
    "builtin_higher_order",  # sorted(), filter(), map(), enumerate(), zip()
    # Containment & Tuple Types
    "containment_test",  # x in collection, x not in collection
    "tuple_types",  # tuple[int, str], tuple unpacking in for loops
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

    def __init__(self, config: Config, auto_convert: bool = True):
        self.config = config
        self.auto_convert = auto_convert
        self.backend_manager = BackendManager(config)
        self.compiler = SharpyCompiler(config.project_root, config.sharpy_cli_project)
        self.issue_reporter = IssueReporter(config.issues_dir)
        self.success_reporter = SuccessReporter(config.successes_dir)
        self.skip_reporter = SkipReporter(config.skips_dir)
        self.summary_reporter = SummaryReporter(config.output_dir)
        self.example_snippets: list[str] = []
        self.test_fixtures: dict[str, list[tuple[str, str]]] = {}
        self.fixtures_prompt_section: str = ""
        self.auto_converted_count: int = 0

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

        Filters to only include snippets that use features from phases 0.1.0-0.1.18.
        Now includes f-strings, collections, comprehensions, exceptions, lambdas, .NET interop.
        """
        snippets_dir = self.config.snippets_dir
        if not snippets_dir.exists():
            return

        # Features that indicate code is beyond phases 0.1.0-0.1.18
        # (only features NOT yet implemented)
        forbidden_patterns = [
            "async def",  # Not implemented
            "await ",  # Not implemented
            " with ",  # Context managers not implemented
            ":=",  # Walrus operator not implemented
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

            # Check for internal compiler errors (SPY09xx) — report immediately
            if gen_result.is_internal_compiler_error:
                print(
                    "  Internal compiler error (SPY09xx) — reporting as compiler bug",
                    file=sys.stderr,
                )
                issue = Issue(
                    issue_type=IssueType.INTERNAL_COMPILER_ERROR,
                    timestamp=timestamp,
                    generated_code=gen_result.code or "",
                    expected_output=gen_result.expected_output,
                    error_message=gen_result.skip_reason,
                    feature_focus=feature_focus,
                    complexity=complexity,
                    backend_used=gen_result.backend_used,
                    generation_duration=gen_result.generation_duration,
                )
                issue_dir = self.issue_reporter.report(issue)
                print(f"  Issue reported: {issue_dir.name}", file=sys.stderr)
                return IterationResult(IterationStatus.FAILED, issue_dir)

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
                verify_upper = verify_result.output.upper()
                if not verify_result.success:
                    reason = "AI verification backend failure"
                    is_mismatch = True
                elif "MISMATCH" in verify_upper:
                    reason = "AI explicitly reported mismatch"
                    is_mismatch = True
                elif "MATCH" not in verify_upper:
                    reason = "AI verification response was ambiguous or empty"
                    is_mismatch = True
                else:
                    is_mismatch = False

                if is_mismatch:
                    print(f"  ✗ Output mismatch detected ({reason})", file=sys.stderr)
                    issue = Issue(
                        issue_type=IssueType.OUTPUT_MISMATCH,
                        timestamp=timestamp,
                        generated_code=code,
                        expected_output=expected_output,
                        actual_output=actual_output,
                        error_message=reason,
                        feature_focus=feature_focus,
                        complexity=complexity,
                        backend_used=gen_result.backend_used,
                        generation_duration=gen_result.generation_duration,
                        execution_duration=run_result.duration_seconds,
                    )
                    issue_dir = self.issue_reporter.report(issue)
                    print(f"  Issue reported: {issue_dir.name}", file=sys.stderr)
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

            # Auto-convert to test fixture if enabled
            if self.auto_convert and success_dir:
                await self._auto_convert_if_unique(success_dir, code, feature_focus)

        print("\n✓ Iteration completed successfully!", file=sys.stderr)
        return IterationResult(IterationStatus.SUCCESS, success_dir=success_dir)

    async def _generate_code(self, feature_focus: str, complexity: str) -> AIResult:
        """Generate Sharpy code using AI."""
        prompt = get_code_generation_prompt(
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
            prevalidation_error = await self._quick_prevalidate(code)
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

            # Step 1.55: Validate Sharpy semantics (full pipeline: lexer → parser → semantic → codegen)
            semantic_error = await self._validate_sharpy_semantics(code)
            if semantic_error:
                # Check for internal compiler errors (SPY09xx) — don't retry these
                if _is_internal_compiler_error(semantic_error):
                    print(
                        f"  Internal compiler error detected (SPY09xx): {semantic_error[:200]}",
                        file=sys.stderr,
                    )
                    return GenerationResult(
                        success=False,
                        code=code,
                        expected_output=extract_expected_output(code),
                        skip_reason=f"Internal compiler error: {semantic_error}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                        is_internal_compiler_error=True,
                    )

                print(
                    f"  Sharpy semantic validation failed: {semantic_error[:200]}",
                    file=sys.stderr,
                )
                last_error = f"Sharpy compiler error: {semantic_error}"
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
                        skip_reason=f"Sharpy compiler error after {attempt} attempts: {semantic_error}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                    )

            # Compiler validation passed — code is semantically valid.
            # Skip AI spec validation (emit csharp runs the full pipeline).

            # Step 1.6: Verify expected output using Python
            expected_output = extract_expected_output(code)
            if expected_output:
                is_valid, python_output, verify_error = (
                    await _verify_expected_with_python(code, expected_output)
                )
                if not is_valid and python_output is not None:
                    # Python ran successfully but output differs from LLM's expected output.
                    # Adopt Python's output as authoritative.
                    print(
                        f"  Expected output corrected by Python (was: '{expected_output[:30]}...', "
                        f"now: '{python_output[:30]}...')",
                        file=sys.stderr,
                    )
                    expected_output = python_output
                    # Update the expected output comment block in the code
                    code = _replace_expected_output_in_code(code, python_output)
                    last_code = code
                elif not is_valid:
                    # Python couldn't run the code — keep LLM's expected output
                    pass
                elif python_output is not None:
                    print(
                        f"  Expected output verified with Python: {python_output[:50]}...",
                        file=sys.stderr,
                    )

            # Success!
            print("  Code validated successfully (compiler + Python)", file=sys.stderr)
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

    async def _regenerate_multifile_code(
        self,
        feature_focus: str,
        complexity: str,
        previous_files: dict[str, str],
        validation_error: str,
        attempt: int,
    ) -> AIResult:
        """Regenerate multi-file Sharpy code with feedback from validation failure."""
        prompt = get_multifile_regeneration_prompt(
            feature_focus=feature_focus,
            complexity=complexity,
            previous_files=previous_files,
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

    async def _generate_and_validate_multifile_code(
        self, feature_focus: str, complexity: str
    ) -> MultifileGenerationResult:
        """Generate and validate multi-file code with retry loop on validation failure.

        Implements a feedback loop for multi-file projects. If code fails parsing,
        pre-validation, or semantic validation, retries up to max_regeneration_attempts
        times with feedback about the validation error.

        Returns:
            MultifileGenerationResult with files if successful, or skip_reason if all
            attempts failed.
        """
        max_attempts = self.config.max_regeneration_attempts
        last_files: Optional[dict[str, str]] = None
        last_error: Optional[str] = None
        total_duration = 0.0
        backend_used: Optional[str] = None

        for attempt in range(1, max_attempts + 1):
            # Generate or regenerate code
            if attempt == 1:
                # First attempt: generate fresh code
                gen_result = await self._generate_multifile_code(
                    feature_focus, complexity
                )
            else:
                # Subsequent attempts: regenerate with feedback
                print(
                    f"  Regenerating multi-file code (attempt {attempt}/{max_attempts})...",
                    file=sys.stderr,
                )
                gen_result = await self._regenerate_multifile_code(
                    feature_focus=feature_focus,
                    complexity=complexity,
                    previous_files=last_files,
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
                    return MultifileGenerationResult(
                        success=False,
                        skip_reason=f"Rate limited: {gen_result.error or 'All backends unavailable'}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        rate_limited=True,
                        attempts=attempt,
                    )
                # Non-rate-limit generation failure
                return MultifileGenerationResult(
                    success=False,
                    skip_reason=gen_result.error or "No code generated",
                    backend_used=backend_used,
                    generation_duration=total_duration,
                    attempts=attempt,
                )

            # Parse multi-file response
            files = extract_multifile_code(gen_result.output)
            if not files:
                print("  Failed to parse multi-file response", file=sys.stderr)
                last_error = "Failed to parse multi-file response. Ensure each file starts with '=== FILE: filename.spy ===' and includes a main.spy file."
                # Keep last_files from previous attempt if available
                if attempt < max_attempts:
                    print(
                        f"  Will retry with feedback ({attempt}/{max_attempts})...",
                        file=sys.stderr,
                    )
                    continue
                else:
                    return MultifileGenerationResult(
                        success=False,
                        skip_reason=f"Failed to parse multi-file response after {attempt} attempts",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                    )

            last_files = files
            print(f"  Generated {len(files)} files:", file=sys.stderr)
            for filename in files:
                print(f"    - {filename}", file=sys.stderr)

            # Pre-validate each file
            prevalidation_failed = False
            for filename, code in files.items():
                prevalidation_error = await self._quick_prevalidate(code)
                if prevalidation_error:
                    print(
                        f"  Pre-validation failed for {filename}: {prevalidation_error}",
                        file=sys.stderr,
                    )
                    last_error = (
                        f"Pre-validation error in {filename}: {prevalidation_error}"
                    )
                    prevalidation_failed = True
                    break

            if prevalidation_failed:
                if attempt < max_attempts:
                    print(
                        f"  Will retry with feedback ({attempt}/{max_attempts})...",
                        file=sys.stderr,
                    )
                    continue
                else:
                    return MultifileGenerationResult(
                        success=False,
                        files=files,
                        expected_output=extract_expected_output_from_multifile(files),
                        skip_reason=f"Pre-validation failed after {attempt} attempts: {last_error}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                    )

            # Semantic validate project
            semantic_error = await self._validate_project_semantics(files)
            if semantic_error:
                # Check for internal compiler errors (SPY09xx) — don't retry these
                if _is_internal_compiler_error(semantic_error):
                    print(
                        f"  Internal compiler error detected (SPY09xx): {semantic_error[:200]}",
                        file=sys.stderr,
                    )
                    return MultifileGenerationResult(
                        success=False,
                        files=files,
                        expected_output=extract_expected_output_from_multifile(files),
                        skip_reason=f"Internal compiler error: {semantic_error}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                        is_internal_compiler_error=True,
                    )

                print(
                    f"  Sharpy project validation failed: {semantic_error[:200]}",
                    file=sys.stderr,
                )
                last_error = f"Sharpy compiler error: {semantic_error}"
                if attempt < max_attempts:
                    print(
                        f"  Will retry with feedback ({attempt}/{max_attempts})...",
                        file=sys.stderr,
                    )
                    continue
                else:
                    return MultifileGenerationResult(
                        success=False,
                        files=files,
                        expected_output=extract_expected_output_from_multifile(files),
                        skip_reason=f"Sharpy compiler error after {attempt} attempts: {semantic_error}",
                        backend_used=backend_used,
                        generation_duration=total_duration,
                        attempts=attempt,
                    )

            # All validation passed
            expected_output = extract_expected_output_from_multifile(files)
            print("  All files validated successfully (compiler)", file=sys.stderr)
            return MultifileGenerationResult(
                success=True,
                files=files,
                expected_output=expected_output,
                backend_used=backend_used,
                generation_duration=total_duration,
                attempts=attempt,
            )

        # Should not reach here, but just in case
        return MultifileGenerationResult(
            success=False,
            files=last_files,
            expected_output=(
                extract_expected_output_from_multifile(last_files)
                if last_files
                else None
            ),
            skip_reason="Multi-file generation failed after all retry attempts",
            backend_used=backend_used,
            generation_duration=total_duration,
            attempts=max_attempts,
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

        # Step 1 & 2: Generate and validate multi-file code (with retry loop)
        print("\n[1/4] Generating multi-file project...", file=sys.stderr)
        gen_result = await self._generate_and_validate_multifile_code(
            feature_focus, complexity
        )

        if not gen_result.success:
            # Check if it's a rate limit issue
            if gen_result.rate_limited:
                print(
                    "  Generation failed due to rate limiting (not a bug)",
                    file=sys.stderr,
                )
                return IterationResult(
                    IterationStatus.SKIPPED,
                    skip_reason=gen_result.skip_reason,
                )

            # Check for internal compiler errors (SPY09xx) — report immediately
            if gen_result.is_internal_compiler_error:
                print(
                    "  Internal compiler error (SPY09xx) — reporting as compiler bug",
                    file=sys.stderr,
                )
                issue = Issue(
                    issue_type=IssueType.INTERNAL_COMPILER_ERROR,
                    timestamp=timestamp,
                    generated_code=gen_result.files.get("main.spy", "") if gen_result.files else "",
                    source_files=gen_result.files,
                    expected_output=gen_result.expected_output,
                    error_message=gen_result.skip_reason,
                    feature_focus=feature_focus,
                    complexity=complexity,
                    backend_used=gen_result.backend_used,
                    generation_duration=gen_result.generation_duration,
                )
                issue_dir = self.issue_reporter.report(issue)
                print(f"  Issue reported: {issue_dir.name}", file=sys.stderr)
                return IterationResult(IterationStatus.FAILED, issue_dir)

            # If it was a validation failure after retries, skip but save for inspection
            if gen_result.attempts > 1:
                print(
                    f"  Multi-file code generation failed after {gen_result.attempts} attempts",
                    file=sys.stderr,
                )
                skip_dir = None
                if gen_result.files:
                    skip = Skip(
                        timestamp=timestamp,
                        skip_reason=gen_result.skip_reason or "Validation failed",
                        generated_code=gen_result.files.get("main.spy", ""),
                        expected_output=gen_result.expected_output,
                        feature_focus=feature_focus,
                        complexity=complexity,
                        backend_used=gen_result.backend_used,
                        generation_duration=gen_result.generation_duration,
                        source_files=gen_result.files,
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

        files = gen_result.files
        expected_output = gen_result.expected_output
        if gen_result.attempts > 1:
            print(
                f"  Multi-file code validated successfully after {gen_result.attempts} attempts",
                file=sys.stderr,
            )

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
                print(
                    "  \u26a0 Multi-file test: output verified by AI only (no Python baseline)",
                    file=sys.stderr,
                )
                verify_result = await self._verify_output(
                    files.get("main.spy", ""), expected_output, actual_output
                )
                verify_upper = verify_result.output.upper()
                if not verify_result.success:
                    reason = "AI verification backend failure"
                    is_mismatch = True
                elif "MISMATCH" in verify_upper:
                    reason = "AI explicitly reported mismatch"
                    is_mismatch = True
                elif "MATCH" not in verify_upper:
                    reason = "AI verification response was ambiguous or empty"
                    is_mismatch = True
                else:
                    is_mismatch = False

                if is_mismatch:
                    print(f"  ✗ Output mismatch detected ({reason})", file=sys.stderr)
                    issue = Issue(
                        issue_type=IssueType.OUTPUT_MISMATCH,
                        timestamp=timestamp,
                        generated_code=files.get("main.spy", ""),
                        source_files=files,
                        expected_output=expected_output,
                        actual_output=actual_output,
                        error_message=reason,
                        feature_focus=feature_focus,
                        complexity=complexity,
                        backend_used=gen_result.backend_used,
                        generation_duration=gen_result.generation_duration,
                        execution_duration=run_result.duration_seconds,
                    )
                    issue_dir = self.issue_reporter.report(issue)
                    print(f"  Issue reported: {issue_dir.name}", file=sys.stderr)
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
                backend_used=gen_result.backend_used,
                generation_duration=gen_result.generation_duration,
                execution_duration=execution_duration,
            )
            success_dir = self.success_reporter.report(success)
            print(f"  Success saved: {success_dir.name}", file=sys.stderr)

        print("\n✓ Multi-file iteration completed successfully!", file=sys.stderr)
        return IterationResult(IterationStatus.SUCCESS, success_dir=success_dir)

    async def _quick_prevalidate(self, code: str) -> Optional[str]:
        """Quick programmatic check for forbidden features.

        Returns None if code passes, or an error message if it fails.
        This catches obvious issues before expensive AI validation.

        Uses the Sharpy lexer (emit tokens) to check for forbidden keyword tokens,
        which avoids false positives from keywords inside f-string text or string
        literals. Falls back to regex if the lexer fails.

        Validates against phases 0.1.0-0.1.18 (includes f-strings, collections,
        exception handling, lambdas, .NET interop, Optional/Result types,
        maybe/try expressions).
        """
        lines = code.split("\n")
        for i, line in enumerate(lines, 1):
            # Skip comments
            stripped = line.split("#")[0].strip()
            if not stripped:
                continue

            # Special check for multi-argument print (needs proper paren parsing)
            if _has_multi_arg_print(stripped):
                return f"Line {i}: multi-argument print (use multiple print() calls) - '{stripped[:50]}...'"

        # Try lexer-based token checking
        token_result = await self._check_forbidden_tokens_via_lexer(code)
        if token_result is not None:
            return token_result

        # Check for @abstract + @virtual combination (decorators on consecutive lines)
        for i, line in enumerate(lines):
            stripped = line.split("#")[0].strip()
            if stripped in ("@abstract", "@virtual"):
                # Look at next non-empty line
                for j in range(i + 1, min(i + 4, len(lines))):
                    next_stripped = lines[j].split("#")[0].strip()
                    if not next_stripped:
                        continue
                    if (
                        next_stripped in ("@abstract", "@virtual")
                        and next_stripped != stripped
                    ):
                        return f"Line {j + 1}: @abstract and @virtual combined (abstract methods are inherently virtual — use only @abstract)"
                    break  # Stop at first non-empty, non-decorator line

        # Regex-only checks for things the lexer doesn't cover as keyword tokens
        forbidden_regex_checks = [
            # Walrus operator (not implemented) - operator token, not keyword
            (r":=", "walrus operator (not implemented)"),
            # Tuple unpacking (may have issues)
            # Anchored to line start to avoid matching keyword arguments
            (r"^\w+\s*,\s*\w+\s*=[^=]", "tuple unpacking (not fully supported)"),
        ]

        for i, line in enumerate(lines, 1):
            stripped = line.split("#")[0].strip()
            if not stripped:
                continue
            for pattern, description in forbidden_regex_checks:
                if re.search(pattern, stripped):
                    return f"Line {i}: {description} - '{stripped[:50]}...'"

        return None

    async def _check_forbidden_tokens_via_lexer(self, code: str) -> Optional[str]:
        """Use the Sharpy lexer to check for forbidden keyword tokens.

        Returns an error message if forbidden tokens are found, None if clean.
        Returns None (passes) if the lexer itself fails (falls back to allow).
        """
        import tempfile

        # Forbidden token types that indicate unimplemented features
        forbidden_tokens = {
            "With": "with statement (not implemented)",
            "Async": "async function (not implemented)",
            "Await": "await expression (not implemented)",
            "Match": "pattern matching (not implemented)",
        }

        try:
            # Write code to a temp file for the lexer
            with tempfile.NamedTemporaryFile(
                mode="w", suffix=".spy", delete=False
            ) as f:
                f.write(code)
                temp_path = Path(f.name)

            try:
                result = await self.compiler.emit_tokens(temp_path, timeout=15.0)
            finally:
                try:
                    temp_path.unlink()
                except Exception:
                    pass

            if not result.success:
                # Lexer failed (e.g., syntax error) — fall back to allowing
                return None

            # Parse token output lines: "   0: With                 @ L1:C1 = 'with'"
            token_pattern = re.compile(r"\d+:\s+(\w+)\s+@\s+L(\d+):C(\d+)")
            for line in result.output.split("\n"):
                m = token_pattern.search(line)
                if m:
                    token_type = m.group(1)
                    token_line = m.group(2)
                    if token_type in forbidden_tokens:
                        return f"Line {token_line}: {forbidden_tokens[token_type]}"

        except Exception:
            # Any unexpected error — fall back to allowing
            return None

        return None

    async def _validate_sharpy_semantics(self, code: str) -> Optional[str]:
        """Validate that code is semantically valid using the full Sharpy pipeline.

        Uses 'emit csharp' which runs lexer → parser → semantic → codegen.
        This catches type errors, unknown symbols, import errors, and validation
        errors — not just syntax errors.

        Returns None if code is valid, or an error message if invalid.
        """
        with TempSourceFile(code) as temp_path:
            result = await self.compiler.check_file(temp_path, timeout=30.0)
            if result.success:
                return None
            return result.error or "Unknown compiler error"

    async def _validate_project_semantics(self, files: dict[str, str]) -> Optional[str]:
        """Validate a multi-file project as a unit using the Sharpy compiler.

        Writes all files to a temp directory and runs 'emit csharp' on main.spy
        with cwd set to the project dir so imports resolve to sibling files.
        Only main.spy is treated as the entry point — library modules don't
        need main() and are validated transitively via imports.

        Returns None if the project is valid, or an error message if invalid.
        """
        with TempProjectDir(files) as project_dir:
            result = await self.compiler.check_project(project_dir, timeout=30.0)
            if result.success:
                return None
            return result.error or "Unknown compiler error"

    async def _verify_output(self, code: str, expected: str, actual: str) -> AIResult:
        """Verify output using AI for fuzzy comparison."""
        from .prompts import get_output_verification_prompt

        prompt = get_output_verification_prompt(code, expected, actual)
        return await self.backend_manager.execute(
            prompt, timeout=30.0  # Quick comparison
        )

    async def _auto_convert_if_unique(
        self,
        success_dir: Path,
        code: str,
        feature_focus: str,
    ) -> bool:
        """Auto-convert a successful test to a fixture if AI deems it unique.

        Args:
            success_dir: Path to the success output directory
            code: The generated Sharpy code
            feature_focus: The feature area that was tested

        Returns:
            True if the test was converted, False otherwise
        """
        category = get_category_from_feature(feature_focus)

        # Get existing tests in this category
        existing_tests = self.test_fixtures.get(category, [])

        # Ask AI to evaluate uniqueness
        prompt = get_test_uniqueness_prompt(code, existing_tests)
        result = await self.backend_manager.execute(prompt, timeout=30.0)

        if not result.success:
            print(
                f"  Auto-convert: AI check failed ({result.error}), skipping",
                file=sys.stderr,
            )
            return False

        if "DUPLICATE" in result.output.upper():
            print(
                f"  Auto-convert: Test is a duplicate, skipping",
                file=sys.stderr,
            )
            return False

        if "UNIQUE" not in result.output.upper():
            print(
                f"  Auto-convert: Unclear AI response, skipping",
                file=sys.stderr,
            )
            return False

        # Convert to test fixture
        test_fixtures_dir = self.config.test_fixtures_dir
        test_path = convert_dogfood_to_test(
            success_dir, test_fixtures_dir, category=category
        )

        if test_path:
            self.auto_converted_count += 1
            # Reload fixtures so subsequent uniqueness checks see the new test
            self._load_test_fixtures()
            print(
                f"  Auto-convert: Added test fixture {test_path.name}",
                file=sys.stderr,
            )
            return True
        else:
            print(
                f"  Auto-convert: Conversion failed",
                file=sys.stderr,
            )
            return False

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
        # Run ~50% of iterations as multi-file tests
        multifile_probability = 0.5
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
        if self.auto_convert:
            print(
                f"Auto-converted: {self.auto_converted_count} tests added",
                file=sys.stderr,
            )
        print(f"\nIssues saved to: {self.config.issues_dir}", file=sys.stderr)
        print(f"Summary: {self.config.output_dir / 'SUMMARY.md'}", file=sys.stderr)

        # Return success if no actual failures (skips don't count)
        return 0 if failed == 0 else 1
