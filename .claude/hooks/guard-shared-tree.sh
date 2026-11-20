#!/bin/bash
# PreToolUse(Bash) hook: the shared-tree contract (docs/design/verification-contract.md §9) and the
# force-push rule (CLAUDE.md › Git & Release), enforced instead of asked for. Denies (exit 2):
#
#   1. In the repository's MAIN worktree (`git rev-parse --git-dir` = `--git-common-dir`), the one
#      a session's agents share (a linked worktree — .claude/worktrees/*, a scratchpad `git
#      worktree add` — is its owner's own and is never judged here):
#      a. a SUBAGENT (hook input has `agent_id`) may not run `git checkout|restore|clean|stash|
#         reset|switch` in any form but `stash list|show`: agents never move the shared branch;
#      b. the LEAD (no `agent_id`) may move and create branches — `switch <b>`, `switch -c <new>
#         [start]`, `switch --detach <c>`, `checkout <b>`, `checkout -b <new> [start]`, which git
#         itself refuses when they would overwrite local changes — but not the forms that destroy
#         uncommitted work: `restore`; `clean` without -n/--dry-run; `stash` but list/show;
#         `reset` but --soft; checkout/switch with -f/--force/--discard-changes/-m/--merge/
#         --ours/--theirs/-p/--patch; `checkout` with a pathspec (`--`, `.`, or an argument that
#         is neither a commit, by `rev-parse --verify <arg>^{commit}`, nor a refs/remotes/*/<arg>).
#      The docs (code.claude.com/docs/en/hooks, "Hooks in subagents"): "When a subagent calls a
#      tool, tool events such as `PreToolUse` and `PostToolUse` fire the same configured hooks as
#      in the main conversation, and the input carries the `agent_id` and `agent_type` common
#      input fields that identify the subagent."
#   2. `rm` of a path inside the main worktree, except under .claude/tmp/ and a .NET build output
#      (a `bin/` or `obj/` directory beside a *.*proj file; build_tools/bin/ is source). A path
#      in a linked worktree or outside the repository (scratchpad, /tmp) is allowed. The path's
#      parent decides, so removing a linked worktree's root is denied: `git worktree remove`.
#   3. Staging without per-file pathspecs, in any worktree of this repository: `git add -A|--all|
#      -u|--update|.|./|:/` and `git commit -a|--all`. -u and commit -a are the same hazard as -A:
#      they stage every tracked file's changes, a peer's uncommitted hunks included.
#   4. A forced `git push` (`--force`, `-f`, `--force-with-lease`, `+refspec`, `--mirror`) unless it
#      carries both `--force-with-lease` and `--force-if-includes` and every pushed ref is
#      `claude/*` (so never dev or mainline). `--mirror`, `--all`, `--branches`, `--tags` never.
#
# The worktree is resolved from the command itself: `git -C <dir>` (cumulative), `--work-tree`,
# a preceding `cd <dir>`/`pushd`, else the hook input's `cwd`. "This repository" is the one whose
# common dir matches that of $CLAUDE_PROJECT_DIR (else `cwd`), so a scratch repo is not guarded.
# Commands are found by .claude/hooks/shell_commands.py: compound lists, pipes, subshells,
# `$(...)`, backticks, `bash -c '...'`, `eval`, leading VAR=v, `env`/`sudo`/`timeout`/`xargs`...
#
# Cannot see: a script file (`bash x.sh`), a function or alias, a python/node subprocess, `find
# -delete`, `xargs rm` input, `git rm`, `unlink`/`mv`, a path held in `$(...)`, a variable set
# earlier in the same command, a `git` alias, `--git-dir` without `--work-tree`. An unparseable
# command (unterminated quote) is allowed: bash refuses to run it as well.
#
# Hook input is JSON on stdin (tool_input.command, cwd). Falsified by .claude/hooks/test-hooks.sh.
input=$(cat)
read -r -d '' PROG <<'PY'
import glob, json, os, subprocess, sys
sys.dont_write_bytecode = True
sys.path.insert(0, os.environ["HOOKS_DIR"])
import shell_commands as sc

try:
    data = json.loads(os.environ["HOOK_INPUT"])
    cmd = data.get("tool_input", {}).get("command", "")
    cwd = data.get("cwd") or os.getcwd()
    agent = bool(data.get("agent_id"))
except Exception:
    sys.exit(0)
if not isinstance(cmd, str) or not cmd:
    sys.exit(0)

_cache = {}
def repo_of(path):
    """(toplevel, git_dir, common_dir) of the worktree holding path, or None."""
    p = path
    while p and not os.path.isdir(p):
        p = os.path.dirname(p)
    if not p:
        return None
    if p not in _cache:
        r = subprocess.run(["git", "-C", p, "rev-parse", "--path-format=absolute",
                            "--show-toplevel", "--git-dir", "--git-common-dir"],
                           capture_output=True, text=True)
        lines = r.stdout.split("\n")
        _cache[p] = (tuple(os.path.realpath(x) for x in lines[:3])
                     if r.returncode == 0 and len(lines) >= 3 and all(lines[:3]) else None)
    return _cache[p]

