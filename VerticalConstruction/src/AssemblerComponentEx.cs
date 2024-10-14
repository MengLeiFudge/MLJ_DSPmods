using System;

namespace VerticalConstruction {
    public class AssemblerComponentEx {
        public int[][] assemblerNextIds = new int[64 * 6][];// 上面的那个是下一个
        public int assemblerCapacity = 64 * 6;
        public int transfarCount = 6;// 1个周期内传输多少个输出

        public void SetAssemblerCapacity(int newCapacity) {
            var oldAssemblerNextIds = assemblerNextIds;

            assemblerNextIds = new int[newCapacity][];

            if (oldAssemblerNextIds != null) {
                Array.Copy(oldAssemblerNextIds, assemblerNextIds,
                    (newCapacity <= assemblerCapacity) ? newCapacity : assemblerCapacity);
            }

            assemblerCapacity = newCapacity;
        }

        public int GetNextId(int index, int assemblerId) {
            if (index >= assemblerNextIds.Length) {
                return 0;
            }

            if (assemblerNextIds[index] == null || assemblerId >= assemblerNextIds[index].Length) {
                return 0;
            }

            return assemblerNextIds[index][assemblerId];
        }

        public static void FindRecipeIdForBuild(FactorySystem factorySystem, int assemblerId) {
            // 上下のアセンブラからレシピを設定
            // LabComponent.FindLabFunctionsForBuild()を参考に実装
            // 自身から下、
            int entityId = factorySystem.assemblerPool[assemblerId].entityId;
            if (entityId == 0) {
                return;
            }

            int otherObjId;

            // まずは自身から下へ辿っていく
            int objId = entityId;
            do {

                factorySystem.factory.ReadObjectConn(objId, PlanetFactory.kMultiLevelInputSlot, out bool _,
                    out otherObjId, out _);
                objId = otherObjId;
                if (objId > 0) {
                    int assemblerId2 = factorySystem.factory.entityPool[objId].assemblerId;
                    if (assemblerId2 > 0 && factorySystem.assemblerPool[assemblerId2].id == assemblerId2) {
                        if (factorySystem.assemblerPool[assemblerId2].recipeId > 0) {
                            factorySystem.assemblerPool[assemblerId].SetRecipe(
                                factorySystem.assemblerPool[assemblerId2].recipeId,
                                factorySystem.factory.entitySignPool);
                            return;
                        }
                    }
                }
            } while (objId != 0);

            // 如果这不起作用，就开始努力向上。
            objId = entityId;
            do {
                factorySystem.factory.ReadObjectConn(objId, PlanetFactory.kMultiLevelInputSlot, out bool _,
                    out otherObjId, out _);
                objId = otherObjId;
                if (objId > 0) {
                    int assemblerId3 = factorySystem.factory.entityPool[objId].assemblerId;
                    if (assemblerId3 > 0 && factorySystem.assemblerPool[assemblerId3].id == assemblerId3) {
                        if (factorySystem.assemblerPool[assemblerId3].recipeId > 0) {
                            factorySystem.assemblerPool[assemblerId].SetRecipe(
                                factorySystem.assemblerPool[assemblerId3].recipeId,
                                factorySystem.factory.entitySignPool);
                            return;
                        }
                    }
                }
            } while (objId != 0);
        }

        public void SetAssemblerInsertTarget(PlanetFactory __instance, int assemblerId, int nextEntityId) {
            var index = __instance.factorySystem.factory.index;
            if (index >= assemblerNextIds.Length) {
                SetAssemblerCapacity(assemblerCapacity * 2);
            }

            if (assemblerId != 0 && __instance.factorySystem.assemblerPool[assemblerId].id == assemblerId) {
                if (nextEntityId == 0) {
                    assemblerNextIds[index][assemblerId] = 0;
                } else {
                    var nextAssemblerId = __instance.entityPool[nextEntityId].assemblerId;

                    assemblerNextIds[index][assemblerId] = nextAssemblerId;

                    // 同じレシピにする
                    FindRecipeIdForBuild(__instance.factorySystem, assemblerId);
                }
            }
        }

        public void UnsetAssemblerInsertTarget(PlanetFactory __instance, int assemblerId, int assemblerRemoveId) {
            var index = __instance.factorySystem.factory.index;
            if (assemblerId != 0 && __instance.factorySystem.assemblerPool[assemblerId].id == assemblerId) {
                assemblerNextIds[index][assemblerId] = 0;
            }
        }

