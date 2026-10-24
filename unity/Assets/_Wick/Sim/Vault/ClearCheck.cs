using System;

namespace Wick.Sim
{
    /// <summary>
    /// Proof that the builder escaped their own vault with at least 1 star (the publish rule):
    /// the run's seed, input log and resulting hash, tied to the exact layout it was played on.
    /// The server re-simulates it before a vault goes live; the client does the same before a
    /// local publish.
    /// </summary>
    public sealed class ClearCheck
    {
        public ClearCheck(int seed, InputLog log, string hash, int stars, int simVersion, string layoutHash)
            : this(seed, log, hash, stars, simVersion, layoutHash, RaidParams.Default)
        {
        }

        public ClearCheck(int seed, InputLog log, string hash, int stars, int simVersion, string layoutHash, RaidParams raidParams)
        {
            Params = raidParams;
            Seed = seed;
            Log = log ?? throw new ArgumentNullException(nameof(log));
            Hash = hash;
            Stars = stars;
            SimVersion = simVersion;
            LayoutHash = layoutHash;
        }

        public int Seed { get; }
        public InputLog Log { get; }

        /// <summary>Final state hash of the run, 16 lowercase hex digits.</summary>
        public string Hash { get; }

        public int Stars { get; }
        public int SimVersion { get; }

        /// <summary>LayoutHashOf the vault it was played on; any edit makes it stale.</summary>
        public string LayoutHash { get; }

        /// <summary>The raid settings the run was played with (its raid length).</summary>
        public RaidParams Params { get; }

        public static ClearCheck FromRun(VaultDef vault, int seed, InputLog log, RaidResult result)
        {
            return FromRun(vault, seed, log, result, RaidParams.Default);
        }

        public static ClearCheck FromRun(VaultDef vault, int seed, InputLog log, RaidResult result, RaidParams raidParams)
        {
            return new ClearCheck(seed, log, result.HashHex, result.Stars, SimInfo.Version, LayoutHashOf(vault), raidParams);
        }

        /// <summary>Whether this clear-check was made on this exact layout (cheap; no re-sim).</summary>
        public bool MatchesLayout(VaultDef vault)
        {
            return LayoutHash == LayoutHashOf(vault);
        }

        /// <summary>
        /// Re-simulates the stored run on the vault. Passes only if the sim version and layout
        /// match, the run escapes with at least 1 star, and it reproduces the stored hash and stars.
        /// </summary>
        public static ClearCheckResult Verify(VaultDef vault, ClearCheck cc)
        {
            if (cc == null)
            {
                return ClearCheckResult.Fail("No clear-check run");
            }

            if (cc.SimVersion != SimInfo.Version)
            {
                return ClearCheckResult.Fail("Recorded with sim version " + cc.SimVersion + ", this is " + SimInfo.Version);
            }

            if (!cc.MatchesLayout(vault))
            {
                return ClearCheckResult.Fail("The vault changed after the clear-check run");
            }

            RaidResult replay = RaidRunner.RunToEnd(vault, cc.Seed, cc.Log, cc.Params);
            if (replay.Outcome != Outcome.Escaped)
            {
                return ClearCheckResult.Fail("The run did not escape (" + replay.Outcome + ")", replay);
            }

            if (replay.Stars < 1)
            {
                return ClearCheckResult.Fail("The run escaped with no stars", replay);
            }

            if (replay.HashHex != cc.Hash || replay.Stars != cc.Stars)
            {
                return ClearCheckResult.Fail("Replay gives hash " + replay.HashHex + ", stored " + cc.Hash, replay);
            }

            return new ClearCheckResult(true, "Verified", replay);
        }

        /// <summary>
        /// FNV-1a over everything that affects play: grid, tiles, entrance, hoard, level and every
        /// object. Not the vault id, version, name or theme.
        /// </summary>
        public static string LayoutHashOf(VaultDef vault)
        {
            var h = new Fnv1a64();
            h.AddInt(vault.Width);
            h.AddInt(vault.Height);
            for (int i = 0; i < vault.Width * vault.Height; i++)
            {
                h.AddByte((byte)vault.TileAtIndex(i));
            }

            h.AddInt(vault.Entrance.X);
            h.AddInt(vault.Entrance.Y);
            h.AddBool(vault.HasHoard);
            if (vault.HasHoard)
            {
                h.AddInt(vault.Hoard.MinX);
                h.AddInt(vault.Hoard.MinY);
                h.AddInt(vault.Hoard.MaxX);
                h.AddInt(vault.Hoard.MaxY);
            }

            h.AddInt(vault.VaultLevel);
            h.AddInt(vault.ObjectCount);
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                h.AddInt(o.Id);
                h.AddByte((byte)o.Type);
                h.AddInt(o.Pos.X);
                h.AddInt(o.Pos.Y);
                h.AddInt(o.Rot);
                h.AddInt(o.Cycle);
                h.AddInt(o.Offset);
                h.AddInt(o.Value);
                h.AddInt(o.Patrol.Length);
                foreach (TilePos p in o.Patrol)
                {
                    h.AddInt(p.X);
                    h.AddInt(p.Y);
                }
            }

            return RaidResult.FormatHash(h.Value);
        }

        public void Write(JsonWriter w)
        {
            w.BeginObject()
                .Field("seed", Seed)
                .Field("log", Log.ToBase64())
                .Field("hash", Hash)
                .Field("stars", Stars)
                .Field("simVersion", SimVersion)
                .Field("layoutHash", LayoutHash)
                .Name("params");
            Params.Write(w);
            w.EndObject();
        }

        public static ClearCheck FromNode(JsonNode node)
        {
            return new ClearCheck(
                node.GetInt("seed"),
                InputLog.FromBase64(node.GetString("log")),
                node.GetString("hash"),
                node.GetInt("stars"),
                node.GetInt("simVersion", 0),
                node.GetString("layoutHash", ""),
                RaidParams.FromNode(node.Find("params")));
        }
    }

    public readonly struct ClearCheckResult
    {
        public readonly bool Ok;
        public readonly string Reason;

        /// <summary>The re-simulated run, when it got that far.</summary>
        public readonly RaidResult? Replay;

        public ClearCheckResult(bool ok, string reason, RaidResult? replay = null)
        {
            Ok = ok;
            Reason = reason;
            Replay = replay;
        }

        public static ClearCheckResult Fail(string reason, RaidResult? replay = null)
        {
            return new ClearCheckResult(false, reason, replay);
        }
    }
}
