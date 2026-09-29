using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FE.Utils;

/// <summary>
/// 标签化存档块读写辅助方法。
/// </summary>
public static partial class Utils {
    private const int MaximumSaveBlockCount = 4096;

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
    /// <param name="w">目标 BinaryWriter</param>
    /// <param name="writeActions">包含多个块的数组，每个块由块标识符和写入逻辑组成</param>
    public static void WriteBlocks(this BinaryWriter w,
        params (string tag, Action<BinaryWriter> action)[] writeActions) {
        w.Write(writeActions.Length);// 自动写入块的数量
        foreach (var (tag, action) in writeActions) {
            w.WriteBlock(tag, action);
        }
    }

    /// <summary>
    /// 读取并处理一个带 Tag 和长度的数据块。
    /// 如果处理过程中出错，只会影响该块，不会导致流偏移。
    /// </summary>
    /// <param name="r">源 BinaryReader</param>
    /// <param name="readAction">处理程序，根据 tag 处理 br 里的数据</param>
    private static void ReadBlock(this BinaryReader r, Action<string, BinaryReader> readAction) {
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
        try {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            readAction(tag, br);
        }
        catch (Exception ex) {
            LogError($"Failed to handle block [{tag}]: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 尝试按顺序读取多个块，直到数据读取完毕或达到指定数量。
    /// </summary>
    /// <param name="r">源 BinaryReader</param>
    /// <param name="readActions">处理程序，根据 tag 处理 br 里的数据</param>
    public static void ReadBlocks(this BinaryReader r, params (string tag, Action<BinaryReader> action)[] readActions) {
        // 转换为字典以便快速匹配（Tag 顺序不一致也能正确处理）
        var handlerDict = readActions.ToDictionary(h => h.tag, h => h.action);
        int count = r.ReadInt32();
        if (count < 0 || count > MaximumSaveBlockCount) {
            throw new InvalidDataException($"存档块数量无效：{count}。");
        }
        for (int i = 0; i < count; i++) {
            r.ReadBlock((tag, br) => {
                if (handlerDict.TryGetValue(tag, out var action)) {
                    action(br);
                } else {
                    LogWarning($"[Save] Skipping unknown tag: {tag}");
                }
            });
        }
    }

    /// <summary>
    /// 写入集合元素数量，再按枚举顺序写入各项；数量和元素布局与手动循环一致。
    /// </summary>
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
            writeItem(writer, item);
        }
    }

    /// <summary>
    /// 根据存储的元素数量逐项读取并返回新列表，不按存档数量预分配容量。
    /// </summary>
    /// <typeparam name="T">回调创建的集合元素类型。</typeparam>
    /// <param name="reader">位于集合数量之前的读取器。</param>
    /// <param name="readItem">完整读取一项并返回其值的回调，可继续读取嵌套块或集合。</param>
    /// <returns>按存档顺序恢复的新列表；数量为 0 时返回空列表。</returns>
    public static List<T> ReadList<T>(this BinaryReader reader, Func<BinaryReader, T> readItem) {
        if (readItem == null) {
            throw new ArgumentNullException(nameof(readItem));
        }

        List<T> items = [];
        reader.ReadList(r => {
            items.Add(readItem(r));
        });
        return items;
    }

    /// <summary>
    /// 根据存储的元素数量逐项执行读取回调，供调用方直接恢复已有对象而不创建临时列表。
    /// </summary>
    /// <param name="reader">位于集合数量之前的读取器。</param>
    /// <param name="readItem">消费一项完整数据的回调；即使目标对象不存在也必须读完该项。</param>
    /// <exception cref="InvalidDataException">集合元素数量为负数。</exception>
    public static void ReadList(this BinaryReader reader, Action<BinaryReader> readItem) {
        if (reader == null) {
            throw new ArgumentNullException(nameof(reader));
        }
        if (readItem == null) {
            throw new ArgumentNullException(nameof(readItem));
        }

        int count = reader.ReadInt32();
        if (count < 0) {
            throw new InvalidDataException($"集合元素数量不能为负数：{count}。");
        }
        for (int i = 0; i < count; i++) {
            readItem(reader);
        }
    }
}
