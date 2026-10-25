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

        private readonly List<EntityView> _views = new List<EntityView>();
        private readonly List<(ReadableGlow glow, TilePos tile)> _tileGlows = new List<(ReadableGlow, TilePos)>();
        private ReadableGlow _entranceGlow;
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
        }

        private void OnDisable()
        {
            _runner.Loaded -= Build;
            _runner.SimEventRaised -= Route;
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
            _mask.SetProfile(theme.Lighting);
            LightingProfile lighting = _mask.Profile;
            lighting.ApplyAmbient();
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
                    DecorView view = go.AddComponent<DecorView>();
                    view.Init(Context, d);
                    _decor.Add(view);
                }
            }

            Wick = Instantiate(_registry.Wick, _root).GetComponent<WickView>();
            Wick.Init(Context);
            Decoy = Instantiate(_registry.Decoy, _root).GetComponent<DecoyView>();
            Decoy.Init(Context);

            Built?.Invoke(this);
        }

        /// <summary>
        /// Sconces along the walls and the static warm-pool map they, torch decorations and the
        /// entrance cast; a glowing strip on every pit edge that meets non-pit ground; the entrance glow.
        /// </summary>
        private void BuildLights(VaultDef vault, ThemeDefinition theme, LightingProfile lighting, Transform tiles)
        {
            List<Sconce> sconces = StaticLights.PlaceSconces(vault, lighting.SconceSpacingTiles);
            if (theme.Sconce != null)
            {
                foreach (Sconce s in sconces)
                {
                    Vector3 face = SimPose.TileCenter(s.Wall) + SimPose.DirVector(s.Facing) * 0.5f;
                    Instantiate(theme.Sconce, face, SimPose.Facing(s.Facing), tiles);
                }
            }

            _mask.SetStaticLight(StaticLights.Compute(vault, sconces, lighting.TorchRadiusTiles));

            var glows = new GameObject("Glows").transform; // not batched: they switch on and off
            glows.SetParent(_root, false);
            if (theme.PitEdge != null)
            {
                for (int y = 0; y < vault.Height; y++)
                {
                    for (int x = 0; x < vault.Width; x++)
                    {
                        var p = new TilePos(x, y);
                        if (vault.TileAt(p) != TileType.Pit)
                        {
                            continue;
                        }

                        for (int d = 0; d < 4; d++)
                        {
                            var dir = (Dir)d;
                            TileType beside = vault.TileAt(p.Step(dir));
                            if (beside != TileType.Pit && beside != TileType.Wall)
                            {
                                GameObject edge = Instantiate(theme.PitEdge, SimPose.TileCenter(p), SimPose.Facing(dir), glows);
                                AddTileGlow(edge, ReadableKind.PitEdge, p, lighting.PitEdgeColor);
                            }
                        }
                    }
                }
            }

            if (theme.EntranceGlow != null)
            {
                GameObject entrance = Instantiate(theme.EntranceGlow, SimPose.TileCenter(vault.Entrance), Quaternion.identity, glows);
                _entranceGlow = AddTileGlow(entrance, ReadableKind.Entrance, vault.Entrance, lighting.EntranceColor);
            }
        }

        private ReadableGlow AddTileGlow(GameObject go, ReadableKind kind, TilePos tile, Color color)
        {
            ReadableGlow glow = go.GetComponent<ReadableGlow>();
            if (glow == null) // not ??: a missing component is Unity's fake null
            {
                glow = go.AddComponent<ReadableGlow>();
            }

            glow.Kind = kind;
            glow.SetColor(color);
            _tileGlows.Add((glow, tile));
            return glow;
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
