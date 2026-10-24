using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FE.Utils;

/// <summary>
/// 标签化存档块读写辅助方法。
/// </summary>
/// <remarks>
/// 每个独立保存的变量、数组、集合或字典使用具名块；标签是持久化身份，不必随代码变量改名。
/// 本工具只负责名称匹配、别名和字节边界，不自动识别载荷类型或推断转换。
/// Import/Export 显式读写实际类型与布局；所属业务负责默认值、校验、迁移及恢复后的状态一致性。
/// </remarks>
public static partial class Utils {
    private const int MaximumSaveBlockCount = 4096;

    /// <summary>一个读取字段及其历史名称；别名只表示同一载荷的改名。</summary>
    public readonly struct SaveField {
        internal readonly string Tag;
        internal readonly Action<BinaryReader> Action;
        internal readonly string[] Aliases;

        internal SaveField(string tag, Action<BinaryReader> action, string[] aliases) {
            Tag = tag;
            Action = action;
            Aliases = aliases;
        }
    }

    /// <summary>声明读取字段；当前名称优先，别名按声明顺序优先，每个字段只执行一次。</summary>
    /// <param name="tag">当前稳定的持久化名称，不要求与代码变量名同步变化。</param>
    /// <param name="action">读取该字段的处理器。</param>
    /// <param name="aliases">含义与载荷相同的历史名称；类型或布局改变时使用新标签，由业务决定转换或跳过旧数据。</param>
    /// <returns>供 ReadBlocks 按名称选择读取器的字段声明。</returns>
    public static SaveField Field(string tag, Action<BinaryReader> action, params string[] aliases) => new(tag, action, aliases);

