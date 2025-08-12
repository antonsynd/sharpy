# Sharpy Lexer Code Flow Documentation

This document provides a comprehensive overview of the Rust-based lexer implementation for the Sharpy programming language.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Entry Points](#entry-points)
3. [Core Components](#core-components)
4. [Token Processing Flow](#token-processing-flow)
5. [Key Data Structures](#key-data-structures)
6. [Error Handling](#error-handling)
7. [Testing Structure](#testing-structure)

## Architecture Overview

The lexer is organized into a modular architecture with clear separation of concerns:

```
rust/src/
├── lib.rs              # Public API exports
├── main.rs             # CLI entry point
├── lexer/              # Core lexing logic
│   ├── mod.rs          # Main lexer orchestration
│   ├── token.rs        # Token type definitions
│   ├── scanner.rs      # Low-level character scanning
│   ├── string_lexer.rs # String literal parsing
│   ├── number_lexer.rs # Numeric literal parsing
│   ├── keyword.rs      # Keyword recognition
│   ├── indent.rs       # Indentation handling
│   └── error.rs        # Error types
└── utils/              # Utility modules
    ├── position.rs     # Source location tracking
    └── unicode.rs      # Unicode validation
```

## Entry Points

### CLI Application (`main.rs`)
The main executable provides a command-line interface for the Sharpy compiler toolchain:

```rust
sharpyc [INPUT_FILE] [OPTIONS]
  --tokenize    # Tokenize only (lexer test mode)
  --verbose     # Verbose output showing token details
```

**Flow:**
1. Parse command-line arguments using `clap`
2. Read input from file or stdin
3. If `--tokenize` flag is set, run lexer and output tokens
4. Otherwise, show "compilation not yet implemented" message

### Library API (`lib.rs`)
Exports the core lexer components for use by other Rust code:
- `SharpyLexer` - Main lexer struct
- `TokenType` - Enum of all token types
- `Token` - Individual token with metadata
- `LexerError` - Error handling

## Core Components

### 1. SharpyLexer (`lexer/mod.rs`)

The main orchestrator that coordinates all lexing operations.

**Key State:**
- `scanner`: Low-level character processing
- `indent_handler`: Manages Python-style indentation
- `pending_tokens`: Queue for multi-token operations
- `opened_parens`: Tracks parentheses for line continuation
- `at_line_start`: Indentation context flag
- `errors`: Accumulated lexing errors

**Main Methods:**
- `new(input: &str)` - Initialize lexer with source code
- `next_token()` - Get next token from input
- `tokenize_all()` - Process entire input into token vector
- `scan_next_token()` - Core tokenization logic

**Processing Flow:**
```
Input → Character Scanning → Token Recognition → Indentation Handling → Token Queue → Output
```

### 2. Scanner (`lexer/scanner.rs`)

Low-level character-by-character processing and token recognition.

**Responsibilities:**
- Character stream management with lookahead
- Source location tracking (line/column)
- Whitespace and comment handling
- Basic token pattern recognition
- Delegation to specialized lexers

**Key Methods:**
- `advance()` - Move to next character
- `peek_char()` - Look ahead without consuming
- `scan_identifier()` - Process names and keywords
- `scan_operator()` - Handle operators and punctuation
- `skip_whitespace()` - Handle non-significant whitespace

### 3. Token System (`lexer/token.rs`)

Comprehensive token type definitions covering all Sharpy language constructs.

**Token Categories:**
- **Literals**: Numbers, strings, f-strings
- **Keywords**: Language reserved words (50+ variants)
- **Operators**: Arithmetic, comparison, logical, bitwise
- **Punctuation**: Parentheses, brackets, delimiters
- **Special**: Indentation, newlines, EOF, comments

**Sharpy-Specific Features:**
- Null-aware operators: `?.` (QuestionDot), `??` (DoubleQuestion)
- Access modifiers: `_protected`, `__private`, `` `internal``, ``` ``file```
- Enhanced string literals with multiple prefix types

### 4. Specialized Lexers

#### String Lexer (`lexer/string_lexer.rs`)
Handles all string literal variants:
- Regular strings: `"hello"`, `'world'`
- Raw strings: `r"no\escape"`
- Byte strings: `b"bytes"`
- F-strings: `f"Hello {name}"`
- Unicode strings: `u"unicode"`
- Multiline strings with triple quotes

**Processing:**
1. Detect string prefix (r, b, f, u combinations)
2. Parse quote style (single, double, triple)
3. Handle escape sequences
4. Manage f-string expression parsing

#### Number Lexer (`lexer/number_lexer.rs`)
Parses all numeric literal formats:
- Decimal: `42`, `3.14`, `2.5e10`
- Binary: `0b1010`
- Octal: `0o755`
- Hexadecimal: `0xFF`
- Complex/Imaginary: `1j`, `2+3j`

#### Keyword Recognition (`lexer/keyword.rs`)
Maps identifier strings to keyword tokens using efficient lookup.

#### Indentation Handler (`lexer/indent.rs`)
Manages Python-style significant indentation:
- Tracks indentation stack
- Generates INDENT/DEDENT tokens
- Handles mixed tabs/spaces
- Manages line continuation in parentheses

## Token Processing Flow

### High-Level Flow
```
Source Code
    ↓
Character Stream (Scanner)
    ↓
Token Recognition
    ↓
Indentation Processing
    ↓
Token Queue Management
    ↓
Final Token Stream
```

### Detailed Token Processing

1. **Line Start Processing**
   - Check for indentation changes
   - Generate INDENT/DEDENT tokens as needed
   - Skip non-significant whitespace

2. **Character Classification**
   ```rust
   match current_char {
       None => handle_eof(),
       '\n' => handle_newline(),
       '#' => scan_comment(),
       '0'..='9' => scan_number(),
       '"' | '\'' => scan_string(),
       'a'..='z' | 'A'..='Z' | '_' | '`' | '$' => scan_identifier(),
       _ => scan_operator(),
   }
   ```

3. **String Prefix Disambiguation**
   - Special logic for characters like `r`, `f`, `b` that could be:
     - String prefixes: `r"raw string"`
     - Regular identifiers: `return`, `for`, `break`
   - Uses lookahead to check for following quotes

4. **Token Finalization**
   - Apply keyword mapping for identifiers
   - Set source location metadata
   - Handle multi-character operators
   - Queue additional tokens (like DEDENT at EOF)

## Key Data Structures

### Token
```rust
struct Token {
    token_type: TokenType,    // What kind of token
    lexeme: String,           // Original text
    location: SourceLocation, // Where in source
}
```

### SourceLocation
```rust
struct SourceLocation {
    line: usize,     // 1-based line number
    column: usize,   // 1-based column number
    position: usize, // 0-based byte offset
}
```

### TokenType Highlights
```rust
enum TokenType {
    // Sharpy-specific operators
    QuestionDot,      // ?.
    DoubleQuestion,   // ??

    // Enhanced literals
    Number(NumberType),   // int, float, complex, etc.
    String(StringType),   // regular, raw, bytes, etc.
    FString(FStringPart), // f-string components

    // Access control
    Name(AccessModifier), // public, protected, private, etc.

    // Indentation
    Indent, Dedent, Newline,
}
```

## Error Handling

### Error Types (`lexer/error.rs`)
- **UnterminatedString**: Missing closing quote
- **InvalidNumber**: Malformed numeric literals
- **InvalidCharacter**: Unrecognized characters
- **IndentationError**: Mixed tabs/spaces or inconsistent indentation
- **UnexpectedEof**: Premature end of input

### Error Recovery
- Lexer continues after errors when possible
- Accumulates multiple errors for batch reporting
- Provides detailed location information for debugging

## Testing Structure

### Unit Tests (`lexer/mod.rs`)
1. **Basic Tokenization**: Simple assignments and literals
2. **Keywords**: Language keyword recognition
3. **Access Modifiers**: Sharpy-specific identifier prefixes
4. **Operators**: All operator variants including Sharpy-specific ones
5. **Numbers**: All numeric literal formats
6. **Indentation**: Python-style INDENT/DEDENT generation

### Test Coverage
- ✅ Basic token types (identifiers, numbers, strings)
- ✅ All keywords and operators
- ✅ Access modifier recognition
- ✅ Indentation handling
- ✅ Error cases and recovery
- ✅ Complex real-world code samples

### Running Tests
```bash
cd rust/
cargo test              # Run all tests
cargo test -- --nocapture  # With output
```

## Usage Examples

### Command Line
```bash
# Tokenize a file with verbose output
./target/release/sharpyc examples/hello.spy --tokenize --verbose

# Process from stdin
echo "x = 42" | ./target/release/sharpyc --tokenize
```

### Library Usage
```rust
use sharpy_compiler_toolchain::SharpyLexer;

let mut lexer = SharpyLexer::new("x = 42");
let tokens = lexer.tokenize_all().unwrap();

for token in tokens {
    println!("{:?}: '{}'", token.token_type, token.lexeme);
}
```

## Performance Characteristics

- **Memory**: Linear in input size with small constant factors
- **Time**: O(n) where n is input length
- **Streaming**: Supports incremental token generation
- **Unicode**: Full UTF-8 support with proper character handling

## Future Considerations

1. **Parser Integration**: Token stream ready for recursive descent parser
2. **Error Recovery**: Enhanced error recovery for IDE integration
3. **Performance**: Potential optimizations for large files
4. **LSP Integration**: Language server protocol support foundations

---

*This documentation reflects the lexer implementation as of the completion of the tokenization phase. The lexer successfully handles all Sharpy language constructs and serves as the foundation for the next compiler phases.*
