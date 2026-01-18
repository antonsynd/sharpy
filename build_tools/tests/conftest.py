"""
Pytest configuration for build_tools tests.

Adds the project root to sys.path so that imports like
`from build_tools.shared...` work correctly.
"""

import sys
from pathlib import Path

# Add the sharpy project root to the Python path
# This allows imports like `from build_tools.shared...` to work
project_root = Path(__file__).parent.parent.parent
if str(project_root) not in sys.path:
    sys.path.insert(0, str(project_root))
