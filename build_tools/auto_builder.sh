#!/bin/bash
#
# Sharpy Auto Builder - Shell Wrapper
#
# This script provides a convenient way to run the Sharpy Auto Builder
# with proper Python environment setup.
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Check for Python
if command -v python3 &> /dev/null; then
    PYTHON=python3
elif command -v python &> /dev/null; then
    PYTHON=python
else
    echo "Error: Python not found"
    exit 1
fi

# Check if virtual environment exists, create if not
VENV_DIR="$SCRIPT_DIR/.venv"
if [ ! -d "$VENV_DIR" ]; then
    echo "Creating virtual environment..."
    $PYTHON -m venv "$VENV_DIR"

    echo "Installing dependencies..."
    source "$VENV_DIR/bin/activate"
    pip install -q -r "$SCRIPT_DIR/sharpy_auto_builder/requirements.txt"
else
    source "$VENV_DIR/bin/activate"
fi

# Run the CLI
cd "$PROJECT_ROOT"
$PYTHON "$SCRIPT_DIR/sharpy_auto_builder/run.py" "$@"
