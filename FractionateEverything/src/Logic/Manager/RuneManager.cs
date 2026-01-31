using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.Utils.Utils;
using Random = UnityEngine.Random;

namespace FE.Logic.Manager;

public enum ERuneStatType {
    Speed = 0,
    Productivity = 1,
    EnergySaving = 2,
    Proliferator = 3,
}

public class Rune {
    public long id;
    public int level;// 0-Max
    public ERuneStatType mainStat;
    public int star;// 1-5
    public List<float> subStatRolls = [];
    public List<ERuneStatType> subStats = [];
    public int MaxLevel => RuneManager.StarSettings[star].MaxLevel;

    public int GetEssenceId() {
        return mainStat switch {
            ERuneStatType.Speed => IFE速度精华,
            ERuneStatType.Productivity => IFE产能精华,
            ERuneStatType.EnergySaving => IFE节能精华,
            ERuneStatType.Proliferator => IFE增产精华,
            _ => 0
        };
    }

    public void GetStats(out float speed, out float power, out float productivity, out float proliferator) {
        speed = 0;
        power = 0;
        productivity = 0;
        proliferator = 0;
        AddStat(mainStat, level, ref speed, ref power, ref productivity, ref proliferator);
        for (int i = 0; i < subStats.Count; i++) {
            AddStat(subStats[i], subStatRolls[i], ref speed, ref power, ref productivity, ref proliferator);
        }
    }

    private static void AddStat(ERuneStatType type, float value,
        ref float speed, ref float power, ref float productivity, ref float proliferator) {
        switch (type) {
            case ERuneStatType.Speed:
                speed += value * 0.5f;
                power += value * 0.7f;
                break;
            case ERuneStatType.Productivity:
                productivity += value * 0.1f;
                power += value * 0.8f;
                speed -= value * 0.15f;
                break;
            case ERuneStatType.EnergySaving:
                power -= value * 0.5f;
                break;
            case ERuneStatType.Proliferator:
                proliferator += value * 0.05f;
                break;
        }
    }
}

public static class RuneManager {
    public static readonly RuneSettings[] StarSettings = [
        null,// Star 0
        new() {
            Star = 1, MaxLevel = 4, InitSubCountMin = 0, InitSubCountMax = 0, RollCount = 1,
            Steps = [0.35f, 0.40f, 0.45f, 0.50f], MainStatMax = 4,
        },
        new() {
            Star = 2, MaxLevel = 8, InitSubCountMin = 1, InitSubCountMax = 1, RollCount = 2,
            Steps = [0.70f, 0.80f, 0.90f, 1.00f], MainStatMax = 8,
        },
        new() {
            Star = 3, MaxLevel = 12, InitSubCountMin = 1, InitSubCountMax = 2, RollCount = 3,
            Steps = [1.05f, 1.20f, 1.35f, 1.50f], MainStatMax = 12,
        },
        new() {
            Star = 4, MaxLevel = 16, InitSubCountMin = 2, InitSubCountMax = 2, RollCount = 4,
            Steps = [1.40f, 1.60f, 1.80f, 2.00f], MainStatMax = 16,
        },
        new() {
            Star = 5, MaxLevel = 20, InitSubCountMin = 2, InitSubCountMax = 3, RollCount = 5,
            Steps = [1.75f, 2.00f, 2.25f, 2.50f], MainStatMax = 20,
        },
    ];

    public static List<Rune> allRunes = [];
    public static long[] equippedRuneIds = new long[5];
    public static int slotCount = 0;

    public static Rune GenerateRune(int star, int? subCountOverride = null) {
        Rune rune = CreateRuneData(star, subCountOverride);
        allRunes.Add(rune);
        return rune;
    }

    public static Rune CreateRuneData(int star, int? subCountOverride = null) {
        Rune rune = new Rune {
            id = DateTime.Now.Ticks + Random.Range(0, 1000),
            star = star,
            level = 0,
            mainStat = (ERuneStatType)Random.Range(0, 4),
        };
        int subCount = subCountOverride
                       ?? Random.Range(StarSettings[star].InitSubCountMin,
                           StarSettings[star].InitSubCountMax + 1);
        for (int i = 0; i < subCount; i++) {
            var subType = (ERuneStatType)Random.Range(0, 4);
            if (rune.subStats.Contains(subType)) {
                int index = rune.subStats.IndexOf(subType);
                rune.subStatRolls[index] += 1.0f;
            } else {
                rune.subStats.Add(subType);
                rune.subStatRolls.Add(1.0f);
            }
        }
        return rune;
    }

