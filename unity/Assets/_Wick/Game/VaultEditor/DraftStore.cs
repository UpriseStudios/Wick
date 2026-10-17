using System;
using System.Collections.Generic;
using System.IO;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Drafts and locally "published" vaults as JSON files (Cloud Save replaces this in M5).
    /// drafts/&lt;draftId&gt;.json holds a VaultDraft; published/&lt;vaultId&gt;.json the vault document.
    /// </summary>
    public sealed class DraftStore
    {
        private readonly string _drafts;
        private readonly string _published;
        private readonly Random _ids = new Random();

        public DraftStore(string root)
        {
            _drafts = Path.Combine(root, "drafts");
            _published = Path.Combine(root, "published");
            Directory.CreateDirectory(_drafts);
            Directory.CreateDirectory(_published);
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

        /// <summary>
        /// Writes the vault as published (next version, with its clear-check) and records that on the
        /// draft. The caller must have validated it and verified the clear-check first.
        /// </summary>
        public string Publish(VaultDraft draft)
        {
            if (string.IsNullOrEmpty(draft.PublishedVaultId))
            {
                draft.PublishedVaultId = "v_" + NewId();
            }

            draft.PublishedVersion++;
            string doc = VaultJson.WriteDocument(draft.ToVaultDef(), draft.ClearCheck);
            string path = Path.Combine(_published, draft.PublishedVaultId + ".json");
            File.WriteAllText(path, doc);
            Save(draft);
            return path;
        }

        public string PublishedPath(string vaultId)
        {
            return Path.Combine(_published, vaultId + ".json");
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
