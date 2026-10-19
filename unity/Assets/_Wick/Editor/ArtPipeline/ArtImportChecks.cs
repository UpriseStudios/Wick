using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wick.Editor.ArtPipeline
{
    /// <summary>
    /// Runs on everything imported under Art/Incoming. It applies the import settings the handoff
    /// specifies (per-platform compression, colour space, audio format) and checks the content
    /// against the budgets, logging each violation. It never fixes content: an oversized texture
    /// keeps its size (maxTextureSize is opened up so nothing is shrunk silently), and a heavy
    /// mesh keeps its triangles. Wick > Art > Check Incoming lists everything at once.
    /// </summary>
    public sealed class ArtImportChecks : AssetPostprocessor
    {
        /// <summary>Large enough that the importer never downscales a source; size limits are reported, not applied.</summary>
        private const int NoSilentResize = 8192;

        private static readonly string[] MobilePlatforms = { "Android", "iPhone" };

        private void OnPreprocessTexture()
        {
            if (!InScope(assetPath, out string category, out string assetId, out string stem))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = NoSilentResize;
            bool data = stem.EndsWith("_mask"); // R emissive, G rim, B spare: linear data, not colour
            importer.sRGBTexture = !data;
            if (category == "ui")
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = category == "vfx";
            }

            // ASTC on both mobile platforms: 4x4 where edges must stay crisp (UI, data masks), 6x6 for painted colour.
            TextureImporterFormat format = category == "ui" || data ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            foreach (string platform in MobilePlatforms)
            {
                TextureImporterPlatformSettings s = importer.GetPlatformTextureSettings(platform);
                s.overridden = true;
                s.maxTextureSize = NoSilentResize;
                s.format = format;
                importer.SetPlatformTextureSettings(s);
            }
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!InScope(assetPath, out string category, out string assetId, out string stem))
            {
                return;
            }

            Log(ArtRules.CheckTexture(category, assetId, stem, ArtFacts.Texture(assetPath, (TextureImporter)assetImporter)));
        }

        private void OnPreprocessModel()
        {
            if (!InScope(assetPath, out _, out _, out _))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true; // Blender's Z-up FBX arrives Y-up with identity root rotation
            importer.importBlendShapes = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.animationType = ModelImporterAnimationType.Generic; // one shared generic rig for Wick and every skin
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!InScope(assetPath, out string category, out string assetId, out string stem))
            {
                return;
            }

            Log(ArtRules.CheckModel(category, assetId, stem, ArtFacts.Model(assetPath, root, (ModelImporter)assetImporter)));
        }

        /// <summary>
        /// Clips arrive as "Armature|idle" from Blender: name them "idle" and loop the ones the
        /// handoff list loops (idle, run, tiptoe, low_health).
        /// </summary>
        private void OnPreprocessAnimation()
        {
            if (!InScope(assetPath, out _, out _, out _))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation c in clips)
            {
                c.name = ClipName(c.takeName);
                c.loopTime = ArtBudgets.Contains(ArtBudgets.LoopingClips, c.name);
            }

            importer.clipAnimations = clips;
        }

        /// <summary>
        /// The shared rig's contract: clips rotate and scale bones under Armature, and only the body bone moves (hops),
        /// because skins put their bones in different places (Pip sits in a cup, Ember is squat). The
        /// flame bone's scale belongs to the sim's light radius. Other curves are dropped here.
        /// </summary>
        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!InScope(assetPath, out string category, out _, out _) || category != "char")
            {
                return;
            }

            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
            {
                // Only bones animate: mesh objects and face shapes (ReactionDriver's) are left alone.
                bool bone = b.path.StartsWith("Armature/") && b.type == typeof(Transform);
                string name = b.path.Substring(b.path.LastIndexOf('/') + 1);
                bool position = b.propertyName.StartsWith("m_LocalPosition") && name != "body";
                bool flameScale = b.propertyName.StartsWith("m_LocalScale") && name == "flame_base";
                if (!bone || position || flameScale)
                {
                    AnimationUtility.SetEditorCurve(clip, b, null);
                }
            }
        }

        public static string ClipName(string take)
        {
            return take.Substring(take.LastIndexOf('|') + 1);
        }

        private void OnPreprocessAudio()
        {
            if (!InScope(assetPath, out string category, out _, out _))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = category == "music" ? 0.5f : 0.7f;
            s.loadType = category == "music" ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = s;
            importer.loadInBackground = category == "music";
        }

        private void OnPostprocessAudio(AudioClip clip)
        {
            if (!InScope(assetPath, out string category, out string assetId, out string stem))
            {
                return;
            }

            Log(ArtRules.CheckAudio(category, assetId, stem, new AssetFacts { Path = assetPath, Channels = clip.channels }));
        }

        private static bool InScope(string path, out string category, out string assetId, out string stem)
        {
            var problems = new List<Violation>();
            bool ok = ArtRules.TryParsePath(path, out category, out assetId, out stem, problems);
            Log(problems);
            return ok;
        }

        private static void Log(List<Violation> violations)
        {
            foreach (Violation v in violations)
            {
                Debug.LogWarning("[Art] " + v, AssetDatabase.LoadMainAssetAtPath(v.Path));
            }
        }
    }
}
