using BepInEx.Logging;
using UnityEngine;

namespace FE.Utils;

public static partial class Utils {
    private static ManualLogSource logger;

    public static void InitLogger(ManualLogSource logger) {
        Utils.logger = logger;
    }

    public static void LogDebug(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}]{data}");
        } else {
            logger.LogDebug(data);
        }
    }

    public static void LogInfo(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}]{data}");
        } else {
            logger.LogInfo(data);
        }
    }

    public static void LogWarning(object data) {
        if (logger == null) {
            Debug.LogWarning($"[{PluginInfo.PLUGIN_NAME}]{data}");
        } else {
            logger.LogWarning(data);
        }
    }

    public static void LogError(object data) {
        if (logger == null) {
            Debug.LogError($"[{PluginInfo.PLUGIN_NAME}]{data}");
        } else {
            logger.LogError(data);
        }
    }

    public static void LogFatal(object data) {
        if (logger == null) {
            Debug.LogError($"[{PluginInfo.PLUGIN_NAME}]{data}");
        } else {
            logger.LogFatal(data);
        }
    }
}