        public void SetAssemblerNext(int index, int assemblerId, int nextId) {
            if (index >= assemblerNextIds.Length) {
                SetAssemblerCapacity(assemblerCapacity * 2);
            }

            if (assemblerNextIds[index] == null || assemblerId >= assemblerNextIds[index].Length) {
                var array = assemblerNextIds[index];

                var newCapacity = assemblerId * 2;
                newCapacity = newCapacity > 256 ? newCapacity : 256;
                assemblerNextIds[index] = new int[newCapacity];
                if (array != null) {
                    var len = array.Length;
                    Array.Copy(array, assemblerNextIds[index], (newCapacity <= len) ? newCapacity : len);
                }
            }

            assemblerNextIds[index][assemblerId] = nextId;
        }

        public void UpdateOutputToNext(PlanetFactory factory, int planeIndex, int assemblerId,
            AssemblerComponent[] assemblerPool, int assemblerNextId, bool useMutex) {
            if (useMutex) {
                var entityId = assemblerPool[assemblerId].entityId;
                var entityNextId = assemblerPool[assemblerNextId].entityId;

                lock (factory.entityMutexs[entityId]) {
                    lock (factory.entityMutexs[entityNextId]) {
                        UpdateOutputToNextInner(assemblerId, assemblerNextId, assemblerPool);
                    }
                }
            } else {
                UpdateOutputToNextInner(assemblerId, assemblerNextId, assemblerPool);
            }
        }

        private void UpdateOutputToNextInner(int assemblerId, int assemblerNextId, AssemblerComponent[] assemblerPool) {
            ref var _this = ref assemblerPool[assemblerId];
            if (_this.served == null)// MEMO: レシピが空の場合はservedがnullになっている
            {
                return;
            }

            ref var nextAssembler = ref assemblerPool[assemblerNextId];

            // アセンブラが保管する素材のバッファの基本的な上限係数
            // AssemblerComponent.UpdateNeeds()のコードが元
            int needsFactor = nextAssembler.speedOverride * 180 / nextAssembler.timeSpend + 1;

            int servedLen = _this.served.Length;
            for (int i = 0; i < servedLen; i++) {
                int served = _this.served[i];
                int nextNeeds = nextAssembler.requireCounts[i] * needsFactor - nextAssembler.served[i];
                if (nextNeeds > 0 && served > 0) {
                    ref int incServed = ref _this.incServed[i];

                    // assemblerIdに素材の在庫があったらnextNeedsを満たすようにnextAssemblerへ送る
                    int transfar = Math.Min(served, nextNeeds);

                    if (incServed <= 0) {
                        incServed = 0;
                    }

                    //var args = new object[] { _this.served[i], incServed, transfar };
                    //int out_one_inc_level = Traverse.Create(nextAssembler).Method("split_inc_level", new System.Type[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int) }).GetValue<int>(args);
                    //_this.served[i] = (int)args[0];
                    //incServed = (int)args[1];

                    // MEMO: 本当はnextAssembler.split_inc_level()を呼ぶのが正しい。
                    //       が、split_inc_level()はstaticでいいのにstaticになってない、さらにprivateなのでここから呼び出すのにどうしてもコストがかかる。
                    //       なのでsplit_inc_level()の実装をそのまま持ってくることにした。
                    int out_one_inc_level = split_inc_level(ref _this.served[i], ref incServed, transfar);
                    if (_this.served[i] == 0) {
                        incServed = 0;
                    }

                    nextAssembler.served[i] += transfar;
                    nextAssembler.incServed[i] += transfar * out_one_inc_level;
                }
            }

            var productCountsLen = _this.productCounts.Length;
            for (int l = 0; l < productCountsLen; l++) {
                var maxCount = _this.productCounts[l] * 9;
                if (_this.produced[l] < maxCount && nextAssembler.produced[l] > 0) {
                    var count = Math.Min(transfarCount, nextAssembler.produced[l]);
                    _this.produced[l] += count;
                    nextAssembler.produced[l] -= count;
                }
            }
        }

        // AssemblerComponent.split_inc_level()がオリジナル
        private static int split_inc_level(ref int n, ref int m, int p) {
            int num = m / n;
            int num2 = m - num * n;
            n -= p;
            num2 -= n;
            m -= ((num2 > 0) ? (num * p + num2) : (num * p));
            return num;
        }
    }
}
