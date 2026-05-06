using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.View.ProgressTask;
using FE.UI.View.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static partial class FracRecipeOperate {
    // ==================== 配置加载 ====================

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("Recipe Operate", "Recipe Type", 0, "想要查看的配方类型。");
        if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypes.Length) {
            RecipeTypeEntry.Value = 0;
        }
        selectedInc = configFile.Bind("Recipe Operate", "Selected Inc", 0, "想要查看的最终输出的增产点数");
        if (selectedInc.Value is < 0 or > 10) {
            selectedInc.Value = 0;
        }
    }

    #region IModCanSave

    // ==================== 存档 ====================

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }

    #endregion
}
