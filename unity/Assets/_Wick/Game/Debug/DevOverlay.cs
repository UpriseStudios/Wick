using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Debug overlays drawn independently of the visibility mask: a tile grid, and a tint on
    /// every tile where RaidSim.IsLit is true. With both on, the tint should cover exactly the
    /// tiles the mask shows as lit.
    /// </summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        [SerializeField] private RaidRunner _runner;
        [SerializeField] private Material _material;
        [SerializeField] private Color _litColor = new Color(0.2f, 1f, 0.4f, 0.35f);
        [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.35f);

        private Mesh _quad;
        private MaterialPropertyBlock _litBlock;
        private MaterialPropertyBlock _gridBlock;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public bool ShowGrid { get; set; }
        public bool ShowLit { get; set; }

        public void Bind(RaidRunner runner, Material material)
        {
            _runner = runner;
            _material = material;
        }

        private void Awake()
        {
            _quad = BuildQuad();
            _litBlock = new MaterialPropertyBlock();
            _litBlock.SetColor(ColorId, _litColor);
            _gridBlock = new MaterialPropertyBlock();
            _gridBlock.SetColor(ColorId, _gridColor);
        }

        private void LateUpdate()
        {
            if (_runner.Sim == null || _material == null || (!ShowGrid && !ShowLit))
            {
                return;
            }

            VaultDef vault = _runner.Vault;
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    var p = new TilePos(x, y);
                    float height = vault.TileAt(p) == TileType.Wall ? 1.03f : 0.03f;
                    Vector3 c = SimPose.TileCenter(p) + Vector3.up * height;

                    if (ShowLit && _runner.Sim.IsLit(p))
                    {
                        Graphics.DrawMesh(_quad, Matrix4x4.TRS(c, Quaternion.identity, new Vector3(0.9f, 1f, 0.9f)), _material, 0, null, 0, _litBlock);
                    }

                    if (ShowGrid)
                    {
                        DrawLine(c + new Vector3(0f, 0.01f, 0.5f), new Vector3(1f, 1f, 0.03f));
                        DrawLine(c + new Vector3(0.5f, 0.01f, 0f), new Vector3(0.03f, 1f, 1f));
                    }
                }
            }
        }

        private void DrawLine(Vector3 center, Vector3 scale)
        {
            Graphics.DrawMesh(_quad, Matrix4x4.TRS(center, Quaternion.identity, scale), _material, 0, null, 0, _gridBlock);
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "OverlayQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
