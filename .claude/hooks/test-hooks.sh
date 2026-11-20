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
# shellcheck disable=SC2016  # single-quoted $... are the commands under test, not this shell's
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
# Launchers (each exited 0 at 48f71558a; the pre-2026-11-20 regex hook blocked them).
blocked 'time -p dotnet test'
blocked "find src -name '*.csproj' -exec dotnet build {} \\;"
blocked "find src -name '*.csproj' -execdir dotnet build {} +"
blocked 'ionice -c3 dotnet test'
blocked 'taskset -c 0 dotnet build'
blocked 'flock /tmp/l dotnet build'
blocked "flock -w 5 /tmp/l -c 'dotnet test'"
blocked 'watch -n 5 dotnet test'
blocked 'strace -f -o /tmp/s dotnet build'
blocked 'parallel -j2 dotnet build ::: a b'
blocked 'chrt -i 0 dotnet build'
blocked "env -S 'dotnet test --no-build'"
# Here-documents: a body with an odd quote must not hide the call beside it.
HDC=$(cat <<'EOS'
dotnet test && git commit -m "$(cat <<'EOF'
fix: don't skip
EOF
)"
EOS
)
blocked "$HDC"
allowed "cat <<'EOF' > .claude/tmp/n.txt
dotnet build
EOF"
# Unparseable by shell_commands but bash runs it: the whole-string fallback judges it.
blocked 'dotnet test; echo "${x:-"it'"'"'s"}"'
allowed 'ls; echo "${x:-"it'"'"'s"}"'
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
git -C "$MAIN" -c user.email=t@t -c user.name=t commit -q --allow-empty -m second
mkdir -p "$MAIN/.claude/worktrees/stale/src"  # a leftover directory, not a registered worktree
git init -q "$G/other"                         # a scratch repository: never guarded
git -C "$MAIN" worktree add -q "$WT" -b claude/x
git -C "$MAIN" worktree add -q "$SWT" -b claude/y
git -C "$MAIN" update-ref refs/remotes/origin/dev HEAD
git -C "$MAIN" update-ref refs/remotes/origin/claude/remote-only HEAD
# gjson COMMAND CWD [AGENT_ID]: a non-empty AGENT_ID is the input a subagent's tool call carries.
gjson() { python3 -c 'import json,sys; d={"session_id":"t","hook_event_name":"PreToolUse","cwd":sys.argv[2],"tool_name":"Bash","tool_input":{"command":sys.argv[1]}}; d.update({"agent_id":sys.argv[3],"agent_type":"general-purpose"} if sys.argv[3] else {}); print(json.dumps(d))' "$1" "$2" "${3:-}"; }
gname()  { local c="${1//$'\n'/\\n}"; printf '%s%s @ %s' "${3:+[agent] }" "${c//"$G"\//}" "${2#"$G"/}"; }
gdeny()  { CLAUDE_PROJECT_DIR="$MAIN" run guard-shared-tree.sh "$(gjson "$1" "$2" "${3:-}")"; if [ "$RC" -eq 2 ] && [ -n "$ERR" ]; then ok "deny: $(gname "$1" "$2" "${3:-}")"; else bad "deny: $(gname "$1" "$2" "${3:-}")" "rc=$RC err=$ERR"; fi; }
gallow() { CLAUDE_PROJECT_DIR="$MAIN" run guard-shared-tree.sh "$(gjson "$1" "$2" "${3:-}")"; if [ "$RC" -eq 0 ] && [ -z "$ERR" ]; then ok "allow: $(gname "$1" "$2" "${3:-}")"; else bad "allow: $(gname "$1" "$2" "${3:-}")" "rc=$RC err=$ERR"; fi; }
# 1a. The LEAD (no agent_id) in MAIN: branch moves and creation are allowed — these are the
# skills' own set-up lines (create-plan, verify-plan, bump-version), and git itself refuses them
# when they would overwrite local changes — and so is unstaging ...
L_ALLOW=('git fetch origin dev && git switch -c claude/x origin/dev'
         'git switch claude/x && git merge --ff-only'
         'git checkout dev'
         'git stash list'
         'git switch -c claude/bump-1.2.3 origin/dev'
         'git switch dev'
         'git switch --detach HEAD'
         'git checkout -b claude/new origin/dev'
         'git checkout claude/remote-only'
         'git checkout -'
         'git stash show'
         'git reset --soft HEAD'
         'git clean -n -d'
         'git clean --dry-run')
for c in "${L_ALLOW[@]}"; do gallow "$c" "$MAIN"; done
L_UNSTAGE=('git restore --staged src/a.txt'
           'git restore -S src/a.txt'
           'git reset src/a.txt'
           'git reset -q HEAD -- src/a.txt'
           'git reset HEAD src/a.txt'
           'git reset -- src/a.txt')
