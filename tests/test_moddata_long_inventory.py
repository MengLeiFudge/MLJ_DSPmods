import unittest
from pathlib import Path


DATA_CENTER_INVENTORY = Path("FractionateEverything/src/Logic/DataCenter/DataCenterInventory.cs")
NEBULA_API = Path("FractionateEverything/src/Compatibility/Nebula/NebulaMultiplayerModAPI.cs")
PLAYER_INVENTORY_ACCESS = Path("FractionateEverything/src/Logic/DataCenter/PlayerInventoryAccess.cs")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def source_between(source: str, start: str, end: str) -> str:
    start_index = source.find(start)
    if start_index < 0:
        return ""
    end_index = source.find(end, start_index)
    if end_index < 0:
        return source[start_index:]
    return source[start_index:end_index]


class ModDataLongInventoryTests(unittest.TestCase):
    def test_mod_data_take_has_int_and_long_inc_bounds(self):
        source = read_text(DATA_CENTER_INVENTORY)
        self.assertIn("private const int MaxIntTakeCountByInc = int.MaxValue / 10;", source)
        self.assertIn("private const long MaxLongTakeCountByInc = long.MaxValue / 10L;", source)
        self.assertNotIn("Math.Min(100000, count)", source)

        int_take = source_between(
            source,
            "public static int TakeItemFromModData",
            "public static long TakeItemFromModData",
        )
        self.assertIn("Math.Min(count, MaxIntTakeCountByInc)", int_take)
        self.assertIn("CenterItemChangePacket", int_take)

        long_take = source_between(
            source,
            "public static long TakeItemFromModData",
            "private static long TakeItemFromModDataInternal",
        )
        self.assertIn("Math.Min(count, MaxLongTakeCountByInc)", long_take)
        self.assertIn("CenterItemChangeLongPacket", long_take)

    def test_mod_data_take_uses_long_inc_math_in_core(self):
        source = read_text(DATA_CENTER_INVENTORY)
        core = source_between(
            source,
            "private static long TakeItemFromModDataInternal",
            "public static int Take10PercentTower",
        )
        self.assertIn("long expectedInc = count * 4;", core)
        self.assertIn("inc = split_inc(ref centerItemCount[itemId], ref centerItemInc[itemId], count);", core)
        self.assertNotIn("inc = count * 4;", core)

    def test_nebula_has_long_center_item_change_packet(self):
        source = read_text(NEBULA_API)
        self.assertIn("public class CenterItemChangeLongPacket", source)
        self.assertIn("public CenterItemChangeLongPacket(int itemId, long count, long inc = 0)", source)
        self.assertIn("public class CenterItemChangeLongPacketProcessor", source)
        self.assertIn("long count = r.ReadInt64();", source)
        self.assertIn("long inc = r.ReadInt64();", source)
        self.assertIn("AddItemToModData(itemId, count, inc);", source)

    def test_player_inventory_access_keeps_single_int_take_contract(self):
        source = read_text(PLAYER_INVENTORY_ACCESS)
        self.assertIn("TakeItemFromModData(itemIdTmp, realCountTemp, out incTemp, true)", source)
        self.assertNotIn("TakeItemFromModDataFully", source)


if __name__ == "__main__":
    unittest.main()
