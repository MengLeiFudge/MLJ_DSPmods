using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static global::GetDspData.GetDspData;

namespace GetDspData.Export;

/// <summary>
/// 按请求渲染并导出游戏中的图标。
/// </summary>
internal static class IconExporter {
    private const string DynamicIconPathPrefix = "Assets/texpack/";
    private const int ExportIconSize = 80;

    /// <summary>
    /// 解析物品计算器图标名；动态 Assets/texpack 路径优先，否则使用 Sprite 名。
    /// </summary>
    /// <param name="proto">物品原型。</param>
    /// <returns>不含扩展名的图标键；无可用名称时为空字符串。</returns>
    internal static string ResolveIconName(ItemProto proto) =>
        ResolveIconName(proto?.IconPath, proto?.iconSprite?.name);

    /// <summary>
    /// 解析配方计算器图标名；动态 Assets/texpack 路径优先，否则使用 Sprite 名。
    /// </summary>
    /// <param name="proto">配方原型。</param>
    /// <returns>不含扩展名的图标键；无可用名称时为空字符串。</returns>
    internal static string ResolveIconName(RecipeProto proto) =>
        ResolveIconName(proto?.IconPath, proto?.iconSprite?.name);

    /// <summary>
    /// 从动态图标路径提取最终文件名；路径不匹配时回退到已加载 Sprite 名。
    /// </summary>
    /// <param name="iconPath">原型 IconPath；动态资源以 Assets/texpack/ 开头。</param>
    /// <param name="fallbackIconName">非动态资源使用的 Sprite 名。</param>
    /// <returns>计算器所需图标键；两个来源均不可用时为空字符串。</returns>
    internal static string ResolveIconName(string iconPath, string fallbackIconName) {
        if (!string.IsNullOrEmpty(iconPath)
            && iconPath.StartsWith(DynamicIconPathPrefix, StringComparison.OrdinalIgnoreCase)) {
            string dynamicIconName = Path.GetFileName(iconPath.Substring(DynamicIconPathPrefix.Length).Trim());
            if (!string.IsNullOrEmpty(dynamicIconName)) {
                return Path.GetFileNameWithoutExtension(dynamicIconName);
            }
        }
        return fallbackIconName ?? "";
    }

    /// <summary>
    /// 处理 AfterBuildEvent 写入的单次图标请求，将当前 LDB 图标渲染为 PNG 并写出成功/失败 marker。
    /// 请求文件不存在时保持无副作用返回。
    /// </summary>
    /// <param name="requestPath">单次图标导出请求文件。</param>
    internal static void ExportRequestedIcons(string requestPath) {
        if (!File.Exists(requestPath)) {
            return;
        }

        string markerPath = "";
        try {
            JObject request = JObject.Parse(File.ReadAllText(requestPath));
            string targetMod = request.Value<string>("TargetMod") ?? "";
            string outputDir = request.Value<string>("OutputDir") ?? "";
            markerPath = request.Value<string>("MarkerPath") ?? "";
            if (string.IsNullOrWhiteSpace(targetMod)
                || string.IsNullOrWhiteSpace(outputDir)
                || string.IsNullOrWhiteSpace(markerPath)) {
                LogWarning("图标导出请求缺少 TargetMod / OutputDir / MarkerPath");
                return;
            }

            HashSet<string> lowerPriorityIcons = CollectLowerPriorityIcons(request["LowerPriorityDirs"] as JArray);
            HashSet<string> requestedIcons = CollectRequestedIcons(request["IconNames"] as JArray);
            Directory.CreateDirectory(outputDir);

            int exported = 0;
            int skippedNotRequested = 0;
            int skippedLowerPriority = 0;
            int skippedDuplicate = 0;
            int failed = 0;
            HashSet<string> handledIcons = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string iconName, Sprite sprite) in EnumerateCurrentIconSprites()) {
                if (string.IsNullOrWhiteSpace(iconName) || sprite == null) {
                    continue;
                }
                if (requestedIcons.Count > 0 && !requestedIcons.Contains(iconName)) {
                    skippedNotRequested++;
                    continue;
                }
                if (!handledIcons.Add(iconName)) {
                    skippedDuplicate++;
                    continue;
                }
                if (lowerPriorityIcons.Contains(iconName)) {
                    skippedLowerPriority++;
                    continue;
                }

                string outputPath = Path.Combine(outputDir, $"{SanitizeFileName(iconName)}.png");
                if (TryWriteSpritePng(sprite, outputPath)) {
                    exported++;
                } else {
                    failed++;
                }
            }

