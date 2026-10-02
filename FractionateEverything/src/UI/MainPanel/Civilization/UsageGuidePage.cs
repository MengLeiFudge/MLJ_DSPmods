using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>随实际发现开放的操作笔记，具体步骤仅涉及玩家已经接触的设备。</summary>
public sealed class UsageGuidePage : CardBrowserPage {
    protected override string Title => "使用指南";
    protected override string Summary => "有些经验，值得在下一次开工前翻一翻。";

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() {
        List<PageCard> cards = [
            new() { Key = "analysis", Title = "从样本开始", ItemId = IFE基础解析塔, Status = "基础解析塔",
                Detail = "残缺的记录留下了几种样本。将它们送进解析塔，也许就能发现一些奇特的东西……\n\n先在“研究 / 解析样本”里选一种已有记录的物品。给基础解析塔接通电网，从侧面送入样本，再将另一侧的流动出口接回输入，形成循环。\n\n大多数样本会完整地流出；只有解析成功时，才会消耗少量样本。可以一边建设其他产线，一边留意面板中的进度。" },
            new() { Key = "explore", Title = "循着回声寻找", ItemId = IFE残片, Status = "遗迹探索",
                Detail = "落地后，先留意屏幕左侧的信号读数。沿一个方向走一段，读数上升就继续前进；若逐渐变弱，换个方向试试。探测器会显示附近最强的一道信号。\n\n读数达到100%时，遗迹就在可以回收的距离内。岩石星球需要让伊卡洛斯落到地面；气态巨星没有可落脚的地面，可以在低空接近信号。\n\n回收时会打开一份结果记录。物资会送入亚空间，已经读过的文字也会保存在地点档案中。" },
            new() { Key = "storage", Title = "随身的库房", ItemId = IFE基础矿物塔, Status = "亚空间",
                Detail = "容器接通之后，伊卡洛斯多了一座不必随身背着的库房。遗迹中带回的物资都可以在“工业 / 亚空间仓储”中找到。\n\n暂时不用的物品可以从基础塔的正面端口送入亚空间。需要它们时，再到仓储页提取。容量不够时可以研究亚空间扩容，日常存取方式则在设置页调整。" },
        ];
        if (ResearchManager.IsContentUnlocked($"mineral-basic:{I铁矿}")
            || ResearchManager.IsContentUnlocked($"mineral-basic:{I铜矿}")
            || ResearchManager.IsContentUnlocked($"mineral-basic:{I石矿}")
            || ResearchManager.IsContentUnlocked($"mineral-basic:{I煤矿}")) {
            cards.Add(new PageCard { Key = "mineral", Title = "第一条复制产线", ItemId = IFE基础矿物塔, Status = "基础矿物塔",
                Detail = "同一块矿石离开塔时，竟然多出了一份。记录中的结构图确实有用。\n\n给基础矿物塔供电，把已经解析出配方的矿物送进侧面入口。另一侧流出的矿物接回输入循环，正面产物口则连接新的传送带。\n\n失败的矿物会继续流动，成功时才产出更多同种矿物。记得为两条输出都留出空间。" });
        }
        if (ExplorationManager.StoryCompleted >= 4) {
            cards.Add(new PageCard { Key = "living", Title = "维持生命", ItemId = IFE高级交互塔, Status = "生命塔",
                Detail = "这些塔不使用电网，矩阵才是它们的食物。为它们留下一条稳定供给矩阵的传送带，即使暂时停工，也不要立刻断粮。\n\n供养充足时，外壳上的裂纹会逐渐愈合；长期饥饿则会让它们衰弱。建造无人机无法代替供养。\n\n打开塔的窗口，选择一份已经解析出的配方，再送入所需原料。若需要搬家，主动拆除会返还缓存物品；已经损毁的塔无法保住这些物资。" });
        }
        if (ResearchManager.IsContentUnlocked(GrowthTechnologyManager.PlanetaryInteractionTechnologyContentId)
            || ResearchManager.IsContentUnlocked(GrowthTechnologyManager.InterstellarInteractionTechnologyContentId)) {
            cards.Add(new PageCard { Key = "logistics", Title = "接通亚空间", ItemId = IFE行星内物流交互站, Status = "物流交互站",
                Detail = "古老接口的结构已经恢复，可以着手建造物流交互站了。它能让产线直接与亚空间交换物品。\n\n在物流站窗口设定上传或下载方向，并接通充足的电力。存取所需能量会随物品价值变化，升级亚空间存取能效可以减少耗电。\n\n需要保留多少库存、何时开始存取，可以在“设置 / 亚空间存取”中调整。" });
        }
        if (ResearchManager.SacrificeUnlocked) {
            cards.Add(new PageCard { Key = "memory", Title = "留下来的光", ItemId = IFE记忆源点, Status = "源点",
                Detail = "过剩的生命塔也能把自己的经历留下来。将它们送入基础解析塔，有时会凝结出一份源点；若手头还有对应的样本记录，解析塔会先研究那些记录。\n\n源点先用于拼合已经能够辨认的阶段资料。资料恢复完毕后，继续投入源点，可以让仍在工作的高级塔得到短暂的成功率加成。\n\n加成按每分钟的供给更新。若想维持它，就为解析塔保留一条持续的源点供给；用于生产配方的源点仍由各自产线使用。" });
        }
        return cards;
    }
}
