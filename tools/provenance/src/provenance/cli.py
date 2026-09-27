"""The provenance checker's command line: `check` gates the repository, `credits` regenerates CREDITS.md."""

import argparse
import sys
from collections.abc import Sequence
from pathlib import Path
from typing import Final

from provenance.check import check
from provenance.credits import write_credits
from provenance.ledger import load_ledger
from provenance.repo import RepoError, repo_root

_PROBLEM_PREFIX: Final = "PROVENANCE: "
_CHECK: Final = "check"
_CREDITS: Final = "credits"
_ROOT_HELP: Final = "the repository root; without it, the root of the repository the working directory is in"
_COMMANDS: Final[tuple[tuple[str, str], ...]] = (
    (_CHECK, "report every asset without an entry, every invalid entry, and credits that no longer match the ledger"),
    (_CREDITS, "write CREDITS.md from the ledger"),
)


def build_parser() -> argparse.ArgumentParser:
    """Builds the command line's parser.

    Returns:
        The parser, with one subcommand a command.
    """
    parser = argparse.ArgumentParser(prog="provenance", description="Gates the repository's assets on the ledger, and generates CREDITS.md from it.")
    commands = parser.add_subparsers(dest="command", required=True)
    for name, help_text in _COMMANDS:
        commands.add_parser(name, help=help_text).add_argument("--root", type=Path, default=None, help=_ROOT_HELP)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Runs one command of the checker.

    Args:
        argv: The words after the program's name, or None to read them from the process.

    Returns:
        0 when the command succeeded, 1 when it found problems, 2 when the repository could not be read.
    """
    arguments = build_parser().parse_args(argv)
    try:
        root = repo_root() if arguments.root is None else Path(arguments.root).resolve()
        return _write_credits(root) if arguments.command == _CREDITS else _report(check(root))
    except RepoError as error:
        sys.stderr.write(f"error: {error}\n")
        return 2


def _write_credits(root: Path) -> int:
    ledger = load_ledger(root)
    if not ledger.readable or ledger.problems:
        return _report(list(ledger.problems))
    write_credits(root, ledger.entries)
    return 0


def _report(problems: list[str]) -> int:
    for problem in problems:
        sys.stdout.write(f"{_PROBLEM_PREFIX}{problem}\n")
    return 1 if problems else 0
