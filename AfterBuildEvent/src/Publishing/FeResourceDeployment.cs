using System;
using System.IO;

using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent;

static partial class AfterBuildEvent {
    /// <summary>资源 DLL 部署成功后，将 R2 中旧图标包移到仓库回收目录，保留可恢复副本。</summary>
    private static void RetireLegacyFeAssetBundle() {
        string pluginDir = Path.Combine(R2PluginsDir, "MengLei-FractionateEverything");
        string backupDir = Path.Combine(SolutionFullDir, ".codex", "trash", "fe-assets",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
        foreach (string name in new[] { "fe", "fe.old" }) {
            string source = Path.Combine(pluginDir, name);
            if (!File.Exists(source)) { continue; }
            Directory.CreateDirectory(backupDir);
            string target = Path.Combine(backupDir, name);
            File.Move(source, target);
            Console.WriteLine($"旧 FE 图标包已移至：{target}");
        }
    }
}
