using UnityEngine;
using UnityEngine.UIElements;

namespace Wick.Game
{
    /// <summary>
    /// Every button on a panel squashes when pressed and springs back with a little overshoot,
    /// and a click plays the UI tap and a light haptic. Registered once on the panel root, so new
    /// buttons (sheets, lists) get it for free.
    /// </summary>
    public static class UiFeedback
    {
        private const float Pressed = 0.92f;

        public static void Register(VisualElement root)
        {
            root.RegisterCallback<PointerDownEvent>(e => Squash(ButtonOf(e.target)), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(e => Release(ButtonOf(e.target)), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(e => Release(ButtonOf(e.target)), TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(e =>
            {
                Button b = ButtonOf(e.target);
                if (b != null && b.enabledInHierarchy)
                {
                    AudioDirector.Tap();
                    Haptics.Play(HapticStrength.Light);
                }
            }, TrickleDown.TrickleDown);
        }

        private static Button ButtonOf(IEventHandler target)
        {
            for (var e = target as VisualElement; e != null; e = e.parent)
            {
                if (e is Button b)
                {
                    return b;
                }
            }

            return null;
        }

        private static void Squash(Button b)
        {
            if (b != null && b.enabledInHierarchy)
            {
                b.style.scale = new Scale(new Vector3(Pressed, Pressed, 1f));
            }
        }

        private static void Release(Button b)
        {
            if (b == null || b.style.scale.value.value.x >= 1f)
            {
                return;
            }

            // Spring back past full size, then settle.
            b.style.scale = new Scale(new Vector3(1.05f, 1.05f, 1f));
            b.schedule.Execute(() => b.style.scale = new Scale(Vector3.one)).StartingIn(90);
        }
    }
}
