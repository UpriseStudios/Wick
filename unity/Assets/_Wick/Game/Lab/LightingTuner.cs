using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wick.Game
{
    /// <summary>
    /// The lighting lab's live tuning panel (docs/m10-2-lighting.md, M10.2c; dev builds only): a slider,
    /// toggle or colour row for every LightingProfile value, read from the profile's own fields so new
    /// values appear by themselves, with the lab's open decisions at the top. Changes apply at once to
    /// the raid in view. It tunes a copy of the theme's profile: Save writes it back to the asset in
    /// the Editor, or to a JSON file on a phone (persistentDataPath/lighting/), which
    /// Wick > Lighting > Import Tuned Profile copies into the asset.
    /// </summary>
    public sealed class LightingTuner : MonoBehaviour
    {
        /// <summary>The decisions the lab settles, shown first (docs/m10-2-lighting.md, Decisions needed).</summary>
        private static readonly string[] Decisions =
        {
            nameof(LightingProfile.Remembered), nameof(LightingProfile.RememberedBrightness),
            nameof(LightingProfile.UnseenBrightness), nameof(LightingProfile.ReadableTreasure),
        };

        /// <summary>Set by the Editor so Save can write the asset (Editor-only code lives in Wick.Editor).</summary>
        public static Action<LightingProfile, LightingProfile> SaveToAsset;

        private VaultView _view;
        private VisibilityMask _mask;
        private LightingProfile _source;
        private LightingProfile _tuned;
        private UIDocument _doc;
        private VisualElement _panel;
        private Label _status;
        private float _rebuildAt = -1f;

        public bool Open => _panel != null && _panel.style.display == DisplayStyle.Flex;

        /// <summary>Opens the panel over the raid scene, tuning the profile the current vault is lit with.</summary>
        public void Show(VaultView view, VisibilityMask mask, PanelSettings panelSettings)
        {
            _view = view;
            _mask = mask;
            if (_tuned == null)
            {
                _source = mask.Profile;
                _tuned = Instantiate(_source);
                _tuned.name = _source.name + " (tuning)";
                _view.LightingOverride = _tuned;
                Apply(rebuild: true);
            }

            if (_doc == null)
            {
                _doc = gameObject.AddComponent<UIDocument>();
                _doc.panelSettings = panelSettings;
                _doc.sortingOrder = 100;
            }

            BuildPanel();
            _panel.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }
        }

        private void Update()
        {
            if (_rebuildAt > 0f && Time.unscaledTime >= _rebuildAt)
            {
                _rebuildAt = -1f;
                _view.RelightStatic();
                _view.BuildAtmosphere();
            }
        }

        private void OnDestroy()
        {
            if (_view != null && _view.LightingOverride == _tuned)
            {
                _view.LightingOverride = null;
            }

            if (_tuned != null)
            {
                Destroy(_tuned);
            }
        }

        // ---- Panel --------------------------------------------------------------------------

        private void BuildPanel()
        {
            VisualElement root = _doc.rootVisualElement;
            root.Clear();
            _panel = new VisualElement { name = "lighting-tuner" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 0;
            _panel.style.right = 0;
            _panel.style.bottom = 0;
            _panel.style.height = Length.Percent(48);
            _panel.style.backgroundColor = new Color(0.05f, 0.04f, 0.08f, 0.88f);
            _panel.style.paddingLeft = _panel.style.paddingRight = 24;
            _panel.style.paddingTop = 12;
            root.Add(_panel);

            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.Add(Button("Save", Save));
            bar.Add(Button("Reset", ResetToSource));
            bar.Add(Button("Silhouette", () =>
            {
                _mask.Silhouette = !_mask.Silhouette;
                Status(_mask.Silhouette ? "Silhouette: every tile Unseen, every body shown" : "Silhouette off");
            }));
            bar.Add(Button("Close", Hide));
            _panel.Add(bar);
            _status = new Label("Tuning " + _source.name);
            _status.style.fontSize = 26;
            _status.style.color = Color.white;
            _panel.Add(_status);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            _panel.Add(scroll);

            scroll.Add(Header("Decisions"));
            var fields = new List<FieldInfo>(typeof(LightingProfile).GetFields(BindingFlags.Public | BindingFlags.Instance));
            foreach (string name in Decisions)
            {
                scroll.Add(Row(typeof(LightingProfile).GetField(name)));
            }

            string group = null;
            foreach (FieldInfo f in fields)
            {
                if (Array.IndexOf(Decisions, f.Name) >= 0 || typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                {
                    continue; // decisions are above; textures (LUTs) aren't tuned with sliders
                }

                var header = f.GetCustomAttribute<HeaderAttribute>();
                if (header != null && header.header != group)
                {
                    group = header.header;
                    scroll.Add(Header(group));
                }

                VisualElement row = Row(f);
                if (row != null)
                {
                    scroll.Add(row);
                }
            }
        }

        private VisualElement Row(FieldInfo f)
        {
            string label = Nicify(f.Name);
            object value = f.GetValue(_tuned);
            var range = f.GetCustomAttribute<RangeAttribute>();
            switch (value)
            {
                case float v:
                    float max = range != null ? range.max : Mathf.Max(2f, v * 3f);
                    var slider = new Slider(label, range != null ? range.min : 0f, max) { value = v, showInputField = true };
                    slider.RegisterValueChangedCallback(e => Set(f, e.newValue));
                    return Styled(slider);
                case int i:
                    var ints = new SliderInt(label, range != null ? (int)range.min : 0, range != null ? (int)range.max : Mathf.Max(8, i * 3)) { value = i, showInputField = true };
                    ints.RegisterValueChangedCallback(e => Set(f, e.newValue));
                    return Styled(ints);
                case bool b:
                    var toggle = new Toggle(label) { value = b };
                    toggle.RegisterValueChangedCallback(e => Set(f, e.newValue));
                    return Styled(toggle);
                case Color c:
                    var box = new VisualElement();
                    box.Add(Styled(new Label(label)));
                    string[] channels = { "R", "G", "B" };
                    for (int ch = 0; ch < 3; ch++)
                    {
                        int k = ch;
                        var s = new Slider("  " + channels[ch], 0f, 1f) { value = c[k], showInputField = true };
                        s.RegisterValueChangedCallback(e =>
                        {
                            Color now = (Color)f.GetValue(_tuned);
                            now[k] = e.newValue;
                            Set(f, now);
                        });
                        box.Add(Styled(s));
                    }

                    return box;
                case Vector3 angles:
                    var vbox = new VisualElement();
                    vbox.Add(Styled(new Label(label)));
                    for (int ax = 0; ax < 2; ax++)
                    {
                        int k = ax;
                        var s = new Slider(ax == 0 ? "  Pitch" : "  Yaw", 0f, 360f) { value = angles[k], showInputField = true };
                        s.RegisterValueChangedCallback(e =>
                        {
                            Vector3 now = (Vector3)f.GetValue(_tuned);
                            now[k] = e.newValue;
                            Set(f, now);
                        });
                        vbox.Add(Styled(s));
                    }

                    return vbox;
                case Enum _:
                    var choices = new List<string>(Enum.GetNames(f.FieldType));
                    var drop = new DropdownField(label, choices, choices.IndexOf(value.ToString()));
                    drop.RegisterValueChangedCallback(e => Set(f, Enum.Parse(f.FieldType, e.newValue)));
                    return Styled(drop);
                default:
                    return null;
            }
        }

        private void Set(FieldInfo f, object value)
        {
            f.SetValue(_tuned, value);
            Apply(rebuild: false);
        }

        /// <summary>Applies the tuned values: at once for the mask, grade and light; atmosphere and pools a moment later.</summary>
        private void Apply(bool rebuild)
        {
            _mask.SetProfile(_tuned);
            _tuned.ApplyAmbient();
            Grade.Active?.ApplyProfile(_tuned);
            _rebuildAt = Time.unscaledTime + (rebuild ? 0f : 0.3f);
        }

        private void ResetToSource()
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_source), _tuned);
            Apply(rebuild: true);
            BuildPanel();
            Status("Back to " + _source.name);
        }

        private void Save()
        {
            if (Application.isEditor && SaveToAsset != null)
            {
                SaveToAsset(_tuned, _source);
                Status("Saved to " + _source.name);
                return;
            }

            string dir = Path.Combine(Application.persistentDataPath, "lighting");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, _source.name + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(_tuned, true));
            Status("Saved " + path + " (pull it with adb, then Wick > Lighting > Import Tuned Profile)");
            Debug.Log("Lighting tuned profile saved: " + path);
        }

        private void Status(string text)
        {
            _status.text = text;
        }

        // ---- Bits ------------------------------------------------------------------------------

        private static Button Button(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.fontSize = 28;
            b.style.height = 72;
            b.style.minWidth = 160;
            return b;
        }

        private static Label Header(string text)
        {
            var l = new Label(text.ToUpperInvariant());
            l.style.fontSize = 24;
            l.style.color = new Color(1f, 0.75f, 0.35f);
            l.style.marginTop = 16;
            return l;
        }

        private static T Styled<T>(T e) where T : VisualElement
        {
            e.style.fontSize = 24;
            e.style.color = Color.white;
            e.style.minHeight = 56;
            return e;
        }

        private static string Nicify(string name)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    sb.Append(' ');
                }

                sb.Append(i == 0 ? name[i] : char.ToLowerInvariant(name[i]));
            }

            return sb.ToString();
        }
    }
}