project = repo_of(os.environ.get("CLAUDE_PROJECT_DIR") or cwd)
def ours(info):
    return bool(info and project and info[2] == project[2])
def main_tree(info):
    return ours(info) and info[1] == info[2]

def deny(msg):
    sys.stderr.write("BLOCKED by .claude/hooks/guard-shared-tree.sh: " + msg + "\n")
    sys.exit(2)

SHARED = ("The main worktree is shared with this session's agents (contract §9): REPORT `git status`, "
          "do not make it clean; restore a mutation from your `cp` copy. To reset, stash or discard, "
          "work in your own worktree (`git worktree add <scratchpad>/wt <ref>`), where it is allowed.")
AGENT = ("Agents never move or clean the shared tree's branch (contract §9); the lead does. "
         "Work in your own worktree (`git worktree add <scratchpad>/wt <ref>`) if you need one.")
GIT_OPTS_WITH_ARG = {"-C", "-c", "--git-dir", "--work-tree", "--namespace", "--config-env",
                     "--exec-path", "--super-prefix"}
FORCE_LONG = {"--force", "--discard-changes", "--merge", "--ours", "--theirs", "--patch"}
CO_NAME_OPTS = {"-b", "-B", "--orphan", "-c", "-C", "--create", "--force-create"}

def is_commit(arg, where):
    r = subprocess.run(["git", "-C", where, "rev-parse", "--verify", "--quiet", arg + "^{commit}"],
                       capture_output=True, text=True)
    if r.returncode == 0:
        return True
    r = subprocess.run(["git", "-C", where, "for-each-ref", "--count=1", "--format=%(refname)",
                        "refs/remotes/*/" + arg], capture_output=True, text=True)
    return bool(r.stdout.strip())

def destroys(verb, rest, where):
    """Why this lead-run verb can destroy uncommitted work in the main worktree, or None."""
    shorts = "".join(a[1:] for a in rest if a.startswith("-") and not a.startswith("--"))
    if verb == "restore":
        return "`git restore` discards uncommitted changes"
    if verb == "clean":
        if "--dry-run" in rest or "n" in shorts:
            return None
        return "`git clean` deletes untracked files"
    if verb == "stash":
        if rest and rest[0] in ("list", "show"):
            return None
        return "`git stash` moves uncommitted changes (a peer's included) off the tree"
    if verb == "reset":
        return None if "--soft" in rest else "`git reset` (other than --soft) discards or unstages work"
    # checkout / switch
    for k, a in enumerate(rest):
        if a == "--":
            break
        if a.split("=", 1)[0] in FORCE_LONG or a == "--pathspec-from-file":
            return "`git %s %s` overwrites uncommitted changes" % (verb, a)
        if a.startswith("-") and not a.startswith("--"):
            for ch in a[1:]:
                if ch in "bBcC":
                    break
                if ch in "fmp":
                    return "`git %s %s` overwrites uncommitted changes" % (verb, a)
    if verb != "checkout":
        return None
    if "--" in rest:
        return "`git checkout -- <path>` overwrites the path's uncommitted changes"
    pos, j = [], 0
    while j < len(rest):
        a = rest[j]
        if a in CO_NAME_OPTS:
            j += 2
            continue
        if a.startswith("-"):  # an option, or `-` (the previous branch)
            j += 1
            continue
        pos.append(a)
        j += 1
    for a in pos:
        if a == "." or not is_commit(a, where):
            return "`git checkout %s` names a path: it overwrites that path's uncommitted changes" % a
    return None

