using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>スキャナーから出る扇形のビームを回し続ける（見た目だけ）。盤面の SpriteMask で盤面の内側にだけ映る。</summary>
    public class ScanSweep : MonoBehaviour
    {
        [Tooltip("負で時計回り")]
        [SerializeField] private float _degreesPerSecond = -70f;

        private void Update() => transform.Rotate(0f, 0f, _degreesPerSecond * Time.deltaTime);
    }
}
