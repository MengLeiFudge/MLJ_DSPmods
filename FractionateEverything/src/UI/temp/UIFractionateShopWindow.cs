using NGPT;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FractionateEverything.UI {
    public class UIFractionateShopWindow : ManualBehaviour   {
        /*public const int colCount = 14;
        public const int queueRowCount = 1;
        public const int recipeRowCount = 8;
        public const int kGridSize = 46;
        public const int kPadding = 3;
        [SerializeField] public RectTransform windowRect;
        [SerializeField] public RectTransform queueGroup;
        [SerializeField] public RectTransform recipeGroup;
        [SerializeField] public Image queueBg;
        [SerializeField] public RawImage queueIcons;
        [SerializeField] public Text queueTotalTimeText;
        [SerializeField] public Text queueCountText;
        [SerializeField] public Image currProgressImage;
        [SerializeField] public Image recipeBg;
        [SerializeField] public RawImage recipeIcons;
        [SerializeField] public Image recipeSelImage;
        [SerializeField] public Text prefabNumText;
        [SerializeField] public UIButton typeButton1;
        [SerializeField] public UIButton typeButton2;
        [SerializeField] public GameObject currPredictGroup;
        [SerializeField] public Text currPredictCountText;
        [SerializeField] public UIButton okButton;
        [SerializeField] public UIButton plusButton;
        [SerializeField] public UIButton minusButton;
        [SerializeField] public Text multiValueText;
        [SerializeField] public CanvasGroup treeGroup;
        [SerializeField] public RectTransform treeMainBox;
        [SerializeField] public UIButton treeMainButton0;
        [SerializeField] public Image treeMainIcon0;
        [SerializeField] public Text treeMainCountText0;
        [SerializeField] public UIButton treeMainButton1;
        [SerializeField] public Image treeMainIcon1;
        [SerializeField] public Text treeMainCountText1;
        [SerializeField] public Text treeMainTimeText;
        [SerializeField] public Text treeMainPlaceText;
        [SerializeField] public Image treeMainLineL;
        [SerializeField] public Image treeMainLineR;
        [SerializeField] public RectTransform treeMainHLine;
        [SerializeField] public UIButton treeDownPrefab;
        [SerializeField] public UIButton treeUpPrefab;
        [SerializeField] public Tweener treeTweener1;
        [SerializeField] public Tweener treeTweener2;
        [SerializeField] public UISwitch instantItemSwitch;
        [SerializeField] public UISwitch batchSwitch;
        [SerializeField] public UIButton sandboxAddUsefulItemButton;
        [SerializeField] public UIButton sandboxClearPackageButton;
        [SerializeField] public Color mainTaskTextColor;
        [SerializeField] public Color childTaskTextColor;
        [SerializeField] public Color warningTextColor;
        [SerializeField] public Color errorTextColor;
        [SerializeField] public bool isInstantItem;
        [SerializeField] public bool isBatch;
        [SerializeField] public bool showTips = true;
        [SerializeField] public float showTipsDelay = 0.4f;
        [SerializeField] public int tipAnchor = 7;
        public UIItemTip screenTip;
        public float mouseInTime;
        public EventTrigger evtQueue;
        public EventTrigger evtRecipe;
        public Material queueIconMat;
        public Material queueBgMat;
        public uint[] queueIndexArray;
        public uint[] queueStateArray;
        public ComputeBuffer queueIndexBuffer;
        public ComputeBuffer queueStateBuffer;
        public int currentType = 1;
        public Material recipeIconMat;
        public Material recipeBgMat;
        public uint[] recipeIndexArray;
        public uint[] recipeStateArray;
        public RecipeProto[] recipeProtoArray;
        public ComputeBuffer recipeIndexBuffer;
        public ComputeBuffer recipeStateBuffer;
        public RecipeProto selectedRecipe;
        public int selectedRecipeIndex;
        public List<UIButton> treeDownList;
        public List<UIButton> treeUpList;
        public Dictionary<int, int> multipliers;
        public Text[] queueNumTexts;
        public MechaForge mechaForge;
        public List<ForgeTask> taskQueue;
        public string _tmp_text0_abs = "制造队列";
        public string _tmp_text0 = "";
        public bool mouseInQueue;
        public int mouseQueueIndex = -1;
        public bool mouseInRecipe;
        public int mouseRecipeIndex = -1;

        public override void _OnCreate() {
            this.queueIndexArray = new uint[120];
            this.queueIndexBuffer = new ComputeBuffer(this.queueIndexArray.Length, 4);
            this.queueStateArray = new uint[120];
            this.queueStateBuffer = new ComputeBuffer(this.queueStateArray.Length, 4);
            this.recipeIndexArray = new uint[120];
            this.recipeIndexBuffer = new ComputeBuffer(this.recipeIndexArray.Length, 4);
            this.recipeStateArray = new uint[120];
            this.recipeStateBuffer = new ComputeBuffer(this.recipeStateArray.Length, 4);
            this.recipeProtoArray = new RecipeProto[120];
            this.queueBgMat = UnityEngine.Object.Instantiate<Material>(this.queueBg.material);
            this.queueIconMat = UnityEngine.Object.Instantiate<Material>(this.queueIcons.material);
            this.recipeBgMat = UnityEngine.Object.Instantiate<Material>(this.recipeBg.material);
            this.recipeIconMat = UnityEngine.Object.Instantiate<Material>(this.recipeIcons.material);
            this.queueBg.material = this.queueBgMat;
            this.queueIcons.material = this.queueIconMat;
            this.recipeBg.material = this.recipeBgMat;
            this.recipeIcons.material = this.recipeIconMat;
            this.SetMaterialProps();
            this.typeButton1.data = 1;
            this.typeButton2.data = 2;
            this.evtQueue = this.queueBg.gameObject.AddComponent<EventTrigger>();
            this.evtRecipe = this.recipeBg.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry1 = new EventTrigger.Entry();
            entry1.eventID = EventTriggerType.PointerDown;
            entry1.callback.AddListener(new UnityAction<BaseEventData>(this.OnQueueMouseDown));
            this.evtQueue.triggers.Add(entry1);
            EventTrigger.Entry entry2 = new EventTrigger.Entry();
            entry2.eventID = EventTriggerType.PointerEnter;
            entry2.callback.AddListener(new UnityAction<BaseEventData>(this.OnQueueMouseEnter));
            this.evtQueue.triggers.Add(entry2);
            EventTrigger.Entry entry3 = new EventTrigger.Entry();
            entry3.eventID = EventTriggerType.PointerExit;
            entry3.callback.AddListener(new UnityAction<BaseEventData>(this.OnQueueMouseExit));
            this.evtQueue.triggers.Add(entry3);
            EventTrigger.Entry entry4 = new EventTrigger.Entry();
            entry4.eventID = EventTriggerType.PointerDown;
            entry4.callback.AddListener(new UnityAction<BaseEventData>(this.OnRecipeMouseDown));
            this.evtRecipe.triggers.Add(entry4);
            EventTrigger.Entry entry5 = new EventTrigger.Entry();
            entry5.eventID = EventTriggerType.PointerEnter;
            entry5.callback.AddListener(new UnityAction<BaseEventData>(this.OnRecipeMouseEnter));
            this.evtRecipe.triggers.Add(entry5);
            EventTrigger.Entry entry6 = new EventTrigger.Entry();
            entry6.eventID = EventTriggerType.PointerExit;
            entry6.callback.AddListener(new UnityAction<BaseEventData>(this.OnRecipeMouseExit));
            this.evtRecipe.triggers.Add(entry6);
            this.treeDownList = new List<UIButton>(10);
            this.treeUpList = new List<UIButton>(10);
            this.multipliers = new Dictionary<int, int>(100);
            this.treeUpList.Add(this.treeUpPrefab);
            this.treeDownList.Add(this.treeDownPrefab);
            this.queueNumTexts = new Text[14];
            this._tmp_text0 = this._tmp_text0_abs.Translate();
        }

        public override void _OnDestroy() {
            if ((UnityEngine.Object)this.screenTip != (UnityEngine.Object)null) {
                UnityEngine.Object.Destroy((UnityEngine.Object)this.screenTip.gameObject);
                this.screenTip = (UIItemTip)null;
            }
            this.treeDownList.Clear();
            this.treeUpList.Clear();
            this.multipliers.Clear();
            this.treeDownList = (List<UIButton>)null;
            this.treeUpList = (List<UIButton>)null;
            this.multipliers = (Dictionary<int, int>)null;
            UnityEngine.Object.Destroy((UnityEngine.Object)this.queueBgMat);
            UnityEngine.Object.Destroy((UnityEngine.Object)this.queueIconMat);
            UnityEngine.Object.Destroy((UnityEngine.Object)this.recipeBgMat);
            UnityEngine.Object.Destroy((UnityEngine.Object)this.recipeIconMat);
            this.queueStateBuffer.Release();
            this.queueIndexBuffer.Release();
            this.recipeStateBuffer.Release();
            this.recipeIndexBuffer.Release();
            this.queueBgMat = (Material)null;
            this.queueIconMat = (Material)null;
            this.recipeBgMat = (Material)null;
            this.recipeIconMat = (Material)null;
            this.queueStateBuffer = (ComputeBuffer)null;
            this.queueIndexBuffer = (ComputeBuffer)null;
            this.recipeStateBuffer = (ComputeBuffer)null;
            this.recipeIndexBuffer = (ComputeBuffer)null;
            this.queueNumTexts = (Text[])null;
        }

        public override bool _OnInit() {
            this.isInstantItem = GameMain.preferences.sandboxIsDirectlyObtain;
            this.isBatch = GameMain.preferences.sandboxDirectlyObtainIsStack;
            this.instantItemSwitch.gameObject.SetActive(GameMain.sandboxToolsEnabled);
            this.batchSwitch.gameObject.SetActive(GameMain.sandboxToolsEnabled && this.isInstantItem);
            this.instantItemSwitch.SetToggleNoEvent(this.isInstantItem);
            this.batchSwitch.SetToggleNoEvent(this.isBatch);
            this.sandboxAddUsefulItemButton.gameObject.SetActive(GameMain.sandboxToolsEnabled && this.isInstantItem);
            this.sandboxClearPackageButton.gameObject.SetActive(GameMain.sandboxToolsEnabled && this.isInstantItem);
            this.SetSelectedRecipeIndex(-1, true);
            Array.Clear((Array)this.queueIndexArray, 0, this.queueIndexArray.Length);
            Array.Clear((Array)this.queueStateArray, 0, this.queueStateArray.Length);
            Array.Clear((Array)this.recipeIndexArray, 0, this.recipeIndexArray.Length);
            Array.Clear((Array)this.recipeStateArray, 0, this.recipeStateArray.Length);
            Array.Clear((Array)this.recipeProtoArray, 0, this.recipeProtoArray.Length);
            this.queueIcons.texture = (Texture)GameMain.iconSet.texture;
            this.recipeIcons.texture = (Texture)GameMain.iconSet.texture;
            for (int index = 0; index < 14; ++index)
                this.CreateQueueText(index);
            this._tmp_text0 = this._tmp_text0_abs.Translate();
            return true;
        }

        public override void _OnFree() {
            this.SetSelectedRecipeIndex(-1, false);
            Array.Clear((Array)this.queueIndexArray, 0, this.queueIndexArray.Length);
            Array.Clear((Array)this.queueStateArray, 0, this.queueStateArray.Length);
            Array.Clear((Array)this.recipeIndexArray, 0, this.recipeIndexArray.Length);
            Array.Clear((Array)this.recipeStateArray, 0, this.recipeStateArray.Length);
            Array.Clear((Array)this.recipeProtoArray, 0, this.recipeProtoArray.Length);
            this.queueIcons.texture = (Texture)null;
            this.recipeIcons.texture = (Texture)null;
        }

        public override void _OnRegEvent() {
            this.typeButton1.onClick += new Action<int>(this.OnTypeButtonClick);
            this.typeButton2.onClick += new Action<int>(this.OnTypeButtonClick);
            foreach (UIButton treeUp in this.treeUpList)
                treeUp.onClick += new Action<int>(this.OnTreeButtonClick);
            foreach (UIButton treeDown in this.treeDownList)
                treeDown.onClick += new Action<int>(this.OnTreeButtonClick);
            this.plusButton.onClick += new Action<int>(this.OnPlusButtonClick);
            this.minusButton.onClick += new Action<int>(this.OnMinusButtonClick);
            this.okButton.onClickEnable += new Action<int, bool>(this.OnOkButtonClick);
            this.sandboxAddUsefulItemButton.onClick += new Action<int>(this.OnSandboxGetUsefulItems);
            this.sandboxClearPackageButton.onClick += new Action<int>(this.OnSandboxClearPlayerPackage);
        }

        public override void _OnUnregEvent() {
            this.typeButton1.onClick -= new Action<int>(this.OnTypeButtonClick);
            this.typeButton2.onClick -= new Action<int>(this.OnTypeButtonClick);
            foreach (UIButton treeUp in this.treeUpList)
                treeUp.onClick -= new Action<int>(this.OnTreeButtonClick);
            foreach (UIButton treeDown in this.treeDownList)
                treeDown.onClick -= new Action<int>(this.OnTreeButtonClick);
            this.plusButton.onClick -= new Action<int>(this.OnPlusButtonClick);
            this.minusButton.onClick -= new Action<int>(this.OnMinusButtonClick);
            this.okButton.onClickEnable -= new Action<int, bool>(this.OnOkButtonClick);
            this.sandboxAddUsefulItemButton.onClick -= new Action<int>(this.OnSandboxGetUsefulItems);
            this.sandboxClearPackageButton.onClick -= new Action<int>(this.OnSandboxClearPlayerPackage);
        }

        public override void _OnOpen() {
            this.mechaForge = GameMain.mainPlayer.mecha.forge;
            this.taskQueue = this.mechaForge.tasks;
            this.instantItemSwitch.gameObject.SetActive(GameMain.sandboxToolsEnabled);
            if (!GameMain.sandboxToolsEnabled) {
                this.isInstantItem = false;
                this.isBatch = true;
            }
            Array.Clear((Array)this.queueIndexArray, 0, this.queueIndexArray.Length);
            Array.Clear((Array)this.queueStateArray, 0, this.queueStateArray.Length);
            Array.Clear((Array)this.recipeIndexArray, 0, this.recipeIndexArray.Length);
            Array.Clear((Array)this.recipeStateArray, 0, this.recipeStateArray.Length);
            Array.Clear((Array)this.recipeProtoArray, 0, this.recipeProtoArray.Length);
            this.OnTypeButtonClick(this.currentType);
            this.SetMaterialProps();
            this.treeTweener1.normalizedTime = 0.0f;
            this.treeTweener2.normalizedTime = 0.0f;
            this.treeTweener1.isForward = false;
            this.treeTweener2.isForward = false;
            this.instantItemSwitch.SetToggleNoEvent(this.isInstantItem);
            this.batchSwitch.gameObject.SetActive(GameMain.sandboxToolsEnabled && this.isInstantItem);
            this.batchSwitch.SetToggleNoEvent(this.isBatch);
            this.sandboxAddUsefulItemButton.gameObject.SetActive(GameMain.sandboxToolsEnabled && this.isInstantItem);
            this.sandboxClearPackageButton.gameObject.SetActive(GameMain.sandboxToolsEnabled && this.isInstantItem);
            this.SetBufferData();
            GameMain.history.onTechUnlocked += new Action<int, int, bool>(this.OnTechUnlocked);
            this.transform.SetAsLastSibling();
            GameData data = GameMain.data;
            if (GameMain.gameScenario?.goalLogic == null
                || !GameMain.gameScenario.goalLogic.isInit
                || !data.goalSystem.GetGoalDataById(1202).displayingState[(int)data.gameDesc.goalLevel])
                return;
            GameMain.history.RegFeatureKey(2020001);
        }

        public override void _OnClose() {
            GameMain.history.onTechUnlocked -= new Action<int, int, bool>(this.OnTechUnlocked);
            if ((UnityEngine.Object)this.screenTip != (UnityEngine.Object)null)
                this.screenTip.gameObject.SetActive(false);
            this.DeactiveAllQueueTexts();
            this.OnQueueMouseExit((BaseEventData)null);
            this.OnRecipeMouseExit((BaseEventData)null);
            Array.Clear((Array)this.queueIndexArray, 0, this.queueIndexArray.Length);
            Array.Clear((Array)this.queueStateArray, 0, this.queueStateArray.Length);
            Array.Clear((Array)this.recipeIndexArray, 0, this.recipeIndexArray.Length);
            Array.Clear((Array)this.recipeStateArray, 0, this.recipeStateArray.Length);
            Array.Clear((Array)this.recipeProtoArray, 0, this.recipeProtoArray.Length);
            this.SetSelectedRecipeIndex(-1, false);
        }

        public override void _OnUpdate() {
            this.TestMouseQueueIndex();
            this.TestMouseRecipeIndex();
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                this.OnPlusButtonClick(0);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                this.OnMinusButtonClick(0);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                this.OnOkButtonClick(0, true);
            this.RefreshQueueIcons();
            for (int index = 0; index < this.queueNumTexts.Length; ++index) {
                if (this.taskQueue.Count > index)
                    this.ActiveQueueText(index);
                else
                    this.DeactiveQueueText(index);
            }
            this.queueTotalTimeText.text = this.taskQueue.Count != 0
                ? string.Format("{0}  {1:0.0} s", (object)this._tmp_text0, (object)this.mechaForge.totalTime)
                : this._tmp_text0;
            this.queueCountText.text = this.taskQueue.Count.ToString();
            this.currProgressImage.fillAmount = this.taskQueue.Count <= 0
                ? 0.0f
                : (float)this.taskQueue[0].tick / (float)this.taskQueue[0].tickSpend;
            this.SetBufferData();
            this.treeGroup.interactable =
                this.selectedRecipe != null && (double)this.treeGroup.alpha > 0.9990000128746033;
            this.treeGroup.blocksRaycasts =
                this.selectedRecipe != null && (double)this.treeGroup.alpha > 0.9990000128746033;
            this.treeGroup.gameObject.SetActive((double)this.treeGroup.alpha > 1.0 / 1000.0
                                                || this.selectedRecipe != null);
            this.isBatch = this.batchSwitch.isOn;
            GameMain.preferences.sandboxDirectlyObtainIsStack = this.isBatch;
            int num1 = 0;
            int maxShowing = 999;
            if (this.selectedRecipe != null && this.selectedRecipe.Handcraft)
                num1 = this.mechaForge.PredictTaskCount(this.selectedRecipe.ID, maxShowing, true);
            if (num1 == 0 && !this.isInstantItem) {
                this.okButton.button.interactable = false;
                this.currPredictCountText.text = "";
                this.currPredictGroup.SetActive(false);
                if (this.selectedRecipe != null) {
                    StorageComponent package = this.mechaForge.player.package;
                    bool flag1 = true;
                    for (int index1 = 0; index1 < this.treeDownList.Count; ++index1) {
                        UIButton treeDown = this.treeDownList[index1];
                        if (index1 < this.selectedRecipe.Items.Length
                            && this.selectedRecipe.Items[index1] == treeDown.tips.itemId) {
                            int num2 = this.selectedRecipe.Items[index1];
                            int itemCount1 = this.selectedRecipe.ItemCounts[index1];
                            bool useBottleneckItem = false;
                            bool flag2 = false;
                            int itemCount2 = package.GetItemCount(num2);
                            int num3 = itemCount1 - itemCount2;
                            if (num3 > 0) {
                                ItemProto itemProto = LDB.items.Select(num2);
                                if (itemProto != null && itemProto.handcraft != null) {
                                    int index2 = 0;
                                    if (itemProto.handcraft.Results[0] == num2)
                                        index2 = 0;
                                    else if (itemProto.handcraft.Results[1] == num2)
                                        index2 = 1;
                                    else if (itemProto.handcraft.Results[2] == num2)
                                        index2 = 2;
                                    else if (itemProto.handcraft.Results[3] == num2)
                                        index2 = 3;
                                    else
                                        Assert.CannotBeReached();
                                    int count = Mathf.CeilToInt((float)num3
                                                                / (float)itemProto.handcraft.ResultCounts[index2]);
                                    if (this.mechaForge.TryTaskWithTestPackage(itemProto.handcraft.ID, count, package,
                                            out useBottleneckItem))
                                        flag2 = true;
                                }
                            } else {
                                if (this.mechaForge.bottleneckItems.Contains(num2))
                                    this.treeDownList[index1].transitions[4].alphaOnly = true;
                                flag2 = true;
                            }
                            if (!flag2) {
                                flag1 = false;
                                this.treeDownList[index1].transitions[3].alphaOnly = true;
                            } else if (useBottleneckItem)
                                this.treeDownList[index1].transitions[4].alphaOnly = true;
                        }
                    }
                    if (!flag1) {
                        for (int index = 0; index < this.treeDownList.Count; ++index) {
                            if (this.treeDownList[index].transitions[4].alphaOnly)
                                this.treeDownList[index].transitions[4].alphaOnly = false;
                        }
                    }
                    this.mechaForge.bottleneckItems.Clear();
                }
            } else {
                if (this.selectedRecipe != null) {
                    Player player = this.mechaForge.player;
                    if (this.multipliers != null && this.multipliers.ContainsKey(this.selectedRecipe.ID)) {
                        int multiplier = this.multipliers[this.selectedRecipe.ID];
                    }
                }
                this.okButton.button.interactable = true;
                this.currPredictCountText.text = num1 >= maxShowing ? ">" + maxShowing.ToString() : num1.ToString();
                this.currPredictGroup.SetActive(!this.isInstantItem);
            }
            for (int index = 0; index < this.treeDownList.Count; ++index) {
                if ((UnityEngine.Object)this.treeDownList[index] != (UnityEngine.Object)null) {
                    UIButton.Transition transition1 = this.treeDownList[index].transitions[3];
                    transition1.target.enabled = transition1.alphaOnly;
                    transition1.alphaOnly = false;
                    UIButton.Transition transition2 = this.treeDownList[index].transitions[4];
                    transition2.target.enabled = transition2.alphaOnly;
                    transition2.alphaOnly = false;
                }
            }
            if (this.showTips) {
                int num4 = -1;
                int num5 = -1;
                int id = 0;
                if (this.mouseRecipeIndex >= 0) {
                    id = this.recipeProtoArray[this.mouseRecipeIndex] == null
                        ? 0
                        : this.recipeProtoArray[this.mouseRecipeIndex].ID;
                    num4 = this.mouseRecipeIndex % 14;
                    num5 = this.mouseRecipeIndex / 14;
                }
                RecipeProto recipeProto = id == 0 ? (RecipeProto)null : LDB.recipes.Select(id);
                if (recipeProto != null) {
                    int itemId = recipeProto.Explicit ? -id : recipeProto.Results[0];
                    bool productive = recipeProto.productive;
                    this.mouseInTime += Time.deltaTime;
                    if ((double)this.mouseInTime > (double)this.showTipsDelay) {
                        if ((UnityEngine.Object)this.screenTip == (UnityEngine.Object)null)
                            this.screenTip = UIItemTip.Create(itemId, this.tipAnchor,
                                new Vector2((float)(num4 * 46 + 15), (float)(-num5 * 46 - 50)), this.recipeBg.transform,
                                0, 0, UIButton.ItemTipType.Recipe, !recipeProto.Handcraft, !productive, true);
                        if (!this.screenTip.gameObject.activeSelf) {
                            this.screenTip.gameObject.SetActive(true);
                            this.screenTip.SetTip(itemId, this.tipAnchor,
                                new Vector2((float)(num4 * 46 + 15), (float)(-num5 * 46 - 50)), this.recipeBg.transform,
                                0, 0, UIButton.ItemTipType.Recipe, !recipeProto.Handcraft, !productive, true);
                        } else if (this.screenTip.showingItemId != itemId)
                            this.screenTip.SetTip(itemId, this.tipAnchor,
                                new Vector2((float)(num4 * 46 + 15), (float)(-num5 * 46 - 50)), this.recipeBg.transform,
                                0, 0, UIButton.ItemTipType.Recipe, !recipeProto.Handcraft, !productive, true);
                    }
                } else {
                    if ((double)this.mouseInTime > 0.0)
                        this.mouseInTime = 0.0f;
                    if ((UnityEngine.Object)this.screenTip != (UnityEngine.Object)null) {
                        this.screenTip.showingItemId = 0;
                        this.screenTip.gameObject.SetActive(false);
                    }
                }
            }
            if (this.selectedRecipe == null)
                return;
            int num6 = 1;
            if (this.multipliers.ContainsKey(this.selectedRecipe.ID))
                num6 = this.multipliers[this.selectedRecipe.ID];
            if (!this.isInstantItem) {
                this.multiValueText.text = num6.ToString() + "x";
            } else {
                int stackSize = LDB.items.Select(this.selectedRecipe.Results[0]).StackSize;
                this.multiValueText.text = (this.isBatch ? num6 * stackSize : num6).ToString();
            }
        }

        public void SetMaterialProps() {
            this.queueBgMat.SetBuffer("_StateBuffer", this.queueStateBuffer);
            this.queueIconMat.SetBuffer("_IndexBuffer", this.queueIndexBuffer);
            this.recipeBgMat.SetBuffer("_StateBuffer", this.recipeStateBuffer);
            this.recipeIconMat.SetBuffer("_IndexBuffer", this.recipeIndexBuffer);
            float num1 = 0.06521739f;
            float num2 = 1.15f;
            Vector4 vector4_1 = new Vector4(14f, 0.0f, 0.04f, 0.04f);
            Vector4 vector4_2 = new Vector4(num1, num1, num2, num2);
            vector4_1.y = 1f;
            this.queueBgMat.SetVector("_Grid", vector4_1);
            this.queueIconMat.SetVector("_Grid", vector4_1);
            this.queueIconMat.SetVector("_Rect", vector4_2);
            vector4_1.y = 8f;
            this.recipeBgMat.SetVector("_Grid", vector4_1);
            this.recipeIconMat.SetVector("_Grid", vector4_1);
            this.recipeIconMat.SetVector("_Rect", vector4_2);
        }

        public void SetBufferData() {
            this.queueStateBuffer.SetData((Array)this.queueStateArray);
            this.queueIndexBuffer.SetData((Array)this.queueIndexArray);
            this.recipeStateBuffer.SetData((Array)this.recipeStateArray);
            this.recipeIndexBuffer.SetData((Array)this.recipeIndexArray);
        }

        public void CreateQueueText(int index) {
            if ((UnityEngine.Object)this.queueNumTexts[index] == (UnityEngine.Object)null)
                this.queueNumTexts[index] =
                    UnityEngine.Object.Instantiate<Text>(this.prefabNumText, (Transform)this.queueGroup);
            this.RepositionQueueText(index);
        }

        public void ActiveQueueText(int index) {
            if (!this.queueNumTexts[index].gameObject.activeSelf)
                this.queueNumTexts[index].gameObject.SetActive(true);
            if (this.taskQueue != null && this.taskQueue.Count > index) {
                RecipeProto recipeProto = LDB.recipes.Select(this.taskQueue[index].recipeId);
                int count = this.taskQueue[index].count;
                if (recipeProto != null && recipeProto.ResultCounts.Length == 1)
                    count *= recipeProto.ResultCounts[0];
                bool flag = this.taskQueue[index].parentTaskIndex < 0;
                this.queueNumTexts[index].text = flag ? count.ToString() : "(" + count.ToString() + ")";
                this.queueNumTexts[index].color = flag ? this.mainTaskTextColor : this.childTaskTextColor;
            } else
                this.queueNumTexts[index].text = "";
        }

        public void DeactiveQueueText(int index) {
            if (!this.queueNumTexts[index].gameObject.activeSelf)
                return;
            this.queueNumTexts[index].text = "";
            this.queueNumTexts[index].gameObject.SetActive(false);
        }

        public void DeactiveAllQueueTexts() {
            for (int index = 0; index < this.queueNumTexts.Length; ++index) {
                if ((UnityEngine.Object)this.queueNumTexts[index] != (UnityEngine.Object)null
                    && this.queueNumTexts[index].gameObject.activeSelf) {
                    this.queueNumTexts[index].text = "";
                    this.queueNumTexts[index].gameObject.SetActive(false);
                }
            }
        }

        public void RepositionQueueText(int index) {
            int num1 = index % 14;
            int num2 = index / 14;
            this.queueNumTexts[index].rectTransform.anchoredPosition =
                new Vector2((float)(num1 * 46 + 43), (float)(num2 * -46 - 48));
        }

        public void RefreshQueueIcons() {
            Array.Clear((Array)this.queueIndexArray, 0, this.queueIndexArray.Length);
            IconSet iconSet = GameMain.iconSet;
            for (int index = 0; index < this.taskQueue.Count && index < 14; ++index)
                this.queueIndexArray[index] = iconSet.recipeIconIndex[this.taskQueue[index].recipeId];
        }

        public void RefreshRecipeIcons() {
            Array.Clear((Array)this.recipeIndexArray, 0, this.recipeIndexArray.Length);
            Array.Clear((Array)this.recipeStateArray, 0, this.recipeStateArray.Length);
            Array.Clear((Array)this.recipeProtoArray, 0, this.recipeProtoArray.Length);
            GameHistoryData history = GameMain.history;
            RecipeProto[] dataArray = LDB.recipes.dataArray;
            IconSet iconSet = GameMain.iconSet;
            for (int index1 = 0; index1 < dataArray.Length; ++index1) {
                if (dataArray[index1].GridIndex >= 1101
                    && (history.RecipeUnlocked(dataArray[index1].ID) || this.isInstantItem)) {
                    int num1 = dataArray[index1].GridIndex / 1000;
                    int num2 = (dataArray[index1].GridIndex - num1 * 1000) / 100 - 1;
                    int num3 = dataArray[index1].GridIndex % 100 - 1;
                    bool handcraft = dataArray[index1].Handcraft;
                    if (num2 >= 0 && num3 >= 0 && num2 < 8 && num3 < 14) {
                        int index2 = num2 * 14 + num3;
                        if (index2 >= 0 && index2 < this.recipeIndexArray.Length && num1 == this.currentType) {
                            this.recipeIndexArray[index2] = iconSet.recipeIconIndex[dataArray[index1].ID];
                            this.recipeStateArray[index2] = handcraft || this.isInstantItem ? 0U : 8U;
                            this.recipeProtoArray[index2] = dataArray[index1];
                        }
                    }
                }
            }
        }

        public void OnTypeButtonClick(int type) {
            this.SetSelectedRecipeIndex(-1, true);
            this.currentType = type;
            this.RefreshRecipeIcons();
            this.typeButton1.highlighted = type == 1;
            this.typeButton2.highlighted = type == 2;
            this.typeButton1.button.interactable = type != 1;
            this.typeButton2.button.interactable = type != 2;
        }

        public void OnTreeButtonClick(int id) {
            if (id <= 0)
                return;
            RecipeProto recipe = LDB.recipes.Select(id);
            if (recipe == null)
                return;
            this.SetSelectedRecipe(recipe, true);
        }

        public void OnPlusButtonClick(int whatever) {
            if (this.selectedRecipe == null)
                return;
            if (!this.multipliers.ContainsKey(this.selectedRecipe.ID))
                this.multipliers[this.selectedRecipe.ID] = 1;
            int num = this.multipliers[this.selectedRecipe.ID] + 1;
            if (num > 10)
                num = 10;
            this.multipliers[this.selectedRecipe.ID] = num;
        }

        public void OnMinusButtonClick(int whatever) {
            if (this.selectedRecipe == null)
                return;
            if (!this.multipliers.ContainsKey(this.selectedRecipe.ID))
                this.multipliers[this.selectedRecipe.ID] = 1;
            int num = this.multipliers[this.selectedRecipe.ID] - 1;
            if (num < 1)
                num = 1;
            this.multipliers[this.selectedRecipe.ID] = num;
        }

        public void OnOkButtonClick(int whatever, bool button_enable) {
            if (this.selectedRecipe == null || GameMain.isFullscreenPaused)
                return;
            int id = this.selectedRecipe.ID;
            int count = 1;
            if (this.multipliers.ContainsKey(id))
                count = this.multipliers[id];
            if (count < 1)
                count = 1;
            else if (count > 10)
                count = 10;
            if (GameMain.sandboxToolsEnabled && this.isInstantItem) {
                Player mainPlayer = GameMain.mainPlayer;
                RecipeProto recipeProto = LDB.recipes.Select(id);
                for (int index = 0; index < recipeProto.Results.Length; ++index) {
                    int result = recipeProto.Results[index];
                    int stackSize = LDB.items.Select(result).StackSize;
                    int num1 = this.isBatch ? count * stackSize : count;
                    int package = mainPlayer.TryAddItemToPackage(result, num1, 0, true);
                    int num2 = num1 - package;
                    if (num2 > 0) {
                        ItemProto itemProto = LDB.items.Select(result);
                        if (itemProto != null)
                            UIRealtimeTip.Popup(string.Format("背包已满未添加".Translate(), (object)num2,
                                (object)itemProto.name));
                    }
                    if (package > 0)
                        UIItemup.Up(result, package);
                    mainPlayer.mecha.AddProductionStat(result, num1, mainPlayer.nearestFactory);
                }
            } else if (!this.selectedRecipe.Handcraft)
                UIRealtimeTip.Popup("该配方".Translate() + this.selectedRecipe.madeFromString + "生产".Translate());
            else if (!GameMain.history.RecipeUnlocked(id)) {
                UIRealtimeTip.Popup("配方未解锁".Translate());
            } else {
                int num = this.mechaForge.PredictTaskCount(this.selectedRecipe.ID);
                if (count > num)
                    count = num;
                if (count == 0) {
                    UIRealtimeTip.Popup("材料不足".Translate());
                    GameMain.data.warningSystem.Broadcast(EBroadcastVocal.InsufficientMaterials);
                } else if (this.mechaForge.AddTask(id, count) == null) {
                    UIRealtimeTip.Popup("材料不足".Translate());
                    GameMain.data.warningSystem.Broadcast(EBroadcastVocal.InsufficientMaterials);
                } else
                    GameMain.history.RegFeatureKey(1000104);
            }
        }

        public void TestMouseQueueIndex() {
            Array.Clear((Array)this.queueStateArray, 0, this.queueStateArray.Length);
            this.mouseQueueIndex = -1;
            Vector2 rectPoint;
            if (!this.mouseInQueue
                || !UIRoot.ScreenPointIntoRect(Input.mousePosition, this.queueBg.rectTransform, out rectPoint))
                return;
            int num1 = Mathf.FloorToInt(rectPoint.x / 46f);
            int num2 = Mathf.FloorToInt((float)(-(double)rectPoint.y / 46.0));
            if (num1 < 0 || num2 < 0 || num1 >= 14 || num2 >= 1)
                return;
            this.mouseQueueIndex = num1 + num2 * 14;
            if (this.queueIndexArray[this.mouseQueueIndex] == 0U)
                return;
            this.queueStateArray[this.mouseQueueIndex] = 1U;
        }

        public void TestMouseRecipeIndex() {
            for (int index = 0; index < this.recipeStateArray.Length; ++index)
                this.recipeStateArray[index] &= 254U;
            this.mouseRecipeIndex = -1;
            Vector2 rectPoint;
            if (!this.mouseInRecipe
                || !UIRoot.ScreenPointIntoRect(Input.mousePosition, this.recipeBg.rectTransform, out rectPoint))
                return;
            int num1 = Mathf.FloorToInt(rectPoint.x / 46f);
            int num2 = Mathf.FloorToInt((float)(-(double)rectPoint.y / 46.0));
            if (num1 < 0 || num2 < 0 || num1 >= 14 || num2 >= 8)
                return;
            this.mouseRecipeIndex = num1 + num2 * 14;
            if (this.recipeProtoArray[this.mouseRecipeIndex] == null)
                return;
            this.recipeStateArray[this.mouseRecipeIndex] |= 1U;
        }

        public void SetSelectedRecipeIndex(int index, bool notify) {
            RecipeProto selectedRecipe = this.selectedRecipe;
            this.mouseRecipeIndex = index;
            this.selectedRecipe = (long)(uint)index >= (long)this.recipeProtoArray.Length
                ? (RecipeProto)null
                : this.recipeProtoArray[index];
            if (this.selectedRecipe == null)
                this.mouseRecipeIndex = -1;
            if (this.selectedRecipe != null) {
                this.recipeSelImage.rectTransform.anchoredPosition =
                    new Vector2((float)(index % 14 * 46 - 1), (float)(-(index / 14) * 46 + 1));
                this.recipeSelImage.gameObject.SetActive(true);
            } else {
                this.recipeSelImage.rectTransform.anchoredPosition = new Vector2(-1f, 1f);
                this.recipeSelImage.gameObject.SetActive(false);
            }
            if (!notify)
                return;
            this.OnSelectedRecipeChange(selectedRecipe != this.selectedRecipe);
        }

        public void SetSelectedRecipe(RecipeProto recipe, bool notify) {
            if (!this.isInstantItem && !GameMain.history.RecipeUnlocked(recipe.ID))
                return;
            int type = recipe.GridIndex / 1000;
            int num1 = (recipe.GridIndex - type * 1000) / 100 - 1;
            int num2 = recipe.GridIndex % 100 - 1;
            bool flag = true;
            if (type != 1 && type != 2)
                flag = false;
            if (num1 < 0 || num2 < 0 || num1 >= 8 || num2 >= 14)
                flag = false;
            int index = num1 * 14 + num2;
            if (index < 0 || index >= this.recipeIndexArray.Length)
                flag = false;
            if (flag) {
                this.OnTypeButtonClick(type);
                this.SetSelectedRecipeIndex(index, notify);
            } else
                this.SetSelectedRecipeIndex(-1, notify);
        }

        public void OnSelectedRecipeChange(bool changed) {
            if (this.selectedRecipe == null) {
                this.treeTweener1.Play1To0Continuing();
                this.treeTweener2.Play1To0Continuing();
            } else {
                this.treeTweener1.Play0To1Continuing();
                if (changed && (double)this.treeTweener2.normalizedTime > 0.5)
                    this.treeTweener2.normalizedTime = 0.5f;
                this.treeTweener2.Play0To1Continuing();
                int length1 = this.selectedRecipe.Items.Length;
                int length2 = this.selectedRecipe.Results.Length;
                ItemProto itemProto1 = (ItemProto)null;
                if (length2 > 0)
                    itemProto1 = LDB.items.Select(this.selectedRecipe.Results[0]);
                ItemProto itemProto2 = (ItemProto)null;
                if (length2 > 1)
                    itemProto2 = LDB.items.Select(this.selectedRecipe.Results[1]);
                int resultCount1 = length2 > 0 ? this.selectedRecipe.ResultCounts[0] : 0;
                int resultCount2 = length2 > 1 ? this.selectedRecipe.ResultCounts[1] : 0;
                this.treeMainBox.sizeDelta = new Vector2(length2 > 1 ? 114f : 64f, 64f);
                this.treeMainButton0.tips.itemId = itemProto1 != null ? itemProto1.ID : 0;
                this.treeMainButton1.tips.itemId = itemProto2 != null ? itemProto2.ID : 0;
                this.treeMainButton0.tips.type = UIButton.ItemTipType.IgnoreIncPoint;
                this.treeMainButton1.tips.type = UIButton.ItemTipType.IgnoreIncPoint;
                this.treeMainIcon0.sprite = itemProto1?.iconSprite;
                this.treeMainIcon1.sprite = itemProto2?.iconSprite;
                this.treeMainIcon0.rectTransform.anchoredPosition = new Vector2(length2 > 1 ? -25f : 0.0f, 0.0f);
                this.treeMainCountText0.rectTransform.anchoredPosition = new Vector2(length2 > 1 ? -49f : -24f, -11f);
                this.treeMainIcon1.gameObject.SetActive(length2 > 1);
                this.treeMainCountText1.gameObject.SetActive(length2 > 1);
                this.treeMainCountText0.text = resultCount1 != 1 ? "x " + (object)resultCount1 : "";
                this.treeMainCountText1.text = resultCount2 != 1 ? "x " + (object)resultCount2 : "";
                this.treeMainTimeText.text = this.selectedRecipe.Type == ERecipeType.Fractionate
                    ? ((float)((double)this.selectedRecipe.ResultCounts[0]
                               / (double)this.selectedRecipe.ItemCounts[0]
                               * 100.0)).ToString()
                      + "%"
                    : ((float)this.selectedRecipe.TimeSpend / 60f).ToString("0.##") + " s";
                this.treeMainPlaceText.text = this.selectedRecipe.madeFromString;
                if (Input.GetKey(KeyCode.KeypadDivide))
                    this.treeMainPlaceText.text = "id: " + (object)this.selectedRecipe.ID;
                foreach (Component treeDown in this.treeDownList)
                    treeDown.gameObject.SetActive(false);
                for (int index = 0; index < length1; ++index) {
                    if (index == this.treeDownList.Count) {
                        UIButton uiButton = UnityEngine.Object.Instantiate<UIButton>(this.treeDownPrefab,
                            this.treeDownPrefab.transform.parent);
                        uiButton.onClick += new Action<int>(this.OnTreeButtonClick);
                        this.treeDownList.Add(uiButton);
                    }
                }
                float num1 = -40f * (float)(length1 - 1);
                for (int index = 0; index < length1; ++index) {
                    ItemProto itemProto3 = LDB.items.Select(this.selectedRecipe.Items[index]);
                    RecipeProto maincraft = itemProto3.maincraft;
                    UIButton treeDown = this.treeDownList[index];
                    if (itemProto3 != null) {
                        treeDown.tips.itemId = itemProto3.ID;
                        treeDown.tips.corner = 8;
                        treeDown.tips.delay = 0.3f;
                        treeDown.tips.type = UIButton.ItemTipType.IgnoreIncPoint;
                        treeDown.transitions[1].target.gameObject.SetActive(true);
                        treeDown.transitions[2].target.gameObject.SetActive(true);
                        (treeDown.transitions[1].target as Image).sprite = itemProto3.iconSprite;
                        (treeDown.transitions[2].target as Text).text =
                            this.selectedRecipe.ItemCounts[index].ToString();
                        (treeDown.transitions[2].target as Text).enabled =
                            this.selectedRecipe.Type != ERecipeType.Fractionate;
                        treeDown.data = maincraft == null ? 0 : maincraft.ID;
                        (treeDown.transform as RectTransform).anchoredPosition =
                            new Vector2(80f * (float)index + num1, -60f);
                        treeDown.gameObject.SetActive(true);
                    } else {
                        treeDown.tips.itemId = 0;
                        treeDown.tips.type = UIButton.ItemTipType.IgnoreIncPoint;
                        treeDown.transitions[1].target.gameObject.SetActive(false);
                        treeDown.transitions[2].target.gameObject.SetActive(false);
                    }
                }
                foreach (Component treeUp in this.treeUpList)
                    treeUp.gameObject.SetActive(false);
                GameHistoryData history = GameMain.history;
                if (itemProto1 != null && itemProto2 == null) {
                    List<RecipeProto> recipeProtoList = new List<RecipeProto>();
                    foreach (RecipeProto make in itemProto1.makes) {
                        if (history.RecipeUnlocked(make.ID))
                            recipeProtoList.Add(make);
                    }
                    int num2 = recipeProtoList.Count;
                    if (num2 > 8)
                        num2 = 8;
                    for (int index = 0; index < num2; ++index) {
                        if (index == this.treeUpList.Count) {
                            UIButton uiButton = UnityEngine.Object.Instantiate<UIButton>(this.treeUpPrefab,
                                this.treeUpPrefab.transform.parent);
                            uiButton.onClick += new Action<int>(this.OnTreeButtonClick);
                            this.treeUpList.Add(uiButton);
                        }
                    }
                    if (num2 > 0)
                        this.treeMainLineL.gameObject.SetActive(true);
                    else
                        this.treeMainLineL.gameObject.SetActive(false);
                    if (num2 > 1)
                        this.treeMainLineR.gameObject.SetActive(true);
                    else
                        this.treeMainLineR.gameObject.SetActive(false);
                    this.treeMainLineL.rectTransform.sizeDelta = new Vector2(32f, 2f);
                    this.treeMainLineR.rectTransform.sizeDelta = new Vector2(32f, 2f);
                    for (int index = 0; index < num2; ++index) {
                        RecipeProto recipeProto = recipeProtoList[index];
                        UIButton treeUp = this.treeUpList[index];
                        (treeUp.transitions[1].target as Image).sprite = recipeProto.iconSprite;
                        treeUp.data = recipeProto.ID;
                        treeUp.tips.itemId = recipeProto.Explicit ? -recipeProto.ID : recipeProto.Results[0];
                        treeUp.tips.corner = 8;
                        treeUp.tips.delay = 0.3f;
                        treeUp.tips.type = UIButton.ItemTipType.IgnoreIncPoint;
                        (treeUp.transform as RectTransform).anchoredPosition = new Vector2(
                            index % 2 != 0 ? (float)(86 + index / 2 * 46) : (float)(-86 - index / 2 * 46), 52f);
                        treeUp.gameObject.SetActive(true);
                    }
                } else if (itemProto1 != null && itemProto2 != null) {
                    List<RecipeProto> recipeProtoList1 = new List<RecipeProto>();
                    foreach (RecipeProto make in itemProto1.makes) {
                        if (history.RecipeUnlocked(make.ID))
                            recipeProtoList1.Add(make);
                    }
                    List<RecipeProto> recipeProtoList2 = new List<RecipeProto>();
                    foreach (RecipeProto make in itemProto2.makes) {
                        if (history.RecipeUnlocked(make.ID))
                            recipeProtoList2.Add(make);
                    }
                    int num3 = recipeProtoList1.Count;
                    if (num3 > 4)
                        num3 = 4;
                    int num4 = recipeProtoList2.Count;
                    if (num4 > 4)
                        num4 = 4;
                    int num5 = num3 + num4;
                    if (num3 > 0)
                        this.treeMainLineL.gameObject.SetActive(true);
                    else
                        this.treeMainLineL.gameObject.SetActive(false);
                    if (num4 > 0)
                        this.treeMainLineR.gameObject.SetActive(true);
                    else
                        this.treeMainLineR.gameObject.SetActive(false);
                    this.treeMainLineL.rectTransform.sizeDelta = new Vector2(12f, 2f);
                    this.treeMainLineR.rectTransform.sizeDelta = new Vector2(12f, 2f);
                    for (int index = 0; index < num5; ++index) {
                        if (index == this.treeUpList.Count) {
                            UIButton uiButton = UnityEngine.Object.Instantiate<UIButton>(this.treeUpPrefab,
                                this.treeUpPrefab.transform.parent);
                            uiButton.onClick += new Action<int>(this.OnTreeButtonClick);
                            this.treeUpList.Add(uiButton);
                        }
                    }
                    for (int index = 0; index < num3; ++index) {
                        RecipeProto recipeProto = recipeProtoList1[index];
                        UIButton treeUp = this.treeUpList[index];
                        (treeUp.transitions[1].target as Image).sprite = recipeProto.iconSprite;
                        treeUp.data = recipeProto.ID;
                        treeUp.tips.itemId = recipeProto.Explicit ? -recipeProto.ID : recipeProto.Results[0];
                        treeUp.tips.corner = 8;
                        treeUp.tips.delay = 0.3f;
                        treeUp.tips.type = UIButton.ItemTipType.IgnoreIncPoint;
                        (treeUp.transform as RectTransform).anchoredPosition =
                            new Vector2((float)(-90 - index * 46), 52f);
                        treeUp.gameObject.SetActive(true);
                    }
                    for (int index = 0; index < num4; ++index) {
                        RecipeProto recipeProto = recipeProtoList2[index];
                        UIButton treeUp = this.treeUpList[index + num3];
                        (treeUp.transitions[1].target as Image).sprite = recipeProto.iconSprite;
                        treeUp.data = recipeProto.ID;
                        treeUp.tips.itemId = recipeProto.Explicit ? -recipeProto.ID : recipeProto.Results[0];
                        treeUp.tips.corner = 8;
                        treeUp.tips.delay = 0.3f;
                        treeUp.tips.type = UIButton.ItemTipType.IgnoreIncPoint;
                        (treeUp.transform as RectTransform).anchoredPosition =
                            new Vector2((float)(90 + index * 46), 52f);
                        treeUp.gameObject.SetActive(true);
                    }
                }
                this.treeMainHLine.sizeDelta = new Vector2((float)(80.0 * (double)length1 - 77.0), 3f);
            }
        }

        public void OnQueueMouseDown(BaseEventData evtData) {
            if (this.mouseQueueIndex < 0 || !(evtData is PointerEventData pointerEventData))
                return;
            if (pointerEventData.button == PointerEventData.InputButton.Left) {
                if (this.taskQueue.Count <= this.mouseQueueIndex || this.taskQueue[this.mouseQueueIndex] == null)
                    return;
                RecipeProto recipe = LDB.recipes.Select(this.taskQueue[this.mouseQueueIndex].recipeId);
                if (recipe == null)
                    return;
                this.SetSelectedRecipe(recipe, true);
                VFAudio.Create("ui-click-0", (Transform)null, Vector3.zero, true);
            } else {
                if (pointerEventData.button != PointerEventData.InputButton.Right
                    || this.taskQueue.Count <= this.mouseQueueIndex
                    || this.taskQueue[this.mouseQueueIndex] == null)
                    return;
                this.mechaForge.CancelTask(this.mouseQueueIndex);
                this.mechaForge.CalculateExtra();
                VFAudio.Create("cancel-0", (Transform)null, Vector3.zero, true);
            }
        }

        public void OnRecipeMouseDown(BaseEventData evtData) {
            if (this.mouseRecipeIndex < 0)
                return;
            if ((long)(uint)this.mouseRecipeIndex < (long)this.recipeProtoArray.Length) {
                this.selectedRecipe = this.recipeProtoArray[this.mouseRecipeIndex];
                if (this.selectedRecipe != null)
                    VFAudio.Create("ui-click-0", (Transform)null, Vector3.zero, true)            ;
            }
            this.SetSelectedRecipeIndex(this.mouseRecipeIndex, true);
        }

        public void OnQueueMouseEnter(BaseEventData evtData) {
            this.mouseInQueue = true;
            this.mouseInRecipe = false;
        }

        public void OnQueueMouseExit(BaseEventData evtData) {
            this.mouseInQueue = false;
            this.mouseQueueIndex = -1;
        }

        public void OnRecipeMouseEnter(BaseEventData evtData) {
            this.mouseInRecipe = true;
            this.mouseInQueue = false;
        }

        public void OnRecipeMouseExit(BaseEventData evtData) {
            this.mouseInRecipe = false;
            this.mouseRecipeIndex = -1;
        }

        public void OnTechUnlocked(int arg0, int arg1, bool arg2) => this.RefreshRecipeIcons();

        public void OnInstantSwitchClick() {
            this.isInstantItem = !this.isInstantItem;
            this.batchSwitch.gameObject.SetActive(this.isInstantItem);
            this.batchSwitch.SetToggleNoEvent(this.isBatch);
            GameMain.preferences.sandboxIsDirectlyObtain = this.isInstantItem;
            this.sandboxAddUsefulItemButton.gameObject.SetActive(GameMain.sandboxToolsEnabled && this.isInstantItem);
            this.sandboxClearPackageButton.gameObject.SetActive(GameMain.sandboxToolsEnabled && this.isInstantItem);
            this.RefreshRecipeIcons();
            if (this.isInstantItem)
                return;
            this.SetSelectedRecipeIndex(-1, true);
        }

        public void OnSandboxGetUsefulItems(int obj) {
            if (GameMain.isFullscreenPaused)
                return;
            ItemProto[] dataArray = LDB.items.dataArray;
            Player mainPlayer = GameMain.mainPlayer;
            for (int index = 0; index < dataArray.Length; ++index) {
                ItemProto itemProto = dataArray[index];
                int count = itemProto.CanBuild || itemProto.IsEntity ? itemProto.StackSize : 0;
                if (itemProto.ID == 1501)
                    count = itemProto.StackSize;
                else if (itemProto.ID == 1503)
                    count = itemProto.StackSize;
                else if (itemProto.ID == 1803)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1804)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1131)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1210)
                    count = itemProto.StackSize;
                else if (itemProto.ID == 2003)
                    count = itemProto.StackSize * 3;
                else if (itemProto.ID == 2013)
                    count = itemProto.StackSize * 3;
                else if (itemProto.ID == 5001)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 5002)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1603)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1606)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1608)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1611)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1613)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID == 1130)
                    count = itemProto.StackSize * 2;
                else if (itemProto.ID > 3000 && itemProto.ID <= 3999 && itemProto.ID != 3006)
                    count = itemProto.StackSize;
                if (count > 0)
                    mainPlayer.TryAddItemToPackage(itemProto.ID, count, 0, true);
            }
        }

        public void OnSandboxClearPlayerPackage(int obj) {
            if (GameMain.isFullscreenPaused)
                return;
            Player mainPlayer = GameMain.mainPlayer;
            mainPlayer.package.Clear();
            mainPlayer.package.NotifyStorageChange();
        }*/
    }
}