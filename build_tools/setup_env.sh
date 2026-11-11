#!/usr/bin/env bash
# One-shot development environment for Sharpy (Linux/macOS):
#   1. The .NET 10 SDK (default ~/.dotnet), which is the `dotnet` on PATH. Sharpy targets
#      net10.0 (Unity caps at .NET 10) and CI runs .NET 10.
#   2. The .NET 11 SDK in a separate directory (default ~/.dotnet11), kept off PATH: Sharpy
#      does not support .NET 11 yet (a net10.0 sharpyc refuses to start on the .NET 11
#      runtime alone; #2548 tracks the work, #2549 the measurements). It is installed so it
#      is there when that work lands; call it as `dotnet11` after sourcing the env file.
#   3. Local .NET tools from dotnet-tools.json (`dotnet tool restore`: dotnet-stryker).
#   4. CPython via uv: the requested version (3.15 by default) and the CI oracle minor
#      pinned in .python-version, if different.
#   5. A .venv on the requested Python with the build_tools dependencies CI installs
#      (pyyaml, click, ruff, and build_tools/sharpy_auto_builder[dev] with pytest, which
#      `pytest build_tools/tests` imports).
#   6. .venv/sharpy-env.sh, which puts all of the above on PATH; source it in each shell.
#
# Usage: build_tools/setup_env.sh [options]
#
#   --python V            Python for the .venv (default 3.15; any `uv python install` spec).
#   --dotnet-dir DIR      .NET 10 SDK dir (default $DOTNET_INSTALL_DIR or ~/.dotnet).
#   --dotnet11-dir DIR    .NET 11 SDK dir (default ~/.dotnet11).
#   --no-dotnet11         skip the .NET 11 SDK.
#   --with-docs           also install mkdocs-material (docs site).
#   --no-auto-builder     skip sharpy_auto_builder (LangGraph); most build_tools tests then fail to import.
#   --profile             source .venv/sharpy-env.sh from the first line of ~/.bashrc (or ~/.zshrc).
#   --build               build sharpy.sln and run the README hello-world smoke test.
#
# Afterwards: `source .venv/sharpy-env.sh`
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUFF_VERSION="0.16.9"   # keep in sync with .github/workflows/python-build-tools.yml

PYTHON_VERSION="3.15"; DOTNET_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"; DOTNET11_DIR="$HOME/.dotnet11"
DOTNET11=1; WITH_DOCS=0; AUTO_BUILDER=1; PROFILE=0; BUILD=0
while (($#)); do
  case "$1" in
    --python) PYTHON_VERSION="${2:?--python needs a version}"; shift ;;
    --python=*) PYTHON_VERSION="${1#*=}" ;;
    --dotnet-dir) DOTNET_DIR="${2:?--dotnet-dir needs a path}"; shift ;;
    --dotnet-dir=*) DOTNET_DIR="${1#*=}" ;;
    --dotnet11-dir) DOTNET11_DIR="${2:?--dotnet11-dir needs a path}"; shift ;;
    --dotnet11-dir=*) DOTNET11_DIR="${1#*=}" ;;
    --no-dotnet11) DOTNET11=0 ;;
    --with-docs) WITH_DOCS=1 ;;
    --no-auto-builder) AUTO_BUILDER=0 ;;
    --profile) PROFILE=1 ;;
    --build) BUILD=1 ;;
    -h|--help) sed -n '2,29p' "$0"; exit 0 ;;
    *) echo "unknown argument: $1 (see --help)" >&2; exit 2 ;;
  esac
  shift
done

log() { printf '\033[1;34m[setup-env]\033[0m %s\n' "$*" >&2; }
die() { printf '\033[1;31m[setup-env]\033[0m %s\n' "$*" >&2; exit 1; }

command -v curl >/dev/null 2>&1 || die "curl is required (e.g. apt-get install -y curl)"
abspath() { mkdir -p "$1" && (cd "$1" && pwd); }
DOTNET_DIR="$(abspath "$DOTNET_DIR")"; DOTNET="$DOTNET_DIR/dotnet"
((DOTNET11)) && DOTNET11_DIR="$(abspath "$DOTNET11_DIR")"
[[ "$DOTNET11_DIR" != "$DOTNET_DIR" ]] || die "--dotnet11-dir must differ from --dotnet-dir, or \`dotnet\` would pick the .NET 11 SDK"
export DOTNET_ROOT="$DOTNET_DIR" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export PATH="$DOTNET_DIR:$HOME/.dotnet/tools:$HOME/.local/bin:$PATH"

# 1-2. .NET SDKs -----------------------------------------------------------------
# have_sdk <dir> <major>
have_sdk() { [[ -x "$1/dotnet" ]] && "$1/dotnet" --list-sdks 2>/dev/null | grep -q "^$2\."; }

