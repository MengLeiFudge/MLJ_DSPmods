using System.IO;
using HarmonyLib;
using FE.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 动态经济系统的统一入口。
/// 这里只负责调度，不承担具体定价/报价/交易逻辑。
/// </summary>
public static class EconomyManager {
    public static void Init() {
        MarketValueManager.Init();
        FragmentExchangeManager.Init();
        ExchangeManager.Init();
        MarketBoardManager.Init();
    }

    public static void Tick() {
        MarketValueManager.Tick();
        FragmentExchangeManager.Tick();
        ExchangeManager.Tick();
        MarketBoardManager.Tick();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameMain_FixedUpdate_Postfix() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null || !GameMain.isRunning) {
            return;
        }
        Tick();
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("MarketValue", MarketValueManager.Import),
            ("FragmentExchange", FragmentExchangeManager.Import),
            ("Exchange", ExchangeManager.Import),
            ("MarketBoard", MarketBoardManager.Import)
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("MarketValue", MarketValueManager.Export),
            ("FragmentExchange", FragmentExchangeManager.Export),
            ("Exchange", ExchangeManager.Export),
            ("MarketBoard", MarketBoardManager.Export)
        );
    }

    public static void IntoOtherSave() {
        MarketValueManager.IntoOtherSave();
        FragmentExchangeManager.IntoOtherSave();
        ExchangeManager.IntoOtherSave();
        MarketBoardManager.IntoOtherSave();
    }
}
