using FractionateEverything.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FractionateEverything.Utils.ProtoID;

namespace FractionateEverything.Main {
    /// <summary>
    /// 感谢jinxOAO和Awbugl，此代码源于MoreMegaStructure的UIBuildMenuPatcher.cs
    /// </summary>
    public static class UIBuildMenuPatcher {
        public static bool MSEnable => Compatibility.MoreMegaStructure.Enable;

        public static List<GameObject> childButtonObjs = [];
        public static List<UIButton> childButtons = [];
        public static List<Image> childIcons = [];
        public static List<Text> childNumTexts = [];
        public static List<Text> childTips = [];
        public static ItemProto[,] protos = new ItemProto[10, 12];
        public static List<CanvasGroup> childCanvasGroups = [];
        public static List<Text> childHotkeyText = [];// F1-F12快捷键文本
        public static List<Text> oriChildHotkeyText = [];// 原始按钮的F1-F12快捷键文本
        public static GameObject switchHotkeyRowText;

        public const int FECategory = 5;
        public const int MSCategory = 8;
        public static int currCategory = 0;
        public static int hotkeyActivateRow = 0;

        public static float secondLevelAlpha = 1f;
        public static int dblClickIndex = 0;
        public static double dblClickTime = 0;

        /// <summary>
        /// 初始化UI，并绑定对应的物品
        /// </summary>
        public static void Init() {
            // 修改底部栏位高度大小、修正文本倾斜度，修正沙盒模式额外面板的位置
            GameObject mainBg = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/bg-trans");
            GameObject sandboxBg =
                GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/bg-trans/sandbox-btn");
            GameObject sandboxTitle =
                GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/bg-trans/sandbox-btn/title");

            float oriXM = mainBg.GetComponent<RectTransform>().sizeDelta.x;
            mainBg.GetComponent<RectTransform>().sizeDelta = new(oriXM, 228);
            float oriXSB = sandboxBg.GetComponent<RectTransform>().sizeDelta.x;
            sandboxBg.GetComponent<RectTransform>().sizeDelta = new(oriXSB, 228);
            sandboxTitle.transform.rotation = Quaternion.AngleAxis(77, new(0, 0, 1));
            sandboxTitle.transform.localPosition = new(81, -50, 0);

            // 新增按钮
            GameObject oriButtonObj =
                GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group/button-1");
            Transform parent = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group")
                .transform;
            float oriX1 = oriButtonObj.transform.localPosition.x;
            float oriY1 = oriButtonObj.transform.localPosition.y;

            for (int i = 0; i < 10; i++) {
                GameObject buildBtnObj = Object.Instantiate(oriButtonObj, parent);
                buildBtnObj.name = $"button-up-{i + 1}";
                buildBtnObj.transform.localPosition = new(oriX1 + i * 52, oriY1 + 60, 0);
                buildBtnObj.AddComponent<CanvasGroup>();
                Object.DestroyImmediate(buildBtnObj.GetComponent<Button>());
                buildBtnObj.AddComponent<Button>();
                int ii = i;
                buildBtnObj.GetComponent<Button>().onClick.AddListener(() => OnChildButtonClick(ii));

                UIButton uiBtn = buildBtnObj.GetComponent<UIButton>();
                uiBtn.button = buildBtnObj.GetComponent<Button>();

                childButtonObjs.Add(buildBtnObj);
                childButtons.Add(uiBtn);
                childIcons.Add(buildBtnObj.transform.Find("icon").GetComponent<Image>());
                childNumTexts.Add(buildBtnObj.transform.Find("count").GetComponent<Text>());
                childCanvasGroups.Add(buildBtnObj.GetComponent<CanvasGroup>());
                childHotkeyText.Add(buildBtnObj.transform.Find("text").GetComponent<Text>());
                oriChildHotkeyText.Add(GameObject
                    .Find($"UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group/button-{i + 1}/text")
                    .GetComponent<Text>());
            }

            // 切换快捷键在哪行的提示文本
            GameObject oriTextObj = oriButtonObj.transform.Find("text").gameObject;
            switchHotkeyRowText = Object.Instantiate(oriTextObj, parent);
            switchHotkeyRowText.name = "switch-note-text";
            switchHotkeyRowText.transform.localPosition = new(200, 155, 0);
            if (GenesisBook.Enable)
                switchHotkeyRowText.transform.localPosition = new(190, 155, 0);
            switchHotkeyRowText.GetComponent<RectTransform>().sizeDelta = new(200, 160);
            switchHotkeyRowText.GetComponent<Text>().text = "切换快捷键".Translate();
            switchHotkeyRowText.GetComponent<Text>().fontSize = 14;
            childCanvasGroups.Add(switchHotkeyRowText.AddComponent<CanvasGroup>());

            //显示内容
            protos[FECategory, 0] = LDB.items.Select(IFE精准分馏塔);
            protos[FECategory, 1] = LDB.items.Select(IFE建筑极速分馏塔);
            protos[FECategory, 2] = LDB.items.Select(I分馏塔_FE通用分馏塔);
            protos[FECategory, 3] = LDB.items.Select(IFE点数聚集分馏塔);
            protos[FECategory, 4] = LDB.items.Select(IFE增产分馏塔);
            if (MSEnable) {
                protos[MSCategory, 0] = LDB.items.Select(IMS铁金属重构装置);
                protos[MSCategory, 1] = LDB.items.Select(IMS铜金属重构装置);
                protos[MSCategory, 2] = LDB.items.Select(IMS高纯硅重构装置);
                protos[MSCategory, 3] = LDB.items.Select(IMS钛金属重构装置);
                protos[MSCategory, 4] = LDB.items.Select(IMS单极磁石重构装置);
                protos[MSCategory, 5] = LDB.items.Select(IMS石墨提炼装置);
                protos[MSCategory, 6] = LDB.items.Select(IMS晶体接收器);
                protos[MSCategory, 7] = LDB.items.Select(IMS光栅晶体接收器);
            }
        }

