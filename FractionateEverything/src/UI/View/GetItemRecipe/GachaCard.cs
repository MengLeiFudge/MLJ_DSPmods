using System.Collections;
using FE.Logic.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.View.GetItemRecipe;

public class GachaCard : MonoBehaviour {
    // 卡背面Image（默认显示）
    private Image _backImage;
    // 卡正面Image（翻转后显示）
    private Image _frontImage;
    // 稀有度光晕Image
    private Image _glowImage;
    // 点击按钮
    private Button _button;
    
    // 卡片数据
    public GachaResult Result { get; private set; }
    public bool IsRevealed { get; private set; }
    
    // 翻转完成回调
    public System.Action<GachaCard> OnRevealed;
    
    // 稀有度颜色
    private static readonly Color ColorC = new(0.8f, 0.8f, 0.8f);   // 白
    private static readonly Color ColorB = new(0.4f, 0.9f, 0.4f);   // 绿
    private static readonly Color ColorA = new(0.4f, 0.6f, 1.0f);   // 蓝
    private static readonly Color ColorS = new(1.0f, 0.85f, 0.2f);  // 金
    
    // 工厂方法：在指定父节点创建卡片
    public static GachaCard Create(RectTransform parent, float x, float y, float w, float h) {
        var go = new GameObject("GachaCard");
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);

        var card = go.AddComponent<GachaCard>();

        // 光晕
        var glowObj = new GameObject("Glow");
        var glowRect = glowObj.AddComponent<RectTransform>();
        glowRect.SetParent(rect, false);
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.sizeDelta = new Vector2(20, 20); // 稍微大一点
        card._glowImage = glowObj.AddComponent<Image>();
        card._glowImage.gameObject.SetActive(false);

        // 背面
        var backObj = new GameObject("Back");
        var backRect = backObj.AddComponent<RectTransform>();
        backRect.SetParent(rect, false);
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.sizeDelta = Vector2.zero;
        card._backImage = backObj.AddComponent<Image>();
        card._backImage.color = Color.gray; // 默认灰色作为背面

        // 正面
        var frontObj = new GameObject("Front");
        var frontRect = frontObj.AddComponent<RectTransform>();
        frontRect.SetParent(rect, false);
        frontRect.anchorMin = Vector2.zero;
        frontRect.anchorMax = Vector2.one;
        frontRect.sizeDelta = Vector2.zero;
        card._frontImage = frontObj.AddComponent<Image>();
        card._frontImage.gameObject.SetActive(false);

        // 按钮
        card._button = go.AddComponent<Button>();
        card._button.onClick.AddListener(card.Reveal);

        return card;
    }
    
    public void SetResult(GachaResult result) {
        Result = result;
        IsRevealed = false;
        _button.interactable = true;
        _backImage.gameObject.SetActive(true);
        _frontImage.gameObject.SetActive(false);
        _glowImage.gameObject.SetActive(false);
        transform.localScale = Vector3.one;
    }

    public void ResetToBack() {
        IsRevealed = false;
        _button.interactable = false;
        _backImage.gameObject.SetActive(true);
        _frontImage.gameObject.SetActive(false);
        _glowImage.gameObject.SetActive(false);
        transform.localScale = Vector3.one;
    }
    
    // 触发翻转动画
    public void Reveal() {
        if (IsRevealed) return;
        StartCoroutine(FlipCoroutine());
    }
    
    // 立即显示结果（无动画）
    public void RevealImmediate() {
        if (IsRevealed) return;
        IsRevealed = true;
        _button.interactable = false;
        
        _backImage.gameObject.SetActive(false);
        _frontImage.gameObject.SetActive(true);
        ApplyRarityStyle();
        
        transform.localScale = Vector3.one;
        
        if (Result.Rarity == GachaRarity.S) {
            StartCoroutine(GlowPulse());
        }
        
        OnRevealed?.Invoke(this);
    }
    
    private IEnumerator FlipCoroutine() {
        IsRevealed = true;
        _button.interactable = false;
        float duration = 0.15f;
        
        // 第一阶段：scaleX 1→0
        float t = 0;
        Vector3 scale = transform.localScale;
        while (t < duration) {
            t += Time.deltaTime;
            scale.x = Mathf.Lerp(1f, 0f, t / duration);
            transform.localScale = scale;
            yield return null;
        }
        
        // 切换图片：隐藏背面，显示正面
        _backImage.gameObject.SetActive(false);
        _frontImage.gameObject.SetActive(true);
        ApplyRarityStyle();
        
        // 第二阶段：scaleX 0→1
        t = 0;
        while (t < duration) {
            t += Time.deltaTime;
            scale.x = Mathf.Lerp(0f, 1f, t / duration);
            transform.localScale = scale;
            yield return null;
        }
        scale.x = 1f;
        transform.localScale = scale;
        
        // S级触发光晕脉冲
        if (Result.Rarity == GachaRarity.S) {
            StartCoroutine(GlowPulse());
        }
        
        OnRevealed?.Invoke(this);
    }

    private void ApplyRarityStyle() {
        Color c = Result.Rarity switch {
            GachaRarity.S => ColorS,
            GachaRarity.A => ColorA,
            GachaRarity.B => ColorB,
            _ => ColorC,
        };
        _glowImage.color = c;
        _glowImage.gameObject.SetActive(true);
        // 正面图片：显示物品图标（ItemProto.iconSprite）
        var item = LDB.items.Select(Result.ItemId);
        if (item?.iconSprite != null) {
            _frontImage.sprite = item.iconSprite;
        }
    }

    private IEnumerator GlowPulse() {
        float duration = 0.6f;
        float t = 0;
        Color baseColor = _glowImage.color;
        while (t < duration) {
            t += Time.deltaTime;
            float alpha = Mathf.Sin(t / duration * Mathf.PI);
            _glowImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
        _glowImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.3f);
    }
}
