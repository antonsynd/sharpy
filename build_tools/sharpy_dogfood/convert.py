"""
Convert dogfood output directories to integration test fixtures.

Converts successful (and optionally failed) dogfood outputs into the
file-based integration test format used by FileBasedIntegrationTests.
"""

import json
import re
import shutil
from pathlib import Path
from typing import Optional


def _extract_program_output(actual_output: str) -> str:
    """Extract just the program output from actual_output.txt.

    The actual_output.txt may contain lines like:
        Successfully compiled to: /path/to/exe
        === Running Program ===
        <actual program output here>

    Or it may just contain the raw program output (newer format).

    This function extracts only the program output portion.
    Note: The dogfood tool strips trailing whitespace from output, but
    Console.WriteLine actually produces a trailing newline, so we add it back.
    """
    lines = actual_output.split("\n")
    output_lines = []
    in_output = False

    for line in lines:
        if "=== Running Program ===" in line:
            in_output = True
            continue
        if in_output:
            output_lines.append(line)

    # If we found the marker, use the extracted lines
    if in_output:
        result = "\n".join(output_lines)
        # Remove leading empty line if present (from the blank line after "=== Running Program ===")
        if result.startswith("\n"):
            result = result[1:]
    else:
        # No marker found - the file contains raw output (newer format)
        result = actual_output

    # Add trailing newline since print() adds one but dogfood strips it
    if result and not result.endswith("\n"):
        result += "\n"
    return result


def sanitize_test_name(name: str) -> str:
    """Convert a directory name to a valid test name.

    Example: 20260117_001155_success_simple_class_0001 -> simple_class_0001
    """
    # Remove timestamp prefix (YYYYMMDD_HHMMSS_)
    name = re.sub(r"^\d{8}_\d{6}_", "", name)
    # Remove success/failure prefix
    name = re.sub(
        r"^(success|compilation_failed|execution_failed|output_mismatch)_", "", name
    )
    # Replace any remaining non-alphanumeric chars with underscore
    name = re.sub(r"[^a-zA-Z0-9_]", "_", name)
    # Remove consecutive underscores
    name = re.sub(r"_+", "_", name)
    # Remove leading/trailing underscores
    name = name.strip("_")
    return name or "unnamed_test"


