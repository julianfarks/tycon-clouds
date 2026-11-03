using System;
using System.Collections.Generic;
using System.Text;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Islands;
using CloudGarden.OfflineProduction;
using CloudGarden.Optimization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloudGarden.UI
{
    /// <summary>The whole heads-up display: top bar, bottom menu, panels, building card, build bar and popups.</summary>
    public sealed class HudController : MonoBehaviour
    {
        UiContext ctx;
        Canvas canvas;
        RectTransform root;
        Rect lastSafeArea;

        // top bar
        Text coinText, coinRateText;
        sealed class Chip { public GameObject Go; public Text Amount; public Text Rate; public Image Fill; }
        readonly Chip[] chips = new Chip[ResourceInfo.Count];
        float topTimer;

        // bottom menu
        Image[] menuBackgrounds;
        RectTransform menuBar;

        // side panel
        RectTransform panelRoot, panelContent;
        Text panelTitle;
        ScrollRect panelScroll;
        PanelBase[] panels;
        int openPanel = -1;
        float refreshTimer;
        int lastVersion = -1;

        // building card
        RectTransform cardRoot;
        Text cardTitle, cardBody;
        Button upgradeBtn, moveBtn, removeBtn, focusBtn;
        int cardUid;
        float cardTimer;

        // placement bar
        RectTransform placeBar;
        Text placeText;
        Button placeConfirm;

        // collect
        RectTransform collectRoot;
        RectTransform sellRoot;
        float collectPulse;

        // toasts and popups
        Text toastText;
        RectTransform toastRoot;
        readonly Queue<string> toasts = new Queue<string>();
        float toastTimer;
        readonly List<FloatingText> popups = new List<FloatingText>();
        GameObjectPool popupPool;

        // locked labels
        RectTransform[] lockLabels;
        Text[] lockTexts;

        // modal
        RectTransform modalRoot;
        Text modalTitle, modalBody;
        Button modalPrimary, modalSecondary;
        Action modalPrimaryAction, modalSecondaryAction;

        public void Init(UiContext context)
        {
            ctx = context;
            ctx.Toast = ShowToast;
            ctx.Confirm = ShowConfirm;
            ctx.ClosePanel = ClosePanel;
            ctx.OpenPanel = OpenPanelByIndex;

            BuildCanvas();
            BuildTopBar();
            BuildBottomMenu();
            BuildSidePanel();
            BuildBuildingCard();
            BuildPlacementBar();
            BuildCollectButton();
            BuildToast();
            BuildLockLabels();
            BuildModal();

            panels = new PanelBase[]
            {
                new BuildPanel(ctx), new UpgradePanel(ctx), new ResearchPanel(ctx),
                new TradePanel(ctx), new ExpandPanel(ctx), new ManagePanel(ctx)
            };
            ((BuildPanel)panels[0]).Refresh = RebuildPanel;
            ((ManagePanel)panels[5]).Refresh = RebuildPanel;

            ctx.State.Toast += ShowToast;
            ctx.Selection.SelectionChanged += OnSelectionChanged;
            ctx.Selection.LockedIslandTapped += OnLockedIslandTapped;
            ctx.Placement.Changed += OnPlacementChanged;
            ctx.State.WorldReset += OnWorldReset;
            OnPlacementChanged();
            UpdateTopBar();
        }

        void OnDestroy()
        {
            if (ctx == null) return;
            ctx.State.Toast -= ShowToast;
            ctx.State.WorldReset -= OnWorldReset;
            if (ctx.Selection != null)
            {
                ctx.Selection.SelectionChanged -= OnSelectionChanged;
                ctx.Selection.LockedIslandTapped -= OnLockedIslandTapped;
            }
            if (ctx.Placement != null) ctx.Placement.Changed -= OnPlacementChanged;
        }

        // ================================================================== canvas

        void BuildCanvas()
        {
            GameObject cg = new GameObject("HUD");
            cg.transform.SetParent(transform, false);
            canvas = cg.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = cg.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            cg.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            root = UIKit.NewRect("SafeRoot", cg.transform);
            UIKit.Stretch(root);
            ApplySafeArea();
        }

        void ApplySafeArea()
        {
            Rect sa = Screen.safeArea;
            if (sa == lastSafeArea && lastSafeArea.width > 0) return;
            lastSafeArea = sa;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            root.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            root.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }

        // ================================================================== top bar

        void BuildTopBar()
        {
            Image bar = UIKit.Panel(root, "TopBar", UIKit.BarBg);
            RectTransform rt = bar.rectTransform;
            UIKit.Place(rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -118), new Vector2(0, 0));

            // coins
            Image coin = UIKit.Panel(rt, "Coins", new Color(1f, 1f, 1f, 0.10f));
            UIKit.Place(coin.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(10, 10), new Vector2(440, -10));
            Image icon = UIKit.Icon(coin.transform, IconFactory.Resource(ResourceType.Coins), 80);
            UIKit.Box(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(80, 80));
            coinText = UIKit.Label(coin.transform, "0", 46, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(coinText.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 1), new Vector2(106, 0), new Vector2(-10, -4));
            coinText.horizontalOverflow = HorizontalWrapMode.Overflow;
            coinRateText = UIKit.Label(coin.transform, "+0/s", 24, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.Place(coinRateText.rectTransform, new Vector2(0, 0), new Vector2(1, 0.4f), new Vector2(106, 6), new Vector2(-10, 0));

            // resources (horizontal scroll)
            RectTransform sv = UIKit.NewRect("Resources", rt);
            UIKit.Place(sv, new Vector2(0, 0), new Vector2(1, 1), new Vector2(456, 8), new Vector2(-136, -8));
            ScrollRect sr = sv.gameObject.AddComponent<ScrollRect>();
            Image svBg = sv.gameObject.AddComponent<Image>();
            svBg.color = new Color(0, 0, 0, 0.001f);
            RectTransform vp = UIKit.NewRect("Viewport", sv);
            UIKit.Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            RectTransform content = UIKit.NewRect("Content", vp);
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            HorizontalLayoutGroup h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.padding = new RectOffset(4, 4, 2, 2);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Clamped;

            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                ResourceType t = (ResourceType)r;
                Chip c = new Chip();
                Image chip = UIKit.Panel(content, "Chip_" + t, new Color(1f, 1f, 1f, 0.10f));
                c.Go = chip.gameObject;
                UIKit.Fixed(c.Go, 232, -1);
                HorizontalLayoutGroup row = UIKit.HStack(c.Go, 6, 8);
                row.childAlignment = TextAnchor.MiddleLeft;
                UIKit.Icon(c.Go.transform, IconFactory.Resource(t), 62);
                RectTransform col = UIKit.NewRect("Col", c.Go.transform);
                VerticalLayoutGroup v = UIKit.VStack(col.gameObject, 0, 0);
                v.childAlignment = TextAnchor.MiddleLeft;
                UIKit.Flex(col.gameObject, 1f);
                c.Amount = UIKit.Label(col, "0", 27, UIKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
                c.Amount.horizontalOverflow = HorizontalWrapMode.Overflow;
                c.Rate = UIKit.Label(col, "", 21, UIKit.TextDim, TextAnchor.MiddleLeft);
                c.Rate.horizontalOverflow = HorizontalWrapMode.Overflow;
                c.Go.SetActive(false);
                chips[r] = c;
            }

            // stats button
            Button stats = UIKit.MakeButton(rt, "", UIKit.CardBgLight, () => OpenManage(0), 20);
            UIKit.Place(stats.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 1), new Vector2(-126, 12), new Vector2(-12, -12));
            Image si = UIKit.Icon(stats.transform, IconFactory.Menu(IconFactory.MenuStats), 70);
            UIKit.Box(si.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
        }

        void OpenManage(int tab)
        {
            ManagePanel.SelectTab(tab);
            OpenPanelByIndex(5);
        }

        void UpdateTopBar()
        {
            GameState s = ctx.State;
            coinText.text = NumberFormat.Compact(s.Res.Get(ResourceType.Coins));
            double rate = s.Stats.CoinRate;
            coinRateText.text = "+" + NumberFormat.Compact(rate) + "/s";

            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                Chip c = chips[r];
                double amount = s.Res.Amount[r];
                bool visible = amount > 0 || s.Res.TotalProduced[r] > 0;
                if (c.Go.activeSelf != visible) c.Go.SetActive(visible);
                if (!visible) continue;
                double cap = s.Res.Capacity[r];
                bool full = amount >= cap * 0.999;
                c.Amount.text = NumberFormat.Compact(amount) + "<size=20><color=#A9B0D0> /" + NumberFormat.Compact(cap) + "</color></size>";
                c.Amount.color = full ? UIKit.Bad : UIKit.TextMain;
                double net = s.Stats.ProdRate[r] - s.Stats.ConsRate[r];
                if (Math.Abs(net) < 0.005) c.Rate.text = "";
                else c.Rate.text = (net > 0 ? "+" : "") + NumberFormat.Compact(net) + "/s";
                c.Rate.color = net >= 0 ? UIKit.Good : UIKit.Warn;
            }
        }

        // ================================================================== bottom menu

        void BuildBottomMenu()
        {
            Image bar = UIKit.Panel(root, "BottomMenu", UIKit.BarBg);
            menuBar = bar.rectTransform;
            UIKit.Place(menuBar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 140));
            HorizontalLayoutGroup h = UIKit.HStack(bar.gameObject, 8, 10);
            h.childForceExpandWidth = true;
            h.childAlignment = TextAnchor.MiddleCenter;

            string[] labels = { "Construir", "Mejoras", "Investigar", "Comercio", "Expandir", "Gestión" };
            int[] icons =
            {
                IconFactory.MenuBuild, IconFactory.MenuUpgrade, IconFactory.MenuResearch,
                IconFactory.MenuTrade, IconFactory.MenuExpand, IconFactory.MenuManage
            };
            menuBackgrounds = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                Button b = UIKit.MakeButton(bar.transform, "", UIKit.CardBgLight, () => TogglePanel(idx), 20);
                menuBackgrounds[i] = b.GetComponent<Image>();
                UIKit.Flex(b.gameObject, 1f);
                // remove the empty default label
                Text empty = b.GetComponentInChildren<Text>();
                if (empty != null) Destroy(empty.gameObject);
                VerticalLayoutGroup v = UIKit.VStack(b.gameObject, 0, 6);
                v.childAlignment = TextAnchor.MiddleCenter;
                Image ic = UIKit.Icon(b.transform, IconFactory.Menu(icons[i]), 62);
                Text lbl = UIKit.Label(b.transform, labels[i], 25, UIKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
                lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Fixed(lbl.gameObject, -1, 32);
            }
        }

        void TogglePanel(int index)
        {
            if (openPanel == index) ClosePanel();
            else OpenPanelByIndex(index);
        }

        void OpenPanelByIndex(int index)
        {
            if (index < 0 || index >= panels.Length) return;
            if (ctx.Placement.Active) ctx.Placement.Cancel();
            ctx.Selection.Clear();
            openPanel = index;
            panelRoot.gameObject.SetActive(true);
            panelTitle.text = panels[index].Title;
            if (panelScroll != null) panelContent.anchoredPosition = Vector2.zero;
            RebuildPanel();
            UpdateMenuHighlight();
            UpdateOverlays();
        }

        void ClosePanel()
        {
            openPanel = -1;
            panelRoot.gameObject.SetActive(false);
            UpdateMenuHighlight();
            UpdateOverlays();
        }

        void UpdateMenuHighlight()
        {
            for (int i = 0; i < menuBackgrounds.Length; i++)
                menuBackgrounds[i].color = i == openPanel ? UIKit.Accent : UIKit.CardBgLight;
        }

        // ================================================================== side panel

        void BuildSidePanel()
        {
            Image p = UIKit.Panel(root, "SidePanel", UIKit.PanelBg);
            panelRoot = p.rectTransform;
            UIKit.Place(panelRoot, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-860, 152), new Vector2(-12, -126));

            Image head = UIKit.Panel(panelRoot, "Head", new Color(1f, 1f, 1f, 0.06f));
            UIKit.Place(head.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -84), new Vector2(-8, -8));
            panelTitle = UIKit.Label(head.transform, "", 38, UIKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(panelTitle.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 0), new Vector2(-100, 0));
            Button close = UIKit.MakeButton(head.transform, "", UIKit.Bad, ClosePanel, 20);
            UIKit.Place(close.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 1), new Vector2(-84, 6), new Vector2(-6, -6));
            Destroy(close.GetComponentInChildren<Text>().gameObject);
            Image ci = UIKit.Icon(close.transform, IconFactory.Menu(IconFactory.MenuClose), 44);
            UIKit.Box(ci.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));

            RectTransform content;
            panelScroll = UIKit.ScrollView(panelRoot, out content);
            panelContent = content;
            UIKit.Place(panelScroll.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 8), new Vector2(-8, -92));
            panelRoot.gameObject.SetActive(false);
        }

        void RebuildPanel()
        {
            if (openPanel < 0) return;
            float y = panelContent.anchoredPosition.y;
            UIKit.Clear(panelContent);
            panels[openPanel].Build(panelContent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelContent);
            float maxY = Mathf.Max(0f, panelContent.rect.height - panelScroll.viewport.rect.height);
            panelContent.anchoredPosition = new Vector2(0, Mathf.Clamp(y, 0f, maxY));
            lastVersion = ctx.State.Version;
            refreshTimer = 0f;
        }

        static bool PointerDown()
        {
            return Input.touchCount > 0 || Input.GetMouseButton(0);
        }

        // ================================================================== building card

        void BuildBuildingCard()
        {
            Image card = UIKit.Panel(root, "BuildingCard", UIKit.PanelBg);
            cardRoot = card.rectTransform;
            UIKit.Box(cardRoot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 156), new Vector2(820, 100));
            VerticalLayoutGroup v = UIKit.VStack(card.gameObject, 8, 16);
            ContentSizeFitter fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            cardTitle = UIKit.Label(card.transform, "", 38, UIKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Fixed(cardTitle.gameObject, -1, 50);
            cardBody = UIKit.Label(card.transform, "", 25, UIKit.TextMain, TextAnchor.UpperLeft);

            RectTransform row = UIKit.NewRect("Buttons", card.transform);
            HorizontalLayoutGroup h = UIKit.HStack(row.gameObject, 8, 0);
            h.childForceExpandWidth = true;
            UIKit.Fixed(row.gameObject, -1, 84);
            upgradeBtn = UIKit.MakeButton(row, "Mejorar", UIKit.Good, OnUpgradeClicked, 25);
            UIKit.Flex(upgradeBtn.gameObject, 1.6f);
            moveBtn = UIKit.MakeButton(row, "Mover", UIKit.Accent, OnMoveClicked, 25);
            UIKit.Flex(moveBtn.gameObject, 1f);
            removeBtn = UIKit.MakeButton(row, "Retirar", UIKit.Bad, OnRemoveClicked, 25);
            UIKit.Flex(removeBtn.gameObject, 1f);
            focusBtn = UIKit.MakeButton(row, "Cerrar", UIKit.CardBgLight, () => ctx.Selection.Clear(), 25);
            UIKit.Flex(focusBtn.gameObject, 1f);
            cardRoot.gameObject.SetActive(false);
        }

        void OnSelectionChanged(int uid)
        {
            cardUid = uid;
            UpdateCard();
            UpdateOverlays();
        }

        void OnLockedIslandTapped(int region)
        {
            OpenPanelByIndex(4);
        }

        void UpdateCard()
        {
            if (cardUid == 0 || ctx.State.FindBuilding(cardUid) == null)
            {
                cardRoot.gameObject.SetActive(false);
                return;
            }
            GameState s = ctx.State;
            BuildingInstance b = s.FindBuilding(cardUid);
            cardRoot.gameObject.SetActive(true);
            cardTitle.text = b.Def.Name + "   <size=28><color=#A9B0D0>Nv." + b.Level + "/" + LevelMath.MaxLevel + "</color></size>";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(BuildingInfo.StatusText(b));
            sb.AppendLine(BuildingInfo.RatesText(s, b.Def, b.Level));
            if (b.Def.IsProducer && b.Level < LevelMath.MaxLevel)
                sb.AppendLine(PanelKit.Colored("Nv." + (b.Level + 1) + ": ", UIKit.TextDim) + BuildingInfo.RatesText(s, b.Def, b.Level + 1));
            if (!b.Def.IsProducer && b.Level < LevelMath.MaxLevel)
                sb.AppendLine(PanelKit.Colored("Nv." + (b.Level + 1) + ": ", UIKit.TextDim) + BuildingInfo.RoleText(s, b.Def, b.Level + 1));
            sb.Append(PanelKit.Colored(b.Def.Description, UIKit.TextDim));
            cardBody.text = sb.ToString();

            if (b.Level >= LevelMath.MaxLevel)
            {
                UIKit.SetButtonLabel(upgradeBtn, "Nivel máximo");
                upgradeBtn.interactable = false;
            }
            else
            {
                double cost = s.UpgradeCost(b);
                UIKit.SetButtonLabel(upgradeBtn, "Mejorar  " + NumberFormat.Compact(cost));
                upgradeBtn.interactable = s.Res.CanAffordCoins(cost);
            }
        }

        void OnUpgradeClicked()
        {
            if (cardUid == 0) return;
            if (ctx.State.TryUpgrade(cardUid)) UpdateCard();
        }

        void OnMoveClicked()
        {
            int uid = cardUid;
            if (uid == 0) return;
            ctx.Selection.Clear();
            ctx.Placement.BeginMove(uid);
        }

        void OnRemoveClicked()
        {
            BuildingInstance b = ctx.State.FindBuilding(cardUid);
            if (b == null) return;
            int uid = b.Uid;
            double refund = b.PaidCoins * GameState.RefundFraction;
            ShowConfirm("¿Retirar " + b.Def.Name + "? Recuperas " + NumberFormat.Compact(refund) + " monedas (60% de lo invertido).", () =>
            {
                ctx.State.TryRemove(uid);
            });
        }

        // ================================================================== placement bar

        void BuildPlacementBar()
        {
            Image bar = UIKit.Panel(root, "PlaceBar", UIKit.PanelBg);
            placeBar = bar.rectTransform;
            UIKit.Box(placeBar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 156), new Vector2(1160, 100));
            VerticalLayoutGroup v = UIKit.VStack(bar.gameObject, 8, 14);
            ContentSizeFitter fit = bar.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            placeText = UIKit.Label(bar.transform, "", 30, UIKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fixed(placeText.gameObject, -1, 80);
            RectTransform row = UIKit.NewRect("Buttons", bar.transform);
            HorizontalLayoutGroup h = UIKit.HStack(row.gameObject, 10, 0);
            h.childForceExpandWidth = true;
            UIKit.Fixed(row.gameObject, -1, 90);
            Button rot = UIKit.MakeButton(row, "Rotar", UIKit.Accent, () => ctx.Placement.Rotate(), 28);
            UIKit.Flex(rot.gameObject, 1f);
            placeConfirm = UIKit.MakeButton(row, "Construir", UIKit.Good, () => ctx.Placement.Confirm(), 30);
            UIKit.Flex(placeConfirm.gameObject, 1.6f);
            Button cancel = UIKit.MakeButton(row, "Cancelar", UIKit.Bad, () => ctx.Placement.Cancel(), 28);
            UIKit.Flex(cancel.gameObject, 1f);
            placeBar.gameObject.SetActive(false);
        }

        void OnPlacementChanged()
        {
            PlacementController p = ctx.Placement;
            placeBar.gameObject.SetActive(p.Active);
            if (p.Active)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(p.Def.Name);
                if (p.Mode == PlacementController.PlacementMode.Place)
                    sb.Append("   " + PanelKit.Colored(NumberFormat.Compact(p.CurrentCost) + " monedas", ctx.State.Res.CanAffordCoins(p.CurrentCost) ? UIKit.Gold : UIKit.Bad));
                sb.AppendLine();
                sb.Append(PanelKit.Colored(p.StatusText(), p.IsValid && p.CanAfford ? UIKit.Good : UIKit.Bad));
                sb.Append(PanelKit.Colored("    Arrastra el edificio o toca una casilla", UIKit.TextDim));
                placeText.text = sb.ToString();
                UIKit.SetButtonLabel(placeConfirm, p.Mode == PlacementController.PlacementMode.Move ? "Mover aquí" : "Construir");
                placeConfirm.interactable = p.IsValid && p.CanAfford;
            }
            UpdateOverlays();
        }

        // ================================================================== collect button

        void BuildCollectButton()
        {
            Image c = UIKit.Panel(root, "Collect", new Color(0.35f, 0.65f, 1f, 0.95f));
            c.sprite = UIKit.Circle;
            c.type = Image.Type.Simple;
            collectRoot = c.rectTransform;
            UIKit.Box(collectRoot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 170), new Vector2(210, 210));
            Button b = c.gameObject.AddComponent<Button>();
            b.targetGraphic = c;
            b.onClick.AddListener(OnCollect);
            Image dew = UIKit.Icon(c.transform, IconFactory.Resource(ResourceType.Dew), 110);
            UIKit.Box(dew.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
            Text t = UIKit.Label(c.transform, "Recolectar", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(t.rectTransform, new Vector2(0, 0), new Vector2(1, 0.3f), new Vector2(0, 8), new Vector2(0, 0));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;

            Button sell = UIKit.MakeButton(root, "Vender rocío", UIKit.Gold, OnQuickSell, 28);
            sell.GetComponentInChildren<Text>().color = new Color(0.25f, 0.18f, 0.02f);
            sellRoot = sell.GetComponent<RectTransform>();
            UIKit.Box(sellRoot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(270, 190), new Vector2(230, 100));
        }

        void OnQuickSell()
        {
            double coins = ctx.State.SellAll(ResourceType.Dew);
            if (coins > 0) SpawnPopup("+" + NumberFormat.Compact(coins) + " monedas", sellRoot.anchoredPosition + new Vector2(115, 140), UIKit.Gold);
            else SpawnPopup("Nada que vender", sellRoot.anchoredPosition + new Vector2(115, 140), UIKit.TextDim);
        }

        void OnCollect()
        {
            double got = ctx.State.Collect();
            if (got > 0) SpawnPopup("+" + NumberFormat.Compact(got) + " Rocío", collectRoot.anchoredPosition + new Vector2(105, 230), UIKit.TextMain);
            else SpawnPopup("¡Almacén lleno!", collectRoot.anchoredPosition + new Vector2(105, 230), UIKit.Bad);
        }

        // ================================================================== toasts & popups

        void BuildToast()
        {
            Image t = UIKit.Panel(root, "Toast", new Color(0.05f, 0.07f, 0.14f, 0.95f));
            toastRoot = t.rectTransform;
            UIKit.Box(toastRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(1000, 74));
            toastText = UIKit.Label(t.transform, "", 29, UIKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(toastText.rectTransform);
            toastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            toastRoot.gameObject.SetActive(false);
        }

        public void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (toasts.Count > 4) toasts.Dequeue();
            toasts.Enqueue(message);
        }

        void UpdateToast(float dt)
        {
            if (toastTimer > 0f)
            {
                toastTimer -= dt;
                if (toastTimer <= 0f) toastRoot.gameObject.SetActive(false);
            }
            if (toastTimer <= 0f && toasts.Count > 0)
            {
                toastText.text = toasts.Dequeue();
                toastRoot.gameObject.SetActive(true);
                toastTimer = 2.6f;
            }
        }

        void SpawnPopup(string text, Vector2 anchoredFromBottomLeft, Color color)
        {
            if (popupPool == null) popupPool = new GameObjectPool(() => FloatingText.Create(root), root, 6);
            GameObject go = popupPool.Get();
            FloatingText f = go.GetComponent<FloatingText>();
            f.Begin(text, color, anchoredFromBottomLeft);
            popups.Add(f);
        }

        void UpdatePopups(float dt)
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                FloatingText f = popups[i];
                if (!f.Step(dt))
                {
                    popups.RemoveAt(i);
                    popupPool.Release(f.gameObject);
                }
            }
        }

        // ================================================================== locked island labels

        void BuildLockLabels()
        {
            lockLabels = new RectTransform[RegionDatabase.Count];
            lockTexts = new Text[RegionDatabase.Count];
            for (int i = 1; i < RegionDatabase.Count; i++)
            {
                Image bg = UIKit.Panel(root, "Lock" + i, new Color(0.05f, 0.07f, 0.14f, 0.8f));
                bg.raycastTarget = false;
                UIKit.Box(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 96));
                Text t = UIKit.Label(bg.transform, RegionDatabase.Get(i).Name + "\n<size=22><color=#FFB84A>Bloqueada - toca la isla</color></size>", 30, UIKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(t.rectTransform);
                lockLabels[i] = bg.rectTransform;
                lockTexts[i] = t;
                bg.gameObject.SetActive(false);
            }
        }

        void UpdateLockLabels()
        {
            Camera cam = ctx.Camera.Cam;
            if (cam == null) return;
            for (int i = 1; i < lockLabels.Length; i++)
            {
                RectTransform rt = lockLabels[i];
                bool locked = !ctx.State.RegionUnlocked[i];
                // Only the next locked island shows its label to keep the screen clean.
                bool next = locked && ctx.State.RegionUnlocked[i - 1];
                if (!next) { if (rt.gameObject.activeSelf) rt.gameObject.SetActive(false); continue; }
                Vector3 world = ctx.World.Islands[i].transform.position + new Vector3(0, 2.5f, 0);
                Vector3 sp = cam.WorldToScreenPoint(world);
                bool on = sp.z > 0 && sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height;
                if (rt.gameObject.activeSelf != on) rt.gameObject.SetActive(on);
                if (!on) continue;
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out local))
                    rt.anchoredPosition = local;
            }
        }

        // ================================================================== modal dialogs

        void BuildModal()
        {
            Image dim = UIKit.Plain(root, "Modal", new Color(0f, 0f, 0.05f, 0.7f));
            modalRoot = dim.rectTransform;
            UIKit.Stretch(modalRoot);
            Image box = UIKit.Panel(modalRoot, "Box", UIKit.PanelBg);
            UIKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 100));
            VerticalLayoutGroup v = UIKit.VStack(box.gameObject, 14, 28);
            ContentSizeFitter fit = box.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            modalTitle = UIKit.Label(box.transform, "", 44, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            modalBody = UIKit.Label(box.transform, "", 30, UIKit.TextMain, TextAnchor.UpperCenter);
            RectTransform row = UIKit.NewRect("Buttons", box.transform);
            HorizontalLayoutGroup h = UIKit.HStack(row.gameObject, 14, 0);
            h.childForceExpandWidth = true;
            UIKit.Fixed(row.gameObject, -1, 92);
            modalSecondary = UIKit.MakeButton(row, "Cancelar", UIKit.CardBgLight, () => CloseModal(false), 30);
            UIKit.Flex(modalSecondary.gameObject, 1f);
            modalPrimary = UIKit.MakeButton(row, "Aceptar", UIKit.Good, () => CloseModal(true), 30);
            UIKit.Flex(modalPrimary.gameObject, 1f);
            modalRoot.gameObject.SetActive(false);
        }

        void ShowModal(string title, string body, string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            modalTitle.text = title;
            modalBody.text = body;
            UIKit.SetButtonLabel(modalPrimary, primary);
            modalPrimaryAction = onPrimary;
            modalSecondaryAction = onSecondary;
            modalSecondary.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            if (!string.IsNullOrEmpty(secondary)) UIKit.SetButtonLabel(modalSecondary, secondary);
            modalRoot.gameObject.SetActive(true);
            modalRoot.SetAsLastSibling();
        }

        void CloseModal(bool primary)
        {
            modalRoot.gameObject.SetActive(false);
            Action a = primary ? modalPrimaryAction : modalSecondaryAction;
            modalPrimaryAction = null;
            modalSecondaryAction = null;
            if (a != null) a();
        }

        public void ShowConfirm(string message, Action onYes)
        {
            ShowModal("Confirmar", message, "Sí", onYes, "No", null);
        }

        public void ShowOfflineReport(OfflineReport r)
        {
            if (r == null || !r.HasGains) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Estuviste fuera " + NumberFormat.Duration(r.ElapsedSeconds) + ".");
            sb.AppendLine("Tus máquinas trabajaron " + NumberFormat.Duration(r.SimulatedSeconds) + (r.Capped ? " (límite sin conexión)" : "") + ".");
            sb.AppendLine();
            if (r.CoinsGained > 0.5) sb.AppendLine(PanelKit.Colored("+" + NumberFormat.Compact(r.CoinsGained) + " monedas", UIKit.Gold));
            for (int i = 1; i < ResourceInfo.Count; i++)
                if (r.Delta[i] >= 1) sb.AppendLine("+" + NumberFormat.Compact(r.Delta[i]) + " " + ResourceInfo.Name((ResourceType)i));
            if (r.ClockTamperDetected) sb.AppendLine(PanelKit.Colored("Se detectó un cambio de hora en el dispositivo.", UIKit.Warn));
            ShowModal("¡Bienvenido de vuelta!", sb.ToString(), "Recoger", null, "", null);
        }

        void OnWorldReset()
        {
            ctx.Selection.Clear();
            cardUid = 0;
            UpdateCard();
            if (openPanel >= 0) RebuildPanel();
        }

        // ================================================================== overlays & update

        void UpdateOverlays()
        {
            bool placing = ctx.Placement.Active;
            bool card = cardUid != 0 && ctx.State.FindBuilding(cardUid) != null;
            collectRoot.gameObject.SetActive(!placing && !card);
            sellRoot.gameObject.SetActive(!placing && !card);
            if (placing) cardRoot.gameObject.SetActive(false);
        }

        void Update()
        {
            ApplySafeArea();
            float dt = Time.unscaledDeltaTime;

            topTimer += dt;
            if (topTimer >= 0.2f)
            {
                topTimer = 0f;
                UpdateTopBar();
            }

            cardTimer += dt;
            if (cardUid != 0 && cardTimer >= 0.3f)
            {
                cardTimer = 0f;
                UpdateCard();
                UpdateOverlays();
            }

            if (openPanel >= 0)
            {
                refreshTimer += dt;
                bool versionChanged = ctx.State.Version != lastVersion;
                if (((versionChanged && refreshTimer > 0.12f) || refreshTimer >= 1.0f) && !PointerDown()) RebuildPanel();
            }

            collectPulse += dt;
            if (collectRoot.gameObject.activeSelf)
            {
                float s = 1f + Mathf.Sin(collectPulse * 3f) * 0.03f;
                collectRoot.localScale = new Vector3(s, s, 1f);
            }

            UpdateToast(dt);
            UpdatePopups(dt);
            UpdateLockLabels();

            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
        }

        void HandleBack()
        {
            if (modalRoot.gameObject.activeSelf) { CloseModal(false); return; }
            if (ctx.Placement.Active) { ctx.Placement.Cancel(); return; }
            if (openPanel >= 0) { ClosePanel(); return; }
            if (cardUid != 0) { ctx.Selection.Clear(); return; }
        }
    }
}
