#!/bin/bash
#
# Sharpy Auto Builder - Shell Wrapper
#
# This script provides a convenient way to run the Sharpy Auto Builder.
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Check for Python - prefer conda environment
if [ -n "$CONDA_PREFIX" ] && [ -x "$CONDA_PREFIX/bin/python3" ]; then
    PYTHON="$CONDA_PREFIX/bin/python3"
elif [ -x "/opt/homebrew/anaconda3/bin/python3" ]; then
    PYTHON="/opt/homebrew/anaconda3/bin/python3"
elif command -v python3 &> /dev/null; then
    PYTHON=python3
elif command -v python &> /dev/null; then
    PYTHON=python
else
    echo "Error: Python not found"
    exit 1
fi

# Run the CLI
cd "$PROJECT_ROOT"
$PYTHON "$SCRIPT_DIR/sharpy_auto_builder/run.py" "$@"
