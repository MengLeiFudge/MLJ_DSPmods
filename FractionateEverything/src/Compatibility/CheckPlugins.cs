using BepInEx;
using BepInEx.Logging;

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
            MoreMegaStructure.Compatible();
            TheyComeFromVoid.Compatible();
            GenesisBook.Compatible();
        }
    }
}
