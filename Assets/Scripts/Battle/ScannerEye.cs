using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>スキャナー（敵）の瞳が SOUL の方を向く（見た目だけ）。</summary>
    public class ScannerEye : MonoBehaviour
    {
        [SerializeField] private SoulController _soul;
        [SerializeField] private Transform _pupil;
        [Tooltip("瞳が中心からずれる最大距離（ローカル単位）")]
        [SerializeField] private float _maxOffset = 0.12f;
        [SerializeField] private float _smoothing = 12f;

        private void Update()
        {
            var dir = _soul.Position - (Vector2)transform.position;
            var target = (Vector3)(dir.normalized * _maxOffset);
            _pupil.localPosition = Vector3.Lerp(_pupil.localPosition, target, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));
        }
    }
}
