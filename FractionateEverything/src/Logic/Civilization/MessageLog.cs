using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FE.Logic.Civilization;

/// <summary>本次游戏的提示环形日志；不持久化，不随联机快照重置。</summary>
public static class MessageLog {
    /// <summary>不可变的提示快照；合并重复时保留序号，替换快照而不改变UI已读取的内容。</summary>
    /// <param name="id">当前游戏内递增身份。</param>
    /// <param name="text">本端提示原文。</param>
    /// <param name="repeatCount">连续出现的次数。</param>
    /// <param name="lastGameTick">最后一次出现的游戏刻数。</param>
    public sealed class Entry(long id, string text, long repeatCount, long lastGameTick) {
        /// <summary>当前游戏内递增身份。</summary>
        public long Id { get; } = id;
        /// <summary>提示出现时的本端文字。</summary>
        public string Text { get; } = text;
        /// <summary>同一文字连续出现的次数，至少为1。</summary>
        public long RepeatCount { get; } = repeatCount;
        /// <summary>最后一次出现时的游戏刻数，60刻为一秒。</summary>
        public long LastGameTick { get; } = lastGameTick;
    }

    private static readonly object Sync = new();
    private static readonly Entry[] Entries = new Entry[50];
    private static int next;
    private static int count;
    private static long sequence;

    /// <summary>连续相同文字合并次数；新条目满50条时覆盖最旧项。</summary>
    /// <param name="text">已经本地化的提示。</param>
    public static void Add(string text) {
        if (!GameMain.isRunning || string.IsNullOrWhiteSpace(text)) { return; }
        lock (Sync) {
            int previous = (next - 1 + Entries.Length) % Entries.Length;
            Entry last = count > 0 ? Entries[previous] : null;
            if (last?.Text == text) {
                Entries[previous] = new Entry(last.Id, text, last.RepeatCount + 1, GameMain.gameTick);
                return;
            }
            Entries[next] = new Entry(++sequence, text, 1, GameMain.gameTick);
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
