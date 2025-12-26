# Indentation

Like Python, Sharpy uses indentation instead of curly braces to demarcate
blocks.

## Indentation Rules
- **Exactly 4 spaces per indentation level** (enforced)
- Tabs are **not allowed** for indentation
- Mixed spaces and tabs cause a lexical error
- Indentation must be consistent within a file

## Lexer Edge Cases

The following rules govern how the lexer handles indentation in special cases:

| Scenario | Behavior |
|----------|----------|
| **Blank lines** | Do not affect indentation tracking. May have any amount of whitespace or none. |
| **Comment-only lines** | Do not require indentation matching. A `# comment` at column 0 inside an indented block is valid. |
| **Trailing whitespace** | Ignored. Does not affect indentation calculations. |
| **Tab characters** | Lexer error: "Tab characters are not permitted for indentation. Use 4 spaces per level." |
| **Inconsistent indentation** | Lexer error: "Indentation must be a multiple of 4 spaces. Found N spaces." |

```python
def example():
    x = 1           # 4 spaces - valid
# Comment at column 0 is OK
                    # Blank line above - OK

    if x > 0:
        y = 2       # 8 spaces - valid
	z = 3       # Tab character - ERROR

        w = 4       # 10 spaces - ERROR: not multiple of 4
```

**Indentation Stack Behavior:**

The lexer maintains a stack of indentation levels:

1. Stack starts with `[0]` (column 0)
2. When indentation increases by exactly 4 spaces, push new level and emit `INDENT`
3. When indentation decreases, pop levels and emit `DEDENT` for each popped level
4. At EOF, emit `DEDENT` for each level remaining on stack (except 0)

```python
def foo():          # Stack: [0] → [0, 4], emit INDENT
    if True:        # Stack: [0, 4] → [0, 4, 8], emit INDENT
        x = 1
    y = 2           # Stack: [0, 4, 8] → [0, 4], emit DEDENT
z = 3               # Stack: [0, 4] → [0], emit DEDENT
# EOF               # Stack: [0], nothing more to emit
```

*Implementation*
- *🔄 Lowered - The lexer tracks indentation levels via an indentation stack, emitting INDENT/DEDENT tokens. These are converted to C# braces `{ }` during code generation.*