INSTALLER=""
fetch_installer() {
  [[ -n "$INSTALLER" ]] && return 0
  INSTALLER="$(mktemp -d)/dotnet-install.sh"
  # dot.net redirects to builds.dotnet.microsoft.com; try the target directly if the short host is blocked.
  curl -sSfL --retry 3 --max-time 60 -o "$INSTALLER" https://dot.net/v1/dotnet-install.sh \
    || curl -sSfL --retry 3 --max-time 60 -o "$INSTALLER" https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh \
    || die "cannot download dotnet-install.sh; install the .NET SDKs by hand (https://dotnet.microsoft.com/download)"
}
# install_sdk <dir> <major>
install_sdk() {
  if have_sdk "$1" "$2"; then
    log ".NET $2 SDK already in $1"
  else
    fetch_installer
    log "installing the .NET $2 SDK into $1"
    # Name the primary feed so the installer does not retry ci.dot.net first (blocked by a
    # network allowlist that lists only builds.dotnet.microsoft.com: ~30 s of backoff per SDK).
    bash "$INSTALLER" --channel "$2.0" --install-dir "$1" --no-path \
      --azure-feed https://builds.dotnet.microsoft.com/dotnet
  fi
}

install_sdk "$DOTNET_DIR" 10
((DOTNET11)) && install_sdk "$DOTNET11_DIR" 11

cd "$ROOT"
sdk="$("$DOTNET" --version)"
log "dotnet in $ROOT uses SDK $sdk"
if [[ "$sdk" != 10.* ]]; then
  log "warning: $DOTNET_DIR also holds a newer SDK, which \`dotnet\` prefers; Sharpy needs .NET 10."
  log "         Remove it from there (keep it in $DOTNET11_DIR) or pin 10.0 with a global.json."
fi

# 3. local .NET tools --------------------------------------------------------------
log "dotnet tool restore"
"$DOTNET" tool restore

# 4. Python ------------------------------------------------------------------------
ORACLE_VERSION="$(grep -v '^#' .python-version | head -1 | tr -d '[:space:]')"
if ! command -v uv >/dev/null 2>&1; then
  log "installing uv into ~/.local/bin"
  curl -LsSf https://astral.sh/uv/install.sh | env UV_NO_MODIFY_PATH=1 sh
  hash -r
fi
UV="$(command -v uv)"
# A uv older than a CPython release does not know it and quietly picks that minor's newest
# pre-release (a 3.15 request got 3.15.0b4 that way). Update uv where it can update itself;
# otherwise fall back to a private, current copy that leaves the user's uv alone.
"$UV" self update >/dev/null 2>&1 || true
PRIVATE_UV_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/sharpy-setup/uv"
use_private_uv() {
  if [[ "$UV" != "$PRIVATE_UV_DIR/uv" ]]; then
    log "using a current uv in $PRIVATE_UV_DIR ($("$UV" --version | cut -d' ' -f2) is too old)"
    mkdir -p "$PRIVATE_UV_DIR"
    # The installer comes from astral.sh and the binary from releases.astral.sh (or GitHub
    # releases); a network allowlist that names only the first fails here.
    local installer; installer="$(mktemp)"
    curl -LsSf --retry 3 --max-time 60 -o "$installer" https://astral.sh/uv/install.sh \
      && env UV_INSTALL_DIR="$PRIVATE_UV_DIR" UV_NO_MODIFY_PATH=1 sh "$installer" >/dev/null \
      && [[ -x "$PRIVATE_UV_DIR/uv" ]] || return 1
    UV="$PRIVATE_UV_DIR/uv"
  fi
  "$PRIVATE_UV_DIR/uv" self update >/dev/null 2>&1 || true
}
uv_quiet() { "$UV" "$@" 2> >(grep -v 'UV_NATIVE_TLS' >&2); }
# find_python <spec>: path of the uv-managed interpreter for <spec>, searched outside the repo
# so the .venv is not what is found.
find_python() { (cd / && VIRTUAL_ENV='' uv_quiet python find --managed-python "$1"); }
# is_final <python>: 0 if it is a final release, or the spec itself names a pre-release.
is_final() { [[ "$2" =~ [a-z] ]] || "$1" -c 'import sys; sys.exit(sys.version_info.releaselevel != "final")'; }

pythons=("$PYTHON_VERSION"); [[ -n "$ORACLE_VERSION" && "$ORACLE_VERSION" != "$PYTHON_VERSION" ]] && pythons+=("$ORACLE_VERSION")
PRERELEASE_NOTE=""
for attempt in 1 2; do
  log "installing CPython ${pythons[*]} with uv $("$UV" --version | cut -d' ' -f2)"
  ok=1
  uv_quiet python install "${pythons[@]}" || ok=0
  if ((ok)); then
    for spec in "${pythons[@]}"; do
      py="$(find_python "$spec")" && is_final "$py" "$spec" || { ok=0; break; }
    done
  fi
  ((ok)) && break
  ((attempt == 1)) || die "uv cannot install a final CPython ${pythons[*]}; pass --python with an exact version"
  if ! use_private_uv; then
    # No current uv to be had (offline, or the host is not allowed). Carry on with what the
    # old uv installed, if it installed anything, rather than leave no environment at all.
    for spec in "${pythons[@]}"; do
      find_python "$spec" >/dev/null || die "cannot download a current uv (astral.sh, releases.astral.sh) and uv $("$UV" --version | cut -d' ' -f2) cannot install CPython $spec"
    done
    PRERELEASE_NOTE="cannot download a current uv (astral.sh, releases.astral.sh); CPython ${pythons[*]} may be a pre-release, which is not the oracle. Allow those hosts and re-run."
    log "warning: $PRERELEASE_NOTE"
    break
  fi
