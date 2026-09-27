"""Throwaway git repositories for the checker's tests, and the calls the tests make into the command line."""

import subprocess
from pathlib import Path

import pytest

from provenance import cli

LEDGER_HEADER = """# The asset provenance ledger: one [[asset]] table per asset file in this repo.

"""


def make_repo(tmp_path: Path) -> Path:
    """Builds a throwaway git repository holding an empty ledger and freshly written credits.

    Args:
        tmp_path: The test's temporary directory.

    Returns:
        The repository root.
    """
    root = tmp_path / "repo"
    (root / "assets").mkdir(parents=True)
    subprocess.run(("git", "init", "--quiet"), cwd=root, check=True, capture_output=True)
    write_ledger(root, LEDGER_HEADER)
    regenerate_credits(root)
    return root


def write_ledger(root: Path, text: str) -> None:
    """Writes the ledger file of a throwaway repository.

    Args:
        root: The repository root.
        text: The file's whole contents.
    """
    (root / "assets" / "PROVENANCE.toml").write_text(text, encoding="utf-8", newline="\n")


def write_file(root: Path, relative: str, data: bytes = b"") -> None:
    """Writes a file into a throwaway repository, making its folders.

    Args:
        root: The repository root.
        relative: The repo-relative path, with forward slashes.
        data: The bytes to write.
    """
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def regenerate_credits(root: Path) -> int:
    """Runs the checker's `credits` command over a throwaway repository.

    Args:
        root: The repository root.

    Returns:
        The command's exit code.
    """
    return cli.main(["credits", "--root", str(root)])


def run_check(root: Path, capsys: pytest.CaptureFixture[str]) -> tuple[int, str]:
    """Runs the checker's `check` command over a throwaway repository.

    Args:
        root: The repository root.
        capsys: The fixture that captures what the command wrote.

    Returns:
        The exit code, and everything the command wrote to standard output.
    """
    code = cli.main(["check", "--root", str(root)])
    return code, capsys.readouterr().out
