using System;
using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Builds the vault's tiles and entity views from the ViewRegistry when a raid loads,
    /// syncs them every frame and routes sim events to the entity that raised them.
    /// </summary>
    public sealed class VaultView : MonoBehaviour
    {
        [SerializeField] private RaidRunner _runner;
        [SerializeField] private ViewRegistry _registry;
        [SerializeField] private VisibilityMask _mask;
        [SerializeField] private RaidTuning _tuning;

        private readonly List<EntityView> _views = new List<EntityView>();
        private EntityView[] _byObject = Array.Empty<EntityView>();
        private Transform _root;

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

            _mask.Refresh();
            Wick.Sync();
            Decoy.Sync();
            foreach (EntityView v in _views)
            {
                v.Sync();
            }
        }

        public void Build(RaidRunner runner)
        {
            Clear();
            VaultDef vault = runner.Vault;
            Context = new ViewContext { Runner = runner, Vault = vault, Mask = _mask, Tuning = _tuning };
            _mask.Refresh();

            _root = new GameObject("Vault").transform;
            _root.SetParent(transform, false);

            var tiles = new GameObject("Tiles").transform;
            tiles.SetParent(_root, false);
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    var p = new TilePos(x, y);
                    GameObject prefab = _registry.ForTile(vault.TileAt(p));
                    if (prefab != null)
                    {
                        Instantiate(prefab, SimPose.TileCenter(p), Quaternion.identity, tiles);
                    }
                }
            }

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

            Wick = Instantiate(_registry.Wick, _root).GetComponent<WickView>();
            Wick.Init(Context);
            Decoy = Instantiate(_registry.Decoy, _root).GetComponent<DecoyView>();
            Decoy.Init(Context);

            Built?.Invoke(this);
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
