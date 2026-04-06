using System;

namespace VanillaCurveSim;

internal sealed class FeWarehouse {
    public double[] MatrixCounts { get; } = new double[6];
    public double Fragments { get; set; }
    public double GrowthPoolPoints { get; set; }

    public double InteractionEmbryos { get; set; }
    public double MineralEmbryos { get; set; }
    public double PointEmbryos { get; set; }
    public double ConversionEmbryos { get; set; }
    public double RectificationEmbryos { get; set; }
    public double DirectionalEmbryos { get; set; }

    public double OpeningLockedRecipes { get; set; }
    public double OpeningUpgradeableCharges { get; set; }
    public double RecipeUnlockRewards { get; set; }
    public double RecipeUpgradeRewards { get; set; }

    public static FeWarehouse CreateInitial(bool isSpeedrun) {
        return new FeWarehouse {
            Fragments = isSpeedrun ? 80 : 120,
            OpeningLockedRecipes = isSpeedrun ? 8 : 10,
            OpeningUpgradeableCharges = isSpeedrun ? 6 : 8,
        };
    }

    public FeWarehouse Clone() {
        var clone = new FeWarehouse {
            Fragments = Fragments,
            GrowthPoolPoints = GrowthPoolPoints,
            InteractionEmbryos = InteractionEmbryos,
            MineralEmbryos = MineralEmbryos,
            PointEmbryos = PointEmbryos,
            ConversionEmbryos = ConversionEmbryos,
            RectificationEmbryos = RectificationEmbryos,
            DirectionalEmbryos = DirectionalEmbryos,
            OpeningLockedRecipes = OpeningLockedRecipes,
            OpeningUpgradeableCharges = OpeningUpgradeableCharges,
            RecipeUnlockRewards = RecipeUnlockRewards,
            RecipeUpgradeRewards = RecipeUpgradeRewards,
        };
        Array.Copy(MatrixCounts, clone.MatrixCounts, MatrixCounts.Length);
        return clone;
    }

    public void AddMatrix(int stageIndex, double count) {
        if (stageIndex < 0 || stageIndex >= MatrixCounts.Length || count <= 0.0) {
            return;
        }
        MatrixCounts[stageIndex] += count;
    }

    public bool TryConsumeMatrix(int stageIndex, double count) {
        if (stageIndex < 0 || stageIndex >= MatrixCounts.Length || count <= 0.0) {
            return false;
        }
        if (MatrixCounts[stageIndex] + 1e-6 < count) {
            return false;
        }
        MatrixCounts[stageIndex] -= count;
        return true;
    }

    public bool TryConsumeFragments(double count) {
        if (count <= 0.0) {
            return true;
        }
        if (Fragments + 1e-6 < count) {
            return false;
        }
        Fragments -= count;
        return true;
    }

    public bool TryConsumePoolPoints(double count) {
        if (count <= 0.0) {
            return true;
        }
        if (GrowthPoolPoints + 1e-6 < count) {
            return false;
        }
        GrowthPoolPoints -= count;
        return true;
    }

    public double GetNormalizedUtility() {
        double utility = 0.0;
        utility += MatrixCounts[0] * 1.0;
        utility += MatrixCounts[1] * 1.1;
        utility += MatrixCounts[2] * 1.25;
        utility += MatrixCounts[3] * 1.45;
        utility += MatrixCounts[4] * 1.75;
        utility += MatrixCounts[5] * 2.10;
        utility += Fragments * 0.02;
        utility += GrowthPoolPoints * 0.10;
        utility += InteractionEmbryos * 4.0;
        utility += MineralEmbryos * 5.0;
        utility += PointEmbryos * 5.0;
        utility += ConversionEmbryos * 5.5;
        utility += RectificationEmbryos * 6.0;
        utility += DirectionalEmbryos * 8.0;
        utility += RecipeUnlockRewards * 6.5;
        utility += RecipeUpgradeRewards * 3.0;
        return utility;
    }

    public void AddRecipeSlotsForStage(int stageIndex, bool isSpeedrun) {
        double lockedDelta = isSpeedrun ? 1 + stageIndex : 2 + stageIndex;
        double upgradeDelta = isSpeedrun ? 1 + stageIndex * 0.5 : 1 + stageIndex;
        OpeningLockedRecipes += lockedDelta;
        OpeningUpgradeableCharges += upgradeDelta;
    }
}
