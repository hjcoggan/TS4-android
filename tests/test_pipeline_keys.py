import struct
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from pipeline_keys import (KeyList, check_against_pack, format_list, main, merge,  # noqa: E402
                           parse_list, read_store, retag)

CONVERTER = 0x19BC9795A8B5E348
KEY_SIZE = 352  # Top Spin 4's sizeof(PipelineKey); the script takes it from the data
DEVICE_TAG = struct.pack("<III16s", 0x1002, 0x744C, 0x8000, bytes(range(16)))


def key(vertex_shader: int, pixel_shader: int, topology: int = 3, size: int = KEY_SIZE) -> bytes:
    body = struct.pack("<QQII", vertex_shader, pixel_shader, 0, topology)
    return body + bytes(size - len(body))


def store_v3(keys: list[bytes], converter: int = CONVERTER, magic: bytes = b"WWEP",
             size: int = KEY_SIZE) -> bytes:
    header = magic + struct.pack("<IIIQ", 3, size, len(keys), converter) + DEVICE_TAG
    return header + bytes(4) + b"".join(keys)


def store_v2(keys: list[bytes], converter: int = CONVERTER) -> bytes:
    return b"SVRP" + struct.pack("<IIIQ", 2, KEY_SIZE, len(keys), converter) + b"".join(keys)


def pack(hashes: list[int], converter: int = CONVERTER) -> bytes:
    out = bytearray(b"WWSP") + struct.pack("<IQIIII", 1, converter, len(hashes), 0, 0, 0)
    for shader_hash in sorted(hashes):
        out += struct.pack("<QIIII", shader_hash, 0, 0, 0, 0)
    return bytes(out)


def test_reads_the_keys_of_a_tagged_store_and_of_an_untagged_one():
    assert read_store(store_v3([key(1, 2)])).keys == [key(1, 2)]
    store = read_store(store_v2([key(1, 2), key(3, 0)]))
    assert (store.converter, store.key_size, store.keys) == (CONVERTER, KEY_SIZE, [key(1, 2), key(3, 0)])


def test_the_key_size_comes_from_the_store_header():
    store = read_store(store_v3([key(1, 2, size=344)], size=344))
    assert store.key_size == 344
    assert store.keys == [key(1, 2, size=344)]


def test_reads_a_store_written_before_the_magic_was_the_ports_own():
    assert read_store(store_v3([key(1, 2)], magic=b"SVRP")).keys == [key(1, 2)]
    with pytest.raises(ValueError, match="not a pipeline store"):
        read_store(store_v3([key(1, 2)], magic=b"XXXX"))


def test_refuses_a_store_of_another_format_or_cut_short():
    old = b"SVRP" + struct.pack("<IIIQ", 1, KEY_SIZE, 1, CONVERTER) + key(1, 2)
    with pytest.raises(ValueError, match="version 1"):
        read_store(old)
    with pytest.raises(ValueError, match="truncated"):
        read_store(store_v3([key(1, 2)])[:-1])


def test_merging_stores_into_the_list_keeps_each_key_once():
    text, report = merge(None, [store_v3([key(1, 2), key(3, 4)]), store_v2([key(3, 4)])], CONVERTER)
    assert parse_list(text) == KeyList(CONVERTER, KEY_SIZE, sorted([key(1, 2), key(3, 4)]))
    assert report.added == 2


def test_merging_the_same_stores_again_changes_nothing():
    stores = [store_v3([key(1, 2)]), store_v2([key(5, 6)])]
    first, _ = merge(None, stores, CONVERTER)
    second, report = merge(first, stores, CONVERTER)
    assert first == second
    assert report.added == 0


def test_a_store_of_another_converter_is_skipped_and_reported():
    text, report = merge(format_list(CONVERTER, KEY_SIZE, []),
                         [store_v3([key(1, 2)], converter=0x1234)], CONVERTER)
    assert parse_list(text) == KeyList(CONVERTER, KEY_SIZE, [])
    assert report.other_converter == 1


