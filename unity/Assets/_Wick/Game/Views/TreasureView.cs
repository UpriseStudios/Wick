using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// A treasure pile: bobs while waiting, gone once taken. A soft gold twinkle keeps it findable in
    /// the dark (docs/m10-2-lighting.md), brighter when Wick's light is on it.
    /// </summary>
    public sealed class TreasureView : EntityView
    {
        [SerializeField] private Transform _pile;

        [Tooltip("The twinkle: a small gold glow that flares now and then.")]
        [SerializeField] private Transform _twinkle;

        public override void Sync()
        {
            SetVisible(!Spent && IsVisible(Def.Pos));
            if (_pile != null)
            {
                _pile.localPosition = new Vector3(0f, 0.25f + Mathf.Sin(Time.time * 3f) * 0.05f, 0f);
            }

            ReadableGlow glow = Glow(ReadableKind.Treasure);
            if (glow != null)
            {
                glow.Active = !Spent;
                // An occasional twinkle: piles flare out of step with each other.
                float t = Mathf.Repeat(Time.time * 0.6f + Def.Id * 0.37f, 1f);
                float flare = Mathf.Max(0f, 1f - Mathf.Abs(t - 0.5f) * 8f);
                glow.SetColor(Ctx.Lighting.TreasureGold, (IsVisible(Def.Pos) ? 1.6f : 0.6f) + flare * 2f);
                if (_twinkle != null)
                {
                    _twinkle.localScale = Vector3.one * (0.08f + flare * 0.1f);
                }
            }
        }
    }
}
