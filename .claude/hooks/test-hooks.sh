#!/usr/bin/env bash
# Falsification harness for the Claude Code hooks in this directory.
#
# Each hook is fed the synthetic stdin JSON Claude Code sends ({"tool_name":..,"tool_input":{..}})
# and its stdout / stderr / exit code are asserted. Run it after touching any hook:
#
#     bash .claude/hooks/test-hooks.sh            # exit 0 = all green; prints PASS/FAIL per case
#
# Every assertion has a direction-paired control (a path that MUST be denied next to one that
# MUST NOT; a glob that MUST dispatch next to one that MUST NOT), so a hook that does nothing
# fails here instead of passing vacuously — which is exactly how three of these hooks were
# no-ops for months (they read $TOOL_INPUT_FILE_PATH, which Claude Code never sets).
# Mutation record (break hook → red, restore → green) lives in the commit body of each change.
set -uo pipefail

HOOKS="$(cd "$(dirname "$0")" && pwd)"
WORK="${TMPDIR:-/tmp}/sharpy-hook-tests.$$"
mkdir -p "$WORK"
trap 'rm -rf "$WORK"' EXIT

pass=0; fail=0; skip=0
ok()   { pass=$((pass+1)); printf 'PASS  %s\n' "$1"; }
bad()  { fail=$((fail+1)); printf 'FAIL  %s\n      %s\n' "$1" "$2"; }
skp()  { skip=$((skip+1)); printf 'SKIP  %s (%s)\n' "$1" "$2"; }

# json TOOL KEY VALUE  → the stdin Claude Code would send for that tool call.
json() {
  python3 -c 'import json,sys; print(json.dumps({"session_id":"t","hook_event_name":"PreToolUse","tool_name":sys.argv[1],"tool_input":{sys.argv[2]:sys.argv[3]}}))' "$1" "$2" "$3"
}

# run HOOK STDIN → sets OUT, ERR, RC
run() {
  local hook="$1" stdin="$2"
  OUT=$(printf '%s' "$stdin" | "$HOOKS/$hook" 2>"$WORK/err"); RC=$?
  ERR=$(cat "$WORK/err")
}

is_deny() { printf '%s' "$1" | python3 -c 'import json,sys; d=json.load(sys.stdin)["hookSpecificOutput"]; sys.exit(0 if d["permissionDecision"]=="deny" else 1)' 2>/dev/null; }
has_ctx() { printf '%s' "$1" | python3 -c 'import json,sys; d=json.load(sys.stdin)["hookSpecificOutput"]; sys.exit(0 if ("additionalContext" in d and "permissionDecision" not in d) else 1)' 2>/dev/null; }

echo "== block-secrets.sh"
for p in /r/.env /r/.env.local /r/certs/server.pem /r/k/private.key /r/x/store.pfx /r/.ssh/id_rsa /r/credentials.json /r/config/secrets.yaml; do
  run block-secrets.sh "$(json Write file_path "$p")"
  if [ "$RC" -eq 0 ] && is_deny "$OUT"; then ok "deny $p"; else bad "deny $p" "rc=$RC out=$OUT"; fi
done
# Controls: the stdlib `secrets` module and its fixtures must stay editable (a path-substring
# rule locks them out), as must the committed .env templates and ordinary sources.
for p in /r/src/Sharpy.Stdlib/spy/secrets_module.spy /r/docs/stdlib/secrets.md \
         /r/src/Sharpy.Stdlib.Tests/Integration/TestFixtures/stdlib_secrets.expected \
         /r/src/Sharpy.Stdlib.Tests/Spy/generated/secrets_tests.cs /r/.env.example /r/src/Foo.cs /r/.claude/hooks/block-secrets.sh; do
  run block-secrets.sh "$(json Edit file_path "$p")"
  if [ "$RC" -eq 0 ] && [ -z "$OUT" ]; then ok "allow $p"; else bad "allow $p" "rc=$RC out=$OUT"; fi