    public static bool UpgradeRune(Rune rune) {
        if (rune.level >= rune.MaxLevel) {
            return false;
        }
        int essenceId = rune.GetEssenceId();
        int cost = GetUpgradeCost(rune.level, rune.star);
        if (!TakeItemWithTip(essenceId, cost, out _, false)) {
            return false;
        }
        rune.level++;
        if (rune.level % 4 == 0) {
            RollSubStat(rune);
        }
        return true;
    }

    private static void RollSubStat(Rune rune) {
        var subType = (ERuneStatType)Random.Range(0, 4);
        float step = StarSettings[rune.star].Steps[Random.Range(0, 4)];
        if (rune.subStats.Contains(subType)) {
            int index = rune.subStats.IndexOf(subType);
            rune.subStatRolls[index] += step;
        } else {
            rune.subStats.Add(subType);
            rune.subStatRolls.Add(step);
        }
    }

    public static int GetUpgradeCost(int level, int star) =>
        // 0-11级二次函数，12-19级指数增长
        level < 12
            ? (int)(600 + 145 * level + 5 * level * level) * star
            : (int)(2600 * Math.Pow(1.45 + 0.05 * (level - 12), level - 12)) * star;

    public static void DeconstructRune(Rune rune) {
        int essenceId = rune.GetEssenceId();
        long refund = 0;
        for (int i = 0; i < rune.level; i++) {
            refund += GetUpgradeCost(i, rune.star);
        }
        refund = (long)(refund * 0.8f) + 10 * rune.star;
        ItemManager.centerItemCount[essenceId] += refund;
        allRunes.Remove(rune);
        for (int i = 0; i < equippedRuneIds.Length; i++) {
            if (equippedRuneIds[i] == rune.id) {
                equippedRuneIds[i] = 0;
            }
        }
    }

    public static int RollRuneStar() {
        float rand = Random.value;
        return rand switch {
            < 0.01f => 5,
            < 0.05f => 4,
            < 0.20f => 3,
            < 0.50f => 2,
            _ => 1,
        };
    }

    public static void UpdateSlotCount() {
        if (GameMain.history == null) {
            return;
        }
        int[] techIds = [T电磁矩阵, T能量矩阵, T结构矩阵, T信息矩阵, T引力矩阵];
        int count = techIds.Count(id => GameMain.history.TechUnlocked(id));
        slotCount = count;
    }

    public static void GetTotalStats(out float speed, out float power, out float productivity, out float yield) {
        speed = 0;
        power = 0;
        productivity = 0;
        yield = 0;
        foreach (long id in equippedRuneIds) {
            if (id == 0) {
                continue;
            }
            Rune rune = allRunes.FirstOrDefault(r => r.id == id);
            if (rune == null) {
                continue;
            }
            rune.GetStats(out float s, out float p, out float pr, out float y);
            speed += s;
            power += p;
            productivity += pr;
            yield += y;
        }
        // if (power < -0.8f) power = -0.8f;
    }

    public class RuneSettings {
        public int InitSubCountMax;
        public int InitSubCountMin;
        public int MainStatMax;
        public int MaxLevel;
        public int RollCount;
        public int Star;
        public float[] Steps;
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        IntoOtherSave();
        int version = r.ReadInt32();
        int runeCount = r.ReadInt32();
        for (int i = 0; i < runeCount; i++) {
            var rune = new Rune {
                id = r.ReadInt64(),
                star = r.ReadInt32(),
                level = r.ReadInt32(),
                mainStat = (ERuneStatType)r.ReadInt32(),
            };
            rune.star = Math.Max(0, Math.Min(5, rune.star));
            rune.level = Math.Max(0, Math.Min(rune.MaxLevel, rune.level));
            int subCount = r.ReadInt32();
            for (int j = 0; j < subCount; j++) {
                rune.subStats.Add((ERuneStatType)r.ReadInt32());
                rune.subStatRolls.Add(r.ReadSingle());
            }
            allRunes.Add(rune);
        }
        for (int i = 0; i < 5; i++) {
            equippedRuneIds[i] = r.ReadInt64();
        }
        slotCount = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);// version
        w.Write(allRunes.Count);
        foreach (Rune rune in allRunes) {
            w.Write(rune.id);
            w.Write(rune.star);
            w.Write(rune.level);
            w.Write((int)rune.mainStat);
            w.Write(rune.subStats.Count);
            for (int i = 0; i < rune.subStats.Count; i++) {
                w.Write((int)rune.subStats[i]);
                w.Write(rune.subStatRolls[i]);
            }
        }
        for (int i = 0; i < 5; i++) {
            w.Write(equippedRuneIds[i]);
        }
        w.Write(slotCount);
    }

    public static void IntoOtherSave() {
        allRunes.Clear();
        for (int i = 0; i < equippedRuneIds.Length; i++) {
            equippedRuneIds[i] = 0;
        }
        slotCount = 0;
    }

    #endregion
}
