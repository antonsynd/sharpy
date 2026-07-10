"""Module entry point: ``python -m build_tools.cpython_oracle ...``."""

import sys

from .cli import main

if __name__ == "__main__":
    sys.exit(main())
