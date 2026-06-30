using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using SlimeRPG;

namespace SlimeRPG.EditorTools
{
    /// <summary>
    /// Builds the Slime RPG home screen: sky+ground world (ground ~62%), a stage-title badge,
    /// a borderless progress road, a slime formation, and a bordered bottom section that holds a
    /// full-width team row + a 5-button nav, with the dice centred above and dipping ~20% into the
    /// section's top edge. Plus the Inventory and Skill Tree overlays. Idempotent.
    /// Menu: Slime RPG/Build Home Screen.
    /// </summary>
    public static class HomeScreenBuilder
    {
        static Font _font;
        static Sprite _circle, _rounded, _hex, _disc, _dome, _roundedRect, _tubeRing;

        const float Border = 6f;

        // palette
        static readonly Color SkyBlue    = new Color(0.44f, 0.57f, 0.70f, 1f);
        static readonly Color GroundCol  = new Color(0.27f, 0.42f, 0.24f, 1f);
        static readonly Color Horizon    = new Color(0.36f, 0.52f, 0.30f, 1f);
        static readonly Color RoadCol    = new Color(0.20f, 0.30f, 0.18f, 1f);
        static readonly Color BorderCol  = new Color(0.40f, 0.47f, 0.58f, 1f);
        static readonly Color RingCol    = new Color(0.50f, 0.58f, 0.70f, 1f); // crisp dice-ring border (brighter than BorderCol)
        static readonly Color PanelDark  = new Color(0.14f, 0.16f, 0.20f, 1f);
        static readonly Color PanelDark2 = new Color(0.10f, 0.11f, 0.14f, 1f);
        static readonly Color Forest     = new Color(0.46f, 0.74f, 0.42f, 1f);
        static readonly Color ForestDim  = new Color(0.22f, 0.34f, 0.22f, 1f);
        static readonly Color GoldCol     = new Color(1f, 0.84f, 0.25f, 1f);
        static readonly Color GemCol      = new Color(0.45f, 0.80f, 1f, 1f);
        static readonly Color TextCol     = new Color(0.92f, 0.94f, 0.97f, 1f);
        static readonly Color SubTextCol  = new Color(0.66f, 0.69f, 0.75f, 1f);
        static readonly Color DiceCol     = new Color(0.96f, 0.96f, 0.98f, 1f);
        static readonly Color EnemyCol     = new Color(0.78f, 0.34f, 0.42f, 1f);
        static readonly Color EyeDark      = new Color(0.12f, 0.12f, 0.15f, 1f);

        static readonly string[] RarityNames = { "Common Slime 1", "Common Slime 2", "Common Slime 3", "Common Slime 4", "Common Slime 5" };
        static readonly Color[] RarityCols = {
            new Color(0.62f, 0.65f, 0.70f), new Color(0.35f, 0.82f, 0.40f), new Color(0.28f, 0.55f, 1f),
            new Color(0.70f, 0.35f, 1f), new Color(1f, 0.80f, 0.16f)
        };

        [MenuItem("Slime RPG/Build Home Screen")]
        public static void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _hex = GetHexSprite();

            Kill("GameCanvas"); Kill("GameManager"); Kill("EventSystem");

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            var modType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (modType != null) esGO.AddComponent(modType); else esGO.AddComponent<StandaloneInputModule>();

            var canvasGO = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGO.transform;

            var enemyContainer = BuildWorld(root, out Transform heroContainer);
            BuildTopBar(root, out Text goldText, out Text luckText);
            BuildStageTitle(root, out Text stageNumText);
            var dots = BuildProgressRoad(root);
            BuildBossTimer(root, out GameObject bossTimerRoot, out Text bossTimerText);
            BuildBottomSection(root, out Button diceBtn, out Image diceImg, out Image cooldownOverlay, out Button invBtn, out Button skillsBtn, out Button collectionBtn, out Image[] slotMini, out GameObject[] slotLock, out Button[] slotButtons, out Text[] slotButtonLabels, out GameObject[] slotCoins, out Text[] slotCostLabels, out Button[] slotEquipBtns, out DiceSpinner diceSpinner, out GameObject[] streakChips, out Image[] streakFills, out Image spinFill, out Text spinCounter, out Image spinBorder);
            BuildPullPopup(root, out Text popupName, out Text popupChance, out GameObject popupRoot);
            var invUI = BuildInventoryPanel(root, out GameObject invPanel, out Button invClose);
            var collUI = BuildCollectionPanel(root, out GameObject collPanel, out Button collClose);
            BuildSkillTreePanel(root, out GameObject skillsPanel, out Button skillsClose, out SkillNode[] skillNodes);

            var gm = new GameObject("GameManager");
            var roller = gm.AddComponent<SlimeRoller>();
            roller.SetupDefaultPool();
            roller.diceButton = diceBtn;
            roller.diceImage = diceImg;
            roller.goldText = goldText;
            roller.luckText = luckText;
            roller.popupNameText = popupName;
            roller.popupChanceText = popupChance;
            roller.popupRoot = popupRoot;
            roller.cooldownOverlay = cooldownOverlay;
            roller.streakChips = streakChips;
            roller.streakFills = streakFills;
            roller.spinFill = spinFill;
            roller.spinButtonBg = diceImg;
            roller.spinBorder = spinBorder;
            roller.spinCounterText = spinCounter;
            roller.diceSpinner = diceSpinner;
            roller.rollLabel = GameObject.Find("RollLabel"); // "TAP TO ROLL" — hidden after first roll
            roller.plusOneAnchor = diceImg.rectTransform;    // floating "+1" spawns at the dice
            roller.font = _font;
            diceSpinner.roller = roller;                      // reel pulls the curated slime list from here
            diceSpinner.SetFace(5); // show a default face at rest
            invUI.roller = roller;
            collUI.roller = roller;

            // "new slime" alert badge — parented to the NavBar (last child) so it draws OVER the adjacent button
            var collBadge = MakeCircle("CollBadge", collectionBtn.transform.parent, new Color(0.86f, 0.26f, 0.30f, 1f));
            var cbr = collBadge.rectTransform; cbr.anchorMin = cbr.anchorMax = new Vector2(0.2f, 1f); cbr.pivot = new Vector2(1f, 1f);
            cbr.sizeDelta = new Vector2(46, 46); cbr.anchoredPosition = new Vector2(-8, -6); collBadge.raycastTarget = false;
            var cbt = MakeText("N", collBadge.transform, "1", 26, Color.white, TextAnchor.MiddleCenter); cbt.raycastTarget = false; Stretch(cbt.rectTransform);
            collBadge.gameObject.SetActive(false);
            roller.collectionBadge = collBadge.gameObject; roller.collectionBadgeText = cbt;

            var nav = gm.AddComponent<ScreenNav>();
            nav.inventoryButton = invBtn; nav.skillsButton = skillsBtn; nav.collectionButton = collectionBtn;
            nav.inventoryPanel = invPanel; nav.skillsPanel = skillsPanel; nav.collectionPanel = collPanel;
            nav.inventoryClose = invClose; nav.skillsClose = skillsClose; nav.collectionClose = collClose;

            var combat = gm.AddComponent<CombatManager>();
            combat.enemyContainer = enemyContainer;
            combat.circleSprite = _circle;
            combat.font = _font;
            combat.stageNumText = stageNumText;
            combat.dots = dots;
            combat.roller = roller;
            combat.bossTimerRoot = bossTimerRoot;
            combat.bossTimerText = bossTimerText;

            var team = gm.AddComponent<TeamManager>();
            team.roller = roller;
            team.combat = combat;
            team.heroContainer = heroContainer;
            team.slotMini = slotMini;
            team.slotLock = slotLock;
            team.slotButtons = slotButtons;
            team.slotButtonLabels = slotButtonLabels;
            team.slotCoinIcons = slotCoins;
            team.slotCostLabels = slotCostLabels;
            team.slotEquipButtons = slotEquipBtns;
            team.unlockedSlots = 1;
            team.inventoryPanel = invPanel;
            team.skillsPanel = skillsPanel;
            team.skillStartNode = skillNodes.Length > 0 ? skillNodes[0] : null; // tree centre
            var heroSlots = new System.Collections.Generic.List<SkillNode>();
            foreach (var nd in skillNodes) if (nd.effect == SkillNode.Effect.HeroSlot) heroSlots.Add(nd);
            team.heroSlotNodes = heroSlots.ToArray(); // Hero Slot chain in order
            invUI.team = team;

            // wire skill nodes to the live systems
            foreach (var n in skillNodes) { n.roller = roller; n.combat = combat; n.team = team; n.spinner = diceSpinner; n.RefreshVisual(); }

            // start at zero: no gold, no slimes, empty team
            roller.gold = 0; roller.UpdateGoldUI(); roller.UpdateLuckUI();
            invUI.Refresh();

