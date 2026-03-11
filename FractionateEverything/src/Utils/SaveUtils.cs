using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FE.Utils;

public static partial class Utils {
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
        byte[] data = r.ReadBytes(length);
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
}
