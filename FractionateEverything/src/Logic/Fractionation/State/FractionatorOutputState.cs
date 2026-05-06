using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FE.Logic.Fractionation.Recipes;

namespace FE.Logic.Fractionation.State;

/// <summary>
/// 分馏塔扩展产物状态缓存、存档和同步逻辑。
/// </summary>
public static class FractionatorOutputState {
    #region 分馏塔产物输出拓展

    /// <summary>
    /// 存储分馏塔所有产物。结构：
    /// (planetId, entityId) => List&lt;ProductOutputInfo&gt;
    /// </summary>
    public sealed class FractionatorExtraState {
        public readonly List<ProductOutputInfo> Products = [];
        public readonly ProductOutputBuffer ScratchOutputs = new();
        public byte CurrentOutputFlags;
        public ProductOutputInfo PrimaryProduct;
        private bool cachedRecipeValid;
        private int cachedFluidId;
        private ERecipe cachedRecipeType;
        private BaseRecipe cachedRecipe;
        private bool runtimeSchemaValid;
        private int runtimeSchemaFluidId;
        private ERecipe runtimeSchemaRecipeType;
        private BaseRecipe runtimeSchemaRecipe;
        private int runtimeSchemaProductId;
        private int runtimeSchemaProductCount;
        private bool fullProductCacheValid;
        private int fullProductCacheThreshold;
        private bool fullProductCacheValue;

        public BaseRecipe GetRecipe(ERecipe recipeType, int fluidId) {
            if (cachedRecipeValid
                && cachedFluidId == fluidId
                && cachedRecipeType == recipeType) {
                return cachedRecipe;
            }

            cachedRecipeValid = true;
            cachedFluidId = fluidId;
            cachedRecipeType = recipeType;
            cachedRecipe = RecipeManager.GetRecipe<BaseRecipe>(recipeType, fluidId);
            return cachedRecipe;
        }

        public T GetRecipe<T>(ERecipe recipeType, int fluidId) where T : BaseRecipe {
            return GetRecipe(recipeType, fluidId) as T;
        }

        public void ClearRecipeCache() {
            cachedRecipeValid = false;
            cachedFluidId = 0;
            cachedRecipeType = default;
            cachedRecipe = null;
            ClearRuntimeSchema();
        }

        public bool TryGetRuntimeSchema(ERecipe recipeType, int fluidId, BaseRecipe recipe, int productId,
            out ProductOutputInfo primaryProduct) {

            primaryProduct = null;
            if (!runtimeSchemaValid
                || runtimeSchemaFluidId != fluidId
                || runtimeSchemaRecipeType != recipeType
                || !ReferenceEquals(runtimeSchemaRecipe, recipe)
                || runtimeSchemaProductId != productId
                || runtimeSchemaProductCount != Products.Count) {
                return false;
            }
            if (runtimeSchemaProductCount <= 0) {
                return true;
            }
            if (PrimaryProduct == null || PrimaryProduct.itemId != productId) {
                return false;
            }

            primaryProduct = PrimaryProduct;
            return true;
        }

        public void MarkRuntimeSchema(ERecipe recipeType, int fluidId, BaseRecipe recipe, int productId,
            ProductOutputInfo primaryProduct) {

            runtimeSchemaValid = true;
            runtimeSchemaFluidId = fluidId;
            runtimeSchemaRecipeType = recipeType;
            runtimeSchemaRecipe = recipe;
            runtimeSchemaProductId = productId;
            runtimeSchemaProductCount = Products.Count;
            PrimaryProduct = primaryProduct;
        }

        public void ClearRuntimeSchema() {
            runtimeSchemaValid = false;
            runtimeSchemaFluidId = 0;
            runtimeSchemaRecipeType = default;
            runtimeSchemaRecipe = null;
            runtimeSchemaProductId = 0;
            runtimeSchemaProductCount = 0;
            PrimaryProduct = null;
        }

        public bool HasFullProduct(int countThreshold, bool forceRefresh = false) {
            if (!forceRefresh
                && fullProductCacheValid
                && fullProductCacheThreshold == countThreshold) {
                return fullProductCacheValue;
            }

            bool hasFullProduct = false;
            foreach (ProductOutputInfo product in Products) {
                if (product.count >= countThreshold) {
                    hasFullProduct = true;
                    break;
                }
            }

            fullProductCacheValid = true;
            fullProductCacheThreshold = countThreshold;
            fullProductCacheValue = hasFullProduct;
            return hasFullProduct;
        }

        public void InvalidateFullProductCache() {
            fullProductCacheValid = false;
        }

        public void MarkFullProductCache(int countThreshold) {
            fullProductCacheValid = true;
            fullProductCacheThreshold = countThreshold;
            fullProductCacheValue = true;
        }
    }

    private static readonly ConcurrentDictionary<(int, int), FractionatorExtraState> outputDic = [];

    /// <summary>
    /// 单个星球的分馏塔扩展状态数组和版本号。
    /// </summary>
    private sealed class FractionatorExtraStateArray(int length) {
        public readonly object SyncRoot = new();
        public FractionatorExtraState[] States = new FractionatorExtraState[length];
        public int Version;
    }

