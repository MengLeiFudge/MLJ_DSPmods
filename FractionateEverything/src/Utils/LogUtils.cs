using BepInEx.Logging;

namespace FE.Utils;

public static partial class Utils {
    private static ManualLogSource logger;

    public static void InitLogger(ManualLogSource logger) {
        Utils.logger = logger;
    }

    public static void LogDebug(object data) => logger.LogDebug(data);
    public static void LogInfo(object data) => logger.LogInfo(data);
    public static void LogWarning(object data) => logger.LogWarning(data);
    public static void LogError(object data) => logger.LogError(data);
    public static void LogFatal(object data) => logger.LogFatal(data);
}