done
# Fail closed: unparseable input and input without a file_path are both denied.
run block-secrets.sh 'this is not json'
if [ "$RC" -eq 0 ] && is_deny "$OUT"; then ok "fail-closed on non-JSON stdin"; else bad "fail-closed on non-JSON stdin" "rc=$RC out=$OUT"; fi
run block-secrets.sh '{"tool_name":"Write","tool_input":{"content":"x"}}'
if [ "$RC" -eq 0 ] && is_deny "$OUT"; then ok "fail-closed on missing file_path"; else bad "fail-closed on missing file_path" "rc=$RC out=$OUT"; fi
run block-secrets.sh ''
if [ "$RC" -eq 0 ] && is_deny "$OUT"; then ok "fail-closed on empty stdin"; else bad "fail-closed on empty stdin" "rc=$RC out=$OUT"; fi

echo "== warn-expectation-edit.sh"
for p in /r/t/foo.expected /r/t/foo.error /r/t/foo.warning /r/t/foo.expected.cs /r/t/dir/main.expected; do
  run warn-expectation-edit.sh "$(json Edit file_path "$p")"
  if [ "$RC" -eq 0 ] && has_ctx "$OUT"; then ok "warn $p"; else bad "warn $p" "rc=$RC out=$OUT"; fi
done
for p in /r/t/foo.spy /r/t/foo.cs /r/t/expected_values.md /r/t/foo.error.bak; do
  run warn-expectation-edit.sh "$(json Edit file_path "$p")"
  if [ "$RC" -eq 0 ] && [ -z "$OUT" ]; then ok "silent $p"; else bad "silent $p" "rc=$RC out=$OUT"; fi
done
run warn-expectation-edit.sh 'garbage'
if [ "$RC" -ne 0 ] && [ "$RC" -ne 2 ] && [ -n "$ERR" ]; then ok "non-blocking error on bad stdin"; else bad "non-blocking error on bad stdin" "rc=$RC err=$ERR"; fi

echo "== format-on-save.sh"
# Stub `dotnet` on PATH records its argv, so the C# branch is asserted without a real workspace
# load: the formatter must be called with the file's OWN csproj (never the solution), must not be
# called for a .cs with no csproj above it, and a hung formatter must be killed inside the budget.
mkdir -p "$WORK/bin" "$WORK/repo/build_tools/sub" "$WORK/repo/other" "$WORK/repo/editors/vscode/src" \
         "$WORK/repo/src/Proj/Sub" "$WORK/repo/src/NoProj"
# The stub records "<cwd>|<argv>": `dotnet format --include` resolves paths against the CURRENT
# DIRECTORY (an absolute path matches 0 files, silently), so the cwd is part of the contract.
# shellcheck disable=SC2016  # $PWD and $* belong to the stub, expanded when it runs
printf '#!/bin/sh\necho "$PWD|$*" >> "%s/dotnet-calls"\n[ -n "${HOOKTEST_HANG:-}" ] && sleep 60\nexit 0\n' "$WORK" > "$WORK/bin/dotnet"; chmod +x "$WORK/bin/dotnet"
printf 'x=1\n' > "$WORK/repo/build_tools/a.py"
printf 'x=1\n' > "$WORK/repo/build_tools/sub/b.py"
printf 'x=1\n' > "$WORK/repo/other/c.py"
printf '<Project/>\n' > "$WORK/repo/src/Proj/Proj.csproj"
printf 'class A{}\n' > "$WORK/repo/src/Proj/Sub/A.cs"
printf 'class B{}\n' > "$WORK/repo/src/NoProj/B.cs"
printf 'const x=1\n' > "$WORK/repo/editors/vscode/src/e.ts"

if command -v ruff >/dev/null 2>&1 || command -v black >/dev/null 2>&1; then
  for f in build_tools/a.py build_tools/sub/b.py; do
    PATH="$WORK/bin:$PATH" run format-on-save.sh "$(json Write file_path "$WORK/repo/$f")"
    if [ "$RC" -eq 0 ] && [ "$(cat "$WORK/repo/$f")" = "x = 1" ]; then ok "py formatted $f"; else bad "py formatted $f" "rc=$RC content=$(cat "$WORK/repo/$f")"; fi
  done
  PATH="$WORK/bin:$PATH" run format-on-save.sh "$(json Write file_path "$WORK/repo/other/c.py")"
  if [ "$RC" -eq 0 ] && [ "$(cat "$WORK/repo/other/c.py")" = "x=1" ]; then ok "py outside build_tools untouched"; else bad "py outside build_tools untouched" "rc=$RC content=$(cat "$WORK/repo/other/c.py")"; fi