def get_category_from_feature(feature_focus: Optional[str]) -> str:
    """Determine the test category based on feature focus."""
    if not feature_focus:
        return "misc"

    # Map feature focuses to categories
    category_map = {
        # Basics
        "integer_variables": "basics",
        "float_variables": "basics",
        "bool_variables": "basics",
        "arithmetic_operators": "basics",
        "comparison_operators": "basics",
        "logical_operators": "basics",
        "augmented_assignment": "basics",
        # Control flow
        "if_else_simple": "control_flow",
        "if_elif_else": "control_flow",
        "while_loop": "control_flow",
        "for_range_single": "control_flow",
        "for_range_start_end": "control_flow",
        "for_range_with_step": "control_flow",
        "break_continue": "control_flow",
        "nested_if_in_loop": "control_flow",
        # Functions
        "simple_function": "functions",
        "function_with_print": "functions",
        "function_calling_function": "functions",
        "function_default_params": "functions",
        "function_keyword_args": "functions",
        "loop_in_function": "functions",
        # Classes
        "simple_class": "classes",
        "class_with_init": "classes",
        "class_instance_methods": "classes",
        "class_static_methods": "classes",
        "class_field_access": "classes",
        "class_with_loop": "classes",
        # Inheritance
        "class_inheritance": "inheritance",
        "super_init_call": "inheritance",
        "abstract_class": "inheritance",
        "virtual_override": "inheritance",
        "inheritance_with_override": "inheritance",
        # Interfaces
        "interface_definition": "interfaces",
        "interface_implementation": "interfaces",
        "access_modifiers": "interfaces",
        # Structs & Enums
        "struct_definition": "structs_enums",
        "enum_definition": "structs_enums",
        "enum_usage": "structs_enums",
        # Type system
        "nullable_types": "type_system",
        "null_coalescing": "type_system",
        "null_conditional": "type_system",
        "type_narrowing": "type_system",
        "type_alias": "type_system",
        "generic_class": "type_system",
        "generic_function": "type_system",
        # Modules
        "import_statement": "modules",
        "from_import": "modules",
        # F-Strings & Collections
        "f_string_basic": "fstrings",
        "f_string_expressions": "fstrings",
        "list_literal": "collections",
        "dict_literal": "collections",
        "set_literal": "collections",
        "list_comprehension": "collections",
        "dict_comprehension": "collections",
        "set_comprehension": "collections",
        "collection_iteration": "collections",
        "collection_methods": "collections",
        # .NET Interop
        "dotnet_import": "dotnet_interop",
        "dotnet_type_usage": "dotnet_interop",
        # Exception Handling
        "try_except_basic": "exceptions",
        "try_except_finally": "exceptions",
        "try_except_else": "exceptions",
        "raise_exception": "exceptions",
        # Lambda Expressions
        "lambda_basic": "lambdas",
        "lambda_multiarg": "lambdas",
        "higher_order_function": "lambdas",
        "lambda_type_inference": "lambdas",
        # Optional & Result Types
        "optional_type": "optional_result",
        "optional_unwrap": "optional_result",
        "result_type": "optional_result",
        "result_unwrap": "optional_result",
        "maybe_expression": "optional_result",
        "try_expression": "optional_result",
        # Dunder Methods
        "dunder_str": "classes",
        "dunder_eq_hash": "classes",
        "dunder_bool": "classes",
        "dunder_len": "classes",
        "dunder_iter": "classes",
        "dunder_reversed": "classes",
        "dunder_operators": "classes",
        "dunder_comparison": "classes",
        "dunder_unary": "classes",
        # Additional Builtins
        "builtin_conversions": "builtins",
        "builtin_aggregation": "builtins",
        "builtin_higher_order": "builtins",
        # Containment & Tuple Types
        "containment_test": "collections",
        "tuple_types": "type_system",
        # Properties
        "auto_property": "properties",
        "function_style_property": "properties",
        "property_inheritance": "properties",
        "property_with_validation": "properties",
        # Tuple Unpacking & Star Patterns
        "tuple_unpacking_assignment": "expressions",
        "tuple_unpacking_nested": "expressions",
        "star_unpacking": "expressions",
        # Spread Operators
        "spread_list": "collections",
        "spread_dict": "collections",
        "spread_set": "collections",
        "spread_call": "expressions",
        "spread_with_comprehension": "collections",
        # Walrus Operator
        "walrus_operator": "expressions",
        # Pattern Matching
        "match_literal": "pattern_matching",
        "match_type_binding": "pattern_matching",
        "match_wildcard": "pattern_matching",
        "match_guard": "pattern_matching",
        "match_with_enum": "pattern_matching",
        # Context Managers
        "with_statement": "with_statement",
        # Named Tuples
        "named_tuple": "named_tuples",
        # Comparison Chaining
        "comparison_chaining": "type_system",
        # Generators & Yield
        "generator_basic": "generators",
        "generator_yield_from": "generators",
        "generator_early_return": "generators",
        "generator_iter_class": "generators",
        "generator_reversed_class": "generators",
        # Phase 0.2.0: Constructor Chaining & Enum Enhancements
        "constructor_chaining": "classes",
        "enum_name_value": "structs_enums",
        "enum_iteration": "structs_enums",
        "generic_type_alias": "type_system",
        "method_overloading": "classes",
        # Phase 0.2.2: Advanced Pattern Matching
        "match_expression": "pattern_matching",
        "match_or_pattern": "pattern_matching",
        "match_type_pattern": "pattern_matching",
        "match_relational_pattern": "pattern_matching",
        "match_property_pattern": "pattern_matching",
        "match_positional_pattern": "pattern_matching",
        "match_exhaustiveness": "pattern_matching",
        "match_union_exhaustive": "pattern_matching",
        # Phase 0.2.2: Tagged Unions
        "union_declaration": "unions",
        "union_matching": "unions",
        "union_generic": "unions",
        "union_with_generics": "unions",
        # Phase 0.2.4: Async/Await
        "async_function": "async",
        "await_expression": "async",
        "async_for": "async",
        "async_with": "async",
        "async_generator": "async",
        "asyncio_gather": "async",
        "async_with_generators": "async",
        # Phase 0.2.5: Advanced Function Features
        "positional_only_params": "functions",
        "keyword_only_params": "functions",
        "partial_application": "expressions",
        "operator_section": "expressions",
        # Phase 0.2.6: Delegates & Variance
        "delegate_declaration": "delegates",
        "delegate_with_lambda": "delegates",
        "generic_variance": "type_system",
        # Feature Combinations
        "overloading_with_inheritance": "classes",
    }

    return category_map.get(feature_focus, "misc")


