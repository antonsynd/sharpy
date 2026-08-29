"""Census and cleanup of crash bundles and stray .spy files under fixture bin/ directories.

Usage:
    python -m build_tools clean-fixture-bins          # dry-run census (default)
    python -m build_tools clean-fixture-bins --clean   # remove the found items
"""

import os
import shutil
from pathlib import Path

import click


def find_fixture_bin_dirs(root: Path) -> list[Path]:
    result = []
    for dirpath, dirnames, _ in os.walk(root):
        dp = Path(dirpath)
        if dp.name == "bin" and "TestFixtures" in dp.parts:
            result.append(dp)
            dirnames.clear()
    return result


def find_crash_bundles(bin_dirs: list[Path]) -> list[Path]:
    bundles = []
    for bd in bin_dirs:
        for dirpath, dirnames, _ in os.walk(bd):
            if Path(dirpath).name == ".sharpy-crash":
                bundles.append(Path(dirpath))
                dirnames.clear()
    return bundles


def find_stray_spy_files(bin_dirs: list[Path]) -> list[Path]:
    stray = []
    for bd in bin_dirs:
        for dirpath, _, filenames in os.walk(bd):
            for f in filenames:
                if f.endswith(".spy"):
                    stray.append(Path(dirpath) / f)
    return stray


@click.command("clean-fixture-bins")
@click.option("--clean", is_flag=True, help="Remove found items (default: dry-run census)")
def clean_fixture_bins(clean: bool) -> None:
    """Census and cleanup of crash bundles and stray .spy under fixture bin/."""
    src_root = Path(__file__).resolve().parent.parent / "src"

    bin_dirs = find_fixture_bin_dirs(src_root)
    bundles = find_crash_bundles(bin_dirs)
    stray = find_stray_spy_files(bin_dirs)

    click.echo(f"Fixture bin/ directories: {len(bin_dirs)}")
    click.echo(f"Crash bundles (.sharpy-crash): {len(bundles)}")
    click.echo(f"Stray .spy files: {len(stray)}")

    if bundles:
        click.echo("\nCrash bundles:")
        for b in sorted(bundles):
            report_count = len(list(b.glob("**/report.md")))
            click.echo(f"  {b.relative_to(src_root)}  ({report_count} reports)")

    if stray:
        click.echo(f"\nStray .spy files (showing first 20 of {len(stray)}):")
        for s in sorted(stray)[:20]:
            click.echo(f"  {s.relative_to(src_root)}")

    if clean:
        removed_bundles = 0
        removed_spy = 0
        for b in bundles:
            shutil.rmtree(b)
            removed_bundles += 1
        for s in stray:
            s.unlink()
            removed_spy += 1
        click.echo(f"\nRemoved {removed_bundles} crash bundles, {removed_spy} stray .spy files")
    elif bundles or stray:
        click.echo("\nDry run — pass --clean to remove")