        public static void IgnoreMSPatches(Harmony harmony) {
            //将巨构原有的所有方法全部屏蔽，不只是patch，初始化UI的也要屏蔽，防止创建两次UI
            var methods = AccessTools.GetDeclaredMethods(typeof(MoreMegaStructure.UIBuildMenuPatcher));
            foreach (var method in methods) {
                if (method.ReturnType == typeof(void)) {
                    harmony.Patch(
                        method,
                        new(typeof(UIBuildMenuPatcher), nameof(MS_RetVoid_PrePatch))
                    );
                }
                else if (method.ReturnType == typeof(bool)) {
                    harmony.Patch(
                        method,
                        new(typeof(UIBuildMenuPatcher), nameof(MS_RetBool_PrePatch))
                    );
                }
            }
        }

        public static bool MS_RetVoid_PrePatch() {
            return false;
        }

        public static bool MS_RetBool_PrePatch(ref bool __result) {
            __result = true;
            return false;
        }

        /// <summary>
        /// 点击页面5时，整个面板起来得更高，可以容纳两行建造按钮
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIFunctionPanel), "_OnUpdate")]
        public static void UIFunctionPanel__OnUpdate_PostPatch(ref UIFunctionPanel __instance) {
            var _this = __instance;
            currCategory = _this.buildMenu.currentCategory;
            bool guideComplete = GameMain.data.guideComplete;
            float oriPosWanted = 0f;
            _this.posWanted = -60;// 对应游戏的0的显示效果
            if (guideComplete) {
                if (_this.buildMenu.active) {
                    if (_this.buildMenu.currentCategory == FECategory
                        || (MSEnable && _this.buildMenu.currentCategory == MSCategory)) {
                        _this.posWanted = 0f;
                    }
                    else {
                        _this.posWanted = -135f;
                        oriPosWanted = -75f;
                    }
                    if (_this.buildMenu.isDismantleMode) {
                        _this.posWanted = -75f;
                    }
                    if (_this.buildMenu.isUpgradeMode) {
                        _this.posWanted = -75f;
                    }
                }
                else if (_this.sandboxMenu.active) {
                    if (_this.sandboxMenu.childGroup.activeSelf) {
                        _this.posWanted = -60f;
                    }
                    else {
                        _this.posWanted = -135f;
                        oriPosWanted = -75f;
                    }
                    if (_this.sandboxMenu.isRemovalMode) {
                        _this.posWanted = -75f;
                    }
                }
                else if (_this.mainPlayer.controller.actionBuild.blueprintMode > EBlueprintMode.None) {
                    _this.posWanted = -193f;
                    oriPosWanted = -133f;
                }
                else {
                    _this.posWanted = -195f;
                    oriPosWanted = -135f;
                }
            }
            else {
                _this.posWanted = -195f;
                oriPosWanted = -135f;
            }

            //下面这段用于抵消原本函数中进行了Lerp.Tween的效果：
            float target = oriPosWanted;
            float now = _this.pos;
            float speed = 18f;
            if (!((float)Mathf.Abs(target - now) < 1E-05)) {
                float deltaTime = Time.deltaTime;
                float t;
                if (deltaTime > 0.01f) {
                    float f = 1f - speed * 0.01f;
                    t = 1f - Mathf.Pow(f, deltaTime * 100f);
                }
                else {
                    t = speed * deltaTime;
                }
                _this.pos = (_this.pos - t * target) / (1 - t);
            }
            // 然后再执行本应仅执行的Tween
            _this.pos = Lerp.Tween(_this.pos, _this.posWanted, 18f);

            _this.sandboxTitleObject.SetActive(_this.pos > -150f);
            _this.sandboxIconObject.SetActive(_this.pos <= -150f);
            _this.bgTrans.anchoredPosition = new(_this.bgTrans.anchoredPosition.x, _this.pos);
            _this.sandboxRect.anchoredPosition = new(Mathf.Clamp(-_this.pos * 2f - 320f, -50f, 10f), 0f);

            // 以下是为了在从category不为8但是展开状态切换成8的时候，让第二行的图标能渐变出现，而非在底部背景未完全展开到需要高度之前就立刻完全显示出来
            if (_this.pos > -10) {
                float targetAlpha = Mathf.Clamp01(secondLevelAlpha + 0.02f);
                if (secondLevelAlpha != targetAlpha) {
                    secondLevelAlpha = targetAlpha;
                    for (int i = 0; i < childCanvasGroups.Count; i++)// i==10是提示文本的canvasGroup，不是按键
                    {
                        childCanvasGroups[i].alpha = secondLevelAlpha;
                    }
                }
            }
            else {
                secondLevelAlpha = 0;
                for (int i = 0; i < childCanvasGroups.Count; i++) {
                    childCanvasGroups[i].alpha = secondLevelAlpha;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIBuildMenu), "SetCurrentCategory")]
        public static void UIBuildMenu_SetCurrentCategory_PostPatch(ref UIBuildMenu __instance, int category) {
            var _this = __instance;
            if (_this.player != null) {
                int num = _this.currentCategory;
                _this.currentCategory = category;
                if (num != _this.currentCategory) {
                    _this.player.controller.actionPick.pickMode = false;
                }
                GameHistoryData history = GameMain.history;

                if (category == FECategory
                    || (MSEnable && category == MSCategory)) {
                    int categoryShift = category == FECategory ? FECategory : MSCategory;
                    StorageComponent package = _this.player.package;
                    for (int i = 0; i < childButtons.Count; i++) {
                        if (childButtons[i] != null) {
                            if (protos[categoryShift, i] != null
                                && (protos[categoryShift, i].IsEntity || protos[categoryShift, i].BuildMode == 4)) {
                                int id = protos[categoryShift, i].ID;
                                if (history.ItemUnlocked(id)) {
                                    int num2 = package.GetItemCount(protos[categoryShift, i].ID);
                                    if (protos[categoryShift, i].ID == _this.player.inhandItemId) {
                                        num2 += _this.player.inhandItemCount;
                                    }
                                    childButtonObjs[i].SetActive(true);
                                    childIcons[i].sprite = protos[categoryShift, i].iconSprite;
                                    StringBuilderUtility.WriteKMG(_this.strb, 5, (long)num2, false);
                                    childNumTexts[i].text = ((num2 > 0) ? _this.strb.ToString().Trim() : "");
                                    childButtons[i].button.interactable = true;
                                }
                                else {
                                    childButtonObjs[i].SetActive(false);
                                    childIcons[i].sprite = null;
                                    StringBuilderUtility.WriteKMG(_this.strb, 5, 0L, false);
                                    childNumTexts[i].text = "";
                                }
                            }
                            else {
                                childButtonObjs[i].SetActive(false);
                                childIcons[i].sprite = null;
                                StringBuilderUtility.WriteKMG(_this.strb, 5, 0L, false);
                                childNumTexts[i].text = "";
                            }
                        }
                    }
                }
                else {
                    for (int i = 0; i < childButtons.Count; i++) {
                        childButtons[i].gameObject.SetActive(false);
                        childIcons[i].sprite = null;
                        StringBuilderUtility.WriteKMG(_this.strb, 5, 0L, false);
                        childNumTexts[i].text = "";
                    }
                }
            }
        }

        public static void OnChildButtonClick(int index) {
            UIBuildMenu _this = UIRoot.instance.uiGame.buildMenu;
            bool flag = false;
            if (_this.player == null) {
                return;
            }
            if (protos[currCategory, index] == null) {
                return;
            }
            int id = protos[currCategory, index].ID;
            if (!_this.showButtonsAnyways && !GameMain.history.ItemUnlocked(id)) {
                return;
            }
            if (index == dblClickIndex && GameMain.gameTime - dblClickTime < 0.33000001311302185) {
                UIRoot.instance.uiGame.FocusOnReplicate(id);
                dblClickTime = 0.0;
                dblClickIndex = 0;
                flag = true;
            }
            dblClickIndex = index;
            dblClickTime = GameMain.gameTime;
            for (int i = 0; i < _this.randRemindTips.Length; i++) {
                if (_this.randRemindTips[i] != null && _this.randRemindTips[i].active) {
                    int featureId = _this.randRemindTips[i].featureId;
                    int num = featureId / 100;
                    int num2 = featureId % 100;
                    if (currCategory == num && index == num2) {
                        _this.randRemindTips[i]._Close();
                    }
                }
            }
            if (_this.player.package.GetItemCount(id) <= 0 && (currCategory != 9 || index != 1)) {
                if (!flag) {
                    UIRealtimeTip.Popup("双击打开合成器".Translate(), true, 2);
                }
                return;
            }
            if (_this.player.inhandItemId == id) {
                _this.player.SetHandItems(0, 0, 0);
            }
            else if (_this.player.package.GetItemCount(id) > 0 || (currCategory == 9 && index == 1)) {
                _this.player.SetHandItems(id, 0, 0);
            }
            else {
                _this.player.SetHandItems(0, 0, 0);
            }
            if (_this.isKeyDownCallingAudio) {
                VFAudio.Create("build-menu-child", null, Vector3.zero, false, 0, -1, -1L).Play();
                _this.isKeyDownCallingAudio = false;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIBuildMenu), "_OnUpdate")]
        public static bool UIBuildMenu__OnUpdate_Prefix(ref UIBuildMenu __instance) {
            bool oriFlag = VFInput.inputing;
            // 如果是第二行快捷键状态，通过让VFInput.inputing = true 拦截可能在原方法内触发的第一行的OnChildButtonClick
            if (__instance.currentCategory == FECategory && hotkeyActivateRow == 1) {
                VFInput.inputing = true;
            }
            if (MSEnable && __instance.currentCategory == MSCategory && hotkeyActivateRow == 1) {
                VFInput.inputing = true;
            }
            // 但如果按键是1234567等等就得不拦截，还原回其状态
            for (int i = 0; i < 10; i++) {
                if (Input.GetKeyDown(KeyCode.Alpha1 + (i - 1))) {
                    VFInput.inputing = oriFlag;
                    break;
                }
            }
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.X)) {
                VFInput.inputing = oriFlag;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIBuildMenu), "_OnUpdate")]
        public static void UIBuildMenu_OnUpdate_PostPatch(ref UIBuildMenu __instance) {
            var _this = __instance;
            if (_this.currentCategory == FECategory
                || (MSEnable && _this.currentCategory == MSCategory)) {
                GameHistoryData history = GameMain.history;
                StorageComponent package = _this.player.package;

                // 快捷键
                VFInput.inputing = (EventSystem.current.currentSelectedGameObject != null
                                    && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>()
                                    != null);
                if (_this.childGroup.gameObject.activeSelf && hotkeyActivateRow == 1) {
                    for (int j = 1; j <= 10; j++) {
                        if (Input.GetKeyDown(KeyCode.F1 + (j - 1))
                            && VFInput.inScreen
                            && VFInput.readyToBuild
                            && !VFInput.inputing) {
                            _this.isKeyDownCallingAudio = true;
                            OnChildButtonClick(j - 1);
                            VFAudio.Create("ui-click-0", null, Vector3.zero, true, 0, -1, -1L);
                        }
                    }
                }

                int num2 = 0;
                while (num2 < 12 && num2 < childNumTexts.Count) {
                    if (protos[_this.currentCategory, num2] != null) {
                        int id2 = protos[_this.currentCategory, num2].ID;
                        if (history.ItemUnlocked(id2) || _this.showButtonsAnyways) {
                            childButtons[num2].tips.itemId = id2;
                            childButtons[num2].tips.itemInc = 0;
                            childButtons[num2].tips.itemCount = 0;
                            childButtons[num2].tips.corner = 8;
                            childButtons[num2].tips.delay = 0.2f;
                            childButtons[num2].tips.type = UIButton.ItemTipType.Other;
                            childButtons[num2].button.gameObject.SetActive(true);
                            int num3 = package.GetItemCount(id2);
                            bool flag2 = _this.player.inhandItemId == id2;
                            if (flag2) {
                                num3 += _this.player.inhandItemCount;
                            }
                            StringBuilderUtility.WriteKMG(_this.strb, 5, (long)num3, false);
                            childNumTexts[num2].text = ((num3 > 0) ? _this.strb.ToString().Trim() : "");
                            childButtons[num2].button.interactable = true;
                            if (childIcons[num2].sprite == null && protos[_this.currentCategory, num2] != null) {
                                childIcons[num2].sprite = protos[_this.currentCategory, num2].iconSprite;
                            }
                            //childTips[num2].color = _this.tipTextColor;
                            childButtons[num2].highlighted = flag2;
                        }
                        else {
                            childButtons[num2].tips.itemId = 0;
                            childButtons[num2].tips.itemInc = 0;
                            childButtons[num2].tips.itemCount = 0;
                            childButtons[num2].tips.type = UIButton.ItemTipType.Other;
                            childButtons[num2].button.interactable = false;
                            childButtons[num2].button.gameObject.SetActive(false);
                        }
                    }
                    num2++;
                }
                if (Input.GetKeyDown(KeyCode.CapsLock)) {
                    hotkeyActivateRow = (hotkeyActivateRow + 1) % 2;
                }
                SwitchHotKeyRow();
            }
            else// 设置F1
            {
                SwitchHotKeyRow(true);
            }
        }

        public static void SwitchHotKeyRow(bool force0 = false) {
            bool flag0 = hotkeyActivateRow == 0;
            bool flag1 = hotkeyActivateRow == 1;
            if (force0) {
                flag0 = true;
                flag1 = false;
            }
            for (int i = 0; i < oriChildHotkeyText.Count; i++) {
                oriChildHotkeyText[i].text = flag0 ? $"F{i + 1}" : "";
            }
            for (int i = 0; i < childHotkeyText.Count; i++) {
                childHotkeyText[i].text = flag1 ? $"F{i + 1}" : "";
            }
        }
    }
}