done
PYTHON="$(find_python "$PYTHON_VERSION")"
PYTHON_FULL="$("$PYTHON" -c 'import platform; print(platform.python_version())')"

# 5. venv ----------------------------------------------------------------------------
VENV="$ROOT/.venv"
if [[ -x "$VENV/bin/python" && "$("$VENV/bin/python" -c 'import platform; print(platform.python_version())' 2>/dev/null)" == "$PYTHON_FULL" ]]; then
  log "reusing $VENV (Python $PYTHON_FULL)"
else
  log "creating $VENV on Python $PYTHON_FULL"
  uv_quiet venv --clear --seed --python "$PYTHON" "$VENV"
fi
pkgs=("pyyaml>=6,<7" click pytest pytest-asyncio "ruff==$RUFF_VERSION")
((WITH_DOCS)) && pkgs+=(mkdocs-material)
((AUTO_BUILDER)) && pkgs+=(-e "build_tools/sharpy_auto_builder[dev]")
log "installing ${pkgs[*]}"
uv_quiet pip install --python "$VENV/bin/python" "${pkgs[@]}"

# 6. env file --------------------------------------------------------------------------
ENV_FILE="$VENV/sharpy-env.sh"
cat > "$ENV_FILE" <<SH
# Sharpy dev environment (written by build_tools/setup_env.sh); source it.
export DOTNET_ROOT="$DOTNET_DIR"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="$DOTNET_DIR:\$HOME/.dotnet/tools:\$HOME/.local/bin:\$PATH"
SH
if ((DOTNET11)); then
  cat >> "$ENV_FILE" <<SH
# .NET 11 SDK, off PATH until Sharpy supports it (#2548): \`dotnet11 --info\`
export DOTNET11_ROOT="$DOTNET11_DIR"
dotnet11() { DOTNET_ROOT="\$DOTNET11_ROOT" "\$DOTNET11_ROOT/dotnet" "\$@"; }
SH
fi
cat >> "$ENV_FILE" <<SH
# build_tools.shared.* imports need the repo root (build_tools/CLAUDE.md).
export PYTHONPATH="$ROOT\${PYTHONPATH:+:\$PYTHONPATH}"
. "$VENV/bin/activate"
SH
if ((PROFILE)); then
  rc="$HOME/.bashrc"; [[ "${SHELL:-}" == */zsh ]] && rc="$HOME/.zshrc"
  # FIRST line, not appended: a stock ~/.bashrc returns at `[ -z "$PS1" ] && return` (or
  # `case $- in *i*) ;; *) return;; esac`), so an appended line never runs in the non-interactive
  # shells that agents, cloud sessions and `bash -lc` use; they then get the system python3 with
  # no pytest and no dotnet on PATH.
  line="[ -f \"$ENV_FILE\" ] && . \"$ENV_FILE\"  # Sharpy"
  if [[ "$(head -n 1 "$rc" 2>/dev/null)" == "$line" ]]; then
    log "$rc already sources $ENV_FILE"
  else
    rest="$(grep -vxF "$line" "$rc" 2>/dev/null || true)"
    printf '%s\n%s\n' "$line" "$rest" > "$rc"
    log "added $ENV_FILE as the first line of $rc"
  fi
fi

# 7. optional build + smoke --------------------------------------------------------------
if ((BUILD)); then
  log "building sharpy.sln"
  "$ROOT/.claude/scripts/dotnet-serialized" build sharpy.sln
  "$ROOT/build_tools/smoke_hello.sh" "$ROOT/src/Sharpy.Cli/bin/Debug/net10.0/sharpyc"
fi

sdks="$("$DOTNET" --list-sdks | cut -d' ' -f1 | paste -sd' ' -)"
((DOTNET11)) && sdks+=" (dotnet11: $("$DOTNET11_DIR/dotnet" --list-sdks | cut -d' ' -f1 | paste -sd' ' -))"
log "done: .NET SDK $sdks, venv $("$VENV/bin/python" --version | cut -d' ' -f2)"
if [[ -n "$ORACLE_VERSION" && "$ORACLE_VERSION" != "$PYTHON_VERSION" ]]; then
  log "note: CI's CPython oracle is $ORACLE_VERSION (.python-version); python$ORACLE_VERSION is installed too"
fi
[[ -z "$PRERELEASE_NOTE" ]] || log "warning: $PRERELEASE_NOTE"
log "run: source .venv/sharpy-env.sh"
