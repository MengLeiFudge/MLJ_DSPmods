using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility.Mods;
using FE.UI.MainPanel.ProgressTask;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Logic.Progression;
/// <summary>
/// 教程条目和触发条件注册入口。
/// </summary>
public static partial class TutorialManager {
    public static void AddTutorials() {
        //DeterminatorName：解锁时机
        //DeterminatorParams：解锁参数
        //TOR_GameSecond    第几秒提示，参数为[伊卡洛斯落地后第几秒]
        //TOR_TechUnlocked  哪个科技解锁后提示，参数为[科技ID, 科技研究完成后再等待几秒]（第二个参数一般为4或7）
        //TOR_OnBuild       哪种建筑建造后提示，参数为[建筑1ID, 建筑2ID, ...]（任何一个建筑建造都会提示）
        //TOR_SandboxMode   沙盒模式提示，参数为[]
        //TOR_CombatMode    战斗系统提示，参数为[]
        //TOR_LowFuel       低能量提示，参数为[]
        //TOR_RecipeCopyTip 配方复制提示，参数为[]

        foreach (TutorialRegistration registration in tutorialRegistrations) {
            AddTutorial(registration);
        }
    }

    private static void AddTutorial(TutorialRegistration registration) {
        TutorialProto proto = new() {
            ID = registration.Id,
            SID = "",
            Name = $"{registration.BaseName}标题",
            name = $"{registration.BaseName}标题",
            LayoutFileName = $"{FeTutorialLayoutPrefix}{registration.Id}",
            DeterminatorName = registration.DeterminatorName,
            DeterminatorParams = registration.DeterminatorParams,
        };
        LDBTool.PreAddProto(proto);
        proto.Preload();
    }
}
