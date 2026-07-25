using System;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.Logic.Progression.Presentation;

/// <summary>
/// 启动信号常驻 HUD：在原版目标面板展开位置下方持续显示当前方向/距离。
/// 独立于原版 GoalSystem，不注册 GoalProto、不写入存档、不新增 Harmony patch；
/// 由 <see cref="FE.Logic.Progression.CivilizationRecoveryManager"/> 在 Player.GameTick 调用链中驱动。
/// 翻译键由启动期（TechManager.AddTranslations）统一注册，本类不再做运行期注册或刷新。
/// </summary>
public static class StarterSignalHud {
    /// <summary>
    /// HUD 在父级 UI 坐标系中的固定宽度。
    /// </summary>
    private const float HudWidth = 340f;

    /// <summary>
    /// HUD 在父级 UI 坐标系中的固定高度，可容纳英文状态的两行文本。
    /// </summary>
    private const float HudHeight = 60f;

    /// <summary>
    /// HUD 顶边与原版目标面板底边之间的 UI 坐标间距。
    /// </summary>
    private const float PanelGap = 8f;

    /// <summary>
    /// HUD 背景色：与原版目标区协调的克制深色半透明，使用普通纯色 Image（无圆角精灵依赖）。
    /// </summary>
    private static readonly Color HudBackgroundColor = new(26f / 255f, 30f / 255f, 36f / 255f, 0.82f);

    /// <summary>
    /// 罗盘八向翻译键，按玩家朝向顺时针排列：前、右前、右、右后、后、左后、左、左前。
    /// </summary>
    private static readonly string[] compassKeys = [
        "启动信号方向-前",
        "启动信号方向-右前",
        "启动信号方向-右",
        "启动信号方向-右后",
        "启动信号方向-后",
        "启动信号方向-左后",
        "启动信号方向-左",
        "启动信号方向-左前",
    ];

    /// <summary>
    /// 复用的世界坐标角点缓冲，避免每帧为目标面板定位分配数组。
    /// </summary>
    private static readonly Vector3[] cornerBuffer = new Vector3[4];

    /// <summary>
    /// 当前存档场景中的 HUD 根节点；切档时销毁并清空。
    /// </summary>
    private static RectTransform hudRoot;

    /// <summary>
    /// HUD 内承载富文本状态的原版字体文本节点。
    /// </summary>
    private static Text hudText;

    /// <summary>
    /// 创建 HUD 时的目标面板父节点，用于检测场景或 UI 根节点切换。
    /// </summary>
    private static RectTransform cachedGoalParent;