    private static readonly ConcurrentDictionary<int, FractionatorExtraStateArray> outputStateArraysByPlanet = [];
    private static int outputStateArrayGeneration;
    [ThreadStatic] private static int cachedOutputStatePlanetId;
    [ThreadStatic] private static int cachedOutputStateVersion;
    [ThreadStatic] private static int cachedOutputStateGeneration;
    [ThreadStatic] private static FractionatorExtraStateArray cachedOutputStateArray;

    private static FractionatorExtraState[] EnsureOutputStateArray(int planetId, int entityId,
        PlanetFactory factory = null) {
        int generation = Volatile.Read(ref outputStateArrayGeneration);
        FractionatorExtraStateArray cachedArray = cachedOutputStateArray;
        if (cachedArray != null
            && cachedOutputStatePlanetId == planetId
            && cachedOutputStateVersion == cachedArray.Version
            && cachedOutputStateGeneration == generation
            && (uint)entityId < (uint)cachedArray.States.Length) {
            return cachedArray.States;
        }

        int minLength = entityId + 1;
        if (factory?.entityPool != null && factory.entityPool.Length > minLength) {
            minLength = factory.entityPool.Length;
        }
        minLength = Math.Max(minLength, 64);

        FractionatorExtraStateArray stateArray =
            outputStateArraysByPlanet.GetOrAdd(planetId, _ => new FractionatorExtraStateArray(minLength));
        FractionatorExtraState[] states;
        int version;
        lock (stateArray.SyncRoot) {
            states = stateArray.States;
            if (states.Length <= entityId) {
                Array.Resize(ref states, minLength);
                stateArray.States = states;
                stateArray.Version++;
            }
            version = stateArray.Version;
        }

        cachedOutputStatePlanetId = planetId;
        cachedOutputStateVersion = version;
        cachedOutputStateGeneration = generation;
        cachedOutputStateArray = stateArray;
        return states;
    }

    public static void OutputExtendImport(BinaryReader r) {
        outputDic.Clear();
        outputStateArraysByPlanet.Clear();
        Interlocked.Increment(ref outputStateArrayGeneration);
        cachedOutputStatePlanetId = 0;
        cachedOutputStateVersion = 0;
        cachedOutputStateGeneration = 0;
        cachedOutputStateArray = null;
        int fractionatorNum = r.ReadInt32();
        for (int i = 0; i < fractionatorNum; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            FractionatorExtraState state = new();
            int outputKinds = r.ReadInt32();
            for (int j = 0; j < outputKinds; j++) {
                bool isMainOutput = r.ReadBoolean();
                int outputId = r.ReadInt32();
                int outputCount = r.ReadInt32();
                if (LDB.items.Exist(outputId)) {
                    continue;
                }
                state.Products.Add(new(isMainOutput, outputId, outputCount));
            }
            outputDic.TryAdd((planetId, entityId), state);
            EnsureOutputStateArray(planetId, entityId)[entityId] = state;
        }
    }

    public static void OutputExtendExport(BinaryWriter w) {
        w.Write(outputDic.Count);
        foreach (var p in outputDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            List<ProductOutputInfo> products = p.Value.Products;
            w.Write(products.Count);
            foreach (ProductOutputInfo outputItem in products) {
                w.Write(outputItem.isMainOutput);
                w.Write(outputItem.itemId);
                w.Write(outputItem.count);
            }
        }
    }

    public static void OutputExtendIntoOtherSave() {
        outputDic.Clear();
        outputStateArraysByPlanet.Clear();
        Interlocked.Increment(ref outputStateArrayGeneration);
        cachedOutputStatePlanetId = 0;
        cachedOutputStateVersion = 0;
        cachedOutputStateGeneration = 0;
        cachedOutputStateArray = null;
    }

    public static List<ProductOutputInfo> products(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        return fractionator.GetExtraState(factory).Products;
    }

    public static FractionatorExtraState GetExtraState(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        FractionatorExtraState[] states = EnsureOutputStateArray(planetId, entityId, factory);
        FractionatorExtraState state = states[entityId];
        if (state != null) {
            return state;
        }

        var key = (planetId, entityId);
        if (outputDic.TryGetValue(key, out state)) {
            states[entityId] = state;
            return state;
        }

        state = new FractionatorExtraState();
        states[entityId] = state;
        outputDic[key] = state;
        return state;
    }

    public static void ClearExtraState(PlanetFactory factory, int entityId) {
        if (factory == null || entityId <= 0) {
            return;
        }
        int planetId = factory.planetId;
        outputDic.TryRemove((planetId, entityId), out _);
        if (outputStateArraysByPlanet.TryGetValue(planetId, out FractionatorExtraStateArray stateArray)) {
            lock (stateArray.SyncRoot) {
                FractionatorExtraState[] states = stateArray.States;
                if (entityId < states.Length) {
                    states[entityId] = null;
                }
            }
        }
        if (cachedOutputStatePlanetId == planetId
            && cachedOutputStateArray != null
            && cachedOutputStateGeneration == Volatile.Read(ref outputStateArrayGeneration)) {
            lock (cachedOutputStateArray.SyncRoot) {
                FractionatorExtraState[] states = cachedOutputStateArray.States;
                if (cachedOutputStateVersion == cachedOutputStateArray.Version
                    && entityId < states.Length) {
                    states[entityId] = null;
                }
            }
        }
    }

    #endregion
}