def check_git(argv, cwd):
    args, i, where = argv[1:], 0, cwd
    while i < len(args) and args[i].startswith("-"):
        a = args[i]
        if a in GIT_OPTS_WITH_ARG and i + 1 < len(args):
            if a == "-C":
                where = sc.resolve(args[i + 1], where)
            elif a == "--work-tree":
                where = sc.resolve(args[i + 1], where)
            i += 2
            continue
        if a.startswith("--work-tree="):
            where = sc.resolve(a.split("=", 1)[1], where)
        i += 1
    if i >= len(args):
        return
    verb, rest = args[i], args[i + 1:]
    info = repo_of(where)
    if verb in ("checkout", "restore", "clean", "stash", "reset", "switch"):
        if main_tree(info):
            if agent:
                if not (verb == "stash" and rest and rest[0] in ("list", "show")):
                    deny("`git %s` by a subagent in the main worktree %s. %s" % (verb, info[0], AGENT))
            else:
                why = destroys(verb, rest, info[0])
                if why:
                    deny("%s in the main worktree %s. %s" % (why, info[0], SHARED))
    elif verb == "add" and ours(info):
        for a in rest:
            if a == "--":
                break
            if a in ("-A", "--all", "-u", "--update", "--no-ignore-removal") or (
                    a.startswith("-") and not a.startswith("--") and set(a[1:]) & set("Au")):
                deny("`git add %s` stages every change, a peer's hunks included (contract §9). "
                     "Stage explicit per-file pathspecs (`git add path/to/file`) and check "
                     "`git diff --cached --stat`." % a)
        if any(p in (".", "./", ":/", ":", "*") for p in rest):
            deny("`git add .` stages every change, a peer's hunks included (contract §9). "
                 "Stage explicit per-file pathspecs and check `git diff --cached --stat`.")
    elif verb == "commit" and ours(info):
        j = 0
        while j < len(rest):
            a = rest[j]
            if a == "--":
                break
            if a == "--all":
                deny("`git commit --all` stages every tracked change (contract §9). Stage per-file, then commit.")
            if a in ("--message", "--file", "--author", "--date", "--template", "--reuse-message",
                     "--reedit-message", "--fixup", "--squash", "--trailer", "--cleanup"):
                j += 2
                continue
            if a.startswith("-") and not a.startswith("--"):
                for k, ch in enumerate(a[1:]):
                    if ch == "a":
                        deny("`git commit -a` stages every tracked change (contract §9). Stage per-file, then commit.")
                    if ch in "mFcCt":
                        if k == len(a) - 2:
                            j += 1
                        break
            j += 1
    elif verb == "push":
        check_push(rest, where, info)

def check_push(rest, where, info):
    flags, pos, j = set(), [], 0
    while j < len(rest):
        a = rest[j]
        if a == "--":
            pos += rest[j + 1:]
            break
        if a.startswith("--"):
            flags.add(a.split("=", 1)[0])
            if a in ("--repo", "--receive-pack", "--exec", "--push-option"):
                j += 1
        elif a.startswith("-") and len(a) > 1:
            for ch in a[1:]:
                flags.add("-" + ch)
            if a[-1] == "o":
                j += 1
        else:
            pos.append(a)
        j += 1
    refspecs = pos[1:]
    forced = bool(flags & {"--force", "-f", "--force-with-lease", "--mirror"}) or any(
        r.startswith("+") for r in refspecs)
    if not forced:
        return
    rule = ("Force-push only with both `--force-with-lease` and `--force-if-includes`, only to "
            "`claude/*` branches, never dev or mainline (CLAUDE.md › Git & Release).")
    if flags & {"--mirror", "--all", "--branches", "--tags"}:
        deny("a forced `git push` of every ref. " + rule)
    if "--force" in flags or "-f" in flags or not {"--force-with-lease", "--force-if-includes"} <= flags:
        deny("a forced `git push` without both lease flags. " + rule)
    dsts = []
    for r in refspecs or ["HEAD"]:
        r = r.lstrip("+")
        dst = r.split(":", 1)[1] if ":" in r else r
        if dst == "HEAD":
            out = subprocess.run(["git", "-C", where, "symbolic-ref", "--short", "-q", "HEAD"],
                                 capture_output=True, text=True).stdout.strip()
            dst = out or "HEAD"
        dsts.append(dst[len("refs/heads/"):] if dst.startswith("refs/heads/") else dst)
    for d in dsts:
        if not d.startswith("claude/"):
            deny("a forced `git push` to `%s`. %s" % (d, rule))

def check_rm(argv, cwd):
    targets, opts = [], True
    for a in argv[1:]:
        if opts and a == "--":
            opts = False
            continue
        if opts and a.startswith("-") and a != "-":
            continue
        targets.append(a)
    for t in targets:
        path = sc.resolve(t, cwd)
        parent = os.path.dirname(path)
        info = repo_of(parent)
        if not main_tree(info):
            continue
        rel = os.path.relpath(os.path.join(os.path.realpath(parent), os.path.basename(path)), info[0])
        parts = rel.split(os.sep)
        if rel == "." or not parts:
            deny("`rm` of the main worktree itself.")
        if parts[:2] == [".claude", "tmp"] and len(parts) > 2:
            continue
        if any(p in ("bin", "obj") and glob.glob(os.path.join(info[0], *parts[:k], "*.*proj"))
               for k, p in enumerate(parts)):
            continue
        deny("`rm %s` inside the shared main worktree %s. Restore a mutation from the copy you made "
             "(`cp`); put scratch files in your scratchpad or .claude/tmp/ (bin/ and obj/ build "
             "outputs may be removed). Contract §9." % (t, info[0]))

def visit(argv, cwd):
    name = os.path.basename(argv[0])
    if name == "git":
        check_git(argv, cwd)
    elif name == "rm":
        check_rm(argv, cwd)

try:
    sc.walk(cmd, cwd, visit)
except sc.ParseError:
    sys.exit(0)
sys.exit(0)
PY
HOOK_INPUT="$input" HOOKS_DIR="$(cd "$(dirname "$0")" && pwd)" exec python3 -c "$PROG"
