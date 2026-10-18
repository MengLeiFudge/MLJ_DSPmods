using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;

namespace FE.Logic.Civilization;

/// <summary>
/// 保存本地完成过丰碑终局的世界身份，重复读档不会重复增加通关次数。
/// </summary>
public static class PermanentProfileStore {
    private static ConfigEntry<string> completedWorlds;
    private static readonly HashSet<string> CompletedWorldIds = new(StringComparer.Ordinal);

    /// <summary>已记录的独立世界数量，旧版是否玩过标记不参与计算。</summary>
    public static int CompletionCount => CompletedWorldIds.Count;

    /// <summary>绑定完成世界账本；账本本身是通关次数的唯一真源。</summary>
    public static void Bind(ConfigFile config) {
        completedWorlds = config.Bind("Progression", "CompletedWorlds", "",
            "完成丰碑终局的世界身份；每个世界只计一次，本次更新前的游玩标记不计入通关次数。");
        CompletedWorldIds.Clear();
        foreach (string id in completedWorlds.Value.Split([';'], StringSplitOptions.RemoveEmptyEntries)) {
            CompletedWorldIds.Add(id);
        }
    }

    /// <summary>由已完成终局判定的主机调用；使用星图与创建时间区分独立世界。</summary>
    /// <returns>首次记录合格世界时为true，重复或不合格时为false。</returns>
    public static bool RecordCompletion() {
        GameDesc game = GameMain.data?.gameDesc;
        if (GameMain.sandboxToolsEnabled || game?.isSandboxMode == true
            || GameMain.history?.createWithSandboxMode == true) {
            ResearchManager.DisqualifyCompletion();
        }
        if (completedWorlds == null || game == null || !ResearchManager.CanRecordCompletion) { return false; }
        string worldId = game.seedKey64.ToString(CultureInfo.InvariantCulture) + ":"
            + game.creationTime.Ticks.ToString(CultureInfo.InvariantCulture);
        if (CompletedWorldIds.Contains(worldId)) { return false; }
        string value = string.Join(";", CompletedWorldIds.Append(worldId).OrderBy(id => id, StringComparer.Ordinal));
        completedWorlds.Value = value;
        completedWorlds.ConfigFile.Save();
        CompletedWorldIds.Add(worldId);
        CivilizationModule.QueueNotification("生命工业传承已记录".Translate());
        return true;
    }
}
