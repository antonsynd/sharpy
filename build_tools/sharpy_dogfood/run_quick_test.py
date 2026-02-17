#!/usr/bin/env python3
"""
Quick single-iteration dogfood test.

Usage:
    python run_quick_test.py                    # Random feature/complexity
    python run_quick_test.py functions simple   # Specific feature and complexity
"""

import asyncio
import sys
import os
from pathlib import Path

# Add the build_tools directory to the path
script_dir = os.path.dirname(os.path.abspath(__file__))
build_tools_dir = os.path.dirname(script_dir)
sys.path.insert(0, build_tools_dir)

from sharpy_dogfood.config import Config
from sharpy_dogfood.orchestrator import (
    DogfoodOrchestrator,
    IterationStatus,
    FEATURE_FOCUSES,
    COMPLEXITY_LEVELS,
)
import random


async def run_quick_test(feature: str = None, complexity: str = None):
    """Run a single quick dogfood iteration."""

    # Use provided values or random
    feature = feature or random.choice(FEATURE_FOCUSES)
    complexity = complexity or random.choice(COMPLEXITY_LEVELS)

    print(f"Quick Dogfood Test: {feature} ({complexity})")
    print("=" * 50)

    config = Config()
    config.max_iterations = 1
    config.generation_timeout = 120.0  # Shorter timeout for quick test
    config.ensure_dirs()

    orchestrator = DogfoodOrchestrator(config)

    if not await orchestrator.initialize():
        print("Failed to initialize", file=sys.stderr)
        return 1

    result = await orchestrator.run_iteration(1, feature, complexity)

    if result.status == IterationStatus.SUCCESS:
        print("\n✓ Test passed!")
        return 0
    else:
        print(f"\n✗ Test failed")
        if result.issue_dir:
            print(f"  Issue report: {result.issue_dir}")
        return 1


def main():
    feature = sys.argv[1] if len(sys.argv) > 1 else None
    complexity = sys.argv[2] if len(sys.argv) > 2 else None

    # Validate arguments
    if feature and feature not in FEATURE_FOCUSES:
        print(f"Unknown feature: {feature}")
        print(f"Available: {', '.join(FEATURE_FOCUSES)}")
        sys.exit(1)

    if complexity and complexity not in COMPLEXITY_LEVELS:
        print(f"Unknown complexity: {complexity}")
        print(f"Available: {', '.join(COMPLEXITY_LEVELS)}")
        sys.exit(1)

    try:
        exit_code = asyncio.run(run_quick_test(feature, complexity))
        sys.exit(exit_code)
    except KeyboardInterrupt:
        print("\nInterrupted")
        sys.exit(130)


if __name__ == "__main__":
    main()
