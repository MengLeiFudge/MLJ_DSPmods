using FractionateEverything.Compatibility;
using FractionateEverything.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using FractionateEverything.Compatibility;
using FractionateEverything.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.FractionateEverything;
using static FractionateEverything.Compatibility.GenesisBook;
using static FractionateEverything.Compatibility.MoreMegaStructure;
using static FractionateEverything.Utils.AddProtoUtils;
using static FractionateEverything.Utils.TranslationUtils;

namespace FractionateEverything.Main {
    public enum FracRecipeType {
        Origin,//仅用于原版分馏塔
        NaturalResource,
        Upgrade,
        DownGrade,
        PointsAggregate,
        Increase,
    }

    /// <summary>
    /// 配方基类
    /// </summary>
    public class FracRecipe {
        #region 构造与基础数据变动

        public readonly FracRecipeType type;
        public readonly int inputID;
        /// <summary>
        /// 可能的输出列表。损毁
        /// </summary>
        public readonly List<int> outputID;
        public readonly List<float> outputRatio;
        public readonly List<int> outputNum;

        /// <summary>
        /// 配方基类
        /// </summary>
        public FracRecipe(FracRecipeType type, int inputID,
            List<int> outputID, List<float> outputRatio, List<int> outputNum) {
            this.type = type;
            this.inputID = inputID;
            this.outputID = outputID;
            this.outputRatio = outputRatio;
            this.outputNum = outputNum;
        }

        /// <summary>
        /// 允许该配方新增一个目标产物。通常用于创建配方，某些特殊加成导致配方改变时也会使用。
        /// 注：如果移除目标产物，应对应概率改为0，而非移除。
        /// </summary>
        public FracRecipe AddFrac(int id, float ratio, int num) {
            outputID.Add(id);
            outputRatio.Add(ratio);
            outputNum.Add(num);
            return this;
        }

        #endregion

        #region 解锁与升级

        /// <summary>
        /// 解锁该配方需要的物品数目。
        /// </summary>
        public int[] unlockItemID;
        /// <summary>
        /// 解锁该配方需要的物品ID。
        /// </summary>
        public int[] unlockItemNum;
        public bool unlocked = false;

        //todo: 这个升级的结构到底怎么写。。。
        public List<float> outputRatioFix;
        public List<int> outputNumFix;

        #endregion

        #region 数据读写（主要存储解锁与升级的数据）

        public void Load() { }

        public void Save() { }

        #endregion
    }

    public static class FracRecipeManager {
        public static FracRecipe DeuteriumFracRecipe =
            new(FracRecipeType.Origin, I氢, [I重氢], [0.01f], [1]) { unlocked = true };
        private static List<FracRecipe> naturalResourceRecipeList = [];
        private static List<FracRecipe> upgradeRecipeList = [];
        private static List<FracRecipe> downgradeRecipeList = [];
        public static FracRecipe PointsAggregateRecipe =
            new(FracRecipeType.PointsAggregate, 1, [1], [0.01f], [1]) { unlocked = true };
        private static List<FracRecipe> increaseRecipeList = [];

        public static FracRecipe GetRecipe1(int inputID) {
            foreach (FracRecipe r in naturalResourceRecipeList) {
                if (r.inputID == inputID) {
                    return r;
                }
            }
            return null;
        }

        public static FracRecipe GetRecipe2(int inputID) {
            foreach (FracRecipe r in upgradeRecipeList) {
                if (r.inputID == inputID) {
                    return r;
                }
            }
            return null;
        }

        public static FracRecipe GetRecipe3(int inputID) {
            foreach (FracRecipe r in downgradeRecipeList) {
                if (r.inputID == inputID) {
                    return r;
                }
            }
            return null;
        }

        public static FracRecipe GetRecipe4(int inputID) {
            foreach (FracRecipe r in increaseRecipeList) {
                if (r.inputID == inputID) {
                    return r;
                }
            }
            return null;
        }


        #region 计算物品价值

        #endregion

        #region 创建分馏配方

#if DEBUG
        private const string SPRITE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
        private const string SPRITE_CSV_PATH = $@"{SPRITE_CSV_DIR}\fracIconPath.csv";
#endif


