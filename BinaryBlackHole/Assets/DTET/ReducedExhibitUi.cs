using System;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // UI elements rendered by the desktop inspection camera.
    internal static class ReducedExhibitUi
    {
        public static readonly Color Ink = new Color(.025f, .04f, .065f, .95f);
        public static readonly Color Accent = new Color(.4f, .86f, .92f);
        public static readonly Color Muted = new Color(.66f, .73f, .81f);

        public static GameObject Canvas(Transform parent, Camera camera, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false); go.layer = 5;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .1f;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return go;
        }
        public static GameObject Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false); go.layer = 5; Place(go, x, y, w, h);
            go.GetComponent<Image>().color = color; return go;
        }
        public static Text Label(Transform parent, string text, int size, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false); go.layer = 5; Place(go, x, y, w, h);
            var label = go.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = size; label.color = color; label.raycastTarget = false;
            label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        public static Button Button(Transform parent, string text, float x, float y, float w, float h, Action action)
        {
            var go = Panel(parent, text, x, y, w, h, new Color(.075f, .13f, .2f));
            var button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(.65f,.9f,1); colors.pressedColor = new Color(.4f,.65f,.85f); button.colors = colors;
            var label = Label(go.transform, text, 15, 8, 0, w-16, h, Accent); label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => action()); return button;
        }
        public static Slider Slider(Transform parent, string name, float x, float y, float w, Action<float> change)
        {
            var go = Panel(parent, name, x, y, w, 14, new Color(.13f,.19f,.27f));
            var slider = go.AddComponent<Slider>();
            var fill = Panel(go.transform,"Fill",0,0,w,14,Accent).GetComponent<RectTransform>();
            var handle = Panel(go.transform,"Handle",0,-3,13,20,Color.white).GetComponent<RectTransform>();
            fill.sizeDelta = Vector2.zero; fill.anchoredPosition = Vector2.zero; fill.pivot = new Vector2(.5f,.5f);
            handle.sizeDelta = new Vector2(13,6); handle.anchoredPosition = Vector2.zero; handle.pivot = new Vector2(.5f,.5f);
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0; slider.maxValue = 1; slider.onValueChanged.AddListener(v => change(v)); return slider;
        }
        public static RawImage Image(Transform parent, string name, Texture texture, float x, float y, float w, float h)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            go.transform.SetParent(parent,false); go.layer=5; Place(go,x,y,w,h);
            var image=go.GetComponent<RawImage>(); image.texture=texture; image.raycastTarget=false; return image;
        }
        public static void Place(GameObject go, float x, float y, float w, float h)
        {
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h);
        }
    }
}