def test_a_list_of_another_converter_or_key_size_is_replaced_by_the_stores():
    stale = format_list(0x1234, KEY_SIZE, [key(7, 8)])
    text, report = merge(stale, [store_v3([key(1, 2)])], CONVERTER)
    assert parse_list(text).keys == [key(1, 2)]
    assert report.dropped == 1
    old_layout = format_list(CONVERTER, 344, [key(7, 8, size=344)])
    text, report = merge(old_layout, [store_v3([key(1, 2)])], CONVERTER)
    assert parse_list(text) == KeyList(CONVERTER, KEY_SIZE, [key(1, 2)])
    assert report.dropped == 1


def test_stores_of_this_converter_must_agree_on_the_key_size():
    with pytest.raises(ValueError, match="disagree"):
        merge(None, [store_v3([key(1, 2)]), store_v3([key(1, 2, size=344)], size=344)], CONVERTER)


def test_the_list_holds_only_fixed_size_hex_keys():
    text = format_list(CONVERTER, KEY_SIZE, [key(1, 2), key(3, 0)])
    lines = [line for line in text.splitlines() if line and not line.startswith("#")]
    assert lines[:3] == ["wwe-pipeline-keys 1", f"key-size {KEY_SIZE}", f"converter {CONVERTER:016x}"]
    for line in lines[3:]:
        assert len(line) == 2 * KEY_SIZE
        int(line, 16)


def test_a_list_line_that_is_not_a_key_is_refused():
    with pytest.raises(ValueError):
        parse_list(format_list(CONVERTER, KEY_SIZE, [key(1, 2)]) + "03021e0f\n")


def test_reports_keys_whose_shaders_left_the_pack():
    text = format_list(CONVERTER, KEY_SIZE, [key(1, 2), key(1, 0), key(1, 9), key(8, 2)])
    assert sorted(check_against_pack(text, pack([1, 2]))) == sorted([key(1, 9), key(8, 2)])


def test_shaders_in_top_up_packs_count_like_the_game_merges_them():
    # The game merges <cache_root>/shader-topup/*.shaderpack into the pack it loads.
    text = format_list(CONVERTER, KEY_SIZE, [key(1, 2), key(1, 9), key(8, 2)])
    assert check_against_pack(text, pack([1, 2]), [pack([9])]) == [key(8, 2)]


def test_check_names_a_list_the_pack_converter_voids(tmp_path, capsys):
    (tmp_path / "keys.txt").write_text(format_list(0x1234, KEY_SIZE, [key(1, 2)]), encoding="utf-8")
    (tmp_path / "pack").write_bytes(pack([1, 2]))
    assert main(["check", str(tmp_path / "keys.txt"), str(tmp_path / "pack")]) == 0
    assert "ignores the list" in capsys.readouterr().out


def test_retag_carries_the_list_to_a_converter_whose_keys_mean_the_same():
    text = format_list(CONVERTER, KEY_SIZE, [key(1, 2), key(3, 0)])
    new = 0x0123456789ABCDEF
    assert parse_list(retag(text, CONVERTER, new)) == KeyList(new, KEY_SIZE, [key(1, 2), key(3, 0)])


def test_retag_refuses_a_list_of_another_converter():
    with pytest.raises(ValueError, match="converter 0000000000001111"):
        retag(format_list(0x1111, KEY_SIZE, [key(1, 2)]), CONVERTER, 0x2222)


def test_merge_writes_the_list_from_store_files(tmp_path):
    (tmp_path / "keys.bin").write_bytes(store_v3([key(1, 2)]))
    listed = tmp_path / "data" / "pipeline-keys.txt"
    assert main(["merge", str(listed), f"{CONVERTER:016x}", str(tmp_path / "keys.bin")]) == 0
    assert parse_list(listed.read_text(encoding="utf-8")).keys == [key(1, 2)]
