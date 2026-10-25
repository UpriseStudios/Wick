using System.IO;
using UnityEditor;
using UnityEngine;
using Wick.Game;
using Wick.Sim;

namespace Wick.Editor
{
    /// <summary>
    /// Wick > Export Drafts as Designer Vaults: copies every vault draft made in the in-game editor
    /// on this machine (Play mode in the Editor saves them under persistentDataPath) into
    /// Data/DesignerVaults as a vault document with its clear-check, named after the draft. Then run
    /// dotnet run --project tools/seedgen -- designer to check them and add them to the seed pool.
    /// </summary>
    public static class DesignerVaultExport
    {
        public const string OutDir = "Assets/_Wick/Data/DesignerVaults";

        [MenuItem("Wick/Export Drafts as Designer Vaults")]
        public static void Export()
        {
            var store = new DraftStore(Application.persistentDataPath);
            Directory.CreateDirectory(OutDir);
            int n = 0;
            foreach (DraftStore.Entry entry in store.List())
            {
                VaultDraft draft = store.Load(entry.DraftId);
                string file = Path.Combine(OutDir, Safe(draft.Name) + ".json");
                File.WriteAllText(file, VaultJson.WriteDocument(draft.ToVaultDef(), draft.ClearCheck));
                n++;
            }

            AssetDatabase.Refresh();
            Debug.Log("Exported " + n + " draft(s) to " + OutDir + ". Next: dotnet run --project tools/seedgen -- designer");
        }

        private static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '-');
            }

            return name.Trim();
        }
    }
}
