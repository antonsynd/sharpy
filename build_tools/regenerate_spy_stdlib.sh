#!/usr/bin/env bash
# Regenerate C# from .spy stdlib modules.
#
# Usage:
#   bash build_tools/regenerate_spy_stdlib.sh           # Regenerate all in-place
#   bash build_tools/regenerate_spy_stdlib.sh --check    # Diff against committed (CI mode)
#   bash build_tools/regenerate_spy_stdlib.sh --dry-run  # Show what would be regenerated
#
# Uses project compilation (sharpyc project stdlib.spyproj --emit-cs-to) to emit
# all modules in one pass, then maps the output files to their target locations.

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

# Mapping: emitted_filename:cs_relative_path
# The emitted filename comes from --emit-cs-to (spy filename stem + .cs).
# The cs_relative_path is the target location in Sharpy.Stdlib.
MODULES=(
    "textwrap:Textwrap/Textwrap.cs"
    "bisect_module:Bisect/Bisect.cs"
    "statistics:Statistics/Statistics.cs"
    "heapq:Heapq/Heapq.cs"
    "itertools:Itertools/Itertools.cs"
    "functools:Functools/Functools.cs"
    "string_module:String/StringModule.cs"
    "fnmatch_module:Fnmatch/FnmatchModule.cs"
    "tempfile_module:Tempfile/Tempfile.cs"
    "math_module:Math/Math.cs"
    "os_module:Os/Os.cs"
    "os_path_module:Os/OsPath.cs"
    "shutil_module:Shutil/Shutil.cs"
    "random_module:Random/Random.cs"
    "hashlib_module:Hashlib/Hashlib.cs"
    "csv_module:Csv/CsvModule.cs"
    "re_module:Re/ReModule.cs"
)

mode="regenerate"
if [[ "${1:-}" == "--check" ]]; then
    mode="check"
elif [[ "${1:-}" == "--dry-run" ]]; then
    mode="dry-run"
fi

if [[ "$mode" == "dry-run" ]]; then
    echo "Would emit all modules via: sharpyc project stdlib.spyproj --emit-cs-to <tmpdir>"
    for entry in "${MODULES[@]}"; do
        IFS=':' read -r emitted_name cs_rel <<< "$entry"
        echo "  ${emitted_name}.cs -> $STDLIB_DIR/${cs_rel}"
    done
    exit 0
fi

WORK_DIR="$(mktemp -d)"
EMIT_DIR="$WORK_DIR/emitted"
mkdir -p "$EMIT_DIR"

# --- Pass 1: Emit all modules in one project compilation pass ---

echo "Emitting all .spy modules via project compilation..."
if ! $SHARPYC project "$SPY_DIR/stdlib.spyproj" --emit-cs-to "$EMIT_DIR" 2>/dev/null; then
    echo "ERROR: sharpyc project compilation failed"
    echo "Run manually to see diagnostics:"
    echo "  dotnet run --project src/Sharpy.Cli -- project src/Sharpy.Stdlib/spy/stdlib.spyproj"
    exit 1
fi
echo "Project compilation succeeded."
echo ""

# --- Pass 2: Post-process and apply/diff ---

errors=0

for entry in "${MODULES[@]}"; do
    IFS=':' read -r emitted_name cs_rel <<< "$entry"

    emitted_file="$EMIT_DIR/${emitted_name}.cs"
    if [[ ! -f "$emitted_file" ]]; then
        echo "ERROR: Expected emitted file not found: ${emitted_name}.cs"
        errors=1
        continue
    fi

    # Build the header comment
    header_cmd="sharpyc emit csharp src/Sharpy.Stdlib/spy/${emitted_name}.spy -t library -n Sharpy"
    header="// Generated from src/Sharpy.Stdlib/spy/${emitted_name}.spy — do not edit directly.
// To regenerate: $header_cmd"

    # Post-process: normalize CRLF→LF, strip trailing whitespace, strip [SharpyModule],
    # strip #line directives (project compilation emits these for source mapping).
    final_file="$WORK_DIR/${emitted_name}_final.cs"
    {
        echo "$header"
        tr -d '\r' < "$emitted_file" \
            | sed '/\[global::Sharpy\.SharpyModule(/d' \
            | sed '/^#line /d' \
            | sed 's/[[:space:]]*$//'
    } > "$final_file"

    # Ensure file ends with a newline
    if [ -n "$(tail -c 1 "$final_file")" ]; then
        echo "" >> "$final_file"
    fi

    cs_target="$STDLIB_DIR/${cs_rel}"

    if [[ "$mode" == "check" ]]; then
        if [[ ! -f "$cs_target" ]]; then
            echo "FAIL: $cs_target not found (expected generated C# for ${emitted_name}.spy)"
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