        public static void AddFracRecipes() {
#if DEBUG
            if (File.Exists(SPRITE_CSV_PATH)) {
                File.Delete(SPRITE_CSV_PATH);
            }
#endif

            LogInfo("Begin to add fractionate recipes...");

            if (!GenesisBook.Enable) {
                //自然资源复制
                //采集10个对应物品，或者向老虎机投入10个对应物品，即可解锁自然资源复制配方
                //使用自然资源分馏塔成功分馏出指定数目的物品之后，配方可以消耗资源来升级
                //todo：能不能判定采集了多少个资源
                CreateRecipe1(I铁矿, 0.05f);
                CreateRecipe1(I铜矿, 0.05f);
                CreateRecipe1(I硅石, 0.05f).AddFrac(I分形硅石, 0.01f, 1);
                CreateRecipe1(I钛石, 0.05f);
                //石矿有概率分馏出硅石、钛石
                CreateRecipe1(I石矿, 0.05f).AddFrac(I硅石, 0.01f, 1).AddFrac(I钛石, 0.01f, 1);
                CreateRecipe1(I煤矿, 0.05f);
                CreateRecipe1(I水, 0.05f);
                CreateRecipe1(I原油, 0.05f);
                CreateRecipe1(I硫酸, 0.05f);
                CreateRecipe1(I氢, 0.05f).AddFrac(I重氢, 0.01f, 1);
                CreateRecipe1(I重氢, 0.05f);
                CreateRecipe1(I可燃冰, 0.025f);
                CreateRecipe1(I金伯利矿石, 0.025f);
                CreateRecipe1(I分形硅石, 0.025f);
                CreateRecipe1(I光栅石, 0.025f);
                CreateRecipe1(I刺笋结晶, 0.025f);
                CreateRecipe1(I单极磁石, 0.01f);
                CreateRecipe1(I有机晶体, 0.025f);
                CreateRecipe1(I临界光子, 0.01f);
                //升降级分馏链
                //分为消耗品、材料、建筑三种
                //消耗品指燃料棒、弹药、增产剂
                //材料指电路板、处理器等
                //建筑指能放置的物品
                CreateFracChain([I原型机, I精准无人机, I攻击无人机]);
                if (!TheyComeFromVoid.Enable) {
                    CreateFracChain([I护卫舰, I驱逐舰]);
                } else {
                    CreateFracChain([I护卫舰, I驱逐舰, IVD水滴]);
                }
                CreateFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素]);
                CreateFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵]);
                CreateFracChain([I增产剂MkI, I增产剂MkII, I增产剂MkIII_GB增产剂]);
                if (!MoreMegaStructure.Enable) {
                    CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器]);
                } else {
                    CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器]);
                }
                CreateFracChain([I配送运输机, I物流运输机, I星际物流运输船]);
                CreateFracChain([I液氢燃料棒, I氘核燃料棒, I反物质燃料棒, I奇异湮灭燃料棒]);
                CreateFracChain([I机枪弹箱, I钛化弹箱, I超合金弹箱]);
                CreateFracChain([I炮弹组, I高爆炮弹组, I晶石炮弹组]);
                CreateFracChain([I导弹组, I超音速导弹组, I引力导弹组]);
                CreateFracChain([I等离子胶囊, I反物质胶囊]);
                CreateFracChain([I干扰胶囊, I压制胶囊]);

                //建筑I
                CreateFracChain([I电力感应塔, I无线输电塔, I卫星配电站]);
                CreateFracChain([I风力涡轮机, I太阳能板, I蓄电器, I蓄电器满, I能量枢纽]);
                CreateFracChain([I火力发电厂_GB燃料电池发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB朱曦K型人造恒星]);
                //建筑II
                CreateFracChain([I传送带, I高速传送带, I极速传送带]);
                CreateFracChain([I流速监测器, I四向分流器, I喷涂机, I自动集装机]);//注意科技解锁顺序
                CreateFracChain([I小型储物仓, I储液罐, I大型储物仓]);//注意科技解锁顺序
                //建筑III
                CreateFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器]);
                CreateFracChain([I采矿机, I大型采矿机]);
                CreateFracChain([I抽水站, I原油萃取站, I原油精炼厂]);
                CreateFracChain([I化工厂, I量子化工厂_GB先进化学反应釜]);
                CreateFracChain([I分馏塔, I微型粒子对撞机]);
                //建筑IV
                CreateFracChain([I电弧熔炉, I位面熔炉, I负熵熔炉]);
                CreateFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂]);
                CreateFracChain([I矩阵研究站, I自演化研究站]);
                CreateFracChain([I电磁轨道弹射器, I射线接收站_MS射线重构站, I垂直发射井]);
                //建筑V
                CreateFracChain([I高斯机枪塔, I导弹防御塔, I聚爆加农炮]);//注意科技解锁顺序
                CreateFracChain([I高频激光塔, I磁化电浆炮, I近程电浆塔]);//注意科技解锁顺序
                CreateFracChain([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]);//注意科技解锁顺序
                //建筑VI
                CreateFracChain([IFE自然资源分馏塔, IFE升级分馏塔, IFE降级分馏塔, IFE垃圾回收分馏塔, IFE点数聚集分馏塔, IFE增产分馏塔]);
            } else {
                //创世改动过大，单独处理
                RegisterOrEditAsync("左键点击：更换生产设备",
                    "Left click: Change machine\nRight click: Assembler or Fractionator",
                    "左键点击：更换生产设备\n右键点击：常规设备或分馏塔");
                //物品页面
                //自然资源自增值
                CreateRecipe1(I铁矿, 0.05f);
                CreateRecipe1(I铜矿, 0.05f);
                CreateRecipe1(IGB铝矿, 0.05f);
                CreateRecipe1(I硅石, 0.05f);
                CreateRecipe1(I钛石, 0.05f);
                CreateRecipe1(IGB钨矿, 0.05f);
                CreateRecipe1(I煤矿, 0.05f);
                CreateRecipe1(I石矿, 0.05f);
                CreateRecipe1(IGB硫矿, 0.05f);
                CreateRecipe1(IGB放射性矿物, 0.05f);

                CreateRecipe1(I原油, 0.05f);
                CreateRecipe1(IGB海水, 0.05f);
                CreateRecipe1(I水, 0.05f);
                CreateRecipe1(IGB盐酸, 0.05f);
                CreateRecipe1(I硫酸, 0.05f);
                CreateRecipe1(IGB硝酸, 0.05f);
                CreateRecipe1(IGB氨, 0.05f);

                CreateRecipe1(I氢, 0.05f);
                CreateRecipe1(I重氢, 0.05f);
                CreateRecipe1(IGB氦, 0.05f);
                CreateRecipe1(IGB氮, 0.05f);
                CreateRecipe1(IGB氧, 0.05f);
                CreateRecipe1(IGB二氧化碳, 0.05f);
                CreateRecipe1(IGB二氧化硫, 0.05f);

                CreateRecipe1(I金伯利矿石, 0.025f);
                CreateRecipe1(I分形硅石, 0.025f);
                CreateRecipe1(I可燃冰, 0.025f);
                CreateRecipe1(I刺笋结晶, 0.025f);
                CreateRecipe1(I有机晶体, 0.025f);
                CreateRecipe1(I光栅石, 0.025f);
                CreateRecipe1(I单极磁石, 0.025f);
                //物品循环链
                CreateFracChain([I配送运输机, I物流运输机, I星际物流运输船]);
                CreateFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素]);
                CreateFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵]);

                //建筑页面
                CreateFracChain([I电力感应塔, I无线输电塔, I卫星配电站]);
                CreateFracChain([I风力涡轮机, I太阳能板, IGB同位素温差发电机, I蓄电器, I蓄电器满, I能量枢纽]);
                CreateFracChain([I火力发电厂_GB燃料电池发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB朱曦K型人造恒星, IGB湛曦O型人造恒星]);
                CreateFracChain([I传送带, I高速传送带, I极速传送带]);
                CreateFracChain([I四向分流器, I流速监测器, IGB大气采集站, I喷涂机, I自动集装机]);//注意科技解锁顺序
                CreateFracChain([I小型储物仓, I大型储物仓, IGB量子储物仓]);
                CreateFracChain([I储液罐, IGB量子储液罐]);
                if (!MoreMegaStructure.Enable) {
                    CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器]);
                } else {
                    CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器]);
                }
                CreateFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器]);
                CreateFracChain([I采矿机, I大型采矿机]);
                CreateFracChain([I抽水站, IGB聚束液体汲取设施]);
                CreateFracChain([I原油萃取站, I原油精炼厂]);
                CreateFracChain([I化工厂, I量子化工厂_GB先进化学反应釜]);
                CreateFracChain([I电弧熔炉, IGB矿物处理厂, I位面熔炉, I负熵熔炉]);
                CreateFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂]);
                CreateFracChain([I矩阵研究站, I自演化研究站]);
                CreateFracChain([I电磁轨道弹射器, I射线接收站_MS射线重构站, I垂直发射井]);
                CreateFracChain([I微型粒子对撞机]);
                CreateFracChain([IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施, IGB工业先锋精密加工中心, IGB苍穹粒子加速器]);
                CreateFracChain([IFE自然资源分馏塔, IFE升级分馏塔, IFE降级分馏塔, IFE垃圾回收分馏塔, IFE点数聚集分馏塔, IFE增产分馏塔]);

                //精炼页面
                CreateFracChain([
                    IGB空燃料棒,
                    I液氢燃料棒, IGB煤油燃料棒, IGB四氢双环戊二烯燃料棒,
                    IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒,
                    I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒,
                    I反物质燃料棒, I奇异湮灭燃料棒,
                ]);

                //化工页面
                CreateFracChain([I增产剂MkIII_GB增产剂]);

                //防御页面
                CreateFracChain([I原型机, I精准无人机, I攻击无人机]);
                if (!TheyComeFromVoid.Enable) {
                    CreateFracChain([I护卫舰, I驱逐舰]);
                } else {
                    CreateFracChain([I护卫舰, I驱逐舰, IVD水滴]);
                }
                CreateFracChain([I高频激光塔, IGB紫外激光塔, I近程电浆塔, I磁化电浆炮]);
                CreateFracChain([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]);
                CreateFracChain([I高斯机枪塔]);
                CreateFracChain([I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱]);
                CreateFracChain([I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元]);
                CreateFracChain([I聚爆加农炮, IGB电磁加农炮]);
                CreateFracChain([I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组]);
                CreateFracChain([I导弹防御塔]);
                CreateFracChain([I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组]);
                CreateFracChain([I干扰胶囊, I压制胶囊]);
                CreateFracChain([I等离子胶囊, I反物质胶囊]);
            }

            //添加所有翻译
            LoadLanguagePostfixAfterCommonApi();

            LogInfo("Finish to add fractionate recipes.");
        }

        /// <summary>
        /// 创建一个自然资源分馏配方。自己复制的配方没有损毁概率。
        /// </summary>
        private static FracRecipe CreateRecipe1(int itemID, float ratio) {
            FracRecipe r = new(FracRecipeType.NaturalResource, itemID, [-1, itemID], [ratio], [2]);
            naturalResourceRecipeList.Add(r);
            r.unlockItemID = [itemID];
            r.unlockItemNum = [10];
            return r;
        }

        /// <summary>
        /// 创建一个升级分馏配方。复制的配方没有损毁概率。
        /// </summary>
        private static FracRecipe CreateRecipe2(int inputID, int outputID, float success, float destroy) {
            FracRecipe r = new(FracRecipeType.Upgrade, inputID, [-1, outputID], [destroy, success], [0, 1]);
            upgradeRecipeList.Add(r);
            r.unlockItemID = [outputID];
            r.unlockItemNum = [10];
            return r;
        }

        /// <summary>
        /// 创建一个降级分馏配方。
        /// </summary>
        private static FracRecipe CreateRecipe3(int inputID, int outputID, float success, float destroy) {
            FracRecipe r = new(FracRecipeType.DownGrade, inputID, [-1, outputID], [destroy, success], [0, 1]);
            downgradeRecipeList.Add(r);
            r.unlockItemID = [outputID];
            r.unlockItemNum = [10];
            return r;
        }

        /// <summary>
        /// 创建一个增产分馏配方。
        /// </summary>
        private static FracRecipe CreateRecipe4(int itemID, float ratio) {
            FracRecipe r = new(FracRecipeType.Increase, itemID, [itemID], [ratio], [2]);
            increaseRecipeList.Add(r);
            r.unlockItemID = [itemID];
            r.unlockItemNum = [10];
            return r;
        }

        /// <summary>
        /// 添加一些物品构成的升降级分馏链对应的配方。
        /// </summary>
        private static List<FracRecipe>[] CreateFracChain(IReadOnlyList<int> itemChain) {
            List<FracRecipe> list1 = [];
            List<FracRecipe> list2 = [];
            for (int i = 0; i < itemChain.Count - 1; i++) {
                //upgrade: itemChain[i] -> itemChain[i + 1]
                float success1 = 0.04f;
                if (LDB.items.Select(itemChain[i + 1]).maincraft != null) {
                    //todo: 还是根据物品价值进行调整比较好
                    //success1 *= 60000.0f / LDB.items.Select(itemChain[i + 1]).maincraft.TimeSpend;
                }
                FracRecipe r1 = CreateRecipe2(itemChain[i], itemChain[i + 1], success1, 0.05f);
                list1.Add(r1);
                //downgrade: itemChain[i + 1] -> itemChain[i]
                float success2 = 0.02f;
                if (LDB.items.Select(itemChain[i + 1]).maincraft != null) {
                    //todo: 还是根据物品价值进行调整比较好
                    //success2 *= 60000.0f / LDB.items.Select(itemChain[i + 1]).maincraft.TimeSpend;
                }
                FracRecipe r2 = CreateRecipe3(itemChain[i], itemChain[i + 1], success2, 0.02f);
                list2.Add(r2);
            }
            return [list1, list2];
        }

        #endregion

        #region 其他Patch

        /// <summary>
        /// 移除TechProto[] dataArray3的所有处理，从而有足够空间容纳所有分馏图标
        /// </summary>
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(IconSet), nameof(IconSet.Create))]
        [HarmonyPriority(Priority.Last)]
        public static IEnumerable<CodeInstruction>
            IconSet_Create_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var matcher = new CodeMatcher(instructions);
            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(LDB), nameof(LDB.techs)))
            );

            var matcher2 = matcher.Clone();
            matcher2.MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(IconSet), nameof(IconSet.techIconIndexBuffer))),
                new CodeMatch(OpCodes.Ldarg_0)
            );

            while (matcher.Pos < matcher2.Pos) {
                matcher.SetAndAdvance(OpCodes.Nop, null);
            }

            return matcher.InstructionEnumeration();
        }

        // /// <summary>
        // /// 如果物品、配方的详情窗口最下面的制作方式有分馏配方，修改对应显示内容-
        // /// </summary>
        // [HarmonyPatch(typeof(UIRecipeEntry), nameof(UIRecipeEntry.SetRecipe))]
        // [HarmonyPrefix]
        // public static bool UIRecipeEntry_SetRecipe_Prefix(ref UIRecipeEntry __instance, RecipeProto recipe) {
        //     if (recipe.Type != ERecipeType.Fractionate) {
        //         return true;
        //     }
        //     Dictionary<int, float> dic = GetNumRatioNaturalResource(recipe.Items[0]);
        //     if (dic.ContainsKey(1) && dic[1] == 0) {
        //         //降级的就不管了
        //         dic = GetNumRatioUpgrade(recipe.Items[0]);
        //     }
        //     var p = dic.FirstOrDefault(p => p.Key > 0);
        //     int index1 = 0;
        //     int x1 = 0;
        //
        //     ItemProto itemProto = LDB.items.Select(recipe.Results[0]);
        //     __instance.icons[index1].sprite = itemProto?.iconSprite;
        //     //产物数目使用dic首个不为损毁的概率的key
        //     __instance.countTexts[index1].text = p.Key.ToString();
        //     __instance.icons[index1].rectTransform.anchoredPosition = new(x1, 0.0f);
        //     __instance.icons[index1].gameObject.SetActive(true);
        //
        //     ++index1;
        //     x1 += 40;
        //     __instance.arrow.anchoredPosition = new(x1, -27f);
        //     //概率显示包括dic首个不为损毁的概率的Value，以及损毁概率（如果有的话）
        //     string str = p.Value.FormatP();
        //     if (enableDestroy && dic.TryGetValue(-1, out float destroyRatio)) {
        //         str += "(" + destroyRatio.FormatP() + ")";
        //     }
        //     __instance.timeText.text = str;
        //     //横向拓展，避免显示不下
        //     __instance.timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        //
        //     int x2 = x1 + 40;
        //     itemProto = LDB.items.Select(recipe.Items[0]);
        //     __instance.icons[index1].sprite = itemProto?.iconSprite;
        //     //原料数目使用1
        //     __instance.countTexts[index1].text = "1";
        //     __instance.icons[index1].rectTransform.anchoredPosition = new(x2, 0.0f);
        //     __instance.icons[index1].gameObject.SetActive(true);
        //
        //     ++index1;
        //     x2 += 40;
        //     for (int index4 = index1; index4 < 7; ++index4)
        //         __instance.icons[index4].gameObject.SetActive(false);
        //     return false;
        // }

        // /// <summary>
        // /// 公式分页移除所有新增配方的图标
        // /// </summary>
        // [HarmonyTranspiler]
        // [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker.RefreshIcons))]
        // [HarmonyPriority(Priority.Last)]
        // public static IEnumerable<CodeInstruction>
        //     UISignalPicker_RefreshIcons_Transpiler(IEnumerable<CodeInstruction> instructions) {
        //     var matcher = new CodeMatcher(instructions);
        //     matcher.MatchForward(false,
        //         new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(RecipeProto), nameof(RecipeProto.hasIcon)))
        //     ).Advance(-3);
        //
        //     var dataArray = matcher.InstructionAt(0).operand;
        //     var index = matcher.InstructionAt(1).operand;
        //     var label = matcher.InstructionAt(4).operand;
        //
        //     //添加：if(dataArray[index11].ID > 1000) 跳转到结尾
        //     matcher.InsertAndAdvance(
        //         new CodeInstruction(OpCodes.Ldloc_S, dataArray),
        //         new CodeInstruction(OpCodes.Ldloc_S, index),
        //         new CodeInstruction(OpCodes.Ldelem_Ref),
        //         new CodeInstruction(OpCodes.Call,
        //             AccessTools.Method(typeof(UISignalPickerPatch),
        //                 nameof(UISignalPicker_RefreshIcons_Transpiler_InsertMethod))),
        //         new CodeInstruction(OpCodes.Brtrue_S, label));
        //
        //     return matcher.InstructionEnumeration();
        // }

        // public static bool UISignalPicker_RefreshIcons_Transpiler_InsertMethod(RecipeProto recipeProto) {
        //     return recipeProto.ID > 1000;
        // }
        //
        // /// <summary>
        // /// 所有分馏配方显示在各个页面
        // /// </summary>
        // [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker.RefreshIcons))]
        // [HarmonyPostfix]
        // public static void UISignalPicker_RefreshIcons_Postfix(ref UISignalPicker __instance) {
        //     if (__instance.currentType > 7) {
        //         IconSet iconSet = GameMain.iconSet;
        //         RecipeProto[] dataArray = LDB.recipes.dataArray;
        //         foreach (var recipe in dataArray) {
        //             if (UISignalPicker_RefreshIcons_Transpiler_InsertMethod(recipe) && recipe.hasIcon) {
        //                 int tab = recipe.GridIndex / 1000;
        //                 if (tab == __instance.currentType - 5) {
        //                     int row = (recipe.GridIndex - tab * 1000) / 100 - 1;
        //                     int column = recipe.GridIndex % 100 - 1;
        //                     if (row >= 0 && column >= 0 && row < maxRowCount && column < maxColumnCount) {
        //                         int index = row * maxColumnCount + column;
        //                         if (index >= 0
        //                             && index < __instance.indexArray.Length
        //                             && __instance.indexArray[index] == 0) {
        //                             //这个条件可以避免配方图标占用原有物品图标
        //                             int index6 = SignalProtoSet.SignalId(ESignalType.Recipe, recipe.ID);
        //                             __instance.indexArray[index] = iconSet.signalIconIndex[index6];
        //                             __instance.signalArray[index] = index6;
        //                         }
        //                     }
        //                 }
        //             }
        //         }
        //     }
        // }

        // /// <summary>
        // /// 如果移动到配方上面，显示配方的弹窗
        // /// </summary>
        // [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker._OnUpdate))]
        // [HarmonyPostfix]
        // public static void UISignalPicker__OnUpdate_Postfix(ref UISignalPicker __instance) {
        //     if (__instance.screenItemTip == null) {
        //         return;
        //     }
        //     if (__instance.hoveredIndex < 0) {
        //         __instance.screenItemTip.showingItemId = 0;
        //         __instance.screenItemTip.gameObject.SetActive(false);
        //         return;
        //     }
        //     int index = __instance.signalArray[__instance.hoveredIndex];
        //     if (index > 20000 && index < 32000) {
        //         var recipe = LDB.recipes.Select(index - 20000);
        //         if (recipe == null) {
        //             __instance.screenItemTip.showingItemId = 0;
        //             __instance.screenItemTip.gameObject.SetActive(false);
        //             return;
        //         }
        //         int num1 = __instance.hoveredIndex % maxColumnCount;
        //         int num2 = __instance.hoveredIndex / maxColumnCount;
        //         if (!__instance.screenItemTip.gameObject.activeSelf) {
        //             __instance.screenItemTip.gameObject.SetActive(true);
        //         }
        //         //itemId为负数时，表示显示id为-itemId的recipe
        //         __instance.screenItemTip.SetTip(-recipe.ID,
        //             __instance.itemTipAnchor, new(num1 * 46 + 15, -num2 * 46 - 50), __instance.iconImage.transform,
        //             0, 0, UIButton.ItemTipType.Other, isSign: true);
        //     }
        // }

        #endregion
    }
}
