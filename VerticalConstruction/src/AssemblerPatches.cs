using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static VerticalConstruction.Utils.ProtoID;

namespace VerticalConstruction {
    public class AssemblerPatches {
        class ModelSetting {
            public bool multiLevelAllowPortsOrSlots;
            public List<int> multiLevelAlternativeIds;
            public List<bool> multiLevelAlternativeYawTransposes;
            public Vector3 lapJoint;

            // 每级垂直结构研究的最大垂直数量
            // 设置的目的是使其在达到最大值时不会悬挂行星防护罩。
            //[0]为默认值，[1]为完成垂直建造第 1 级研究时的值，[6]为完成最大垂直建造级别（6）研究时的值。
            public int[] multiLevelMaxBuildCount;

            public ModelSetting(bool multiLevelAllowPortsOrSlots, List<int> multiLevelAlternativeIds,
                List<bool> multiLevelAlternativeYawTransposes, Vector3 lapJoint, int[] multiLevelMaxBuildCount) {
                this.multiLevelAllowPortsOrSlots = multiLevelAllowPortsOrSlots;
                this.multiLevelAlternativeIds = multiLevelAlternativeIds;
                this.multiLevelAlternativeYawTransposes = multiLevelAlternativeYawTransposes;
                this.lapJoint = lapJoint;
                this.multiLevelMaxBuildCount = multiLevelMaxBuildCount;
            }
        }

        public static void ResetNextIds() {
            for (int i = 0; i < GameMain.data.factories.Length; i++) {
                if (GameMain.data.factories[i] == null) {
                    continue;
                }

                var _this = GameMain.data.factories[i].factorySystem;
                if (_this == null) {
                    continue;
                }

                var factoryIndex = _this.factory.index;
                int[] assemblerPrevIds = new int[assemblerComponentEx.assemblerNextIds[factoryIndex].Length];

                var assemblerCapacity = Traverse.Create(_this).Field("assemblerCapacity").GetValue<int>();
                for (int j = 1; j < assemblerCapacity; j++) {
                    var assemblerId = j;

                    int entityId = _this.assemblerPool[assemblerId].entityId;
                    if (entityId == 0) {
                        continue;
                    }

                    int nextEntityId = entityId;
                    do {
                        int prevEntityId = nextEntityId;

                        bool isOutput;
                        int otherObjId;
                        int otherSlot;
                        _this.factory.ReadObjectConn(nextEntityId, PlanetFactory.kMultiLevelOutputSlot, out isOutput,
                            out otherObjId, out otherSlot);

                        nextEntityId = otherObjId;

                        if (nextEntityId > 0) {
                            int prevAssemblerId = _this.factory.entityPool[prevEntityId].assemblerId;
                            int nextAssemblerId = _this.factory.entityPool[nextEntityId].assemblerId;
                            if (nextAssemblerId > 0 && _this.assemblerPool[nextAssemblerId].id == nextAssemblerId) {
                                // まだRootは特定できないので0にしておく
                                // MEMO: まだRootが特定できないのでassemblerComponentEx.SetAssemblerInsertTarget()は呼び出せない
                                assemblerComponentEx.SetAssemblerNext(factoryIndex, prevAssemblerId, nextAssemblerId);
                                assemblerPrevIds[nextAssemblerId] = prevAssemblerId;
                            }
                        }
                    } while (nextEntityId != 0);
                }

                // レシピの設定(一番下のアセンブラのレシピに合わせる)
                var lenAssemblerPrevIds = assemblerPrevIds.Length;
                for (int j = 1; j < lenAssemblerPrevIds; j++) {
                    var assemblerPrevId = assemblerPrevIds[j];
                    if (assemblerPrevId == 0 && _this.assemblerPool[assemblerPrevId].id == assemblerPrevId) {
                        // Rootを見つけたらそこから子を辿ってレシピを設定する
                        var assemblerNextId = assemblerComponentEx.GetNextId(factoryIndex, j);
                        while (assemblerNextId != 0) {
                            AssemblerComponentEx.FindRecipeIdForBuild(_this, assemblerNextId);
                            assemblerNextId = assemblerComponentEx.GetNextId(factoryIndex, assemblerNextId);
                        }
                    }
                }
            }
        }

