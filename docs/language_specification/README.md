# Sharpy Language Reference

See [introduction.md](introduction.md) for goals, principles, and philosophy.

## Lexical Structure

See [source_files.md](source_files.md) for file format, line structure, and continuation rules.

See [identifiers.md](identifiers.md) for identifier syntax, naming conventions, and backtick escaping.

See [keywords.md](keywords.md) for reserved keywords.

See [indentation.md](indentation.md) for indentation rules.

See [comments.md](comments.md) for comment syntax.

---

## Literals

See [integer_literals.md](integer_literals.md) for integer literals and suffixes.

See [float_literals.md](float_literals.md) for float literals and suffixes.

See [extended_numeric_literals.md](extended_numeric_literals.md) for binary, hex, octal, and scientific notation.

See [string_literals.md](string_literals.md) for string syntax, escape sequences, and raw strings.

See [fstrings.md](fstrings.md) for formatted string literals (f-strings).

See [boolean_literals.md](boolean_literals.md) for `True` and `False`.

See [none_literal.md](none_literal.md) for `None` literal and its semantics.

See [ellipsis_literal.md](ellipsis_literal.md) for the `...` placeholder.

See [empty_set_literal.md](empty_set_literal.md) for the `{/}` empty set literal.

---

## Types

See [primitive_types.md](primitive_types.md) for built-in primitive types and arrays.

See [string_type.md](string_type.md) for string type and UTF-16 semantics.

See [type_annotations.md](type_annotations.md) for type annotation syntax.

See [type_hierarchy.md](type_hierarchy.md) for the type hierarchy and object model.

See [nullable_types.md](nullable_types.md) for nullable type semantics.

See [named_tuples.md](named_tuples.md) for named tuple syntax and usage.

---

## Function Types

See [function_types.md](function_types.md) for function type syntax, compatibility, and usage.

---

## Operators

See [null_coalescing_operator.md](null_coalescing_operator.md) for the `??` operator.

See [null_coalescing_assignment.md](null_coalescing_assignment.md) for the `??=` operator.

See [null_conditional_access.md](null_conditional_access.md) for the `?.` operator.

See [type_narrowing.md](type_narrowing.md) for type narrowing rules with `is not None` and `isinstance()`.

---

## Collection Types

See [collection_types.md](collection_types.md) for collection types, methods, and .NET interop.

See [smart_ranges.md](smart_ranges.md) for range operations, membership testing, and pattern matching.

See [spread_operator.md](spread_operator.md) for spreading collections and objects.

### Collection Literals

See [del_statement.md](del_statement.md) for the `del` statement.

---

## Operators

See [arithmetic_operators.md](arithmetic_operators.md) for arithmetic operators and numeric type promotion.

See [comparison_operators.md](comparison_operators.md) for comparison operators.

See [comparison_chaining.md](comparison_chaining.md) for chained comparisons.

See [logical_operators.md](logical_operators.md) for `and`, `or`, `not`.

See [bitwise_operators.md](bitwise_operators.md) for bitwise operations.

See [string_operators.md](string_operators.md) for string concatenation and repetition.

See [membership_operators.md](membership_operators.md) for `in` and `not in`.

See [identity_operators.md](identity_operators.md) for `is` and `is not`.

See [assignment_operators.md](assignment_operators.md) for assignment operators.

See [pipe_operator.md](pipe_operator.md) for the `|>` pipe operator.

See [operator_precedence.md](operator_precedence.md) for operator precedence table.

---

## Expressions

See [expressions.md](expressions.md) for primary expressions, member access, index access, function calls, conditional expressions, and expression evaluation order.

See [type_casting.md](type_casting.md) for the `to` operator and type casting.

See [lambdas.md](lambdas.md) for lambda expressions.

See [partial_application.md](partial_application.md) for partial application with `_` placeholder.

See [expression_blocks.md](expression_blocks.md) for `do:` expression blocks.

---

## Statements

See [statements.md](statements.md) for expression statements, variable declaration and assignment, constants.

## Variable Scoping Rules

See [variable_scoping.md](variable_scoping.md) for:
- No `global` or `nonlocal` keywords
- Block-scoped vs containing-scope constructs
- Variable shadowing
- Variable declaration and assignment
- No declaration without assignment

### Pass Statement

See [pass_statement.md](pass_statement.md) for the pass statement.

### Break and Continue

See [break_continue.md](break_continue.md) for break and continue statements.

### Return Statement

See [return_statement.md](return_statement.md) for the return statement.

