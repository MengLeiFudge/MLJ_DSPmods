using System;
using System.IO;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 将一个数据块包装成带 Tag 和长度的格式写入。
    /// </summary>
    /// <param name="w">目标 BinaryWriter</param>
    /// <param name="tag">块标识符</param>
    /// <param name="writeAction">实际写入逻辑</param>
    public static void WriteBlock(this BinaryWriter w, string tag, Action<BinaryWriter> writeAction) {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        writeAction(bw);
        byte[] data = ms.ToArray();

        w.Write(tag);
        w.Write(data.Length);
        w.Write(data);
    }

    /// <summary>
    /// 读取并处理一个带 Tag 和长度的数据块。
    /// 如果处理过程中出错，只会影响该块，不会导致流偏移。
    /// </summary>
    /// <param name="r">源 BinaryReader</param>
    /// <param name="blockHandler">处理程序，根据 tag 处理 br 里的数据</param>
    public static void ReadAndHandleBlock(this BinaryReader r, Action<string, BinaryReader> blockHandler) {
        string tag = r.ReadString();
        int length = r.ReadInt32();
        byte[] data = r.ReadBytes(length);
        try {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            blockHandler(tag, br);
        }
        catch (Exception ex) {
            LogError($"Failed to handle block [{tag}]: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 尝试按顺序读取多个块，直到数据读取完毕或达到指定数量。
    /// </summary>
    public static void ReadBlocks(this BinaryReader r, int count, Action<string, BinaryReader> blockHandler) {
        for (int i = 0; i < count; i++) {
            r.ReadAndHandleBlock(blockHandler);
        }
    }
}
