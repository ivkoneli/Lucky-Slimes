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
        static Sprite _circle, _rounded, _hex;

        const float Border = 6f;

        // palette
        static readonly Color SkyBlue    = new Color(0.44f, 0.57f, 0.70f, 1f);
        static readonly Color GroundCol  = new Color(0.27f, 0.42f, 0.24f, 1f);
        static readonly Color Horizon    = new Color(0.36f, 0.52f, 0.30f, 1f);
        static readonly Color RoadCol    = new Color(0.20f, 0.30f, 0.18f, 1f);
        static readonly Color BorderCol  = new Color(0.40f, 0.47f, 0.58f, 1f);
        static readonly Color PanelDark  = new Color(0.14f, 0.16f, 0.20f, 1f);
        static readonly Color PanelDark2 = new Color(0.10f, 0.11f, 0.14f, 1f);
        static readonly Color Forest     = new Color(0.46f, 0.74f, 0.42f, 1f);
        static readonly Color ForestDim  = new Color(0.22f, 0.34f, 0.22f, 1f);
        static readonly Color GoldCol     = new Color(1f, 0.84f, 0.25f, 1f);
        static readonly Color TextCol     = new Color(0.92f, 0.94f, 0.97f, 1f);
        static readonly Color SubTextCol  = new Color(0.66f, 0.69f, 0.75f, 1f);
        static readonly Color DiceCol     = new Color(0.96f, 0.96f, 0.98f, 1f);
        static readonly Color EnemyCol     = new Color(0.78f, 0.34f, 0.42f, 1f);
        static readonly Color EyeDark      = new Color(0.12f, 0.12f, 0.15f, 1f);

        static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
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
            BuildBottomSection(root, out Button diceBtn, out Image diceImg, out Image cooldownOverlay, out Button invBtn, out Button skillsBtn, out Image[] slotMini, out GameObject[] slotLock);
            BuildPullPopup(root, out Text popupName, out Text popupChance, out GameObject popupRoot);
            var invUI = BuildInventoryPanel(root, out GameObject invPanel, out Button invClose);
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
            invUI.roller = roller;

            var nav = gm.AddComponent<ScreenNav>();
            nav.inventoryButton = invBtn; nav.skillsButton = skillsBtn;
            nav.inventoryPanel = invPanel; nav.skillsPanel = skillsPanel;
            nav.inventoryClose = invClose; nav.skillsClose = skillsClose;

            var combat = gm.AddComponent<CombatManager>();
            combat.enemyContainer = enemyContainer;
            combat.circleSprite = _circle;
            combat.font = _font;
            combat.stageNumText = stageNumText;
            combat.dots = dots;
            combat.roller = roller;

            var team = gm.AddComponent<TeamManager>();
            team.roller = roller;
            team.combat = combat;
            team.heroContainer = heroContainer;
            team.slotMini = slotMini;
            team.slotLock = slotLock;
            team.unlockedSlots = 2;
            invUI.team = team;

            // wire skill nodes to the live systems
            foreach (var n in skillNodes) { n.roller = roller; n.combat = combat; n.team = team; n.RefreshVisual(); }

            // start at zero: no gold, no slimes, empty team
            roller.gold = 0; roller.UpdateGoldUI(); roller.UpdateLuckUI();
            invUI.Refresh();

            invPanel.SetActive(false);
            skillsPanel.SetActive(false);

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

            const float groundH = 1190f; // raised ground (~62% of 1920)
            var ground = MakeImage("Ground", wt, GroundCol);
            var gr = ground.rectTransform; gr.anchorMin = new Vector2(0, 0); gr.anchorMax = new Vector2(1, 0); gr.pivot = new Vector2(0.5f, 0);
            gr.sizeDelta = new Vector2(0, groundH); gr.anchoredPosition = Vector2.zero;
            var hz = MakeImage("Horizon", wt, Horizon);
            var hr = hz.rectTransform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(1, 0); hr.pivot = new Vector2(0.5f, 0);
            hr.sizeDelta = new Vector2(0, 5); hr.anchoredPosition = new Vector2(0, groundH);

            // heroes (left) and enemies (right) are spawned at runtime by TeamManager / CombatManager.
            var hc = new GameObject("HeroContainer", typeof(RectTransform));
            hc.transform.SetParent(wt, false);
            Stretch(hc.GetComponent<RectTransform>());
            heroContainer = hc.transform;

            var ec = new GameObject("EnemyContainer", typeof(RectTransform));
            ec.transform.SetParent(wt, false);
            Stretch(ec.GetComponent<RectTransform>());
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
            var topBar = MakeImage("TopBar", root, PanelDark);
            AnchorTop(topBar.rectTransform, 150, 0);

            var goldPill = MakePanel("GoldPill", topBar.transform, PanelDark2);
            var gp = goldPill.rectTransform; gp.anchorMin = gp.anchorMax = new Vector2(0, 0.5f); gp.pivot = new Vector2(0, 0.5f);
            gp.sizeDelta = new Vector2(340, 92); gp.anchoredPosition = new Vector2(36, 0);
            var icon = MakeCircle("GoldIcon", goldPill.transform, GoldCol);
            var ic = icon.rectTransform; ic.anchorMin = ic.anchorMax = new Vector2(0, 0.5f); ic.pivot = new Vector2(0, 0.5f);
            ic.sizeDelta = new Vector2(54, 54); ic.anchoredPosition = new Vector2(24, 0);
            goldText = MakeText("GoldText", goldPill.transform, "0", 40, GoldCol, TextAnchor.MiddleLeft);
            var gt = goldText.rectTransform; gt.anchorMin = Vector2.zero; gt.anchorMax = Vector2.one;
            gt.offsetMin = new Vector2(94, 0); gt.offsetMax = new Vector2(-16, 0);

            var luckPill = MakePanel("LuckPill", topBar.transform, PanelDark2);
            var lp = luckPill.rectTransform; lp.anchorMin = lp.anchorMax = new Vector2(1, 0.5f); lp.pivot = new Vector2(1, 0.5f);
            lp.sizeDelta = new Vector2(300, 92); lp.anchoredPosition = new Vector2(-36, 0);
            luckText = MakeText("LuckText", luckPill.transform, "Luck 1x", 38, Forest, TextAnchor.MiddleCenter);
            Stretch(luckText.rectTransform);
        }

        // ================= stage title badge =================

        static void BuildStageTitle(Transform root, out Text stageNumText)
        {
            var badge = MakePanel("StageTitle", root, new Color(0.10f, 0.12f, 0.16f, 0.94f));
            var br = badge.rectTransform; br.anchorMin = br.anchorMax = new Vector2(0.5f, 1); br.pivot = new Vector2(0.5f, 1);
            br.sizeDelta = new Vector2(440, 158); br.anchoredPosition = new Vector2(0, -166);

            var s1 = MakeText("StageNum", badge.transform, "Stage 1", 56, TextCol, TextAnchor.MiddleCenter);
            stageNumText = s1;
            var s1r = s1.rectTransform; s1r.anchorMin = new Vector2(0, 1); s1r.anchorMax = new Vector2(1, 1); s1r.pivot = new Vector2(0.5f, 1);
            s1r.sizeDelta = new Vector2(-24, 88); s1r.anchoredPosition = new Vector2(0, -14);

            var zn = MakeText("ZoneName", badge.transform, "Forest", 34, Forest, TextAnchor.MiddleCenter);
            var znr = zn.rectTransform; znr.anchorMin = new Vector2(0, 0); znr.anchorMax = new Vector2(1, 0); znr.pivot = new Vector2(0.5f, 0);
            znr.sizeDelta = new Vector2(-24, 56); znr.anchoredPosition = new Vector2(0, 16);
        }

        // ================= progress road (no border) =================

        static Image[] BuildProgressRoad(Transform root)
        {
            var prog = new GameObject("ProgressRoad", typeof(RectTransform));
            prog.transform.SetParent(root, false);
            var pr = prog.GetComponent<RectTransform>(); pr.anchorMin = new Vector2(0, 1); pr.anchorMax = new Vector2(1, 1); pr.pivot = new Vector2(0.5f, 1);
            pr.sizeDelta = new Vector2(0, 100); pr.anchoredPosition = new Vector2(0, -360);
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

        // ================= bottom section (dice + team row + 5 nav buttons) =================

        static void BuildBottomSection(Transform root, out Button diceBtn, out Image diceImg, out Image cooldownOverlay, out Button invBtn, out Button skillsBtn, out Image[] slotMini, out GameObject[] slotLock)
        {
            const float secH = 414f;
            var outer = MakePanel("BottomSection", root, BorderCol);
            var oRt = outer.rectTransform; oRt.anchorMin = new Vector2(0, 0); oRt.anchorMax = new Vector2(1, 0); oRt.pivot = new Vector2(0.5f, 0);
            oRt.sizeDelta = new Vector2(0, secH); oRt.anchoredPosition = Vector2.zero;
            var fill = MakePanel("Fill", outer.transform, PanelDark);
            var fRt = fill.rectTransform; fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
            fRt.offsetMin = new Vector2(Border, Border); fRt.offsetMax = new Vector2(-Border, -Border);
            fill.raycastTarget = false;
            var sec = fill.transform;

            // shared 5-column grid
            int cols = 5; float colW = 200f, colGap = 10f;
            float gridW = cols * colW + (cols - 1) * colGap;
            float gx = -gridW / 2f + colW / 2f;

            // 5 nav buttons stuck to the bottom
            string[] navNames = { "Battle", "Inventory", "Collection", "Upgrades", "Skill Tree" };
            var navBtns = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var b = MakePanel("Nav_" + navNames[i].Replace(" ", ""), sec, PanelDark2);
                var r = b.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0); r.pivot = new Vector2(0.5f, 0);
                r.sizeDelta = new Vector2(colW, 106); r.anchoredPosition = new Vector2(gx + i * (colW + colGap), 8);
                var bt = b.gameObject.AddComponent<Button>(); bt.targetGraphic = b; navBtns[i] = bt;
                var lab = MakeText("Label", b.transform, navNames[i], 26, TextCol, TextAnchor.MiddleCenter);
                Stretch(lab.rectTransform);
            }
            invBtn = navBtns[1]; skillsBtn = navBtns[4];

            // per-slot upgrade buttons, just below the team slots
            for (int i = 0; i < 5; i++)
            {
                var u = MakePanel("Upgrade_" + (i + 1), sec, new Color(0.20f, 0.32f, 0.22f, 1f));
                var ur = u.rectTransform; ur.anchorMin = ur.anchorMax = new Vector2(0.5f, 0); ur.pivot = new Vector2(0.5f, 0);
                ur.sizeDelta = new Vector2(colW, 72); ur.anchoredPosition = new Vector2(gx + i * (colW + colGap), 124);
                u.gameObject.AddComponent<Button>().targetGraphic = u;
                var ul = MakeText("Label", u.transform, "Upgrade", 24, TextCol, TextAnchor.MiddleCenter);
                Stretch(ul.rectTransform);
            }

            // current team: 5 slots full width, above the upgrade buttons (start empty; slots 3-5 locked)
            slotMini = new Image[5]; slotLock = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                var slot = MakePanel("Slot_" + (i + 1), sec, new Color(0.13f, 0.15f, 0.18f, 1f));
                var s = slot.rectTransform; s.anchorMin = s.anchorMax = new Vector2(0.5f, 0); s.pivot = new Vector2(0.5f, 0);
                s.sizeDelta = new Vector2(colW, 150); s.anchoredPosition = new Vector2(gx + i * (colW + colGap), 206);

                var mini = MakeCircle("Mini", slot.transform, RarityCols[0]);
                var m = mini.rectTransform; m.anchorMin = m.anchorMax = new Vector2(0.5f, 0.5f); m.pivot = new Vector2(0.5f, 0.5f);
                m.sizeDelta = new Vector2(96, 96); m.anchoredPosition = Vector2.zero;
                mini.gameObject.SetActive(false); // no slime yet
                slotMini[i] = mini;

                var lockOv = MakePanel("Lock", slot.transform, new Color(0.07f, 0.08f, 0.10f, 0.96f));
                var lk = lockOv.rectTransform; lk.anchorMin = Vector2.zero; lk.anchorMax = Vector2.one; lk.offsetMin = Vector2.zero; lk.offsetMax = Vector2.zero;
                var lkTxt = MakeText("LockText", lockOv.transform, "Locked", 24, SubTextCol, TextAnchor.MiddleCenter);
                Stretch(lkTxt.rectTransform);
                lockOv.gameObject.SetActive(i >= 2); // first 2 slots unlocked
                slotLock[i] = lockOv.gameObject;
            }

            // dice: centred, sits above the section, dips ~20% into its top edge (matching border)
            var frame = MakePanel("DiceFrame", outer.transform, BorderCol);
            var dfr = frame.rectTransform; dfr.anchorMin = dfr.anchorMax = new Vector2(0.5f, 1); dfr.pivot = new Vector2(0.5f, 0);
            dfr.sizeDelta = new Vector2(206, 206); dfr.anchoredPosition = new Vector2(0, -42);
            var dice = MakePanel("DiceButton", frame.transform, DiceCol);
            var dr = dice.rectTransform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
            dr.offsetMin = new Vector2(Border, Border); dr.offsetMax = new Vector2(-Border, -Border);
            diceImg = dice;
            diceBtn = dice.gameObject.AddComponent<Button>();
            diceBtn.transition = Selectable.Transition.None; diceBtn.targetGraphic = dice;

            Color pipCol = new Color(0.16f, 0.17f, 0.21f, 1f);
            float d = 46f, pip = 28f;
            Vector2[] pipPos = { new Vector2(0, 0), new Vector2(-d, d), new Vector2(d, d), new Vector2(-d, -d), new Vector2(d, -d) };
            foreach (var pp in pipPos)
            {
                var pipImg = MakeCircle("Pip", dice.transform, pipCol);
                var prc = pipImg.rectTransform; prc.anchorMin = prc.anchorMax = new Vector2(0.5f, 0.5f); prc.pivot = new Vector2(0.5f, 0.5f);
                prc.sizeDelta = new Vector2(pip, pip); prc.anchoredPosition = pp;
            }

            // cooldown overlay (radial wipe over the dice); fill 1 = cooling, 0 = ready
            var cd = MakeCircle("Cooldown", dice.transform, new Color(0f, 0f, 0f, 0.55f));
            var cdr = cd.rectTransform; cdr.anchorMin = Vector2.zero; cdr.anchorMax = Vector2.one; cdr.offsetMin = Vector2.zero; cdr.offsetMax = Vector2.zero;
            cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360; cd.fillOrigin = (int)Image.Origin360.Top; cd.fillClockwise = true;
            cd.fillAmount = 0f; cd.raycastTarget = false;
            cooldownOverlay = cd;

            var roll = MakeText("RollLabel", outer.transform, "TAP TO ROLL", 26, TextCol, TextAnchor.MiddleCenter);
            var rr = roll.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 1); rr.pivot = new Vector2(0.5f, 0);
            rr.sizeDelta = new Vector2(360, 40); rr.anchoredPosition = new Vector2(0, 172);
        }

        // ================= pull popup =================

        static void BuildPullPopup(Transform root, out Text nameText, out Text chanceText, out GameObject popupRoot)
        {
            var bgp = MakePanel("PullPopup", root, new Color(0.10f, 0.11f, 0.14f, 0.92f));
            var p = bgp.rectTransform; p.anchorMin = p.anchorMax = new Vector2(0.5f, 1); p.pivot = new Vector2(0.5f, 1);
            p.sizeDelta = new Vector2(760, 170); p.anchoredPosition = new Vector2(0, -520);
            bgp.gameObject.AddComponent<CanvasGroup>();
            popupRoot = bgp.gameObject;

            nameText = MakeText("PopupName", bgp.transform, "—", 50, TextCol, TextAnchor.MiddleCenter);
            var n = nameText.rectTransform; n.anchorMin = new Vector2(0, 1); n.anchorMax = new Vector2(1, 1); n.pivot = new Vector2(0.5f, 1);
            n.sizeDelta = new Vector2(-30, 88); n.anchoredPosition = new Vector2(0, -18);

            chanceText = MakeText("PopupChance", bgp.transform, "", 30, SubTextCol, TextAnchor.MiddleCenter);
            var c = chanceText.rectTransform; c.anchorMin = new Vector2(0, 0); c.anchorMax = new Vector2(1, 0); c.pivot = new Vector2(0.5f, 0);
            c.sizeDelta = new Vector2(-30, 54); c.anchoredPosition = new Vector2(0, 18);
        }

        // ================= inventory =================

        static InventoryUI BuildInventoryPanel(Transform root, out GameObject panelGO, out Button closeBtn)
        {
            var panel = MakeImage("InventoryPanel", root, new Color(0, 0, 0, 0.66f));
            Stretch(panel.rectTransform);
            panelGO = panel.gameObject;
            var ui = panelGO.AddComponent<InventoryUI>();

            var card = MakePanel("Card", panel.transform, PanelDark);
            var cr = card.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(940, 1320); cr.anchoredPosition = Vector2.zero;

            var title = MakeText("Title", card.transform, "Inventory", 46, TextCol, TextAnchor.MiddleLeft);
            var ti = title.rectTransform; ti.anchorMin = new Vector2(0, 1); ti.anchorMax = new Vector2(1, 1); ti.pivot = new Vector2(0.5f, 1);
            ti.sizeDelta = new Vector2(-80, 90); ti.anchoredPosition = new Vector2(40, -30);
            closeBtn = MakeCloseButton(card.transform);

            int n = RarityNames.Length;
            var rowButtons = new Button[n]; var rowBgs = new Image[n]; var counts = new Text[n];
            float rowW = 840, rowH = 140, startY = 460, stepY = 165;
            for (int i = 0; i < n; i++)
            {
                var row = MakePanel("Row_" + i, card.transform, new Color(0.13f, 0.15f, 0.18f, 1f));
                var rr = row.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f); rr.pivot = new Vector2(0.5f, 0.5f);
                rr.sizeDelta = new Vector2(rowW, rowH); rr.anchoredPosition = new Vector2(0, startY - i * stepY);
                rowBgs[i] = row;
                rowButtons[i] = row.gameObject.AddComponent<Button>(); rowButtons[i].targetGraphic = row;

                var blob = MakeCircle("Slime", row.transform, RarityCols[i]);
                var bl = blob.rectTransform; bl.anchorMin = bl.anchorMax = new Vector2(0, 0.5f); bl.pivot = new Vector2(0, 0.5f);
                bl.sizeDelta = new Vector2(96, 96); bl.anchoredPosition = new Vector2(28, 0);

                var nm = MakeText("Name", row.transform, RarityNames[i] + " Slime", 36, RarityCols[i], TextAnchor.MiddleLeft);
                var nmr = nm.rectTransform; nmr.anchorMin = new Vector2(0, 0); nmr.anchorMax = new Vector2(1, 1); nmr.pivot = new Vector2(0.5f, 0.5f);
                nmr.offsetMin = new Vector2(150, 0); nmr.offsetMax = new Vector2(-180, 0);

                counts[i] = MakeText("Count", row.transform, "x0", 40, TextCol, TextAnchor.MiddleRight);
                var cnt = counts[i].rectTransform; cnt.anchorMin = new Vector2(1, 0); cnt.anchorMax = new Vector2(1, 1); cnt.pivot = new Vector2(1, 0.5f);
                cnt.sizeDelta = new Vector2(160, 0); cnt.anchoredPosition = new Vector2(-30, 0);
            }

            // Auto-Equip Best (full width)
            var auto = MakePanel("AutoEquipButton", card.transform, new Color(0.28f, 0.42f, 0.58f, 1f));
            var ar = auto.rectTransform; ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0); ar.pivot = new Vector2(0.5f, 0);
            ar.sizeDelta = new Vector2(860, 110); ar.anchoredPosition = new Vector2(0, 180);
            var autoBtn = auto.gameObject.AddComponent<Button>(); autoBtn.targetGraphic = auto;
            var autoLabel = MakeText("Label", auto.transform, "Auto-Equip Best", 32, Color.white, TextAnchor.MiddleCenter);
            Stretch(autoLabel.rectTransform);

            var sell = MakePanel("SellButton", card.transform, new Color(0.30f, 0.55f, 0.32f, 1f));
            var sr = sell.rectTransform; sr.anchorMin = new Vector2(0.5f, 0); sr.anchorMax = new Vector2(0.5f, 0); sr.pivot = new Vector2(0.5f, 0);
            sr.sizeDelta = new Vector2(860, 110); sr.anchoredPosition = new Vector2(0, 40);
            var sellBtn = sell.gameObject.AddComponent<Button>(); sellBtn.targetGraphic = sell;
            var sellLabel = MakeText("SellLabel", sell.transform, "Select a slime", 36, Color.white, TextAnchor.MiddleCenter);
            Stretch(sellLabel.rectTransform);

            ui.rowButtons = rowButtons; ui.rowBackgrounds = rowBgs; ui.countTexts = counts;
            ui.sellButton = sellBtn; ui.sellLabel = sellLabel;
            ui.autoEquipButton = autoBtn;
            return ui;
        }

        // ================= skill tree =================

        static void BuildSkillTreePanel(Transform root, out GameObject panelGO, out Button closeBtn, out SkillNode[] nodesOut)
        {
            var panel = MakeImage("SkillsPanel", root, new Color(0, 0, 0, 0.66f));
            Stretch(panel.rectTransform);
            panelGO = panel.gameObject;

            var card = MakePanel("Card", panel.transform, PanelDark);
            var cr = card.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(940, 1240); cr.anchoredPosition = Vector2.zero;

            var title = MakeText("Title", card.transform, "Skill Tree", 46, TextCol, TextAnchor.MiddleLeft);
            var ti = title.rectTransform; ti.anchorMin = new Vector2(0, 1); ti.anchorMax = new Vector2(1, 1); ti.pivot = new Vector2(0.5f, 1);
            ti.sizeDelta = new Vector2(-80, 90); ti.anchoredPosition = new Vector2(40, -30);
            closeBtn = MakeCloseButton(card.transform);

            var hint = MakeText("Hint", card.transform, "Buy a hex (gold) to apply it and reveal its neighbours", 26, SubTextCol, TextAnchor.MiddleCenter);
            var hr = hint.rectTransform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(1, 0); hr.pivot = new Vector2(0.5f, 0);
            hr.sizeDelta = new Vector2(-60, 56); hr.anchoredPosition = new Vector2(0, 40);

            float W = 178f, H = W * Mathf.Sqrt(3f) / 2f;
            Vector2 ctr = new Vector2(0, 40);
            Vector2[] pos = {
                ctr,
                ctr + new Vector2(0,  H),
                ctr + new Vector2( W * 0.75f,  H * 0.5f),
                ctr + new Vector2( W * 0.75f, -H * 0.5f),
                ctr + new Vector2(0, -H),
                ctr + new Vector2(-W * 0.75f, -H * 0.5f),
                ctr + new Vector2(-W * 0.75f,  H * 0.5f),
            };
            string[] names = { "Auto Roll", "Hero Slot", "Luck", "Damage", "Gold", "Crit", "Speed" };
            SkillNode.Effect[] effects = {
                SkillNode.Effect.AutoRoll, SkillNode.Effect.HeroSlot, SkillNode.Effect.Luck, SkillNode.Effect.Damage,
                SkillNode.Effect.Gold, SkillNode.Effect.Crit, SkillNode.Effect.Speed
            };
            int[] baseCosts = { 0, 100, 60, 60, 60, 80, 80 };

            var nodes = new SkillNode[pos.Length];
            for (int i = 0; i < pos.Length; i++) nodes[i] = MakeHexNode(card.transform, names[i], pos[i], W, effects[i], baseCosts[i]);

            nodes[0].neighbors = new[] { nodes[1], nodes[2], nodes[3], nodes[4], nodes[5], nodes[6] };
            for (int i = 1; i < nodes.Length; i++) nodes[i].neighbors = new SkillNode[0];

            nodes[0].SetState(SkillNode.State.Available);
            for (int i = 1; i < nodes.Length; i++) nodes[i].SetState(SkillNode.State.Hidden);
            nodesOut = nodes;
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
