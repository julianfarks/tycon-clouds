using System;
using System.Collections.Generic;
using System.Text;
using CloudGarden.Construction;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Islands;
using UnityEngine;
using UnityEngine.UI;

namespace CloudGarden.UI
{
    /// <summary>Everything the interface needs to talk to the game.</summary>
    public sealed class UiContext
    {
        public GameState State;
        public WorldView World;
        public CameraController Camera;
        public PlacementController Placement;
        public SelectionController Selection;
        public Action SaveNow;
        public Action ResetGame;
        public Action<string> Toast;
        public Action<string, Action> Confirm;
        public Action ClosePanel;
        public Action<int> OpenPanel;
    }

    public abstract class PanelBase
    {
        protected UiContext Ctx;
        public abstract string Title { get; }
        public abstract void Build(RectTransform content);

        protected PanelBase(UiContext ctx) { Ctx = ctx; }
    }

    /// <summary>Shared builders for list entries and formatted text.</summary>
    public static class PanelKit
    {
        public sealed class Entry
        {
            public string Title = "";
            public string Subtitle = "";
            public string Info = "";
            public Sprite Icon;
            public Color IconTint = Color.white;
            public string Button = "";
            public Color ButtonColor = UIKit.Accent;
            public bool ButtonEnabled = true;
            public Action OnClick;
            public string Button2 = "";
            public Color Button2Color = UIKit.CardBgLight;
            public Action OnClick2;
            public float Progress = -1f;
            public Color Tint = UIKit.CardBg;
        }

        public static string Hex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }

        public static string Colored(string text, Color c)
        {
            return "<color=#" + Hex(c) + ">" + text + "</color>";
        }

        public static string Coins(double v)
        {
            return NumberFormat.Compact(v) + " monedas";
        }

        /// <summary>Cost line with each missing item highlighted in red.</summary>
        public static string CostText(GameState s, double coins, IList<ResourceAmount> mats, double matMult)
        {
            StringBuilder sb = new StringBuilder();
            bool ok = s.Res.CanAffordCoins(coins);
            sb.Append(Colored(NumberFormat.Compact(coins) + " monedas", ok ? UIKit.Gold : UIKit.Bad));
            if (mats != null)
            {
                for (int i = 0; i < mats.Count; i++)
                {
                    double need = mats[i].Amount * matMult;
                    bool have = s.Res.Get(mats[i].Type) + 1e-9 >= need;
                    sb.Append("  ");
                    sb.Append(Colored(NumberFormat.Compact(need) + " " + ResourceInfo.ShortName(mats[i].Type), have ? UIKit.TextMain : UIKit.Bad));
                }
            }
            return sb.ToString();
        }

        public static string IoText(Buildings.BuildingDef d)
        {
            StringBuilder sb = new StringBuilder();
            if (d.Inputs.Count > 0)
            {
                sb.Append(Colored("Usa ", UIKit.TextDim));
                for (int i = 0; i < d.Inputs.Count; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    sb.Append(NumberFormat.Compact(d.Inputs[i].Amount) + " " + ResourceInfo.ShortName(d.Inputs[i].Type));
                }
                sb.Append("  ");
            }
            if (d.Outputs.Count > 0)
            {
                sb.Append(Colored("Produce ", UIKit.Good));
                for (int i = 0; i < d.Outputs.Count; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    sb.Append(NumberFormat.Compact(d.Outputs[i].Amount) + " " + ResourceInfo.ShortName(d.Outputs[i].Type));
                }
                sb.Append(Colored(" cada " + d.CycleSeconds.ToString("0.#") + "s", UIKit.TextDim));
            }
            return sb.ToString();
        }

        public static void Header(Transform parent, string text)
        {
            Text t = UIKit.Label(parent, text, 30, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Fixed(t.gameObject, -1, 52);
        }

        public static void Note(Transform parent, string text)
        {
            Text t = UIKit.Label(parent, text, 24, UIKit.TextDim, TextAnchor.UpperLeft);
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        public static GameObject AddEntry(Transform parent, Entry e)
        {
            Image card = UIKit.Panel(parent, "Entry", e.Tint);
            HorizontalLayoutGroup row = UIKit.HStack(card.gameObject, 14, 14);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childForceExpandHeight = false;
            row.childControlHeight = true;

            if (e.Icon != null)
            {
                Image icon = UIKit.Icon(card.transform, e.Icon, 84);
                icon.color = e.IconTint;
            }

            RectTransform col = UIKit.NewRect("Info", card.transform);
            VerticalLayoutGroup v = UIKit.VStack(col.gameObject, 4, 0);
            v.childAlignment = TextAnchor.MiddleLeft;
            UIKit.Flex(col.gameObject, 1f);

            Text title = UIKit.Label(col, e.Title, 31, UIKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (!string.IsNullOrEmpty(e.Subtitle)) UIKit.Label(col, e.Subtitle, 23, UIKit.TextDim, TextAnchor.UpperLeft);
            if (!string.IsNullOrEmpty(e.Info)) UIKit.Label(col, e.Info, 24, UIKit.TextMain, TextAnchor.UpperLeft);
            if (e.Progress >= 0f)
            {
                Slider s = UIKit.MakeBar(col, UIKit.Good);
                s.value = Mathf.Clamp01(e.Progress);
                UIKit.Fixed(s.gameObject, -1, 14);
            }
            title.horizontalOverflow = HorizontalWrapMode.Wrap;

            if (!string.IsNullOrEmpty(e.Button) || !string.IsNullOrEmpty(e.Button2))
            {
                RectTransform btns = UIKit.NewRect("Buttons", card.transform);
                VerticalLayoutGroup bv = UIKit.VStack(btns.gameObject, 8, 0);
                bv.childAlignment = TextAnchor.MiddleCenter;
                UIKit.Fixed(btns.gameObject, 250, -1);
                if (!string.IsNullOrEmpty(e.Button))
                {
                    Button b = UIKit.MakeButton(btns, e.Button, e.ButtonEnabled ? e.ButtonColor : UIKit.Disabled, e.OnClick, 25);
                    b.interactable = e.ButtonEnabled;
                    UIKit.Fixed(b.gameObject, 250, 78);
                }
                if (!string.IsNullOrEmpty(e.Button2))
                {
                    Button b2 = UIKit.MakeButton(btns, e.Button2, e.Button2Color, e.OnClick2, 23);
                    UIKit.Fixed(b2.gameObject, 250, 60);
                }
            }
            return card.gameObject;
        }

        public static Button TabButton(Transform parent, string label, bool selected, Action onClick)
        {
            Button b = UIKit.MakeButton(parent, label, selected ? UIKit.Accent : UIKit.CardBgLight, onClick, 23);
            UIKit.Flex(b.gameObject, 1f);
            UIKit.Fixed(b.gameObject, -1, 62);
            return b;
        }

        public static RectTransform TabRow(Transform parent)
        {
            RectTransform row = UIKit.NewRect("Tabs", parent);
            HorizontalLayoutGroup h = UIKit.HStack(row.gameObject, 8, 0);
            h.childForceExpandWidth = true;
            UIKit.Fixed(row.gameObject, -1, 66);
            return row;
        }
    }
}
