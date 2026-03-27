using System.Collections;
using FE.Logic.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.View.DrawGrowth;

public class GachaSSREffect : MonoBehaviour {
    private Image _overlay;        // 半透明黑色蒙层
    private Image _itemIcon;       // 物品大图
    private Image _radialGlow;     // 径向光圈（旋转）
    private Image _flashStripe;    // 扫光条
    private Text _rarityText;      // 稀有度文字
    private Button _skipButton;    // 跳过按钮
    
    private bool _skipped = false;
    
    public System.Action OnComplete;

    public static GachaSSREffect Create(Transform canvasParent) {
        var go = new GameObject("GachaSSREffect");
        go.transform.SetParent(canvasParent, false);
        var effect = go.AddComponent<GachaSSREffect>();
        effect.InitUI();
        go.SetActive(false);
        return effect;
    }
    
    private void InitUI() {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        
        // Overlay
        var overlayGo = new GameObject("Overlay");
        overlayGo.transform.SetParent(transform, false);
        _overlay = overlayGo.AddComponent<Image>();
        _overlay.color = new Color(0, 0, 0, 0);
        var overlayRt = _overlay.rectTransform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;
        
        // Radial Glow
        var glowGo = new GameObject("RadialGlow");
        glowGo.transform.SetParent(transform, false);
        _radialGlow = glowGo.AddComponent<Image>();
        _radialGlow.color = new Color(1, 1, 1, 0);
        var glowRt = _radialGlow.rectTransform;
        glowRt.sizeDelta = new Vector2(800, 800);
        
        // Item Icon
        var iconGo = new GameObject("ItemIcon");
        iconGo.transform.SetParent(transform, false);
        _itemIcon = iconGo.AddComponent<Image>();
        _itemIcon.color = new Color(1, 1, 1, 0);
        var iconRt = _itemIcon.rectTransform;
        iconRt.sizeDelta = new Vector2(200, 200);
        
        // Flash Stripe
        var flashGo = new GameObject("FlashStripe");
        flashGo.transform.SetParent(transform, false);
        _flashStripe = flashGo.AddComponent<Image>();
        _flashStripe.color = new Color(1, 1, 1, 0);
        var flashRt = _flashStripe.rectTransform;
        flashRt.sizeDelta = new Vector2(50, 400);
        
        // Rarity Text
        var textGo = new GameObject("RarityText");
        textGo.transform.SetParent(transform, false);
        _rarityText = textGo.AddComponent<Text>();
        _rarityText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _rarityText.fontSize = 60;
        _rarityText.alignment = TextAnchor.MiddleCenter;
        _rarityText.color = new Color(1, 1, 1, 0);
        var textRt = _rarityText.rectTransform;
        textRt.sizeDelta = new Vector2(400, 100);
        textRt.anchoredPosition = new Vector2(0, -200);
        
        // Skip Button
        var skipGo = new GameObject("SkipButton");
        skipGo.transform.SetParent(transform, false);
        _skipButton = skipGo.AddComponent<Button>();
        var skipImage = skipGo.AddComponent<Image>();
        skipImage.color = new Color(0, 0, 0, 0);
        var skipRt = skipImage.rectTransform;
        skipRt.anchorMin = Vector2.zero;
        skipRt.anchorMax = Vector2.one;
        skipRt.sizeDelta = Vector2.zero;
        _skipButton.onClick.AddListener(() => _skipped = true);
    }
    
    public void Play(GachaResult result, System.Action onComplete = null) {
        if (onComplete != null) OnComplete = onComplete;
        gameObject.SetActive(true);
        _skipped = false;
        StartCoroutine(PlayCoroutine(result));
    }
    
