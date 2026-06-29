using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SlimeRPG
{
    /// <summary>
    /// Pan + zoom for a content RectTransform inside a masked viewport. Pan = drag (mouse or one
    /// finger). Zoom = mouse wheel or two-finger pinch. Put this on the viewport (which needs a
    /// raycast-target Graphic); dragging anywhere — even starting on a hex button — pans, while a
    /// click still buys the hex.
    /// </summary>
    public class PanZoom : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public RectTransform content;
        public float minZoom = 0.45f, maxZoom = 2.2f;

        float _canvasScale = 1f, _lastPinch = 0f;

        void Start()
        {
            var c = GetComponentInParent<Canvas>();
            if (c != null) _canvasScale = Mathf.Max(0.01f, c.scaleFactor);
        }

        public void OnDrag(PointerEventData e)
        {
            if (content != null) content.anchoredPosition += e.delta / _canvasScale;
        }

        public void OnScroll(PointerEventData e)
        {
            ApplyZoom(1f + e.scrollDelta.y * 0.12f);
        }

        void Update()
        {
            var ts = Touchscreen.current;
            if (ts == null) { _lastPinch = 0f; return; }
            int n = 0; Vector2 a = default, b = default;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue();
                n++;
            }
            if (n >= 2)
            {
                float d = Vector2.Distance(a, b);
                if (_lastPinch > 1f && d > 1f) ApplyZoom(d / _lastPinch);
                _lastPinch = d;
            }
            else _lastPinch = 0f;
        }

        void ApplyZoom(float factor)
        {
            if (content == null) return;
            float z = Mathf.Clamp(content.localScale.x * factor, minZoom, maxZoom);
            content.localScale = new Vector3(z, z, 1f);
        }
    }
}
