using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Wick.Editor.ArtPipeline;
using static Wick.Editor.UiPainter;

namespace Wick.Editor
{
    /// <summary>
    /// The UI kit in the logo's materials: carved dark wood plaques with gold trim, burnished gold and
    /// crimson buttons, round wooden icon buttons, leather cards, a crimson ribbon, and icons, all at 3x
    /// (the panels' reference resolution is 1080 x 1920). Each sprite comes from Art/Incoming/ui/&lt;id&gt;/&lt;id&gt;@3x.png
    /// when supplied, otherwise a painted stand-in. Writes Game/UI/UiKit.uss (the look); layout stays in
    /// each screen's own stylesheet. Menu: Wick > Art > Build UI Kit. Also run by Rebuild Placeholder Content.
    /// </summary>
    public static class UiKitBuilder
    {
        private const string Root = "Assets/_Wick";
        private const string StandInDir = Root + "/Art/StandIns/UI";
        private const string FontsDir = Root + "/Art/Fonts";
        public const string UssPath = Root + "/Game/UI/UiKit.uss";
        public const string TitlePath = Root + "/Art/UI/title_v1.png";

        // The title reference's palette.
        private static readonly Color WoodHi = Hex(0x4A2E1B), Wood = Hex(0x3A2416), WoodLo = Hex(0x21140C);
        private static readonly Color Gold = Hex(0xE2A63E), GoldHi = Hex(0xF6D27A), GoldLo = Hex(0x9C6A1E);
        private static readonly Color Crimson = Hex(0x8E2228), CrimsonHi = Hex(0xC2433F), CrimsonLo = Hex(0x5C1418);
        private static readonly Color Leather = Hex(0x5A3520), LeatherHi = Hex(0x6E442A), Wax = Hex(0xF4E6C6), WaxLo = Hex(0xD9C49A);
        private static readonly Color Stone = Hex(0x5A5048), Flame = Hex(0xFF9F1C), FlameCore = Hex(0xFFE29A);

        [MenuItem("Wick/Art/Build UI Kit")]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            Debug.Log("UiKitBuilder: UI kit and UiKit.uss rebuilt.");
        }

        public static void Build()
        {
            Directory.CreateDirectory(StandInDir);
            Directory.CreateDirectory(Path.GetDirectoryName(TitlePath));
            if (!File.Exists(TitlePath))
            {
                File.Copy("../docs/art/references/title_screen_reference_v1.png", TitlePath); // the first AAB uses the reference as title art
                AssetDatabase.ImportAsset(TitlePath, ImportAssetOptions.ForceSynchronousImport);
            }

            var sprites = new Dictionary<string, string>
            {
                ["plaque"] = Sliced("ui_plaque", 120, 40, Plaque(120, trim: true, rivets: false)),
                ["panel"] = Sliced("ui_panel", 180, 66, Plaque(180, trim: true, rivets: true)),
                ["gold"] = Sliced("ui_button_gold", 120, 46, Button(120, GoldHi, Gold, GoldLo)),
                ["crimson"] = Sliced("ui_button_crimson", 120, 44, Button(120, CrimsonHi, Crimson, CrimsonLo)),
                ["wood"] = Sliced("ui_button_wood", 120, 44, Button(120, WoodHi, Wood, WoodLo)),
                ["round"] = Sliced("ui_round", 150, 0, RoundButton(150)),
                ["card"] = Sliced("ui_card", 120, 40, Card(120)),
                ["ribbon"] = Sliced("ui_ribbon", 200, 64, Ribbon(200)),
                ["bar"] = Sliced("ui_bar", 60, 26, Bar(60, false)),
                ["bar_fill"] = Sliced("ui_bar_fill", 60, 26, Bar(60, true)),
                ["avatar"] = Sliced("ui_avatar", 120, 0, Avatar(120)),
            };

            var icons = new Dictionary<string, Func<UiPainter>>
            {
                ["coin"] = CoinIcon, ["wax"] = WaxIcon, ["gem"] = GemIcon, ["crown"] = CrownIcon, ["shop"] = ShopIcon, ["pass"] = PassIcon,
                ["trophy"] = TrophyIcon, ["skins"] = SkinsIcon, ["settings"] = GearIcon, ["log"] = ShieldIcon, ["share"] = ShareIcon,
                ["build"] = HammerIcon, ["flame"] = () => FlameIcon(true), ["flame_out"] = () => FlameIcon(false), ["star"] = () => StarIcon(true),
                ["star_empty"] = () => StarIcon(false), ["sneak"] = SneakIcon, ["clock"] = ClockIcon, ["vault"] = VaultIcon,
            };

            var iconUrls = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Func<UiPainter>> kv in icons)
            {
                iconUrls[kv.Key] = Sliced("ui_icon_" + kv.Key, 128, 0, kv.Value());
            }