def convert_dogfood_to_test(
    dogfood_dir: Path,
    test_fixtures_dir: Path,
    category: Optional[str] = None,
    test_name: Optional[str] = None,
    force: bool = False,
) -> Optional[Path]:
    """Convert a single dogfood output directory to a test fixture.

    Args:
        dogfood_dir: Path to the dogfood output directory (issues/ or successes/)
        test_fixtures_dir: Path to the TestFixtures directory
        category: Override category (subfolder) for the test
        test_name: Override name for the test files
        force: Overwrite existing test files

    Returns:
        Path to the created test file, or None if conversion failed
    """
    # Check required files
    source_file = dogfood_dir / "source.spy"
    actual_output_file = dogfood_dir / "actual_output.txt"
    expected_file = dogfood_dir / "expected_output.txt"
    metadata_file = dogfood_dir / "metadata.json"

    if not source_file.exists():
        print(f"  Error: {source_file} not found")
        return None

    # Load metadata if available
    metadata = {}
    if metadata_file.exists():
        try:
            metadata = json.loads(metadata_file.read_text())
        except json.JSONDecodeError:
            pass

    # Determine test name
    if not test_name:
        test_name = sanitize_test_name(dogfood_dir.name)

    # Determine category
    if not category:
        feature_focus = metadata.get("feature_focus")
        category = get_category_from_feature(feature_focus)

    # Create category directory
    category_dir = test_fixtures_dir / category
    category_dir.mkdir(parents=True, exist_ok=True)

    # Check if files already exist
    test_spy = category_dir / f"{test_name}.spy"
    test_expected = category_dir / f"{test_name}.expected"

    if test_spy.exists() and not force:
        print(f"  Error: {test_spy} already exists (use --force to overwrite)")
        return None

    # Read source code
    source_code = source_file.read_text()

    # Check if this is a failed compilation (error.txt exists and directory indicates failure)
    error_file = dogfood_dir / "error.txt"
    is_failure = error_file.exists() and any(
        x in dogfood_dir.name for x in ["_failed", "_mismatch", "_timeout"]
    )

    if is_failure:
        # This is a failed compilation - create an error test
        test_error = category_dir / f"{test_name}.error"
        error_content = error_file.read_text().strip()
        # Extract a meaningful error substring
        error_substring = _extract_error_substring(error_content)

        # Write files
        test_spy.write_text(source_code)
        test_error.write_text(error_substring)

        print(f"  Created error test: {test_spy.relative_to(test_fixtures_dir)}")
        return test_spy

    # Read expected output for success tests
    # Prefer actual_output.txt (compiler output) over expected_output.txt (AI-generated)
    # because actual_output.txt has the correct floating-point precision
    if actual_output_file.exists():
        expected_output = _extract_program_output(actual_output_file.read_text())
    elif expected_file.exists():
        expected_output = expected_file.read_text()
    else:
        print(
            f"  Error: No actual_output.txt or expected_output.txt found for success test"
        )
        return None

    # Write test files
    test_spy.write_text(source_code)
    test_expected.write_text(expected_output)

    print(f"  Created test: {test_spy.relative_to(test_fixtures_dir)}")
    return test_spy


def _extract_error_substring(error_content: str) -> str:
    """Extract a meaningful substring from an error message for matching."""
    lines = error_content.strip().split("\n")
    if not lines:
        return "error"

    # Try to find the most specific error line
    for line in lines:
        # Skip generic lines
        if "error" in line.lower() and len(line) > 10:
            # Extract the key part of the error
            # Remove file paths and line numbers for portability
            clean = re.sub(r"\([^)]+\):", "", line)  # Remove (file:line:col):
            clean = re.sub(r"^\s*error\s*\w*:\s*", "", clean, flags=re.IGNORECASE)
            if clean.strip():
                return clean.strip()[:100]  # Limit length

    # Fallback: use first non-empty line
    for line in lines:
        if line.strip():
            return line.strip()[:100]

    return "error"


def convert_all_dogfood_outputs(
    dogfood_output_dir: Path,
    test_fixtures_dir: Path,
    include_failures: bool = False,
    force: bool = False,
) -> tuple[int, int]:
    """Convert all dogfood outputs to test fixtures.

    Args:
        dogfood_output_dir: Path to dogfood_output directory
        test_fixtures_dir: Path to TestFixtures directory
        include_failures: Also convert failed compilations as error tests
        force: Overwrite existing test files

    Returns:
        Tuple of (successful_conversions, failed_conversions)
    """
    successes_dir = dogfood_output_dir / "successes"
    issues_dir = dogfood_output_dir / "issues"

    converted = 0
    failed = 0

    # Convert successful runs
    if successes_dir.exists():
        for entry in sorted(successes_dir.iterdir()):
            if entry.is_dir():
                print(f"Converting success: {entry.name}")
                result = convert_dogfood_to_test(entry, test_fixtures_dir, force=force)
                if result:
                    converted += 1
                else:
                    failed += 1

    # Optionally convert failures as error tests
    if include_failures and issues_dir.exists():
        for entry in sorted(issues_dir.iterdir()):
            if entry.is_dir() and "compilation_failed" in entry.name:
                print(f"Converting failure: {entry.name}")
                result = convert_dogfood_to_test(
                    entry, test_fixtures_dir, category="errors", force=force
                )
                if result:
                    converted += 1
                else:
                    failed += 1

    return converted, failed
