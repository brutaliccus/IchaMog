using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Transmog
{
    internal static class UiUtil
    {
        private static Sprite _circle;
        private static Sprite _rounded;

        public static readonly Color Gold = new Color(0.93f, 0.72f, 0.28f, 1f);
        public static readonly Color Dark = new Color(0.10f, 0.09f, 0.08f, 0.92f);
        public static readonly Color Panel = new Color(0.13f, 0.12f, 0.11f, 0.97f);

        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeSprite(64, 32f, 0));
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = MakeSprite(32, 8f, 10));

        // White anti-aliased rounded rect (radius == half size gives a circle). border > 0 makes it 9-sliceable.
        private static Sprite MakeSprite(int size, float radius, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Abs(x + 0.5f - half), cy = Mathf.Abs(y + 0.5f - half);
                float qx = Mathf.Max(cx - (half - radius), 0f), qy = Mathf.Max(cy - (half - radius), 0f);
                float d = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.HideAndDontSave;
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static void Stretch(RectTransform rt) => Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(RectTransform rt, string text, float size, TMP_FontAsset font, TextAlignmentOptions align)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public static Button Button(RectTransform rt, Image target, UnityAction onClick)
        {
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = target;
            var c = b.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            c.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            c.colorMultiplier = 1f;
            b.colors = c;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        public static TMP_FontAsset GameFont()
        {
            var gui = InventoryGui.instance;
            if (gui != null && gui.m_weight != null && gui.m_weight.font != null) return gui.m_weight.font;
            return null;
        }
    }

    /// <summary>Hover callback that does not swallow scroll/drag events (unlike EventTrigger).</summary>
    internal sealed class HoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action OnEnter;
        public System.Action OnExit;
        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();
    }
}
