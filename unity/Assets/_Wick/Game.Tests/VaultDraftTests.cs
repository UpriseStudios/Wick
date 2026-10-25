using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game.Tests
{
    public class VaultDraftTests
    {
        private static VaultDraft Template(string name)
        {
            TextAsset text = Resources.Load<TextAsset>("VaultTemplates/" + name);
            return VaultDraft.FromVault(VaultJson.Parse(text.text), name);
        }

        private static VaultDraft Halls() => Template("1-three-halls");

        private static List<VaultIssueCode> Codes(VaultDraft d)
        {
            var codes = new List<VaultIssueCode>();
            foreach (VaultIssue i in VaultValidator.Validate(d.ToVaultDef()))
            {
                codes.Add(i.Code);
            }

            return codes;
        }

        // ---- Templates ------------------------------------------------------------------

        [Test]
        public void ThreeTemplates_AreValidVaults()
        {
            TextAsset[] templates = Resources.LoadAll<TextAsset>("VaultTemplates");
            Assert.AreEqual(3, templates.Length);
            foreach (TextAsset t in templates)
            {
                VaultDraft d = VaultDraft.FromVault(VaultJson.Parse(t.text), t.name);
                CollectionAssert.IsEmpty(VaultValidator.Validate(d.ToVaultDef()), t.name);
            }
        }

        // ---- Expanding (decision B in docs/m10-plan.md) ------------------------------------

        [Test]
        public void Expand_DoesNothing_WhileTheGridIsFullSizeForTheLevel()
        {
            VaultDraft d = Halls();
            Assert.IsFalse(d.CanExpand);
            Assert.IsNull(d.Expand());
            Assert.AreEqual(12, d.Width);
        }

        [Test]
        public void Expand_KeepsTheLayoutAgainstTheEntranceEdge_AndAddsWall()
        {
            VaultDraft d = Halls();
            VaultDraft before = d.Clone();
            d.SetLevel(3);
            Assert.IsTrue(d.CanExpand);

            ExpandResult r = d.Expand();
            Assert.AreEqual(14, d.Width);
            Assert.AreEqual(18, d.Height);
            Assert.AreEqual(new TilePos(1, 2), r.Offset, "one column each side, two new rows on top");
            Assert.AreEqual(new TilePos(before.Entrance.X + 1, 17), d.Entrance, "the entrance stays on the bottom edge");
            Assert.AreEqual(new TilePos(5, 4), d.Objects[0].Pos, "the treasure moved with its tile");
            Assert.AreEqual(before.Hoard.MinY + 2, d.Hoard.MinY);

            for (int x = 0; x < d.Width; x++)
            {
                if (!r.Doorway.Contains(new TilePos(x, 1)))
                {
                    Assert.AreEqual(TileType.Wall, d.TileAt(new TilePos(x, 1)), "new space is wall at x " + x);
                }
            }

            Assert.AreEqual(2, r.Doorway.Count);
            Assert.AreEqual(2, r.Doorway[0].Y, "the doorway is in the old top wall");
            Assert.AreEqual(1, r.Doorway[1].Y, "and reaches one tile into the new space");
            foreach (TilePos p in r.Doorway)
            {
                Assert.AreEqual(TileType.Floor, d.TileAt(p));
            }

            Assert.AreEqual(TileType.Floor, d.TileAt(new TilePos(r.Doorway[0].X, 3)), "it opens onto the old room");
            CollectionAssert.AreEqual(new[] { VaultIssueCode.UnderBudget }, Codes(d),
                "the expanded vault is sound; only short of Level 3's minimum build points");
            Assert.IsFalse(d.CanExpand);
        }

        [Test]
        public void Expand_DropsTheClearCheck_AndUndoes()
        {
            VaultDraft d = Halls();
            d.SetLevel(5);
            VaultDraft before = d.Clone();
            d.Expand();
            Assert.IsNull(d.ClearCheck);
            Assert.AreEqual(16, d.Width);
            Assert.AreEqual(12, before.Width, "a clone keeps its own size, so undo restores it");
            Assert.AreEqual(VaultJson.Write(before.ToVaultDef()), VaultJson.Write(VaultDraft.FromJson(before.ToJson()).ToVaultDef()));
        }

        [Test]
        public void ADraftOnAnyTableGrid_RoundTripsItsSize()
        {
            VaultDraft d = Halls();
            d.SetLevel(10);
            d.Expand();
            VaultDraft back = VaultDraft.FromJson(d.ToJson());
            Assert.AreEqual(20, back.Width);
            Assert.AreEqual(28, back.Height);
            Assert.AreEqual(d.Entrance, back.Entrance);
        }

        // ---- Decorations (M10) --------------------------------------------------------------

        [Test]
        public void Decorations_PlaceMoveTurnAndRemove_AsUndoableEdits()
        {
            VaultDraft d = Halls();
            Assert.IsNull(d.DecorationPlaceProblem(Decorations.Statue, new TilePos(2, 6)));
            Assert.IsNotNull(d.DecorationPlaceProblem(Decorations.Statue, new TilePos(0, 6)), "a floor prop on a wall");
            Assert.IsNotNull(d.DecorationPlaceProblem("prop_trophy_raider_bronze", new TilePos(2, 6)), "trophies go in the hoard");

            DraftDecoration statue = d.PlaceDecoration(Decorations.Statue, new TilePos(2, 6));
            Assert.AreNotEqual(d.Objects[0].Id, statue.Id, "ids never clash with objects");
            Assert.IsTrue(d.MoveDecoration(statue.Id, new TilePos(3, 6)));
            Assert.IsTrue(d.RotateDecoration(statue.Id));
            Assert.AreEqual(1, statue.Rot);

            VaultDraft back = VaultDraft.FromJson(d.ToJson());
            Assert.AreEqual(1, back.Decorations.Count);
            Assert.AreEqual(new TilePos(3, 6), back.Decorations[0].Pos);

            Assert.IsTrue(d.Delete(statue.Id));
            Assert.AreEqual(0, d.Decorations.Count);
        }

        [Test]
        public void Decorations_DontNeedANewTestRun()
        {
            VaultDraft d = Halls();
            string before = ClearCheck.LayoutHashOf(d.ToVaultDef());
            d.PlaceDecoration(Decorations.Banner, new TilePos(0, 6));
            Assert.AreEqual(before, ClearCheck.LayoutHashOf(d.ToVaultDef()));
        }

        [Test]
        public void Decorations_MoveWithTheLayoutWhenItExpands()
        {
            VaultDraft d = Halls();
            DraftDecoration statue = d.PlaceDecoration(Decorations.Statue, new TilePos(2, 6));
            d.SetLevel(3);
            ExpandResult r = d.Expand();
            Assert.AreEqual(new TilePos(2 + r.Offset.X, 6 + r.Offset.Y), statue.Pos);
        }

        [Test]
        public void TheDecorationLimit_FollowsVaultLevel()
        {
            VaultDraft d = Halls();
            for (int i = 0; i < VaultRules.MaxDecorations(1); i++)
            {
                d.PlaceDecoration(Decorations.Statue, new TilePos(1 + i, 6));
            }

            Assert.IsNotNull(d.DecorationBlock(Decorations.Statue));
        }

        // ---- Lever Doors (M10) ----------------------------------------------------------------

        [Test]
        public void ALever_LinksToAGate_AndDeletingTheGateUnlinksIt()
        {
            VaultDraft d = Halls();
            d.SetLevel(8);
            DraftObject gate = d.Place(ObjectType.Gate, new TilePos(4, 9));
            DraftObject lever = d.Place(ObjectType.Lever, new TilePos(2, 12));
            Assert.IsFalse(d.ToggleLink(lever.Id, new TilePos(2, 6)), "no gate there");
            Assert.IsTrue(d.ToggleLink(lever.Id, gate.Pos));
            CollectionAssert.AreEqual(new[] { gate.Id }, lever.Links);

            VaultDraft back = VaultDraft.FromJson(d.ToJson());
            CollectionAssert.AreEqual(new[] { gate.Id }, back.Find(lever.Id).Links);

            Assert.IsTrue(d.ToggleLink(lever.Id, gate.Pos), "tapping again unlinks");
            Assert.IsEmpty(lever.Links);
            d.ToggleLink(lever.Id, gate.Pos);
            d.Delete(gate.Id);
            Assert.IsEmpty(lever.Links);
        }

        // ---- Edits ------------------------------------------------------------------------

        [Test]
        public void PaintingAWallUnderTreasure_IsReported()
        {
            VaultDraft d = Halls();
            Assert.IsTrue(d.PaintTile(new TilePos(4, 2), TileType.Wall));
            Assert.IsFalse(d.PaintTile(new TilePos(4, 2), TileType.Wall), "no change the second time");
            Assert.That(Codes(d), Has.Member(VaultIssueCode.ObjectOnWall));
        }

        [Test]
        public void PlacingTraps_SpendsBudget()
        {
            VaultDraft d = Halls();
            for (int i = 0; i < 12; i++)
            {
                d.Place(ObjectType.DripPipe, new TilePos(1 + i % 10, 6 + i / 10));
            }

            Assert.AreEqual(120, d.BuildPoints);
            CollectionAssert.IsEmpty(Codes(d));
            d.Place(ObjectType.DripPipe, new TilePos(3, 7));
            Assert.That(Codes(d), Has.Member(VaultIssueCode.OverBudget));
        }

        [Test]
        public void NewTimedTraps_GetDefaultCycles()
        {
            VaultDraft d = Halls();
            Assert.AreEqual(45, d.Place(ObjectType.DripPipe, new TilePos(2, 6)).Cycle);
            Assert.AreEqual(60, d.Place(ObjectType.SpikeFloor, new TilePos(3, 6)).Cycle);
            Assert.AreEqual(75, d.Place(ObjectType.Bellows, new TilePos(0, 6)).Cycle);
            CollectionAssert.IsEmpty(Codes(d));
        }

        [Test]
        public void Rotate_CyclesThroughFourFacings()
        {
            VaultDraft d = Halls();
            d.SetLevel(2);
            int id = d.Place(ObjectType.NightOwl, new TilePos(2, 6)).Id;
            for (int i = 1; i <= 4; i++)
            {
                d.Rotate(id);
                Assert.AreEqual(i % 4, d.Find(id).Rot);
            }
        }

        [Test]
        public void Bellows_FacesOpenFloor_AndRotatesOnlyThroughOpenSides()
        {
            VaultDraft d = Halls();
            DraftObject west = d.Place(ObjectType.Bellows, new TilePos(0, 6));
            Assert.AreEqual((int)Dir.E, west.Rot, "a west-wall bellows blows east");
            Assert.IsFalse(d.Rotate(west.Id), "east is its only open side");

            // Beside the door at (4,4): north and south are rooms, east is the doorway.
            DraftObject corner = d.Place(ObjectType.Bellows, new TilePos(3, 4));
            Assert.AreEqual((int)Dir.N, corner.Rot);
            d.Rotate(corner.Id);
            Assert.AreEqual((int)Dir.E, corner.Rot);
            d.Rotate(corner.Id);
            Assert.AreEqual((int)Dir.S, corner.Rot);
            d.Rotate(corner.Id);
            Assert.AreEqual((int)Dir.N, corner.Rot, "skips the wall to the west");

            Assert.IsTrue(d.Move(west.Id, new TilePos(11, 6)));
            Assert.AreEqual((int)Dir.W, d.Find(west.Id).Rot, "turns to face the room after a move");
        }

        [Test]
        public void PlaceProblem_RefusesBellowsOnFloor_AndCratesInDoorways()
        {
            VaultDraft d = Halls();
            Assert.IsNotNull(d.PlaceProblem(ObjectType.Bellows, new TilePos(2, 6)));
            Assert.IsNull(d.PlaceProblem(ObjectType.Bellows, new TilePos(0, 6)));
            Assert.AreEqual("Crates cannot block a doorway", d.PlaceProblem(ObjectType.Crate, new TilePos(4, 4)));
        }

        [Test]
        public void Delete_RemovesTheObject()
        {
            VaultDraft d = Halls();
            int id = d.Place(ObjectType.Crate, new TilePos(2, 6)).Id;
            Assert.IsTrue(d.Delete(id));
            Assert.IsNull(d.Find(id));
            Assert.IsFalse(d.Delete(id));
        }

        [Test]
        public void ToggleTreasure_AddsAndRemoves()
        {
            VaultDraft d = Halls();
            Assert.IsTrue(d.ToggleTreasure(new TilePos(2, 2)));
            Assert.AreEqual(2, d.Objects.FindAll(o => o.Type == ObjectType.Treasure).Count);
            Assert.IsTrue(d.ToggleTreasure(new TilePos(2, 2)));
            Assert.AreEqual(1, d.Objects.FindAll(o => o.Type == ObjectType.Treasure).Count);
        }

        [Test]
        public void TreasureValue_FollowsTheLevel()
        {
            VaultDraft d = Halls();
            d.SetLevel(2);
            VaultDef v = d.ToVaultDef();
            Assert.AreEqual(200, v.GetObject(0).Value);
            CollectionAssert.AreEqual(new[] { VaultIssueCode.UnderBudget }, Codes(d), "right value; only short of Level 2's minimum");
        }

        [Test]
        public void Patrol_TapsAddPoints_TappingTheLastRemovesIt()
        {
            VaultDraft d = Halls();
            d.SetLevel(2);
            int owl = d.Place(ObjectType.NightOwl, new TilePos(2, 6)).Id;
            d.AddWaypoint(owl, new TilePos(2, 6));
            d.AddWaypoint(owl, new TilePos(6, 6));
            d.AddWaypoint(owl, new TilePos(6, 8));
            Assert.AreEqual(3, d.Find(owl).Patrol.Count);
            d.AddWaypoint(owl, new TilePos(6, 8));
            Assert.AreEqual(2, d.Find(owl).Patrol.Count);
            Assert.That(Codes(d), Has.No.Member(VaultIssueCode.PatrolBlocked).And.No.Member(VaultIssueCode.PatrolUnreachable));
            Assert.IsTrue(d.ClearPatrol(owl));
            Assert.AreEqual(0, d.Find(owl).Patrol.Count);
        }

        [Test]
        public void SetHoard_NormalisesCorners()
        {
            VaultDraft d = Halls();
            d.SetHoard(new TilePos(6, 3), new TilePos(2, 1));
            Assert.AreEqual(new TilePos(2, 1), new TilePos(d.Hoard.MinX, d.Hoard.MinY));
            Assert.AreEqual(new TilePos(6, 3), new TilePos(d.Hoard.MaxX, d.Hoard.MaxY));
        }

        [Test]
        public void ObjectAt_PrefersGuardiansThenTraps()
        {
            VaultDraft d = Halls();
            d.SetLevel(2);
            d.Place(ObjectType.DripPipe, new TilePos(4, 2));
            Assert.AreEqual(ObjectType.DripPipe, d.ObjectAt(new TilePos(4, 2)).Type, "trap over treasure");
            d.Place(ObjectType.NightOwl, new TilePos(4, 2));
            Assert.AreEqual(ObjectType.NightOwl, d.ObjectAt(new TilePos(4, 2)).Type);
        }

        // ---- Placement checks -------------------------------------------------------------

        [Test]
        public void ShopBlock_ExplainsLevelLimitsAndBudget()
        {
            VaultDraft d = Halls();
            Assert.IsNull(d.ShopBlock(ObjectType.DripPipe));
            Assert.AreEqual("Unlocks at Vault Level 2", d.ShopBlock(ObjectType.NightOwl));

            d.Place(ObjectType.Brazier, new TilePos(0, 6));
            Assert.AreEqual("No more braziers at this Vault Level", d.ShopBlock(ObjectType.Brazier));

            d.Place(ObjectType.Treasure, new TilePos(2, 2));
            d.Place(ObjectType.Treasure, new TilePos(6, 2));
            Assert.AreEqual("No more treasure piles at this Vault Level", d.ShopBlock(ObjectType.Treasure));

            for (int i = 0; i < 12; i++)
            {
                d.Place(ObjectType.DripPipe, new TilePos(1 + i % 10, 6 + i / 10));
            }

            Assert.AreEqual("Not enough build points", d.ShopBlock(ObjectType.Crate));
        }

        [Test]
        public void PlaceProblem_RefusesWalls_AllowsOpenFloor()
        {
            VaultDraft d = Halls();
            Assert.IsNotNull(d.PlaceProblem(ObjectType.DripPipe, new TilePos(0, 6)), "on a wall");
            Assert.IsNotNull(d.PlaceProblem(ObjectType.Crate, d.Entrance), "crate on the entrance");
            Assert.IsNull(d.PlaceProblem(ObjectType.DripPipe, new TilePos(2, 6)));
            Assert.AreEqual(1, d.Objects.Count, "checks never change the draft");
        }

        [Test]
        public void PlaceProblem_RefusesACrateThatBlocksTheOnlyDoor()
        {
            VaultDraft d = Halls();
            Assert.IsNotNull(d.PlaceProblem(ObjectType.Crate, new TilePos(4, 9)));
            Assert.IsNull(d.PlaceProblem(ObjectType.Crate, new TilePos(2, 6)));
        }

        [Test]
        public void MoveProblem_RefusesTakingTheLastTreasureOutOfTheHoard()
        {
            VaultDraft d = Halls();
            int pile = d.Objects[0].Id;
            Assert.IsNotNull(d.MoveProblem(pile, new TilePos(4, 6)));
            Assert.IsNull(d.MoveProblem(pile, new TilePos(5, 2)));
            Assert.AreEqual(new TilePos(4, 2), d.Find(pile).Pos, "checks never change the draft");
        }

        [Test]
        public void NewProblem_IgnoresProblemsThatWereAlreadyThere()
        {
            VaultDraft d = Halls();
            d.Place(ObjectType.DripPipe, new TilePos(0, 6)); // already on a wall
            Assert.IsNull(d.PlaceProblem(ObjectType.Crate, new TilePos(2, 6)));
        }

        // ---- Undo -----------------------------------------------------------------------

        [Test]
        public void Undo_RestoresTheSnapshot_AndRedoReappliesIt()
        {
            var undo = new UndoStack<VaultDraft>(x => x.Clone());
            VaultDraft d = Halls();
            undo.Record(d);
            d.PaintTile(new TilePos(1, 6), TileType.Wall);
            undo.Record(d);
            d.Place(ObjectType.Crate, new TilePos(2, 6));

            d = undo.Undo(d);
            Assert.AreEqual(0, d.Objects.FindAll(o => o.Type == ObjectType.Crate).Count);
            Assert.AreEqual(TileType.Wall, d.TileAt(new TilePos(1, 6)));
            d = undo.Undo(d);
            Assert.AreEqual(TileType.Floor, d.TileAt(new TilePos(1, 6)));
            Assert.IsFalse(undo.CanUndo);

            d = undo.Redo(d);
            Assert.AreEqual(TileType.Wall, d.TileAt(new TilePos(1, 6)));
            d = undo.Redo(d);
            Assert.AreEqual(1, d.Objects.FindAll(o => o.Type == ObjectType.Crate).Count);
            Assert.IsFalse(undo.CanRedo);
        }

        [Test]
        public void NewEdit_ClearsRedo()
        {
            var undo = new UndoStack<VaultDraft>(x => x.Clone());
            VaultDraft d = Halls();
            undo.Record(d);
            d.PaintTile(new TilePos(1, 6), TileType.Wall);
            d = undo.Undo(d);
            undo.Record(d);
            d.PaintTile(new TilePos(2, 6), TileType.Wall);
            Assert.IsFalse(undo.CanRedo);
        }

        // ---- Clear-check and storage ------------------------------------------------------

        /// <summary>
        /// Zigzags through Three Halls' entry corridors, runs up the middle to the hoard and back out:
        /// each turn is a run to the next wall, started a few ticks after Wick stops.
        /// </summary>
        private static (RaidResult, InputLog, int) EscapeHalls(VaultDef vault)
        {
            const int seed = 4242;
            var sim = new Sim.RaidSim(vault, seed, InputLog.Empty);
            Dir[] turns = { Dir.N, Dir.W, Dir.N, Dir.E, Dir.N, Dir.W, Dir.N, Dir.S, Dir.E, Dir.S, Dir.W, Dir.S, Dir.E, Dir.S };
            foreach (Dir d in turns)
            {
                for (int wait = 0; wait < 10 && !sim.IsOver; wait++)
                {
                    sim.Step();
                }

                sim.Submit(Command.RunStart(sim.State.Tick, d));
                do
                {
                    sim.Step();
                }
                while (!sim.IsOver && sim.State.Wick.Moving);
            }

            InputLog log = sim.RecordedLog;
            return (Sim.RaidRunner.RunToEnd(vault, seed, log), log, seed);
        }

        [Test]
        public void StoredClearCheck_ReplaysToTheStoredHash_AfterSaveAndLoad()
        {
            VaultDraft d = Halls();
            VaultDef vault = d.ToVaultDef();
            (RaidResult result, InputLog log, int seed) = EscapeHalls(vault);
            Assert.AreEqual(Outcome.Escaped, result.Outcome);
            Assert.AreEqual(3, result.Stars);
            d.ClearCheck = ClearCheck.FromRun(vault, seed, log, result);

            VaultDraft loaded = VaultDraft.FromJson(d.ToJson());
            ClearCheckResult verify = ClearCheck.Verify(loaded.ToVaultDef(), loaded.ClearCheck);
            Assert.IsTrue(verify.Ok, verify.Reason);
            Assert.AreEqual(result.HashHex, verify.Replay.Value.HashHex);

            loaded.Place(ObjectType.Crate, new TilePos(2, 6));
            Assert.IsFalse(ClearCheck.Verify(loaded.ToVaultDef(), loaded.ClearCheck).Ok, "any edit makes it stale");
        }

        [Test]
        public void Store_SavesListsLoadsDeletes_AndRemembersThePublishedVersion()
        {
            string root = Path.Combine(Path.GetTempPath(), "wick-store-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new DraftStore(root);
                VaultDraft d = Halls();
                d.Name = "My vault";
                store.Save(d);
                Assert.IsNotEmpty(d.DraftId);
                Assert.AreEqual(1, store.List().Count);
                Assert.AreEqual("My vault", store.List()[0].Name);

                VaultDraft loaded = store.Load(d.DraftId);
                Assert.AreEqual(d.ToJson(), loaded.ToJson());

                VaultDef vault = d.ToVaultDef();
                (RaidResult result, InputLog log, int seed) = EscapeHalls(vault);
                d.ClearCheck = ClearCheck.FromRun(vault, seed, log, result);
                store.MarkPublished(d, "v_player123", 2);
                VaultDraft reloaded = store.Load(d.DraftId);
                Assert.AreEqual("v_player123", reloaded.PublishedVaultId);
                Assert.AreEqual(2, reloaded.PublishedVersion);
                Assert.IsTrue(ClearCheck.Verify(reloaded.ToVaultDef(), reloaded.ClearCheck).Ok,
                    "the server's id and version don't make the clear-check stale");

                store.Delete(d.DraftId);
                Assert.AreEqual(0, store.List().Count);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
