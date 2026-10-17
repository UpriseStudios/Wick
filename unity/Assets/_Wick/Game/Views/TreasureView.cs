using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>A treasure pile: bobs while waiting, gone once taken.</summary>
    public sealed class TreasureView : EntityView
    {
        [SerializeField] private Transform _pile;

        public override void Sync()
        {
            SetVisible(!Spent && IsVisible(Def.Pos));
            if (_pile != null)
            {
                _pile.localPosition = new Vector3(0f, 0.25f + Mathf.Sin(Time.time * 3f) * 0.05f, 0f);
            }
        }
    }
}
