using System;
using System.Collections.Generic;
using System.IO;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Drafts as JSON files on the device: drafts/&lt;draftId&gt;.json holds a VaultDraft. Publishing
    /// goes through the server (PublishVault); the draft only remembers the id and version it got.
    /// </summary>
    public sealed class DraftStore
    {
        private readonly string _drafts;
        private readonly Random _ids = new Random();

        public DraftStore(string root)
        {
            _drafts = Path.Combine(root, "drafts");
            Directory.CreateDirectory(_drafts);
        }

        public readonly struct Entry
        {
            public readonly string DraftId;
            public readonly string Name;
            public readonly DateTime Modified;

            public Entry(string draftId, string name, DateTime modified)
            {
                DraftId = draftId;
                Name = name;
                Modified = modified;
            }
        }

        /// <summary>Drafts, most recently edited first.</summary>
        public List<Entry> List()
        {
            var entries = new List<Entry>();
            foreach (string file in Directory.GetFiles(_drafts, "*.json"))
            {
                try
                {
                    VaultDraft d = VaultDraft.FromJson(File.ReadAllText(file));
                    entries.Add(new Entry(Path.GetFileNameWithoutExtension(file), d.Name, File.GetLastWriteTimeUtc(file)));
                }
                catch (FormatException)
                {
                    // Skip a damaged draft rather than failing the whole list.
                }
            }

            entries.Sort((a, b) => b.Modified.CompareTo(a.Modified));
            return entries;
        }

        public void Save(VaultDraft draft)
        {
            if (string.IsNullOrEmpty(draft.DraftId))
            {
                draft.DraftId = "d_" + NewId();
            }

            string path = Path.Combine(_drafts, draft.DraftId + ".json");
            string temp = path + ".tmp";
            File.WriteAllText(temp, draft.ToJson());
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temp, path);
        }

        public VaultDraft Load(string draftId)
        {
            return VaultDraft.FromJson(File.ReadAllText(Path.Combine(_drafts, draftId + ".json")));
        }

        public void Delete(string draftId)
        {
            string path = Path.Combine(_drafts, draftId + ".json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>Records the live vault id and version the server published this draft as.</summary>
        public void MarkPublished(VaultDraft draft, string vaultId, int version)
        {
            draft.PublishedVaultId = vaultId;
            draft.PublishedVersion = version;
            Save(draft);
        }

        private string NewId()
        {
            const string chars = "abcdefghijkmnpqrstuvwxyz23456789";
            var id = new char[8];
            for (int i = 0; i < id.Length; i++)
            {
                id[i] = chars[_ids.Next(chars.Length)];
            }

            return new string(id);
        }
    }
}
