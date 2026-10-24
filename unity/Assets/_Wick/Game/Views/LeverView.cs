using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>Lever Doors: the handle is thrown while any gate it works is open.</summary>
    public sealed class LeverView : EntityView
    {
        [SerializeField] private Transform _handle;

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            if (_handle == null)
            {
                return;
            }

            bool open = false;
            foreach (int id in Def.Links)
            {
                int g = Ctx.Vault.IndexOfObject(id);
                open |= g >= 0 && Ctx.Runner.Sim.State.GateOpen[g] > 0;
            }

            Quaternion target = Quaternion.Euler(open ? -40f : 40f, 0f, 0f);
            _handle.localRotation = Quaternion.Slerp(_handle.localRotation, target, Time.deltaTime * 10f);
        }
    }
}
