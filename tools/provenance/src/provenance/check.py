"""The gate's rules: the ledger against the repo's assets, and the credits against the ledger."""

from collections import Counter
from pathlib import Path

from provenance.assets import discover_assets
from provenance.credits import CREDITS_COMMAND, CREDITS_PATH, render_credits
from provenance.ledger import Entry, load_ledger


def check(root: Path) -> list[str]:
    """Collects every problem the provenance gate reports for a repository.

    Args:
        root: The repository root.

    Returns:
        One line per problem, each without the "PROVENANCE: " prefix; empty when the repository is clean.
    """
    ledger = load_ledger(root)
    if not ledger.readable:
        return list(ledger.problems)
    return [
        *ledger.problems,
        *_duplicate_paths(ledger.entries),
        *_unentered_assets(root, ledger.entries),
        *_stale_credits(root, ledger.entries),
    ]


def _duplicate_paths(entries: tuple[Entry, ...]) -> list[str]:
    counts = Counter(entry.path for entry in entries)
    return [f"{path}: {count} entries have this path" for path, count in counts.items() if count > 1]


def _unentered_assets(root: Path, entries: tuple[Entry, ...]) -> list[str]:
    covered = {entry.path for entry in entries}
    return [f"{asset}: no ledger entry" for asset in discover_assets(root) if asset not in covered]


def _stale_credits(root: Path, entries: tuple[Entry, ...]) -> list[str]:
    credits_path = root / CREDITS_PATH
    current = credits_path.read_text(encoding="utf-8") if credits_path.is_file() else None
    if current == render_credits(entries):
        return []
    return [f"{CREDITS_PATH}: does not match the ledger; run `{CREDITS_COMMAND}`"]