### Assert Statement

See [assert_statement.md](assert_statement.md) for the assert statement.

---

## Control Flow

See [if_statement.md](if_statement.md) for if/elif/else statements.

See [while_statement.md](while_statement.md) for while loops.

See [for_statement.md](for_statement.md) for for loops.

See [loop_else.md](loop_else.md) for else clauses on loops.

---

## Exception Handling

See [exception_handling.md](exception_handling.md) for exception types, try/except/finally, and raise statements.

---

## Functions

See [function_definition.md](function_definition.md) for function definition syntax, rules, and placeholder bodies.

See [function_parameters.md](function_parameters.md) for parameter types overview, named arguments, and function overloading.

See [function_default_parameters.md](function_default_parameters.md) for default parameter values and compile-time constant requirements.

See [function_variadic_arguments.md](function_variadic_arguments.md) for variadic arguments (*args), unpacking, and C# interop.

See [contracts.md](contracts.md) for design-by-contract with preconditions, postconditions, and invariants.

---

## Classes

See [classes.md](classes.md) for basic class definition, field declarations, instance methods, and rules.

See [constructors.md](constructors.md) for constructor overloading and constructor chaining.

---

## Imports

See [import_statements.md](import_statements.md) for import and from-import syntax variations.

See [module_resolution.md](module_resolution.md) for how module names are resolved.

See [module_system.md](module_system.md) for package structure, `__init__.spy` files, and circular import handling.

---

## Structs

See [structs.md](structs.md) for struct definition, usage, value semantics, and comparison with classes.

---

## Interfaces

See [interfaces.md](interfaces.md) for interface definition, implementation, generic interfaces, interface inheritance, default methods, conflict resolution, and dunder methods in interfaces.

---

## Inheritance

See [inheritance.md](inheritance.md) for single class inheritance, multiple interface implementation, super() usage, and abstract classes.

---

## Decorators

See [decorators.md](decorators.md) for @static, @virtual, @override, @abstract, @final, and access modifiers (@public, @private, @protected, @internal).

---
## Generics

See [generics.md](generics.md) for generic classes, generic methods, and type constraints.

---
## Enumerations

See [enums.md](enums.md) for enum definition, usage, and flags.

---
## Operator Overloading

See [operator_overloading.md](operator_overloading.md) for dunder methods, arithmetic operators, comparison operators, and container operations.

---
## Pattern Matching

See [match_statement.md](match_statement.md) for match statement syntax and pattern matching.

---
## Type Aliases

See [type_aliases.md](type_aliases.md) for type alias syntax.

---
## Tagged Unions (Algebraic Data Types)

See [tagged_unions.md](tagged_unions.md) for union type definitions and pattern matching.

See [tagged_unions_result.md](tagged_unions_result.md) for the Result type for error handling with typed errors.

See [tagged_unions_optional.md](tagged_unions_optional.md) for the Optional type for representing optional values.

---
## Try expressions

See [try_expressions.md](try_expressions.md) for try expressions and error handling.

---
## Maybe expressions

See [maybe_expressions.md](maybe_expressions.md) for maybe expressions and optional chaining.

---
## Comprehensions

See [comprehensions.md](comprehensions.md) for list, dict, and set comprehensions.

---
## Walrus Operator

See [walrus_operator.md](walrus_operator.md) for assignment expressions using :=.

---
## Properties

See [properties.md](properties.md) for auto-properties overview and property forms.

See [properties_function_style.md](properties_function_style.md) for function-style properties with custom logic, validation, and static properties.

See [properties_inheritance.md](properties_inheritance.md) for virtual, abstract, override properties, and interface implementation.

---
## Context Managers

See [context_managers.md](context_managers.md) for with statement and context manager protocol.

---
## Events

See [events.md](events.md) for event declaration and handling.

---
## Async Programming

See [async_programming.md](async_programming.md) for async/await and AsyncIterator.

---
## Built-in Functions

See [builtin_functions.md](builtin_functions.md) for type conversion, type checking, collection operations, I/O, math, and object builtins.

---
## .NET Interop

See [dotnet_interop.md](dotnet_interop.md) for importing .NET types, extension methods, and IDisposable.

---
## Naming Conventions Summary
See [naming_conventions.md](naming_conventions.md) for symbol naming conventions.

---
## Program Entry Point

See [program_entry_point.md](program_entry_point.md) for the program entry point.

---
## Deferred Features

See [deferred_features.md](deferred_features.md) for deferred features.
