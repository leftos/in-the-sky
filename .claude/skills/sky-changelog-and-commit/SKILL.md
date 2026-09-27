---
name: sky-changelog-and-commit
description: "The In the Sky variant of the user-level changelog-and-commit skill. Use when the user says \"changelog and commit\", \"log it and commit\", \"update the changelog and commit\", \"changelog + commit\", or invokes /changelog-and-commit in this repo, and whenever the `sky-nextup` landing path writes a commit. Covers the working tree's uncommitted work only. Invoking it IS the approval to commit."
---

# Changelog and Commit, In the Sky

The project variant of the user-level `changelog-and-commit` skill, which defers to it in this repo. Invoking it is the approval: it sweeps the docs, reconciles the plan, writes the changelog when there is one, stages by name and commits, announcing each step and asking nothing. Older committed work missing from a changelog is `/update-changelog`'s job; `git log <baseline>..HEAD` is never a source of bullets.

## In the Sky specifics

- **No bullets before the first release** (owner, 2026-09-26). The repo has no release and no `CHANGELOG.md`. Until one exists, every Step 2 verdict is no-bullet: skip Steps 3, 4 and the changelog half of Step 6, and run the docs sweep, the plan reconciliation and the commit as usual. `CHANGELOG.md` is created by the first release cut, with that release's section and an empty `## Unreleased` above it; from the next commit on, Steps 3 and 4 apply. Never create it from this skill.
- **The changelog is one owner among several.** Before Step 5, walk the Docs map in `.claude/skills/sky-nextup/SKILL.md` against the diff's paths and bring every owning doc into the scope, true for the change.
- **`Docs: unchanged, <why>`.** A commit staging anything under `src/Sky.Content/` and no `docs/design/` file carries that line in its body, at the start of a line, naming why no design doc changed; the commit-msg hook refuses the commit without it once that hook lands. Write it only when the sweep found nothing to change.
- **Plan convention.** A resolved `docs/plans/MAIN.md` line leaves the index in this commit, moved ticked to the foot of `docs/plans/archive/YYYY-MM-done.md` with a `Landed YYYY-MM-DD: …` note; it is never ticked in place.
- **Hooks never skipped.** prek runs the whitespace fixers, `dotnet format style`, CSharpier, PSScriptAnalyzer, line length, gitleaks, the provenance check and a warnings-as-errors build. Never commit while an `implementer` has work in the same tree: prek stashes unstaged tracked edits but not untracked files, and the build hook compiles a mixed tree.
- **This skill stops at the commit.** Pushing is the owner's call at a milestone; `/nextup` owns what follows a landing.

## Step 0: Snapshot the index before anything touches it

`git status -sb > .tmp/changelog-commit-presnapshot.txt`. The first line names the ref: `## HEAD (no branch)` halts (a detached commit is never pushed); recover with `git checkout -B main <sha>` when `git merge-base --is-ancestor main HEAD` holds. Any branch other than the expected one halts and is named. Bucket the list: pre-staged (column 1 set, not `?`), unstaged (column 2 set), untracked (`??`). All three empty halts with "nothing to commit".

## Step 1: Scope

Anything pre-staged makes the scope exactly the pre-staged files. Nothing pre-staged makes it every modified tracked file plus every untracked file, announced in Step 5 rather than asked about. A secrets-shaped path (`.env`, `*credentials*`, `*.pem`, `*.key`, `id_rsa*`) halts the skill and asks; it is the one question it ever asks.

## Step 2: Read the diff and decide what it is

Read the content (`git diff --cached -- <paths>`, `git diff HEAD -- <paths>`, the Read tool for untracked files), never the file list alone.

- **Reach from the code, not the plan note.** A plan heading names what motivated the change, never what it reaches; resolve the gating condition and its call sites and word from that reach.
- **A mixed-shape diff splits.** Many call sites changed identically plus a small logic change lands as a `ref:` commit of the mechanical part first, then the behaviour change; only hunks that cannot be separated commit once, saying so.
- **Gate: does it warrant a bullet?** Before the first release, never. After it, planning docs for unbuilt work, internal refactors and test/CI-only diffs take none; check precedent with `rg -F "<topic>" CHANGELOG.md` and `git log --oneline -- <paths>`, and state the verdict with both findings in the announcement.

