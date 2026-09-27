"""The provenance checker's rules, each proved against a throwaway repository."""

from pathlib import Path

import pytest
from helpers import make_repo, regenerate_credits, run_check, write_file, write_ledger

_SEAT = "src/Sky.Client/Art/seat.png"
_CHIME = "Audio/Chime.ogg"

_ORIGINAL = """[[asset]]
path = "{path}"
origin = "original"
license = "{license}"
author = "{author}"
modifications = {modifications}
{extra}"""

_LIBRARY = """[[asset]]
path = "{path}"
origin = "library"
license = "CC0-1.0"
author = "Kenney"
{source}modifications = []
"""

_GENERATED = """[[asset]]
path = "{path}"
origin = "generated"
license = "CC0-1.0"
author = "Leftos"
source_url = "https://example.invalid/pack"
retrieved = 2026-09-26
modifications = []
{generation}"""


def original(path: str, license_id: str = "MIT", author: str = "Leftos", modifications: str = "[]", extra: str = "") -> str:
    """Builds one original-work entry."""
    return _ORIGINAL.format(path=path, license=license_id, author=author, modifications=modifications, extra=extra)


def library(path: str, source: str = 'source_url = "https://kenney.nl/pack"\nretrieved = 2026-09-26\n') -> str:
    """Builds one library entry; `source` holds its source_url and retrieved lines."""
    return _LIBRARY.format(path=path, source=source)


def generated(path: str, prompt: str = "prompts/art.md", generation: str | None = None) -> str:
    """Builds one generated entry; `generation` replaces the generation block it writes by default."""
    block = (
        f'tool = "Stable Diffusion"\ntool_version = "1.0"\nmodel = "sd-xl"\nprompt = "{prompt}"\ncreated = 2026-09-26\n'
        if generation is None
        else generation
    )
    return _GENERATED.format(path=path, generation=block)


def test_empty_ledger_without_assets_passes(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)

    code, output = run_check(root, capsys)

    assert code == 0
    assert output == ""


def test_asset_without_entry_fails(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")

    code, output = run_check(root, capsys)

    assert code == 1
    assert output.splitlines() == [f"PROVENANCE: {_SEAT}: no ledger entry"]


def test_entry_for_missing_file_fails(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_ledger(root, original(_SEAT))

    code, output = run_check(root, capsys)

    assert code == 1
    assert f"PROVENANCE: {_SEAT}: the file is not in the repo" in output.splitlines()


def test_duplicate_entry_fails(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_ledger(root, original(_SEAT) + original(_SEAT))

    code, output = run_check(root, capsys)

    assert code == 1
    assert f"PROVENANCE: {_SEAT}: 2 entries have this path" in output.splitlines()


def test_license_outside_allowlist_fails(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_ledger(root, original(_SEAT, license_id="GPL-3.0"))

    code, output = run_check(root, capsys)

    assert code == 1
    assert f"PROVENANCE: {_SEAT}: license 'GPL-3.0' is not in the allowlist" in output.splitlines()


def test_share_alike_license_requires_flag(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_ledger(root, original(_SEAT, license_id="CC-BY-SA-4.0"))

    code, output = run_check(root, capsys)

    assert code == 1
    assert f"PROVENANCE: {_SEAT}: license 'CC-BY-SA-4.0' requires share_alike = true" in output.splitlines()


def test_flag_without_share_alike_license_fails(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_ledger(root, original(_SEAT, extra="share_alike = true\n"))

    code, output = run_check(root, capsys)

    assert code == 1
    assert f"PROVENANCE: {_SEAT}: share_alike is only for CC-BY-SA licenses" in output.splitlines()


def test_generated_entry_requires_generation_fields(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_ledger(root, generated(_SEAT, generation=""))

    code, output = run_check(root, capsys)

    assert code == 1
    for field in ("tool", "tool_version", "model", "prompt", "created"):
        assert f"PROVENANCE: {_SEAT}: field '{field}' is required" in output.splitlines()


def test_generated_prompt_file_must_exist(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_ledger(root, generated(_SEAT, prompt="prompts/absent.md"))

    code, output = run_check(root, capsys)

    assert code == 1
    assert f"PROVENANCE: {_SEAT}: prompt file 'prompts/absent.md' is not in the repo" in output.splitlines()


def test_library_entry_requires_source_url_and_retrieved(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_file(root, _CHIME, b"ogg")
    write_ledger(root, library(_SEAT, source='source_url = "https://kenney.nl/pack"\n') + library(_CHIME, source=""))

    code, output = run_check(root, capsys)

    assert code == 1
    assert f"PROVENANCE: {_SEAT}: field 'retrieved' is required when source_url is set" in output.splitlines()
    assert f"PROVENANCE: {_CHIME}: field 'source_url' is required when origin is 'library'" in output.splitlines()


def test_unknown_key_fails(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_ledger(root, original(_SEAT, extra='weight = "12"\n'))

    code, output = run_check(root, capsys)

    assert code == 1
    assert f"PROVENANCE: {_SEAT}: unknown key 'weight'" in output.splitlines()


def test_ignored_asset_is_not_counted(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, ".gitignore", b"art/\n")
    write_file(root, "art/scratch.png", b"png")

    code, output = run_check(root, capsys)

    assert code == 0
    assert output == ""


def test_non_asset_extension_is_not_counted(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, "src/Sky.Scripting/Scripts/board.lua", b"return {}")
    write_file(root, "src/Sky.Content/Data/seats.json", b"{}")

    code, output = run_check(root, capsys)

    assert code == 0
    assert output == ""


def test_stale_credits_fails(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = make_repo(tmp_path)
    write_file(root, _SEAT, b"png")
    write_ledger(root, original(_SEAT))

    code, output = run_check(root, capsys)

    assert code == 1
    assert output.splitlines() == [
        "PROVENANCE: CREDITS.md: does not match the ledger; run `uv run --project tools/provenance python -m provenance credits`",
    ]


def test_credits_groups_by_license_and_sorts_by_path(tmp_path: Path) -> None:
    root = make_repo(tmp_path)
    for name in ("z.png", "a.png", "b.png", "c.png"):
        write_file(root, f"src/Art/{name}", b"png")
    write_ledger(
        root,
        original("src/Art/z.png")
        + library("src/Art/a.png")
        + original("src/Art/b.png", modifications='["cropped", "resized"]')
        + original("src/Art/c.png", license_id="CC-BY-SA-4.0", author="Someone", extra="share_alike = true\n"),
    )

    assert regenerate_credits(root) == 0

    expected_lines = [
        "Generated from `assets/PROVENANCE.toml` by `uv run --project tools/provenance python -m provenance credits`. Do not edit by hand.",
        "",
        "## CC-BY-SA-4.0",
        "",
        "- src/Art/c.png: Someone (share-alike)",
        "",
        "## CC0-1.0",
        "",
        "- src/Art/a.png: Kenney, https://kenney.nl/pack",
        "",
        "## MIT",
        "",
        "- src/Art/b.png: Leftos (modified: cropped; resized)",
        "- src/Art/z.png: Leftos",
    ]
    assert (root / "CREDITS.md").read_text(encoding="utf-8") == "\n".join(expected_lines) + "\n"
