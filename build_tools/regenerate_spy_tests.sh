#!/usr/bin/env bash
# Regenerate C# from .spy stdlib test modules.
#
# Usage:
#   bash build_tools/regenerate_spy_tests.sh            # Regenerate all in-place
#   bash build_tools/regenerate_spy_tests.sh --check     # Diff against committed (CI mode)
#   bash build_tools/regenerate_spy_tests.sh --dry-run   # Show what would be regenerated
#
# Uses project compilation (sharpyc project tests.spyproj --emit-cs-to) to emit
# all test modules in one pass, then syncs the output into Spy/generated/.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
SPY_DIR="$REPO_ROOT/src/Sharpy.Stdlib.Tests/Spy"
GENERATED_DIR="$SPY_DIR/generated"
SPYPROJ="$SPY_DIR/tests.spyproj"
SHARPYC="dotnet run --project $REPO_ROOT/src/Sharpy.Cli --"
WORK_DIR=""

cleanup() {
    if [[ -n "$WORK_DIR" && -d "$WORK_DIR" ]]; then
        rm -rf "$WORK_DIR"
    fi
}
trap cleanup EXIT

mode="regenerate"
if [[ "${1:-}" == "--check" ]]; then
    mode="check"
elif [[ "${1:-}" == "--dry-run" ]]; then
    mode="dry-run"
fi

# Count .spy source files
spy_count=$(find "$SPY_DIR" -name '*.spy' -not -path '*/generated/*' | wc -l | tr -d ' ')

if [[ "$spy_count" -eq 0 ]]; then
    echo "No .spy files found in $SPY_DIR"
    if [[ "$mode" == "check" ]]; then
        # No sources means nothing to check — not stale
        echo "OK: no .spy test sources to check"
        exit 0
    fi
    exit 0
fi

if [[ "$mode" == "dry-run" ]]; then
    echo "Would emit $spy_count .spy files via: sharpyc project tests.spyproj --emit-cs-to <tmpdir>"
    find "$SPY_DIR" -name '*.spy' -not -path '*/generated/*' | sort | while read -r f; do
        stem=$(basename "$f" .spy)
        echo "  $f -> generated/${stem}.cs"
    done
    exit 0
fi

WORK_DIR="$(mktemp -d)"
EMIT_DIR="$WORK_DIR/emitted"
mkdir -p "$EMIT_DIR"

# --- Emit all test modules in one project compilation pass ---

echo "Emitting .spy test modules via project compilation..."
# The project compilation will fail at the C# compilation stage because the
# spyproj doesn't reference xUnit packages. That's expected — we only need
# the emitted C# files, which are saved before compilation is attempted.
# Files excluded in the spyproj (via Exclude=) are not compiled and won't be emitted.
$SHARPYC project "$SPYPROJ" --emit-cs-to "$EMIT_DIR" 2>/dev/null || true
echo "Project emission done."
echo ""

# Verify at least some files were emitted
emitted_count=$(find "$EMIT_DIR" -name '*.cs' | wc -l | tr -d ' ')
if [[ "$emitted_count" -eq 0 ]]; then
    echo "ERROR: No .cs files were emitted. The Sharpy compilation may have failed."
    echo "Run manually to see diagnostics:"
    echo "  dotnet run --project src/Sharpy.Cli -- project $SPYPROJ"
    exit 1
fi

echo "Emitted $emitted_count files."

# Build stems array from the emitted .cs files (respects spyproj Exclude patterns)
stems=()
while IFS= read -r f; do
    stem=$(basename "$f" .cs)
    stems+=("$stem")
done < <(find "$EMIT_DIR" -name '*.cs' | sort)

# --- Post-process and sync ---

# Escape REPO_ROOT once for safe use as a sed literal in #line path rewriting
# (guards against regex metacharacters such as '[' in directory names).
escaped_root=$(printf '%s' "$REPO_ROOT" | sed 's/[[\.*^$()+?{|]/\\&/g')

errors=0

for stem in "${stems[@]}"; do
    emitted_file="$EMIT_DIR/${stem}.cs"
    if [[ ! -f "$emitted_file" ]]; then
        echo "ERROR: Expected emitted file not found: ${stem}.cs"
        errors=1
        continue
    fi
    # Post-process: normalize CRLF→LF, strip trailing whitespace,
    # and normalize absolute repo paths in #line directives to repo-relative paths
    # so the generated files are stable across machines.
    final_file="$WORK_DIR/${stem}_final.cs"
    {
        echo "// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly."
        echo "// To regenerate: bash build_tools/regenerate_spy_tests.sh"
        tr -d '\r' < "$emitted_file" \
            | sed 's/[[:space:]]*$//' \
            | sed "/^#line /s|\"${escaped_root}/|\"|g"
    } > "$final_file"

    # Ensure file ends with a newline
    if [ -n "$(tail -c 1 "$final_file")" ]; then
        echo "" >> "$final_file"
    fi

    cs_target="$GENERATED_DIR/${stem}.cs"

    if [[ "$mode" == "check" ]]; then
        if [[ ! -f "$cs_target" ]]; then
            echo "FAIL: $cs_target not found (expected generated C# for ${stem}.spy)"
            errors=1
            continue
        fi
        if ! diff -u "$cs_target" "$final_file" --label "committed: generated/${stem}.cs" --label "generated: generated/${stem}.cs" > /dev/null 2>&1; then
            diff -u "$cs_target" "$final_file" --label "committed: generated/${stem}.cs" --label "generated: generated/${stem}.cs" || true
            echo "STALE: generated/${stem}.cs does not match generated output"
            errors=1
        else
            echo "OK: generated/${stem}.cs is up-to-date"
        fi
    else
        cp "$final_file" "$cs_target"
        echo "Regenerated: generated/${stem}.cs"
    fi
done

# --- Orphan detection: delete generated .cs files whose .spy source is gone ---

if [[ "$mode" != "check" ]]; then
    for cs_file in "$GENERATED_DIR"/*.cs; do
        [[ -f "$cs_file" ]] || continue
        stem=$(basename "$cs_file" .cs)
        if ! printf '%s\n' "${stems[@]}" | grep -qx "$stem"; then
            echo "Removing orphan: generated/${stem}.cs"
            rm "$cs_file"
        fi
    done
elif [[ -d "$GENERATED_DIR" ]]; then
    for cs_file in "$GENERATED_DIR"/*.cs; do
        [[ -f "$cs_file" ]] || continue
        stem=$(basename "$cs_file" .cs)
        if ! printf '%s\n' "${stems[@]}" | grep -qx "$stem"; then
            echo "ORPHAN: generated/${stem}.cs has no corresponding .spy source"
            errors=1
        fi
    done
fi

if [[ "$errors" -ne 0 ]]; then
    echo ""
    echo "FAILED: One or more files are stale or missing."
    exit 1
fi

echo ""
echo "Done. $emitted_count files processed."