else
  skp "py branch" "neither ruff nor black on PATH"
fi

CLAUDE_PROJECT_DIR="$WORK/repo" PATH="$WORK/bin:$PATH" run format-on-save.sh "$(json Write file_path "$WORK/repo/src/Proj/Sub/A.cs")"
want="$WORK/repo/src/Proj|format whitespace Proj.csproj --include Sub/A.cs --no-restore"
if [ "$RC" -eq 0 ] && [ "$(cat "$WORK/dotnet-calls" 2>/dev/null)" = "$want" ]; then ok "cs: dotnet format run from the file's project dir with a relative --include"; else bad "cs: dotnet format run from the file's project dir with a relative --include" "rc=$RC calls=$(cat "$WORK/dotnet-calls" 2>/dev/null) want=$want"; fi
rm -f "$WORK/dotnet-calls"
CLAUDE_PROJECT_DIR="$WORK/repo" PATH="$WORK/bin:$PATH" run format-on-save.sh "$(json Write file_path "$WORK/repo/src/NoProj/B.cs")"
if [ "$RC" -eq 0 ] && [ ! -e "$WORK/dotnet-calls" ]; then ok "cs: no csproj above the file → no dotnet call"; else bad "cs: no csproj above the file → no dotnet call" "rc=$RC calls=$(cat "$WORK/dotnet-calls" 2>/dev/null)"; fi
rm -f "$WORK/dotnet-calls"
# A formatter that hangs is killed by the hook inside its own budget (8 s), so a harness-level
# timeout can never leave an orphan that rewrites the file after a later edit.
start=$(date +%s)
HOOKTEST_HANG=1 CLAUDE_PROJECT_DIR="$WORK/repo" PATH="$WORK/bin:$PATH" run format-on-save.sh "$(json Write file_path "$WORK/repo/src/Proj/Sub/A.cs")"
elapsed=$(( $(date +%s) - start ))
sleep 0.5
survivors=$(pgrep -f "HOOKTEST_HANG|$WORK/bin/dotnet" 2>/dev/null | wc -l | tr -d ' ')
if [ "$RC" -eq 0 ] && [ "$elapsed" -ge 7 ] && [ "$elapsed" -le 10 ]; then ok "cs: hung formatter capped (${elapsed}s)"; else bad "cs: hung formatter capped" "rc=$RC took ${elapsed}s"; fi
if [ "$survivors" = "0" ]; then ok "cs: hung formatter was killed (no survivor)"; else bad "cs: hung formatter was killed" "$survivors survivor(s)"; fi
rm -f "$WORK/dotnet-calls"

PATH="$WORK/bin:$PATH" run format-on-save.sh "$(json Write file_path "$WORK/repo/editors/vscode/src/e.ts")"
if [ "$RC" -eq 0 ]; then ok "ts branch exits 0 (prettier $(command -v prettier >/dev/null 2>&1 && echo present || echo absent))"; else bad "ts branch exits 0" "rc=$RC err=$ERR"; fi

run format-on-save.sh "$(json Write file_path /nonexistent/zzz.py)"
if [ "$RC" -eq 0 ] && [ -z "$OUT" ]; then ok "missing file: no-op"; else bad "missing file: no-op" "rc=$RC"; fi
run format-on-save.sh '{"tool_name":"NotebookEdit","tool_input":{"notebook_path":"/r/n.ipynb"}}'
if [ "$RC" -eq 0 ]; then ok "NotebookEdit input: no-op"; else bad "NotebookEdit input: no-op" "rc=$RC err=$ERR"; fi
run format-on-save.sh 'garbage'
if [ "$RC" -ne 0 ] && [ "$RC" -ne 2 ]; then ok "non-blocking error on bad stdin"; else bad "non-blocking error on bad stdin" "rc=$RC"; fi

