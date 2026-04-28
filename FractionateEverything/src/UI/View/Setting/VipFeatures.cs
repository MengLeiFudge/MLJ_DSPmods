using System;
using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using FE.UI.View.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

/// <summary>
/// 旧版 VIP 模块，仅为兼容旧存档数据保留。
/// 当前 2.3 主路径不再注册或消费该模块。
/// </summary>
public static class VipFeatures {
    private static RectTransform window;
    private static RectTransform tab;

    #region VIP相关数据

    /// <summary>
    /// 当前VIP等级
    /// </summary>
    private static int Level = 0;
    /// <summary>
    /// 当前VIP经验值
    /// </summary>
    private static float Exp = 0;
    /// <summary>
    /// 经验达到多少可以升级VIP
    /// </summary>
    private static float ExpLevelUp => Level == 0 ? 1 : 1000 * (float)Math.Pow(2.0f, Level);

    /// <summary>
    /// 旧版 VIP 经验接口。
    /// 当前版本已冻结，保留为空实现，避免遗留调用重新启用旧逻辑。
    /// </summary>
    public static void AddExp(float exp) { }

    /// <summary>
    /// 旧版 VIP 抽奖倍率。当前冻结为中性值。
    /// </summary>
    public static float TicketValueMulti => 1f;
    /// <summary>
    /// 旧版 VIP 免费兑换项数。当前冻结为 0。
    /// </summary>
    public static int FreeExchangeCount => 0;
    /// <summary>
    /// 旧版 VIP 商店折扣。当前冻结为中性值。
    /// </summary>
    public static float ExchangeDiscount => 1f;

    #endregion

    private static Text txtVipInfo;
    private static Text[] txtVipBonus = new Text[3];

    public static void AddTranslations() {
        Register("VIP特权", "VIP Features");

        Register("VIP等级：", "Legacy VIP level: ");
        Register("VIP加成如下：", "Legacy VIP data (archived):");
        Register("奖券抽奖加成",
            "Legacy archived bonus: x{0:P2}",
            "旧版归档倍率：x{0:P2}");
        Register("限时商店免费兑换项数",
            "Legacy archived free exchanges: {0}",
            "旧版归档免费项：{0}");
        Register("限时商店购买折扣",
            "Legacy archived discount: {0:P2}",
            "旧版归档折扣：{0:P2}");
        //todo: 商店刷新间隔减少，刷新需要矩阵减少
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        CreateUIInternal(wnd, trans);
    }

    private static void CreateUIInternal(MyWindow wnd, RectTransform parent) {
        float x = 0f;
        float y = 18f;
        txtVipInfo = wnd.AddText2(x, y, parent, "动态刷新");
        txtVipInfo.supportRichText = true;
        y += 36f;
        wnd.AddText2(x, y, parent, "VIP加成如下：");
        for (int i = 0; i < 3; i++) {
            y += 36f;
            txtVipBonus[i] = wnd.AddText2(x, y, parent, "动态刷新");
            txtVipBonus[i].supportRichText = true;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        txtVipInfo.text = $"{"VIP等级：".Translate()}{Level} ({Exp:F0} / {Math.Ceiling(ExpLevelUp):F0})"
            .WithColor((Level - 1) / 3 + 1);
        txtVipBonus[0].text = string.Format("奖券抽奖加成".Translate(), TicketValueMulti)
            .WithColor((Level - 1) / 3 + 1);
        txtVipBonus[1].text = string.Format("限时商店免费兑换项数".Translate(), FreeExchangeCount)
            .WithColor((Level - 1) / 3 + 1);
        txtVipBonus[2].text = string.Format("限时商店购买折扣".Translate(), ExchangeDiscount)
            .WithColor((Level - 1) / 3 + 1);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Exp", br => {
                Exp = br.ReadSingle();
                AddExp(0);
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(("Exp", bw => bw.Write(Exp)));
    }

    public static void IntoOtherSave() {
        Exp = 0;
        Level = 0;
    }

    #endregion
}
