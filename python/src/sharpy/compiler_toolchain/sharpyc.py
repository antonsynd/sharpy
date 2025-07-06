#!/usr/bin/env python3
import argparse
from pathlib import Path
from typing import Sequence


def main() -> None:
    args: argparse.Namespace = parse_args()
    inputs: Sequence = args.input
    output: Path = args.output


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="The Sharpy compiler.")
    parser.add_argument("-i", "--input", type=Path, action="append", required=True)
    parser.add_argument("-o", "--output", type=Path, required=True)
    parser.add_argument("-v", "--verbose", action="store_true", help="Enable verbose output.")
    parser.add_argument("--version", action="store_true", help="Show version information.")

    return parser.parse_args()


if __name__ == "__main__":
    main()
