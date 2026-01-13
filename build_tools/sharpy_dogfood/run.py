#!/usr/bin/env python3
"""
Run the Sharpy dogfooding tool.

This is a convenience wrapper that can be invoked directly.
"""

import sys
import os

# Add the build_tools directory to the path
script_dir = os.path.dirname(os.path.abspath(__file__))
build_tools_dir = os.path.dirname(script_dir)
sys.path.insert(0, build_tools_dir)

from sharpy_dogfood.cli import cli_main

if __name__ == "__main__":
    cli_main()
