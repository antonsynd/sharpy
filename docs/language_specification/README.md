# Sharpy Language Reference

See [introduction.md](introduction.md) for goals, principles, and philosophy.

> **Note:** This reference describes the complete Sharpy language design. Some features are not yet implemented
> in the compiler — these are marked with implementation status banners in their respective documents.
> For the implementation roadmap, see [phases2.md](../implementation_planning/phases2.md).

## Lexical Structure

See [source_files.md](source_files.md) for file format, line structure, and continuation rules.

See [identifiers.md](identifiers.md) for identifier syntax, naming conventions, and backtick escaping.

See [keywords.md](keywords.md) for reserved keywords.

See [indentation.md](indentation.md) for indentation rules.

See [comments.md](comments.md) for comment syntax.

See [lexer_implementation.md](lexer_implementation.md) for lexer implementation details and token types.

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

---

## Types

See [primitive_types.md](primitive_types.md) for built-in primitive types and arrays.

See [string_type.md](string_type.md) for string type and UTF-16 semantics.

See [type_annotations.md](type_annotations.md) for type annotation syntax.

See [type_annotation_shorthand.md](type_annotation_shorthand.md) for shorthand type annotation syntax (`T?`, `T !E`).

See [type_hierarchy.md](type_hierarchy.md) for the type hierarchy and object model.

See [nullable_types.md](nullable_types.md) for `T | None` C# nullable types (.NET interop).

See [named_tuples.md](named_tuples.md) for named tuple syntax and usage.

---

## Function Types

See [function_types.md](function_types.md) for function type syntax, compatibility, and usage.

See [delegates.md](delegates.md) for named delegate type declarations with variance support.

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

See [null_coalescing_operator.md](null_coalescing_operator.md) for the `??` operator.

See [null_coalescing_assignment.md](null_coalescing_assignment.md) for the `??=` operator.

See [null_conditional_access.md](null_conditional_access.md) for the `?.` operator.

See [pipe_operator.md](pipe_operator.md) for the `|>` pipe operator.

See [operator_precedence.md](operator_precedence.md) for operator precedence table.

See [type_narrowing.md](type_narrowing.md) for type narrowing rules with `is not None` and `isinstance()`.

---

## Expressions

See [expressions.md](expressions.md) for primary expressions, member access, index access, function calls, conditional expressions, and expression evaluation order.

See [type_casting.md](type_casting.md) for the `to` operator and type casting.

See [lambdas.md](lambdas.md) for lambda expressions.

See [partial_application.md](partial_application.md) for partial application with `_` placeholder.

See [expression_blocks.md](expression_blocks.md) for `do:` expression blocks.

---

## Collection Types

See [collection_types.md](collection_types.md) for collection types, methods, and .NET interop.

See [spread_operator.md](spread_operator.md) for spreading collections and objects.

---

## Statements

See [statements.md](statements.md) for expression statements, variable declaration and assignment, constants.

See [variable_declaration.md](variable_declaration.md) for variable declaration syntax and rules.

See [del_statement.md](del_statement.md) for the `del` statement.

---

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

See [method_overloading.md](method_overloading.md) for class method overloading, constructor overloading, and operator overloading.

See [function_default_parameters.md](function_default_parameters.md) for default parameter values and compile-time constant requirements.

See [function_variadic_arguments.md](function_variadic_arguments.md) for variadic arguments (*args), unpacking, and C# interop.

See [flexible_arguments.md](flexible_arguments.md) for positional-only (`/`), keyword-only (`*`), `@kwargs`, and `@dynamic_kwargs`.

See [parameter_modifiers.md](parameter_modifiers.md) for `ref`, `out`, and `in` pass-by-reference parameters.

See [generators.md](generators.md) for generator functions, `yield`, `yield from`, and generator `__iter__`/`__reversed__`.

---

## Classes

See [classes.md](classes.md) for basic class definition, field declarations, instance methods, and rules.

See [class_methods.md](class_methods.md) for class methods and the absence of `@classmethod`.

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

See [interfaces.md](interfaces.md) for interface definition, implementation, generic interfaces, interface inheritance, and dunder methods in interfaces.

See [interface_default_methods.md](interface_default_methods.md) for default method implementations, conflict resolution between base classes and interfaces, and guidelines for choosing interfaces vs abstract classes.

---

## Inheritance

See [inheritance.md](inheritance.md) for single class inheritance, multiple interface implementation, super() usage, and abstract classes.

---

## Decorators

See [decorators.md](decorators.md) for @static, @virtual, @override, @abstract, @final, and access modifiers (@public, @private, @protected, @internal).

---

## Generics

See [generics.md](generics.md) for generic classes, generic methods, and type constraints.

See [generic_variance.md](generic_variance.md) for covariance (`out`) and contravariance (`in`) on type parameters.

---

## Enumerations

See [enums.md](enums.md) for enum definition, usage, and flags.

---

## Operator Overloading

See [operator_overloading.md](operator_overloading.md) for dunder methods, arithmetic operators, comparison operators, and container operations.

See [dunder_methods.md](dunder_methods.md) for comprehensive dunder method reference.

See [dunder_methods_recommendations.md](dunder_methods_recommendations.md) for dunder method recommendations and best practices.

See [dunder_invocation_rules.md](dunder_invocation_rules.md) for rules on when and how dunder methods can be called, inheritance, and cross-dunder synthesis.

See [conversion_operators.md](conversion_operators.md) for `@implicit` and `@explicit` user-defined type conversions.

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

## Try Expressions

See [try_expressions.md](try_expressions.md) for try expressions and error handling.

---

## Maybe Expressions

See [maybe_expressions.md](maybe_expressions.md) for maybe expressions and optional chaining.

---

## Comprehensions

See [comprehensions.md](comprehensions.md) for list, dict, and set comprehensions.

See [tuple_unpacking.md](tuple_unpacking.md) for tuple destructuring, nested unpacking, rest patterns, and comprehension unpacking.

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

See [events_alt.md](events_alt.md) for alternate event design document.

---

## Async Programming

See [async_programming.md](async_programming.md) for async/await and AsyncIterator.

---

## Built-in Functions

See [builtin_functions.md](builtin_functions.md) for type conversion, type checking, collection operations, I/O, math, and object builtins.

---

## Name Mangling

See [name_mangling.md](name_mangling.md) for snake_case to PascalCase conversion and special name mappings.

---

## .NET Interop

See [dotnet_interop.md](dotnet_interop.md) for importing .NET types, extension methods, and IDisposable.

---

## Static Methods

See [static_methods.md](static_methods.md) for static method rules and implicit `@static` inference.

---

## Naming Conventions Summary

See [naming_conventions.md](naming_conventions.md) for symbol naming conventions.

See [conventions.md](conventions.md) for broader project conventions.

---

## Program Entry Point

See [program_entry_point.md](program_entry_point.md) for the program entry point.

---

## Deferred Features

See [deferred_features.md](deferred_features.md) for features requiring newer C#/.NET versions.
