using System;

namespace VanillaCurveSim;

internal static class SimulatorSelfCheck {
    public static void RunAll() {
        FeScenarioSimulator.RunSelfCheck();
    }

    public static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException($"Simulator self-check failed: {message}");
        }
    }
}
