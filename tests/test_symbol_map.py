"""symbols/tu1-symbols.csv as tools/ghidra/ApplySymbolMap.java reads it: the exact header, then
rows split on the first four commas (the last column, evidence, may hold more)."""

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HEADER = "module,address,name,confidence,evidence"
SWING = (0x88000000, 0x88DD0000)


def rows():
    lines = (ROOT / "symbols/tu1-symbols.csv").read_text(encoding="utf-8").splitlines()
    assert lines[0] == HEADER
    return [line.split(",", 4) for line in lines[1:] if line.strip()]


def test_every_row_has_a_swing_address_and_a_name():
    for module, address, name, confidence, _ in rows():
        assert module == "Swing_DLL.xex"
        assert len(address) == 8 and SWING[0] <= int(address, 16) < SWING[1], address
        assert name and "," not in name and '"' not in name, name
        assert confidence in ("high", "medium"), confidence


def test_names_and_addresses_are_unique():
    table = rows()
    assert len({row[2] for row in table}) == len(table)
    assert len({row[1] for row in table}) == len(table)
