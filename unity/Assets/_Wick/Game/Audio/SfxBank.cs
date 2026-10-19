using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>One sound: a few takes picked at random, its level and a little pitch variety.</summary>
    [Serializable]
    public sealed class SfxCue
    {
        /// <summary>The asset id (sfx_snuff...), as in Art/Incoming/sfx.</summary>
        public string Id;

        public AudioClip[] Clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float Volume = 0.8f;
        [Range(0f, 0.3f)] public float PitchJitter = 0.05f;

        /// <summary>True while this cue is a generated stand-in.</summary>
        public bool StandIn;
    }

    /// <summary>Every sound effect and music layer, built by Wick > Art > Build Audio.</summary>
    [CreateAssetMenu(menuName = "Wick/Sfx Bank", fileName = "SfxBank")]
    public sealed class SfxBank : ScriptableObject
    {
        public List<SfxCue> Cues = new List<SfxCue>();

        [Header("Music layers (same key; calm and tense share a tempo)")]
        public AudioClip MusicCalm;
        public AudioClip MusicTense;
        public AudioClip MusicEscape;

        public SfxCue Find(string id)
        {
            foreach (SfxCue c in Cues)
            {
                if (c.Id == id)
                {
                    return c;
                }
            }

            return null;
        }
    }
}