    /// <summary>
    /// 隐藏 HUD，但保留已创建对象以便复用。
    /// </summary>
    public static void Hide() {
        if (hudRoot != null && hudRoot.gameObject.activeSelf) {
            hudRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 显示“返回出生星”提示。UI 尚未就绪时安全返回并隐藏残留 HUD。
    /// </summary>
    public static void ShowReturnToBirth() {
        if (!EnsureCreated()) {
            Hide();
            return;
        }

        hudText.text = ComposeLine("启动信号HUD-标题", "启动信号HUD-返回出生星".Translate());
        hudRoot.gameObject.SetActive(true);
        UpdatePosition();
    }

    /// <summary>
    /// 显示信号相对玩家朝向的罗盘方向与距离。UI 尚未就绪时安全返回并隐藏残留 HUD。
    /// </summary>
    /// <param name="playerPosition">玩家当前位置，用于计算距离与方向。</param>
    /// <param name="signalPosition">启动信号位置，与 <paramref name="playerPosition"/> 同坐标系。</param>
    public static void ShowSignal(Vector3 playerPosition, Vector3 signalPosition) {
        if (!EnsureCreated()) {
            Hide();
            return;
        }

        Player player = GameMain.mainPlayer;
        int compass = player != null
            ? CompassIndex(player.transform.forward, player.transform.right, signalPosition - playerPosition)
            : 0;
        int meters = Mathf.CeilToInt(Vector3.Distance(playerPosition, signalPosition));
        string body = string.Format("启动信号HUD-方向距离".Translate(), compassKeys[compass].Translate(), meters);
        hudText.text = ComposeLine("启动信号HUD-标题", body);
        hudRoot.gameObject.SetActive(true);
        UpdatePosition();
    }

    /// <summary>
    /// 切档或导入时彻底销毁 HUD，避免残留旧存档的目标块；先隐藏再销毁，规避 Unity 延迟销毁的一帧残影。
    /// </summary>
    public static void DestroyForSaveSwitch() {
        if (hudRoot != null) {
            hudRoot.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(hudRoot.gameObject);
        }

        hudRoot = null;
        hudText = null;
        cachedGoalParent = null;
    }

    /// <summary>
    /// 拼装 HUD 单行富文本：橙色标题 + 白色正文。
    /// </summary>
    /// <param name="titleKey">标题翻译键。</param>
    /// <param name="body">已翻译的正文文本。</param>
    /// <returns>富文本字符串。</returns>
    private static string ComposeLine(string titleKey, string body) {
        return $"{titleKey.Translate().WithColor(Orange)}  {body.WithColor(White)}";
    }

    /// <summary>
    /// 根据玩家朝向把指向信号的水平向量映射为 0..7 罗盘索引。
    /// </summary>
    /// <param name="forward">玩家朝向。</param>
    /// <param name="right">玩家右侧方向。</param>
    /// <param name="toSignal">从玩家指向信号的向量。</param>
    /// <returns>0=前，1=右前，2=右，3=右后，4=后，5=左后，6=左，7=左前。</returns>
    private static int CompassIndex(Vector3 forward, Vector3 right, Vector3 toSignal) {
        if (toSignal.sqrMagnitude < 1e-6f) {
            return 0;
        }

        Vector3 fwd = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        Vector3 rgt = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
        float angle = Mathf.Atan2(Vector3.Dot(toSignal, rgt), Vector3.Dot(toSignal, fwd)) * Mathf.Rad2Deg;
        return ((int)Math.Round(angle / 45f, MidpointRounding.AwayFromZero) + 8) % 8;
    }

    /// <summary>
    /// 懒创建 HUD 并确保挂载在当前目标面板父节点下；UI 尚未就绪、玩家死亡或原版目标面板因全屏界面关闭时返回 false。
    /// </summary>
    /// <returns>HUD 已就绪且当前游戏界面允许显示时返回 true。</returns>
    private static bool EnsureCreated() {
        UIGame uiGame = UIRoot.instance?.uiGame;
        Player player = GameMain.mainPlayer;
        if (uiGame == null || !uiGame.active || uiGame.dashboard.active || uiGame.dysonEditor.active
            || player == null || !player.isAlive) {
            return false;
        }

        UIGoalPanel goalPanel = uiGame.goalPanel;
        if (goalPanel == null || goalPanel.rect == null) {
            return false;
        }

        RectTransform goalParent = goalPanel.rect.parent as RectTransform;
        if (goalParent == null) {
            return false;
        }

        // 外部（切档、场景卸载）销毁过 HUD 时，清理残留引用以便重建。
        if (hudRoot == null) {
            hudText = null;
            cachedGoalParent = null;
        } else if (hudRoot.parent != goalParent || cachedGoalParent != goalParent) {
            DestroyForSaveSwitch();
        }

        if (hudRoot == null) {
            CreateHud(goalParent);
            cachedGoalParent = goalParent;
        }

        return hudRoot != null;
    }

    /// <summary>
    /// 创建 HUD 节点：纯色深色背景 + 克隆自原版文本的 Text，挂到目标面板父节点下。
    /// </summary>
    /// <param name="goalParent">目标面板所在父节点，HUD 作为其兄弟节点。</param>
    private static void CreateHud(RectTransform goalParent) {
        Text sourceText = UIRoot.instance.uiGame.assemblerWindow?.stateText;
        if (sourceText == null) {
            return;
        }

        var rootObj = new GameObject("fe-starter-signal-hud", typeof(RectTransform), typeof(Image));
        hudRoot = rootObj.GetComponent<RectTransform>();
        hudRoot.SetParent(goalParent, false);
        hudRoot.pivot = new Vector2(0.5f, 1f);
        hudRoot.sizeDelta = new Vector2(HudWidth, HudHeight);
        // 锚点设为父节点顶部中点；实际位置由 UpdatePosition 用世界坐标覆盖。
        hudRoot.anchorMin = new Vector2(0.5f, 1f);
        hudRoot.anchorMax = new Vector2(0.5f, 1f);

        // 普通纯色 Image（无精灵），base.OnPopulateMesh 绘制实色矩形；不依赖 FE.Theme。
        Image background = rootObj.GetComponent<Image>();
        background.color = HudBackgroundColor;
        background.raycastTarget = false;

        hudText = UnityEngine.Object.Instantiate(sourceText, hudRoot);
        hudText.gameObject.name = "fe-starter-signal-hud-text";
        hudText.gameObject.SetActive(true);
        hudText.text = string.Empty;
        hudText.color = White;
        hudText.alignment = TextAnchor.MiddleCenter;
        hudText.fontSize = 15;
        hudText.supportRichText = true;
        hudText.horizontalOverflow = HorizontalWrapMode.Wrap;
        // 英文返回提示可能换两行：52px 文本区可保留行间余量，Truncate 防止第三行越界。
        hudText.verticalOverflow = VerticalWrapMode.Truncate;
        hudText.raycastTarget = false;
        RectTransform textRect = hudText.rectTransform;
        textRect.localScale = Vector3.one;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        hudRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 把 HUD 顶部对齐到原版目标面板当前底边下方 <see cref="PanelGap"/> 像素处。
    /// X 方向用 <c>expandPosX</c> 校正，使目标面板 collapse/off 时 HUD 仍固定在展开位置，仅 Y 跟随其底边。
    /// </summary>
    private static void UpdatePosition() {
        UIGoalPanel goalPanel = UIRoot.instance == null ? null : UIRoot.instance.uiGame?.goalPanel;
        if (goalPanel == null || goalPanel.rect == null || hudRoot == null) {
            return;
        }

        RectTransform goalParent = goalPanel.rect.parent as RectTransform;
        if (goalParent == null) {
            return;
        }

        goalPanel.rect.GetWorldCorners(cornerBuffer);
        Vector3 bottomCenter = (cornerBuffer[0] + cornerBuffer[3]) * 0.5f;
        // 目标面板 collapse/off 时 anchoredPosition.x 会移到 collapsePosX；按 expandPosX 偏移把 HUD 固定在展开列。
        // X 校正和垂直间距都从父级 UI 坐标转换到世界坐标，保持 Canvas 缩放下的视觉距离一致。
        float xCorrection = goalPanel.expandPosX - goalPanel.rect.anchoredPosition.x;
        Vector3 expandedPositionOffset = goalParent.TransformVector(new Vector3(xCorrection, -PanelGap, 0f));
        hudRoot.position = bottomCenter + expandedPositionOffset;
    }
}
