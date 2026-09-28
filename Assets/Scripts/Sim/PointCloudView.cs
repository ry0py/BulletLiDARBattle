using System.Collections.Generic;
using UndertaleLiDAR.LiDAR;
using UnityEngine;

namespace UndertaleLiDAR.Sim
{
    /// <summary>
    /// スキャン点群を小さな四角形の集合メッシュとして Game ビューに描く。
    /// この GameObject はワールド原点・無回転に置く (頂点をワールド座標で書くため)。
    /// バッファを再利用し、毎スキャンの GC アロケーションを避ける。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PointCloudView : MonoBehaviour
    {
        [SerializeField] private float _pointSizeM = 0.008f;
        [SerializeField] private Color _color = Color.cyan;

        private Mesh _mesh;
        private readonly List<Vector3> _vertices = new List<Vector3>(4096);
        private readonly List<Color> _colors = new List<Color>(4096);
        private readonly List<int> _triangles = new List<int>(6144);

        private void Awake()
        {
            _mesh = new Mesh { name = "PointCloud" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            var renderer = GetComponent<MeshRenderer>();
            if (renderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                renderer.sharedMaterial = new Material(shader);
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        public void Show(LidarScan scan, Transform sensor)
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            float h = _pointSizeM * 0.5f;
            for (int i = 0; i < scan.Count; i++)
            {
                Vector3 w = sensor.TransformPoint(scan[i].ToCartesian());
                int b = _vertices.Count;
                _vertices.Add(w + new Vector3(-h, -h, 0f));
                _vertices.Add(w + new Vector3(h, -h, 0f));
                _vertices.Add(w + new Vector3(h, h, 0f));
                _vertices.Add(w + new Vector3(-h, h, 0f));
                for (int k = 0; k < 4; k++) _colors.Add(_color);
                _triangles.Add(b);
                _triangles.Add(b + 2);
                _triangles.Add(b + 1);
                _triangles.Add(b);
                _triangles.Add(b + 3);
                _triangles.Add(b + 2);
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
        }
    }
}