for c in "${L_UNSTAGE[@]}"; do gallow "$c" "$MAIN"; done
# ... the forms that destroy uncommitted work are not.
L_DENY=('git checkout -- src/a.cs'
        'git checkout .'
        'git checkout HEAD -- x'
        'git checkout src/a.cs'
        'git checkout dev src/a.txt'
        'git switch -f dev'
        'git switch --discard-changes dev'
        'git checkout --force dev'
        'git checkout -m dev'
        'git checkout -p'
        'git checkout --theirs src/a.txt'
        'git restore x'
        'git restore --staged --worktree src/a.txt'
        'git restore -SW src/a.txt'
        'git stash'
        'git stash push -m wip'
        'git stash pop'
        'git stash drop'
        'git reset --hard'
        'git reset --merge'
        'git reset --keep HEAD'
        'git reset'
        'git reset HEAD~1'
        'git clean -fd')
for c in "${L_DENY[@]}"; do gdeny "$c" "$MAIN"; done
# The worktree is found from -C, --work-tree, --git-dir, GIT_DIR/GIT_WORK_TREE, cd (cd -,
# pushd/popd), compound lists, nested commands and launchers.
gdeny "git -C $MAIN reset --hard" "$WT"
gdeny "git --work-tree=$MAIN checkout ." "$G"
gdeny "git --git-dir=$MAIN/.git reset --hard" "$G"
gdeny "GIT_DIR=$MAIN/.git GIT_WORK_TREE=$MAIN git reset --hard" "$G"
gdeny "GIT_DIR=$MAIN/.git git stash" "$G/scratch"
gdeny "export GIT_WORK_TREE=$MAIN; git reset --hard" "$G"
gallow "GIT_DIR=$G/other/.git git reset --hard" "$G"
gdeny "cd $MAIN && git checkout -- src/a.txt" "$WT"
gdeny "cd $MAIN && cd $WT && cd - && git reset --hard" "$G"
gallow "cd $WT && cd $MAIN && cd - && git reset --hard" "$G"
gdeny "pushd $WT && popd && git reset --hard" "$MAIN"
gdeny "cd src; git restore a.txt" "$MAIN"
gdeny 'FOO=1 git -c core.pager=cat --no-pager checkout -f dev' "$MAIN"
gdeny 'git status && git diff --stat || git stash' "$MAIN"
gdeny 'echo start
git reset --hard' "$MAIN"
gdeny '(git stash)' "$MAIN"
gdeny "bash -c 'git reset --hard'" "$MAIN"
# shellcheck disable=SC2016  # the $(...) is the command under test, not this shell's
gdeny 'echo "$(git stash)"' "$MAIN"
gdeny 'git status | cat; timeout 60 git clean -fdx' "$MAIN"
gdeny 'time -p git reset --hard' "$MAIN"
gdeny 'find . -maxdepth 0 -exec git reset --hard \;' "$MAIN"
gdeny 'ionice -c3 nice -n 5 git clean -fd' "$MAIN"
# 1b. A SUBAGENT (agent_id set) in MAIN: §9's verbs (checkout, restore, clean, stash, reset) in
# every form but stash list/show; `git switch` is judged by the lead's rules, so /plan-flow's stage
# agents can cut and move their branch (create-plan, verify-plan set-up lines).
A_ALLOW=('git fetch origin dev && git switch -c claude/x origin/dev'
         'git switch claude/x && git merge --ff-only'
         'git fetch origin && git switch claude/x'
         'git switch -c claude/bump-1.2.3 origin/dev'
         'git switch --detach HEAD'
         'git stash list'
         'git stash show')
for c in "${A_ALLOW[@]}"; do gallow "$c" "$MAIN" agent-1; done
A_DENY=('git checkout dev'
        'git checkout -b claude/new origin/dev'
        'git checkout -'
        'git reset --soft HEAD'
        'git clean -n -d'
        'git restore --staged src/a.txt'
        'git reset src/a.txt'
        'git stash'
        'git switch -f dev'
        'git switch --discard-changes dev'
        'git checkout -- src/a.cs')
for c in "${A_DENY[@]}"; do gdeny "$c" "$MAIN" agent-1; done
# 1c. Linked worktrees (an owner's own), scratch repos and mere mentions: never judged.
for v in 'switch -c y' 'checkout dev' 'reset --hard' 'stash' 'restore src/a.txt' 'clean -fd' 'checkout -- src/a.txt'; do
  gallow "git $v" "$WT"
  gallow "git $v" "$WT" agent-1