echo "== enforce-dotnet-serialized.sh"
blocked() { run enforce-dotnet-serialized.sh "$(json Bash command "$1")"; if [ "$RC" -eq 2 ] && [ -n "$ERR" ]; then ok "block: $1"; else bad "block: $1" "rc=$RC err=$ERR"; fi; }
allowed() { run enforce-dotnet-serialized.sh "$(json Bash command "$1")"; if [ "$RC" -eq 0 ] && [ -z "$ERR" ]; then ok "allow: $1"; else bad "allow: $1" "rc=$RC err=$ERR"; fi; }
blocked 'dotnet build sharpy.sln'
blocked 'dotnet test --filter "FullyQualifiedName~Lexer"'
blocked 'cd src && dotnet test'
blocked 'dotnet run --project src/Sharpy.Cli -- run file.spy'
blocked 'dotnet format whitespace sharpy.sln --verify-no-changes'
blocked 'dotnet pack src/Sharpy.Core'
blocked 'dotnet publish -c Release'
blocked 'dotnet msbuild /t:Rebuild'
blocked 'dotnet clean'
blocked 'dotnet stryker'
blocked 'echo start; dotnet build'
blocked 'bash -c "dotnet build"'
# Measured bypasses of the pre-2026-11-20 hook (each exited 0): a path-qualified binary, a raw
# call beside a mention of the wrapper, and the vstest verb.
blocked '/root/.dotnet/dotnet build'
# shellcheck disable=SC2016  # expanded by the hook, not this shell
blocked '"$HOME/.dotnet/dotnet" test'
blocked 'echo dotnet-serialized; dotnet test'
blocked '.claude/scripts/dotnet-serialized build sharpy.sln && dotnet test --no-build'
blocked 'dotnet vstest src/X/bin/Debug/net10.0/X.dll'
blocked 'DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build'
blocked 'timeout 600 env X=1 dotnet test'
blocked 'ls *.csproj | xargs -n1 dotnet build'
# shellcheck disable=SC2016  # the substitution is the command under test
blocked 'echo "$(dotnet build)"'
blocked "bash -lc 'cd src && dotnet test'"
blocked 'dotnet --diagnostics build'
allowed '.claude/scripts/dotnet-serialized build sharpy.sln'
allowed '.claude/scripts/dotnet-serialized vstest src/X/bin/Debug/net10.0/X.dll'
allowed '.claude/scripts/dotnet-serialized build sharpy.sln && .claude/scripts/dotnet-serialized test --no-build'
allowed 'grep -rn "dotnet build" docs/'
allowed 'echo dotnet test'
allowed 'dotnet src/X/bin/Debug/net10.0/X.dll'
allowed '.claude/scripts/dotnet-serialized run --project src/Sharpy.Cli -- run file.spy'
allowed 'dotnet --version'
allowed 'dotnet --list-sdks'
allowed 'dotnet watch run --project src/Sharpy.Playground'
allowed 'dotnet tool list'
allowed 'bash build_tools/regenerate_spy_stdlib.sh'
allowed 'src/Sharpy.Cli/bin/Debug/net10.0/sharpyc run file.spy'
allowed 'ls'
allowed "dotnet build 'unterminated"
run enforce-dotnet-serialized.sh 'garbage'
if [ "$RC" -eq 0 ]; then ok "bad stdin: allow (Bash hook fails open by design; the lock is the safety net)"; else bad "bad stdin: allow" "rc=$RC"; fi

