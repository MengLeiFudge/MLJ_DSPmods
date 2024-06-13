using BepInEx.Bootstrap;
using DSP_Battle;
using HarmonyLib;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.Compatibility.CheckPlugins;

namespace FractionateEverything.Compatibility {
    public class TheyComeFromVoid {
        internal const string GUID = "com.ckcz123.DSP_Battle";

        internal static bool Enable;
        private static Sprite alienmatrix;
        private static Sprite alienmatrixGray;
        private static bool _finished;

        internal static void Compatible() {
            Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);

            if (!Enable) return;

            alienmatrix = Resources.Load<Sprite>("Assets/DSPBattle/alienmatrix");
            alienmatrixGray = Resources.Load<Sprite>("Assets/DSPBattle/alienmatrixGray");

            var harmony = new Harmony(FractionateEverything.GUID + ".Compatibility.TheyComeFromVoid");
            harmony.PatchAll(typeof(TheyComeFromVoid));
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(TheyComeFromVoid), nameof(AfterLDBToolPostAddData)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            LogInfo("TheyComeFromVoid Compatibility Compatible finish.");
        }

        public static void AfterLDBToolPostAddData() {
            if (_finished) return;

            //xxx

            _finished = true;
            LogInfo("TheyComeFromVoid Compatibility LDBToolOnPostAddDataAction finish.");
        }

        /// <summary>
        /// 去除Resources.Load带来的大量debuglog
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIEventSystem), "RefreshESButton")]
        public static bool UIEventSystem_RefreshESButton_Prefix() {
            if (EventSystem.recorder != null && EventSystem.recorder.protoId > 0 && GameMain.instance != null) {
                //UIEventSystem.ESButtonImage.sprite = Resources.Load<Sprite>("Assets/DSPBattle/alienmatrix");
                UIEventSystem.ESButtonImage.sprite = alienmatrix;
                UIEventSystem.ESButtonHighlighting = false;
                if (EventSystem.protos.ContainsKey(EventSystem.recorder.protoId)) {
                    EventProto proto = EventSystem.protos[EventSystem.recorder.protoId];
                    int[][] decisionReqNeed = proto.decisionRequestNeed;
                    for (int i = 0; i < proto.decisionLen; i++) {
                        bool allSatisfied = true;
                        int[] reqs = decisionReqNeed[i];
                        if (reqs != null && reqs.Length > 0) {
                            for (int j = 0; j < reqs.Length; j++) {
                                int reqIndex = reqs[j];
                                if (reqIndex < EventSystem.recorder.requestCount.Length) {
                                    if (EventSystem.recorder.requestMeet[reqIndex]
                                        < EventSystem.recorder.requestCount[reqIndex]) {
                                        allSatisfied = false;
                                        break;
                                    }
                                }
                            }
                        }
                        if (allSatisfied) {
                            int[] decisionResults = proto.decisionResultId[i];
                            if (decisionResults == null) {
                                allSatisfied = false;
                                continue;
                            }
                            for (int j = 0; j < decisionResults.Length; j++) {
                                if (decisionResults[j] == -1) {
                                    allSatisfied = false;
                                    break;
                                }
                            }
                        }
                        if (allSatisfied
                            && (EventSystem.recorder.decodeType == 0
                                || EventSystem.recorder.decodeTimeSpend >= EventSystem.recorder.decodeTimeNeed)) {
                            UIEventSystem.ESButtonHighlighting = true;
                            break;
                        }
                    }
                }
            }
            else {
                //UIEventSystem.ESButtonImage.sprite = Resources.Load<Sprite>("Assets/DSPBattle/alienmatrixGray");
                UIEventSystem.ESButtonImage.sprite = alienmatrixGray;
                UIEventSystem.ESButtonHighlighting = false;
            }
            // 处理有选项可完成时，按钮始终显示以及闪烁动画
            if (UIEventSystem.ESButtonHighlighting) {
                float y = UIEventSystem.eventButtonObj.transform.localPosition.y;
                float outDis = UIRelic.relicSlotsWindowObj.transform.localPosition.x
                               + 105f
                               + 0.5f * UIRelic.resolutionX;
                UIEventSystem.eventButtonObj.transform.localPosition = new Vector3(50 + 105 - outDis, y, 0);
                UIEventSystem.attentionMarkObj.SetActive(true);
                UIEventSystem.ESButtonBorderObj.SetActive(true);
                int t = (int)(GameMain.instance.timei % 120);
                float alpha = 0.7f + 0.3f * t / 60;
                if (t > 60) {
                    alpha = 1f - 0.3f * (t - 60) / 60;
                }
                UIEventSystem.attentionMarkText.color = new Color(0.14f, 0.94f, 1f, alpha);
                UIEventSystem.ESButtonCircle.color = new Color(0.14f, 0.94f, 1f, alpha);
            }
            else {
                float y = UIEventSystem.eventButtonObj.transform.localPosition.y;
                UIEventSystem.eventButtonObj.transform.localPosition = new Vector3(50, y, 0);
                UIEventSystem.attentionMarkObj.SetActive(false);
                UIEventSystem.ESButtonBorderObj.SetActive(false);
                //ESButtonCircle.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
            return false;
        }
    }
}
