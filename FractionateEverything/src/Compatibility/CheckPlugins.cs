using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
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
            if (_shown) return;
            if (!FractionateEverything.disableMessageBox) {
                ShowMessageBox();
            } else if (FractionateEverything.isVersionChanged) {
                ShowMessageBox141();
            }
            _shown = true;
        }

        private static void ShowMessageBox() {
            UIMessageBox.Show(
                "FE标题".Translate(), "FE信息".Translate(),
                "确定".Translate(), "FE日志".Translate(), "FE交流群".Translate(),
                UIMessageBox.INFO,
                Response确定1, ResponseFE日志, ResponseFE交流群
            );
        }

        private static void ShowMessageBox141() {
            //添加蓝图至蓝图库
            try {
                CopyDirectory(Path.Combine(FractionateEverything.ModPath, "blueprints"),
                    Path.Combine(GameConfig.blueprintFolder, "FEBlueprints".Translate()));
            }
            catch (Exception ex) {
                LogError($"Copy FractionateEverything Blueprints directory error: {ex}");
            }
            UIMessageBox.Show(
                "141标题".Translate(), "141信息".Translate(),
                "确定".Translate(), "FE日志".Translate(), "FE交流群".Translate(),
                UIMessageBox.INFO,
                Response确定2, ResponseFE日志, ResponseFE交流群
            );
        }

        private static void CopyDirectory(string sourceDir, string targetDir) {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the source directory does not exist, throw an exception.
            if (!dir.Exists) {
                throw new DirectoryNotFoundException(
                    $"Source directory does not exist or could not be found: {sourceDir}");
            }
            // If the destination directory does not exist, create it.
            if (!Directory.Exists(targetDir)) {
                Directory.CreateDirectory(targetDir);
            }
            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                string tempPath = Path.Combine(targetDir, file.Name);
                file.CopyTo(tempPath, false);
            }
            // If copying subdirectories, copy them and their contents to the new location.
            foreach (DirectoryInfo subDir in dirs) {
                string tempPath = Path.Combine(targetDir, subDir.Name);
                CopyDirectory(subDir.FullName, tempPath);
            }
        }

        private static void ResponseFE交流群() {
            Application.OpenURL("FE交流群链接".Translate());
            Response确定1();
        }

        private static void ResponseFE日志() {
            Application.OpenURL(string.IsNullOrEmpty(FractionateEverything.VERSION_Debug)
                ? "FE日志链接".Translate()
                : Path.Combine(FractionateEverything.ModPath, "CHANGELOG.md"));
            Response确定1();
        }

        private static void Response确定1() {
            FractionateEverything.SetConfig1();
            ShowMessageBox141();
        }

        private static void Response确定2() {
            FractionateEverything.SetConfig2();
        }
    }
}
