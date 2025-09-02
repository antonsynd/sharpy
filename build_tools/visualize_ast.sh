#!/bin/bash

# Sharpy AST Visualizer Helper Script
# Usage: visualize_ast.sh <input_file> [options]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RUST_DIR="$PROJECT_ROOT/rust"
AST_VISUALIZER="$RUST_DIR/target/debug/sharpy-ast-visualizer"

# Check if the tool is built
if [ ! -f "$AST_VISUALIZER" ]; then
    echo "Building AST visualizer..."
    cd "$RUST_DIR"
    cargo build --bin sharpy-ast-visualizer
fi

# Check if input file is provided
if [ $# -eq 0 ]; then
    echo "Usage: $0 <input_file> [options]"
    echo "Examples:"
    echo "  $0 test.spy                          # Generate PNG"
    echo "  $0 test.spy --dot-only               # Generate DOT only"
    echo "  $0 test.spy --filter-nodes Lambda    # Show only Lambda nodes"
    echo "  $0 test.spy --debug                  # Enable debug output"
    echo ""
    echo "Available options:"
    "$AST_VISUALIZER" --help
    exit 1
fi

INPUT_FILE="$1"
shift

# Check if input file exists
if [ ! -f "$INPUT_FILE" ]; then
    echo "Error: Input file '$INPUT_FILE' not found"
    exit 1
fi

echo "Visualizing AST for: $INPUT_FILE"
"$AST_VISUALIZER" --input "$INPUT_FILE" "$@"

# If PNG was generated, report it
if [ ! "$*" == *"--dot-only"* ]; then
    BASENAME=$(basename "$INPUT_FILE" .spy)
    PNG_FILE="$BASENAME.png"
    if [ -f "$PNG_FILE" ]; then
        echo "✅ AST visualization generated: $PNG_FILE"

        # Try to open the image if we're on macOS
        if command -v open >/dev/null 2>&1; then
            echo "Opening image..."
            open "$PNG_FILE"
        fi
    fi
fi
