using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using UnityEngine;

namespace FractionateEverything.Compatibility {
    /// <summary>
    /// 加载万物分馏主插件前，检测是否使用其他mod，并对其进行适配。
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(MoreMegaStructure.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(TheyComeFromVoid.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(GenesisBook.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class CheckPlugins : BaseUnityPlugin {
        public const string GUID = FractionateEverything.GUID + ".CheckPlugins";
        public const string NAME = FractionateEverything.NAME + ".CheckPlugins";
        public const string VERSION = FractionateEverything.VERSION;

        private static bool _shown;

        #region Logger

        private static ManualLogSource logger;
        public static void LogDebug(object data) => logger.LogDebug(data);
        public static void LogInfo(object data) => logger.LogInfo(data);
        public static void LogWarning(object data) => logger.LogWarning(data);
        public static void LogError(object data) => logger.LogError(data);
        public static void LogFatal(object data) => logger.LogFatal(data);

        #endregion

        public void Awake() {
            logger = Logger;

            new Harmony(GUID).Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(CheckPlugins), nameof(OnMainMenuOpen)) { priority = Priority.Last }
            );

            MoreMegaStructure.Compatible();
            TheyComeFromVoid.Compatible();
            GenesisBook.Compatible();
        }

        public static void OnMainMenuOpen() {
            if (FractionateEverything.disableMessageBox || _shown) return;
            _shown = true;
            UIMessageBox.Show(
                "FE标题".Translate(), "FE信息".Translate(),
                "确定".Translate(), "FE交流群".Translate(), "FE日志".Translate(),
                UIMessageBox.INFO,
                null, OpenBrowser, OpenLog
            );
        }

        private static void OpenBrowser() => Application.OpenURL("FE交流群链接".Translate());

        private static void OpenLog() =>
            Application.OpenURL(Path.Combine(FractionateEverything.ModPath, "CHANGELOG.md"));
    }
}
