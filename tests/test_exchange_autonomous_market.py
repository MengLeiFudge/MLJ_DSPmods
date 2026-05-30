import re
import unittest
from pathlib import Path


EXCHANGE_MANAGER = Path("FractionateEverything/src/Logic/Economy/ExchangeManager.cs")
EXCHANGE_UI = Path("FractionateEverything/src/UI/MainPanel/ResourceInteraction/Exchange.cs")
FRAC_STATISTIC = Path("FractionateEverything/src/UI/MainPanel/Archive/FracStatistic.cs")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


class ExchangeAutonomousMarketTests(unittest.TestCase):
    def test_refresh_tickers_uses_external_order_flow(self):
        source = read_text(EXCHANGE_MANAGER)
        self.assertIn("PriceRefreshIntervalTicks = 15L * 60L", source)
        self.assertIn("CalculateExternalOrderFlow(ticker, anchor)", source)
        self.assertIn("ticker.NetMarketVolume * FlowCarryRatio + externalFlow", source)
        self.assertIn("ticker.NetMarketVolume = Mathf.RoundToInt(netFlow)", source)

        refresh_body = re.search(
            r"private static void RefreshTickers\(\) \{\n(?P<body>.*?)\n    \}",
            source,
            re.DOTALL,
        )
        self.assertIsNotNone(refresh_body, "未找到 RefreshTickers 方法")
        self.assertIn(
            "CalculateExternalOrderFlow",
            refresh_body.group("body"),
            "行情刷新必须注入外部市场流量，不能只依赖玩家交易冲击",
        )

    def test_player_trade_impact_feeds_market_flow_alias(self):
        source = read_text(EXCHANGE_MANAGER)
        self.assertIn("public int NetMarketVolume", source)
        self.assertIn("get => NetPlayerVolume;", source)
        self.assertIn("set => NetPlayerVolume = value;", source)
        self.assertIn("ticker.NetMarketVolume += count", source)
        self.assertIn("ticker.NetMarketVolume -= count", source)

    def test_ui_displays_price_change_and_market_flow(self):
        exchange_ui = read_text(EXCHANGE_UI)
        frac_statistic = read_text(FRAC_STATISTIC)
        self.assertIn("ticker.ChangePercent:+0.00;-0.00;0.00", exchange_ui)
        self.assertIn("市场净流量 {ticker.NetMarketVolume}", exchange_ui)
        self.assertIn("净流量 {hotTicker.NetMarketVolume}", frac_statistic)

    def test_frac_statistic_uses_saturating_volume_magnitude(self):
        frac_statistic = read_text(FRAC_STATISTIC)
        self.assertIn(
            ".OrderByDescending(ticker => GetVolumeMagnitude(ticker.NetMarketVolume))",
            frac_statistic,
        )
        self.assertIn("volume == int.MinValue ? int.MaxValue : Mathf.Abs(volume)", frac_statistic)
        self.assertNotIn(
            ".OrderByDescending(ticker => Mathf.Abs(ticker.NetMarketVolume))",
            frac_statistic,
        )


if __name__ == "__main__":
    unittest.main()