    /// <summary>
    /// 将一个数据块包装成带 Tag 和长度的格式写入。
    /// </summary>
    /// <param name="w">目标 BinaryWriter</param>
    /// <param name="tag">块标识符</param>
    /// <param name="writeAction">实际写入逻辑</param>
    private static void WriteBlock(this BinaryWriter w, string tag, Action<BinaryWriter> writeAction) {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, true)) {
            writeAction(bw);
        }
        w.Write(tag);
        w.Write((int)ms.Length);
        w.Write(ms.GetBuffer(), 0, (int)ms.Length);
    }

    /// <summary>
    /// 将多个数据块包装成带 Tag 和长度的格式并依次写入。
    /// </summary>
    /// <remarks>
    /// 每个独立状态各用一个稳定标签；数组或集合整体也需具名，再在其回调内调用 WriteList。
    /// 同级具名块可换序；回调内的原始 Write 序列仍有顺序，可独立增删的字段须继续拆成子块。
    /// </remarks>
    /// <param name="w">目标 BinaryWriter</param>
    /// <param name="writeActions">包含多个块的数组，每个块由块标识符和写入逻辑组成</param>
    public static void WriteBlocks(this BinaryWriter w,
        params (string tag, Action<BinaryWriter> action)[] writeActions) {
        w.Write(writeActions.Length);// 自动写入块的数量
        foreach (var (tag, action) in writeActions) {
            w.WriteBlock(tag, action);
        }
    }

    /// <summary>先消费完整块边界；内容处理失败不影响外层流位置。</summary>
    private static (string Tag, byte[] Data) ReadBlock(BinaryReader r) {
        string tag = r.ReadString();
        int length = r.ReadInt32();
        if (length < 0) {
            throw new InvalidDataException($"存档块 [{tag}] 的长度不能为负数：{length}。");
        }
        if (r.BaseStream.CanSeek && length > r.BaseStream.Length - r.BaseStream.Position) {
            throw new InvalidDataException(
                $"存档块 [{tag}] 声明长度 {length} 超过剩余数据 {r.BaseStream.Length - r.BaseStream.Position}。");
        }
        byte[] data = r.ReadBytes(length);
        if (data.Length != length) {
            throw new EndOfStreamException(
                $"存档块 [{tag}] 需要 {length} 字节，实际只读取到 {data.Length} 字节。");
        }
        return (tag, data);
    }

    /// <summary>在独立流内处理一个已选中的块，保留可恢复错误与代码错误的日志区别。</summary>
    private static void ReadBlockContent(string tag, byte[] data, Action<BinaryReader> action) {
        try {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            action(br);
        } catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException
            or DecoderFallbackException or NotSupportedException) {
            LogWarning($"[Save] 存档块 [{tag}] 未能完整恢复，已跳过剩余内容并继续读取其他块：{ex.Message}");
        } catch (Exception ex) {
            LogError($"Failed to handle block [{tag}]: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>读取无别名的字段声明；未知块跳过，处理顺序由当前声明确定。</summary>
    public static void ReadBlocks(this BinaryReader r, params (string tag, Action<BinaryReader> action)[] readActions) =>
        ReadFields(r, readActions.Select(h => Field(h.tag, h.action)).ToArray());

    /// <summary>读取带历史别名的字段声明；与已有二元组入口共用同一选择规则。</summary>
    public static void ReadBlocks(this BinaryReader r, SaveField first, params SaveField[] remaining) =>
        ReadFields(r, new[] { first }.Concat(remaining).ToArray());

    /// <summary>收集同名字段的最佳匹配后再处理，确保文件顺序不会改变新旧名称的优先级。</summary>
    private static void ReadFields(BinaryReader r, SaveField[] fields) {
        var names = new Dictionary<string, (int Index, int Rank)>(StringComparer.Ordinal);
        for (int i = 0; i < fields.Length; i++) {
            if (fields[i].Action == null) { throw new ArgumentException("存档字段读取器不能为空。"); }
            string[] tags = new[] { fields[i].Tag }.Concat(fields[i].Aliases ?? []).ToArray();
            for (int rank = 0; rank < tags.Length; rank++) {
                string tag = tags[rank];
                if (string.IsNullOrEmpty(tag)) { throw new ArgumentException("存档字段名称不能为空。"); }
                if (names.TryGetValue(tag, out var previous)) {
                    if (previous.Index != i) { throw new ArgumentException($"存档字段名称或别名重复：{tag}。"); }
                    continue;
                }
                names.Add(tag, (i, rank));
            }
        }
        byte[][] payloads = new byte[fields.Length][];
        int[] ranks = Enumerable.Repeat(int.MaxValue, fields.Length).ToArray();
        try {
            int count = r.ReadInt32();
            if (count < 0 || count > MaximumSaveBlockCount) {
                throw new InvalidDataException($"存档块数量无效：{count}。");
            }
            for (int i = 0; i < count; i++) {
                var block = ReadBlock(r);
                if (!names.TryGetValue(block.Tag, out var field)) {
                    LogWarning($"[Save] 跳过未知存档块 [{block.Tag}]。");
                    continue;
                }
                if (field.Rank < ranks[field.Index]) {
                    payloads[field.Index] = block.Data;
                    ranks[field.Index] = field.Rank;
                }
            }
        } catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException
            or DecoderFallbackException) {
            LogWarning($"[Save] 当前存档块列表未能完整读取，保留已恢复的数据：{ex.Message}");
        }
        for (int i = 0; i < fields.Length; i++) {
            if (payloads[i] != null) { ReadBlockContent(fields[i].Tag, payloads[i], fields[i].Action); }
        }
    }

    /// <summary>
    /// 写入集合数量及各元素的独立长度边界；复杂元素内部仍用命名子块保存字段。
    /// </summary>
    /// <remarks>
    /// 调用方在具名业务块内保存集合，本方法的 Item 标签仅表示元素边界，不是业务身份或类型标记。
    /// 字典键或数组的稳定身份由元素回调读写，无须给每个元素另造字符串名称。
    /// </remarks>
    /// <typeparam name="T">集合元素类型，由调用方的回调决定其存储结构。</typeparam>
    /// <param name="writer">接收集合数据的写入器。</param>
    /// <param name="items">待写入的集合，不能为 null；空集合只写入数量 0，支持列表和数组。</param>
    /// <param name="writeItem">写入一项完整数据的回调，可继续嵌套命名块或集合。</param>
    public static void WriteList<T>(this BinaryWriter writer, IReadOnlyCollection<T> items,
        Action<BinaryWriter, T> writeItem) {
        if (writer == null) {
            throw new ArgumentNullException(nameof(writer));
        }
        if (items == null) {
            throw new ArgumentNullException(nameof(items));
        }
        if (writeItem == null) {
            throw new ArgumentNullException(nameof(writeItem));
        }

        writer.Write(items.Count);
        foreach (T item in items) {
            writer.WriteBlock("Item", w => writeItem(w, item));
        }
    }

    /// <summary>
    /// 根据存储的元素数量逐项读取并返回新列表，不按存档数量预分配容量。
    /// </summary>
    /// <typeparam name="T">回调创建的集合元素类型。</typeparam>
    /// <param name="reader">位于集合数量之前的读取器。</param>
    /// <param name="readItem">完整读取一项并返回其值的回调，可继续读取嵌套块或集合。</param>
    /// <param name="maximumCount">所属业务允许的最大元素数。</param>
    /// <returns>按存档顺序恢复的有效元素；无法读取的元素跳过。</returns>
    public static List<T> ReadList<T>(this BinaryReader reader, Func<BinaryReader, T> readItem, int maximumCount = 1000000) {
        if (readItem == null) {
            throw new ArgumentNullException(nameof(readItem));
        }

        List<T> items = [];
        reader.ReadList(r => {
            items.Add(readItem(r));
        }, maximumCount);
        return items;
    }

    /// <summary>
    /// 按存档数量读取有长度边界的元素；单项失败不改变下一项的读取位置。
    /// </summary>
    /// <param name="reader">位于集合数量之前的读取器。</param>
    /// <param name="readItem">处理独立元素流；目标已移除时可直接返回，其余字节由元素边界跳过。</param>
    /// <param name="maximumCount">所属业务允许的最大元素数。</param>
    /// <exception cref="InvalidDataException">集合元素数量越界。</exception>
    public static void ReadList(this BinaryReader reader, Action<BinaryReader> readItem, int maximumCount = 1000000) {
        if (reader == null) {
            throw new ArgumentNullException(nameof(reader));
        }
        if (readItem == null) {
            throw new ArgumentNullException(nameof(readItem));
        }

        int count = reader.ReadInt32();
        if (count < 0 || count > maximumCount) {
            throw new InvalidDataException($"集合元素数量越界：{count}，上限为 {maximumCount}。");
        }
        for (int i = 0; i < count; i++) {
            var block = ReadBlock(reader);
            if (block.Tag == "Item") { ReadBlockContent("Item", block.Data, readItem); }
        }
    }
}
