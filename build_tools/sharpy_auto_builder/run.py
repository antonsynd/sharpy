#!/usr/bin/env python3
"""
Sharpy Auto Builder - Main Entry Point

This script automates the implementation of Sharpy compiler tasks using
GitHub Copilot CLI or Claude Code, with validation agents to ensure
spec adherence and quality.

Usage:
    # Initialize (first time)
    python run.py init

    # Check status
    python run.py status

    # Run implementation
    python run.py run --max-tasks 5

    # Generate report
    python run.py report -o status_report.md

    # Answer a question
    python run.py answer q_20250111_abc123 "Use approach B"

    # Submit review
    python run.py review r_20250111_def456 approved --notes "Looks good"

For more options:
    python run.py --help
    python run.py run --help
"""

import sys
from pathlib import Path

# Add the parent directory to path so we can import the package
script_dir = Path(__file__).parent
sys.path.insert(0, str(script_dir.parent))

from sharpy_auto_builder.cli import main

if __name__ == "__main__":
    main()
