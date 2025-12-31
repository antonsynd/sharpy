# Lexer Implementation Notes

This document provides implementation guidance for the Sharpy lexer. It covers state machine design, token generation, and edge case handling.

## Lexer State Machine Overview

The Sharpy lexer operates as a finite state machine with the following primary states:

```
                    ┌─────────────────────────────────────────────────────┐
                    │                                                     │
                    ▼                                                     │
    ┌──────────┐  any    ┌──────────────┐                                │
    │  NORMAL  │◄────────│ LINE_START   │◄────────────────────────┐      │
    └────┬─────┘         └──────────────┘                         │      │
         │                      │                                  │      │
    ┌────┴────┬────────────────┬┴───────────┐                     │      │
    │         │                │            │                      │      │
    │ '"'     │ '#'            │ NEWLINE    │ f'"'                 │      │
    ▼         ▼                ▼            ▼                      │      │
┌───────┐ ┌───────┐     ┌──────────┐  ┌──────────┐                │      │
│STRING │ │COMMENT│     │LINE_START│  │ FSTRING  │────────────────┤      │
└───┬───┘ └───┬───┘     └──────────┘  └────┬─────┘                │      │
    │         │                            │                       │      │
    │ '"'     │ NEWLINE                    │ '{'                   │      │
    ▼         ▼                            ▼                       │      │
┌──────────────────┐                ┌─────────────┐                │      │
│      NORMAL      │◄───────────────│ FSTRING_EXPR│────────────────┘      │
└──────────────────┘    '}'         └─────────────┘                       │
                                           │                              │
                                           │ nested f'"'                  │
                                           ▼                              │
                                    ┌─────────────┐                       │
                                    │FSTRING_NESTED│───────────────────────┘
                                    └─────────────┘
```

## State Descriptions

### LINE_START

Entered at the beginning of each logical line. Responsible for:

1. **Indentation measurement**: Count leading spaces (tabs are errors)
2. **INDENT/DEDENT emission**: Compare with indentation stack
3. **Blank line handling**: Skip lines with only whitespace
4. **Comment line handling**: Comment-only lines don't affect indentation

```
Transitions:
  - Non-whitespace found → emit INDENT/DEDENT as needed → NORMAL
  - Comment found → COMMENT (after indentation check if code follows)
  - NEWLINE found → stay in LINE_START (blank line)
  - EOF → emit remaining DEDENTs → done
```

### NORMAL

Primary lexing state for identifiers, operators, literals, and keywords.

```
Transitions:
  - '"' or "'" → STRING (with appropriate quote type)
  - f'"' or f"'" → FSTRING
  - '"""' or "'''" → MULTILINE_STRING
  - '#' → COMMENT
  - NEWLINE → emit NEWLINE token → LINE_START
  - Digit → NUMBER
  - Letter or '_' → IDENTIFIER
  - Operator chars → OPERATOR
```

### STRING

Lexing a regular string literal.

```
State data:
  - quote_char: '"' or "'"
  - is_raw: bool (r-prefix)

Transitions:
  - quote_char → emit STRING token → NORMAL
  - '\\' (if not raw) → handle escape sequence
  - NEWLINE → error: "Unterminated string literal"
  - EOF → error: "Unterminated string literal"
```

### FSTRING

Lexing an f-string with expression interpolation.

```
State data:
  - quote_char: '"' or "'"
  - brace_depth: int (for nested braces)
  - nesting_level: int (for nested f-strings)

Transitions:
  - '{' → FSTRING_EXPR (brace_depth++)
  - '{{' → literal '{' (stay in FSTRING)
  - quote_char → emit FSTRING_END → NORMAL or parent state
  - NEWLINE → error (for single-quoted)
```

### FSTRING_EXPR

Inside an f-string expression (`{...}`).

```
State data:
  - parent_fstring_state
  - brace_depth: int

Transitions:
  - '}' → brace_depth--; if 0: → back to FSTRING
  - '{' → brace_depth++ (nested dict literal)
  - f'"' → FSTRING_NESTED (nested f-string)
  - All other tokens → normal tokenization
```

### COMMENT

Processing a comment from `#` to end of line.

```
Transitions:
  - NEWLINE → emit COMMENT token (if needed) → LINE_START
  - EOF → emit COMMENT token → done
```

### MULTILINE_STRING

Processing triple-quoted strings (`"""..."""` or `'''...'''`).

```
State data:
  - quote_sequence: '"""' or "'''"
  - is_fstring: bool

Transitions:
  - Matching quote_sequence → emit STRING → NORMAL
  - NEWLINE → include in string (valid)
  - EOF → error: "Unterminated multi-line string"
```

## Indentation Stack Algorithm

The lexer maintains an indentation stack to emit INDENT/DEDENT tokens:

```python
# Pseudocode implementation
indent_stack = [0]  # Always starts with column 0

def process_line_start():
    spaces = count_leading_spaces()

    # Validate: must be multiple of 4
    if spaces % 4 != 0:
        error("Indentation must be a multiple of 4 spaces")

    current_indent = indent_stack[-1]

    if spaces > current_indent:
        # Must increase by exactly 4
        if spaces != current_indent + 4:
            error(f"Expected {current_indent + 4} spaces, found {spaces}")
        indent_stack.append(spaces)
        emit(INDENT)

    elif spaces < current_indent:
        # May decrease by multiple levels
        while indent_stack[-1] > spaces:
            indent_stack.pop()
            emit(DEDENT)

        # Must land on an existing level
        if indent_stack[-1] != spaces:
            error(f"Unindent does not match any outer indentation level")

    # else: same indentation, no tokens
```

## Token Types

The lexer produces the following token categories:

### Structural Tokens

