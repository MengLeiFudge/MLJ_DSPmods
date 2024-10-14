using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using crecheng.DSPModSave;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using VerticalConstruction.Compatibility;
using VerticalConstruction.Utils;

namespace VerticalConstruction {
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(TabSystem), nameof(LocalizationModule))]
    [BepInDependency(CheckPlugins.GUID)]
    public class VerticalConstruction : BaseUnityPlugin, IModCanSave {
        public const string GUID = "com.menglei.dsp." + NAME;
        public const string NAME = "VerticalConstruction";
        public const string VERSION_Main = "1.0.0";
        public const string VERSION_Debug = "";
        public const string VERSION = VERSION_Main + VERSION_Debug;

        #region Logger

        private static ManualLogSource logger;
        public static void LogDebug(object data) => logger.LogDebug(data);
        public static void LogInfo(object data) => logger.LogInfo(data);
        public static void LogWarning(object data) => logger.LogWarning(data);
        public static void LogError(object data) => logger.LogError(data);
        public static void LogFatal(object data) => logger.LogFatal(data);

        #endregion

        #region Fields

        public static readonly int CurrentSavedataVersion = 3;

        private static ConfigFile configFile;
        public static ConfigEntry<bool> IsResetNextIds;

        public static readonly Harmony harmony = new(GUID);
        private static bool _finished;

        #endregion

        ~VerticalConstruction() {
            if (IsResetNextIds.Value) {
                IsResetNextIds.Value = false;
                Config.Save();
            }
        }

        public void Awake() {
            using (ProtoRegistry.StartModLoad(GUID)) {
                logger = Logger;

                LoadConfig();

                var executingAssembly = Assembly.GetExecutingAssembly();
                foreach (Type type in executingAssembly.GetTypes()) {
                    //Compatibility内的类由自己patch，不在这里处理
                    if (type.Namespace == null
                        || type.Namespace.StartsWith("VerticalConstruction.Compatibility")) {
                        continue;
                    }
                    harmony.PatchAll(type);
                }
            }
        }

        public void LoadConfig() {
            configFile = Config;

            IsResetNextIds = Config.Bind("config", "IsResetNextIds", false,
                new ConfigDescription(
                    "如果加载保存数据时必须重新计算构建覆盖关系，则为 true。\n"
                    + "游戏关闭时该值始终重置为 false。\n"
                    + "true if building overlay relationships must be recalculated when loading save data.\n"
                    + "This value is always reset to false when the game is closed.",
                    new AcceptableBoolValue(false), null));

            //移除之前多余的设置项，然后保存
            (Traverse.Create(Config).Property("OrphanedEntries").GetValue() as IDictionary)?.Clear();
            Config.Save();
        }

        public void Import(BinaryReader binaryReader) {
            if (DSPGame.Game == null) {
                return;
            }

            if (IsResetNextIds.Value) {
                LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }

            var version = binaryReader.ReadInt32() * -1;//将其作为负数处理，因为正数可能会被误解为assemblerCapacity
            if (version < CurrentSavedataVersion) {
                LogInfo(string.Format("Old save data version: read {0} current {1}", version,
                    CurrentSavedataVersion));
                LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }
            if (version != CurrentSavedataVersion) {
                LogWarning(string.Format("Invalid save data version: read {0} current {1}", version,
                    CurrentSavedataVersion));
                LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }

            var assemblerCapacity = binaryReader.ReadInt32();

            if (assemblerCapacity > AssemblerPatches.assemblerComponentEx.assemblerCapacity) {
                AssemblerPatches.assemblerComponentEx.SetAssemblerCapacity(assemblerCapacity);
            }

            for (int i = 0; i < assemblerCapacity; i++) {
                var num = binaryReader.ReadInt32();
                for (int j = 0; j < num; j++) {
                    var nextId = binaryReader.ReadInt32();
                    AssemblerPatches.assemblerComponentEx.SetAssemblerNext(i, j, nextId);
                }
            }
        }

        public void Export(BinaryWriter binaryWriter) {
            if (DSPGame.Game == null) {
                return;
            }

            binaryWriter.Write(CurrentSavedataVersion * -1);//将其作为负数处理，因为正数可能会被误解为assemblerCapacity

            binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerCapacity);
            for (int i = 0; i < AssemblerPatches.assemblerComponentEx.assemblerCapacity; i++) {
                if (AssemblerPatches.assemblerComponentEx.assemblerNextIds[i] != null) {
                    binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerNextIds[i].Length);
                    for (int j = 0; j < AssemblerPatches.assemblerComponentEx.assemblerNextIds[i].Length; j++) {
                        binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerNextIds[i][j]);
                    }
                } else {
                    binaryWriter.Write(0);
                }
            }
        }

        public void IntoOtherSave() {
            // 在标题演示的工厂加载中也会调用它，但不应该使用该模块的功能，因此不要使用 ResetNextIds() 以降低成本。
            if (DSPGame.Game == null || DSPGame.IsMenuDemo) {
                return;
            }

            LogInfo("ResetNextIds");

            AssemblerPatches.ResetNextIds();
        }
    }
}
