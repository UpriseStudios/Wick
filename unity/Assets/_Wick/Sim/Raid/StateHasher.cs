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
            h.AddInt(w.Tile.X);
            h.AddInt(w.Tile.Y);
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
            h.AddInt(w.DecoyTile.X);
            h.AddInt(w.DecoyTile.Y);
            h.AddInt(w.DecoyTicksLeft);

            // TODO M2: traps, guardians, treasure and braziers, each sorted by entity id.

            h.AddLong(s.Rng.State);
            h.AddLong(s.Rng.Inc);
        }
    }
}
