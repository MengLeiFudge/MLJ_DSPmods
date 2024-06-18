using FractionateEverything.Compatibility;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static FractionateEverything.FractionateEverything;

namespace FractionateEverything.Utils {
    public class RecipeHelper {
        private int currID = 1010;
        /// <summary>
        /// 存储所有配方的ID，避免冲突
        /// </summary>
        private readonly List<int> recipeIDList = [];
        public static int maxRowCount;
        public static int maxColumnCount;
        private int tab;
        private int row;
        private int column;
        private readonly int firstEmptyGridIndex;
        /// <summary>
        /// 存储所有配方的显示位置，避免冲突
        /// </summary>
        private Dictionary<RecipeProto, int> gridIndexDic = [];

        public RecipeHelper(int firstPage) {
            foreach (var recipe in LDB.recipes.dataArray) {
                if (recipe is { GridIndex: > 0 }) {
                    recipeIDList.Add(recipe.ID);
                }
            }
            //万物分馏只要两页图标
            tab = firstPage + 1;
            row = 6;
            column = 5;
            firstEmptyGridIndex = tab * 1000 + row * 100 + column + 1;
            if (GenesisBook.Enable) {
                maxRowCount = 7;
                maxColumnCount = 17;
            }
            else {
                maxRowCount = 8;
                maxColumnCount = 14;
            }
            foreach (var recipe in LDB.recipes.dataArray) {
                if (recipe is { GridIndex: > 0 }) {
                    gridIndexDic.Add(recipe, recipe.GridIndex);
                }
            }
        }

        public int GetUnusedRecipeID() {
            while (LDB.recipes.Select(currID) != null || recipeIDList.Contains(currID)) {
                currID++;
            }
            recipeIDList.Add(currID);
            return currID;
        }

        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        public void ModifyGridIndex(RecipeProto r, int tab, int rowColumn) {
            ModifyGridIndex(r, tab * 1000 + rowColumn);
        }

        public void ModifyGridIndex(RecipeProto r, int gridIndex) {
            //传入非正数表示使用末尾的空位
            if (gridIndex <= 0) {
                column++;
                if (column > maxColumnCount) {
                    column = 1;
                    row++;
                }
                int gridIndex0 = tab * 1000 + row * 100 + column;
                if (row > maxRowCount) {
                    LogWarning($"配方{r.name}图标超出显示范围，当前GridIndex={gridIndex0}");
                }
                gridIndexDic[r] = gridIndex0;
                r.GridIndex = gridIndex0;
                return;
            }
            //如果传入的位置已被占用，使用末尾的空位
            if (gridIndexDic.ContainsValue(gridIndex)) {
                ModifyGridIndex(r, -1);
                return;
            }
            //移除原来的位置，使用新位置
            if (gridIndexDic.ContainsKey(r)) {
                int oldGridIndex = gridIndexDic[r];
                gridIndexDic.Remove(r);
                //如果原来位置是末尾空位，还需要前移所有末尾配方
                if (oldGridIndex / 1000 == tab && oldGridIndex >= firstEmptyGridIndex) {
                    //查找所有在其之后的配方，并前移
                    Dictionary<RecipeProto, int> dic = [];
                    foreach (KeyValuePair<RecipeProto, int> p in gridIndexDic) {
                        if (p.Value / 1000 == tab && p.Value > oldGridIndex) {
                            int row0 = p.Value % 1000 / 100;
                            int column0 = p.Value % 1000 % 100;
                            if (column0 >= 2) {
                                p.Key.GridIndex = p.Value - 1;
                            }
                            else {
                                p.Key.GridIndex = tab * 1000 + (row0 - 1) * 100 + maxColumnCount;
                            }
                            dic.Add(p.Key, p.Key.GridIndex);
                        }
                        else {
                            dic.Add(p.Key, p.Value);
                        }
                    }
                    gridIndexDic = dic;
                    //row和column也要前移
                    column--;
                    if (column <= 0) {
                        row--;
                        column = maxColumnCount;
                    }
                }
            }
            gridIndexDic.Add(r, gridIndex);
            r.GridIndex = gridIndex;
        }
    }
}
