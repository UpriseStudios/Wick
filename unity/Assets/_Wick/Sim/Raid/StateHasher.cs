namespace Wick.Sim
{
    /// <summary>
    /// Folds raid state into the running FNV-1a hash. The field order below is part of the
    /// sim rules: changing it changes every hash, so bump SimInfo.Version and update goldens.
    /// </summary>
    public static class StateHasher
    {
        public static void Fold(ref Fnv1a64 h, RaidState s)
        {
            h.AddInt(s.Tick);
            h.AddByte((byte)s.Outcome);

            WickState w = s.Wick;
            AddPos(ref h, w.Tile);
            h.AddInt(w.Progress);
            h.AddByte((byte)w.Facing);
            h.AddInt(w.Flickers);
            h.AddBool(w.Tiptoe);
            h.AddInt(w.CarriedTreasure);
            h.AddBool(w.DecoyUsed);
            h.AddBool(w.Moving);
            h.AddByte((byte)w.MoveDir);
            h.AddBool(w.MoveIsRun);
            h.AddBool(w.Running);
            h.AddByte((byte)w.RunDir);
            h.AddBool(w.HasQueued);
            h.AddByte((byte)w.QueuedDir);
            h.AddBool(w.LeftEntrance);
            AddPos(ref h, w.DecoyTile);
            h.AddInt(w.DecoyTicksLeft);
            h.AddInt(w.InvulnTicks);
            h.AddInt(w.CarriedPiles);
            h.AddInt(w.GustDimTicks);
            h.AddBool(w.ReachedHoard);
            h.AddInt(w.KnockTiles);
            h.AddBool(w.Sliding);

            // Entities, each in id order. Timed traps are a pure function of the tick, so only
            // their spent flags (bells) carry state.
            h.AddInt(s.Spent.Length);
            for (int i = 0; i < s.Spent.Length; i++)
            {
                h.AddBool(s.Spent[i]);
            }

            h.AddInt(s.Guardians.Length);
            for (int i = 0; i < s.Guardians.Length; i++)
            {
                GuardianState g = s.Guardians[i];
                h.AddInt(g.ObjectIndex);
                h.AddByte((byte)g.Kind);
                AddPos(ref h, g.Tile);
                h.AddInt(g.Progress);
                h.AddBool(g.Moving);
                h.AddByte((byte)g.MoveDir);
                h.AddByte((byte)g.Facing);
                h.AddByte((byte)g.Mode);
                h.AddInt(g.Waypoint);
                AddPos(ref h, g.Target);
                h.AddInt(g.LostTicks);
                h.AddInt(g.ContactTicks);
            }

            h.AddLong(s.Rng.State);
            h.AddLong(s.Rng.Inc);
        }

        private static void AddPos(ref Fnv1a64 h, TilePos p)
        {
            h.AddInt(p.X);
            h.AddInt(p.Y);
        }
    }
}
