using UnityEngine;

namespace Wick.Game
{
    /// <summary>
    /// Puts a skin's model on Wick: the shared Animator controller goes on the model's own root (so
    /// clip paths match its bones), the body and flame get the skin's materials, and the flame bone,
    /// flame renderer and face renderer are found by the shared rig's names.
    /// </summary>
    public sealed class WickSkin : MonoBehaviour
    {
        public const string FlameBone = "flame_base";
        public const string FlameTipBone = "flame_tip";

        [SerializeField] private SkinCatalog _catalog;
        [SerializeField] private RuntimeAnimatorController _controller;
        [SerializeField] private Transform _holder;

        private GameObject _model;

        public SkinDefinition Current { get; private set; }
        public Animator Animator { get; private set; }
        public Transform Flame { get; private set; }
        public Transform FlameTip { get; private set; }
        public Renderer FlameRenderer { get; private set; }
        public SkinnedMeshRenderer Face { get; private set; }

        public SkinCatalog Catalog => _catalog;
        public RuntimeAnimatorController Controller => _controller;

        public void Bind(SkinCatalog catalog, RuntimeAnimatorController controller, Transform holder)
        {
            _catalog = catalog;
            _controller = controller;
            _holder = holder;
        }

        /// <summary>The equipped skin, unless this one already shows it.</summary>
        public void ApplyEquipped()
        {
            SkinDefinition skin = _catalog != null ? _catalog.Equipped : null;
            if (skin != null && skin != Current)
            {
                Apply(skin);
            }
        }

        public void Apply(SkinDefinition skin)
        {
            if (_model != null)
            {
                DestroySafe(_model);
            }

            Current = skin;
            _model = Instantiate(skin.Model, _holder != null ? _holder : transform, false);
            _model.name = skin.Model.name;

            Animator = _model.GetComponent<Animator>();
            if (Animator == null)
            {
                Animator = _model.AddComponent<Animator>();
            }

            Animator.runtimeAnimatorController = _controller;
            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // the clip renderer draws offscreen

            Flame = FindDeep(_model.transform, FlameBone);
            FlameTip = FindDeep(_model.transform, FlameTipBone);
            Face = null;
            FlameRenderer = null;
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                bool flame = r.name.ToLowerInvariant().Contains("flame");
                if (flame)
                {
                    FlameRenderer = r;
                    if (skin.FlameMaterial != null)
                    {
                        r.sharedMaterial = skin.FlameMaterial;
                    }

                    continue;
                }

                if (skin.BodyMaterial != null)
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = skin.BodyMaterial;
                    }

                    r.sharedMaterials = mats;
                }

                if (Face == null && r is SkinnedMeshRenderer s && s.sharedMesh != null && s.sharedMesh.GetBlendShapeIndex("fear") >= 0)
                {
                    Face = s;
                }
            }

            if (skin.FlameVfx != null && FlameTip != null)
            {
                Instantiate(skin.FlameVfx, FlameTip, false);
            }
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void DestroySafe(Object o)
        {
            if (Application.isPlaying)
            {
                Destroy(o);
            }
            else
            {
                DestroyImmediate(o);
            }
        }
    }
}
