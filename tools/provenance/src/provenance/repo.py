"""The git-facing edge of the checker: where the repository root is, and which files git would carry."""

import subprocess
from collections.abc import Sequence
from pathlib import Path
from typing import Final

_GIT: Final = "git"
_ROOT_ARGUMENTS: Final[tuple[str, ...]] = ("rev-parse", "--show-toplevel")
_FILES_ARGUMENTS: Final[tuple[str, ...]] = ("ls-files", "-z", "--cached", "--others", "--exclude-standard")


class RepoError(RuntimeError):
    """git could not answer a question about the repository."""


def repo_root() -> Path:
    """Finds the root of the repository the working directory is in.

    Returns:
        The absolute path of the repository root.

    Raises:
        RepoError: git is not on the path, or the working directory is not in a repository.
    """
    return Path(_git(_ROOT_ARGUMENTS, Path.cwd()).strip()).resolve()


def files_git_carries(root: Path) -> list[str]:
    """Lists the repository-relative paths of the files git carries: the tracked ones and the untracked but not ignored ones.

    Args:
        root: The repository root, where git runs.

    Returns:
        The paths git lists, in git's order.
    """
    return [name for name in _git(_FILES_ARGUMENTS, root).split("\0") if name]


def _git(arguments: Sequence[str], cwd: Path) -> str:
    try:
        completed = subprocess.run((_GIT, *arguments), check=True, capture_output=True, cwd=cwd, text=True)
    except OSError as error:
        message = f"git could not be run: {error}"
        raise RepoError(message) from error
    except subprocess.CalledProcessError as error:
        message = f"git {' '.join(arguments)} failed in {cwd}: {error.stderr.strip()}"
        raise RepoError(message) from error
    return completed.stdout
