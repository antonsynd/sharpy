#!/usr/bin/env python3

import subprocess
import sys
import tempfile
import os
import json

def test_access_modifiers():
    """Test access modifier parsing behavior"""

    test_cases = [
        # Test explicit keywords
        ("public def test(): pass", "public"),
        ("protected def test(): pass", "protected"),
        ("private def test(): pass", "private"),
        ("internal def test(): pass", "internal"),
        ("file def test(): pass", "file"),

        # Test underscore prefixes (should take precedence)
        ("def _test(): pass", "protected"),
        ("def __test(): pass", "private"),
        ("public def _test(): pass", "protected"),  # prefix wins
        ("private def __test(): pass", "private"),   # prefix wins

        # Test no modifier (should be public)
        ("def test(): pass", None),  # None means public (default)
    ]

    for sharpy_code, expected_access in test_cases:
        print(f"\nTesting: {sharpy_code}")
        print(f"Expected access: {expected_access}")

        # Create temporary file
        with tempfile.NamedTemporaryFile(mode='w', suffix='.spy', delete=False) as f:
            f.write(sharpy_code)
            temp_file = f.name

        try:
            # Run the AST visualizer which should parse the code
            result = subprocess.run([
                'cargo', 'run', '--bin', 'sharpy-ast-visualizer', '--',
                '--input', temp_file
            ], cwd='/Users/anton/Documents/github/sharpy/rust', capture_output=True, text=True)

            if result.returncode == 0:
                print("✅ Parsed successfully")
            else:
                print(f"❌ Parse error: {result.stderr}")

        finally:
            os.unlink(temp_file)

if __name__ == "__main__":
    test_access_modifiers()
