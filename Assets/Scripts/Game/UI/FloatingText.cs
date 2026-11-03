using UnityEngine;
using UnityEngine.UI;

namespace CloudGarden.UI
{
    /// <summary>Pooled floating label ("+12 Rocío") that rises and fades out.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        public const float Lifetime = 1.0f;

        public Text Label;
        public RectTransform Rect;
        [HideInInspector] public float Age;
        [HideInInspector] public Vector2 Start;

        public static GameObject Create(Transform parent)
        {
            GameObject go = new GameObject("Popup", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            FloatingText f = go.AddComponent<FloatingText>();
            f.Rect = go.GetComponent<RectTransform>();
            f.Label = UIKit.Label(go.transform, "", 36, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            f.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Stretch(f.Label.rectTransform);
            UIKit.Box(f.Rect, new Vector2(0, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 60));
            return go;
        }

        public void Begin(string text, Color color, Vector2 start)
        {
            Label.text = text;
            Label.color = color;
            Age = 0f;
            Start = start;
            Rect.anchoredPosition = start;
        }

        /// <summary>Advances the animation; returns false when finished.</summary>
        public bool Step(float dt)
        {
            Age += dt;
            float t = Age / Lifetime;
            if (t >= 1f) return false;
            Rect.anchoredPosition = Start + new Vector2(0f, t * 90f);
            Color c = Label.color;
            c.a = 1f - t * t;
            Label.color = c;
            return true;
        }
    }
}
