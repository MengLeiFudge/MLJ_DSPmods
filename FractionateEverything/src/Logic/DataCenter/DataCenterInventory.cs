using System;
using System.Collections.Generic;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.Progression;
using FE.UI.MainPanel.Setting;
using NebulaAPI;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter;

/// <summary>
/// 分馏数据中心库存、手动上传/提取统计，以及对应存档。
/// </summary>
public static class DataCenterInventory {
    public static readonly long[] centerItemCount = new long[12000];
    public static readonly long[] centerItemInc = new long[12000];
    public static int leftInc = 0;
    public static long ManualExtractCount;
    public static long ManualUploadCount;

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("CenterItems", bw => {
                List<int> activeIds = [];
                for (int i = 0; i < centerItemCount.Length; i++) {
                    if (centerItemCount[i] > 0) activeIds.Add(i);
                }
                bw.Write(activeIds.Count);
                foreach (int itemId in activeIds) {
                    bw.Write(itemId);
                    bw.Write(centerItemCount[itemId]);
                    bw.Write(centerItemInc[itemId]);
                }
            }),
            ("LeftInc", bw => bw.Write(leftInc)),
            ("ManualInteractionStats", bw => {
                bw.Write(ManualExtractCount);
                bw.Write(ManualUploadCount);
            })
        );
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("CenterItems", br => {
                int size = br.ReadInt32();
                for (int i = 0; i < size; i++) {
                    int itemId = br.ReadInt32();
                    long count = br.ReadInt64();
                    long inc = br.ReadInt64();
                    if (itemId >= 0 && itemId < centerItemCount.Length) {
                        centerItemCount[itemId] = Math.Max(0, count);
                        centerItemInc[itemId] = Math.Max(0, Math.Min(inc, centerItemCount[itemId] * 10));
                    }
                }
            }),
            ("LeftInc", br => leftInc = br.ReadInt32()),
            ("ManualInteractionStats", br => {
                ManualExtractCount = Math.Max(0L, br.ReadInt64());
                ManualUploadCount = Math.Max(0L, br.ReadInt64());
            })
        );
    }

    public static void IntoOtherSave() {
        Array.Clear(centerItemCount, 0, centerItemCount.Length);
        Array.Clear(centerItemInc, 0, centerItemInc.Length);
        leftInc = 0;
        ManualExtractCount = 0;
        ManualUploadCount = 0;
    }

    /// <summary>
    /// 从数据中心取出指定物品，再将其放入玩家背包/物流背包/手上。
    /// </summary>
    public static void ClickToMoveModDataItem(int itemId, bool leftClick) {
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            return;
        }
        int count = leftClick
            ? item.StackSize * Miscellaneous.LeftClickTakeCount
            : item.StackSize * Miscellaneous.RightClickTakeCount;
        int inc;
        lock (centerItemCount) {
            count = TakeItemFromModData(itemId, count, out inc, true);
        }
        if (count > 0) {
            ManualExtractCount++;
        }
        if (itemId == I沙土) {
            if (GameMain.mainPlayer.inhandItemId != I沙土) {
                GameMain.mainPlayer.ThrowHandItems();
            }
            GameMain.mainPlayer.inhandItemId = I沙土;
            GameMain.mainPlayer.inhandItemCount += count;
        } else {
            if (Miscellaneous.ExtractToHand) {
                if (GameMain.mainPlayer.inhandItemId == 0 || GameMain.mainPlayer.inhandItemCount == 0) {
                    GameMain.mainPlayer.inhandItemId = itemId;
                    GameMain.mainPlayer.inhandItemCount = count;
                    GameMain.mainPlayer.inhandItemInc = inc;
                } else if (GameMain.mainPlayer.inhandItemId == itemId) {
                    GameMain.mainPlayer.inhandItemCount += count;
                    GameMain.mainPlayer.inhandItemInc += inc;
                } else {
                    GameMain.mainPlayer.ThrowHandItems();
                    GameMain.mainPlayer.inhandItemId = itemId;
                    GameMain.mainPlayer.inhandItemCount = count;
                    GameMain.mainPlayer.inhandItemInc = inc;
                }
            } else {
                PlayerInventoryAccess.AddItemToPackage(itemId, count, inc, true);
            }
        }
    }

    public static int GetFragmentMinCount() {
        long count = centerItemCount[IFE残片];
        return (int)Math.Min(int.MaxValue, count);
    }

    public static bool TakeFragmentsFromModData(int n, int[] consumeRegister) {
        if (centerItemCount[IFE残片] < n) {
            return false;
        }
        lock (centerItemCount) {
            TakeItemFromModData(IFE残片, n, out _);
        }
        lock (consumeRegister) {
            consumeRegister[IFE残片] += n;
        }
        return true;
    }

    public static int GetEssenceMinCount() => GetFragmentMinCount();

    public static bool TakeEssenceFromModData(int n, int[] consumeRegister) =>
        TakeFragmentsFromModData(n, consumeRegister);

    public static void AddItemToModData(int itemId, int count, int inc = 0, bool manual = false) {
        if (itemId == I沙土) {
            GameMain.mainPlayer.sandCount += count;
            return;
        }
        if (itemId <= 0 || itemId >= 12000) {
            return;
        }
        lock (centerItemCount) {
            centerItemCount[itemId] += count;
            centerItemInc[itemId] += inc;
            if (itemId >= IFE交互塔 && itemId <= IFE精馏塔) {
                TechManager.CheckTechUnlockCondition(itemId);
            }
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new CenterItemChangePacket(itemId, count, inc));
        }
    }

    public static long GetModDataItemCount(int itemId) {
        return GetModDataItemCount(itemId, out _);
    }

    public static long GetModDataItemCount(int itemId, out long inc) {
        inc = 0;
        if (itemId == I沙土) {
            if (GameMain.data.history.HasFeatureKey(1100001) && GameMain.sandboxToolsEnabled) {
                return long.MaxValue;
            }
            return GameMain.mainPlayer.sandCount;
        }
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        inc = centerItemInc[itemId];
        return centerItemCount[itemId];
    }

    public static int TakeItemFromModData(int itemId, int count, out int inc, bool manual = false) {
        if (itemId == I沙土) {
            inc = 0;
            if (GameMain.mainPlayer.sandCount >= count) {
                GameMain.mainPlayer.sandCount -= count;
                return count;
            } else {
                count = (int)GameMain.mainPlayer.sandCount;
                GameMain.mainPlayer.sandCount = 0;
                return count;
            }
        }
        if (itemId <= 0 || itemId >= 12000) {
            inc = 0;
            return 0;
        }
        lock (centerItemCount) {
            count = (int)Math.Min(count, centerItemCount[itemId]);
            count = Math.Min(100000, count);
            if (count <= 0) {
                inc = 0;
                return 0;
            }
            if (centerItemInc[itemId] / centerItemCount[itemId] >= 4) {
                inc = (int)PlayerInventoryAccess.split_inc(ref centerItemCount[itemId], ref centerItemInc[itemId],
                    count);
            } else {
                if (centerItemInc[itemId] >= count * 4) {
                    centerItemCount[itemId] -= count;
                    inc = count * 4;
                    centerItemInc[itemId] -= inc;
                } else {
                    centerItemCount[itemId] -= count;
                    inc = (int)centerItemInc[itemId];
                    centerItemInc[itemId] = 0;
                }
            }
            if (NebulaModAPI.IsMultiplayerActive && manual) {
                NebulaModAPI.MultiplayerSession.Network.SendPacket(new CenterItemChangePacket(itemId, -count, -inc));
            }
            return count;
        }
    }

    public static int Take10PercentTower(int itemId) {
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        return centerItemCount[itemId] < 1000
            ? 0
            : TakeItemFromModData(itemId, (int)Math.Min(int.MaxValue, centerItemCount[itemId] / 10), out _);
    }
}
