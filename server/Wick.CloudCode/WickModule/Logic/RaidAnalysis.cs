using System;
using System.Collections.Generic;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// Re-simulates a raid tick by tick (the same RaidSim the client and RaidRunner use, so the
    /// result and hash are identical) and keeps what the server needs besides the result: what
    /// snuffed Wick, and how each input lined up with the traps around him.
    /// </summary>
    public sealed class RaidAnalysis
    {
        public RaidResult Result { get; private set; }

        /// <summary>Object type that took Wick's last flicker ("DripPipe", ...), or null if he wasn't snuffed.</summary>
        public string SnuffedBy { get; private set; }

        /// <summary>Ticks of every input, in order.</summary>
        public List<int> InputTicks { get; } = new List<int>();

        /// <summary>Moves onto a timed trap's tile: the moments a player has to wait for a trap.</summary>
        public int InputsIntoTraps { get; private set; }

        /// <summary>Of those, moves on the tick that trap turned safe, or the tick after.</summary>
        public int InputsOnSafeFrame { get; private set; }

        public static RaidAnalysis Run(VaultDef vault, int seed, InputLog log, RaidParams raidParams)
        {
            var a = new RaidAnalysis();
            var sim = new RaidSim(vault, seed, log, raidParams);
            int next = 0;
            int lastHit = 0;
            int snuffer = 0;

            while (!sim.IsOver)
            {
                int tick = sim.State.Tick;
                TilePos at = sim.State.Wick.Occupied;
                while (next < log.Count && log[next].Tick == tick)
                {
                    a.Observe(vault, tick, at, log[next]);
                    next++;
                }

                sim.Step();
                foreach (SimEvent e in sim.Events)
                {
                    if (e.Type == SimEventType.FlickerLost)
                    {
                        lastHit = e.EntityId;
                    }
                    else if (e.Type == SimEventType.TrapFired && IsBell(vault, e.EntityId))
                    {
                        lastHit = e.EntityId; // a bell snuffs outright
                    }
                    else if (e.Type == SimEventType.Snuffed)
                    {
                        snuffer = lastHit;
                    }
                }
            }

            a.Result = sim.GetResult();
            a.SnuffedBy = snuffer == 0 ? null : TypeOf(vault, snuffer);
            return a;
        }

        private void Observe(VaultDef vault, int tick, TilePos at, Command command)
        {
            InputTicks.Add(tick);
            if (command.Type != CommandType.Dash && command.Type != CommandType.RunStart)
            {
                return;
            }

            TilePos into = at.Step(command.Dir);
            bool intoTrap = false;
            bool onSafeFrame = false;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (ObjectTypes.IsTimedTrap(o.Type) && o.Type != ObjectType.Bellows && o.Pos == into)
                {
                    intoTrap = true;
                    onSafeFrame |= TurnedSafe(vault, o, tick) || TurnedSafe(vault, o, tick - 1);
                }
            }

            if (intoTrap)
            {
                InputsIntoTraps++;
                InputsOnSafeFrame += onSafeFrame ? 1 : 0;
            }
        }

        /// <summary>Was the trap active on the tick before t and safe on t?</summary>
        private static bool TurnedSafe(VaultDef vault, ObjectDef trap, int t)
        {
            int level = vault.LevelOf(trap.Type);
            return t > 0 && RaidSim.IsTrapActive(t - 1, trap, level) && !RaidSim.IsTrapActive(t, trap, level);
        }

        private static bool IsBell(VaultDef vault, int id)
        {
            return TypeOf(vault, id) == ObjectType.SnufferBell.ToString();
        }

        private static string TypeOf(VaultDef vault, int id)
        {
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                if (vault.GetObject(i).Id == id)
                {
                    return vault.GetObject(i).Type.ToString();
                }
            }

            return null;
        }
    }

    /// <summary>
    /// A 0-100 score for how machine-like a raid looks. It only routes accounts to the review
    /// pool; it never bans and never changes a payout. Signals: input timing regularity, moves onto
    /// traps on the exact frame they turn safe, perfect clears of hard vaults (a win rate the vault's
    /// snuff rate says is unlikely), and clears much faster than the owner's own clear-check.
    /// </summary>
    public static class AnomalyScore
    {
        public static int Score(RaidAnalysis a, RaidToken token)
        {
            int score = Regularity(a.InputTicks) + Precision(a.InputsIntoTraps, a.InputsOnSafeFrame);
            RaidResult r = a.Result;
            if (token.Difficulty >= 60 && r.Stars == 3)
            {
                score += 10;
            }

            if (r.Outcome == Outcome.Escaped && token.ClearTicks > 0 && r.Ticks * 2 < token.ClearTicks)
            {
                score += 10;
            }

            return Math.Min(100, score);
        }

        /// <summary>
        /// A solver sends its next input the moment a move allows, so its gaps between inputs take
        /// one or two exact values (Wick slows once he carries treasure); people's gaps wander by a
        /// few frames. Scores the share of gaps that equal one of the two commonest.
        /// </summary>
        public static int Regularity(List<int> ticks)
        {
            var counts = new Dictionary<int, int>();
            int gaps = 0;
            for (int i = 1; i < ticks.Count; i++)
            {
                int gap = ticks[i] - ticks[i - 1];
                if (gap > 0)
                {
                    counts.TryGetValue(gap, out int n);
                    counts[gap] = n + 1;
                    gaps++;
                }
            }

            if (gaps < 6)
            {
                return 0;
            }

            int first = 0;
            int second = 0;
            foreach (int n in counts.Values)
            {
                if (n > first)
                {
                    second = first;
                    first = n;
                }
                else if (n > second)
                {
                    second = n;
                }
            }

            int percent = (first + second) * 100 / gaps;
            return percent >= 70 ? 40 : percent >= 50 ? 20 : 0;
        }

        /// <summary>People step onto a trap a few frames after it goes quiet; a solver moves on the frame.</summary>
        public static int Precision(int intoTraps, int onSafeFrame)
        {
            if (intoTraps < 3)
            {
                return 0;
            }

            int percent = onSafeFrame * 100 / intoTraps;
            return percent >= 75 ? 40 : percent >= 50 ? 20 : 0;
        }

        /// <summary>Adds a raid's score to the profile; enough high scores move the account to review.</summary>
        public static void Record(Profile profile, int score)
        {
            profile.RecentScores.Add(score);
            while (profile.RecentScores.Count > Rules.ScoreWindow)
            {
                profile.RecentScores.RemoveAt(0);
            }

            if (profile.RecentScores.Count >= Rules.ScoreMinimumRaids)
            {
                int sum = 0;
                foreach (int s in profile.RecentScores)
                {
                    sum += s;
                }

                if (sum / profile.RecentScores.Count >= Rules.ReviewAverage)
                {
                    profile.Pool = Pools.Review;
                }
            }
        }
    }
}
