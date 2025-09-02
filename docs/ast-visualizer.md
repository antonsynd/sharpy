# Sharpy AST Visualizer

A tool for generating visual representations of Sharpy Abstract Syntax Trees (ASTs) as PNG diagrams.

## Overview

The Sharpy AST Visualizer parses Sharpy source files and generates graphical representations of their AST structure using Graphviz. This tool is useful for:

- Understanding how Sharpy code is parsed and structured
- Debugging parser issues
- Educational purposes to visualize language constructs
- Analyzing complex expressions and lambda functions

## Prerequisites

- Rust toolchain (for building)
- Graphviz installed on your system (for PNG generation)
  - macOS: `brew install graphviz`
  - Ubuntu/Debian: `sudo apt-get install graphviz`
  - Windows: Download from [graphviz.org](https://graphviz.org/download/)

## Building

The AST visualizer is built as part of the Sharpy Rust project:

```bash
cd rust/
cargo build --bin sharpy-ast-visualizer
```

## Usage

### Command Line Interface

```bash
./rust/target/debug/sharpy-ast-visualizer --input <INPUT_FILE> [OPTIONS]
```

### Options

- `--input <INPUT>` - Input Sharpy source file (required)
- `--output-dir <DIR>` - Output directory for files (default: current directory)
- `--basename <NAME>` - Base name for output files (default: input file stem)
- `--keep-temp` - Keep temporary DOT files
- `--debug` - Enable debug output
- `--filter-nodes <TYPES>` - Render only specific node types (comma-separated)
- `--dot-only` - Skip PNG rendering and output only DOT format
- `--help` - Show help message

### Helper Script

For convenience, use the provided helper script:

```bash
./build_tools/visualize_ast.sh <input_file> [options]
```

The helper script automatically builds the tool if needed and can open the generated PNG on macOS.

## Examples

### Basic Usage

Generate a PNG visualization of a Sharpy file:

```bash
./build_tools/visualize_ast.sh example.spy
```

### DOT Output Only

Generate only the DOT format (useful for debugging or custom processing):

```bash
./build_tools/visualize_ast.sh example.spy --dot-only
```

### Filtering Nodes

Show only specific node types (useful for focusing on particular language constructs):

```bash
./build_tools/visualize_ast.sh example.spy --filter-nodes "Lambda,BinaryOp"
```

### Debug Mode

Enable debug output to see parsing details:

```bash
./build_tools/visualize_ast.sh example.spy --debug
```

## Node Types and Visualization

The visualizer uses different colors and shapes for different AST node types:

### Colors
- **Light Blue**: Lambda expressions
- **Light Green**: Binary/Unary operations, comparisons
- **Light Yellow**: Constants
- **Light Pink**: Names and typed names
- **Light Cyan**: Collections (Dict, Set, List, Tuple)
- **Light Gray**: Function calls, attributes, subscripts
- **White**: Other nodes

### Shapes
- **Ellipse**: Lambda expressions
- **Diamond**: Operations and comparisons
- **Box**: Names, constants, and general nodes
- **Hexagon**: Function calls
- **Plain Text**: Filtered placeholder nodes

## Example Sharpy Code and AST

Given this Sharpy code:
```python
x = 5
y = x + 10
f = lambda a: a * 2
result = f(y)
```

The visualizer generates an AST showing:
- Module with 4 statements
- Assignment nodes linking variables to values
- Binary operations (addition, multiplication)
- Lambda expression with parameter and body
- Function call connecting the lambda to its argument

## Output Files

- `<basename>.dot` - Graphviz DOT format file
- `<basename>.png` - PNG image of the AST (if Graphviz is available)

## Troubleshooting

### Parsing Errors

If you encounter parsing errors:
1. Check that your Sharpy code is syntactically correct
2. Try with a simpler example first
3. Use `--debug` to see detailed parsing output
4. Note that comments are not currently supported in all cases

### Missing PNG Output

If only DOT files are generated:
1. Ensure Graphviz is installed: `dot -V`
2. Check that the `dot` command is in your PATH
3. Use the DOT file manually: `dot -Tpng input.dot -o output.png`

### Build Issues

If the tool fails to build:
1. Ensure you're using a recent Rust toolchain
2. Check that all dependencies are available
3. Try a clean build: `cargo clean && cargo build`

## Integration with Development Workflow

The AST visualizer integrates well with Sharpy development:

1. **Parser Development**: Visualize how parser changes affect AST structure
2. **Language Design**: See how new language constructs are represented
3. **Debugging**: Understand unexpected parsing behavior
4. **Documentation**: Generate diagrams for language documentation

## Technical Details

The visualizer consists of:
- `src/bin/ast_visualizer.rs` - Main CLI application
- `src/bin/ast_renderer.rs` - AST traversal and DOT generation
- Uses the `dot` crate for Graphviz integration
- Leverages the existing Sharpy lexer and parser

The tool performs safe AST traversal using pointer-based navigation to avoid Rust borrowing issues while maintaining memory safety.