done
gallow 'git reset --hard HEAD' "$SWT"
gallow "cd $WT && git reset --hard" "$MAIN"
gallow "git -C $WT stash" "$MAIN" agent-1
gallow "(cd $WT && git restore src/a.txt)" "$MAIN"
gallow 'git status --short; git diff --stat; git log --oneline -1; git worktree list' "$MAIN" agent-1
gallow 'echo "git reset --hard"; grep -n "git checkout -- x" docs.md' "$MAIN"
gallow 'git reset --hard' "$G/other"
gallow 'git reset --hard' "$G/other" agent-1
# a subshell's cd does not leak: the trailing reset runs in MAIN again
gdeny "(cd $WT && git switch -c z2); git reset --hard" "$MAIN"
# 1d. Here-documents: a body is text, never commands, even inside "$(...)" and with an odd quote.
HD_ADD=$(cat <<'EOS'
git add -A && git commit -m "$(cat <<'EOF'
fix: don't break
EOF
)"
EOS
)
HD_BODY1=$(cat <<'EOS'
git commit -m "$(cat <<'EOF'
1) deny "git status; git reset --hard" by agents
EOF
)"
EOS
)
HD_BODY2=$(cat <<'EOS'
git commit -m "$(cat <<'EOF'
a) run "cd src && rm -rf obj" first
EOF
)"
EOS
)
HD_LINE=$(cat <<'EOS'
cat <<'EOF' > .claude/tmp/note.txt
git reset --hard
rm -rf src
EOF
EOS
)
gdeny "$HD_ADD" "$WT"
gdeny "$HD_ADD" "$WT" agent-1
gallow "$HD_BODY1" "$MAIN"
gallow "$HD_BODY1" "$MAIN" agent-1
gallow "$HD_BODY2" "$MAIN"
gallow "$HD_LINE" "$MAIN"
gallow "$HD_LINE" "$MAIN" agent-1
gallow "git commit -m \"\$(cat <<'EOF'
fix: say why git reset --hard and rm -rf src are denied
EOF
)\"" "$MAIN"
# Unparseable by shell_commands but bash runs it: the whole-string fallback judges it.
# shellcheck disable=SC2016  # the ${...} is the command under test
ODD='echo "${x:-"it'"'"'s"}"'
gdeny "git reset --hard; $ODD" "$MAIN"
gallow "git reset --hard; $ODD" "$WT"
gallow "ls; $ODD" "$MAIN"
# 2. rm: denied inside MAIN, allowed for .claude/tmp/, .NET bin/obj, stale .claude/worktrees/*
# directories, linked worktrees, outside, and targets the text cannot resolve.
gdeny 'rm src/a.txt' "$MAIN"
gdeny "rm -rf $MAIN/src" "$G"
gdeny 'cd src && rm -f a.txt' "$MAIN"
gdeny 'rm -rf build_tools/bin' "$MAIN"
gdeny "rm -rf $WT" "$MAIN"
gdeny 'rm -rf .claude/worktrees/wt1' "$MAIN"
gdeny 'rm -rf -- src/a.txt' "$MAIN"
gdeny 'sudo rm -rf src' "$MAIN"
gdeny 'D=src; rm -rf "$D"' "$MAIN"
gdeny 'export D=src/a.txt; rm -f "$D"' "$MAIN"
gdeny "find src -name '*.txt' -delete" "$MAIN"
gdeny "find src -name '*.txt' -exec rm -f {} +" "$MAIN"
gallow 'rm -rf .claude/tmp/x' "$MAIN"
gallow 'find .claude/tmp -name "*.log" -delete' "$MAIN"
gallow 'rm -rf src/P/bin src/P/obj/Debug' "$MAIN"
gallow 'rm -rf src/*/obj' "$MAIN"
gallow 'rm -rf .claude/worktrees/stale' "$MAIN"
gallow 'rm -rf .claude/worktrees/stale/src' "$MAIN" agent-1
gallow "rm -f $WT/src/a.txt" "$MAIN"
gallow 'rm src/a.txt' "$WT"
gallow "rm -rf $G/scratch/junk /tmp/sharpy-hook-nonexistent" "$MAIN"
gallow 'grep -rn "rm -rf" src' "$MAIN"
unset OUT
# shellcheck disable=SC2016  # variables of the command under test
U_ALLOW=('tmp=$(mktemp -d); rm -rf "$tmp"'
         'rm -f "${OUT}"'
         'rm -f "${OUT}.log"'
         'tmp=$(mktemp -d); rm -rf "$tmp"/*'
         'for f in src/a.txt src/P/P.csproj; do rm -f "$f"; done'
         'for f in a.txt b.txt; do rm -f "src/$f"; done'
         'RESULTS=.claude/tmp/ps-1; mkdir -p "$RESULTS"; rm -rf "$RESULTS"'
         'rm -rf "$(mktemp -d)"'
         'cd "$(mktemp -d)" && rm -rf x')
