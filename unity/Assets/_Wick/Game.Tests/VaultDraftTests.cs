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
            for (int i = 0; i < 6; i++)
            {
                d.Place(ObjectType.DripPipe, new TilePos(1 + i, 6));
            }

            Assert.AreEqual(60, d.BuildPoints);
            CollectionAssert.IsEmpty(Codes(d));
            d.Place(ObjectType.DripPipe, new TilePos(7, 6));
            Assert.That(Codes(d), Has.Member(VaultIssueCode.OverBudget));
        }

        [Test]
        public void NewTimedTraps_GetDefaultCycles()
        {
            VaultDraft d = Halls();
            Assert.AreEqual(45, d.Place(ObjectType.DripPipe, new TilePos(2, 6)).Cycle);
            Assert.AreEqual(60, d.Place(ObjectType.SpikeFloor, new TilePos(3, 6)).Cycle);
            Assert.AreEqual(75, d.Place(ObjectType.Bellows, new TilePos(5, 6)).Cycle);
            CollectionAssert.IsEmpty(Codes(d));
        }

        [Test]
        public void Rotate_CyclesThroughFourFacings()
        {
            VaultDraft d = Halls();
            int id = d.Place(ObjectType.Bellows, new TilePos(2, 6)).Id;
            for (int i = 1; i <= 4; i++)
            {
                d.Rotate(id);
                Assert.AreEqual(i % 4, d.Find(id).Rot);
            }
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
            CollectionAssert.IsEmpty(VaultValidator.Validate(v));
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
            CollectionAssert.IsEmpty(Codes(d));
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

        /// <summary>Runs up the middle of Three Halls to the hoard and back out.</summary>
        private static (RaidResult, InputLog, int) EscapeHalls(VaultDef vault)
        {
            var log = new InputLog(new[] { Command.RunStart(0, Dir.N), Command.RunStart(150, Dir.S) });
            const int seed = 4242;
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
        public void Store_SavesListsLoadsDeletesAndPublishes()
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
                string path = store.Publish(d);
                Assert.AreEqual(1, d.PublishedVersion);
                JsonNode doc = JsonReader.Parse(File.ReadAllText(path));
                Assert.AreEqual(1, doc.GetInt("version"));
                Assert.IsTrue(ClearCheck.Verify(VaultJson.FromNode(doc), VaultJson.ReadClearCheck(doc)).Ok);

                store.Publish(d);
                Assert.AreEqual(2, d.PublishedVersion, "republishing bumps the version");

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