echo "== guard-shared-tree.sh"
# A scratch repository with the session layout: MAIN (the shared main worktree), a linked worktree
# under MAIN/.claude/worktrees (as an isolated agent gets), one outside it (a scratchpad worktree),
# a .NET project with build outputs, and build_tools/bin/ (tracked source, not a build output).
G="$WORK/g"; MAIN="$G/main"; WT="$MAIN/.claude/worktrees/wt1"; SWT="$G/scratch/wt2"
mkdir -p "$G"
git init -q -b dev "$MAIN"
mkdir -p "$MAIN/src/P/bin" "$MAIN/src/P/obj" "$MAIN/build_tools/bin" "$MAIN/.claude/tmp/x"
echo x > "$MAIN/src/a.txt"; echo '<Project/>' > "$MAIN/src/P/P.csproj"; echo t > "$MAIN/build_tools/bin/tool"
git -C "$MAIN" add src build_tools
git -C "$MAIN" -c user.email=t@t -c user.name=t commit -qm init
git -C "$MAIN" worktree add -q "$WT" -b claude/x
git -C "$MAIN" worktree add -q "$SWT" -b claude/y
gjson() { python3 -c 'import json,sys; print(json.dumps({"session_id":"t","hook_event_name":"PreToolUse","cwd":sys.argv[2],"tool_name":"Bash","tool_input":{"command":sys.argv[1]}}))' "$1" "$2"; }
gname()  { local c="${1//$'\n'/\\n}"; printf '%s @ %s' "${c//"$G"\//}" "${2#"$G"/}"; }
gdeny()  { CLAUDE_PROJECT_DIR="$MAIN" run guard-shared-tree.sh "$(gjson "$1" "$2")"; if [ "$RC" -eq 2 ] && [ -n "$ERR" ]; then ok "deny: $(gname "$1" "$2")"; else bad "deny: $(gname "$1" "$2")" "rc=$RC err=$ERR"; fi; }
gallow() { CLAUDE_PROJECT_DIR="$MAIN" run guard-shared-tree.sh "$(gjson "$1" "$2")"; if [ "$RC" -eq 0 ] && [ -z "$ERR" ]; then ok "allow: $(gname "$1" "$2")"; else bad "allow: $(gname "$1" "$2")" "rc=$RC err=$ERR"; fi; }
# 1. tree-mutating git verbs: denied in MAIN (by cwd, -C, --work-tree, cd, compound, nested) ...
for v in 'checkout dev' 'checkout -- src/a.txt' 'restore src/a.txt' 'restore --staged .' 'clean -fd' 'stash' 'stash list' 'reset --hard' 'reset' 'switch -c y'; do
  gdeny "git $v" "$MAIN"
done
gdeny "git -C $MAIN reset --hard" "$WT"
gdeny "git --work-tree=$MAIN checkout ." "$G"
gdeny "cd $MAIN && git checkout ." "$WT"
gdeny "cd src; git restore a.txt" "$MAIN"
gdeny 'FOO=1 git -c core.pager=cat --no-pager checkout dev' "$MAIN"
gdeny 'git status && git diff --stat || git stash' "$MAIN"
gdeny 'echo start
git reset --hard' "$MAIN"
gdeny '(git stash)' "$MAIN"
gdeny "bash -c 'git reset --hard'" "$MAIN"
# shellcheck disable=SC2016  # the $(...) is the command under test, not this shell's
gdeny 'echo "$(git stash)"' "$MAIN"
gdeny 'git status | cat; timeout 60 git switch dev' "$MAIN"
# ... and allowed in a linked worktree (the session's own), in a scratch repo, or when only named.
for v in 'switch -c y' 'checkout dev' 'reset --hard' 'stash list' 'restore src/a.txt' 'clean -fd'; do
  gallow "git $v" "$WT"
