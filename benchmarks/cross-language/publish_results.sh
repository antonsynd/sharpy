#!/usr/bin/env bash
# Seed and publish the weekly cross-language benchmark results on a dedicated branch.
#
# The results used to be committed straight to `dev`, which the `dev` ruleset (pull request +
# required checks, no bypass actors) rejects with GH013. They now live on their own branch,
# `benchmark-results` by default, whose tree is exactly the contents of results/ (latest.json,
# latest.md, history.json, trend.svg) and whose history is one commit per published run. No
# ruleset covers that branch, and no workflow triggers on a push to it.
#
# Usage:
#   publish_results.sh seed    <results-dir> [remote] [branch]
#       If <branch> exists on <remote>, overwrite <results-dir>/history.json with the branch's copy,
#       so this run appends to the published history. If the branch is absent, keep the copy in
#       the checked-out tree (the seed for the first publish). Any other failure exits non-zero.
#   publish_results.sh publish <results-dir> [remote] [branch]
#       Commit <results-dir>'s contents as the whole tree of <branch> on top of its current tip
#       (creating the branch as an orphan if absent) and push it. Unchanged results are a no-op.
#       A rejected push exits non-zero: nothing is retried, rebased or masked.
#
# Run from inside the git checkout whose <remote> is the publishing target. The commit identity
# defaults to github-actions[bot]; GIT_AUTHOR_*/GIT_COMMITTER_* in the environment override it.

set -euo pipefail

usage() { echo "usage: $0 {seed|publish} <results-dir> [remote] [branch]" >&2; exit 64; }

[ $# -ge 2 ] || usage
cmd=$1
results_dir=$(cd "$2" && pwd)
remote=${3:-origin}
branch=${4:-benchmark-results}

# 0 = branch exists, 2 = branch absent; anything else (auth, network) is a hard failure.
branch_state() {
  local rc=0
  git ls-remote --exit-code --heads "$remote" "refs/heads/$branch" >/dev/null || rc=$?
  case $rc in
    0) echo present ;;
    2) echo absent ;;
    *) echo "::error::git ls-remote $remote $branch failed (exit $rc)" >&2; return 1 ;;
  esac
}

fetch_tip() {
  git fetch --no-tags --depth=1 "$remote" "+refs/heads/$branch:refs/remotes/$remote/$branch"
}

seed() {
  local state
  state=$(branch_state)
  if [ "$state" = absent ]; then
    echo "seed: $branch absent on $remote; keeping the in-tree history.json"
    return 0
  fi
  fetch_tip
  git show "refs/remotes/$remote/$branch:history.json" > "$results_dir/history.json"
  echo "seed: history.json taken from $remote/$branch ($(git rev-parse --short "refs/remotes/$remote/$branch"))"
}

publish() {
  local state work date subject tmp_branch
  state=$(branch_state)
  work=$(mktemp -d)
  tmp_branch="publish-$branch-$$"
  # Cleanup only; never alters the exit status. shellcheck disable=SC2064 (expand now).
  # shellcheck disable=SC2064
  trap "git worktree remove --force '$work' >/dev/null 2>&1 || rm -rf '$work'; git branch -D '$tmp_branch' >/dev/null 2>&1 || true" EXIT

  if [ "$state" = present ]; then
    fetch_tip
    git worktree add --quiet --detach "$work" "refs/remotes/$remote/$branch"
  else
    echo "publish: $branch absent on $remote; creating it as an orphan branch"
    git worktree add --quiet --detach "$work"
    git -C "$work" checkout --quiet --orphan "$tmp_branch"
    git -C "$work" read-tree --empty
  fi

  # The branch tree is exactly results/: clear the worktree (keeping its .git link), then copy.
  find "$work" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
  cp -R "$results_dir"/. "$work"/
  git -C "$work" add -A

  if git -C "$work" rev-parse --verify --quiet HEAD >/dev/null && git -C "$work" diff --cached --quiet; then
    echo "publish: results unchanged; nothing to commit"
    return 0
  fi

  date=$(date -u +%Y-%m-%d)
  subject="bench: weekly cross-language benchmark results $date"
  if [ ${#subject} -gt 72 ]; then
    echo "::error::commit subject is ${#subject} chars (> 72): $subject" >&2
    return 1
  fi
  local body="Published by the Cross-Language Benchmarks workflow."
  if [ -n "${GITHUB_RUN_ID:-}" ]; then
    body="$body
Run: ${GITHUB_SERVER_URL:-https://github.com}/${GITHUB_REPOSITORY:-}/actions/runs/$GITHUB_RUN_ID"
  fi

  GIT_AUTHOR_NAME=${GIT_AUTHOR_NAME:-github-actions[bot]} \
  GIT_AUTHOR_EMAIL=${GIT_AUTHOR_EMAIL:-41898282+github-actions[bot]@users.noreply.github.com} \
  GIT_COMMITTER_NAME=${GIT_COMMITTER_NAME:-github-actions[bot]} \
  GIT_COMMITTER_EMAIL=${GIT_COMMITTER_EMAIL:-41898282+github-actions[bot]@users.noreply.github.com} \
    git -C "$work" commit --quiet -m "$subject" -m "$body"

  if ! git -C "$work" push "$remote" "HEAD:refs/heads/$branch"; then
    echo "::error::push of $branch to $remote was rejected; the results are in this run's artifact" >&2
    return 1
  fi
  echo "publish: $branch -> $(git -C "$work" rev-parse --short HEAD)"
}

case $cmd in
  seed) seed ;;
  publish) publish ;;
  *) usage ;;
esac