for c in "${U_ALLOW[@]}"; do gallow "$c" "$MAIN"; done
# The main worktree itself, or a directory above it: denied for anyone, however it is spelled.
for c in 'rm -rf .' 'rm -rf ./' 'cd src && rm -rf ..' "rm -rf $MAIN" 'rm -rf ../main' "rm -rf $G/mai*" "rm -rf $G"; do
  gdeny "$c" "$MAIN"
  gdeny "$c" "$MAIN" agent-1
done
gdeny "rm -rf $MAIN" "$G"
gallow "rm -rf $G/other" "$MAIN"
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

echo "== require-agent-brief.sh"
# The block as contract §9 holds it today: a brief that pastes it must pass (drift between the
# hook's key sentences and the contract turns this red).
BLOCK=$(python3 -c 'import re,sys; s=open(sys.argv[1],encoding="utf-8").read(); print(re.search(r"^## 9\.[^\n]*\n(?:(?!> )[^\n]*\n)*?((?:> [^\n]*\n)+)", s, re.M).group(1), end="")' "$HOOKS/../../docs/design/verification-contract.md")
ajson() { python3 -c 'import json,sys; ti={"description":"d","prompt":sys.argv[2]}; ti.update({"subagent_type":sys.argv[3]} if sys.argv[3] else {}); print(json.dumps({"session_id":"t","hook_event_name":"PreToolUse","tool_name":sys.argv[1],"tool_input":ti}))' "$1" "$2" "$3"; }
adeny()  { run require-agent-brief.sh "$(ajson "$1" "$2" "$3")"; if [ "$RC" -eq 2 ] && printf '%s' "$ERR" | grep -qF 'never from git.'; then ok "deny: $4"; else bad "deny: $4" "rc=$RC err=$ERR"; fi; }
aallow() { run require-agent-brief.sh "$(ajson "$1" "$2" "$3")"; if [ "$RC" -eq 0 ] && [ -z "$ERR" ]; then ok "allow: $4"; else bad "allow: $4" "rc=$RC err=$ERR"; fi; }
if [ -n "$BLOCK" ]; then ok "contract §9 block found"; else bad "contract §9 block found" "no '> ' block under '## 9.'"; fi
TASK='Implement task 3 of the plan in src/Sharpy.Compiler/Semantic.'
PARA='Shared tree: never run git checkout, restore, clean, stash, reset or rm on repo paths; restore a mutation with cp; use .claude/scripts/dotnet-serialized.'
UNQUOTED=$(printf '%s\n' "$BLOCK" | sed 's/^> //')
ONELINE=$(printf '%s' "$UNQUOTED" | tr '\n' ' ')
INDENTED=$(printf '%s\n' "$UNQUOTED" | sed 's/^/    /')
HALF=$(printf '%s\n' "$UNQUOTED" | grep -vF 'never from git')
adeny  Agent "$TASK" general-purpose "general-purpose brief without the block"
adeny  Agent "$TASK" "" "no subagent_type (general-purpose) without the block"
adeny  Agent "$TASK" implementer "implementer brief without the block"
adeny  Agent "$TASK" Explore "Explore brief without the block"
adeny  Agent "$TASK
$PARA" general-purpose "a paraphrase of the block"
adeny  Agent "$TASK
$HALF" general-purpose "the block with its cp/restore line dropped"
adeny  Task "$TASK" general-purpose "the Task tool name"
if printf '%s' "$ERR" | grep -qF '> with ' && ! printf '%s' "$ERR" | grep -q '^## 10'; then
  ok "denial carries the §9 block and only it"; else bad "denial carries the §9 block and only it" "err=$ERR"; fi
aallow Agent "$TASK

$BLOCK" general-purpose "the block as quoted in the contract"
aallow Agent "$UNQUOTED
$TASK" implementer "the block unquoted"
aallow Agent "$TASK $ONELINE" "" "the block re-wrapped onto one line"
aallow Agent "SHARED TREE:
$INDENTED
$TASK" test-expert "the block indented"
for t in fork web-fetch claude-code-guide statusline-setup; do
  aallow Agent "$TASK" "$t" "exempt subagent_type $t"
done
run require-agent-brief.sh '{"tool_name":"Agent","tool_input":{"subagent_type":"general-purpose"}}'
if [ "$RC" -eq 0 ]; then ok "no prompt string: allow"; else bad "no prompt string: allow" "rc=$RC"; fi
run require-agent-brief.sh 'garbage'
if [ "$RC" -eq 0 ]; then ok "bad stdin: allow"; else bad "bad stdin: allow" "rc=$RC"; fi

echo
echo "hooks: $pass passed, $fail failed, $skip skipped"
[ "$fail" -eq 0 ]
