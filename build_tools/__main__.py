"""
Sharpy Build Tools - Main module entry point.

This allows running build_tools as a module:
    python -m build_tools <command> [options]
"""

from .cli import main

if __name__ == "__main__":
    main()
