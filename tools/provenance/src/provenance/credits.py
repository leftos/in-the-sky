"""Rendering CREDITS.md from the ledger: one section per license, one line per entry."""

from collections.abc import Sequence
from pathlib import Path
from typing import Final

from provenance.ledger import Entry

CREDITS_PATH: Final = "CREDITS.md"
CREDITS_COMMAND: Final = "uv run --project tools/provenance python -m provenance credits"
HEADER: Final = f"Generated from `assets/PROVENANCE.toml` by `{CREDITS_COMMAND}`. Do not edit by hand."
EMPTY_NOTICE: Final = "No third-party or generated assets yet."


def render_credits(entries: Sequence[Entry]) -> str:
    """Renders the credits markdown the ledger produces.

    Args:
        entries: The validated ledger entries.

    Returns:
        The document, with LF endings and one trailing newline.
    """
    lines = [HEADER, ""]
    licenses = sorted({entry.license for entry in entries})
    if not licenses:
        lines.append(EMPTY_NOTICE)
    for license_id in licenses:
        lines += [f"## {license_id}", "", *(_entry_line(entry) for entry in _by_path(entries, license_id)), ""]
    return "\n".join(lines).rstrip("\n") + "\n"


def write_credits(root: Path, entries: Sequence[Entry]) -> Path:
    """Writes the credits markdown the ledger produces into a repository.

    Args:
        root: The repository root.
        entries: The validated ledger entries.

    Returns:
        The path written.
    """
    destination = root / CREDITS_PATH
    destination.write_text(render_credits(entries), encoding="utf-8", newline="\n")
    return destination


def _by_path(entries: Sequence[Entry], license_id: str) -> list[Entry]:
    return sorted((entry for entry in entries if entry.license == license_id), key=lambda entry: entry.path)


def _entry_line(entry: Entry) -> str:
    line = f"- {entry.path}: {entry.author}"
    if entry.source_url is not None:
        line += f", {entry.source_url}"
    if entry.modifications:
        line += f" (modified: {'; '.join(entry.modifications)})"
    if entry.share_alike:
        line += " (share-alike)"
    return line
