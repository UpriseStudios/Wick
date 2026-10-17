using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>Snuffer Bell: a pressure plate with a bell hanging above that drops once.</summary>
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
                _bell.localPosition = p;
            }
        }
    }
}