            invPanel.SetActive(false);
            skillsPanel.SetActive(false);
            collPanel.SetActive(false); // overlays start closed in the editor; opened in-game via nav

            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Slime RPG] Home screen rebuilt. Sample rolls: " + roller.rollCount);
        }

        [MenuItem("Slime RPG/Preview (World Space)")]
        public static void PreviewWorldSpace() => SetWorldSpace(true);

        [MenuItem("Slime RPG/Restore Overlay")]
        public static void RestoreOverlay() => SetWorldSpace(false);

        static void SetWorldSpace(bool world)
        {
            var canvasGO = GameObject.Find("GameCanvas");
            if (!canvasGO) { Debug.LogWarning("[Slime RPG] No GameCanvas — run Build first."); return; }
            var canvas = canvasGO.GetComponent<Canvas>();
            var rt = canvasGO.GetComponent<RectTransform>();
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            if (world)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                rt.sizeDelta = new Vector2(1080, 1920);
                rt.position = Vector3.zero;
                rt.localScale = Vector3.one * 0.01f;
            }
            else
            {
                rt.localScale = Vector3.one;
                rt.position = Vector3.zero;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
            }
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Slime RPG] Canvas " + (world ? "World Space preview" : "Screen Space - Overlay (shippable)"));
        }

        // ================= world (sky + raised ground + slime formation) =================

        static Transform BuildWorld(Transform root, out Transform heroContainer)
        {
            var world = MakeImage("World", root, SkyBlue);  // full-screen sky
            Stretch(world.rectTransform);
            var wt = world.transform;

            const float groundH = 1380f; // raised ground/horizon higher (more visible field above the tall bottom UI)
            var ground = MakeImage("Ground", wt, GroundCol);
            var gr = ground.rectTransform; gr.anchorMin = new Vector2(0, 0); gr.anchorMax = new Vector2(1, 0); gr.pivot = new Vector2(0.5f, 0);
            gr.sizeDelta = new Vector2(0, groundH); gr.anchoredPosition = Vector2.zero;
            var hz = MakeImage("Horizon", wt, Horizon);
            var hr = hz.rectTransform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(1, 0); hr.pivot = new Vector2(0.5f, 0);
            hr.sizeDelta = new Vector2(0, 5); hr.anchoredPosition = new Vector2(0, groundH);

            // heroes (left) and enemies (right) are spawned at runtime by TeamManager / CombatManager.
            // Containers are shifted UP so the formation lands in the middle of the visible ground band
            // (above the tall bottom UI), not behind it.
            const float combatLift = 270f;
            var hc = new GameObject("HeroContainer", typeof(RectTransform));
            hc.transform.SetParent(wt, false);
            var hcr = hc.GetComponent<RectTransform>(); hcr.anchorMin = Vector2.zero; hcr.anchorMax = Vector2.one; hcr.offsetMin = new Vector2(0, combatLift); hcr.offsetMax = new Vector2(0, combatLift);
            heroContainer = hc.transform;

            var ec = new GameObject("EnemyContainer", typeof(RectTransform));
            ec.transform.SetParent(wt, false);
            var ecr = ec.GetComponent<RectTransform>(); ecr.anchorMin = Vector2.zero; ecr.anchorMax = Vector2.one; ecr.offsetMin = new Vector2(0, combatLift); ecr.offsetMax = new Vector2(0, combatLift);
            return ec.transform;
        }

        /// <summary>Round slime: body circle + two eyes shifted toward the way it faces.</summary>
        static void MakeBlob(Transform parent, string name, Vector2 pos, float size, Color color, bool faceRight)
        {
            var body = MakeCircle(name, parent, color);
            var b = body.rectTransform; b.anchorMin = b.anchorMax = new Vector2(0.5f, 0.5f); b.pivot = new Vector2(0.5f, 0.5f);
            b.sizeDelta = new Vector2(size, size); b.anchoredPosition = pos;

            float eo = size * 0.16f, eye = size * 0.16f, ey = size * 0.06f;
            float face = size * 0.12f * (faceRight ? 1f : -1f);
            var le = MakeCircle("EyeL", body.transform, EyeDark);
            var ler = le.rectTransform; ler.anchorMin = ler.anchorMax = new Vector2(0.5f, 0.5f); ler.pivot = new Vector2(0.5f, 0.5f);
            ler.sizeDelta = new Vector2(eye, eye); ler.anchoredPosition = new Vector2(-eo + face, ey);
            var re = MakeCircle("EyeR", body.transform, EyeDark);
            var rer = re.rectTransform; rer.anchorMin = rer.anchorMax = new Vector2(0.5f, 0.5f); rer.pivot = new Vector2(0.5f, 0.5f);
            rer.sizeDelta = new Vector2(eye, eye); rer.anchoredPosition = new Vector2(eo + face, ey);
        }

        // ================= top bar =================

        static void BuildTopBar(Transform root, out Text goldText, out Text luckText)
        {
            // 3 smaller rounded fields floating over the sky (no full-width bar) — Gold | Gems | Luck
            const float fieldH = 74f, sideW = 244f, midW = 244f, topY = -22f; // midW kept at 244 (user-tuned diamond width)

            // Gold (left)
            var goldField = MakeRounded("GoldField", root, PanelDark2);
            var gf = goldField.rectTransform; gf.anchorMin = gf.anchorMax = new Vector2(0, 1); gf.pivot = new Vector2(0, 1);
            gf.sizeDelta = new Vector2(sideW, fieldH); gf.anchoredPosition = new Vector2(26, topY);
            var gicon = MakeCircle("GoldIcon", goldField.transform, GoldCol);
            var gic = gicon.rectTransform; gic.anchorMin = gic.anchorMax = new Vector2(0, 0.5f); gic.pivot = new Vector2(0, 0.5f);
            gic.sizeDelta = new Vector2(42, 42); gic.anchoredPosition = new Vector2(18, 0);
            goldText = MakeText("GoldText", goldField.transform, "0", 34, GoldCol, TextAnchor.MiddleLeft);
            var gt = goldText.rectTransform; gt.anchorMin = Vector2.zero; gt.anchorMax = Vector2.one;
            gt.offsetMin = new Vector2(74, 0); gt.offsetMax = new Vector2(-66, 0);
            AddInsidePlus(goldField);

            // Gems — sits right next to Gold (both left-anchored so the two are close; Luck stays on the right)
            var gemField = MakeRounded("GemField", root, PanelDark2);
            var gmf = gemField.rectTransform; gmf.anchorMin = gmf.anchorMax = new Vector2(0, 1); gmf.pivot = new Vector2(0, 1);
            gmf.sizeDelta = new Vector2(midW, fieldH); gmf.anchoredPosition = new Vector2(26 + sideW + 14, topY);
            var gem = MakePanel("GemIcon", gemField.transform, GemCol);
            var gemr = gem.rectTransform; gemr.anchorMin = gemr.anchorMax = new Vector2(0, 0.5f); gemr.pivot = new Vector2(0.5f, 0.5f); // centre pivot so the 45° rotation stays vertically centred
            gemr.sizeDelta = new Vector2(34, 34); gemr.anchoredPosition = new Vector2(38, 0); gem.transform.localRotation = Quaternion.Euler(0, 0, 45);
            var gemText = MakeText("GemText", gemField.transform, "0", 34, GemCol, TextAnchor.MiddleLeft);
            var gmt = gemText.rectTransform; gmt.anchorMin = Vector2.zero; gmt.anchorMax = Vector2.one;
            gmt.offsetMin = new Vector2(72, 0); gmt.offsetMax = new Vector2(-66, 0);
            AddInsidePlus(gemField);

            // Luck (right)
            var luckField = MakeRounded("LuckField", root, PanelDark2);
            var lp = luckField.rectTransform; lp.anchorMin = lp.anchorMax = new Vector2(1, 1); lp.pivot = new Vector2(1, 1);
            lp.sizeDelta = new Vector2(sideW, fieldH); lp.anchoredPosition = new Vector2(-26, topY);
            luckText = MakeText("LuckText", luckField.transform, "Luck 1x", 32, Forest, TextAnchor.MiddleCenter);
            var lt = luckText.rectTransform; lt.anchorMin = Vector2.zero; lt.anchorMax = Vector2.one; lt.offsetMin = new Vector2(12, 0); lt.offsetMax = new Vector2(-56, 0);
            // styled info (i) button on the right of the Luck pill
            var info = MakeCircle("LuckInfo", luckField.transform, new Color(0.30f, 0.47f, 0.64f, 1f));
            var ifr = info.rectTransform; ifr.anchorMin = ifr.anchorMax = new Vector2(1, 0.5f); ifr.pivot = new Vector2(1, 0.5f);
            ifr.sizeDelta = new Vector2(48, 48); ifr.anchoredPosition = new Vector2(-13, 0);
            info.gameObject.AddComponent<Button>().targetGraphic = info;
            var infoTxt = MakeText("i", info.transform, "i", 32, Color.white, TextAnchor.MiddleCenter);
            infoTxt.fontStyle = FontStyle.Bold; infoTxt.raycastTarget = false; Stretch(infoTxt.rectTransform);
        }

        /// <summary>Green rounded "+" button placed INSIDE a currency pill, near the right edge where the corner
        /// starts to round (smaller than the pill, on its dark background). Add-currency stub; no behaviour yet.</summary>
        static void AddInsidePlus(Image field)
        {
            var plus = MakeRounded("Plus", field.transform, new Color(0.30f, 0.62f, 0.32f, 1f));
            var pr = plus.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(1, 0.5f); pr.pivot = new Vector2(1, 0.5f);
            pr.sizeDelta = new Vector2(54, 54); pr.anchoredPosition = new Vector2(-10, 0);
            plus.gameObject.AddComponent<Button>().targetGraphic = plus;
            var t = MakeText("PlusTxt", plus.transform, "+", 44, Color.white, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold; t.raycastTarget = false; BoldOutline(t, 2); Stretch(t.rectTransform);
        }

        // ================= stage title (no background, bold + outlined, lifted) =================

        static void BuildStageTitle(Transform root, out Text stageNumText)
        {
            var holder = new GameObject("StageTitle", typeof(RectTransform));
            holder.transform.SetParent(root, false);
            var br = holder.GetComponent<RectTransform>(); br.anchorMin = br.anchorMax = new Vector2(0.5f, 1); br.pivot = new Vector2(0.5f, 1);
            br.sizeDelta = new Vector2(620, 150); br.anchoredPosition = new Vector2(0, -116);

            var s1 = MakeText("StageNum", holder.transform, "Stage 1", 66, TextCol, TextAnchor.MiddleCenter);
            BoldOutline(s1, 3);
            stageNumText = s1;
            var s1r = s1.rectTransform; s1r.anchorMin = new Vector2(0, 1); s1r.anchorMax = new Vector2(1, 1); s1r.pivot = new Vector2(0.5f, 1);
            s1r.sizeDelta = new Vector2(-24, 90); s1r.anchoredPosition = new Vector2(0, 0);

            var zn = MakeText("ZoneName", holder.transform, "Forest", 42, Forest, TextAnchor.MiddleCenter);
            BoldOutline(zn, 3);
            var znr = zn.rectTransform; znr.anchorMin = new Vector2(0, 0); znr.anchorMax = new Vector2(1, 0); znr.pivot = new Vector2(0.5f, 0);
            znr.sizeDelta = new Vector2(-24, 56); znr.anchoredPosition = new Vector2(0, 6);
        }

        // ================= boss timer =================

        static void BuildBossTimer(Transform root, out GameObject timerRoot, out Text timerText)
        {
            // nested rings: outer black border → dark red border → dark ("black-ish") red body
            var p = MakePanel("BossTimer", root, new Color(0.05f, 0.05f, 0.06f, 0.97f)); // black border (outermost)
            var pr = p.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1); pr.pivot = new Vector2(0.5f, 1);
            pr.sizeDelta = new Vector2(444, 96); pr.anchoredPosition = new Vector2(0, -372);
            var red = MakePanel("RedBorder", p.transform, new Color(0.56f, 0.13f, 0.15f, 1f)); // dark red border
            var redr = red.rectTransform; redr.anchorMin = Vector2.zero; redr.anchorMax = Vector2.one; redr.offsetMin = new Vector2(6, 6); redr.offsetMax = new Vector2(-6, -6); red.raycastTarget = false;
            var body = MakePanel("Fill", red.transform, new Color(0.25f, 0.06f, 0.08f, 1f)); // dark ("black lighter red") body
            var bodyr = body.rectTransform; bodyr.anchorMin = Vector2.zero; bodyr.anchorMax = Vector2.one; bodyr.offsetMin = new Vector2(6, 6); bodyr.offsetMax = new Vector2(-6, -6); body.raycastTarget = false;
            var lab = MakeText("Label", body.transform, "BOSS", 34, new Color(1f, 0.62f, 0.55f), TextAnchor.MiddleLeft);
            BoldOutline(lab, 2);
            var lr = lab.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(0.5f, 1); lr.offsetMin = new Vector2(30, 0); lr.offsetMax = Vector2.zero;
            var t = MakeText("Time", body.transform, "20s", 50, Color.white, TextAnchor.MiddleRight);
            BoldOutline(t, 2);
            var tr = t.rectTransform; tr.anchorMin = new Vector2(0.5f, 0); tr.anchorMax = new Vector2(1, 1); tr.offsetMin = Vector2.zero; tr.offsetMax = new Vector2(-30, 0);
            p.gameObject.SetActive(false);
            timerRoot = p.gameObject; timerText = t;
        }

        // ================= progress road (no border) =================

        static Image[] BuildProgressRoad(Transform root)
        {
            var prog = new GameObject("ProgressRoad", typeof(RectTransform));
            prog.transform.SetParent(root, false);
            var pr = prog.GetComponent<RectTransform>(); pr.anchorMin = new Vector2(0, 1); pr.anchorMax = new Vector2(1, 1); pr.pivot = new Vector2(0.5f, 1);
            pr.sizeDelta = new Vector2(0, 100); pr.anchoredPosition = new Vector2(0, -258);
            var pt = prog.transform;

            var line = MakeImage("Road", pt, RoadCol);
            var ln = line.rectTransform; ln.anchorMin = ln.anchorMax = new Vector2(0.5f, 0.5f); ln.pivot = new Vector2(0.5f, 0.5f);
            ln.sizeDelta = new Vector2(900, 8); ln.anchoredPosition = Vector2.zero;

            int dotCount = 10; float spanW = 900f; float stepX = spanW / (dotCount - 1); float startX = -spanW / 2f;
            var dots = new Image[dotCount];
            for (int i = 0; i < dotCount; i++)
            {
                int stageNum = i + 1;
                bool isBoss = (stageNum % 10 == 0); bool isCurrent = (stageNum == 1);
                float size = isBoss ? 72f : 46f; // boss dot is just bigger, same green palette
                Color dc = isCurrent ? Forest : ForestDim;
                var dot = MakeCircle("Dot_" + stageNum, pt, dc);
                var d = dot.rectTransform; d.anchorMin = d.anchorMax = new Vector2(0.5f, 0.5f); d.pivot = new Vector2(0.5f, 0.5f);
                d.sizeDelta = new Vector2(size, size); d.anchoredPosition = new Vector2(startX + i * stepX, 0);
                var num = MakeText("Num", dot.transform, stageNum.ToString(), isBoss ? 30 : 24, Color.white, TextAnchor.MiddleCenter);
                Stretch(num.rectTransform);
                dots[i] = dot;
            }
            return dots;
        }

        // ===== bottom: fixed navbar + scrollable 7-slot slime roster + dice dome =====

        static void BuildBottomSection(Transform root, out Button diceBtn, out Image diceImg, out Image cooldownOverlay, out Button invBtn, out Button skillsBtn, out Button collectionBtn, out Image[] slotMini, out GameObject[] slotLock, out Button[] slotButtons, out Text[] slotButtonLabels, out GameObject[] slotCoins, out Text[] slotCostLabels, out Button[] slotEquipBtns, out DiceSpinner diceSpinner, out GameObject[] streakChips, out Image[] streakFills, out Image spinFill, out Text spinCounter, out Image spinBorder)
        {
            const int SLOTS = 7;
            Color cellFill = new Color(0.13f, 0.15f, 0.18f, 1f);

            // ---------- fixed bottom navbar (Battle in the middle) ----------
            float navH = 156f;
            var navBar = MakePanel("NavBar", root, PanelDark);
            var nbr = navBar.rectTransform; nbr.anchorMin = new Vector2(0, 0); nbr.anchorMax = new Vector2(1, 0); nbr.pivot = new Vector2(0.5f, 0);
            nbr.sizeDelta = new Vector2(0, navH); nbr.anchoredPosition = Vector2.zero;
            var navTop = MakeImage("TopLine", navBar.transform, BorderCol);
            var ntr = navTop.rectTransform; ntr.anchorMin = new Vector2(0, 1); ntr.anchorMax = new Vector2(1, 1); ntr.pivot = new Vector2(0.5f, 1);
            ntr.sizeDelta = new Vector2(0, 6); ntr.anchoredPosition = Vector2.zero; navTop.raycastTarget = false;

            string[] navNames = { "Collection", "Upgrades", "Battle", "Inventory", "Skill Tree" };
            Color battleCol = new Color(0.24f, 0.42f, 0.28f, 1f);
            var navBtns = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                bool isBattle = (i == 2);
                var b = MakeImage("Nav_" + navNames[i].Replace(" ", ""), navBar.transform, isBattle ? battleCol : PanelDark2);
                var r = b.rectTransform; r.anchorMin = new Vector2(i / 5f, 0); r.anchorMax = new Vector2((i + 1) / 5f, 1);
                r.offsetMin = new Vector2(0, 0); r.offsetMax = new Vector2(0, -6); // flush, no gaps (top 6 = the border line)
                var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b; navBtns[i] = bt;
                var lab = MakeText("Label", b.transform, navNames[i], isBattle ? 30 : 25, isBattle ? Color.white : TextCol, TextAnchor.MiddleCenter);
                lab.raycastTarget = false; if (isBattle) lab.fontStyle = FontStyle.Bold; Stretch(lab.rectTransform);
            }
            // thin vertical borders between the (now flush) buttons
            for (int i = 1; i < 5; i++)
            {
                var div = MakeImage("Divider_" + i, navBar.transform, BorderCol);
                var dvr = div.rectTransform; dvr.anchorMin = new Vector2(i / 5f, 0); dvr.anchorMax = new Vector2(i / 5f, 1);
                dvr.pivot = new Vector2(0.5f, 0); dvr.sizeDelta = new Vector2(3, -6); dvr.anchoredPosition = new Vector2(0, 0);
                div.raycastTarget = false;
            }
            invBtn = navBtns[3]; skillsBtn = navBtns[4]; collectionBtn = navBtns[0];

            // ---------- scrollable slime roster (7 horizontal slot-rows) ----------
            float rosterH = 720f;
            var rosterOuter = MakePanel("RosterPanel", root, BorderCol);
            var ror = rosterOuter.rectTransform; ror.anchorMin = new Vector2(0, 0); ror.anchorMax = new Vector2(1, 0); ror.pivot = new Vector2(0.5f, 0);
            ror.sizeDelta = new Vector2(0, rosterH); ror.anchoredPosition = new Vector2(0, navH);
            var rosterFill = MakePanel("Fill", rosterOuter.transform, PanelDark);
            var rfr = rosterFill.rectTransform; rfr.anchorMin = Vector2.zero; rfr.anchorMax = Vector2.one; rfr.offsetMin = new Vector2(Border, Border); rfr.offsetMax = new Vector2(-Border, -Border);
            rosterFill.raycastTarget = false;

            var scroll = rosterOuter.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true; scroll.decelerationRate = 0.135f; scroll.scrollSensitivity = 20f;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGO.transform.SetParent(rosterFill.transform, false);
            var vp = viewportGO.GetComponent<RectTransform>(); vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = new Vector2(8, 8); vp.offsetMax = new Vector2(-8, -8);
            viewportGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.0015f); // near-invisible, just catches drag
            scroll.viewport = vp;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var content = contentGO.GetComponent<RectTransform>(); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            float rowH = 150f, rowGap = 12f;
            content.sizeDelta = new Vector2(0, rowGap + SLOTS * (rowH + rowGap)); content.anchoredPosition = Vector2.zero;
            scroll.content = content;

            slotMini = new Image[SLOTS]; slotLock = new GameObject[SLOTS]; slotButtons = new Button[SLOTS]; slotButtonLabels = new Text[SLOTS];
            slotCoins = new GameObject[SLOTS]; slotCostLabels = new Text[SLOTS]; slotEquipBtns = new Button[SLOTS];
            for (int i = 0; i < SLOTS; i++)
            {
                float y = -rowGap - i * (rowH + rowGap);
                var rowOuter = MakePanel("Row_" + (i + 1), content, BorderCol);
                var rrt = rowOuter.rectTransform; rrt.anchorMin = new Vector2(0, 1); rrt.anchorMax = new Vector2(1, 1); rrt.pivot = new Vector2(0.5f, 1);
                rrt.sizeDelta = new Vector2(-16, rowH); rrt.anchoredPosition = new Vector2(0, y);
                var rowFill = MakePanel("Fill", rowOuter.transform, cellFill);
                var rfill = rowFill.rectTransform; rfill.anchorMin = Vector2.zero; rfill.anchorMax = Vector2.one; rfill.offsetMin = new Vector2(4, 4); rfill.offsetMax = new Vector2(-4, -4);
                rowFill.raycastTarget = false;

                // left: framed slime portrait (border frame + inset; slime icon shown once equipped)
                var portrait = MakeRounded("Portrait", rowFill.transform, BorderCol);
                var por = portrait.rectTransform; por.anchorMin = por.anchorMax = new Vector2(0, 0.5f); por.pivot = new Vector2(0, 0.5f);
                por.sizeDelta = new Vector2(120, 120); por.anchoredPosition = new Vector2(18, 0); portrait.raycastTarget = false;
                var portIn = MakeRounded("In", portrait.transform, new Color(0.10f, 0.12f, 0.15f, 1f));
                var pinr = portIn.rectTransform; pinr.anchorMin = Vector2.zero; pinr.anchorMax = Vector2.one; pinr.offsetMin = new Vector2(6, 6); pinr.offsetMax = new Vector2(-6, -6); portIn.raycastTarget = false;
                var mini = MakeCircle("Mini", portIn.transform, RarityCols[0]);
                var mr = mini.rectTransform; mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0.5f); mr.pivot = new Vector2(0.5f, 0.5f);
                mr.sizeDelta = new Vector2(94, 94); mr.anchoredPosition = Vector2.zero;
                mini.raycastTarget = false; AddFace(mini.transform, 94f); mini.gameObject.SetActive(false);
                slotMini[i] = mini;

                // green "+" equip button over the empty portrait — shown when the slot is unlocked but empty; opens inventory
                var eqBtn = MakeRounded("EquipBtn", rowFill.transform, new Color(0.30f, 0.62f, 0.32f, 1f));
                var eqr = eqBtn.rectTransform; eqr.anchorMin = eqr.anchorMax = new Vector2(0, 0.5f); eqr.pivot = new Vector2(0.5f, 0.5f);
                eqr.sizeDelta = new Vector2(96, 96); eqr.anchoredPosition = new Vector2(78, 0); // centred over the 120px portrait
                slotEquipBtns[i] = eqBtn.gameObject.AddComponent<Button>(); slotEquipBtns[i].targetGraphic = eqBtn;
                var eqlb = MakeText("Plus", eqBtn.transform, "+", 60, Color.white, TextAnchor.MiddleCenter); eqlb.fontStyle = FontStyle.Bold; eqlb.raycastTarget = false; BoldOutline(eqlb, 2); Stretch(eqlb.rectTransform);
                eqBtn.gameObject.SetActive(false);

                // level + ability slots
                var lvl = MakeText("Level", rowFill.transform, "Lv 1", 28, TextCol, TextAnchor.MiddleLeft);
                lvl.raycastTarget = false;
                var lvr = lvl.rectTransform; lvr.anchorMin = lvr.anchorMax = new Vector2(0, 1); lvr.pivot = new Vector2(0, 1);
                lvr.sizeDelta = new Vector2(170, 40); lvr.anchoredPosition = new Vector2(180, -22);
                for (int a = 0; a < 3; a++)
                {
                    // stylized "+" ability slot (rounded dark cell + bordered inset + green plus glyph)
                    var ab = MakeRounded("Ability_" + a, rowFill.transform, new Color(0.34f, 0.40f, 0.30f, 1f));
                    var abr = ab.rectTransform; abr.anchorMin = abr.anchorMax = new Vector2(0, 0); abr.pivot = new Vector2(0, 0);
                    abr.sizeDelta = new Vector2(54, 54); abr.anchoredPosition = new Vector2(180 + a * 62, 20);
                    ab.raycastTarget = false;
                    var abIn = MakeRounded("In", ab.transform, new Color(0.15f, 0.17f, 0.21f, 1f));
                    var abinr = abIn.rectTransform; abinr.anchorMin = Vector2.zero; abinr.anchorMax = Vector2.one; abinr.offsetMin = new Vector2(3, 3); abinr.offsetMax = new Vector2(-3, -3);
                    abIn.raycastTarget = false;
                    var plus = MakeText("Plus", abIn.transform, "+", 42, new Color(0.56f, 0.86f, 0.52f), TextAnchor.MiddleCenter);
                    plus.fontStyle = FontStyle.Bold; plus.raycastTarget = false; BoldOutline(plus, 1); Stretch(plus.rectTransform);
                }

                // right: Slot Upgrade / Unlock button
                var btnPanel = MakePanel("SlotButton", rowFill.transform, new Color(0.28f, 0.42f, 0.58f, 1f));
                var bpr = btnPanel.rectTransform; bpr.anchorMin = bpr.anchorMax = new Vector2(1, 0.5f); bpr.pivot = new Vector2(1, 0.5f);
                bpr.sizeDelta = new Vector2(220, 96); bpr.anchoredPosition = new Vector2(-20, 0);
                var sbtn = btnPanel.gameObject.AddComponent<Button>(); sbtn.targetGraphic = btnPanel; slotButtons[i] = sbtn;
                var sblab = MakeText("Label", btnPanel.transform, "Upgrade", 26, Color.white, TextAnchor.MiddleCenter);
                sblab.raycastTarget = false;
                var sblr = sblab.rectTransform; sblr.anchorMin = Vector2.zero; sblr.anchorMax = Vector2.one; sblr.offsetMin = new Vector2(0, 30); sblr.offsetMax = Vector2.zero; // upper area
                slotButtonLabels[i] = sblab;
                // gold coin + cost (shown only in the Upgrade state)
                var coin = MakeCircle("Coin", btnPanel.transform, GoldCol); coin.raycastTarget = false;
                var coinR = coin.rectTransform; coinR.anchorMin = coinR.anchorMax = new Vector2(0.5f, 0); coinR.pivot = new Vector2(0.5f, 0.5f);
                coinR.sizeDelta = new Vector2(26, 26); coinR.anchoredPosition = new Vector2(-30, 18);
                slotCoins[i] = coin.gameObject;
                var cost = MakeText("Cost", btnPanel.transform, "100", 24, GoldCol, TextAnchor.MiddleLeft); cost.raycastTarget = false;
                var costR = cost.rectTransform; costR.anchorMin = costR.anchorMax = new Vector2(0.5f, 0); costR.pivot = new Vector2(0, 0.5f);
                costR.sizeDelta = new Vector2(80, 30); costR.anchoredPosition = new Vector2(-10, 18);
                slotCostLabels[i] = cost;

                // lock overlay covers the WHOLE row (no separate "Locked" text — the button shows the state);
                // the Unlock/Upgrade button is raised above it so only the button is visibly clickable
                var lockOv = MakePanel("Lock", rowFill.transform, new Color(0.07f, 0.08f, 0.10f, 0.94f));
                var lkr = lockOv.rectTransform; lkr.anchorMin = Vector2.zero; lkr.anchorMax = Vector2.one; lkr.offsetMin = Vector2.zero; lkr.offsetMax = Vector2.zero;
                lockOv.gameObject.SetActive(i >= 1); // only slot 1 unlocked at start
                slotLock[i] = lockOv.gameObject;
                btnPanel.transform.SetAsLastSibling(); // keep the button (and its coin/cost) on top of the lock overlay
            }

            // ===== central SPIN button: stone-ring frame + radial streak fills + 3 unlock chips =====
            // Smaller circle SEATED INTO the roster panel: built on its own root that renders BEHIND the
            // navbar + roster panel (sibling index set below), so the bottom of the circle is cut off by the
            // panel's top edge while the top half (button + chips) sits over the battlefield.
            var disc = GetDiscSprite();
            float frameDia = 300f, btnDia = 190f;
            float rosterTopY = navH + rosterH;     // panel top edge, measured from the canvas bottom
            float CY = rosterTopY + 96f;            // circle centre: ~54px of the frame dips behind the panel
            Color spinDefault = new Color(0.30f, 0.62f, 0.34f, 1f); // green by default; recoloured per roll type at runtime

            var spinRoot = new GameObject("SpinAssembly", typeof(RectTransform));
            spinRoot.transform.SetParent(root, false);
            var sprt = spinRoot.GetComponent<RectTransform>(); sprt.anchorMin = sprt.anchorMax = new Vector2(0.5f, 0); sprt.pivot = new Vector2(0.5f, 0); sprt.sizeDelta = Vector2.zero; sprt.anchoredPosition = Vector2.zero;

            // neon glow halo behind the frame
            var glow = MakeImage("SpinGlow", spinRoot.transform, new Color(0.40f, 0.74f, 0.45f, 0.40f));
            glow.sprite = disc; glow.raycastTarget = false;
            var glr = glow.rectTransform; glr.anchorMin = glr.anchorMax = new Vector2(0.5f, 0); glr.pivot = new Vector2(0.5f, 0.5f);
            glr.sizeDelta = new Vector2(frameDia + 26, frameDia + 26); glr.anchoredPosition = new Vector2(0, CY);

            // stone frame (outer ring) + inner bevel — everything else is parented under the frame
            var frame = MakeImage("SpinFrame", spinRoot.transform, new Color(0.31f, 0.35f, 0.42f, 1f));
            frame.sprite = disc; frame.raycastTarget = false;
            var frr = frame.rectTransform; frr.anchorMin = frr.anchorMax = new Vector2(0.5f, 0); frr.pivot = new Vector2(0.5f, 0.5f);
            frr.sizeDelta = new Vector2(frameDia, frameDia); frr.anchoredPosition = new Vector2(0, CY);
            var bevel = MakeImage("Bevel", frame.transform, new Color(0.18f, 0.21f, 0.26f, 1f));
            bevel.sprite = disc; bevel.raycastTarget = false;
            var bvr = bevel.rectTransform; bvr.anchorMin = bvr.anchorMax = new Vector2(0.5f, 0.5f); bvr.pivot = new Vector2(0.5f, 0.5f);
            bvr.sizeDelta = new Vector2(frameDia - 34, frameDia - 34); bvr.anchoredPosition = Vector2.zero;

            // roll ring (radial): a single TUBE-look ring — dark in the middle, lighter at the edges (same hue),
            // like the boss-timer border — tinted to the roll-type colour. Fills as the dice spins; full once it lands.
            spinFill = MakeImage("SpinFill", frame.transform, new Color(0.42f, 0.78f, 0.42f, 1f)); // default green (recoloured per roll)
            spinFill.sprite = GetTubeRingSprite(); spinFill.raycastTarget = false;
            spinFill.type = Image.Type.Filled; spinFill.fillMethod = Image.FillMethod.Radial360;
            spinFill.fillOrigin = (int)Image.Origin360.Bottom; spinFill.fillClockwise = true; spinFill.fillAmount = 0f;
            var sfr = spinFill.rectTransform; sfr.anchorMin = sfr.anchorMax = new Vector2(0.5f, 0.5f); sfr.pivot = new Vector2(0.5f, 0.5f);
            sfr.sizeDelta = new Vector2(btnDia + 76, btnDia + 76); sfr.anchoredPosition = Vector2.zero;

            // border ring hugging the button — turns a darker streak colour when a streak is armed
            spinBorder = MakeImage("SpinBorder", frame.transform, new Color(0.22f, 0.25f, 0.30f, 1f));
            spinBorder.sprite = disc; spinBorder.raycastTarget = false;
            var sbr = spinBorder.rectTransform; sbr.anchorMin = sbr.anchorMax = new Vector2(0.5f, 0.5f); sbr.pivot = new Vector2(0.5f, 0.5f);
            sbr.sizeDelta = new Vector2(btnDia + 18, btnDia + 18); sbr.anchoredPosition = Vector2.zero;

            // the SPIN button itself (roll trigger). Turns gold/platinum/diamond when a streak is armed.
            var dice = MakeImage("DiceButton", frame.transform, spinDefault);
            dice.sprite = disc;
            var dr = dice.rectTransform; dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f); dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(btnDia, btnDia); dr.anchoredPosition = Vector2.zero;
            diceImg = dice;
            diceBtn = dice.gameObject.AddComponent<Button>();
            diceBtn.transition = Selectable.Transition.None; diceBtn.targetGraphic = dice;
            var blur = dice.gameObject.AddComponent<CanvasGroup>();
            diceSpinner = dice.gameObject.AddComponent<DiceSpinner>();
            diceSpinner.target = dice.rectTransform; diceSpinner.spinDuration = 2f; diceSpinner.blurGroup = blur;
            diceSpinner.facePips = new Image[0]; // no cube pips anymore — the reel covers the spin
            var spinLab = MakeText("SpinLabel", dice.transform, "SPIN", 52, Color.white, TextAnchor.MiddleCenter);
            BoldOutline(spinLab, 3); spinLab.raycastTarget = false;
            var slr = spinLab.rectTransform; slr.anchorMin = Vector2.zero; slr.anchorMax = Vector2.one; slr.offsetMin = new Vector2(0, 14); slr.offsetMax = Vector2.zero; // lift text so the counter fits below

            // (no dark cooldown radial — the gold roll ring is the only fill indicator now)
            cooldownOverlay = null;

            // reel: a SLIME FACE (body + eyes) flashing over the button while spinning; "2X" frame mixed in
            float reelD = btnDia * 0.9f;
            var reel = MakeCircle("DiceReel", frame.transform, RarityCols[0]);
            var rlr = reel.rectTransform; rlr.anchorMin = rlr.anchorMax = new Vector2(0.5f, 0.5f); rlr.pivot = new Vector2(0.5f, 0.5f);
            rlr.sizeDelta = new Vector2(reelD, reelD); rlr.anchoredPosition = Vector2.zero;
            reel.raycastTarget = false;
            var reelEyes = new GameObject("Eyes", typeof(RectTransform));
            reelEyes.transform.SetParent(reel.transform, false);
            Stretch(reelEyes.GetComponent<RectTransform>());
            float reo = reelD * 0.16f, reye = reelD * 0.17f, rey = reelD * 0.06f;
            var eL = MakeCircle("EyeL", reelEyes.transform, EyeDark); eL.raycastTarget = false;
            var eLr = eL.rectTransform; eLr.anchorMin = eLr.anchorMax = new Vector2(0.5f, 0.5f); eLr.pivot = new Vector2(0.5f, 0.5f); eLr.sizeDelta = new Vector2(reye, reye); eLr.anchoredPosition = new Vector2(-reo, rey);
            var eR = MakeCircle("EyeR", reelEyes.transform, EyeDark); eR.raycastTarget = false;
            var eRr = eR.rectTransform; eRr.anchorMin = eRr.anchorMax = new Vector2(0.5f, 0.5f); eRr.pivot = new Vector2(0.5f, 0.5f); eRr.sizeDelta = new Vector2(reye, reye); eRr.anchoredPosition = new Vector2(reo, rey);
            var reelTxt = MakeText("ReelText", reel.transform, "2X", 70, Color.white, TextAnchor.MiddleCenter);
            BoldOutline(reelTxt, 4); reelTxt.raycastTarget = false; Stretch(reelTxt.rectTransform);
            reelTxt.gameObject.SetActive(false);
            reel.gameObject.SetActive(false);
            diceSpinner.reelIcon = reel; diceSpinner.reelEyes = reelEyes; diceSpinner.reelText = reelTxt;
            diceSpinner.reelColors = (Color[])RarityCols.Clone();

            // "x/10" counter pill below the button (Gold streak); hidden until Gold Roll unlocks
            var pill = MakeRounded("CounterPill", frame.transform, new Color(0.08f, 0.09f, 0.12f, 0.95f));
            var pr2 = pill.rectTransform; pr2.anchorMin = pr2.anchorMax = new Vector2(0.5f, 0.5f); pr2.pivot = new Vector2(0.5f, 0.5f);
            pr2.sizeDelta = new Vector2(122, 44); pr2.anchoredPosition = new Vector2(0, -btnDia / 2f + 18f); // sits low on the button, above the cut
            var pdie = MakePanel("Die", pill.transform, DiceCol); pdie.raycastTarget = false;
            var pdr = pdie.rectTransform; pdr.anchorMin = pdr.anchorMax = new Vector2(0, 0.5f); pdr.pivot = new Vector2(0, 0.5f);
            pdr.sizeDelta = new Vector2(26, 26); pdr.anchoredPosition = new Vector2(14, 0);
            spinCounter = MakeText("Count", pill.transform, "0/10", 26, TextCol, TextAnchor.MiddleCenter); spinCounter.raycastTarget = false;
            var scr2 = spinCounter.rectTransform; scr2.anchorMin = Vector2.zero; scr2.anchorMax = Vector2.one; scr2.offsetMin = new Vector2(32, 0); scr2.offsetMax = new Vector2(-6, 0);
            pill.gameObject.SetActive(false);

            // 3 streak chips on the top arc: Gold (right), Platinum (top), Diamond (left). Hidden until unlocked.
            Color[] chipCols = { new Color(1f, 0.84f, 0.25f), new Color(0.82f, 0.86f, 0.94f), new Color(0.45f, 0.84f, 0.98f) };
            string[] chipNames = { "Gold", "Platinum", "Diamond" };
            string[] chipGlyph = { "★", "◆", "◆" };
            float[] chipAng = { 35f, 90f, 145f };
            float chipDia = 74f, arcR = frameDia / 2f - 4f;
            streakChips = new GameObject[3]; streakFills = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var chip = new GameObject("Streak_" + chipNames[i], typeof(RectTransform));
                chip.transform.SetParent(frame.transform, false);
                var chr = chip.GetComponent<RectTransform>(); chr.anchorMin = chr.anchorMax = new Vector2(0.5f, 0.5f); chr.pivot = new Vector2(0.5f, 0.5f);
                chr.sizeDelta = new Vector2(chipDia, chipDia);
                float a = chipAng[i] * Mathf.Deg2Rad;
                chr.anchoredPosition = new Vector2(Mathf.Cos(a) * arcR, Mathf.Sin(a) * arcR);

                var chipBack = MakeImage("Back", chip.transform, new Color(0.10f, 0.12f, 0.16f, 1f));
                chipBack.sprite = disc; chipBack.raycastTarget = false; Stretch(chipBack.rectTransform);

                var chipFill = MakeImage("Fill", chip.transform, chipCols[i]);
                chipFill.sprite = GetTubeRingSprite(); chipFill.raycastTarget = false;
                chipFill.type = Image.Type.Filled; chipFill.fillMethod = Image.FillMethod.Radial360;
                chipFill.fillOrigin = (int)Image.Origin360.Bottom; chipFill.fillClockwise = true; chipFill.fillAmount = 0f;
                Stretch(chipFill.rectTransform);
                streakFills[i] = chipFill;

                var med = MakeImage("Medallion", chip.transform, new Color(0.13f, 0.15f, 0.20f, 1f));
                med.sprite = disc; med.raycastTarget = false;
                var mr2 = med.rectTransform; mr2.anchorMin = mr2.anchorMax = new Vector2(0.5f, 0.5f); mr2.pivot = new Vector2(0.5f, 0.5f);
                mr2.sizeDelta = new Vector2(chipDia - 16, chipDia - 16); mr2.anchoredPosition = Vector2.zero;
                var glyph = MakeText("Glyph", med.transform, chipGlyph[i], 34, chipCols[i], TextAnchor.MiddleCenter);
                BoldOutline(glyph, 2); glyph.raycastTarget = false; Stretch(glyph.rectTransform);

                chip.SetActive(false);
                streakChips[i] = chip;
            }

            // floating 2x Speed + Ascend badges (bordered) on the roster's top-right edge, above the Upgrade buttons
            MakeCornerBadge(rosterOuter.transform, "SpeedBtn", new Vector2(-158, -8), new Color(0.24f, 0.44f, 0.30f, 1f), "2x", "Speed", new Color(0.85f, 0.92f, 0.85f));
            MakeCornerBadge(rosterOuter.transform, "AscendBtn", new Vector2(-26, -8), new Color(0.42f, 0.34f, 0.56f, 1f), "▲", "Ascend", new Color(0.90f, 0.86f, 0.98f));

            var roll = MakeText("RollLabel", spinRoot.transform, "TAP TO ROLL", 26, TextCol, TextAnchor.MiddleCenter);
            var rr = roll.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0); rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = new Vector2(360, 40); rr.anchoredPosition = new Vector2(0, CY + frameDia / 2f + 22);

            // render the whole SPIN assembly BEHIND the navbar + roster panel so its bottom is cut by the panel edge
            spinRoot.transform.SetSiblingIndex(navBar.transform.GetSiblingIndex());
        }

        /// <summary>Bordered top-right corner badge (BorderCol frame + coloured inset) with a big glyph + small label.
        /// Sits on the roster panel's top edge; carries a no-op Button for now.</summary>
        static void MakeCornerBadge(Transform parent, string name, Vector2 pos, Color fill, string big, string label, Color labelCol)
        {
            var outer = MakeRounded(name, parent, BorderCol);
            var or = outer.rectTransform; or.anchorMin = or.anchorMax = new Vector2(1, 1); or.pivot = new Vector2(1, 0);
            or.sizeDelta = new Vector2(122, 112); or.anchoredPosition = pos;
            outer.gameObject.AddComponent<Button>().targetGraphic = outer;
            var inner = MakeRounded("Fill", outer.transform, fill);
            var inr = inner.rectTransform; inr.anchorMin = Vector2.zero; inr.anchorMax = Vector2.one; inr.offsetMin = new Vector2(5, 5); inr.offsetMax = new Vector2(-5, -5); inner.raycastTarget = false;
            var b = MakeText("Big", inner.transform, big, 40, Color.white, TextAnchor.MiddleCenter); b.fontStyle = FontStyle.Bold; b.raycastTarget = false;
            var br = b.rectTransform; br.anchorMin = new Vector2(0, 0.34f); br.anchorMax = new Vector2(1, 1); br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            var l = MakeText("L", inner.transform, label, 22, labelCol, TextAnchor.MiddleCenter); l.raycastTarget = false;
            var lr = l.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0.36f); lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        }

        /// <summary>Bordered panel: a BorderCol outer frame + an inset coloured fill (bottom-anchored grid cell).
        /// Returns the fill (parent children to it); outputs the outer image (use as the Button target).</summary>
        static Image MakeBordered(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Color fillColor, out Image outer, float border = 4f)
        {
            outer = MakePanel(name, parent, BorderCol);
            var oR = outer.rectTransform; oR.anchorMin = oR.anchorMax = new Vector2(0.5f, 0); oR.pivot = new Vector2(0.5f, 0);
            oR.sizeDelta = size; oR.anchoredPosition = anchoredPos;
            var f = MakePanel("Fill", outer.transform, fillColor);
            var fR = f.rectTransform; fR.anchorMin = Vector2.zero; fR.anchorMax = Vector2.one;
            fR.offsetMin = new Vector2(border, border); fR.offsetMax = new Vector2(-border, -border);
            f.raycastTarget = false;
            return f;
        }

        // ================= pull popup =================

        static void BuildPullPopup(Transform root, out Text nameText, out Text chanceText, out GameObject popupRoot)
        {
            // no background/border — just bold outlined text over the sky: name on top, chance below
            var holder = new GameObject("PullPopup", typeof(RectTransform), typeof(CanvasGroup));
            holder.transform.SetParent(root, false);
            var p = holder.GetComponent<RectTransform>(); p.anchorMin = p.anchorMax = new Vector2(0.5f, 1); p.pivot = new Vector2(0.5f, 1);
            p.sizeDelta = new Vector2(860, 170); p.anchoredPosition = new Vector2(0, -470);
            popupRoot = holder;

            nameText = MakeText("PopupName", holder.transform, "—", 62, TextCol, TextAnchor.MiddleCenter);
            BoldOutline(nameText, 3);
            var n = nameText.rectTransform; n.anchorMin = new Vector2(0, 1); n.anchorMax = new Vector2(1, 1); n.pivot = new Vector2(0.5f, 1);
            n.sizeDelta = new Vector2(-20, 96); n.anchoredPosition = new Vector2(0, 0);

            chanceText = MakeText("PopupChance", holder.transform, "", 46, TextCol, TextAnchor.MiddleCenter);
            BoldOutline(chanceText, 3);
            var c = chanceText.rectTransform; c.anchorMin = new Vector2(0, 0); c.anchorMax = new Vector2(1, 0); c.pivot = new Vector2(0.5f, 0);
            c.sizeDelta = new Vector2(-20, 64); c.anchoredPosition = new Vector2(0, 8);
        }

        // ================= inventory =================

        static InventoryUI BuildInventoryPanel(Transform root, out GameObject panelGO, out Button closeBtn)
        {
            var panel = MakeImage("InventoryPanel", root, new Color(0, 0, 0, 0.66f));
            Stretch(panel.rectTransform);
            panelGO = panel.gameObject;
            // render above the HP bars' override-sorting canvas (20) so bars don't bleed through this overlay
            var icv = panelGO.AddComponent<Canvas>(); icv.overrideSorting = true; icv.sortingOrder = 30;
            panelGO.AddComponent<GraphicRaycaster>();
            var ui = panelGO.AddComponent<InventoryUI>();

            var card = MakePanel("Card", panel.transform, PanelDark);
            var cr = card.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(940, 1340); cr.anchoredPosition = Vector2.zero;
            closeBtn = MakeCloseButton(card.transform);

            Color cellBg = new Color(0.13f, 0.15f, 0.18f, 1f);
            Color tabOn = new Color(0.28f, 0.42f, 0.58f, 1f), tabOff = new Color(0.16f, 0.18f, 0.22f, 1f);
            Color scrollBg = new Color(0.09f, 0.10f, 0.13f, 1f);

            // ----- tabs: Slimes | Items (left-anchored so the top-right corner stays free for the X) -----
            var slimesTab = MakePanel("SlimesTab", card.transform, tabOn);
            var st = slimesTab.rectTransform; st.anchorMin = st.anchorMax = new Vector2(0, 1); st.pivot = new Vector2(0, 1);
            st.sizeDelta = new Vector2(360, 92); st.anchoredPosition = new Vector2(24, -24);
            var slimesTabBtn = slimesTab.gameObject.AddComponent<Button>(); slimesTabBtn.targetGraphic = slimesTab;
            var stl = MakeText("Label", slimesTab.transform, "Slimes", 36, Color.white, TextAnchor.MiddleCenter); stl.raycastTarget = false; Stretch(stl.rectTransform);

            var itemsTab = MakePanel("ItemsTab", card.transform, tabOff);
            var it = itemsTab.rectTransform; it.anchorMin = it.anchorMax = new Vector2(0, 1); it.pivot = new Vector2(0, 1);
            it.sizeDelta = new Vector2(360, 92); it.anchoredPosition = new Vector2(400, -24);
            var itemsTabBtn = itemsTab.gameObject.AddComponent<Button>(); itemsTabBtn.targetGraphic = itemsTab;
            var itl = MakeText("Label", itemsTab.transform, "Items", 36, Color.white, TextAnchor.MiddleCenter); itl.raycastTarget = false; Stretch(itl.rectTransform);

            // ----- panel containers (below tabs) -----
            var slimesPanel = new GameObject("SlimesPanel", typeof(RectTransform));
            slimesPanel.transform.SetParent(card.transform, false);
            var sp = slimesPanel.GetComponent<RectTransform>(); sp.anchorMin = Vector2.zero; sp.anchorMax = Vector2.one; sp.offsetMin = new Vector2(24, 24); sp.offsetMax = new Vector2(-24, -140);

            var itemsPanel = new GameObject("ItemsPanel", typeof(RectTransform));
            itemsPanel.transform.SetParent(card.transform, false);
            var ipr = itemsPanel.GetComponent<RectTransform>(); ipr.anchorMin = Vector2.zero; ipr.anchorMax = Vector2.one; ipr.offsetMin = new Vector2(24, 24); ipr.offsetMax = new Vector2(-24, -140);
            var itemsMsg = MakeText("Msg", itemsPanel.transform, "No items yet", 40, SubTextCol, TextAnchor.MiddleCenter); Stretch(itemsMsg.rectTransform);
            itemsPanel.SetActive(false);

            // ===== equipped preview strip (horizontal scroll) =====
            const int SLOTS = 7;
            float slotW = 150f, slotGap = 12f;
            var eqScrollGO = new GameObject("EquippedScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            eqScrollGO.transform.SetParent(slimesPanel.transform, false);
            var eqs = eqScrollGO.GetComponent<RectTransform>(); eqs.anchorMin = new Vector2(0, 1); eqs.anchorMax = new Vector2(1, 1); eqs.pivot = new Vector2(0.5f, 1);
            eqs.sizeDelta = new Vector2(0, 176); eqs.anchoredPosition = Vector2.zero;
            eqScrollGO.GetComponent<Image>().color = scrollBg;
            var eqScroll = eqScrollGO.GetComponent<ScrollRect>(); eqScroll.horizontal = true; eqScroll.vertical = false; eqScroll.movementType = ScrollRect.MovementType.Clamped; eqScroll.scrollSensitivity = 20f;
            var eqViewGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            eqViewGO.transform.SetParent(eqScrollGO.transform, false);
            var eqv = eqViewGO.GetComponent<RectTransform>(); eqv.anchorMin = Vector2.zero; eqv.anchorMax = Vector2.one; eqv.offsetMin = new Vector2(8, 8); eqv.offsetMax = new Vector2(-8, -8);
            eqViewGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.0015f);
            eqScroll.viewport = eqv;
            var eqContentGO = new GameObject("Content", typeof(RectTransform));
            eqContentGO.transform.SetParent(eqViewGO.transform, false);
            var eqc = eqContentGO.GetComponent<RectTransform>(); eqc.anchorMin = new Vector2(0, 0); eqc.anchorMax = new Vector2(0, 1); eqc.pivot = new Vector2(0, 0.5f);
            eqc.sizeDelta = new Vector2(slotGap + SLOTS * (slotW + slotGap), 0); eqc.anchoredPosition = Vector2.zero;
            eqScroll.content = eqc;

            var equipBtns = new Button[SLOTS]; var equipIcons = new Image[SLOTS]; var equipLocks = new GameObject[SLOTS];
            for (int i = 0; i < SLOTS; i++)
            {
                var sq = MakePanel("Equip_" + (i + 1), eqContentGO.transform, cellBg);
                var sqr = sq.rectTransform; sqr.anchorMin = sqr.anchorMax = new Vector2(0, 0.5f); sqr.pivot = new Vector2(0, 0.5f);
                sqr.sizeDelta = new Vector2(slotW, slotW); sqr.anchoredPosition = new Vector2(slotGap + i * (slotW + slotGap), 0);
                equipBtns[i] = sq.gameObject.AddComponent<Button>(); equipBtns[i].targetGraphic = sq;
                var icon = MakeCircle("Icon", sq.transform, RarityCols[0]);
                var icr = icon.rectTransform; icr.anchorMin = icr.anchorMax = new Vector2(0.5f, 0.5f); icr.pivot = new Vector2(0.5f, 0.5f); icr.sizeDelta = new Vector2(104, 104); icr.anchoredPosition = Vector2.zero;
                icon.raycastTarget = false; AddFace(icon.transform, 104f); icon.gameObject.SetActive(false); equipIcons[i] = icon;
                var lockOv = MakePanel("Lock", sq.transform, new Color(0.05f, 0.06f, 0.08f, 0.97f));
                var lkr = lockOv.rectTransform; lkr.anchorMin = Vector2.zero; lkr.anchorMax = Vector2.one; lkr.offsetMin = Vector2.zero; lkr.offsetMax = Vector2.zero;
                equipLocks[i] = lockOv.gameObject;
            }

            // ===== owned-slime grid (vertical scroll) =====
            var invScrollGO = new GameObject("InvScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            invScrollGO.transform.SetParent(slimesPanel.transform, false);
            var ivs = invScrollGO.GetComponent<RectTransform>(); ivs.anchorMin = Vector2.zero; ivs.anchorMax = Vector2.one; ivs.offsetMin = new Vector2(0, 130); ivs.offsetMax = new Vector2(0, -196);
            invScrollGO.GetComponent<Image>().color = scrollBg;
            var invScroll = invScrollGO.GetComponent<ScrollRect>(); invScroll.horizontal = false; invScroll.vertical = true; invScroll.movementType = ScrollRect.MovementType.Clamped; invScroll.scrollSensitivity = 24f;
            var ivViewGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            ivViewGO.transform.SetParent(invScrollGO.transform, false);
            var ivv = ivViewGO.GetComponent<RectTransform>(); ivv.anchorMin = Vector2.zero; ivv.anchorMax = Vector2.one; ivv.offsetMin = new Vector2(8, 8); ivv.offsetMax = new Vector2(-8, -8);
            ivViewGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.0015f);
            invScroll.viewport = ivv;
            var ivContentGO = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            ivContentGO.transform.SetParent(ivViewGO.transform, false);
            // anchor content TOP-LEFT and let ContentSizeFitter size it to the grid exactly (avoids a stretched/centered content that cuts cells off on the left)
            var ivc = ivContentGO.GetComponent<RectTransform>(); ivc.anchorMin = new Vector2(0, 1); ivc.anchorMax = new Vector2(0, 1); ivc.pivot = new Vector2(0, 1); ivc.anchoredPosition = Vector2.zero; ivc.sizeDelta = Vector2.zero;
            var grid = ivContentGO.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(270, 350); grid.spacing = new Vector2(14, 14); grid.padding = new RectOffset(8, 8, 8, 8); grid.childAlignment = TextAnchor.UpperLeft; grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            var ivFitter = ivContentGO.GetComponent<ContentSizeFitter>(); ivFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; ivFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            invScroll.content = ivc;

            int n = RarityNames.Length;
            var invBtns = new Button[n]; var invFrames = new Image[n]; var invIcons = new Image[n]; var invCounts = new Text[n];
            var invSellBtns = new Button[n]; var invEquipBtns = new Button[n];
            Color frameOff = new Color(0.10f, 0.11f, 0.14f, 1f);
            Color sellCol = new Color(0.30f, 0.50f, 0.32f, 1f), equipCol = new Color(0.28f, 0.42f, 0.58f, 1f);
            for (int i = 0; i < n; i++)
            {
                // outer frame = selection highlight; inner bg (white base so the Button colour-tint shows directly = hover)
                var frame = MakePanel("Inv_" + i, ivContentGO.transform, frameOff);
                invFrames[i] = frame;
                var cellBtn = frame.gameObject.AddComponent<Button>(); invBtns[i] = cellBtn;
                var bg = MakePanel("Bg", frame.transform, Color.white);
                var bgr = bg.rectTransform; bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.offsetMin = new Vector2(6, 6); bgr.offsetMax = new Vector2(-6, -6);
                cellBtn.targetGraphic = bg;
                var cbk = cellBtn.colors; cbk.normalColor = cellBg; cbk.highlightedColor = new Color(0.20f, 0.24f, 0.30f, 1f); cbk.pressedColor = new Color(0.26f, 0.32f, 0.40f, 1f); cbk.selectedColor = cellBg; cbk.fadeDuration = 0.08f; cellBtn.colors = cbk;

                var icon = MakeCircle("Icon", bg.transform, RarityCols[i]);
                var icr = icon.rectTransform; icr.anchorMin = icr.anchorMax = new Vector2(0.5f, 1); icr.pivot = new Vector2(0.5f, 1); icr.sizeDelta = new Vector2(110, 110); icr.anchoredPosition = new Vector2(0, -14);
                icon.raycastTarget = false; AddFace(icon.transform, 110f); invIcons[i] = icon;

                var cnt = MakeText("Count", bg.transform, "x0", 26, TextCol, TextAnchor.MiddleRight); cnt.raycastTarget = false;
                var cntr = cnt.rectTransform; cntr.anchorMin = cntr.anchorMax = new Vector2(1, 1); cntr.pivot = new Vector2(1, 1); cntr.sizeDelta = new Vector2(96, 38); cntr.anchoredPosition = new Vector2(-8, -8);
                invCounts[i] = cnt;

                // name right below the image
                var nm = MakeText("Name", bg.transform, RarityNames[i], 24, RarityCols[i], TextAnchor.MiddleCenter); nm.raycastTarget = false;
                var nmr = nm.rectTransform; nmr.anchorMin = new Vector2(0, 1); nmr.anchorMax = new Vector2(1, 1); nmr.pivot = new Vector2(0.5f, 1); nmr.sizeDelta = new Vector2(-8, 40); nmr.anchoredPosition = new Vector2(0, -130);

                // Sell + Equip stacked at the bottom
                var sellB = MakePanel("Sell", bg.transform, sellCol);
                var sbr = sellB.rectTransform; sbr.anchorMin = sbr.anchorMax = new Vector2(0.5f, 0); sbr.pivot = new Vector2(0.5f, 0); sbr.sizeDelta = new Vector2(226, 54); sbr.anchoredPosition = new Vector2(0, 72);
                invSellBtns[i] = sellB.gameObject.AddComponent<Button>(); invSellBtns[i].targetGraphic = sellB;
                var sbl = MakeText("L", sellB.transform, "Sell", 26, Color.white, TextAnchor.MiddleCenter); sbl.raycastTarget = false; Stretch(sbl.rectTransform);

                var equipB = MakePanel("Equip", bg.transform, equipCol);
                var ebr = equipB.rectTransform; ebr.anchorMin = ebr.anchorMax = new Vector2(0.5f, 0); ebr.pivot = new Vector2(0.5f, 0); ebr.sizeDelta = new Vector2(226, 54); ebr.anchoredPosition = new Vector2(0, 10);
                invEquipBtns[i] = equipB.gameObject.AddComponent<Button>(); invEquipBtns[i].targetGraphic = equipB;
                var ebl = MakeText("L", equipB.transform, "Equip", 26, Color.white, TextAnchor.MiddleCenter); ebl.raycastTarget = false; Stretch(ebl.rectTransform);
            }

            // ===== bottom: Auto-Equip on the right (left slot left empty for a future button) =====
            var auto = MakePanel("AutoEquipButton", slimesPanel.transform, new Color(0.28f, 0.42f, 0.58f, 1f));
            var ar = auto.rectTransform; ar.anchorMin = ar.anchorMax = new Vector2(1, 0); ar.pivot = new Vector2(1, 0); ar.sizeDelta = new Vector2(420, 110); ar.anchoredPosition = Vector2.zero;
            var autoBtn = auto.gameObject.AddComponent<Button>(); autoBtn.targetGraphic = auto;
            var autoLabel = MakeText("Label", auto.transform, "Auto-Equip", 32, Color.white, TextAnchor.MiddleCenter); autoLabel.raycastTarget = false; Stretch(autoLabel.rectTransform);

            // wire
            ui.slimesTabBtn = slimesTabBtn; ui.itemsTabBtn = itemsTabBtn; ui.slimesTabBg = slimesTab; ui.itemsTabBg = itemsTab;
            ui.slimesPanel = slimesPanel; ui.itemsPanel = itemsPanel;
            ui.equipSlotBtns = equipBtns; ui.equipSlotIcons = equipIcons; ui.equipSlotLocks = equipLocks;
            ui.invBtns = invBtns; ui.invFrames = invFrames; ui.invIcons = invIcons; ui.invCounts = invCounts;
            ui.invSellBtns = invSellBtns; ui.invEquipBtns = invEquipBtns;
            ui.autoEquipButton = autoBtn;
            return ui;
        }

        // ================= collection =================

        static CollectionUI BuildCollectionPanel(Transform root, out GameObject panelGO, out Button closeBtn)
        {
            var panel = MakeImage("CollectionPanel", root, new Color(0, 0, 0, 0.66f));
            Stretch(panel.rectTransform);
            panelGO = panel.gameObject;
            var ccv = panelGO.AddComponent<Canvas>(); ccv.overrideSorting = true; ccv.sortingOrder = 30;
            panelGO.AddComponent<GraphicRaycaster>();
            var ui = panelGO.AddComponent<CollectionUI>();

            var card = MakePanel("Card", panel.transform, PanelDark);
            var cr = card.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(940, 1340); cr.anchoredPosition = Vector2.zero;
            var title = MakeText("Title", card.transform, "Collection", 46, TextCol, TextAnchor.MiddleLeft);
            var ti = title.rectTransform; ti.anchorMin = new Vector2(0, 1); ti.anchorMax = new Vector2(1, 1); ti.pivot = new Vector2(0.5f, 1); ti.sizeDelta = new Vector2(-80, 90); ti.anchoredPosition = new Vector2(40, -30);
            closeBtn = MakeCloseButton(card.transform);

            var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(card.transform, false);
            var sc = scrollGO.GetComponent<RectTransform>(); sc.anchorMin = Vector2.zero; sc.anchorMax = Vector2.one; sc.offsetMin = new Vector2(24, 24); sc.offsetMax = new Vector2(-24, -130);
            scrollGO.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 1f);
            var scroll = scrollGO.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24f;
            var viewGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewGO.transform.SetParent(scrollGO.transform, false);
            var vv = viewGO.GetComponent<RectTransform>(); vv.anchorMin = Vector2.zero; vv.anchorMax = Vector2.one; vv.offsetMin = new Vector2(8, 8); vv.offsetMax = new Vector2(-8, -8);
            viewGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.0015f);
            scroll.viewport = vv;
            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(viewGO.transform, false);
            var cc = contentGO.GetComponent<RectTransform>(); cc.anchorMin = new Vector2(0, 1); cc.anchorMax = new Vector2(0, 1); cc.pivot = new Vector2(0, 1); cc.anchoredPosition = Vector2.zero; cc.sizeDelta = Vector2.zero;
            var grid = contentGO.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(270, 300); grid.spacing = new Vector2(14, 14); grid.padding = new RectOffset(8, 8, 8, 8); grid.childAlignment = TextAnchor.UpperLeft; grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            var fit = contentGO.GetComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = cc;

            int n = RarityNames.Length;
            var icons = new Image[n]; var eyesArr = new GameObject[n]; var names = new Text[n]; var chances = new Text[n];
            for (int i = 0; i < n; i++)
            {
                var cell = MakePanel("Coll_" + i, contentGO.transform, new Color(0.13f, 0.15f, 0.18f, 1f));
                var body = MakeCircle("Body", cell.transform, new Color(0.20f, 0.22f, 0.26f));
                var bdr = body.rectTransform; bdr.anchorMin = bdr.anchorMax = new Vector2(0.5f, 1); bdr.pivot = new Vector2(0.5f, 1); bdr.sizeDelta = new Vector2(120, 120); bdr.anchoredPosition = new Vector2(0, -16);
                body.raycastTarget = false; icons[i] = body;
                var eyes = new GameObject("Eyes", typeof(RectTransform)); eyes.transform.SetParent(body.transform, false); Stretch(eyes.GetComponent<RectTransform>());
                float eo = 120 * 0.16f, eye = 120 * 0.17f, ey = 120 * 0.06f;
                var eL = MakeCircle("EyeL", eyes.transform, EyeDark); eL.raycastTarget = false; var eLr = eL.rectTransform; eLr.anchorMin = eLr.anchorMax = new Vector2(0.5f, 0.5f); eLr.pivot = new Vector2(0.5f, 0.5f); eLr.sizeDelta = new Vector2(eye, eye); eLr.anchoredPosition = new Vector2(-eo, ey);
                var eR = MakeCircle("EyeR", eyes.transform, EyeDark); eR.raycastTarget = false; var eRr = eR.rectTransform; eRr.anchorMin = eRr.anchorMax = new Vector2(0.5f, 0.5f); eRr.pivot = new Vector2(0.5f, 0.5f); eRr.sizeDelta = new Vector2(eye, eye); eRr.anchoredPosition = new Vector2(eo, ey);
                eyes.SetActive(false); eyesArr[i] = eyes;
                var nm = MakeText("Name", cell.transform, "???", 24, SubTextCol, TextAnchor.MiddleCenter); nm.raycastTarget = false;
                var nmr = nm.rectTransform; nmr.anchorMin = new Vector2(0, 1); nmr.anchorMax = new Vector2(1, 1); nmr.pivot = new Vector2(0.5f, 1); nmr.sizeDelta = new Vector2(-8, 40); nmr.anchoredPosition = new Vector2(0, -142);
                names[i] = nm;
                var ch = MakeText("Chance", cell.transform, "1/?", 30, GoldCol, TextAnchor.MiddleCenter); ch.raycastTarget = false;
                var chr = ch.rectTransform; chr.anchorMin = new Vector2(0, 0); chr.anchorMax = new Vector2(1, 0); chr.pivot = new Vector2(0.5f, 0); chr.sizeDelta = new Vector2(-8, 50); chr.anchoredPosition = new Vector2(0, 18);
                chances[i] = ch;
            }
            ui.icons = icons; ui.eyes = eyesArr; ui.names = names; ui.chances = chances;
            return ui;
        }

        // ================= skill tree =================

        static void BuildSkillTreePanel(Transform root, out GameObject panelGO, out Button closeBtn, out SkillNode[] nodesOut)
        {
            var panel = MakeImage("SkillsPanel", root, new Color(0, 0, 0, 0.66f));
            Stretch(panel.rectTransform);
            panelGO = panel.gameObject;
            // render above the HP bars' override-sorting canvas (20) so bars don't bleed through this overlay
            var scv = panelGO.AddComponent<Canvas>(); scv.overrideSorting = true; scv.sortingOrder = 30;
            panelGO.AddComponent<GraphicRaycaster>();

            var card = MakePanel("Card", panel.transform, PanelDark);
            var cr = card.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(940, 1240); cr.anchoredPosition = Vector2.zero;

            var title = MakeText("Title", card.transform, "Skill Tree", 46, TextCol, TextAnchor.MiddleLeft);
            var ti = title.rectTransform; ti.anchorMin = new Vector2(0, 1); ti.anchorMax = new Vector2(1, 1); ti.pivot = new Vector2(0.5f, 1);
            ti.sizeDelta = new Vector2(-80, 90); ti.anchoredPosition = new Vector2(40, -30);
            closeBtn = MakeCloseButton(card.transform);

            var hint = MakeText("Hint", card.transform, "Buy a hex to unlock it · drag to pan · scroll / pinch to zoom", 24, SubTextCol, TextAnchor.MiddleCenter);
            var hr = hint.rectTransform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(1, 0); hr.pivot = new Vector2(0.5f, 0);
            hr.sizeDelta = new Vector2(-60, 52); hr.anchoredPosition = new Vector2(0, 24);

            // pan/zoom viewport holding a movable tree-content
            var vpGO = new GameObject("TreeViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            vpGO.transform.SetParent(card.transform, false);
            var vprt = vpGO.GetComponent<RectTransform>(); vprt.anchorMin = Vector2.zero; vprt.anchorMax = Vector2.one; vprt.offsetMin = new Vector2(20, 92); vprt.offsetMax = new Vector2(-20, -108);
            vpGO.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 1f);
            var contentGO = new GameObject("TreeContent", typeof(RectTransform));
            contentGO.transform.SetParent(vpGO.transform, false);
            var content = contentGO.GetComponent<RectTransform>(); content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f); content.pivot = new Vector2(0.5f, 0.5f); content.sizeDelta = new Vector2(100, 100); content.anchoredPosition = Vector2.zero;
            var pz = vpGO.AddComponent<PanZoom>(); pz.content = content;

            // ----- chained single-purchase tree (each hex unlocks its neighbour) -----
            float W = 178f, H = W * Mathf.Sqrt(3f) / 2f;
            Vector2 dN = new Vector2(0, H), dNE = new Vector2(W * 0.75f, H * 0.5f), dSE = new Vector2(W * 0.75f, -H * 0.5f),
                    dS = new Vector2(0, -H), dSW = new Vector2(-W * 0.75f, -H * 0.5f), dNW = new Vector2(-W * 0.75f, H * 0.5f);
            var nodes = new System.Collections.Generic.List<SkillNode>();

            SkillNode Node(string label, Vector2 p, SkillNode.Effect eff, int cost)
            {
                var nn = MakeHexNode(content, label, p, W, eff, cost);
                nn.neighbors = new SkillNode[0];
                nodes.Add(nn);
                return nn;
            }
            void Link(SkillNode a, SkillNode b)
            {
                var l = new System.Collections.Generic.List<SkillNode>(a.neighbors); l.Add(b); a.neighbors = l.ToArray();
            }
            SkillNode Chain(SkillNode start, Vector2 startPos, Vector2 dir, string baseName, SkillNode.Effect eff, int[] costs)
            {
                var prev = start; var p = startPos;
                for (int i = 0; i < costs.Length; i++) { p += dir; var nn = Node(baseName + " " + (i + 2), p, eff, costs[i]); Link(prev, nn); prev = nn; }
                return prev; // last node in the chain (so callers can branch off the arm's tip)
            }

            Vector2 c0 = Vector2.zero;
            var center = Node("Start", c0, SkillNode.Effect.AutoRoll, 0);                       // [0]

            // six straight spokes radiate from the centre — they diverge by 60° so they never cross
            var hs1 = Node("Hero Slot", c0 + dN, SkillNode.Effect.HeroSlot, 100); Link(center, hs1); // [1]
            Chain(hs1, c0 + dN, dN, "Hero Slot", SkillNode.Effect.HeroSlot, new[] { 300, 800, 2000 });

            var lk1 = Node("Luck", c0 + dNE, SkillNode.Effect.Luck, 60); Link(center, lk1);
            var lkEnd = Chain(lk1, c0 + dNE, dNE, "Luck", SkillNode.Effect.Luck, new[] { 160, 420 }); // tip at c0 + dNE*3

            // Spin-streak chain branches straight UP off the TIP of the Luck arm — far to the right of the
            // Hero Slot column, so it reads as a clean separate branch instead of touching unrelated hexes.
            var gr = Node("Gold Roll", c0 + dNE * 3 + dN, SkillNode.Effect.GoldRoll, 300); Link(lkEnd, gr);
            var pr = Node("Platinum Roll", c0 + dNE * 3 + dN * 2, SkillNode.Effect.PlatinumRoll, 800); Link(gr, pr);
            var dr = Node("Diamond Roll", c0 + dNE * 3 + dN * 3, SkillNode.Effect.DiamondRoll, 2000); Link(pr, dr);

            var dm1 = Node("Damage", c0 + dSE, SkillNode.Effect.Damage, 60); Link(center, dm1);
            Chain(dm1, c0 + dSE, dSE, "Damage", SkillNode.Effect.Damage, new[] { 160, 420 });

            var gd1 = Node("Gold", c0 + dS, SkillNode.Effect.Gold, 60); Link(center, gd1);
            Chain(gd1, c0 + dS, dS, "Gold", SkillNode.Effect.Gold, new[] { 160, 420 });

            var cr1 = Node("Crit", c0 + dSW, SkillNode.Effect.Crit, 80); Link(center, cr1);
            Chain(cr1, c0 + dSW, dSW, "Crit", SkillNode.Effect.Crit, new[] { 200, 500 });

            // Speed spoke straight NW; Roll Speed branches straight UP off the TIP, far to the left
            var sp1 = Node("Speed", c0 + dNW, SkillNode.Effect.Speed, 80); Link(center, sp1);
            var spEnd = Chain(sp1, c0 + dNW, dNW, "Speed", SkillNode.Effect.Speed, new[] { 200, 500 }); // tip at c0 + dNW*3
            var rs = Node("Roll Speed", c0 + dNW * 3 + dN, SkillNode.Effect.Spin, 250); Link(spEnd, rs);

            center.SetState(SkillNode.State.Available);
            for (int i = 1; i < nodes.Count; i++) nodes[i].SetState(SkillNode.State.Hidden);
            nodesOut = nodes.ToArray(); // [0]=center, [1]=Hero Slot
        }

        static SkillNode MakeHexNode(Transform parent, string label, Vector2 pos, float size, SkillNode.Effect effect, int baseCost)
        {
            var go = new GameObject("Hex_" + label.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.sprite = _hex; img.type = Image.Type.Simple;
            img.color = new Color(0.20f, 0.34f, 0.26f, 1f);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = pos;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;

            var nameLab = MakeText("Name", go.transform, label, 24, Color.white, TextAnchor.MiddleCenter);
            var nlr = nameLab.rectTransform; nlr.anchorMin = nlr.anchorMax = new Vector2(0.5f, 0.5f); nlr.pivot = new Vector2(0.5f, 0.5f);
            nlr.sizeDelta = new Vector2(size - 20, 40); nlr.anchoredPosition = new Vector2(0, 20);
            var costLab = MakeText("Cost", go.transform, "", 22, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter);
            var clr = costLab.rectTransform; clr.anchorMin = clr.anchorMax = new Vector2(0.5f, 0.5f); clr.pivot = new Vector2(0.5f, 0.5f);
            clr.sizeDelta = new Vector2(size - 20, 34); clr.anchoredPosition = new Vector2(0, -22);

            var node = go.AddComponent<SkillNode>();
            node.button = btn; node.background = img; node.nameLabel = nameLab; node.costLabel = costLab;
            node.effect = effect; node.baseCost = baseCost;
            return node;
        }

        static Sprite GetHexSprite()
        {
            if (_hex != null) return _hex;
            int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Vector2 c = new Vector2(S / 2f, S / 2f);
            float R = S / 2f - 2f;
            var v = new Vector2[6];
            for (int i = 0; i < 6; i++) { float a = Mathf.Deg2Rad * 60f * i; v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R; }
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (PointInPoly(new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f), v)) hit++;
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 4));
                }
            tex.SetPixels32(px); tex.Apply();
            _hex = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return _hex;
        }

        /// <summary>Crisp anti-aliased solid white disc (hard edge, unlike the soft builtin Knob). Tinted by Image.color.</summary>
        static Sprite GetDiscSprite()
        {
            if (_disc != null) return _disc;
            int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Vector2 c = new Vector2(S / 2f, S / 2f);
            float R = S / 2f - 1f;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            var p = new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f);
                            if ((p - c).sqrMagnitude <= R * R) hit++;
                        }
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 4));
                }
            tex.SetPixels32(px); tex.Apply();
            _disc = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return _disc;
        }

        /// <summary>Annulus "tube" sprite: a ring whose band is dark in the middle and lighter at both edges
        /// (grayscale, so Image.color tints it). Used as a radial-fill so progress rings get a boss-timer-style
        /// beveled look. The transparent centre lets the button/medallion show through.</summary>
        static Sprite GetTubeRingSprite()
        {
            if (_tubeRing != null) return _tubeRing;
            int S = 200;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Vector2 c = new Vector2(S / 2f, S / 2f);
            float R = S / 2f - 1f;
            const float inner = 0.78f, outer = 1.0f, mid = (inner + outer) / 2f, half = (outer - inner) / 2f;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float fr = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / R;
                    float a = 0f, v = 0.5f;
                    if (fr >= inner && fr <= outer)
                    {
                        a = Mathf.Min(Mathf.Clamp01((fr - inner) / 0.03f), Mathf.Clamp01((outer - fr) / 0.03f)); // soft band edges
                        float t = Mathf.Clamp01(Mathf.Abs(fr - mid) / half);  // 0 = band middle, 1 = band edges
                        v = Mathf.Lerp(0.45f, 1.0f, t * t);                    // dark middle -> bright edges
                    }
                    byte g = (byte)(v * 255);
                    px[y * S + x] = new Color32(g, g, g, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            _tubeRing = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return _tubeRing;
        }

        /// <summary>Top-half semicircle (dome) sprite, pivot at the bottom-centre so the flat side sits on an edge.</summary>
        static Sprite GetDomeSprite()
        {
            if (_dome != null) return _dome;
            int W = 256, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Vector2 c = new Vector2(W / 2f, 0f); // bottom-centre
            float R = W / 2f - 1f;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            var p = new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f);
                            if ((p - c).sqrMagnitude <= R * R) hit++;
                        }
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 4));
                }
            tex.SetPixels32(px); tex.Apply();
            _dome = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
            return _dome;
        }

        /// <summary>9-sliced rounded-rectangle sprite with a clear corner radius (for the floating top fields).</summary>
        static Sprite GetRoundedSprite()
        {
            if (_roundedRect != null) return _roundedRect;
            int S = 64; float R = 24f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float pxv = x + 0.25f + sx * 0.5f, pyv = y + 0.25f + sy * 0.5f;
                            float cx = Mathf.Clamp(pxv, R, S - R), cy = Mathf.Clamp(pyv, R, S - R);
                            float dx = pxv - cx, dy = pyv - cy;
                            if (dx * dx + dy * dy <= R * R) hit++;
                        }
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 4));
                }
            tex.SetPixels32(px); tex.Apply();
            _roundedRect = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
            return _roundedRect;
        }

        static Image MakeRounded(string name, Transform parent, Color color)
        {
            var img = MakeImage(name, parent, color);
            img.sprite = GetRoundedSprite(); img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>Make a Text bold with a black outline (legible over the sky without a panel background).</summary>
        static void BoldOutline(Text t, float dist)
        {
            t.fontStyle = FontStyle.Bold;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.92f);
            o.effectDistance = new Vector2(dist, -dist);
        }

        static bool PointInPoly(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            return inside;
        }

        static Button MakeCloseButton(Transform card)
        {
            var c = MakePanel("CloseButton", card, new Color(0.35f, 0.20f, 0.22f, 1f));
            var r = c.rectTransform; r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1);
            r.sizeDelta = new Vector2(90, 90); r.anchoredPosition = new Vector2(-26, -26);
            var bt = c.gameObject.AddComponent<Button>(); bt.targetGraphic = c;
            var x = MakeText("X", c.transform, "X", 44, Color.white, TextAnchor.MiddleCenter);
            Stretch(x.rectTransform);
            return bt;
        }

        // ================= helpers =================

        static void Kill(string name) { var go = GameObject.Find(name); if (go) Object.DestroyImmediate(go); }

        static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = color;
            return img;
        }

        static Image MakePanel(string name, Transform parent, Color color)
        {
            var img = MakeImage(name, parent, color);
            if (_rounded) { img.sprite = _rounded; img.type = Image.Type.Sliced; }
            return img;
        }

        static Image MakeCircle(string name, Transform parent, Color color)
        {
            var img = MakeImage(name, parent, color);
            if (_circle) { img.sprite = _circle; img.type = Image.Type.Simple; }
            return img;
        }

        /// <summary>Adds two dark eye-dots to a circular slime icon so it reads as a slime face
        /// (decorative children; do not affect the body colour the gameplay code sets).</summary>
        static void AddFace(Transform body, float bodySize)
        {
            float eo = bodySize * 0.17f, eye = bodySize * 0.15f, ey = bodySize * 0.04f;
            var eL = MakeCircle("EyeL", body, EyeDark); eL.raycastTarget = false;
            var lr = eL.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f); lr.pivot = new Vector2(0.5f, 0.5f); lr.sizeDelta = new Vector2(eye, eye); lr.anchoredPosition = new Vector2(-eo, ey);
            var eR = MakeCircle("EyeR", body, EyeDark); eR.raycastTarget = false;
            var rr = eR.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f); rr.pivot = new Vector2(0.5f, 0.5f); rr.sizeDelta = new Vector2(eye, eye); rr.anchoredPosition = new Vector2(eo, ey);
        }

        static Text MakeText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font; t.text = content; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void AnchorTop(RectTransform rt, float height, float yOffset)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, height); rt.anchoredPosition = new Vector2(0, yOffset);
        }
    }
}