## Step 2b: Reconcile the plan

List the unchecked lines (`rg -n "^\s*- \[ \]" docs/plans/MAIN.md`, plus the subplans it links for the work in scope). Every line this diff resolves, or that an earlier commit of this session resolved and that outlived its fix, moves by the plan convention above; a tracking line broader than the diff stays, with the landed part recorded in its text. The plan files join the scope, and the announcement names the outcome (`Plan: 2 lines archived — <line>, <line>` or `Plan: nothing to reconcile`).

## Step 3: Mode, fresh or iterate (once `CHANGELOG.md` exists)

Match the topmost version heading against `git tag --sort=-creatordate | head -10` in `vX.Y.Z` and `X.Y.Z` forms. A matching tag means **fresh mode**: a new `## Unreleased` above it, no version and no date. An untagged top heading means **iterate mode**: edit that section in place, never a second heading. In iterate mode:

- A genuinely new topic gets its own bullet.
- Work that extends or supersedes a bullet in the section modifies that bullet to describe the end state; no second bullet.
- Work that reverts something bulleted drops that bullet.
- A fix to something first added or changed in this same unreleased section is never a `### Fixed` bullet: nothing is fixed for a reader who never saw it broken (user, 2026-09-20). Fold what the fix makes true into the bullet that introduced the thing; the test for each would-be Fixed bullet is `git tag --contains <the commit that introduced the behaviour>`, and no tag means fold or drop.

**Consolidate the section, not just your own bullets.** Touching the section folds any two bullets about one feature's successive states, or a Fixed bullet under a feature the section adds, in the same edit, and the announcement names the fold: the section reads as the difference between the last release and now, never the history of getting there.

## Step 4: Write the bullets

One sentence per bullet, at most 25 words, stating what works now. A reader scans bullets, so:

- No "Previously..." framing and no mechanism: describe the new state, never the bug behind it.
- The audience is players: no framework, class, method, property, file or project names, no exception types, decision numbers or attribution. The game's own words stay (a lever, a verdict, an event by its title, a button's text).
- Match the file's voice and its sub-headings (`### Added`, `### Changed`, `### Fixed`), inventing neither.
- No SHAs, author names, issue numbers or superlatives unless the file already carries them.
- One bullet per distinct change a reader would look for, never one per bug or per batch: a change a reader could miss inside another bullet, or revert on its own, is its own bullet.

## Step 5: Announce, then continue in the same turn

State the mode and section (or `No changelog before the first release`), the scope (file count, notable names), the docs sweep's result, the bullets, and the plan reconciliation. Transparency, not a gate.

## Step 6: Write and stage by name

When there are bullets, `Edit` `CHANGELOG.md` per the mode. Then `git add <scope-paths>` (with `CHANGELOG.md` when edited), never `git add -A` or `git add .`.

## Step 7: The commit message

- Subject: a type tag of at most four characters from the repo's set (`feat:`, `add:`, `fix:`, `ref:`, `test:`, `docs:`, `ci:`, `dep:`, `chore:`), chosen from the dominant non-changelog work, then the change itself in the imperative, at most 72 characters in all. `docs:` only for a doc-only diff.
- Body: one line per bullet or, with none, per distinct change, then the `Docs: unchanged, <why>` line when the specifics above require it.
- Trailers: the `Co-Authored-By:` and `Claude-Session:` lines the session supplies, on every commit; the landing path reads a missing trailer as a foreign commit.

## Step 8: Run the implicated tests, then commit

Show the message as a status ("Committing as: ..."), not a question. Run the tests the diff implicates: a changed or removed user-visible string is a search key, `rg -F "<literal>" tests/`, and every matching class runs (`pwsh ./sky.ps1 test -Project P -Filter "*XTests"`). A partial-tree scope follows "Partial commits under prek" in the user-level skill's `reference.md` (`~/.claude/skills/changelog-and-commit/reference.md`). Write the message to `.tmp/commit-msg.txt` with the Write tool, never a heredoc, then:

```bash
git commit -F .tmp/commit-msg.txt -- <exact paths>
git log -1 --oneline
git show --stat HEAD
```

Never `--no-verify`, `--amend` or `--no-gpg-sign`. A failed hook is surfaced, fixed forward, re-staged and committed as a **new** commit. Report the sha, the subject and the working-tree state.
