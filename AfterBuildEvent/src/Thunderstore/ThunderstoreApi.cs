using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace AfterBuildEvent.Thunderstore;

/// <summary>Thunderstore 公开查询；只有明确的 HTTP 404 才视为未发布，其他失败均向上传递。</summary>
internal static class ThunderstoreApi {
    public const string Community = "dyson-sphere-program";
    private const string ApiRoot = "https://thunderstore.io/api/experimental/";

    /// <summary>没有现有 DSP 页面分类时的默认值；已有页面始终优先。</summary>
    public static readonly IReadOnlyDictionary<string, string[]> DefaultCategories =
        new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["FractionateEverything"] = ["cheats", "game-breaking", "quality-of-life"],
            ["GetDspData"] = ["info"],
        };

    /// <summary>读取包及社区信息；新包返回 null。</summary>
    public static JObject GetPackage(string name) => Get($"package/MengLei/{name}/", true);

    /// <summary>查询精确版本，防止把旧版或已停用版本误当成可重新上传的版本。</summary>
    public static JObject GetVersion(string name, string version) => Get($"package/MengLei/{name}/{version}/", true);

    /// <summary>建立官方显示名称与 slug 的映射，避免把页面名称直接写入 CLI 分类配置。</summary>
    public static Dictionary<string, string> GetCategoryCatalog() {
        JObject response = Get($"community/{Community}/category/", false);
        if (response["pagination"]?["next_link"]?.Type is not (null or JTokenType.Null)) {
            throw new InvalidDataException("社区分类出现分页，请更新分类读取流程后再发布。");
        }
        JArray results = response["results"] as JArray
            ?? throw new InvalidDataException("Thunderstore 分类响应缺少 results。");
        Dictionary<string, string> catalog = new(StringComparer.OrdinalIgnoreCase);
        foreach (JToken item in results) {
            string name = (string)item["name"];
            string slug = (string)item["slug"];
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug)) {
                throw new InvalidDataException("Thunderstore 分类缺少名称或 slug。");
            }
            catalog[name] = slug;
            catalog[slug] = slug;
        }
        return catalog;
    }

    /// <summary>沿用当前页面的分类；没有 DSP 分类时采用经选择的默认项。</summary>
    public static string[] ResolveCategories(string name, JObject package, Dictionary<string, string> catalog) {
        string[] names = GetListing(package)?["categories"]?.Values<string>().ToArray();
        if (names == null || names.Length == 0) {
            names = DefaultCategories[name];
        }
        return NormalizeCategories(names, catalog);
    }

    /// <summary>只返回 DSP 的社区条目，避免借其他社区的审核状态宣称发布完成。</summary>
    public static JToken GetListing(JObject package) => (package?["community_listings"] as JArray)?
        .FirstOrDefault(item => (string)item["community"] == Community);

    /// <summary>核对上传后的精确版本、激活状态、社区及分类；审核状态保留在回执中单独报告。</summary>
    public static bool IsPublished(ThunderstorePackage local, JObject version, JObject package,
        string[] categories, Dictionary<string, string> catalog) {
        JToken listing = GetListing(package);
        JArray remoteCategories = listing?["categories"] as JArray;
        return (string)version?["namespace"] == "MengLei"
            && (string)version?["name"] == local.Name
            && (string)version?["version_number"] == local.Version
            && (bool?)version?["is_active"] == true
            && remoteCategories != null
            && new HashSet<string>(categories, StringComparer.Ordinal).SetEquals(
                NormalizeCategories(remoteCategories.Values<string>(), catalog));
    }

    /// <summary>只允许当前社区存在的分类，未知名称阻止发布。</summary>
    private static string[] NormalizeCategories(IEnumerable<string> names, Dictionary<string, string> catalog) =>
        names.Select(name => catalog.TryGetValue(name, out string slug) ? slug
            : throw new InvalidDataException($"DSP 社区不存在分类：{name}"))
            .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();

    /// <summary>每次查询的连接和读取均限制为 30 秒；不带认证，也不自动重试。</summary>
    private static JObject Get(string route, bool allowNotFound) {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ApiRoot + route);
        request.Method = "GET";
        request.UserAgent = "MLJ_DSPmods-publishing/1.0";
        request.Accept = "application/json";
        request.Timeout = 30000;
        request.ReadWriteTimeout = 30000;
        try {
            using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using StreamReader reader = new(response.GetResponseStream(), Encoding.UTF8);
            return JObject.Parse(reader.ReadToEnd());
        } catch (WebException ex) when (ex.Response is HttpWebResponse response) {
            using (response) {
                if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) {
                    return null;
                }
                throw new InvalidOperationException(
                    $"Thunderstore 查询失败：HTTP {(int)response.StatusCode}，{request.RequestUri}", ex);
            }
        }
    }
}
