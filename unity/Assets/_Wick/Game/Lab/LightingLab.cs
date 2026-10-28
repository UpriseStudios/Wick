using System;
using System.Collections;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The lighting lab (docs/m10-2-lighting.md, M10.2c): a Classic Treasury test vault with one of
    /// every trap, guardian, prop family, treasure, brazier, pit, ice and the entrance, and Wick on a
    /// scripted walk along its gallery (east to the hoard, grabbing gold, and back out), looping. As he
    /// goes, Lit, Remembered and Unseen form around him. The guardians sit in rooms behind closed gates
    /// whose levers he never reaches, so they wake and give chase but can't get to him; the Mimic
    /// Chest sits two tiles off his path. A row of white, mid-grey and dark test props shows how the
    /// light reads on plain values. Dev builds only (dev menu), and the editor-only LightingLab scene.
    /// </summary>
    public sealed class LightingLab : MonoBehaviour
    {
        public const int Seed = 7;

        /// <summary>The gallery Wick walks along, and where it ends.</summary>
        public static readonly TilePos FarEnd = new TilePos(19, 6);

        // The north hall holds the traps and props, the gallery (row 6) is the walk, and the south rooms
        // hold the guardians behind gated doors.
        private static readonly string[] Map =
        {
            "#####################",
            "#...................#",
            "#...................#",
            "#...................#",
            "#...................#",
            "#...................#",
            "#E..................#",
            "###D####D####D###D###",
            "#.....#....#....#...#",
            "#.....#....#....#...#",
            "#.....#....#....#...#",
            "#.....#....#....#...#",
            "#####################",
        };

        /// <summary>The grey value props: white, mid-grey and dark, on these hall tiles.</summary>
        public static readonly (TilePos tile, float value)[] GreyProps =
        {
            (new TilePos(16, 3), 0.9f), (new TilePos(17, 3), 0.5f), (new TilePos(18, 3), 0.15f),
        };

        private RaidRunner _runner;
        private VaultView _view;
        private VaultDef _vault;
        private InputLog _walk;
        private Coroutine _restart;

        public bool Running { get; private set; }

        public VaultDef Vault => _vault ??= Build();

        public InputLog Walk => _walk ??= ScriptWalk(Vault);

        /// <summary>The lab vault. Built for the sim to run, not to be published (it breaks the building rules on purpose).</summary>
        public static VaultDef Build()
        {
            int w = Map[0].Length;
            int h = Map.Length;
            var tiles = new TileType[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    char c = Map[y][x];
                    tiles[y * w + x] = c == '#' ? TileType.Wall : c == 'D' ? TileType.Door : TileType.Floor;
                }
            }

            tiles[2 * w + 6] = TileType.Pit;
            tiles[6 * w + 10] = TileType.Ice;
            tiles[6 * w + 11] = TileType.Ice;

            int id = 1;
            var none = new TilePos[0];
            ObjectDef Obj(ObjectType type, int x, int y, int rot = 0, TilePos[] patrol = null, int[] links = null)
            {
                int cycle = ObjectTypes.IsTimedTrap(type) ? ObjectTypes.MinCycle(type) + 45 : 0;
                int value = type == ObjectType.Treasure ? 100 : 0;
                return new ObjectDef(id++, type, new TilePos(x, y), rot, cycle, 0, value, patrol ?? none, links);
            }

            var objects = new System.Collections.Generic.List<ObjectDef>
            {
                Obj(ObjectType.Brazier, 2, 0),
                Obj(ObjectType.DripPipe, 3, 3),
                Obj(ObjectType.SpikeFloor, 5, 3),
                Obj(ObjectType.Bellows, 8, 0, rot: (int)Dir.S),
                Obj(ObjectType.Crate, 8, 3), // stops the bellows' gust short of the gallery
                Obj(ObjectType.SnufferBell, 10, 3),
                Obj(ObjectType.MimicChest, 12, 4),
                Obj(ObjectType.SwingAxe, 14, 3, rot: (int)Dir.E),
                Obj(ObjectType.Fan, 16, 1, rot: (int)Dir.N),
                Obj(ObjectType.Treasure, 17, 6),
                Obj(ObjectType.Treasure, 18, 6),
                Obj(ObjectType.Treasure, 19, 2), // out of reach: its twinkle stays in the dark
            };

            // Guardians behind gates: each gate's only lever is inside its own room.
            void Room(ObjectType guardian, int gx, int gy, int doorX, int leverX, TilePos[] patrol = null)
            {
                objects.Add(Obj(guardian, gx, gy, rot: (int)Dir.N, patrol: patrol));
                ObjectDef gate = Obj(ObjectType.Gate, doorX, 7);
                objects.Add(gate);
                objects.Add(Obj(ObjectType.Lever, leverX, 11, links: new[] { gate.Id }));
            }

            // Each just inside its gate, close enough to Wick's light to wake as he passes.
            Room(ObjectType.NightOwl, 3, 8, 3, 1, new[] { new TilePos(3, 8), new TilePos(4, 8) });
            Room(ObjectType.WaxMoths, 8, 9, 8, 10);
            Room(ObjectType.SleepyTroll, 13, 8, 13, 15);
            Room(ObjectType.SirWhiskers, 17, 8, 17, 19);

            var decorations = new[]
            {
                new DecorationDef(id++, Decorations.Banner, new TilePos(5, 0), 0),
                new DecorationDef(id++, Decorations.Torch, new TilePos(11, 0), 0),
                new DecorationDef(id++, Decorations.GoldPile, new TilePos(4, 5), 0),
                new DecorationDef(id++, Decorations.Statue, new TilePos(15, 4), 0),
            };

            return new VaultDef(1, "v_lighting_lab", 1, VaultRules.MaxVaultLevel, w, h, tiles, new TilePos(1, 6), objects.ToArray(),
                true, new TileRect(16, 5, 19, 6), ThemeCatalog.DefaultId, null, decorations);
        }

        /// <summary>
        /// Wick's walk: run east along the gallery to the far end (grabbing the gold on the way), then
        /// back west out of the entrance. Recorded from a live run of the sim, so it's a real raid.
        /// </summary>
        public static InputLog ScriptWalk(VaultDef vault)
        {
            var sim = new RaidSim(vault, Seed, InputLog.Empty);
            bool outbound = true;
            sim.Submit(Command.RunStart(0, Dir.E));
            while (!sim.IsOver && sim.State.Tick < RaidParams.Default.RaidTicks)
            {
                WickState w = sim.State.Wick;
                if (outbound && w.Tile == FarEnd && !w.Moving)
                {
                    // A run into the far wall stays a run: turn it round explicitly.
                    outbound = false;
                    sim.Submit(Command.RunStop(sim.State.Tick));
                    sim.Submit(Command.RunStart(sim.State.Tick, Dir.W));
                }
                else if (!w.Moving && !w.Running && !w.Falling)
                {
                    sim.Submit(Command.RunStart(sim.State.Tick, outbound ? Dir.E : Dir.W)); // e.g. after an ice slide ends a run
                }

                sim.Step();
            }

            return sim.RecordedLog;
        }

        /// <summary>Starts the lab in the raid scene: the walk replays on a loop until Stop.</summary>
        public void Begin(RaidRunner runner, VaultView view)
        {
            _runner = runner;
            _view = view;
            if (!Running)
            {
                Running = true;
                _view.Built += SpawnProps;
                _runner.Ended += OnEnded;
            }

            Play();
        }

        public void Stop()
        {
            if (!Running)
            {
                return;
            }

            Running = false;
            _view.Built -= SpawnProps;
            _runner.Ended -= OnEnded;
            if (_restart != null)
            {
                StopCoroutine(_restart);
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        /// <summary>Loads the walk from its start.</summary>
        public void Play()
        {
            _runner.LoadReplay(Vault, Seed, Walk);
        }

        private void OnEnded(RaidRunner runner, RaidResult result)
        {
            if (Running && isActiveAndEnabled)
            {
                _restart = StartCoroutine(RestartSoon());
            }
        }

        private IEnumerator RestartSoon()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (Running && _runner.Vault == Vault)
            {
                Play();
            }
        }

        /// <summary>The grey value props, rebuilt with the vault each time it loads.</summary>
        public void SpawnProps(VaultView view)
        {
            if (view.Context == null || view.Context.Vault != Vault)
            {
                return;
            }

            Transform vaultRoot = view.transform.Find("Vault");
            Shader shader = Shader.Find("Wick/StylisedLit");
            foreach ((TilePos tile, float value) in GreyProps)
            {
                GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prop.name = "Grey " + value.ToString("0.00");
                DestroyImmediate(prop.GetComponent<Collider>()); // also runs in edit mode (sign-off captures)
                prop.transform.SetParent(vaultRoot, false);
                prop.transform.position = SimPose.TileCenter(tile) + Vector3.up * 0.35f;
                prop.transform.localScale = new Vector3(0.6f, 0.7f, 0.6f);
                var material = new Material(shader) { name = prop.name };
                material.SetColor("_BaseColor", new Color(value, value, value));
                prop.GetComponent<Renderer>().sharedMaterial = material;
            }
        }
    }
}
