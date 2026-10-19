using UnityEngine;

namespace Wick.Game
{
    /// <summary>
    /// One Wick skin. Every skin shares the rig (bone names and rest pose), the Animator controller
    /// and the sim footprint; only the look changes: mesh, materials, flame and snuff effects, icon.
    /// Built from Art/Incoming/char by Wick > Art > Build Skins (stand-ins until the art arrives).
    /// </summary>
    [CreateAssetMenu(menuName = "Wick/Skin", fileName = "Skin")]
    public sealed class SkinDefinition : ScriptableObject
    {
        /// <summary>The cosmetic id the economy grants (skin_default, skin_pip...).</summary>
        public string Id;

        public string DisplayName;

        /// <summary>The rigged model: shared skeleton, body SkinnedMeshRenderer with the face blend shapes, flame mesh.</summary>
        public GameObject Model;

        /// <summary>Body material (Wick/StylisedLit). Null keeps the model's own.</summary>
        public Material BodyMaterial;

        /// <summary>Flame material (Wick/Flame), for skins with a different flame colour.</summary>
        public Material FlameMaterial;

        /// <summary>Optional effect parented to the flame tip (sparks).</summary>
        public GameObject FlameVfx;

        /// <summary>Played where the flame was when Wick is snuffed.</summary>
        public GameObject SnuffVfx;

        public Sprite Icon;

        /// <summary>True for generated stand-ins, so reviews know which skins still await art.</summary>
        public bool StandIn;
    }
}
