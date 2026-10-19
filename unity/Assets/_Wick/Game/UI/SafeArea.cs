using UnityEngine;
using UnityEngine.UIElements;

namespace Wick.Game
{
    /// <summary>
    /// Keeps every full-screen layer of a UI clear of notches, punch-hole cameras and gesture bars:
    /// each direct child of the root is inset by Screen.safeArea, converted to panel units, and
    /// re-applied whenever the screen or its safe area changes (rotation, split screen, foldables).
    /// </summary>
    public sealed class SafeArea
    {
        private readonly VisualElement _root;
        private Rect _applied;
        private Vector2Int _screen;

        private SafeArea(VisualElement root)
        {
            _root = root;
            root.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            root.schedule.Execute(Apply).Every(500); // the safe area can change without a layout change
        }

        public static SafeArea Attach(VisualElement root)
        {
            var s = new SafeArea(root);
            s.Apply();
            return s;
        }

        /// <summary>Insets in panel units (left, top, right, bottom) for a safe area on a screen of the given size.</summary>
        public static Vector4 Insets(Rect safe, Vector2 screen, Vector2 panelSize)
        {
            if (screen.x <= 0 || screen.y <= 0)
            {
                return Vector4.zero;
            }

            float sx = panelSize.x / screen.x, sy = panelSize.y / screen.y;
            return new Vector4(safe.xMin * sx, (screen.y - safe.yMax) * sy, (screen.x - safe.xMax) * sx, safe.yMin * sy);
        }

        private void Apply()
        {
            if (_root.panel == null)
            {
                return;
            }

            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            Vector2 size = _root.layout.size;
            if (safe == _applied && screen == _screen || float.IsNaN(size.x) || size.x <= 0f)
            {
                return;
            }

            _applied = safe;
            _screen = screen;
            Vector4 inset = Insets(safe, screen, size);
            foreach (VisualElement layer in _root.Children())
            {
                if (layer.resolvedStyle.position != Position.Absolute)
                {
                    continue;
                }

                layer.style.left = inset.x;
                layer.style.top = inset.y;
                layer.style.right = inset.z;
                layer.style.bottom = inset.w;
            }
        }
    }
}
