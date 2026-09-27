"""The ledger schema: the fields an entry carries, the licenses it may name, and what makes an entry invalid."""

import datetime
import tomllib
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Final, cast

LEDGER_PATH: Final = "assets/PROVENANCE.toml"

ALLOWED_LICENSES: Final[frozenset[str]] = frozenset(
    {
        "CC0-1.0",
        "LicenseRef-PublicDomain",
        "CC-BY-3.0",
        "CC-BY-4.0",
        "CC-BY-SA-3.0",
        "CC-BY-SA-4.0",
        "OFL-1.1",
        "MIT",
        "BSD-2-Clause",
        "BSD-3-Clause",
    }
)

ORIGINS: Final[tuple[str, ...]] = ("original", "library", "generated")
SHARE_ALIKE_PREFIX: Final = "CC-BY-SA-"

_TEXT_FIELDS: Final[tuple[str, ...]] = ("path", "origin", "license", "author", "source_url", "tool", "tool_version", "model", "prompt")
_DATE_FIELDS: Final[tuple[str, ...]] = ("retrieved", "created")
_REQUIRED_FIELDS: Final[tuple[str, ...]] = ("path", "origin", "license", "author", "modifications")
_GENERATED_FIELDS: Final[tuple[str, ...]] = ("tool", "tool_version", "model", "prompt", "created")
_SOURCED_ORIGINS: Final[frozenset[str]] = frozenset({"library", "generated"})
_KNOWN_FIELDS: Final[frozenset[str]] = frozenset({*_TEXT_FIELDS, *_DATE_FIELDS, "modifications", "share_alike"})


@dataclass(frozen=True)
class Entry:
    """One ledger entry, as the credits render it."""

    path: str
    license: str
    author: str
    source_url: str | None
    modifications: tuple[str, ...]
    share_alike: bool


@dataclass(frozen=True)
class Ledger:
    """The ledger file once read: the entries that validate, and every problem found reading it."""

    entries: tuple[Entry, ...]
    problems: tuple[str, ...]
    readable: bool


def load_ledger(root: Path) -> Ledger:
    """Reads and validates `assets/PROVENANCE.toml` under a repository root.

    Args:
        root: The repository root.

    Returns:
        The entries that validate, and one line per problem, each without the "PROVENANCE: " prefix. `readable` is False
        when the file itself could not be read, and then the problems are the file-level ones alone.
    """
    raw_entries, problems = _read_entries(root)
    if raw_entries is None:
        return Ledger((), tuple(problems), readable=False)
    entries: list[Entry] = []
    for index, raw in enumerate(raw_entries, start=1):
        entry, entry_problems = _validate_entry(root, raw, index)
        problems += entry_problems
        if entry is not None:
            entries.append(entry)
    return Ledger(tuple(entries), tuple(problems), readable=True)


def _read_entries(root: Path) -> tuple[list[Any] | None, list[str]]:
    try:
        document = tomllib.loads((root / LEDGER_PATH).read_text(encoding="utf-8"))
    except FileNotFoundError:
        return None, [f"{LEDGER_PATH}: the ledger file is missing"]
    except tomllib.TOMLDecodeError as error:
        return None, [f"{LEDGER_PATH}: is not valid TOML: {error}"]
    except OSError as error:
        return None, [f"{LEDGER_PATH}: cannot be read: {error.strerror or error}"]
    raw_entries = document.get("asset", [])
    if not isinstance(raw_entries, list):
        return None, [f"{LEDGER_PATH}: 'asset' must be an array of tables"]
    return raw_entries, []


def _validate_entry(root: Path, raw: object, index: int) -> tuple[Entry | None, list[str]]:
    if not isinstance(raw, dict):
        return None, [f"{_entry_name(None, index)}: must be a table"]
    name = _entry_name(raw.get("path"), index)
    problems = _unknown_keys(raw, name) + _missing_fields(raw, name, _REQUIRED_FIELDS)
    if problems:
        return None, problems
    problems += _wrong_types(raw, name)
    if problems:
        return None, problems
    problems += _origin_and_license(raw, name)
    problems += _share_alike(raw, name)
    problems += _source_url(raw, name)
    problems += _generated(root, raw, name)
    problems += _path_on_disk(root, raw, name)
    if problems:
        return None, problems
    return _entry(raw), []


