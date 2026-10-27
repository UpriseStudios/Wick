using System;
using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Builds the vault's tiles and entity views from the ViewRegistry when a raid loads,
    /// syncs them every frame and routes sim events to the entity that raised them. It also lights
    /// the vault from the theme's LightingProfile (docs/m10-2-lighting.md): ambient, the dim main
    /// light, sconces and the static warm-pool map, and the always-readable pit edges and entrance.
    /// </summary>
    public sealed class VaultView : MonoBehaviour
    {
        [SerializeField] private RaidRunner _runner;
        [SerializeField] private ViewRegistry _registry;
        [SerializeField] private VisibilityMask _mask;
        [SerializeField] private RaidTuning _tuning;

        [Tooltip("The scene's dim directional light; the profile sets its colour, strength and angle.")]
        [SerializeField] private Light _mainLight;

        /// <summary>Lights the next vault with this profile instead of the theme's (the lighting lab, tests).</summary>
        public LightingProfile LightingOverride { get; set; }

        private readonly List<EntityView> _views = new List<EntityView>();
        private readonly List<(ReadableGlow glow, TilePos tile)> _tileGlows = new List<(ReadableGlow, TilePos)>();
        private ReadableGlow _entranceGlow;
        private Transform _atmosphere;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private readonly List<DecorView> _decor = new List<DecorView>();
        private EntityView[] _byObject = Array.Empty<EntityView>();
        private Transform _root;

        public ViewRegistry Registry => _registry;

        private float _hitStop;

        /// <summary>True while a hit-stop holds the views still (the sim keeps running).</summary>
        public bool HitStopped => _hitStop > 0f;

        public WickView Wick { get; private set; }
        public DecoyView Decoy { get; private set; }
        public ViewContext Context { get; private set; }

        /// <summary>Raised after a raid's views are built.</summary>
        public event Action<VaultView> Built;

        /// <summary>Every sim event, after the entity's own view has seen it.</summary>
        public event Action<SimEvent> SimEventRaised;

        public void Bind(RaidRunner runner, ViewRegistry registry, VisibilityMask mask, RaidTuning tuning)
        {
            _runner = runner;
            _registry = registry;
            _mask = mask;
            _tuning = tuning;
        }

        private void OnEnable()
        {
            _runner.Loaded += Build;
            _runner.SimEventRaised += Route;
            LightingQuality.Changed += OnQualityChanged;
        }

        private void OnDisable()
        {
            _runner.Loaded -= Build;
            _runner.SimEventRaised -= Route;
            LightingQuality.Changed -= OnQualityChanged;
        }

        private void OnQualityChanged(QualityTier tier)
        {
            if (Context != null)
            {
                BuildAtmosphere(); // fog, shafts and dust follow the tier
            }
        }

        private void LateUpdate()
        {
            SyncAll();
        }

        /// <summary>Refreshes the mask and moves every view to the current interpolated state.</summary>
        public void SyncAll()
        {
            if (_runner.Sim == null || Context == null)
            {
                return;
            }

            _mask.Refresh(); // visibility always follows the sim, even during a hit-stop
            if (_hitStop > 0f)
            {
                _hitStop -= Time.unscaledDeltaTime;
                if (_hitStop > 0f)
                {
                    return; // hold every view's pose; the next sync shows the sim's current state
                }

                SetAnimatorSpeed(1f);
            }

            Wick.Sync();
            Decoy.Sync();
            foreach (EntityView v in _views)
            {
                v.Sync();
                v.SyncGlows();
            }

            SyncTileGlows();

            foreach (DecorView d in _decor)
            {
                d.Sync();
            }
        }

        /// <summary>
        /// Presentation-only hit-stop: views and Wick's animator pause for `seconds` of real time.
        /// RaidRunner, the sim clock and input are untouched, so replays and hashes can't change.
        /// </summary>
        public void HitStop(float seconds)
        {
            _hitStop = Mathf.Max(_hitStop, seconds);
            SetAnimatorSpeed(0f);
        }

        private void SetAnimatorSpeed(float speed)
        {
            Animator a = Wick != null && Wick.Skin != null ? Wick.Skin.Animator : null;
            if (a != null)
            {
                a.speed = speed;
            }
        }

        public void Build(RaidRunner runner)
        {
            _hitStop = 0f;
            Clear();
            VaultDef vault = runner.Vault;
            Context = new ViewContext { Runner = runner, Vault = vault, Mask = _mask, Tuning = _tuning };
            _mask.Refresh();

            _root = new GameObject("Vault").transform;
            _root.SetParent(transform, false);

            var tiles = new GameObject("Tiles").transform;
            tiles.SetParent(_root, false);
            ThemeDefinition theme = _registry.Theme;
            _mask.SetProfile(LightingOverride != null ? LightingOverride : theme.Lighting);
            LightingProfile lighting = _mask.Profile;
            lighting.ApplyAmbient();
            Grade.Active?.ApplyProfile(lighting);
            Grade.Active?.ClearResult();
            if (_mainLight != null)
            {
                _mainLight.type = LightType.Directional;
                _mainLight.shadows = LightShadows.None;
                _mainLight.color = lighting.MainLightColor;
                _mainLight.intensity = lighting.MainLightIntensity;
                _mainLight.transform.rotation = Quaternion.Euler(lighting.MainLightAngles);
            }

            Func<TilePos, TileType> tileAt = vault.TileAt;
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    var p = new TilePos(x, y);
                    theme.Spawn(ThemeKit.Choose(vault.Width, vault.Height, tileAt, vault.Entrance, p), SimPose.TileCenter(p), tiles);
                }
            }

            BuildLights(vault, theme, lighting, tiles);
            if (Application.isPlaying)
            {
                StaticBatchingUtility.Combine(tiles.gameObject);
            }

            var entities = new GameObject("Entities").transform;
            entities.SetParent(_root, false);
            _byObject = new EntityView[vault.ObjectCount];
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                GameObject prefab = _registry.ForObject(vault.GetObject(i).Type);
                if (prefab == null)
                {
                    continue;
                }

                GameObject go = Instantiate(prefab, entities);
                EntityView view = go.GetComponent<EntityView>();
                if (view == null)
                {
                    Debug.LogError("Prefab " + prefab.name + " has no EntityView");
                    continue;
                }

                view.Init(Context, i);
                _byObject[i] = view;
                _views.Add(view);
            }

            var decor = new GameObject("Decorations").transform;
            decor.SetParent(_root, false);
            for (int i = 0; i < vault.DecorationCount; i++)
            {
                DecorationDef d = vault.GetDecoration(i);
                GameObject go = DecorationProps.Spawn(d, vault, _registry, decor);
                if (go != null)
                {
                    if (theme.ContactShadow != null && !Decorations.IsWallMounted(d.Kind))
                    {
                        Instantiate(theme.ContactShadow, go.transform, false); // hides with the prop in the dark
                    }

                    DecorView view = go.AddComponent<DecorView>();
                    view.Init(Context, d);
                    _decor.Add(view);
                }
            }

            Wick = Instantiate(_registry.Wick, _root).GetComponent<WickView>();
            Wick.Init(Context);
            Decoy = Instantiate(_registry.Decoy, _root).GetComponent<DecoyView>();
            Decoy.Init(Context);
            BuildAtmosphere();

            Built?.Invoke(this);
        }

        /// <summary>
        /// Sconces along the walls and the static warm-pool map they, torch decorations and the
        /// entrance cast; a glowing strip on every pit edge that meets non-pit ground; the entrance glow.
        /// </summary>
        private void BuildLights(VaultDef vault, ThemeDefinition theme, LightingProfile lighting, Transform tiles)
        {
            List<Sconce> sconces = VaultLights.SpawnSconces(vault, theme, lighting, tiles);
            _mask.SetStaticLight(StaticLights.Compute(vault, sconces, lighting.TorchRadiusTiles));

            var glows = new GameObject("Glows").transform; // not batched: they switch on and off
            glows.SetParent(_root, false);
            _entranceGlow = VaultLights.SpawnTileGlows(vault, theme, lighting, glows, _tileGlows);
        }

        /// <summary>
        /// The vault's air from the theme's profile and the quality tier (docs/m10-2-lighting.md, layer
        /// 7): low fog cards in pits and far corners, light shafts from a grate in each room's back
        /// wall with a little dust drifting in them, and warm motes in Wick's own light. Placed from the
        /// layout (Atmosphere); rebuilt when the tier changes. Never hides or reveals anything.
        /// </summary>
        public void BuildAtmosphere()
        {
            if (_atmosphere != null)
            {
                Kill(_atmosphere.gameObject);
            }

            _atmosphere = new GameObject("Atmosphere").transform;
            _atmosphere.SetParent(_root, false);
            VaultDef vault = Context.Vault;
            ThemeDefinition theme = _registry.Theme;
            LightingProfile p = _mask.Profile;
            QualityTier tier = LightingQuality.Tier;
            List<List<TilePos>> rooms = Atmosphere.Rooms(vault);

            if (theme.FogCard != null && LightingQuality.Fog(tier) && p.FogDensity > 0f)
            {
                foreach (TilePos spot in Atmosphere.FogSpots(vault, rooms, p.FogCardsPerRoom))
                {
                    float depth = vault.TileAt(spot) == TileType.Pit ? -0.15f : 0.12f; // pooling in a pit
                    GameObject fog = Instantiate(theme.FogCard, SimPose.TileCenter(spot) + Vector3.up * depth,
                        Quaternion.Euler(0f, (spot.X * 37 + spot.Y * 53) % 360, 0f), _atmosphere);
                    Tint(fog, p.FogColor, p.FogDensity);
                }
            }

            int shafts = Atmosphere.Scaled(p.ShaftsPerRoom, LightingQuality.ShaftShare(tier));
            if (theme.ShaftCard != null && p.ShaftStrength > 0f)
            {
                foreach (TilePos wall in Atmosphere.ShaftWalls(vault, rooms, shafts))
                {
                    // From the grate high on the wall's south face, slanting down onto the floor below.
                    Vector3 face = SimPose.TileCenter(wall) + SimPose.DirVector(Dir.S) * 0.5f;
                    GameObject shaft = Instantiate(theme.ShaftCard, face, SimPose.Facing(Dir.S), _atmosphere);
                    Tint(shaft, p.ShaftColor, p.ShaftStrength);
                    AddDust(theme, p, shaft.transform, 0.3f * LightingQuality.DustShare(tier), 0.6f);
                }
            }

            if (Wick != null)
            {
                AddDust(theme, p, Wick.transform, LightingQuality.DustShare(tier), 1f);
            }
        }

        private void AddDust(ThemeDefinition theme, LightingProfile p, Transform parent, float share, float radius)
        {
            float rate = p.DustAmount * share * 14f;
            if (theme.Dust == null || rate <= 0f)
            {
                return;
            }

            GameObject dust = Instantiate(theme.Dust, parent);
            dust.transform.localPosition = Vector3.up * 0.8f;
            dust.transform.localRotation = Quaternion.identity;
            var ps = dust.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startColor = p.DustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // motes hang in the air as Wick moves
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.radius = radius;
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            float rise = p.Dust == DustKind.Bubbles ? 0.35f : p.Dust == DustKind.Ash ? -0.08f : 0.02f;
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.6f, rise * 1.4f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            ps.Play();
        }

        private static void Tint(GameObject go, Color color, float strength)
        {
            var block = new MaterialPropertyBlock();
            Color c = color;
            c.a = strength;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, c);
                r.SetPropertyBlock(block);
            }
        }

        private static void Kill(GameObject go)
        {
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        /// <summary>Pit edges and the entrance: readable in the dark; the entrance pulses, brighter when Wick carries loot.</summary>
        private void SyncTileGlows()
        {
            LightingProfile lighting = _mask.Profile;
            foreach ((ReadableGlow glow, TilePos tile) in _tileGlows)
            {
                glow.Apply(_mask.IsVisible(tile), lighting);
            }

            if (_entranceGlow != null)
            {
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 2.2f);
                float loot = Context.Current.Wick.CarriedPiles > 0 ? lighting.EntranceLootBoost : 1f;
                Color c = lighting.EntranceColor;
                c.a = 0.55f;
                _entranceGlow.SetColor(c, pulse * loot);
            }
        }

        private void Route(SimEvent e)
        {
            if (e.EntityId > 0)
            {
                for (int i = 0; i < _byObject.Length; i++)
                {
                    if (_byObject[i] != null && _byObject[i].EntityId == e.EntityId)
                    {
                        _byObject[i].OnSimEvent(e);
                    }
                }
            }

            if (Wick != null)
            {
                Wick.OnSimEvent(e);
            }

            SimEventRaised?.Invoke(e);
        }

        private void Clear()
        {
            _views.Clear();
            _tileGlows.Clear();
            _entranceGlow = null;
            _atmosphere = null;
            _decor.Clear();
            _byObject = Array.Empty<EntityView>();
            Wick = null;
            Decoy = null;
            if (_root != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_root.gameObject);
                }
                else
                {
                    DestroyImmediate(_root.gameObject);
                }

                _root = null;
            }
        }
    }
}