            string body = Font("Baloo2-Medium");
            string display = Font("Baloo2-ExtraBold");
            string title = "url(\"project://database/" + TitlePath + "?fileID=2800000&guid=" + AssetDatabase.AssetPathToGUID(TitlePath) + "&type=3#title_v1\")";
            File.WriteAllText(UssPath, Uss(sprites, iconUrls, body, display, title));
            AssetDatabase.ImportAsset(UssPath, ImportAssetOptions.ForceSynchronousImport);
        }

        // ---- The stylesheet: the look only ---------------------------------------------------------

        private static string Uss(Dictionary<string, string> s, Dictionary<string, string> icons, string body, string display, string title)
        {
            const string ink = "rgb(26, 15, 8)";
            var u = new StringBuilder();
            u.AppendLine("/* Generated by Wick > Art > Build UI Kit (Editor/UiKitBuilder.cs): the look. Layout lives in each screen's stylesheet. */");
            u.AppendLine();
            u.AppendLine("/* Baloo 2 (SIL OFL): Medium to read, ExtraBold for lettering. Ivory text with an ink outline and drop, like the logo. */");
            u.AppendLine(".root { -unity-font-definition: " + body + "; color: rgb(244, 230, 198); }");
            u.AppendLine(".lettering, .title, .section-title, .ribbon, Button, .loot, .timer, .banner, .res-label, .round-caption, .badge {");
            u.AppendLine("    -unity-font-definition: " + display + ";");
            u.AppendLine("    -unity-font-style: normal;");
            u.AppendLine("    color: rgb(244, 230, 198);");
            u.AppendLine("    -unity-text-outline-width: 6px;");
            u.AppendLine("    -unity-text-outline-color: " + ink + ";");
            u.AppendLine("    text-shadow: 0 7px 0 " + ink + ";");
            u.AppendLine("}");
            u.AppendLine();

            void Bg(string selector, string url, string extra = "")
            {
                u.AppendLine(selector + " {");
                u.AppendLine("    background-image: " + url + ";");
                u.AppendLine("    background-color: rgba(0, 0, 0, 0);");
                u.AppendLine("    border-width: 0;");
                u.AppendLine("    border-radius: 0;");
                if (extra.Length > 0)
                {
                    u.AppendLine("    " + extra);
                }

                u.AppendLine("}");
            }

            Bg(".plaque, .bar-button, .toast, .selection-bar", s["plaque"]);
            Bg(".panel-card, .issues-panel", s["panel"]);
            // Gold for the main action, wood for the rest, crimson for danger. Two-class selectors win over one.
            Bg(".btn-wood, .wide-button.secondary, .action, .small-button, .tiny-button, .sel-button, .tab, .tool", s["wood"]);
            Bg(".btn-gold, .wide-button, .log-button, .action.primary, .tab.selected, .tool.selected, .pill", s["gold"]);
            Bg(".btn-crimson, .log-button.revenge, .close-button, .sel-button.danger, .pill.bad", s["crimson"]);
            Bg(".round, .round-button", s["round"], "-unity-background-scale-mode: scale-to-fit;");
            Bg(".card, .log-row, .draft-row, .issue-row", s["card"]);
            Bg(".ribbon", s["ribbon"]);
            Bg(".bar, .budget-track", s["bar"]);
            Bg(".bar-fill, .budget-fill", s["bar_fill"]);
            Bg(".avatar", s["avatar"], "-unity-background-scale-mode: scale-to-fit;");
            Bg(".title-screen", title, "-unity-background-scale-mode: scale-and-crop;");
            u.AppendLine(".icon { -unity-background-scale-mode: scale-to-fit; }");
            foreach (KeyValuePair<string, string> kv in icons)
            {
                u.AppendLine(".icon-" + kv.Key.Replace('_', '-') + " { background-image: " + kv.Value + "; }");
            }

            u.AppendLine(".flicker { background-image: " + icons["flame"] + "; background-color: rgba(0, 0, 0, 0); border-radius: 0; -unity-background-scale-mode: scale-to-fit; }");
            u.AppendLine(".flicker.out { background-image: " + icons["flame_out"] + "; }");
            u.AppendLine(".star { background-image: " + icons["star_empty"] + "; background-color: rgba(0, 0, 0, 0); -unity-background-scale-mode: scale-to-fit; border-width: 0; }");
            u.AppendLine(".star.on { background-image: " + icons["star"] + "; }");
            u.AppendLine();
            u.AppendLine("/* Text on leather and wood stays ivory; held and robbed lines are tinted. */");
            u.AppendLine(".log-text { color: rgb(240, 222, 188); }");
            u.AppendLine(".log-text.held { color: rgb(180, 222, 150); }");
            u.AppendLine(".log-text.robbed { color: rgb(255, 170, 150); }");
            u.AppendLine("Button:disabled { opacity: 0.45; }");
            return u.ToString();
        }

        // ---- Sprites ------------------------------------------------------------------------------

        /// <summary>Saves a painted sprite (or uses the supplied one) with its 9-slice border, as a USS url.</summary>
        private static string Sliced(string id, int size, int border, UiPainter painted)
        {
            string path = ArtBudgets.IncomingRoot + "/ui/" + id + "/" + id + "@3x.png";
            bool standIn = !File.Exists(path);
            if (standIn)
            {
                path = StandInDir + "/" + id + ".png";
                Texture2D tex = painted.ToTexture();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            if (border > 0 && (standIn || importer.spriteBorder == Vector4.zero)) // the artist's slices win on real art
            {
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                float b = Mathf.Round(Mathf.Min(w, h) * border / (float)size);
                importer.spriteBorder = new Vector4(b, b, b, b);
            }

            importer.SaveAndReimport();
            string guid = AssetDatabase.AssetPathToGUID(path);
            return "url(\"project://database/" + path + "?fileID=21300000&guid=" + guid + "&type=3#" + Path.GetFileNameWithoutExtension(path) + "\")";
        }

        /// <summary>Carved dark timber with grain, a gold inlay, and (on big panels) gold corner brackets.</summary>
        private static UiPainter Plaque(int n, bool trim, bool rivets)
        {
            var p = new UiPainter(n);
            float h = n / 2f;
            Func<float, float, float> body = RoundRect(h, h, h - 1, h - 1, n * 0.18f);
            p.Fill(body, WoodHi, WoodLo, outline: 6);
            p.Grain(Inset(body, 6), Hex(0x140A05), 0.35f, spacing: n / 14f);
            // A bevel: light on the top edge, shade on the bottom.
            p.Solid(Subtract(Inset(body, 6), RoundRect(h, h - 3, h - 7, h - 8, n * 0.16f)), Hex(0xF6D27A, 0.18f));
            p.Solid(Subtract(Inset(body, 6), RoundRect(h, h + 3, h - 7, h - 8, n * 0.16f)), Hex(0x000000, 0.28f));
            if (trim)
            {
                float t = n * 0.1f;
                p.Solid(Subtract(RoundRect(h, h, h - t, h - t, n * 0.12f), RoundRect(h, h, h - t - 3, h - t - 3, n * 0.1f)), GoldLo);
                p.Solid(Subtract(RoundRect(h, h + 1, h - t, h - t, n * 0.12f), RoundRect(h, h, h - t - 1.5f, h - t - 1.5f, n * 0.11f)), Hex(0xF6D27A, 0.5f));
            }

            if (rivets)
            {
                // Gold corner brackets with a rivet: the panel looks bound like a chest.
                float a = 10, l = n * 0.24f, w = 9;
                foreach ((float sx, float sy) in new[] { (1f, 1f), (-1f, 1f), (1f, -1f), (-1f, -1f) })
                {
                    float cx = sx > 0 ? a : n - a, cy = sy > 0 ? a : n - a;
                    Func<float, float, float> bracket = Union(
                        RoundRect(cx + sx * l / 2, cy + sy * w / 2, l / 2, w / 2, 3),
                        RoundRect(cx + sx * w / 2, cy + sy * l / 2, w / 2, l / 2, 3));
                    p.Fill(bracket, GoldHi, GoldLo, outline: 2.5f);
                    p.Fill(Circle(cx + sx * w * 0.9f, cy + sy * w * 0.9f, 4.5f), GoldHi, GoldLo, outline: 1.5f);
                }
            }

            return p;
        }

        /// <summary>
        /// A chunky raised button: a darker base with the face sitting on it (so it looks pressable),
        /// a bright inner rim on the face and a curved highlight. Ink outline round the lot.
        /// </summary>
        private static UiPainter Button(int n, Color hi, Color mid, Color lo, float radius = 26)
        {
            var p = new UiPainter(n);
            float h = n / 2f;
            p.Fill(RoundRect(h, h, h - 1, h - 1, radius), Color.Lerp(lo, Color.black, 0.15f), Color.Lerp(lo, Color.black, 0.35f), outline: 7);
            Func<float, float, float> face = RoundRect(h, h + 5, h - 9, h - 14, radius - 6);
            p.Fill(face, Color.Lerp(mid, hi, 0.55f), Color.Lerp(mid, lo, 0.35f));
            p.Solid(Subtract(face, RoundRect(h, h + 3, h - 12, h - 15, radius - 8)), Color.Lerp(hi, Color.white, 0.35f) * new Color(1, 1, 1, 0.7f));
            p.Solid(Ellipse(h, n * 0.74f, h - 22, n * 0.075f), new Color(1f, 1f, 1f, 0.3f));
            return p;
        }

        /// <summary>Round icon button: a brass rim with highlight around a carved wooden face.</summary>
        private static UiPainter RoundButton(int n)
        {
            var p = new UiPainter(n);
            float h = n / 2f;
            p.Fill(Circle(h, h, h - 2), GoldHi, GoldLo, outline: 6);
            Func<float, float, float> face = Circle(h, h, h - 15);
            p.Fill(face, Hex(0x5A3824), WoodLo, outline: 3);
            p.Grain(face, Hex(0x140A05), 0.3f, spacing: 10f);
            p.Solid(Subtract(face, Circle(h, h - 4, h - 17)), Hex(0x000000, 0.35f)); // inner shadow under the rim
            p.Solid(Ellipse(h - h * 0.3f, h + h * 0.55f, h * 0.35f, h * 0.12f), new Color(1f, 1f, 1f, 0.35f)); // shine on the brass
            return p;
        }

        /// <summary>Leather with a stitched groove.</summary>
        private static UiPainter Card(int n)
        {
            var p = new UiPainter(n);
            float h = n / 2f;
            Func<float, float, float> body = RoundRect(h, h, h - 1, h - 1, 20);
            p.Fill(body, LeatherHi, Leather, outline: 6);
            p.Grain(Inset(body, 6), Hex(0x2A170C), 0.12f, spacing: 5f);
            p.Solid(Subtract(Inset(body, 6), RoundRect(h, h - 2, h - 7, h - 8, 16)), Hex(0xFFD9A0, 0.14f));
            // Stitches: short dashes along an inset outline.
            Func<float, float, float> seam = Subtract(RoundRect(h, h, h - 12, h - 12, 12), RoundRect(h, h, h - 14, h - 14, 10));
            p.Solid((x, y) => Mathf.Max(seam(x, y), Mathf.Sin((x + y) * 0.55f) * 3f), Hex(0xD9B98A, 0.6f));
            return p;
        }

        /// <summary>A crimson banner with folded, notched tails (the tails sit in the 9-slice borders).</summary>
        private static UiPainter Ribbon(int n)
        {
            var p = new UiPainter(n);
            float h = n / 2f;
            float t = n * 0.3f; // tail width
            Func<float, float, float> tails = Union(
                Polygon(new Vector2(2, h + 26), new Vector2(t + 4, h + 26), new Vector2(t + 4, h - 34), new Vector2(2, h - 34), new Vector2(18, h - 4)),
                Polygon(new Vector2(n - 2, h + 26), new Vector2(n - t - 4, h + 26), new Vector2(n - t - 4, h - 34), new Vector2(n - 2, h - 34), new Vector2(n - 18, h - 4)));
            p.Fill(tails, Color.Lerp(Crimson, Color.black, 0.25f), Color.Lerp(CrimsonLo, Color.black, 0.3f), outline: 6);
            Func<float, float, float> band = RoundRect(h, h + 6, h - t * 0.6f, h - 18, 8);
            p.Fill(band, CrimsonHi, CrimsonLo, outline: 6);
            p.Solid(Subtract(Inset(band, 6), RoundRect(h, h + 3, h - t * 0.6f - 7, h - 25, 6)), Hex(0xFFB0A0, 0.35f));
            p.Solid(Subtract(RoundRect(h, h + 6, h - t * 0.6f - 12, h - 30, 4), RoundRect(h, h + 6, h - t * 0.6f - 14, h - 32, 3)), Hex(0xF6D27A, 0.55f)); // gold stitch line
            return p;
        }

        private static UiPainter Bar(int n, bool fill)
        {
            var p = new UiPainter(n);
            float h = n / 2f;
            if (fill)
            {
                p.Fill(RoundRect(h, h, h - 2, h - 8, 12), GoldHi, GoldLo, outline: 3);
            }
            else
            {
                p.Fill(RoundRect(h, h, h - 1, h - 5, 14), Hex(0x140A05), Hex(0x24150C), outline: 4);
            }

            return p;
        }

        private static UiPainter Avatar(int n)
        {
            var p = new UiPainter(n);
            float h = n / 2f;
            p.Fill(RoundRect(h, h, h - 2, h - 2, 26), Hex(0x3A2416), Hex(0x1E120A), outline: 6);
            p.Fill(RoundRect(h, n * 0.42f, n * 0.22f, n * 0.26f, 14), Wax, WaxLo, outline: 5);
            p.Fill(Drop(h, n * 0.74f, n * 0.09f, n * 0.2f), FlameCore, Flame, outline: 4);
            p.Solid(Circle(h - n * 0.08f, n * 0.47f, n * 0.035f), Ink);
            p.Solid(Circle(h + n * 0.08f, n * 0.47f, n * 0.035f), Ink);
            return p;
        }

        // ---- Icons (128 px) --------------------------------------------------------------------

        private static UiPainter CoinIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Circle(64, 62, 52), GoldHi, GoldLo, outline: 8);
            p.Solid(Subtract(Circle(64, 62, 36), Circle(64, 62, 30)), Hex(0x9C6A1E, 0.8f));
            p.Solid(Ellipse(48, 82, 12, 8), new Color(1, 1, 1, 0.45f));
            return p;
        }

        private static UiPainter WaxIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Drop(64, 48, 38, 112), Wax, WaxLo, outline: 8);
            p.Solid(Ellipse(52, 56, 9, 14), new Color(1, 1, 1, 0.5f));
            return p;
        }

        private static UiPainter GemIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Polygon(new Vector2(64, 120), new Vector2(118, 64), new Vector2(64, 8), new Vector2(10, 64)), Hex(0xE86B6B), Hex(0x8E1F2B), outline: 8);
            p.Solid(Polygon(new Vector2(64, 104), new Vector2(92, 70), new Vector2(64, 60), new Vector2(40, 72)), new Color(1, 1, 1, 0.3f));
            return p;
        }

        private static UiPainter CrownIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Polygon(new Vector2(12, 24), new Vector2(116, 24), new Vector2(116, 96), new Vector2(88, 64), new Vector2(64, 108),
                new Vector2(40, 64), new Vector2(12, 96)), GoldHi, GoldLo, outline: 8);
            p.Fill(Circle(64, 44, 10), Hex(0xE86B6B), Hex(0x8E1F2B), outline: 4);
            return p;
        }

        private static UiPainter ShopIcon()
        {
            var p = new UiPainter(128);
            p.Fill(RoundRect(64, 46, 50, 32, 8), Hex(0x8A5A30), Hex(0x5C3A1C), outline: 8);
            p.Fill(Union(RoundRect(64, 84, 50, 16, 8), Ellipse(64, 96, 50, 22)), Hex(0xA06A38), Hex(0x7A4C24), outline: 8);
            p.Fill(RoundRect(64, 64, 12, 14, 3), GoldHi, GoldLo, outline: 5);
            return p;
        }

        private static UiPainter PassIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Polygon(new Vector2(30, 120), new Vector2(98, 120), new Vector2(98, 10), new Vector2(64, 34), new Vector2(30, 10)), CrimsonHi, CrimsonLo, outline: 8);
            p.Fill(Circle(64, 76, 22), GoldHi, GoldLo, outline: 6);
            return p;
        }

        private static UiPainter TrophyIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Union(Polygon(new Vector2(26, 118), new Vector2(102, 118), new Vector2(90, 62), new Vector2(38, 62)), Circle(64, 70, 36)), GoldHi, GoldLo, outline: 8);
            p.Fill(RoundRect(64, 24, 32, 10, 4), Hex(0x6E442A), Hex(0x4A2E1B), outline: 6);
            p.Fill(RoundRect(64, 40, 8, 12, 2), Gold, GoldLo, outline: 5);
            return p;
        }

        private static UiPainter SkinsIcon()
        {
            var p = new UiPainter(128);
            p.Fill(RoundRect(64, 42, 30, 36, 12), Wax, WaxLo, outline: 8);
            p.Fill(Drop(64, 86, 14, 124), FlameCore, Flame, outline: 6);
            p.Solid(Circle(54, 50, 5), Ink);
            p.Solid(Circle(74, 50, 5), Ink);
            return p;
        }

        private static UiPainter GearIcon()
        {
            var p = new UiPainter(128);
            var teeth = new List<Func<float, float, float>> { Circle(64, 64, 38) };
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                teeth.Add(Line(new Vector2(64, 64), new Vector2(64 + Mathf.Cos(a) * 50, 64 + Mathf.Sin(a) * 50), 11));
            }

            p.Fill(Subtract(Union(teeth.ToArray()), Circle(64, 64, 14)), Hex(0xC9B8A0), Hex(0x8C7A62), outline: 8);
            return p;
        }

        private static UiPainter ShieldIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Polygon(new Vector2(14, 112), new Vector2(114, 112), new Vector2(110, 58), new Vector2(64, 8), new Vector2(18, 58)), Hex(0x6B7280), Hex(0x3E434C), outline: 8);
            p.Fill(Polygon(new Vector2(36, 98), new Vector2(92, 98), new Vector2(90, 62), new Vector2(64, 30), new Vector2(38, 62)), CrimsonHi, CrimsonLo, outline: 5);
            return p;
        }

        private static UiPainter ShareIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Line(new Vector2(36, 64), new Vector2(92, 98), 9), Wax, WaxLo, outline: 6);
            p.Fill(Line(new Vector2(36, 64), new Vector2(92, 30), 9), Wax, WaxLo, outline: 6);
            foreach (Vector2 c in new[] { new Vector2(36, 64), new Vector2(92, 98), new Vector2(92, 30) })
            {
                p.Fill(Circle(c.x, c.y, 20), GoldHi, GoldLo, outline: 7);
            }

            return p;
        }

        private static UiPainter HammerIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Line(new Vector2(40, 18), new Vector2(84, 86), 10), Hex(0x8A5A30), Hex(0x5C3A1C), outline: 7);
            p.Fill(Polygon(new Vector2(54, 92), new Vector2(84, 118), new Vector2(118, 84), new Vector2(92, 58)), Hex(0x9AA0A8), Hex(0x5E646C), outline: 8);
            return p;
        }

        private static UiPainter FlameIcon(bool lit)
        {
            var p = new UiPainter(128);
            if (lit)
            {
                p.Fill(Drop(64, 44, 36, 124), FlameCore, Hex(0xC4561A), outline: 8);
                p.Solid(Drop(64, 38, 16, 84), new Color(1f, 0.95f, 0.8f, 0.85f));
            }
            else
            {
                p.Fill(Drop(64, 44, 36, 124), Hex(0x4A3A32), Hex(0x2A201C), outline: 8);
            }

            return p;
        }

        private static UiPainter StarIcon(bool lit)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 58 : 26;
                pts[i] = new Vector2(64 + Mathf.Cos(a) * r, 60 + Mathf.Sin(a) * r);
            }

            var p = new UiPainter(128);
            p.Fill(Polygon(pts), lit ? GoldHi : Hex(0x4A3A32), lit ? GoldLo : Hex(0x2A201C), outline: 8);
            return p;
        }

        private static UiPainter SneakIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Ellipse(44, 46, 16, 26), Wax, WaxLo, outline: 6);
            p.Fill(Ellipse(84, 80, 16, 26), Wax, WaxLo, outline: 6);
            return p;
        }

        private static UiPainter ClockIcon()
        {
            var p = new UiPainter(128);
            p.Fill(Circle(64, 64, 52), Wax, WaxLo, outline: 8);
            p.Solid(Line(new Vector2(64, 64), new Vector2(64, 100), 6), Ink);
            p.Solid(Line(new Vector2(64, 64), new Vector2(90, 52), 6), Ink);
            return p;
        }

        private static UiPainter VaultIcon()
        {
            var p = new UiPainter(128);
            p.Fill(RoundRect(64, 56, 52, 44, 8), Stone, Hex(0x3A322C), outline: 8);
            p.Fill(Polygon(new Vector2(6, 92), new Vector2(64, 124), new Vector2(122, 92)), Hex(0x6E442A), Hex(0x4A2E1B), outline: 7);
            p.Fill(RoundRect(64, 40, 18, 28, 8), Hex(0x3A2416), Hex(0x1E120A), outline: 6);
            return p;
        }

        // ---- Fonts --------------------------------------------------------------------------------

        /// <summary>A dynamic font asset for a TTF in Art/Fonts, as a USS url (glyphs added as text needs them).</summary>
        private static string Font(string name)
        {
            string path = FontsDir + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (asset == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(FontsDir + "/" + name + ".ttf");
                asset = FontAsset.CreateFontAsset(source);
                asset.name = name;
                AssetDatabase.CreateAsset(asset, path);
                foreach (Texture2D atlas in asset.atlasTextures)
                {
                    atlas.name = name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }

                asset.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                AssetDatabase.SaveAssets();
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            return "url(\"project://database/" + path + "?fileID=11400000&guid=" + guid + "&type=2#" + name + "\")";
        }
    }
}
