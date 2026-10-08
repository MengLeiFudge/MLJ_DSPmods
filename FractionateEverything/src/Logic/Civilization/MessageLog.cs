using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FE.Logic.Civilization;

/// <summary>本次游戏的提示环形日志；不持久化，不随联机快照重置。</summary>
public static class MessageLog {
    /// <summary>本端已经收到的一条提示，序号仅用于界面稳定选择。</summary>
    public sealed class Entry(long id, string text) {
        /// <summary>当前游戏内递增身份。</summary>
        public long Id { get; } = id;
        /// <summary>提示出现时的本端文字。</summary>
        public string Text { get; } = text;
    }

    private static readonly object Sync = new();
    private static readonly Entry[] Entries = new Entry[50];
    private static int next;
    private static int count;
    private static long sequence;

    /// <summary>保存一条提示，满50条时覆盖最旧项。</summary>
    /// <param name="text">已经本地化的提示。</param>
    public static void Add(string text) {
        if (!GameMain.isRunning || string.IsNullOrWhiteSpace(text)) { return; }
        lock (Sync) {
            Entries[next] = new Entry(++sequence, text);
            next = (next + 1) % Entries.Length;
            count = System.Math.Min(count + 1, Entries.Length);
        }
    }

    /// <summary>读取最新在前的只读副本，不将环形数组交给UI修改。</summary>
    /// <returns>最近最多50条提示。</returns>
    public static IReadOnlyList<Entry> GetEntries() {
        lock (Sync) {
            var entries = new Entry[count];
            for (int i = 0; i < count; i++) {
                entries[i] = Entries[(next - i - 1 + Entries.Length) % Entries.Length];
            }
            return entries;
        }
    }

    /// <summary>仅在切档生命周期清空，不因文明快照更新而丢掉本地消息。</summary>
    public static void Clear() {
        lock (Sync) {
            System.Array.Clear(Entries, 0, Entries.Length);
            next = count = 0;
            sequence = 0;
        }
    }

    /// <summary>保留原版鼠标提示行为，同时记录FE与原版共用入口收到的文字。</summary>
    /// <param name="text">原始提示文字。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIRealtimeTip), nameof(UIRealtimeTip.SetText), [typeof(string), typeof(Vector2)])]
    private static void UIRealtimeTip_SetText_Postfix(string text) => Add(text);
}