        // 创世之书巨型建筑
        static readonly ModelSetting MegaBuildingSetting = new ModelSetting(
            false,
            new List<int> { IGB天穹装配厂, IGB物质裂解塔, IGB埃克森美孚化工厂, IGB工业先锋精密加工中心, IGB物质分解设施, IGB苍穹粒子加速器 },
            new List<bool> { false, false, false, false, false, false },
            new Vector3(0, 26.5f, 0),
            new int[7] { 1, 1, 1, 1, 1, 2, 3 }
        );
        // 电力设施
        static readonly ModelSetting ElectricSetting = new ModelSetting(
            false,
            new List<int>
                { I风力涡轮机, I火力发电厂, I太阳能板, I地热发电站, I微型聚变发电站_GB裂变能源发电站, IGB同位素温差发电机, I人造恒星_GB人造恒星MKI, IGB人造恒星MKII },
            new List<bool> { false, false, false, false, false, false, false, false },
            new Vector3(0, 5.05f, 0),
            new int[7] { 2, 4, 6, 8, 10, 11, 12 }
        );
        // 制造台
        static readonly ModelSetting AssemblerSetting = new ModelSetting(
            false,
            new List<int> { I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂 },
            new List<bool> { false, false, false, false },
            new Vector3(0, 5.05f, 0),
            new int[7] { 2, 4, 6, 8, 10, 11, 12 }
        );
        // 熔炉
        static readonly ModelSetting SmelterSetting = new ModelSetting(
            false,
            new List<int> { I电弧熔炉, I位面熔炉, IGB矿物处理厂, I负熵熔炉 },
            new List<bool> { false, false, false, false },
            new Vector3(0, 4.3f, 0),
            new int[7] { 2, 4, 6, 8, 10, 12, 14 }
        );
        // 化工厂
        static readonly ModelSetting ChemicalPlantSetting = new ModelSetting(
            false,
            new List<int> { I化工厂, I量子化工厂_GB先进化学反应釜 },
            new List<bool> { false, false },
            new Vector3(0, 6.85f, 0),
            new int[7] { 2, 4, 5, 6, 7, 8, 9 }
        );
        // 原油精炼厂
        static readonly ModelSetting OilRefinerySetting = new ModelSetting(
            false,
            new List<int> { I原油精炼厂 },
            new List<bool> { false },
            new Vector3(0, 11.8f, 0),
            new int[7] { 1, 2, 2, 3, 3, 4, 5 }
        );
        // 小型粒子加速器
        static readonly ModelSetting MiniatureParticleColliderSetting = new ModelSetting(
            false,
            new List<int> { I微型粒子对撞机 },
            new List<bool> { false },
            new Vector3(0, 15.2f, 0),
            new int[7] { 1, 2, 2, 3, 3, 3, 4 }
        );

        // 建筑匹配
        static readonly Dictionary<int, ModelSetting> ModelSettingDict = new Dictionary<int, ModelSetting> {
            { IGB天穹装配厂, MegaBuildingSetting },
            { IGB物质裂解塔, MegaBuildingSetting },
            { IGB埃克森美孚化工厂, MegaBuildingSetting },
            { IGB工业先锋精密加工中心, MegaBuildingSetting },
            { IGB物质分解设施, MegaBuildingSetting },
            { IGB苍穹粒子加速器, MegaBuildingSetting },

            // { I风力涡轮机, ElectricSetting },
            // { I火力发电厂, ElectricSetting },
            // { I太阳能板, ElectricSetting },
            // { I地热发电站, ElectricSetting },
            // { I微型聚变发电站_GB裂变能源发电站, ElectricSetting },
            // { IGB同位素温差发电机, ElectricSetting },
            // { I人造恒星_GB人造恒星MKI, ElectricSetting },
            // { IGB人造恒星MKII, ElectricSetting },

            { I制造台MkI_GB基础制造台, AssemblerSetting },
            { I制造台MkII_GB标准制造单元, AssemblerSetting },
            { I制造台MkIII_GB高精度装配线, AssemblerSetting },
            { I重组式制造台_GB物质重组工厂, AssemblerSetting },

            { I电弧熔炉, SmelterSetting },
            { I位面熔炉, SmelterSetting },
            { IGB矿物处理厂, SmelterSetting },
            { I负熵熔炉, SmelterSetting },

            { I化工厂, ChemicalPlantSetting },
            { I量子化工厂_GB先进化学反应釜, ChemicalPlantSetting },

            { I原油精炼厂, OilRefinerySetting },

            { I微型粒子对撞机, MiniatureParticleColliderSetting },
        };

