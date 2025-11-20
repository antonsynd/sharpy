# Contributing to Documentation

## Overview

**docs/** contains all documentation for the Sharpy programming language, including language reference, specifications, architecture documentation, and user guides.

**Location:** `docs/`

## What's in This Directory

### Directory Structure

```
docs/
├── manual/                      # User guides
│   ├── basics.md               # Getting started
│   ├── variables.md            # Variable declarations
│   ├── functions.md            # Functions and lambdas
│   ├── types.md                # Type system basics
│   ├── control_flow.md         # if, while, for, etc.
│   ├── errors.md               # Exception handling
│   ├── operators.md            # Operator reference
│   ├── structs.md              # Struct types
│   └── protocols.md            # Protocol/interface system
├── specs/                       # Language specifications
│   ├── language_reference.md   # Complete language spec
│   ├── type_system.md          # Type system specification
│   └── builtins.md             # Builtin functions reference
├── architecture/                # Design documentation
│   ├── semantic-analyzer-architecture.md
│   ├── logging-architecture.md
│   ├── cached-overload-discovery.md
│   └── sharpy-csharp-feature-enhancements.md
├── planning/                    # Planning documents (future features)
├── status/                      # Feature status tracking
├── validation/                  # Validation and testing docs
└── README.md                    # Documentation index
```

## Documentation Types

### User Guides (`manual/`)
- **Purpose:** Help users learn and use Sharpy
- **Audience:** Developers writing Sharpy code
- **Style:** Tutorial-like, with examples
- **Format:** Markdown with code examples

### Specifications (`specs/`)
- **Purpose:** Define language behavior precisely
- **Audience:** Compiler developers, language designers
- **Style:** Formal, comprehensive, precise
- **Format:** Markdown with formal definitions

### Architecture (`architecture/`)
- **Purpose:** Explain compiler internals and design decisions
- **Audience:** Contributors, compiler developers
- **Style:** Technical, design-focused
- **Format:** Markdown with diagrams where helpful

### Planning (`planning/`)
- **Purpose:** Track future features and designs
- **Audience:** Core team, contributors
- **Style:** Informal, evolving
- **Format:** Markdown, notes, brainstorming

### Status (`status/`, `validation/`)
- **Purpose:** Track implementation progress
- **Audience:** Contributors, users
- **Style:** Lists, checkboxes, status indicators
- **Format:** Markdown with checklists

## How to Update Documentation

### When to Update Documentation

**Always update documentation when:**
- Adding a new language feature
- Changing behavior of existing features
- Fixing bugs that affect documented behavior
- Adding new public APIs to Sharpy.Core
- Making architecture changes

**Documentation should be updated in the SAME PR** as the code changes.

### Which Documentation to Update

**For new language features:**
1. **`specs/language_reference.md`** - Formal specification
2. **`manual/<topic>.md`** - User guide with examples
3. **`README.md`** (root) - If it's a major feature

**For standard library additions:**
1. **`specs/builtins.md`** - If it's a builtin function
2. **Code comments** - In the implementation

**For compiler changes:**
1. **`architecture/<component>-architecture.md`** - Design documentation
2. **Code comments** - In the implementation

### Documentation Style Guide

**Code Examples:**
```markdown
## Feature Name

Brief description of the feature.

### Syntax

```python
# Example syntax
x: int = 42
```

### Description

Detailed explanation of how it works.

### Examples

```python
# Example 1: Basic usage
def greet(name: str) -> str:
    return f"Hello, {name}!"

result = greet("World")
print(result)  # Output: Hello, World!
```

```python
# Example 2: Advanced usage
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

calc = Calculator()
result = calc.add(5, 3)
print(result)  # Output: 8
```
```

**Formatting:**
- Use `###` for major sections
- Use `####` for subsections
- Use code blocks with `python` syntax highlighting for Sharpy code
- Use code blocks with `csharp` for generated C# code
- Use code blocks with `bash` for shell commands
- Keep lines under 100 characters where possible
- Use **bold** for emphasis
- Use `code` for identifiers, keywords, function names

**Tone:**
- Clear and concise
- Beginner-friendly for user guides
- Technical and precise for specifications
- Use active voice: "The compiler generates..." not "The code is generated..."

## Common Documentation Tasks

### Adding Documentation for a New Feature

**Example: Adding docs for the `@` operator**

1. **Update language reference (`specs/language_reference.md`):**
   ```markdown
   ## Matrix Multiplication Operator
   
   The `@` operator performs matrix multiplication.
   
   **Syntax:**
   ```python
   result = matrix_a @ matrix_b
   ```
   
   **Requirements:**
   - Both operands must implement `__matmul__` method
   - Dimensions must be compatible
   
   **Returns:** Result of matrix multiplication
   ```

2. **Update operators manual (`manual/operators.md`):**
   ```markdown
   ### Matrix Multiplication (`@`)
   
   Use `@` for matrix multiplication:
   
   ```python
   import numpy as np
   
   a = np.array([[1, 2], [3, 4]])
   b = np.array([[5, 6], [7, 8]])
   c = a @ b  # Matrix multiplication
   ```
   ```

3. **Update README if it's a major feature**

### Fixing Documentation Bugs

1. **Identify the error** in the documentation
2. **Verify correct behavior** (test in compiler/REPL)
3. **Update documentation** to match reality
4. **Check related documentation** for consistency

### Adding Examples

**Good examples:**
- Show common use cases
- Include expected output
- Are self-contained (no external dependencies)
- Progress from simple to complex
- Include error cases when relevant

**Example:**
```markdown
### Error Handling

```python
# Example: Handling division by zero
def safe_divide(a: float, b: float) -> float?:
    try:
        return a / b
    except ZeroDivisionError:
        print("Cannot divide by zero")
        return None

result = safe_divide(10, 0)
if result is not None:
    print(f"Result: {result}")
else:
    print("Division failed")

# Output:
# Cannot divide by zero
# Division failed
```
```

### Updating Architecture Docs

When making significant compiler changes:

1. **Read existing architecture docs** to understand current design
2. **Update affected docs** to reflect new design
3. **Add new docs** if you're introducing a new subsystem
4. **Include:**
   - Design rationale (why this approach?)
   - Key components
   - Data flow
   - Trade-offs considered
   - Examples

**Example structure:**
```markdown
# Component Name Architecture

## Overview
Brief description of what this component does.

## Design Goals
- Goal 1
- Goal 2

## Components

### Component A
Description...

### Component B
Description...

## Data Flow

```
Input → Process → Output
```

## Key Algorithms

### Algorithm Name
Explanation...

## Trade-offs
- **Chose X over Y because:** Reason
- **Performance vs. Simplicity:** Decision

## Examples
Code examples showing the component in action.

## Future Work
- Planned enhancement 1
- Planned enhancement 2
```

## Building/Viewing Documentation

### Viewing Locally
Documentation is in Markdown format - view with any Markdown viewer:

```bash
# View in VSCode
code docs/manual/functions.md

# View in browser with grip (if installed)
grip docs/manual/functions.md

# View with Markdown preview in GitHub
# Just push and view on GitHub
```

### Checking Links
```bash
# Check for broken internal links (manual)
grep -r "\[.*\](.*\.md)" docs/ | grep -v "^docs/.*:.*docs/"
```

### Spell Checking
```bash
# Use VSCode spell checker or
aspell check docs/manual/functions.md
```

## Documentation Best Practices

### Keep Documentation in Sync with Code
- **Update docs in the same PR** as code changes
- **Review docs** during code review
- **Test examples** to ensure they work

### Write for Your Audience
- **User guides:** Assume beginner knowledge
- **Specifications:** Be precise and complete
- **Architecture:** Assume technical knowledge

### Use Examples Liberally
- Show, don't just tell
- Include expected output
- Cover common cases

### Be Consistent
- Use consistent terminology
- Follow existing formatting
- Match the style of nearby docs

### Keep It Current
- Remove outdated information
- Update status when features are implemented
- Mark deprecated features clearly

## Common Pitfalls to Avoid

**Don't:**
- ❌ Leave outdated examples that don't work
- ❌ Use vague language like "might" or "could"
- ❌ Copy-paste without adapting to context
- ❌ Forget to update related documentation
- ❌ Use overly complex examples for basic concepts

**Do:**
- ✅ Test your examples before committing
- ✅ Be precise and specific
- ✅ Keep examples simple and focused
- ✅ Cross-reference related documentation
- ✅ Start simple, then show advanced usage

## Dependencies

None - documentation is plain Markdown.

## Related Documentation

- **Repository README:** `README.md` (root)
- **CLI Documentation:** `src/Sharpy.Cli/README.md`
- **All other guides:** `.github/instructions/*/HOW_TO_CONTRIBUTE.instructions.md`

## Review Checklist

Before submitting documentation changes:

- [ ] Examples are tested and work
- [ ] Code blocks have proper syntax highlighting
- [ ] Spelling and grammar are correct
- [ ] Formatting is consistent with existing docs
- [ ] Links to other docs work
- [ ] Audience-appropriate level of detail
- [ ] Related documentation is updated
- [ ] No outdated information remains

## Getting Help

- Review existing documentation for style and structure
- Check Python documentation for reference (for language features)
- Ask in pull request if unsure about terminology or approach
- Look at recent documentation PRs for examples