    private IEnumerator PlayCoroutine(GachaResult result) {
        // 1. 蒙层淡入（0→0.85，0.2秒）
        yield return FadeImage(_overlay, 0f, 0.85f, 0.2f);
        if (_skipped) { FinishImmediate(result); yield break; }
        
        // 2. 物品大图从0.5缩放到1.0，同时淡入（0.3秒）
        var item = LDB.items.Select(result.ItemId);
        if (item?.iconSprite != null) _itemIcon.sprite = item.iconSprite;
        yield return ScaleAndFade(_itemIcon.rectTransform, _itemIcon, 0.5f, 1.0f, 0f, 1f, 0.3f);
        if (_skipped) { FinishImmediate(result); yield break; }
        
        // 3. 扫光条横扫（0.25秒）
        yield return SweepFlash(0.25f);
        if (_skipped) { FinishImmediate(result); yield break; }
        
        // 4. 稀有度文字淡入
        _rarityText.text = result.Rarity.ToString();
        _rarityText.color = GetRarityColor(result.Rarity);
        yield return FadeText(_rarityText, 0f, 1f, 0.2f);
        
        // 5. 径向光圈持续旋转0.5秒
        yield return RotateGlow(0.5f);
        
        // 6. 等待点击或0.8秒后自动继续
        float waitTime = 0f;
        while (waitTime < 0.8f && !_skipped) {
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        // 7. 淡出
        yield return FadeImage(_overlay, 0.85f, 0f, 0.2f);
        gameObject.SetActive(false);
        OnComplete?.Invoke();
    }
    
    private IEnumerator FadeImage(Image img, float from, float to, float duration) {
        float time = 0;
        Color c = img.color;
        while (time < duration) {
            time += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, time / duration);
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }
    
    private IEnumerator ScaleAndFade(RectTransform rt, Image img, float scaleFrom, float scaleTo, float alphaFrom, float alphaTo, float duration) {
        float time = 0;
        Color c = img.color;
        while (time < duration) {
            time += Time.deltaTime;
            float t = time / duration;
            float scale = Mathf.Lerp(scaleFrom, scaleTo, t);
            rt.localScale = new Vector3(scale, scale, 1);
            c.a = Mathf.Lerp(alphaFrom, alphaTo, t);
            img.color = c;
            yield return null;
        }
        rt.localScale = new Vector3(scaleTo, scaleTo, 1);
        c.a = alphaTo;
        img.color = c;
    }
    
    private IEnumerator SweepFlash(float duration) {
        float time = 0;
        Color c = _flashStripe.color;
        c.a = 0.5f;
        _flashStripe.color = c;
        
        var rt = _flashStripe.rectTransform;
        float startX = -300f;
        float endX = 300f;
        
        while (time < duration) {
            time += Time.deltaTime;
            float t = time / duration;
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, t), 0);
            yield return null;
        }
        
        c.a = 0f;
        _flashStripe.color = c;
    }
    
    private IEnumerator FadeText(Text txt, float from, float to, float duration) {
        float time = 0;
        Color c = txt.color;
        while (time < duration) {
            time += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, time / duration);
            txt.color = c;
            yield return null;
        }
        c.a = to;
        txt.color = c;
    }
    
    private IEnumerator RotateGlow(float duration) {
        float time = 0;
        Color c = _radialGlow.color;
        c.a = 0.5f;
        _radialGlow.color = c;
        
        var rt = _radialGlow.rectTransform;
        
        while (time < duration) {
            time += Time.deltaTime;
            rt.Rotate(0, 0, -180f * Time.deltaTime);
            yield return null;
        }
        
        c.a = 0f;
        _radialGlow.color = c;
    }
    
    private static Color GetRarityColor(GachaRarity rarity) => rarity switch {
        GachaRarity.S => new Color(1.0f, 0.85f, 0.2f),
        GachaRarity.A => new Color(0.4f, 0.6f, 1.0f),
        GachaRarity.B => new Color(0.4f, 0.9f, 0.4f),
        _ => new Color(0.8f, 0.8f, 0.8f),
    };
    
    private void FinishImmediate(GachaResult result) {
        StopAllCoroutines();
        _overlay.color = new Color(0, 0, 0, 0);
        gameObject.SetActive(false);
        OnComplete?.Invoke();
    }
}
