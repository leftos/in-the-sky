"""Which files in the repository are assets the ledger must cover."""

from pathlib import Path, PurePosixPath
from typing import Final

from provenance.repo import files_git_carries

ASSET_EXTENSIONS: Final[frozenset[str]] = frozenset(
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif",
        ".bmp",
        ".tga",
        ".svg",
        ".exr",
        ".hdr",
        ".ogg",
        ".wav",
        ".mp3",
        ".flac",
        ".opus",
        ".ttf",
        ".otf",
        ".woff",
        ".woff2",
        ".gdshader",
        ".gdshaderinc",
        ".glsl",
        ".onnx",
        ".gguf",
        ".safetensors",
    }
)


def is_asset(path: str) -> bool:
    """Says whether a path names a file the ledger must cover.

    Args:
        path: A repository-relative path, with forward slashes.

    Returns:
        True when the path's lowercase extension is one the ledger covers.
    """
    return PurePosixPath(path).suffix.lower() in ASSET_EXTENSIONS


def discover_assets(root: Path) -> list[str]:
    """Lists every asset git carries under a repository root.

    Args:
        root: The repository root.

    Returns:
        The repository-relative asset paths, sorted.
    """
    return sorted(path for path in files_git_carries(root) if is_asset(path))
