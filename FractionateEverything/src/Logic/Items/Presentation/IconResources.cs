using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using CommonAPI;
using HarmonyLib;
using UnityEngine;

namespace FE.Logic.Items.Presentation;

/// <summary>从独立资源程序集加载图标，并为原型预加载提供共享的纹理和精灵。</summary>
internal static class IconResources {
    private const string PathPrefix = "Assets/fe/";
    private const string ResourcePrefix = "FE.Icons.";
    private const string AssemblyFileName = "FractionateEverything.Resources.dll";
    private static readonly Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);

    /// <summary>在主线程启动阶段加载全部图标；缓存与游戏原型同寿命，不在切档时释放。</summary>
    /// <param name="modPath">主插件所在目录，资源程序集与其同目录部署。</param>
    internal static void Load(string modPath) {
        string path = Path.Combine(modPath, AssemblyFileName);
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"缺少 FE 图标资源：{path}。请完整安装模组发布包。", path);
        }
        Assembly assembly = Assembly.LoadFrom(path);
        foreach (string resource in assembly.GetManifestResourceNames()) {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                || !resource.EndsWith(".png", StringComparison.Ordinal)) { continue; }
            string name = resource.Substring(ResourcePrefix.Length, resource.Length - ResourcePrefix.Length - 4);
            using Stream stream = assembly.GetManifestResourceStream(resource);
            using MemoryStream bytes = new();
            stream.CopyTo(bytes);
            Texture2D decoded = new(2, 2, TextureFormat.RGBA32, false);
            Texture2D texture = null;
            Sprite sprite = null;
            try {
                if (!ImageConversion.LoadImage(decoded, bytes.ToArray()) || decoded.width != 80 || decoded.height != 80) {
                    throw new InvalidDataException($"FE 图标必须是 80×80 PNG：{resource}");
                }
                // LoadImage 会改写纹理格式；统一转换为图集的 ARGB32，保证 CopyTexture 格式一致。
                texture = new Texture2D(80, 80, TextureFormat.ARGB32, true) {
                    name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels32(decoded.GetPixels32());
                texture.Apply(true, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 80, 80), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = name;
                sprites.Add(PathPrefix + name, sprite);
            } catch {
                if (sprite != null) { UnityEngine.Object.Destroy(sprite); }
                if (texture != null) { UnityEngine.Object.Destroy(texture); }
                throw;
            } finally {
                UnityEngine.Object.Destroy(decoded);
            }
        }
        if (sprites.Count == 0) {
            throw new InvalidDataException($"FE 资源程序集没有图标：{path}。请完整安装模组发布包。");
        }
    }

    /// <summary>仅接管 FE 图标路径；原版与其他模组的资源仍由原有加载链处理。</summary>
    /// <param name="path">原型声明的资源路径。</param>
    /// <param name="systemTypeInstance">原版请求的精灵或纹理类型。</param>
    /// <param name="__result">缓存中的共享资源。</param>
    /// <returns>非 FE 图标请求继续原有加载；FE 图标请求在本方法完成。</returns>
    [HarmonyPrefix]
    [HarmonyBefore(CommonAPIPlugin.GUID)]
    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), typeof(string), typeof(Type))]
    private static bool Resources_Load_Prefix(string path, Type systemTypeInstance, ref UnityEngine.Object __result) {
        if (path == null || !path.StartsWith(PathPrefix, StringComparison.Ordinal)
            || (systemTypeInstance != typeof(Sprite) && systemTypeInstance != typeof(Texture2D))) { return true; }
        if (!sprites.TryGetValue(path, out Sprite sprite)) {
            throw new InvalidDataException($"FE 图标不存在：{path}。请核对资源程序集与主插件是否配套。");
        }
        __result = systemTypeInstance == typeof(Sprite) ? sprite : sprite.texture;
        return false;
    }
}