| Token | Description |
|-------|-------------|
| `NEWLINE` | Logical line ending (not inside brackets/parens) |
| `INDENT` | Indentation increase |
| `DEDENT` | Indentation decrease |
| `EOF` | End of file |

### Literal Tokens

| Token | Examples |
|-------|----------|
| `INT_LITERAL` | `42`, `0xFF`, `0b1010`, `1_000_000` |
| `FLOAT_LITERAL` | `3.14`, `1e10`, `2.5f` |
| `STRING_LITERAL` | `"hello"`, `'world'`, `"""multi"""` |
| `FSTRING_START` | `f"` (begins f-string) |
| `FSTRING_MIDDLE` | Text between expressions in f-string |
| `FSTRING_END` | Closing quote of f-string |
| `TRUE` | `True` |
| `FALSE` | `False` |
| `NONE` | `None` |

### Identifier/Keyword Tokens

| Token | Description |
|-------|-------------|
| `IDENTIFIER` | Variable/function/class names |
| `ESCAPED_IDENT` | Backtick-escaped identifier (`` `class` ``) |
| `KEYWORD` | Reserved words (`def`, `class`, `if`, etc.) |

### Operator Tokens

| Token | Symbols |
|-------|---------|
| `PLUS`, `MINUS`, `STAR`, `SLASH` | `+`, `-`, `*`, `/` |
| `DOUBLE_STAR` | `**` |
| `DOUBLE_SLASH` | `//` |
| `PERCENT` | `%` |
| `AMPERSAND`, `PIPE`, `CARET` | `&`, `\|`, `^` |
| `TILDE` | `~` |
| `LSHIFT`, `RSHIFT` | `<<`, `>>` |
| `LT`, `LE`, `GT`, `GE` | `<`, `<=`, `>`, `>=` |
| `EQ`, `NE` | `==`, `!=` |
| `ASSIGN` | `=` |
| `AUGMENTED_ASSIGN` | `+=`, `-=`, `*=`, etc. |
| `WALRUS` | `:=` |
| `ARROW` | `->` |
| `PIPE_ARROW` | `\|>` |
| `DOT`, `QUESTION_DOT` | `.`, `?.` |
| `DOUBLE_QUESTION` | `??` |
| `COLON` | `:` |
| `COMMA` | `,` |
| `SEMICOLON` | `;` (for inline compound statements) |

### Delimiter Tokens

| Token | Symbols |
|-------|---------|
| `LPAREN`, `RPAREN` | `(`, `)` |
| `LBRACKET`, `RBRACKET` | `[`, `]` |
| `LBRACE`, `RBRACE` | `{`, `}` |

## Bracket Depth Tracking

NEWLINEs inside brackets/parentheses are treated as whitespace, not line terminators:

```python
# Pseudocode
bracket_depth = 0

def handle_open_bracket():
    bracket_depth += 1
    emit(LPAREN/LBRACKET/LBRACE)

def handle_close_bracket():
    bracket_depth -= 1
    emit(RPAREN/RBRACKET/RBRACE)

def handle_newline():
    if bracket_depth == 0:
        emit(NEWLINE)
        transition_to(LINE_START)
    else:
        # Implicit line continuation, skip
        pass
```

## Lookahead Requirements

Some tokens require lookahead to disambiguate:

| Context | Lookahead | Tokens |
|---------|-----------|--------|
| `<` | 1 char | `<` vs `<=` vs `<<` |
| `>` | 1 char | `>` vs `>=` vs `>>` |
| `*` | 1 char | `*` vs `**` |
| `/` | 1 char | `/` vs `//` |
| `:` | 1 char | `:` vs `:=` |
| `?` | 1 char | `?` vs `?.` vs `??` |
| `.` | 1 char | `.` vs `...` (ellipsis) |
| `\|` | 1 char | `\|` vs `\|>` |
| `0` | 1 char | `0` (decimal) vs `0x`, `0b`, `0o` |
| `"` | 2 chars | `"` vs `"""` |
| String prefix | 1-2 chars | `r"`, `f"`, `rf"` |

## Error Recovery in Lexer

The lexer should attempt to recover from errors to report multiple issues:

1. **Unterminated string**: Close at EOL/EOF, report error, continue
2. **Invalid character**: Skip, report error, continue
3. **Tab in indentation**: Report error, treat as 4 spaces, continue
4. **Invalid escape sequence**: Include literally, report warning, continue
5. **Invalid numeric literal**: Tokenize what's valid, report error

```python
# Example: invalid character recovery
def handle_invalid_char(c):
    report_error(f"Invalid character: {repr(c)}")
    advance()  # Skip the invalid character
    # Continue lexing from next character
```

## Position Tracking

Each token should include:

```
Token {
    type: TokenType
    value: string           # Raw text of token
    line: int               # 1-indexed line number
    column: int             # 0-indexed column (in characters)
    offset: int             # 0-indexed byte offset in source
    length: int             # Length in bytes
}
```

For error messages, positions should be reported as `line:column` (both 1-indexed for display).

## Implementation Notes

1. **UTF-8 handling**: Source files are UTF-8. Column positions should account for multi-byte characters.

2. **BOM handling**: Skip UTF-8 BOM (0xEF 0xBB 0xBF) if present at start of file.

3. **Line endings**: Accept `\n`, `\r\n`, or `\r`. Normalize to `\n` internally.

4. **Maximum nesting**: Limit f-string nesting depth (e.g., 4 levels) to prevent stack overflow.

5. **Token buffer**: For lookahead, buffer up to 2 tokens rather than peeking into source repeatedly.

6. **Interning**: Consider interning identifier strings for faster comparison in later phases.

*Implementation*
- *The actual Sharpy lexer is in `src/Sharpy.Compiler/Lexer/`. This document describes the specification; implementation may differ in details.*
