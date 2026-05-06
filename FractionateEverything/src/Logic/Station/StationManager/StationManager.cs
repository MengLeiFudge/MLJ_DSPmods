using static FE.Utils.Utils;

namespace FE.Logic.Station;

/// <summary>
/// 物流交互站管理器。
/// 负责交互站与数据中心的物品同步、两套站点面板的扩展按钮，以及弹窗状态与存档读写。
/// </summary>
public static partial class StationManager {
    /// <summary>
    /// 注册交互站扩展按钮的多语言文本。
    /// </summary>
    public static void AddTranslations() {
        // 传输模式按钮文本
        Register("双向同步", "Sync", "双向同步");
        Register("仅上传", "Upload Only", "仅上传");
        Register("仅下载", "Download Only", "仅下载");
        // 容量模式按钮文本
        Register("有限上传", "Limited Upload", "有限上传");
        Register("无限上传", "Infinite Upload", "无限上传");
    }
}