        public static AssemblerComponentEx assemblerComponentEx = new AssemblerComponentEx();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemProto), "Preload")]
        private static void PreloadPatch(ItemProto __instance, int _index) {
            ModelProto modelProto = LDB.models.modelArray[__instance.ModelIndex];
            if (modelProto != null && modelProto.prefabDesc != null && modelProto.prefabDesc.isAssembler) {
                ModelSetting setting;
                if (ModelSettingDict.TryGetValue(__instance.ID, out setting)) {
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevel = true;
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAllowPortsOrSlots =
                        setting.multiLevelAllowPortsOrSlots;
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.lapJoint = setting.lapJoint;

                    // multiLevelAlternative*に自身のIDは含まないので除く
                    var index = setting.multiLevelAlternativeIds.FindIndex(item => item == __instance.ID);
                    if (index >= 0) {
                        var multiLevelAlternativeIds = new int[setting.multiLevelAlternativeIds.Count - 1];
                        var multiLevelAlternativeYawTransposes = new bool[setting.multiLevelAlternativeIds.Count - 1];

                        int count = 0;
                        for (int i = 0; i < setting.multiLevelAlternativeIds.Count; i++) {
                            if (i == index) {
                                continue;
                            }

                            multiLevelAlternativeIds[count] = setting.multiLevelAlternativeIds[i];
                            multiLevelAlternativeYawTransposes[count] = setting.multiLevelAlternativeYawTransposes[i];
                            count++;
                        }

                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeIds =
                            multiLevelAlternativeIds;
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeYawTransposes =
                            multiLevelAlternativeYawTransposes;
                    } else {
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeIds =
                            setting.multiLevelAlternativeIds.ToArray();
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeYawTransposes =
                            setting.multiLevelAlternativeYawTransposes.ToArray();
                    }
                }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(FactorySystem), "SetAssemblerCapacity")]
        private static bool SetAssemblerCapacityPatch(FactorySystem __instance, int newCapacity) {
            var index = __instance.factory.index;
            if (index > assemblerComponentEx.assemblerNextIds.Length) {
                assemblerComponentEx.SetAssemblerCapacity(assemblerComponentEx.assemblerCapacity * 2);
            }

            var assemblerCapacity = Traverse.Create(__instance).Field("assemblerCapacity").GetValue<int>();

            int[] oldAssemblerNextIds = assemblerComponentEx.assemblerNextIds[index];
            assemblerComponentEx.assemblerNextIds[index] = new int[newCapacity];
            if (oldAssemblerNextIds != null) {
                Array.Copy(oldAssemblerNextIds, assemblerComponentEx.assemblerNextIds[index],
                    (newCapacity <= assemblerCapacity) ? newCapacity : assemblerCapacity);
            }

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "ApplyInsertTarget")]
        public static bool ApplyInsertTargetPatch(PlanetFactory __instance, int entityId, int insertTarget, int slotId,
            int offset) {
            if (entityId != 0) {
                if (insertTarget < 0) {
                    Assert.CannotBeReached();
                    insertTarget = 0;
                } else {
                    // MEMO: PlanetFactory.ApplyEntityOutput()から呼ばれるかPlanetFactory.ApplyEntityInput()からでentityIdとinsertTargetが入れ替わる
                    //       なのでどっちが上なのか下なのか判定しないといけない
                    //       このプログラムではinsertTargetが上(next)という想定
                    bool isOutput;
                    int otherObjId;
                    int otherSlot;
                    __instance.ReadObjectConn(entityId, PlanetFactory.kMultiLevelOutputSlot, out isOutput,
                        out otherObjId, out otherSlot);
                    if (!(isOutput && otherObjId == insertTarget)) {
                        // Swap
                        int temp = insertTarget;
                        insertTarget = entityId;
                        entityId = temp;
                    }

                    int assemblerId = __instance.entityPool[entityId].assemblerId;
                    if (assemblerId > 0 && __instance.entityPool[insertTarget].assemblerId > 0) {
                        assemblerComponentEx.SetAssemblerInsertTarget(__instance, assemblerId, insertTarget);
                    }
                }
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "ApplyEntityDisconnection")]
        public static bool ApplyEntityDisconnectionPatch(PlanetFactory __instance, int otherEntityId,
            int removingEntityId, int otherSlotId, int removingSlotId) {
            if (otherEntityId == 0) {
                return true;
            }

            var _this = __instance;
            int assemblerId = _this.entityPool[otherEntityId].assemblerId;
            if (assemblerId > 0) {
                int assemblerRemoveId = _this.entityPool[removingEntityId].assemblerId;
                if (assemblerRemoveId > 0) {
                    assemblerComponentEx.UnsetAssemblerInsertTarget(__instance, assemblerId, assemblerRemoveId);
                }
            }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(PlanetFactory), "CreateEntityLogicComponents")]
        public static void CreateEntityLogicComponentsPatch(PlanetFactory __instance, int entityId, PrefabDesc desc,
            int prebuildId) {
            if (entityId == 0 || !desc.isAssembler) {
                return;
            }

            // プレビルド設置後にレシピ再設定
            // MEMO: プレビルドだった場合ApplyInsertTarget()後にレシピがプレビルドのものに上書きされてしまうのでここで再設定する必要がある
            int assemblerId = __instance.entityPool[entityId].assemblerId;
            AssemblerComponentEx.FindRecipeIdForBuild(__instance.factorySystem, assemblerId);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(FactorySystem), "GameTick", typeof(long), typeof(bool))]
        public static void GameTickPatch(FactorySystem __instance, long time, bool isActive) {
            PerformanceMonitor.BeginSample(ECpuWorkEntry.Assembler);
            var factory = __instance.factory;
            var factoryIndex = factory.index;
            var assemblerPool = __instance.assemblerPool;
            var assemblerCursor = __instance.assemblerCursor;
            for (int num17 = 1; num17 < assemblerCursor; num17++) {
                if (assemblerPool[num17].id == num17) {
                    var NextId = assemblerComponentEx.GetNextId(factoryIndex, num17);
                    if (NextId > 0) {
                        assemblerComponentEx.UpdateOutputToNext(factory, factoryIndex, num17, assemblerPool, NextId,
                            false);
                    }
                }
            }
            PerformanceMonitor.EndSample(ECpuWorkEntry.Assembler);
        }

        [HarmonyPostfix,
         HarmonyPatch(typeof(FactorySystem), "GameTick", typeof(long), typeof(bool), typeof(int), typeof(int),
             typeof(int))]
        public static void GameTickPatch(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt,
            int _curThreadIdx, int _minimumMissionCnt) {
            var factory = __instance.factory;
            var factoryIndex = factory.index;
            var assemblerPool = __instance.assemblerPool;
            var assemblerCursor = __instance.assemblerCursor;

            if (WorkerThreadExecutor.CalculateMissionIndex(1, assemblerCursor - 1, _usedThreadCnt, _curThreadIdx,
                    _minimumMissionCnt, out var _start, out var _end)) {
                // useMutexの判定をfor内でやらなくて済むように部分的にループアンローリングしてる

                int i = _start;
                bool useMutex = i == 1;
                var nextId = assemblerComponentEx.GetNextId(factoryIndex, i);
                if (assemblerPool[i].id == i && nextId > 0) {
                    assemblerComponentEx.UpdateOutputToNext(factory, factoryIndex, i, assemblerPool, nextId, useMutex);
                }

                for (i = _start + 1; i < _end - 1; i++) {
                    nextId = assemblerComponentEx.GetNextId(factoryIndex, i);
                    if (assemblerPool[i].id == i && nextId > 0) {
                        assemblerComponentEx.UpdateOutputToNext(factory, factoryIndex, i, assemblerPool, nextId, false);
                    }
                }

                i = _end - 1;
                if (i != _start) {
                    useMutex = i == 1;
                    nextId = assemblerComponentEx.GetNextId(factoryIndex, i);
                    if (assemblerPool[i].id == i && nextId > 0) {
                        assemblerComponentEx.UpdateOutputToNext(factory, factoryIndex, i, assemblerPool, nextId,
                            useMutex);
                    }
                }
            }
        }

        public static void SyncAssemblerFunctions(FactorySystem factorySystem, Player player, int assemblerId) {
            var _this = factorySystem;
            int entityId = _this.assemblerPool[assemblerId].entityId;
            if (entityId == 0) {
                return;
            }

            int num = entityId;
            do {
                bool flag;
                int num3;
                int num4;
                _this.factory.ReadObjectConn(num, PlanetFactory.kMultiLevelInputSlot, out flag, out num3, out num4);
                num = num3;
                if (num > 0) {
                    int assemblerId2 = _this.factory.entityPool[num].assemblerId;
                    if (assemblerId2 > 0 && _this.assemblerPool[assemblerId2].id == assemblerId2) {
                        if (_this.assemblerPool[assemblerId].recipeId > 0) {
                            if (_this.assemblerPool[assemblerId2].recipeId
                                != _this.assemblerPool[assemblerId].recipeId) {
                                _this.TakeBackItems_Assembler(player, assemblerId2);
                                _this.assemblerPool[assemblerId2].SetRecipe(_this.assemblerPool[assemblerId].recipeId,
                                    _this.factory.entitySignPool);
                            }
                        } else if (_this.assemblerPool[assemblerId2].recipeId != 0) {
                            _this.TakeBackItems_Assembler(player, assemblerId2);
                            _this.assemblerPool[assemblerId2].SetRecipe(0, _this.factory.entitySignPool);
                        }
                    }
                }
            } while (num != 0);

            num = entityId;
            do {
                bool flag;
                int num3;
                int num4;
                _this.factory.ReadObjectConn(num, PlanetFactory.kMultiLevelOutputSlot, out flag, out num3, out num4);
                num = num3;
                if (num > 0) {
                    int assemblerId3 = _this.factory.entityPool[num].assemblerId;
                    if (assemblerId3 > 0 && _this.assemblerPool[assemblerId3].id == assemblerId3) {
                        if (_this.assemblerPool[assemblerId].recipeId > 0) {
                            if (_this.assemblerPool[assemblerId3].recipeId
                                != _this.assemblerPool[assemblerId].recipeId) {
                                _this.TakeBackItems_Assembler(_this.factory.gameData.mainPlayer, assemblerId3);
                                _this.assemblerPool[assemblerId3].SetRecipe(_this.assemblerPool[assemblerId].recipeId,
                                    _this.factory.entitySignPool);
                            }
                        } else if (_this.assemblerPool[assemblerId3].recipeId != 0) {
                            _this.TakeBackItems_Assembler(_this.factory.gameData.mainPlayer, assemblerId3);
                            _this.assemblerPool[assemblerId3].SetRecipe(0, _this.factory.entitySignPool);
                        }
                    }
                }
            } while (num != 0);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnRecipeResetClick")]
        public static void OnRecipeResetClickPatch(UIAssemblerWindow __instance) {
            if (__instance.assemblerId == 0 || __instance.factory == null) {
                return;
            }
            AssemblerComponent assemblerComponent = __instance.factorySystem.assemblerPool[__instance.assemblerId];
            if (assemblerComponent.id != __instance.assemblerId) {
                return;
            }
            SyncAssemblerFunctions(__instance.factorySystem, __instance.player, __instance.assemblerId);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnRecipePickerReturn")]
        public static void OnRecipePickerReturnPatch(UIAssemblerWindow __instance) {
            if (__instance.assemblerId == 0 || __instance.factory == null) {
                return;
            }
            AssemblerComponent assemblerComponent = __instance.factorySystem.assemblerPool[__instance.assemblerId];
            if (assemblerComponent.id != __instance.assemblerId) {
                return;
            }
            SyncAssemblerFunctions(__instance.factorySystem, __instance.player, __instance.assemblerId);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildingParameters), "PasteToFactoryObject")]
        public static void PasteToFactoryObjectPatch(BuildingParameters __instance, int objectId,
            PlanetFactory factory) {
            if (objectId <= 0) {
                return;
            }

            int assemblerId = factory.entityPool[objectId].assemblerId;
            if (assemblerId != 0
                && __instance.type == BuildingType.Assembler
                && factory.factorySystem.assemblerPool[assemblerId].recipeId == __instance.recipeId) {
                ItemProto itemProto = LDB.items.Select(factory.entityPool[objectId].protoId);
                if (itemProto != null && itemProto.prefabDesc != null) {
                    SyncAssemblerFunctions(factory.factorySystem, factory.gameData.mainPlayer, assemblerId);
                }
            }

        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildTool_Click), "CheckBuildConditions")]
        public static void CheckBuildConditionsPatch(BuildTool_Click __instance, ref bool __result) {
            if (__instance.buildPreviews.Count == 0) {
                return;
            }

            GameHistoryData history = __instance.actionBuild.history;

            bool isNoLevelLimit = true;
            for (int i = 0; i < __instance.buildPreviews.Count; i++) {
                BuildPreview buildPreview = __instance.buildPreviews[i];
                if (buildPreview.condition != 0) {
                    continue;
                }

                if (buildPreview.desc.isAssembler && buildPreview.desc.multiLevel) {
                    int id = buildPreview.item.ID;

                    ModelSetting setting;
                    if (ModelSettingDict.TryGetValue(id, out setting)) {
                        var storageResearchLevel = history.storageLevel - 2;
                        if (storageResearchLevel
                            < setting.multiLevelMaxBuildCount
                                .Length)// 念のため垂直建設研究の最大レベルがMOD制作時の最大レベルである6より大きくなってたら何もしないようにする
                        {
                            int level = setting.multiLevelMaxBuildCount[storageResearchLevel];
                            int maxCount = setting.multiLevelMaxBuildCount[6];

                            int verticalCount = 0;
                            if (buildPreview.inputObjId != 0) {
                                __instance.factory.ReadObjectConn(buildPreview.inputObjId,
                                    PlanetFactory.kMultiLevelInputSlot, out var isOutput, out var otherObjId,
                                    out var otherSlot);
                                while (otherObjId != 0) {
                                    verticalCount++;
                                    __instance.factory.ReadObjectConn(otherObjId, PlanetFactory.kMultiLevelInputSlot,
                                        out isOutput, out otherObjId, out otherSlot);
                                }
                            }

                            if (level >= 2 && verticalCount >= level - 1) {
                                isNoLevelLimit = level >= maxCount;
                                buildPreview.condition = EBuildCondition.OutOfVerticalConstructionHeight;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < __instance.buildPreviews.Count; i++) {
                BuildPreview buildPreview3 = __instance.buildPreviews[i];
                if (buildPreview3.condition == EBuildCondition.OutOfVerticalConstructionHeight) {
                    __instance.actionBuild.model.cursorState = -1;
                    __instance.actionBuild.model.cursorText = buildPreview3.conditionText;
                    if (!isNoLevelLimit) {
                        __instance.actionBuild.model.cursorText += "垂直建造可升级".Translate();
                    }

                    if (!VFInput.onGUI) {
                        UICursor.SetCursor(ECursor.Ban);
                    }

                    __result = false;

                    break;
                }
            }
        }
    }
}
