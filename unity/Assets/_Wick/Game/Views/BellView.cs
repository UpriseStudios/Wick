using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Snuffer Bell: a pressure plate with a bell above; touched, it shakes, then drops (once). Its
    /// plate's red warning glow stays readable in the dark while the bell falls.
    /// </summary>
    public sealed class BellView : EntityView
    {
        [SerializeField] private Transform _bell;
        [SerializeField] private float _hangHeight = 2.2f;
        [SerializeField] private float _droppedHeight = 0.35f;

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            if (_bell != null)
            {
                Vector3 p = _bell.localPosition;
                float target = Spent ? _droppedHeight : _hangHeight;
                p.y = Mathf.MoveTowards(p.y, target, Time.deltaTime * 20f);

                // Falling (the plate was touched): it shakes until it lands BellDropTicks later.
                bool falling = !Spent && Ctx.Runner.Sim.State.BellFall[ObjectIndex] > 0;
                p.x = falling ? Mathf.Sin(Time.time * 70f) * 0.06f : 0f;
                _bell.localPosition = p;
            }

            // The plate glows red while the bell is coming down: a warning stays readable in the dark.
            ReadableGlow warning = Glow(ReadableKind.TrapWarning);
            if (warning != null)
            {
                warning.Active = !Spent && Ctx.Runner.Sim.State.BellFall[ObjectIndex] > 0 && Mathf.Repeat(Time.time * 8f, 1f) < 0.6f;
            }
        }
    }
}
