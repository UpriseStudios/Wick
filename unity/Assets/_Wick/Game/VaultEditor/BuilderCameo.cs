using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The little builder who opens up a freshly expanded vault (decision B in docs/m10-plan.md):
    /// he walks in from the entrance, blasts a doorway through the old outer wall with a hand-held
    /// bellows, cheers and walks out. Presentation only: the expansion is already in the draft, and
    /// a tap skips straight to the end. A stand-in built from the Wick and Bellows prefabs until
    /// phase 2 art supplies char_builder.
    /// </summary>
    public sealed class BuilderCameo : MonoBehaviour
    {
        private const float TilesPerSecond = 3.5f;
        private const float BuilderScale = 0.7f;
        private static readonly Color Overalls = new Color(0.95f, 0.62f, 0.25f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private GameObject _builder;
        private Transform _bellows;
        private readonly List<GameObject> _rubble = new List<GameObject>();
        private Action _onBlast;
        private Action _onDone;
        private Coroutine _play;

        public bool Playing => _play != null;

        /// <param name="room">The old room tile beside the doorway, where he stands to blast.</param>
        /// <param name="wall">The old outer wall tile he blasts.</param>
        public void Play(VaultDraft draft, TilePos room, TilePos wall, ViewRegistry registry, GameObject rubble,
            Action onBlast, Action onDone)
        {
            Skip();
            _onBlast = onBlast;
            _onDone = onDone;
            List<TilePos> path = Path(draft, draft.Entrance, room);
            if (path == null || registry == null || registry.Wick == null)
            {
                Finish();
                return;
            }

            _builder = Instantiate(registry.Wick, transform);
            _builder.name = "Builder (stand-in)";
            _builder.transform.localScale = Vector3.one * BuilderScale;
            foreach (MonoBehaviour b in _builder.GetComponentsInChildren<MonoBehaviour>())
            {
                b.enabled = false; // a body to move around, not a raid view
            }

            Tint(_builder, Overalls);
            if (registry.Bellows != null)
            {
                _bellows = Instantiate(registry.Bellows, _builder.transform).transform;
                _bellows.localPosition = new Vector3(0f, 0.45f, 0.45f);
                _bellows.localRotation = Quaternion.identity;
                _bellows.localScale = Vector3.one * 0.45f;
            }

            _play = StartCoroutine(Run(path, room, wall, rubble));
        }

        /// <summary>Jumps to the end: the doorway opens and the builder is gone.</summary>
        public void Skip()
        {
            if (_play != null)
            {
                StopCoroutine(_play);
                _play = null;
            }

            Finish();
        }

        private IEnumerator Run(List<TilePos> path, TilePos room, TilePos wall, GameObject rubble)
        {
            yield return Walk(path);
            Face(room, wall);

            // Three pumps of the bellows, and the wall gives on the last.
            for (int pump = 0; pump < 3; pump++)
            {
                yield return Pump();
            }

            Burst(SimPose.TileCenter(wall), rubble);
            Blast();
            yield return Cheer();

            path.Reverse();
            yield return Walk(path);
            _play = null;
            Finish();
        }

        private IEnumerator Walk(List<TilePos> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = SimPose.TileCenter(path[i - 1]);
                Vector3 to = SimPose.TileCenter(path[i]);
                _builder.transform.rotation = Quaternion.LookRotation(to - from);
                for (float t = 0f; t < 1f; t += Time.deltaTime * TilesPerSecond)
                {
                    float bob = Mathf.Abs(Mathf.Sin(t * Mathf.PI)) * 0.08f;
                    _builder.transform.position = Vector3.Lerp(from, to, t) + Vector3.up * bob;
                    yield return null;
                }
            }

            _builder.transform.position = SimPose.TileCenter(path[path.Count - 1]);
        }

        private void Face(TilePos from, TilePos to)
        {
            _builder.transform.rotation = Quaternion.LookRotation(SimPose.TileCenter(to) - SimPose.TileCenter(from));
        }

        private IEnumerator Pump()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime * 3f)
            {
                float squash = 1f - Mathf.Sin(t * Mathf.PI) * 0.35f;
                if (_bellows != null)
                {
                    _bellows.localScale = new Vector3(0.45f, 0.45f * squash, 0.45f);
                }

                yield return null;
            }
        }

        private IEnumerator Cheer()
        {
            Vector3 ground = _builder.transform.position;
            for (int hop = 0; hop < 2; hop++)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime * 3f)
                {
                    _builder.transform.position = ground + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.35f;
                    yield return null;
                }
            }

            _builder.transform.position = ground;
        }

        private void Burst(Vector3 at, GameObject rubble)
        {
            if (rubble == null)
            {
                return;
            }

            var rng = new System.Random(at.GetHashCode());
            for (int i = 0; i < 10; i++)
            {
                GameObject piece = Instantiate(rubble, at + Vector3.up * 0.5f, Quaternion.identity, transform);
                piece.transform.localScale = Vector3.one * 0.15f;
                var velocity = new Vector3((float)rng.NextDouble() - 0.5f, 1f + (float)rng.NextDouble(), (float)rng.NextDouble() - 0.5f) * 3f;
                _rubble.Add(piece);
                StartCoroutine(Fly(piece, velocity));
            }
        }

        private IEnumerator Fly(GameObject piece, Vector3 velocity)
        {
            for (float t = 0f; t < 0.8f && piece != null; t += Time.deltaTime)
            {
                velocity += Physics.gravity * Time.deltaTime;
                piece.transform.position += velocity * Time.deltaTime;
                piece.transform.Rotate(velocity * 60f * Time.deltaTime);
                yield return null;
            }

            if (piece != null)
            {
                _rubble.Remove(piece);
                Destroy(piece);
            }
        }

        private void Blast()
        {
            Action blast = _onBlast;
            _onBlast = null;
            blast?.Invoke();
        }

        private void Finish()
        {
            Blast();
            if (_builder != null)
            {
                Destroy(_builder);
                _builder = null;
                _bellows = null;
            }

            foreach (GameObject piece in _rubble)
            {
                Destroy(piece);
            }

            _rubble.Clear();
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        private static void Tint(GameObject go, Color color)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Shortest walk over the draft's floor, or null if there is none.</summary>
        private static List<TilePos> Path(VaultDraft draft, TilePos from, TilePos to)
        {
            var prev = new Dictionary<TilePos, TilePos> { [from] = from };
            var queue = new Queue<TilePos>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                if (p == to)
                {
                    var path = new List<TilePos> { p };
                    while (p != from)
                    {
                        p = prev[p];
                        path.Add(p);
                    }

                    path.Reverse();
                    return path;
                }

                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (draft.InBounds(n) && draft.TileAt(n) != TileType.Wall && !prev.ContainsKey(n))
                    {
                        prev[n] = p;
                        queue.Enqueue(n);
                    }
                }
            }

            return null;
        }
    }
}
