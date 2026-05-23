#!/usr/bin/env bash
# Regenerate C# from .spy stdlib modules.
#
# Usage:
#   bash build_tools/regenerate_spy_stdlib.sh           # Regenerate all in-place
#   bash build_tools/regenerate_spy_stdlib.sh --check    # Diff against committed (CI mode)
#   bash build_tools/regenerate_spy_stdlib.sh --dry-run  # Show what would be regenerated
#
# Two-pass design: all files are emitted to a temp directory first, then
# applied to the tree (or diffed). This avoids cascading build failures
# since Sharpy.Cli has a build-order dependency on Sharpy.Stdlib.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
STDLIB_DIR="$REPO_ROOT/src/Sharpy.Stdlib"
SPY_DIR="$STDLIB_DIR/spy"
SHARPYC="dotnet run --project $REPO_ROOT/src/Sharpy.Cli --"
WORK_DIR=""

cleanup() {
    if [[ -n "$WORK_DIR" && -d "$WORK_DIR" ]]; then
        rm -rf "$WORK_DIR"
    fi
}
trap cleanup EXIT

# Mapping: spy_name:cs_relative_path:extra_flags
# All modules use -n Sharpy to emit into the Sharpy namespace (matching __Init__.cs).
MODULES=(
    "textwrap:Textwrap/Textwrap.cs:-n Sharpy"
    "bisect_module:Bisect/Bisect.cs:-n Sharpy"
    "statistics:Statistics/Statistics.cs:-n Sharpy"
    "heapq:Heapq/Heapq.cs:-n Sharpy"
)

mode="regenerate"
if [[ "${1:-}" == "--check" ]]; then
    mode="check"
elif [[ "${1:-}" == "--dry-run" ]]; then
    mode="dry-run"
fi

if [[ "$mode" == "dry-run" ]]; then
    for entry in "${MODULES[@]}"; do
        IFS=':' read -r spy_name cs_rel extra_flags <<< "$entry"
        flags="-t library"
        if [[ -n "$extra_flags" ]]; then flags="$flags $extra_flags"; fi
        echo "Would regenerate: $SPY_DIR/${spy_name}.spy -> $STDLIB_DIR/${cs_rel} (flags: $flags)"
    done
    exit 0
fi

WORK_DIR="$(mktemp -d)"
errors=0
emitted=()

# --- Pass 1: Emit all files to temp directory ---

for entry in "${MODULES[@]}"; do
    IFS=':' read -r spy_name cs_rel extra_flags <<< "$entry"
    spy_file="$SPY_DIR/${spy_name}.spy"

    if [[ ! -f "$spy_file" ]]; then
        echo "ERROR: $spy_file not found"
        errors=1
        continue
    fi

    flags="-t library"
    if [[ -n "$extra_flags" ]]; then
        flags="$flags $extra_flags"
    fi

    header_cmd="sharpyc emit csharp src/Sharpy.Stdlib/spy/${spy_name}.spy $flags"
    header="// Generated from src/Sharpy.Stdlib/spy/${spy_name}.spy — do not edit directly.
// To regenerate: $header_cmd"

    tmp_file="$WORK_DIR/${spy_name}.cs"
    if ! $SHARPYC emit csharp "$spy_file" $flags -o "$tmp_file" 2>/dev/null; then
        echo "ERROR: sharpyc emit csharp failed for $spy_file"
        errors=1
        continue
    fi

    # Strip auto-generated [SharpyModule] attribute — __Init__.cs is the source of truth
    # (the compiler derives the module name from the filename, which may differ from the
    # actual module name, e.g. bisect_module.spy -> "bisect_module" vs correct "bisect").
    sed -i '' '/\[global::Sharpy\.SharpyModule(/d' "$tmp_file"

    # BUG(#690): -n flag produces "new Sharpy.System.X" instead of "new System.X"
    sed -i '' 's/new Sharpy\.System\./new global::System./g' "$tmp_file"

    # BUG(#690): Using alias "Math = global::System.Math" is shadowed by Sharpy.Math.
    # Remove the alias and replace bare Math.X calls with fully qualified names.
    sed -i '' '/using Math = global::System\.Math;/d' "$tmp_file"
    sed -i '' 's/ Math\.Min(/ global::System.Math.Min(/g; s/ Math\.Max(/ global::System.Math.Max(/g; s/ Math\.Abs(/ global::System.Math.Abs(/g' "$tmp_file"
    sed -i '' 's/=Math\.Min(/=global::System.Math.Min(/g; s/=Math\.Max(/=global::System.Math.Max(/g' "$tmp_file"

    final_file="$WORK_DIR/${spy_name}_final.cs"
    {
        echo "$header"
        cat "$tmp_file"
    } > "$final_file"

    emitted+=("${spy_name}:${cs_rel}")
    echo "Emitted: $spy_file"
done

if [[ $errors -ne 0 ]]; then
    echo ""
    echo "Some modules failed to emit. Aborting."
    exit 1
fi

# --- Pass 2: Apply or diff ---

for entry in "${emitted[@]}"; do
    IFS=':' read -r spy_name cs_rel <<< "$entry"
    final_file="$WORK_DIR/${spy_name}_final.cs"
    cs_target="$STDLIB_DIR/${cs_rel}"

    if [[ "$mode" == "check" ]]; then
        if [[ ! -f "$cs_target" ]]; then
            echo "FAIL: $cs_target not found (expected generated C# for ${spy_name}.spy)"
            errors=1
            continue
        fi
        if ! diff -u "$cs_target" "$final_file" --label "committed: $cs_rel" --label "generated: $cs_rel"; then
            echo "STALE: $cs_rel does not match generated output"
            errors=1
        else
            echo "OK: $cs_rel is up-to-date"
        fi
    else
        mkdir -p "$(dirname "$cs_target")"
        cp "$final_file" "$cs_target"
        echo "Regenerated: $cs_rel"
    fi
done

if [[ $errors -ne 0 ]]; then
    echo ""
    if [[ "$mode" == "check" ]]; then
        echo "Some .spy modules have stale generated C#. Regenerate with:"
        echo "  bash build_tools/regenerate_spy_stdlib.sh"
    fi
    exit 1
fi

echo ""
if [[ "$mode" == "check" ]]; then
    echo "All .spy modules are up-to-date."
else
    echo "All .spy modules regenerated successfully."
fi
