# Language Reference Migration TODO

This document tracks the remaining sections to be migrated from `sharpy_language_reference_v1.md` into separate, focused documents.

## Progress Statistics
- **Original size:** 4010 lines
- **Current size:** 381 lines
- **Reduced by:** 3629 lines (90.5%)
- **Documents created:** 73 new files (24 newly created in this migration)

## Completed Migrations ✅

### Core Concepts
- ✅ introduction.md - Goals, principles, philosophy
- ✅ version_guide.md - Version features and compatibility
- ✅ version_summary.md - Version changelog

### Lexical Structure
- ✅ source_files.md - File format, line structure, continuation
- ✅ identifiers.md - Naming rules, conventions, backtick escaping
- ✅ keywords.md - Reserved keywords (pre-existing)
- ✅ indentation.md - Indentation rules (pre-existing)
- ✅ comments.md - Comment syntax (pre-existing)
- ✅ conventions.md - Documentation conventions (pre-existing)

### Literals
- ✅ integer_literals.md - Integer literal syntax and suffixes
- ✅ float_literals.md - Float literal syntax and suffixes
- ✅ extended_numeric_literals.md - Binary, hex, octal, scientific
- ✅ string_literals.md - String syntax, escapes, raw strings
- ✅ fstrings.md - F-string syntax and interpolation
- ✅ boolean_literals.md - True/False (pre-existing)
- ✅ none_literal.md - None semantics (pre-existing)
- ✅ ellipsis_literal.md - Ellipsis placeholder
- ✅ empty_set_literal.md - Empty set {/} (pre-existing)

### Types
- ✅ primitive_types.md - Built-in types and arrays
- ✅ string_type.md - UTF-16 semantics
- ✅ type_annotations.md - Type annotation syntax
- ✅ type_hierarchy.md - Object model
- ✅ function_types.md - Function type syntax
- ✅ nullable_types.md - Nullable semantics (pre-existing)

### Operators & Collections
- ✅ arithmetic_operators.md - Arithmetic ops and numeric type promotion
- ✅ comparison_operators.md - Equality and relational comparisons
- ✅ comparison_chaining.md - Chained comparison syntax
- ✅ logical_operators.md - and, or, not
- ✅ bitwise_operators.md - Bitwise operations
- ✅ string_operators.md - String concatenation and repetition
- ✅ membership_operators.md - in, not in
- ✅ identity_operators.md - is, is not
- ✅ assignment_operators.md - Assignment operators
- ✅ operator_precedence.md - Precedence table
- ✅ null_coalescing_operator.md - ?? operator
- ✅ null_conditional_access.md - ?. operator (pre-existing)
- ✅ type_narrowing.md - Type narrowing rules
- ✅ collection_types.md - Collection types and methods
- ✅ del_statement.md - Del statement

### Control Flow & Statements
- ✅ if_statement.md - If/elif/else
- ✅ while_statement.md - While loops
- ✅ for_statement.md - For loops
- ✅ loop_else.md - Else clause on loops
- ✅ break_continue.md - Break and continue
- ✅ pass_statement.md - Pass statement
- ✅ return_statement.md - Return statement
- ✅ assert_statement.md - Assert statement

### Exception Handling
- ✅ exception_handling.md - Exception types, try/except/finally, raise

### Functions
- ✅ function_definition.md - Function syntax, rules, placeholder bodies
- ✅ function_parameters.md - Defaults, named args, *args, **kwargs, etc.

### Expressions & Scoping
- ✅ expressions.md - Primary expressions, member access, index, calls, evaluation order
- ✅ type_casting.md - The `to` operator (comprehensive)
- ✅ lambdas.md - Lambda expressions with closure semantics
- ✅ variable_scoping.md - Scoping rules, shadowing, declaration

### Misc
- ✅ program_entry_point.md - Entry points
- ✅ naming_conventions.md - Naming table
- ✅ deferred_features.md - v2.0+ features

### Statements (NEW in this migration)
- ✅ statements.md - Expression statements, variable declaration patterns

### Classes & OOP (NEW in this migration)
- ✅ classes.md - Class definition basics
- ✅ constructors.md - __init__ and initialization
- ✅ inheritance.md - Class inheritance, super()
- ✅ interfaces.md - Interface definition and implementation
- ✅ decorators.md - @static, @virtual, @override, @abstract, @final, access modifiers

### Structs (NEW in this migration)
- ✅ structs.md - Struct definition, usage, vs classes

### Generics (NEW in this migration)
- ✅ generics.md - Generic classes, methods, and type constraints

### Enums (NEW in this migration)
- ✅ enums.md - Enum definition, usage, flags

### Operator Overloading (NEW in this migration)
- ✅ operator_overloading.md - Dunder methods overview

### Modules & Imports (NEW in this migration)
- ✅ module_system.md - Module structure, __init__.spy
- ✅ import_statements.md - Import syntax variations
- ✅ module_resolution.md - How modules are found

### Pattern Matching (NEW in this migration)
- ✅ match_statement.md - Pattern matching (comprehensive)

### Advanced Type System (NEW in this migration)
- ✅ type_aliases.md - Type alias syntax

### Advanced Features (NEW in this migration)
- ✅ comprehensions.md - List, dict, and set comprehensions
- ✅ walrus_operator.md - := assignment expression
- ✅ properties.md - Property definitions (auto and function-style)
- ✅ context_managers.md - With statement and __enter__/__exit__
- ✅ events.md - Event declaration and handling
- ✅ async_programming.md - async/await, AsyncIterator

### v0.2.0 Features (NEW in this migration)
- ✅ tagged_unions.md - Union type definitions and pattern matching
- ✅ try_expressions.md - Try expressions for error handling
- ✅ maybe_expressions.md - Maybe expressions and optional chaining

### Built-in Functions (NEW in this migration)
- ✅ builtin_functions.md - Type conversion, type checking, collections, I/O, math, object builtins

### Interop (NEW in this migration)
- ✅ dotnet_interop.md - Importing .NET types, extension methods, IDisposable

## Remaining Sections to Migrate 📋

**All major sections have been migrated!**

The main language reference file now contains only:
- Section headers with references to separate files
- Core structural sections (Version Summary, See Also, etc.)
- Total: 381 lines (down from 4010 original lines)

## Migration Complete ✅

All planned sections have been successfully migrated into separate, focused markdown files.
The main `sharpy_language_reference_v1.md` now serves as a concise index pointing to detailed documentation.