            JObject marker = new() {
                { "TargetMod", targetMod },
                { "OutputDir", outputDir },
                { "Exported", exported },
                { "SkippedNotRequested", skippedNotRequested },
                { "SkippedLowerPriority", skippedLowerPriority },
                { "SkippedDuplicate", skippedDuplicate },
                { "Failed", failed },
                { "Handled", handledIcons.Count },
            };
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath) ?? ".");
            File.WriteAllText(markerPath, marker.ToString(Formatting.Indented), Encoding.UTF8);
            LogInfo(
                $"已导出 {targetMod} 图标：{exported} 个，跳过非请求 {skippedNotRequested} 个，跳过已存在/低优先级 {skippedLowerPriority} 个，失败 {failed} 个");
        }
        catch (Exception ex) {
            LogError($"导出图标失败：{ex}");
            if (!string.IsNullOrWhiteSpace(markerPath)) {
                JObject marker = new() {
                    { "Failed", true },
                    { "Error", ex.ToString() },
                };
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath) ?? ".");
                File.WriteAllText(markerPath, marker.ToString(Formatting.Indented), Encoding.UTF8);
            }
        }
    }

    /// <summary>
    /// 收集低优先级目录中已存在的 PNG 文件名，用于避免当前模组重复导出。
    /// </summary>
    /// <param name="lowerPriorityDirs">目录路径 JSON 数组；缺失目录会被忽略。</param>
    /// <returns>按大小写不敏感去重的无扩展名图标集合。</returns>
    private static HashSet<string> CollectLowerPriorityIcons(JArray lowerPriorityDirs) {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        if (lowerPriorityDirs == null) {
            return result;
        }

        foreach (JToken token in lowerPriorityDirs) {
            string dirPath = token.Value<string>();
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) {
                continue;
            }

            foreach (string filePath in Directory.GetFiles(dirPath, "*.png")) {
                result.Add(Path.GetFileNameWithoutExtension(filePath));
            }
        }
        return result;
    }

    /// <summary>
    /// 从请求 JSON 中读取需要导出的图标键，并按大小写不敏感去重。
    /// </summary>
    /// <param name="iconNames">图标名 JSON 数组；为 null 时返回空集合。</param>
    /// <returns>非空图标键集合。</returns>
    private static HashSet<string> CollectRequestedIcons(JArray iconNames) {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        if (iconNames == null) {
            return result;
        }

        foreach (JToken token in iconNames) {
            string iconName = token.Value<string>();
            if (!string.IsNullOrWhiteSpace(iconName)) {
                result.Add(iconName);
            }
        }
        return result;
    }

    /// <summary>
    /// 按物品、配方、科技的 LDB 顺序枚举当前组合可见的图标键与 Sprite。
    /// </summary>
    /// <returns>允许名称或 Sprite 为空的图标候选序列，由调用方过滤。</returns>
    private static IEnumerable<(string iconName, Sprite sprite)> EnumerateCurrentIconSprites() {
        foreach (ItemProto item in LDB.items.dataArray) {
            yield return (ResolveIconName(item), item.iconSprite);
        }
        foreach (RecipeProto recipe in LDB.recipes.dataArray) {
            yield return (ResolveIconName(recipe), recipe.iconSprite);
        }
        foreach (TechProto tech in LDB.techs.dataArray) {
            yield return (ResolveIconName(tech.IconPath, tech.iconSprite?.name), tech.iconSprite);
        }
    }

    /// <summary>
    /// 将 Sprite 的纹理子矩形渲染到 80x80 透明画布并编码为 PNG；始终恢复 RenderTexture 并释放临时资源。
    /// </summary>
    /// <param name="sprite">待导出的游戏图标 Sprite。</param>
    /// <param name="outputPath">PNG 目标路径；父目录不存在时创建。</param>
    /// <returns>成功写出非空 PNG 时为 <c>true</c>；渲染或写文件失败时记录警告并返回 <c>false</c>。</returns>
    private static bool TryWriteSpritePng(Sprite sprite, string outputPath) {
        Texture2D texture = null;
        Material material = null;
        RenderTexture renderTexture = null;
        RenderTexture previous = RenderTexture.active;

        try {
            Texture2D sourceTexture = sprite.texture;
            Rect rect = sprite.textureRect;
            renderTexture = RenderTexture.GetTemporary(ExportIconSize, ExportIconSize, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));

            material = new Material(Shader.Find("Unlit/Transparent"));
            material.mainTextureScale =
                new Vector2(rect.width / sourceTexture.width, rect.height / sourceTexture.height);
            material.mainTextureOffset = new Vector2(rect.x / sourceTexture.width, rect.y / sourceTexture.height);
            Graphics.Blit(sourceTexture, renderTexture, material);

            texture = new Texture2D(ExportIconSize, ExportIconSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, ExportIconSize, ExportIconSize), 0, 0);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            if (png == null || png.Length == 0) {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllBytes(outputPath, png);
            return true;
        }
        catch (Exception ex) {
            LogWarning($"导出图标失败：{sprite.name} -> {outputPath}，{ex.Message}");
            return false;
        }
        finally {
            RenderTexture.active = previous;
            if (renderTexture != null) {
                RenderTexture.ReleaseTemporary(renderTexture);
            }
            if (material != null) {
                UnityEngine.Object.DestroyImmediate(material);
            }
            if (texture != null) {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>
    /// 将平台非法文件名字符替换为下划线并移除首尾空白，供 PNG 文件名使用。
    /// </summary>
    /// <param name="fileName">原始图标键。</param>
    /// <returns>可作为单个文件名片段的清理结果。</returns>
    private static string SanitizeFileName(string fileName) {
        foreach (char invalidChar in Path.GetInvalidFileNameChars()) {
            fileName = fileName.Replace(invalidChar, '_');
        }
        return fileName.Trim();
    }
}
