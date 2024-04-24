using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.ProtoID;

namespace FractionateEverything
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(TabSystem), nameof(LocalizationModule))]
    public class FractionateEverything : BaseUnityPlugin
    {
        private const string GUID = "com.menglei.dsp." + NAME;
        private const string NAME = "FractionateEverything";
        private const string VERSION = "1.2.0";
        private static ManualLogSource logger;

        /// <summary>
        /// 是否启用前置科技。如果不启用，所有分馏配方将在开局可用。
        /// </summary>
        private static bool usePreTech;
        /// <summary>
        /// 是否显示所有分馏配方。
        /// </summary>
        private static bool showRecipes;
        /// <summary>
        /// 分馏配方从哪页开始显示。
        /// </summary>
        private static int firstPage;
        /// <summary>
        /// 新增分馏配方的ID，使用后应+1。
        /// </summary>
        private static int nextRecipeID;
        /// <summary>
        /// 用于获取图标资源。
        /// </summary>
        private AssetBundle ab;
        /// <summary>
        /// 用于Update方法的循环计时。
        /// </summary>
        private DateTime lastUpdateTime = DateTime.Now;
        /// <summary>
        /// 存储所有配方的显示位置，以确保没有显示位置冲突
        /// </summary>
        private readonly List<int> gridIndexList = [];
        /// <summary>
        /// 如果发生显示位置冲突，从这里开始显示冲突的配方
        /// </summary>
        private int currLastIdx = 4701;
        /// <summary>
        /// 存储所有分馏配方信息
        /// </summary>
        private static readonly Dictionary<int, FracRecipe> fracRecipeDic = [];
        /// <summary>
        /// 伪随机数种子。
        /// </summary>
        private static uint seed2 = (uint)new System.Random().Next(int.MinValue, int.MaxValue);
#if DEBUG
        /// <summary>
        /// sprite名称将被记录在该文件中。
        /// </summary>
        private const string SPRITE_CSV_PATH = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\gamedata\fracIconPath.csv";
#endif

        public void Awake()
        {
            logger = Logger;

            LocalizationModule.RegisterTranslation("分馏页面1", "Fractionate I", "分馏 I", "Fractionate I");
            LocalizationModule.RegisterTranslation("分馏页面2", "Fractionate II", "分馏 II", "Fractionate II");
            LocalizationModule.RegisterTranslation("分馏", " Fractionation", "分馏", " Fractionation");
            LocalizationModule.RegisterTranslation("从", "Fractionate ", "从", "Fractionate ");
            LocalizationModule.RegisterTranslation("中分馏出", " to ", "中分馏出", " to ");
            LocalizationModule.RegisterTranslation("。", ".", "。", ".");
            LocalizationModule.RegisterTranslation("分馏出", " fractionated ", "分馏出", " fractionated");
            LocalizationModule.RegisterTranslation("个产物", " product", "个产物", " product");

            LocalizationModule.RegisterTranslation("低功率分馏塔", "Low Power Fractionator", "低功率分馏塔", "Low Power Fractionator");
            LocalizationModule.RegisterTranslation("I低功率分馏塔",
                "The same functionality as the Universal Fractionator, but with a significant reduction in power consumption. Although there is some reduction in capacity, it allows us to use powerful fractionation features early in the development process. The Low Power Fractionator has a larger footprint and higher capacity for the same amount of power, making it suitable for large-scale construction.",
                "与通用分馏塔功能相同，但耗电大幅减少。虽然产能有一定下降，但它能使我们在发展初期就使用强大的分馏功能。在相同电力情况下，低功率分馏塔占地更大，产能更高，适合大规模建造使用。",
                "The same functionality as the Universal Fractionator, but with a significant reduction in power consumption. Although there is some reduction in capacity, it allows us to use powerful fractionation features early in the development process. The Low Power Fractionator has a larger footprint and higher capacity for the same amount of power, making it suitable for large-scale construction.");
            LocalizationModule.RegisterTranslation("通用分馏塔", "Universal Fractionator", "通用分馏塔", "Universal Fractionator");
            LocalizationModule.RegisterTranslation("I通用分馏塔",
                "The Universal Fractionator is used in a large number of applications throughout the universe. However, many Icarus have only mastered the method of fractionating out deuterium, but do not know of any other uses for its true power. In fact, it can fractionate everything - as long as you have the ability to make the target products, you can access them more easily through the Fractionator.",
                "通用分馏塔在宇宙各处都有大量应用。不过，很多伊卡洛斯只掌握了分馏出重氢的方法，却不知道其他的用法，无法发挥出它的真正威力。其实，它可以分馏万物——只要你拥有了制造目标产物的能力，就可以通过分馏塔更便捷地获取它们。",
                "The Universal Fractionator is used in a large number of applications throughout the universe. However, many Icarus have only mastered the method of fractionating out deuterium, but do not know of any other uses for its true power. In fact, it can fractionate everything - as long as you have the ability to make the target products, you can access them more easily through the Fractionator.");
            LocalizationModule.RegisterTranslation("建筑极速分馏塔", "Building-HighSpeed Fractionator", "建筑极速分馏塔", "Building-HighSpeed Fractionator");
            LocalizationModule.RegisterTranslation("I建筑极速分馏塔",
                "Still worried about the piles of low-level buildings in your storage box? With this, we can quickly convert low-level buildings into high-level buildings. However, non-building items seem to have become extremely difficult to fractionate. Maybe we should double check if the item being fractionated is a building before fractionating.",
                "还在为储物箱中堆积的低级建筑而烦恼吗？有了它，我们能很快地将低级建筑转换为高级建筑。不过，非建筑物品似乎变得极难分馏了。也许在分馏前，我们应该仔细确认被分馏的物品是不是建筑。",
                "Still worried about the piles of low-level buildings in your storage box? With this, we can quickly convert low-level buildings into high-level buildings. However, non-building items seem to have become extremely difficult to fractionate. Maybe we should double check if the item being fractionated is a building before fractionating.");
            LocalizationModule.RegisterTranslation("增殖分馏塔", "Augmentation fractionator", "增殖分馏塔", "Augmentation fractionator");
            LocalizationModule.RegisterTranslation("I增殖分馏塔",
                "Along with having the ability to make the Cosmic Matrix, we also learned how to make this extremely powerful Fractionation Tower. It has a certain probability of doubling the output of the product, literally creating something out of nothing. If you input raw materials that have been sprayed with a production enhancer, it does not speed up production, but rather extra output. The extra output can be stacked with the doubled output for up to 4 items at a time.",
                "拥有制作宇宙矩阵能力的同时，我们也得知了如何制作这个极其强大的分馏塔。它有一定概率将产物翻倍输出，真正达到无中生有的效果。如果输入被增产剂喷涂过的原料，并不会加速生产，而是额外产出。额外产出可以与翻倍输出叠加，最多一次产出4个物品。",
                "Along with having the ability to make the Cosmic Matrix, we also learned how to make this extremely powerful Fractionation Tower. It has a certain probability of doubling the output of the product, literally creating something out of nothing. If you input raw materials that have been sprayed with a production enhancer, it does not speed up production, but rather extra output. The extra output can be stacked with the doubled output for up to 4 items at a time.");

            ConfigEntry<bool> UsePreTech = Config.Bind("config", "UsePreTech", true,
                new ConfigDescription("Whether or not to use front-end tech.\n" +
                                      "If set to false, all fractionation recipes are unlocked at the beginning.\n" +
                                      "是否使用前置科技。\n" +
                                      "如果设为false，所有分馏配方都会在开局解锁。", new AcceptableBoolValue(true), null));
            usePreTech = UsePreTech.Value;

            ConfigEntry<bool> ShowFractionateRecipes = Config.Bind("config", "ShowFractionateRecipes", true,
                new ConfigDescription("Whether show all fractionate recipes or not.\n" +
                                      "是否显示所有的分馏配方。", new AcceptableBoolValue(true), null));
            showRecipes = ShowFractionateRecipes.Value;
            firstPage = 3;
            if (showRecipes)
            {
                string iconPath = LDB.techs.Select(T重氢分馏).IconPath;
                firstPage = TabSystem.RegisterTab(GUID + "Tab1", new TabData("分馏页面1".Translate(), iconPath));
                TabSystem.RegisterTab(GUID + "Tab2", new TabData("分馏页面2".Translate(), iconPath));
            }

            //配方ID是int型，没有限制
            ConfigEntry<int> FirstRecipeID = Config.Bind("config", "FirstRecipeID", 1000,
                new ConfigDescription("Which recipe ID to start adding fractionated recipes (1000-100000).\n" +
                                      "Can be used to avoid recipe conflicts with other mods.\n" +
                                      "从哪个ID开始添加分馏配方（1000-100000）。\n" +
                                      "用于避免mod之间可能存在的配方ID冲突。", new AcceptableIntValue(1000, 1000, 100000), null));
            nextRecipeID = FirstRecipeID.Value;

            Config.Save();


            ab = AssetBundle.LoadFromStream(
                Assembly.GetExecutingAssembly().GetManifestResourceStream("FractionateEverything.fracicons"));

            LDBTool.EditDataAction += EditDataAction;
            LDBTool.PreAddDataAction += AddFractionators;
            LDBTool.PostAddDataAction += AddFracRecipes;
            Harmony.CreateAndPatchAll(typeof(FractionateEverything), GUID);
        }

        public void Update()
        {
            DateTime time = DateTime.Now;
            if ((time - lastUpdateTime).TotalMilliseconds < 200)
            {
                return;
            }
            lastUpdateTime = time;
            //running为true表示游戏已经加载好（不是在某存档内部，仅仅是加载好能显示主界面了）
            if (GameMain.instance is null || !GameMain.instance.running)
            {
                return;
            }
            //不断更新分馏塔可接受哪些物品
            int oldLen = RecipeProto.fractionatorRecipes.Length;
            if (usePreTech)
            {
                //仅添加已解锁的配方
                RecipeProto[] dataArray = LDB.recipes.dataArray;
                List<RecipeProto> list = [];
                List<int> list2 = [];
                foreach (RecipeProto r in dataArray)
                {
                    if (r.Type == ERecipeType.Fractionate && GameMain.history.RecipeUnlocked(r.ID))
                    {
                        list.Add(r);
                        list2.Add(r.Items[0]);
                    }
                }
                RecipeProto.fractionatorRecipes = [.. list];
                RecipeProto.fractionatorNeeds = [.. list2];
            }
            else
            {
                //添加所有配方
                RecipeProto.InitFractionatorNeeds();
            }
            int currLen = RecipeProto.fractionatorRecipes.Length;
            if (oldLen != currLen)
            {
                logger.LogInfo($"RecipeProto.fractionatorRecipes.Length: {oldLen} -> {currLen}");
            }
        }

        private void EditDataAction(Proto proto)
        {
            if (proto is ItemProto { ID: I分馏塔 } item)
            {
                item.Name = "通用分馏塔";
                item.name = "通用分馏塔".Translate();
                item.Description = "I通用分馏塔";
                item.description = "I通用分馏塔".Translate();
                item.GridIndex = 2603;
                item.maincraft.GridIndex = 2603;
                item.BuildIndex = 409;
                LDBTool.SetBuildBar(item.BuildIndex / 100, item.BuildIndex % 100, item.ID);
            }
        }

        private void AddFractionators()
        {
            //低功率分馏塔
            //耗电量为原版分馏塔的20%，分馏成功率为原版分馏塔的33.33%
            AddFractionator(I低功率分馏塔, "低功率分馏塔",
                [I铁块, I铜块, I磁线圈], [4, 2, 2],
                2601, T电磁学, 407, M低功率分馏塔,
                new Color(0.6275f, 0.3804f, 0.6431f), 0.2);

            //建筑极速分馏塔
            //分馏建筑成功率12.5%，分馏非建筑成功率0.1%
            AddFractionator(I建筑极速分馏塔, "建筑极速分馏塔",
                [I铁块, I石材, I玻璃, I电路板], [8, 4, 4, 1],
                2602, T改良物流系统, 408, M建筑极速分馏塔,
                new Color(0.3216F, 0.8157F, 0.09020F), 1.0);

            //增殖分馏塔
            //可输入任意物品，每次分馏成功消耗1个原料，输出2个输入的物品。
            //分馏成功率受物品种类、是否喷涂增产剂影响。
            //基础分馏成功率：普通物品0.2%，普通矿物2%，珍奇矿物1%。
            //增产剂成功率加成：MK1 25%，MK2 50%，MK3 100%（每个增产点数加25%）
            AddFractionator(I增殖分馏塔, "增殖分馏塔",
                [I钛合金, I石材, I钛化玻璃, I量子芯片], [8, 8, 4, 1],
                2604, T宇宙矩阵, 410, M增殖分馏塔,
                Color.HSVToRGB(0.1404f, 0.8294f, 0.9882f), 4.0);
        }

        /// <summary>
        /// 添加一个分馏塔，以及制作它的配方。
        /// </summary>
        /// <param name="buildingID">要添加的分馏塔的id</param>
        /// <param name="name">分馏塔名称，用于名称显示、描述显示</param>
        /// <param name="items">制作分馏塔需要的材料种类</param>
        /// <param name="itemCounts">制作分馏塔需要的材料个数</param>
        /// <param name="gridIndex">分馏塔在背包显示的位置（配方位置）、物流塔选择物品位置（物品位置）</param>
        /// <param name="preTech">建筑和配方的前置科技</param>
        /// <param name="buildIndex">在下方快捷制作栏的哪个位置</param>
        /// <param name="modelID">模型ID</param>
        /// <param name="color">建筑颜色</param>
        /// <param name="energyRatio">能耗比例（相比于原版分馏塔）</param>
        private void AddFractionator(int buildingID, string name, int[] items, int[] itemCounts, int gridIndex, int preTech,
            int buildIndex, int modelID, Color? color = null, double energyRatio = 1.0)
        {
            ItemProto oriItem = LDB.items.Select(I分馏塔);
            ModelProto oriModel = LDB.models.Select(M分馏塔);
            RecipeProto oriRecipe = oriItem.maincraft;
            Sprite sprite = oriItem.iconSprite;

            int recipeID = nextRecipeID++;
            RecipeProto recipe = new();
            oriRecipe.CopyPropsTo(ref recipe);
            recipe.Name = name;
            recipe.name = name.Translate();
            recipe.Items = items;
            recipe.ItemCounts = itemCounts;
            recipe.Results = [buildingID];
            recipe.ResultCounts = [1];
            recipe.GridIndex = gridIndex;
            recipe.preTech = LDB.techs.Select(preTech);
            //Traverse.Create(recipe).Field("_iconSprite").SetValue(sprite);
            LDBTool.PreAddProto(recipe);
            recipe.ID = recipeID;

            ItemProto item = new();
            oriItem.CopyPropsTo(ref item);
            item.prefabDesc = new();
            oriItem.prefabDesc.CopyPropsTo(ref item.prefabDesc);
            item.ID = buildingID;
            item.Name = name;
            item.name = name.Translate();
            item.Description = "I" + name;
            item.description = ("I" + name).Translate();
            item.GridIndex = gridIndex;
            item.preTech = recipe.preTech;
            item.recipes = [recipe];
            item.maincraft = recipe;
            item.handcraft = recipe;
            item.handcrafts = [recipe];
            item.BuildIndex = buildIndex;
            item.prefabDesc.workEnergyPerTick = (long)(item.prefabDesc.workEnergyPerTick * energyRatio);
            item.prefabDesc.idleEnergyPerTick = (long)(item.prefabDesc.idleEnergyPerTick * energyRatio);
            //Traverse.Create(item).Field("_iconSprite").SetValue(sprite);
            LDBTool.PreAddProto(item);

            var model = CopyModelUtils.CopyModelProto(M分馏塔, modelID, buildingID, buildIndex,
                name, color);
            model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
            model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);

            // ModelProto model = new();
            // oriModel.CopyPropsTo(ref model);
            // model.prefabDesc = new();
            // oriModel.prefabDesc.CopyPropsTo(ref model.prefabDesc);
            // model.ID = modelID;
            // model.Name = modelID.ToString();
            // model.name = modelID.ToString();
            // model.sid = name;
            // model.SID = name;
            // model.prefabDesc.modelIndex = modelID;
            // model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
            // model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
            // if (color.HasValue)
            // {
            //     foreach (Material[] lodMaterial in model.prefabDesc.lodMaterials)
            //     {
            //         if (lodMaterial == null) continue;
            //         for (var j = 0; j < lodMaterial.Length; j++)
            //         {
            //
            //             // ref Material material = ref lodMaterial[j];
            //             // if (material == null) continue;
            //             // material = new Material(material);
            //
            //             if (lodMaterial[j] == null) continue;
            //             Material material = new Material(lodMaterial[j]);
            //             lodMaterial[j] = material;
            //
            //             material.SetColor("_Color", color.Value);
            //         }
            //     }
            // }
            // LDBTool.PreAddProto(model);

            item.ModelIndex = modelID;
            LDBTool.SetBuildBar(buildIndex / 100, buildIndex % 100, item.ID);
        }

        //乐，虽然是邪教，但是确实管用
        //代码源于SmelterMiner-jinxOAO

        #region 邪教修改建筑耗电

        //下面两个prefix+postfix联合作用。由于新版游戏实际执行的能量消耗、采集速率等属性都使用映射到的modelProto的prefabDesc中的数值，而不是itemProto的PrefabDesc，而修改/新增modelProto我还不会改，会报错（貌似是和模型读取不到有关）
        //因此，提前修改设定建筑信息时读取的PrefabDesc的信息，在存储建筑属性前先修改一下（改成itemProto的PrefabDesc中对应的某些值），建造建筑设定完成后再改回去
        //并且，原始item和model执向的貌似是同一个PrefabDesc，所以不能直接改model的，然后再还原成oriItem的prefabDesc，因为改了model的oriItem的也变了，还原不回去了。所以得Copy一个出来改。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetFactory), "AddEntityDataWithComponents")]
        public static bool AddEntityDataPrePatch(EntityData entity, out PrefabDesc __state)
        {
            int gmProtoId = entity.protoId;
            if (gmProtoId != I分馏塔 && gmProtoId != I低功率分馏塔 && gmProtoId != I建筑极速分馏塔 && gmProtoId != I增殖分馏塔)
            {
                __state = null;
                return true;//不相关建筑直接返回
            }
            ItemProto itemProto = LDB.items.Select((int)entity.protoId);
            if (itemProto == null || !itemProto.IsEntity)
            {
                __state = null;
                return true;
            }

            ModelProto modelProto = LDB.models.Select((int)entity.modelIndex);
            __state = modelProto.prefabDesc;
            modelProto.prefabDesc = __state.Copy();
            modelProto.prefabDesc.workEnergyPerTick = itemProto.prefabDesc.workEnergyPerTick;
            modelProto.prefabDesc.idleEnergyPerTick = itemProto.prefabDesc.idleEnergyPerTick;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetFactory), "AddEntityDataWithComponents")]
        public static void AddEntityDataPostPatch(EntityData entity, PrefabDesc __state)
        {
            if (__state == null)
            {
                return;
            }
            int gmProtoId = entity.protoId;
            if (gmProtoId != I分馏塔 && gmProtoId != I低功率分馏塔 && gmProtoId != I建筑极速分馏塔 && gmProtoId != I增殖分馏塔)
            {
                return;//不相关
            }
            ModelProto modelProto = LDB.models.Select((int)entity.modelIndex);
            modelProto.prefabDesc = __state;//还原
        }

        #endregion

        private void AddFracRecipes()
        {
#if DEBUG
            if (File.Exists(SPRITE_CSV_PATH))
            {
                File.Delete(SPRITE_CSV_PATH);
            }
#endif
            //添加重氢分馏
            fracRecipeDic.Add(I氢, new FracRecipe(LDB.recipes.Select(115), new() { { 1, 0.01 } }));
            //添加分馏配方。每个分馏配方只能有一种输入和一种输出；不同分馏配方原料必须唯一，产物可以相同。
            //建筑I
            AddCycleFracChain(I电力感应塔, I无线输电塔, I卫星配电站);
            AddCycleFracChain(I风力涡轮机, I太阳能板, I蓄电器, I蓄电器满, I能量枢纽);
            AddCycleFracChain(I火力发电厂, I地热发电站, I微型聚变发电站, I人造恒星);
            //建筑II
            AddCycleFracChain(I传送带, I高速传送带, I极速传送带);
            AddCycleFracChain(I流速监测器, I四向分流器, I喷涂机, I自动集装机);//注意科技解锁顺序
            AddCycleFracChain(I小型储物仓, I储液罐, I大型储物仓);//注意科技解锁顺序
            AddCycleFracChain(I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器);
            //建筑III
            AddCycleFracChain(I分拣器, I高速分拣器, I极速分拣器, I集装分拣器);
            AddCycleFracChain(I采矿机, I大型采矿机);
            AddCycleFracChain(I抽水站, I原油萃取站, I原油精炼厂);
            AddCycleFracChain(I化工厂, I量子化工厂);
            //建筑IV
            AddCycleFracChain(I电弧熔炉, I位面熔炉, I负熵熔炉);
            AddCycleFracChain(I制造台MkI, I制造台MkII, I制造台MkIII, I重组式制造台);
            AddCycleFracChain(I矩阵研究站, I自演化研究站);
            AddCycleFracChain(I电磁轨道弹射器, I射线接收站, I垂直发射井);
            //建筑V
            AddCycleFracChain(I高斯机枪塔, I导弹防御塔, I聚爆加农炮);//注意科技解锁顺序
            AddCycleFracChain(I高频激光塔, I磁化电浆炮, I近程电浆塔);//注意科技解锁顺序
            AddCycleFracChain(I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器);//注意科技解锁顺序
            //建筑VI
            AddCycleFracChain(I低功率分馏塔, I建筑极速分馏塔, I分馏塔, I增殖分馏塔);
            //物品左侧区域
            //矿物自增值比冶炼更有意义
            AddFracRecipe(I铁矿, I铁矿, new() { { 2, 0.005 } });
            AddFracRecipe(I铜矿, I铜矿, new() { { 2, 0.005 } });
            AddFracRecipe(I硅石, I硅石, new() { { 2, 0.005 } });
            AddFracRecipe(I钛石, I钛石, new() { { 2, 0.005 } });
            AddFracRecipe(I石矿, I石矿, new() { { 2, 0.005 } });
            AddFracRecipe(I煤矿, I煤矿, new() { { 2, 0.005 } });
            AddFracRecipe(I可燃冰, I可燃冰, new() { { 2, 0.0025 } });
            AddFracRecipe(I金伯利矿石, I金伯利矿石, new() { { 2, 0.0025 } });
            AddFracRecipe(I分形硅石, I分形硅石, new() { { 2, 0.0025 } });
            AddFracRecipe(I光栅石, I光栅石, new() { { 2, 0.0025 } });
            AddFracRecipe(I刺笋结晶, I刺笋结晶, new() { { 2, 0.0025 } });
            AddFracRecipe(I单极磁石, I单极磁石, new() { { 2, 0.0025 } });
            AddFracRecipe(I有机晶体, I有机晶体, new() { { 2, 0.0025 } });
            //部分非循环链
            AddFracChain(I铁块, I钢材, I钛合金, I框架材料, I戴森球组件, I小型运载火箭);
            AddFracChain(I高纯硅块, I晶格硅);
            AddFracChain(I石材, I地基);
            AddFracChain(I玻璃, I钛化玻璃, I位面过滤器);
            AddFracChain(I棱镜, I电浆激发器, I光子合并器, I太阳帆);
            AddFracChain(I高能石墨, I金刚石);
            AddFracChain(I石墨烯, I碳纳米管, I粒子宽带);
            AddFracChain(I粒子容器, I奇异物质, I引力透镜, I空间翘曲器);
            AddFracChain(I精炼油, I塑料, I有机晶体);
            AddFracChain(I钛晶石, I卡西米尔晶体);
            //部分循环链
            AddCycleFracChain(I水, I硫酸);
            AddFracChain(I磁铁, I磁线圈, I电动机);
            AddCycleFracChain(I电动机, I电磁涡轮, I超级磁场环);
            AddCycleFracChain(I电路板, I处理器, I量子芯片);
            AddCycleFracChain(I原型机, I精准无人机, I攻击无人机);
            AddCycleFracChain(I护卫舰, I驱逐舰);
            AddFracChain(I能量碎片, I硅基神经元, I物质重组器, I负熵奇点, I核心素, I黑雾矩阵);
            AddCycleFracChain(I黑雾矩阵, I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵);
            AddFracRecipe(I宇宙矩阵, I宇宙矩阵, new() { { 2, 0.0015 }, { 4, 0.001 }, { 8, 0.0005 } });
            AddFracRecipe(I临界光子, I反物质);
            AddFracRecipe(I反物质, I临界光子, new() { { 2, 0.01 } });
            //物品右侧区域
            AddCycleFracChain(I增产剂MkI, I增产剂MkII, I增产剂MkIII);
            AddCycleFracChain(I燃烧单元, I爆破单元, I晶石爆破单元);
            AddCycleFracChain(I动力引擎, I推进器, I加力推进器);
            AddCycleFracChain(I配送运输机, I物流运输机, I星际物流运输船);
            AddCycleFracChain(I液氢燃料棒, I氘核燃料棒, I反物质燃料棒, I奇异湮灭燃料棒);
            AddCycleFracChain(I机枪弹箱, I钛化弹箱, I超合金弹箱);
            AddCycleFracChain(I炮弹组, I高爆炮弹组, I晶石炮弹组);
            AddCycleFracChain(I导弹组, I超音速导弹组, I引力导弹组);
            AddCycleFracChain(I等离子胶囊, I反物质胶囊);
            AddCycleFracChain(I干扰胶囊, I压制胶囊);
        }

        /// <summary>
        /// 添加一个分馏链。
        /// 链尾物品会分馏为第1个物品，前置科技使用链尾物品的前置科技。
        /// </summary>
        private void AddCycleFracChain(params int[] itemChain)
        {
            AddFracChain([.. itemChain, itemChain[0]], true);
        }

        /// <summary>
        /// 添加一个分馏链。
        /// 链尾物品不会分馏，可在其他链添加链尾物品的分馏配方。
        /// </summary>
        private void AddFracChain(params int[] itemChain)
        {
            AddFracChain(itemChain, false);
        }

        /// <summary>
        /// 添加一个分馏链。
        /// 第i个物品分馏出第i+1个物品，前置科技为第i+1个物品的前置科技；
        /// 链尾物品会分馏为第1个物品，前置科技根据lastUseInputTech选择链尾或第1个物品的前置科技。
        /// </summary>
        private void AddFracChain(int[] itemChain, bool lastUseInputTech)
        {
            for (int i = 0; i < itemChain.Length - 1; i++)
            {
                AddFracRecipe(itemChain[i], itemChain[i + 1], new() { { 1, 0.01 } }, lastUseInputTech && i == itemChain.Length - 2);
            }
        }

        /// <summary>
        /// 添加一个分馏配方，前置科技使用产物的前置科技。
        /// </summary>
        /// <param name="inputItemID"></param>
        /// <param name="outputItemID"></param>
        private void AddFracRecipe(int inputItemID, int outputItemID)
        {
            AddFracRecipe(inputItemID, outputItemID, new() { { 1, 0.01 } }, false);
        }

        /// <summary>
        /// 添加一个分馏配方。
        /// </summary>
        /// <param name="inputItemID">分馏原料</param>
        /// <param name="outputItemID">分馏产物</param>
        /// <param name="fracNumRatioDic">分馏产物的数目与概率</param>
        /// <param name="useInputTech">如果为true，表示前置科技使用原料的前置科技；否则前置科技使用产物的前置科技</param>
        private void AddFracRecipe(int inputItemID, int outputItemID, Dictionary<int, double> fracNumRatioDic, bool useInputTech = false)
        {
            //LDB.ItemName 等价于 itemproto.name，itemproto.name 等价于 itemproto.Name.Translate()
            //name: 推进器  name.Translate: <0xa0>-<0xa0>推进器  Name: 推进器2  Name.Translate: 推进器
            //name: Thruster  name.Translate: Thruster  Name: 推进器2  Name.Translate: Thruster
            //name: 制造台<0xa0>Mk.I  name.Translate: 制造台<0xa0>Mk.I  Name: 制造台 Mk.I  Name.Translate: 制造台<0xa0>Mk.I
            //name: Assembling Machine Mk.I  name.Translate: Assembling Machine Mk.I  Name: 制造台 Mk.I  Name.Translate: Assembling Machine Mk.I
            try
            {
                int recipeID = nextRecipeID++;
                ItemProto inputItem = LDB.items.Select(inputItemID);
                ItemProto outputItem = LDB.items.Select(outputItemID);
                string recipeName = outputItem.name + "分馏".Translate();
                //获取前置科技
                TechProto preTech = inputItemID switch
                {
                    I铁矿 => LDB.techs.Select(T戴森球计划),
                    I铜矿 => LDB.techs.Select(T戴森球计划),
                    I硅石 => LDB.techs.Select(T冶炼提纯),
                    I钛石 => LDB.techs.Select(T钛矿冶炼),
                    I石矿 => LDB.techs.Select(T戴森球计划),
                    I煤矿 => LDB.techs.Select(T戴森球计划),
                    I可燃冰 => LDB.techs.Select(T应用型超导体),
                    I金伯利矿石 => LDB.techs.Select(T晶体冶炼),
                    I分形硅石 => LDB.techs.Select(T粒子可控),
                    I光栅石 => LDB.techs.Select(T卡西米尔晶体),
                    I刺笋结晶 => LDB.techs.Select(T高强度材料),
                    I单极磁石 => LDB.techs.Select(T粒子磁力阱),
                    I有机晶体 => LDB.techs.Select(T高分子化工),
                    I硫酸 => LDB.techs.Select(T基础化工),
                    I磁铁 => LDB.techs.Select(T戴森球计划),
                    I黑雾矩阵 => LDB.techs.Select(T电磁矩阵),
                    I能量碎片 => LDB.techs.Select(T戴森球计划),
                    I硅基神经元 => LDB.techs.Select(T戴森球计划),
                    I物质重组器 => LDB.techs.Select(T戴森球计划),
                    I负熵奇点 => LDB.techs.Select(T戴森球计划),
                    I核心素 => LDB.techs.Select(T戴森球计划),
                    I蓄电器 => LDB.techs.Select(T能量储存),
                    _ => usePreTech
                        ? useInputTech ? inputItem.preTech : outputItem.preTech
                        : null,
                };
                //前置科技如果为null，【必须】修改为戴森球计划，才能确保某些配方能正常解锁、显示
                if (preTech == null)
                {
                    preTech = LDB.techs.Select(T戴森球计划);
                    logger.LogWarning($"配方{recipeName}前置科技为null，调整为戴森球计划！");
                }
                //调整部分配方的显示位置，包括产物无配方能生成产物、显示位置重合、分馏循环链影响的情况
                int gridIndex = 0;
                if (showRecipes)
                {
                    gridIndex = inputItemID switch
                    {
                        I铁矿 => 1101,
                        I铜矿 => 1102,
                        I硅石 => 1103,
                        I钛石 => 1104,
                        I石矿 => 1105,
                        I煤矿 => 1106,
                        I可燃冰 => 1208,
                        I金伯利矿石 => 1306,
                        I分形硅石 => 1303,
                        I光栅石 => 1605,
                        I刺笋结晶 => 1508,
                        I单极磁石 => 1606,
                        I有机晶体 => 1309,
                        I硫酸 => 1407,
                        I磁铁 => 1201,
                        I磁线圈 => 1202,
                        I临界光子 => 1707,
                        I反物质 => 1708,
                        I黑雾矩阵 => 1801,
                        I引力矩阵 => 1806,
                        I宇宙矩阵 => 1807,
                        I能量碎片 => 1808,
                        I硅基神经元 => 1809,
                        I物质重组器 => 1810,
                        I负熵奇点 => 1811,
                        I核心素 => 1812,
                        I蓄电器 => 2113,
                        I低功率分馏塔 => 2601,
                        I建筑极速分馏塔 => 2602,
                        I分馏塔 => 2603,
                        I增殖分馏塔 => 2604,
                        _ => outputItem.recipes.Count == 0
                            ? 0
                            : outputItem.recipes[0].GridIndex,
                    };
                    gridIndex += (firstPage - 1) * 1000;
                    if (gridIndex == (firstPage - 1) * 1000 || gridIndexList.Contains(gridIndex))
                    {
                        logger.LogWarning($"配方{recipeName}显示位置{gridIndex}已被占用，调整至{currLastIdx}！");
                        gridIndex = currLastIdx++;
                    }
                    gridIndexList.Add(gridIndex);
                }
                //获取重氢分馏类似样式的图标。图标由python拼接，由unity打包
                Sprite sprite = null;
                string inputIconName = "";
                string outputIconName = "";
                if (!string.IsNullOrEmpty(inputItem.IconPath) && !string.IsNullOrEmpty(outputItem.IconPath))
                {
                    inputIconName = inputItem.IconPath.Substring(inputItem.IconPath.LastIndexOf("/") + 1);
                    outputIconName = outputItem.IconPath.Substring(outputItem.IconPath.LastIndexOf("/") + 1);
                    //“原料-产物”这样的名称可以避免冲突
                    sprite = ab.LoadAsset<Sprite>(inputIconName + "-" + outputIconName + "-formula");
                }
                if (sprite == null)
                {
                    sprite = outputItem.iconSprite;
                    inputIconName = inputItem.iconSprite.name;
                    outputIconName = outputItem.iconSprite.name;
                    logger.LogWarning($"缺失配方{recipeName}的图标，使用产物{outputItem.Name}图标代替！");
                }
                //配方中的ResultCounts[0]大于1时，仅影响分馏成功率与显示上的分馏产物数目，实际并不能分出多个；
                //实际分馏出多个是通过FractionatorInternalUpdatePatch方法达成的
                //根据fracNumRatioDic的内容，构建配方的description
                string description = $"{"从".Translate()}{inputItem.name}{"中分馏出".Translate()}{outputItem.name}{"。".Translate()}";
                foreach (var p in fracNumRatioDic)
                {
                    description += $"\n{p.Value:P3}{"分馏出".Translate()}{p.Key}{"个产物".Translate()}";
                }
                //ProtoRegistry.RegisterRecipe用起来有很多问题，自己创建不容易出bug
                RecipeProto r = new()
                {
                    Type = ERecipeType.Fractionate,
                    Handcraft = false,
                    Explicit = true,
                    TimeSpend = 60,
                    Items = [inputItemID],
                    ItemCounts = [100],
                    Results = [outputItemID],
                    ResultCounts = [1],
                    Description = "R" + inputItem.Name + "分馏",
                    description = description,
                    GridIndex = gridIndex,
                    Name = inputItem.Name + "分馏",
                    name = recipeName,
                    preTech = preTech,
                    ID = recipeID
                };
                Traverse.Create(r).Field("_iconSprite").SetValue(sprite);
                LDBTool.PostAddProto(r);
                //add之后要再次设定ID，不然id会莫名其妙变化。不知道这个bug怎么回事，反正这样就正常了。
                r.ID = recipeID;
                //为基础配方添加这个公式的显示
                outputItem.recipes.Add(r);
                //所有配方以及配方的概率详情存起来，为FractionatorInternalUpdatePatch提供分馏个产物的数据支持
                fracRecipeDic.Add(inputItemID, new FracRecipe(r, fracNumRatioDic));
#if DEBUG
                //logger.LogDebug(
                //    $"\nID{r.ID} index{r.index} {outputItem.name + "分馏".Translate()}\n" +
                //    $"Handcraft:{r.Handcraft} Explicit:{r.Handcraft} GridIndex:{r.GridIndex}\n" +
                //    $"hasIcon:{r.hasIcon} IconPath:{r.IconPath} iconSprite:{r.iconSprite}\n" +
                //    $"preTech:{r.preTech} preTech.ID:{r.preTech?.ID}\n" +
                //    $"NonProductive:{r.NonProductive} productive:{r.productive}\n");
                //输出分馏配方需要的图标的路径，以便于制作图标
                if (Directory.Exists(SPRITE_CSV_PATH.Substring(0, SPRITE_CSV_PATH.LastIndexOf('\\'))))
                {
                    using StreamWriter sw = new(SPRITE_CSV_PATH, true);
                    sw.WriteLine(inputIconName + "," + outputIconName);
                }
#endif
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FractionatorComponent), "InternalUpdate")]
        public static bool FractionatorInternalUpdatePatch(ref FractionatorComponent __instance, PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister, ref uint __result)
        {
            if (power < 0.1f)
            {
                __result = 0u;
                return false;
            }

            double num = 1.0;
            if (__instance.fluidInputCount == 0)
            {
                __instance.fluidInputCargoCount = 0f;
            }
            else
            {
                num = (((double)__instance.fluidInputCargoCount > 0.0001) ? ((float)__instance.fluidInputCount / __instance.fluidInputCargoCount) : 4f);
            }

            if (__instance.fluidInputCount > 0 && __instance.productOutputCount < __instance.productOutputMax && __instance.fluidOutputCount < __instance.fluidOutputMax)
            {
                int num2 = (int)((double)power * 166.66666666666666 * (double)((__instance.fluidInputCargoCount < 30f) ? __instance.fluidInputCargoCount : 30f) * num + 0.75);
                __instance.progress += num2;
                if (__instance.progress > 100000)
                {
                    __instance.progress = 100000;
                }

                while (__instance.progress >= 10000)
                {
                    //不同分馏配方的产物可能相同，但原料一定不同，故__instance.fluidId可以找到唯一对应的配方
                    //num3是平均增产点数
                    int num3 = ((__instance.fluidInputInc > 0 && __instance.fluidInputCount > 0) ? (__instance.fluidInputInc / __instance.fluidInputCount) : 0);
                    __instance.seed = (uint)((int)((ulong)((long)(__instance.seed % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);

                    double randomVal = (double)__instance.seed / 2147483646.0;
                    if (factory.entityPool[__instance.entityId].protoId != I增殖分馏塔)
                    {
                        //如果不是增殖分馏塔，增产剂可以提升分馏成功率
                        randomVal /= 1.0 + Cargo.accTableMilli[(num3 < 10) ? num3 : 10];
                    }
                    if (factory.entityPool[__instance.entityId].protoId == I低功率分馏塔)
                    {
                        randomVal *= 3;
                    }
                    if (factory.entityPool[__instance.entityId].protoId == I建筑极速分馏塔)
                    {
                        var item = LDB.items.Select(__instance.fluidId);
                        //BuildMode0-5都有，0是不可放置的物品
                        randomVal = item.BuildMode == 0 ? randomVal * 10 : randomVal / 12.5;
                    }
                    int outputNum = fracRecipeDic[__instance.fluidId].GetOutputNum(randomVal);
                    if (factory.entityPool[__instance.entityId].protoId == I增殖分馏塔)
                    {
                        //增殖分馏塔的增产剂提升产物数目
                        seed2 = (uint)((int)((ulong)((long)(seed2 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                        bool outputDouble = (double)seed2 / 2147483646.0 < 0.01 * (1.0 + Cargo.incTableMilli[(num3 < 10) ? num3 : 10]);
                        if (outputDouble)
                        {
                            outputNum *= 2;
                        }
                    }

                    __instance.fractionSuccess = outputNum > 0;
                    if (__instance.fractionSuccess)
                    {
                        __instance.productOutputCount += outputNum;
                        __instance.productOutputTotal += outputNum;
                        lock (productRegister)
                        {
                            productRegister[__instance.productId] += outputNum;
                        }
                        lock (consumeRegister)
                        {
                            consumeRegister[__instance.fluidId]++;
                        }
                    }
                    else
                    {
                        __instance.fluidOutputCount++;
                        __instance.fluidOutputTotal++;
                        __instance.fluidOutputInc += num3;
                    }

                    __instance.fluidInputCount--;
                    __instance.fluidInputInc -= num3;
                    __instance.fluidInputCargoCount -= (float)(1.0 / num);
                    if (__instance.fluidInputCargoCount < 0f)
                    {
                        __instance.fluidInputCargoCount = 0f;
                    }

                    __instance.progress -= 10000;
                }
            }
            else
            {
                __instance.fractionSuccess = false;
            }

            CargoTraffic cargoTraffic = factory.cargoTraffic;
            byte stack;
            byte inc;
            if (__instance.belt1 > 0)
            {
                if (__instance.isOutput1)
                {
                    if (__instance.fluidOutputCount > 0)
                    {
                        int num4 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                        if (cargoPath != null && cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num4))
                        {
                            __instance.fluidOutputCount--;
                            __instance.fluidOutputInc -= num4;
                            if (__instance.fluidOutputCount > 0)
                            {
                                num4 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num4))
                                {
                                    __instance.fluidOutputCount--;
                                    __instance.fluidOutputInc -= num4;
                                }
                            }
                        }
                    }
                }
                else if (!__instance.isOutput1 && __instance.fluidInputCargoCount < (float)__instance.fluidInputMax)
                {
                    if (__instance.fluidId > 0)
                    {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt1, __instance.fluidId, null, out stack, out inc) > 0)
                        {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                        }
                    }
                    else
                    {
                        int num5 = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, RecipeProto.fractionatorNeeds, out stack, out inc);
                        if (num5 > 0)
                        {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                            __instance.SetRecipe(num5, signPool);
                        }
                    }
                }
            }

            if (__instance.belt2 > 0)
            {
                if (__instance.isOutput2)
                {
                    if (__instance.fluidOutputCount > 0)
                    {
                        int num6 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        CargoPath cargoPath2 = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                        if (cargoPath2 != null && cargoPath2.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num6))
                        {
                            __instance.fluidOutputCount--;
                            __instance.fluidOutputInc -= num6;
                            if (__instance.fluidOutputCount > 0)
                            {
                                num6 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                if (cargoPath2.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num6))
                                {
                                    __instance.fluidOutputCount--;
                                    __instance.fluidOutputInc -= num6;
                                }
                            }
                        }
                    }
                }
                else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < (float)__instance.fluidInputMax)
                {
                    if (__instance.fluidId > 0)
                    {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt2, __instance.fluidId, null, out stack, out inc) > 0)
                        {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                        }
                    }
                    else
                    {
                        int num7 = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, RecipeProto.fractionatorNeeds, out stack, out inc);
                        if (num7 > 0)
                        {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                            __instance.SetRecipe(num7, signPool);
                        }
                    }
                }
            }

            if (__instance.belt0 > 0 && __instance.isOutput0 && __instance.productOutputCount > 0 && cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId, 1, 0))
            {
                __instance.productOutputCount--;
            }

            if (__instance.fluidInputCount == 0 && __instance.fluidOutputCount == 0 && __instance.productOutputCount == 0)
            {
                __instance.fluidId = 0;
            }

            __instance.isWorking = __instance.fluidInputCount > 0 && __instance.productOutputCount < __instance.productOutputMax && __instance.fluidOutputCount < __instance.fluidOutputMax;
            if (!__instance.isWorking)
            {
                __result = 0u;
                return false;
            }

            __result = 1u;
            return false;
        }
    }
}
