using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Sir Whiskers, stand-in poses read from the sim: curled up asleep, crouched while stalking,
    /// a big bottom-wiggle for the 30-tick tell (it must read on its own at gameplay zoom), a lunge
    /// on the pounce, and sitting up while he licks his paw. The cushion stays where it was placed.
    /// Phase 2 art plays the nap / stalk / wiggle / pounce / lick_paw clips here.
    /// </summary>
    public sealed class WhiskersView : EntityView
    {
        [SerializeField] private Transform _body;

        private int _guardian = -1;
        private Vector3 _home;

        protected override void OnInit()
        {
            GuardianState[] all = Ctx.Runner.Sim.State.Guardians;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].ObjectIndex == ObjectIndex)
                {
                    _guardian = i;
                }
            }

            _home = SimPose.TileCenter(Def.Pos);
        }

        public override void Sync()
        {
            if (_guardian < 0 || _body == null)
            {
                return;
            }

            GuardianState prev = Ctx.Previous.Guardians[_guardian];
            GuardianState cur = Ctx.Current.Guardians[_guardian];
            transform.position = _home; // the cushion
            _body.position = Vector3.Lerp(SimPose.Position(prev), SimPose.Position(cur), Ctx.Alpha);
            _body.rotation = Quaternion.Slerp(_body.rotation, SimPose.Facing(cur.Facing), Time.deltaTime * 12f);
            SetVisible(IsVisible(cur.Occupied) || IsVisible(Def.Pos));

            Vector3 scale = Vector3.one;
            float wiggle = 0f;
            switch (cur.Mode == GuardianMode.Chase ? cur.Waypoint : -1)
            {
                case RaidSim.WhiskersWiggling:
                    scale = new Vector3(1f, 0.75f, 1.1f);
                    wiggle = Mathf.Sin(Time.time * 40f) * 18f;
                    break;
                case RaidSim.WhiskersPouncing:
                    scale = new Vector3(0.9f, 0.9f, 1.4f);
                    break;
                case RaidSim.WhiskersLicking:
                    scale = new Vector3(0.9f, 1.25f, 0.9f);
                    break;
                case RaidSim.WhiskersStalking:
                    scale = new Vector3(1f, 0.8f, 1.15f);
                    break;
                default: // asleep or padding home
                    scale = cur.Tile == Def.Pos && !cur.Moving ? new Vector3(1.1f, 0.6f, 1.1f) : Vector3.one;
                    break;
            }

            _body.localScale = scale;
            if (wiggle != 0f)
            {
                _body.rotation *= Quaternion.Euler(0f, wiggle, 0f);
            }
        }
    }
}
