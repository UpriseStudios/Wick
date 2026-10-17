namespace Wick.Sim
{
    // Step 6: pickups, in entity-id order, then the hoard check.
    public sealed partial class RaidSim
    {
        private void ResolvePickups()
        {
            ref WickState w = ref State.Wick;
            if (w.Flickers <= 0)
            {
                return;
            }

            TilePos at = w.Occupied;
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (State.Spent[i])
                {
                    continue;
                }

                if (o.Type == ObjectType.Treasure && o.Pos == at)
                {
                    State.Spent[i] = true;
                    w.CarriedTreasure += o.Value;
                    w.CarriedPiles++;
                    Emit(SimEventType.TreasureTaken, o.Id, o.Pos, o.Value);
                }
                else if (o.Type == ObjectType.Brazier && w.Flickers < SimTuning.StartFlickers && IsOrthogonallyAdjacent(at, o.Pos))
                {
                    State.Spent[i] = true;
                    w.Flickers++;
                    Emit(SimEventType.BrazierUsed, o.Id, o.Pos, w.Flickers);
                }
            }

            if (_vault.HasHoard && _vault.Hoard.Contains(at))
            {
                w.ReachedHoard = true;
            }
        }

        private static bool IsOrthogonallyAdjacent(TilePos a, TilePos b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return dx * dx + dy * dy == 1;
        }

        /// <summary>Wick's speed this tick: base, then treasure weight, then tiptoe.</summary>
        public static int WickSpeed(in WickState w)
        {
            int speed = w.MoveIsRun ? SimTuning.RunSpeed : SimTuning.DashSpeed;
            int slow = w.CarriedPiles * SimTuning.SlowPercentPerPile;
            if (slow > SimTuning.MaxSlowPercent)
            {
                slow = SimTuning.MaxSlowPercent;
            }

            speed = speed * (100 - slow) / 100;
            if (w.Tiptoe)
            {
                speed /= SimTuning.TiptoeSpeedDivisor;
            }

            return speed;
        }
    }
}