done
gallow 'git reset --hard HEAD' "$SWT"
gallow "cd $WT && git checkout claude/x" "$MAIN"
gallow "git -C $WT stash list" "$MAIN"
gallow "(cd $WT && git switch -c z)" "$MAIN"
gallow 'git status --short; git diff --stat; git log --oneline -1; git worktree list' "$MAIN"
gallow 'echo "git reset --hard"; grep -n "git checkout" docs.md' "$MAIN"
gallow "git commit -m \"\$(cat <<'EOF'
fix: say why git reset --hard and rm -rf src are denied
EOF
)\"" "$MAIN"
git init -q "$G/other"
gallow 'git reset --hard' "$G/other"
# a subshell's cd does not leak: the trailing reset runs in MAIN again
gdeny "(cd $WT && git switch -c z2); git reset --hard" "$MAIN"
# 2. rm: denied inside MAIN, allowed for .claude/tmp/, .NET bin/obj, linked worktrees, outside.
gdeny 'rm src/a.txt' "$MAIN"
gdeny "rm -rf $MAIN/src" "$G"
gdeny 'cd src && rm -f a.txt' "$MAIN"
gdeny 'rm -rf build_tools/bin' "$MAIN"
gdeny "rm -rf $WT" "$MAIN"
gdeny 'rm -rf -- src/a.txt' "$MAIN"
gdeny 'sudo rm -rf src' "$MAIN"
gallow 'rm -rf .claude/tmp/x' "$MAIN"
gallow 'rm -rf src/P/bin src/P/obj/Debug' "$MAIN"
gallow 'rm -rf src/*/obj' "$MAIN"
gallow "rm -f $WT/src/a.txt" "$MAIN"
gallow 'rm src/a.txt' "$WT"
gallow "rm -rf $G/scratch/junk /tmp/sharpy-hook-nonexistent" "$MAIN"
gallow 'grep -rn "rm -rf" src' "$MAIN"
# 3. staging without per-file pathspecs: denied in every worktree of this repository.
for a in 'add -A' 'add --all' 'add .' 'add ./' 'add -u' 'add --update' 'add -Av' 'commit -am wip' 'commit --all -m wip' 'commit -a'; do
  gdeny "git $a" "$WT"
done
gdeny 'git add .' "$MAIN"
gdeny "git -C $WT add -A" "$G"
gallow 'git add src/a.txt' "$WT"
gallow 'git add -- src/a.txt src/P/P.csproj' "$MAIN"
gallow 'git commit -m "fix -a and --all handling"' "$WT"
gallow 'git commit -m wip -- src/a.txt' "$WT"
gallow 'git add -A' "$G/other"
# 4. forced push: only lease + includes, only claude/* refs.
gdeny 'git push --force origin claude/x' "$WT"
gdeny 'git push -f origin claude/x' "$WT"
gdeny 'git push -uf origin claude/x' "$WT"
gdeny 'git push origin +claude/x' "$WT"
gdeny 'git push --force-with-lease origin claude/x' "$WT"
gdeny 'git push --force-if-includes origin +claude/x' "$WT"
gdeny 'git push --force --force-with-lease --force-if-includes origin claude/x' "$WT"
gdeny 'git push --force-with-lease --force-if-includes origin dev' "$WT"
gdeny 'git push --force-with-lease --force-if-includes origin mainline' "$WT"
gdeny 'git push --force-with-lease --force-if-includes origin claude/x:dev' "$WT"
gdeny 'git push --force-with-lease --force-if-includes origin HEAD:refs/heads/mainline' "$WT"
gdeny 'git push --force-with-lease --force-if-includes origin feature/x' "$WT"
gdeny 'git push --force-with-lease --force-if-includes origin claude/x dev' "$WT"
gdeny 'git push --force-with-lease --force-if-includes origin' "$MAIN"
gdeny 'git push --mirror origin' "$WT"
gallow 'git push --force-with-lease --force-if-includes origin claude/x' "$WT"
gallow 'git push --force-with-lease=claude/x:abc123 --force-if-includes origin claude/x' "$WT"
gallow 'git push --force-with-lease --force-if-includes origin HEAD' "$WT"
gallow 'git push --force-with-lease --force-if-includes -u origin HEAD:refs/heads/claude/x' "$WT"
gallow 'git push -u origin claude/x' "$WT"
# unparseable (bash refuses it too) and unreadable input: allowed.
gallow "git reset --hard 'unterminated" "$MAIN"
run guard-shared-tree.sh 'garbage'
if [ "$RC" -eq 0 ]; then ok "bad stdin: allow"; else bad "bad stdin: allow" "rc=$RC"; fi

echo
echo "hooks: $pass passed, $fail failed, $skip skipped"
[ "$fail" -eq 0 ]
