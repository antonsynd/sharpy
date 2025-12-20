# Language Reference Migration TODO

This document tracks the remaining sections to be migrated from `sharpy_language_reference_v1.md` into separate, focused documents.

## Progress Statistics
- **Original size:** 6688 lines
- **Current size:** 5428 lines
- **Reduced by:** 1260 lines (18.8%)
- **Documents created:** 24 new files

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
- ✅ null_coalescing_operator.md - ?? operator
- ✅ null_conditional_access.md - ?. operator (pre-existing)
- ✅ type_narrowing.md - Type narrowing rules
- ✅ collection_types.md - Collection types and methods
- ✅ del_statement.md - Del statement

### Misc
- ✅ program_entry_point.md - Entry points
- ✅ naming_conventions.md - Naming table
- ✅ deferred_features.md - v2.0+ features

## Remaining Sections to Migrate 📋

### Operators (Estimated ~400 lines)
Priority: HIGH

- [ ] **arithmetic_operators.md** - +, -, *, /, //, %, **
  - Operator table
  - Division semantics (always float)
  - Floor division rules
  - Numeric type promotion table
  - Implementation notes

- [ ] **comparison_operators.md** - ==, !=, <, >, <=, >=, is, is not
  - Comparison operators
  - Object identity vs equality
  - Implementation notes

- [ ] **logical_operators.md** - and, or, not
  - Boolean logic
  - Short-circuit evaluation
  - Truthiness rules

- [ ] **bitwise_operators.md** - &, |, ^, ~, <<, >>
  - Bitwise operations
  - Shift operations

- [ ] **membership_operators.md** - in, not in
  - Membership testing
  - __contains__ dunder

- [ ] **assignment_operators.md** - =, +=, -=, etc.
  - Simple and augmented assignment
  - Multiple assignment
  - Unpacking assignment

- [ ] **conditional_expression.md** - Ternary operator
  - x if condition else y

### Control Flow (Estimated ~600 lines)
Priority: HIGH

- [ ] **if_statement.md** - If/elif/else
- [ ] **while_statement.md** - While loops
- [ ] **for_statement.md** - For loops, iteration protocol
- [ ] **loop_else.md** - Else clauses on loops
- [ ] **break_continue.md** - Loop control
- [ ] **pass_statement.md** - No-op statement
- [ ] **match_statement.md** - Pattern matching
  - Match/case syntax
  - Pattern types (literal, capture, wildcard, sequence, mapping, class, or, as)
  - Guards
  - Exhaustiveness checking

### Functions (Estimated ~500 lines)
Priority: HIGH

- [ ] **function_definition.md** - Basic function syntax
- [ ] **function_parameters.md** - Parameters and arguments
  - Positional, keyword, default
  - *args and **kwargs
  - Type annotations
- [ ] **return_statement.md** - Return behavior
- [ ] **function_overloading.md** - @overload decorator
- [ ] **lambdas.md** - Lambda expressions

### Classes & OOP (Estimated ~800 lines)
Priority: MEDIUM

- [ ] **classes.md** - Class definition basics
- [ ] **constructors.md** - __init__ and initialization
- [ ] **methods.md** - Instance and class methods
- [ ] **inheritance.md** - Class inheritance, super()
- [ ] **interfaces.md** - Interface definition and implementation
- [ ] **abstract_classes.md** - Abstract classes and methods
- [ ] **decorators.md** - @static, @virtual, @override, @abstract, @final
- [ ] **access_modifiers.md** - @public, @private, @protected, @internal

### Structs (Estimated ~150 lines)
Priority: MEDIUM

- [ ] **structs.md** - Struct definition, usage, vs classes

### Generics (Estimated ~300 lines)
Priority: MEDIUM

- [ ] **generic_classes.md** - Generic class syntax
- [ ] **generic_methods.md** - Generic method syntax
- [ ] **type_constraints.md** - Where clauses, constraints

### Enums (Estimated ~120 lines)
Priority: MEDIUM

- [ ] **enums.md** - Enum definition, usage, flags

### Operator Overloading (Estimated ~320 lines)
Priority: MEDIUM

- [ ] **dunder_methods.md** - Overview of dunder methods
- [ ] **arithmetic_dunders.md** - __add__, __sub__, __mul__, etc.
- [ ] **comparison_dunders.md** - __eq__, __lt__, etc.
- [ ] **container_dunders.md** - __getitem__, __setitem__, __len__, etc.

### Exception Handling (Estimated ~180 lines)
Priority: MEDIUM

- [ ] **try_except.md** - Try/except blocks
- [ ] **finally_clause.md** - Finally behavior
- [ ] **raise_statement.md** - Raising exceptions
- [ ] **custom_exceptions.md** - Defining custom exceptions

### Modules & Imports (Estimated ~300 lines)
Priority: MEDIUM

- [ ] **module_system.md** - Module structure, __init__.spy
- [ ] **import_statements.md** - Import syntax variations
- [ ] **module_resolution.md** - How modules are found

### Advanced Type System (Estimated ~280 lines)
Priority: LOW

- [ ] **type_aliases.md** - Type alias syntax with `type`
- [ ] **variable_shadowing.md** - Shadowing rules
- [ ] **casting_to_operator.md** - Explicit casting with `to`
- [ ] **type_equivalence.md** - Type compatibility rules

### Advanced Features (Estimated ~830 lines)
Priority: LOW

- [ ] **comparison_chaining.md** - Chained comparisons (a < b < c)
- [ ] **list_comprehensions.md** - List comprehension syntax
- [ ] **dict_comprehensions.md** - Dict comprehension syntax
- [ ] **set_comprehensions.md** - Set comprehension syntax
- [ ] **comprehension_scoping.md** - Variable scoping in comprehensions
- [ ] **walrus_operator.md** - := assignment expression (already created, but may need update)
- [ ] **properties.md** - Property definitions (auto and function-style)
- [ ] **context_managers.md** - With statement and __enter__/__exit__
- [ ] **events.md** - Event declaration and handling
- [ ] **async_programming.md** - async/await, AsyncIterator

### Built-in Functions (Estimated ~320 lines)
Priority: LOW

- [ ] **type_conversion_builtins.md** - int(), str(), bool(), etc.
- [ ] **type_checking_builtins.md** - isinstance(), type()
- [ ] **collection_builtins.md** - len(), min(), max(), sum(), enumerate(), zip(), range(), etc.
- [ ] **io_builtins.md** - print(), input()
- [ ] **math_builtins.md** - abs(), pow(), round(), divmod()
- [ ] **object_builtins.md** - repr(), hash(), id()

### Interop
Priority: LOW

- [ ] **dotnet_interop.md** - Importing .NET types, extension methods, IDisposable

## Migration Guidelines

When migrating a section:

1. **Extract cleanly** - Copy the complete section with all subsections
2. **Don't alter content** - Keep the text as-is unless absolutely necessary
3. **No cross-links** - Don't add links between documents
4. **Update main document** - Replace migrated content with a reference like:
   ```markdown
   ## Section Name **[version]**
   
   See [section_name.md](section_name.md) for detailed information.
   ```
5. **Test builds** - Ensure no broken references
6. **Update this TODO** - Check off completed items

## Target Metrics

- **Goal:** Reduce main document to < 1000 lines (mostly just section headers and references)
- **Current:** 5428 lines (19% complete)
- **Remaining:** ~4400 lines to migrate
