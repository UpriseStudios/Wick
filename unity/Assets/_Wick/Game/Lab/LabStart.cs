using System.Collections;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>
    /// In the editor-only LightingLab scene (a copy of the Raid scene, never in a build): skips Home and
    /// starts the lighting lab's walk as soon as the scene is up. Open the dev menu for the tuning panel.
    /// </summary>
    public sealed class LabStart : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null; // after RaidFlow has shown Home
            RaidFlow flow = FindAnyObjectByType<RaidFlow>();
            if (flow != null)
            {
                flow.StartLightingLab();
            }
        }
    }
}
