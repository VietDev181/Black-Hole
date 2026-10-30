using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Factory helpers: every visual element is an Image/Text driven by the Black Hole UI shaders.</summary>
public static class UIKit
{
    private static BlackHoleCatalog cat;
    private static Font font;

    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    public static readonly Color Lavender = new Color(0.82f, 0.78f, 1f);

    public static void Init(BlackHoleCatalog catalog)
    {
        cat = catalog;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        Color top, Color bottom, float radius, Color border, float borderW = 0f, float shine = 0f, int shape = 0, bool raycast = false)
    {
        var rt = Rect(name, parent, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        var m = new Material(cat.uiPanelShader);
        m.SetColor("_ColTop", top);
        m.SetColor("_ColBot", bottom);
        m.SetColor("_Border", border);
        m.SetFloat("_BorderW", borderW);
        m.SetFloat("_Radius", radius);
        m.SetVector("_Size", new Vector4(size.x, size.y, 0f, 0f));
        m.SetFloat("_Shine", shine);
        m.SetFloat("_Shape", shape);
        img.material = m;
        img.color = Color.white;
        img.raycastTarget = raycast;
        return img;
    }

    public static Text Label(Transform parent, string name, string text, int fontSize, Vector2 anchor, Vector2 pos, Vector2 size,
        Color top, Color bottom, TextAnchor align = TextAnchor.MiddleCenter, float shine = 0f, bool outline = true)
    {
        var rt = Rect(name, parent, anchor, pos, size);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontStyle = FontStyle.Bold;
        t.fontSize = fontSize;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
        t.raycastTarget = false;
        t.color = Color.white;

        var m = new Material(cat.uiTextShader);
        m.SetColor("_ColTop", top);
        m.SetColor("_ColBot", bottom);
        m.SetFloat("_TextH", fontSize * 1.2f);
        m.SetFloat("_TextW", Mathf.Max(size.x, 60f));
        m.SetFloat("_Shine", shine);
        t.material = m;

        if (outline)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.0f, 0.18f, 0.85f);
            o.effectDistance = new Vector2(2.5f, -2.5f);
        }
        return t;
    }

    public static Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size,
        Color top, Color bottom, Color border, int fontSize, Action onClick)
    {
        var img = Panel(parent, name, anchor, pos, size, top, bottom, size.y * 0.32f, border, 3f, 1f, 0, true);
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(() => onClick?.Invoke());
        img.gameObject.AddComponent<UIButtonFX>();
        if (!string.IsNullOrEmpty(label))
            Label(img.transform, "Label", label, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size, Color.white, Lavender, TextAnchor.MiddleCenter, 0f);
        return b;
    }
}

/// <summary>Squash-on-press feedback for buttons. Uses unscaled time so it works while paused.</summary>
public class UIButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private bool down;
    private float scale = 1f;

    public void OnPointerDown(PointerEventData e) => down = true;
    public void OnPointerUp(PointerEventData e) => down = false;
    public void OnPointerExit(PointerEventData e) => down = false;

    private void OnDisable()
    {
        down = false;
        scale = 1f;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        float target = down ? 0.93f : 1f;
        scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        if (Mathf.Abs(scale - target) > 0.0005f || Mathf.Abs(transform.localScale.x - scale) > 0.0005f)
            transform.localScale = Vector3.one * scale;
    }
}