def _entry_name(value: object, index: int) -> str:
    return value if isinstance(value, str) else f"entry #{index}"


def _unknown_keys(raw: Mapping[str, Any], name: str) -> list[str]:
    return [f"{name}: unknown key '{key}'" for key in raw if key not in _KNOWN_FIELDS]


def _missing_fields(raw: Mapping[str, Any], name: str, fields: Sequence[str]) -> list[str]:
    return [f"{name}: field '{field}' is required" for field in fields if field not in raw]


def _wrong_types(raw: Mapping[str, Any], name: str) -> list[str]:
    problems = [f"{name}: field '{field}' must be a string" for field in _TEXT_FIELDS if field in raw and not isinstance(raw[field], str)]
    problems += [f"{name}: field '{field}' must be a TOML date" for field in _DATE_FIELDS if field in raw and not _is_date(raw[field])]
    if "share_alike" in raw and not isinstance(raw["share_alike"], bool):
        problems.append(f"{name}: field 'share_alike' must be a boolean")
    if not isinstance(raw["modifications"], list) or any(not isinstance(item, str) for item in raw["modifications"]):
        problems.append(f"{name}: field 'modifications' must be an array of strings")
    return problems


def _is_date(value: object) -> bool:
    return isinstance(value, datetime.date) and not isinstance(value, datetime.datetime)


def _origin_and_license(raw: Mapping[str, Any], name: str) -> list[str]:
    problems = []
    if raw["origin"] not in ORIGINS:
        problems.append(f"{name}: origin '{raw['origin']}' must be one of {_joined(ORIGINS)}")
    if raw["license"] not in ALLOWED_LICENSES:
        problems.append(f"{name}: license '{raw['license']}' is not in the allowlist")
    return problems


def _joined(values: Sequence[str]) -> str:
    return ", ".join(f"'{value}'" for value in values[:-1]) + f" or '{values[-1]}'"


def _share_alike(raw: Mapping[str, Any], name: str) -> list[str]:
    license_id = raw["license"]
    if isinstance(license_id, str) and license_id.startswith(SHARE_ALIKE_PREFIX):
        return [] if raw.get("share_alike") is True else [f"{name}: license '{license_id}' requires share_alike = true"]
    if "share_alike" in raw:
        return [f"{name}: share_alike is only for CC-BY-SA licenses"]
    return []


def _source_url(raw: Mapping[str, Any], name: str) -> list[str]:
    problems = []
    if raw.get("source_url") is None and raw["origin"] in _SOURCED_ORIGINS:
        problems.append(f"{name}: field 'source_url' is required when origin is '{raw['origin']}'")
    if raw.get("source_url") is not None and "retrieved" not in raw:
        problems.append(f"{name}: field 'retrieved' is required when source_url is set")
    return problems


def _generated(root: Path, raw: Mapping[str, Any], name: str) -> list[str]:
    if raw["origin"] != "generated":
        return []
    problems = _missing_fields(raw, name, _GENERATED_FIELDS)
    if problems:
        return problems
    prompt = raw["prompt"]
    if not (root / prompt).is_file():
        return [f"{name}: prompt file '{prompt}' is not in the repo"]
    return []


def _path_on_disk(root: Path, raw: Mapping[str, Any], name: str) -> list[str]:
    path = raw["path"]
    if PurePosixPath(path).is_absolute():
        return [f"{name}: path must be repository-relative"]
    if "\\" in path:
        return [f"{name}: path must use forward slashes"]
    if not (root / path).is_file():
        return [f"{name}: the file is not in the repo"]
    return []


def _entry(raw: Mapping[str, Any]) -> Entry:
    source_url = raw.get("source_url")
    return Entry(
        path=raw["path"],
        license=raw["license"],
        author=raw["author"],
        source_url=source_url if isinstance(source_url, str) else None,
        modifications=cast("tuple[str, ...]", tuple(raw["modifications"])),
        share_alike=raw.get("share_alike") is True,
    )
