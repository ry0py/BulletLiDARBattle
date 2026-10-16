using LidarBattle.Battle;
using Unity.MLAgents;
using UnityEngine;

namespace LidarBattle.Training
{
    /// <summary>
    /// 学習全体の進め方: 盤面（DodgeArea）を格子状に複製して同時に学習させ、ML-Agents を 1 フレームに 1 回だけ進める。
    /// 学習中（mlagents-learn とつながっているとき）は 1 フレームを 1/60 秒に固定して、PC が出せる最高速で回す。
    /// 盤面の数は学習中は mlagents-learn の --num-areas（YAML の env_settings.num_areas）、そうでなければ Inspector の値。
    /// ML-Agents の TrainingAreaReplicator は奥行き方向にも並べて 2D カメラで重なるので、平面に並べるこれを使う。
    /// 詳細は .claude/docs/rl-training.md。
    /// </summary>
    [DefaultExecutionOrder(-5)] // エージェントが動き出す前に盤面をそろえる
    public class DodgeTrainingRunner : MonoBehaviour
    {
        /// <summary>学習中の 1 フレームの長さ（秒）。弾の固定ステップと同じにする。</summary>
        private const float TrainingFrameSeconds = 1f / 60f;

        [Tooltip("複製する盤面 1 つ分（盤面・SOUL・弾・Timeline・エージェントをまとめたもの）")]
        [SerializeField] private GameObject _area;
        [Tooltip("Python につながっていないとき（キーボード・学習済みモデルで見るとき）の盤面の数")]
        [SerializeField] private int _editorAreas = 16;
        [Tooltip("盤面どうしのすき間（ワールド単位）。枠の外を飛ぶ弾が隣と重なりにくいように")]
        [SerializeField] private float _gap = 1.5f;
        [SerializeField] private Camera _camera;

        private void Awake()
        {
            // 物理の FixedUpdate ではなく、このスクリプトの Update で 1 フレームに 1 回だけ進める。
            var academy = Academy.Instance;
            academy.AutomaticSteppingEnabled = false;
            // 判断は 4 フレームに 1 回なので、学習済みの AI を見るとき（Python なし）も 60 fps にそろえる。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            if (academy.IsCommunicatorOn)
            {
                Time.captureDeltaTime = TrainingFrameSeconds;
                Application.targetFrameRate = -1;
                Application.runInBackground = true; // ウィンドウが裏に回っても学習を止めない
                AudioListener.volume = 0f;
            }

            Replicate(Mathf.Max(1, academy.IsCommunicatorOn ? academy.NumAreas : _editorAreas));
        }

        private void OnDestroy()
        {
            if (Academy.IsInitialized) Academy.Instance.AutomaticSteppingEnabled = true;
            Time.captureDeltaTime = 0f;
        }

        private void Update()
        {
            // mlagents-learn の time_scale（既定 20）は 1 フレームを長くするだけで SOUL の動きが粗くなるので、1 に戻す。
            if (Academy.Instance.IsCommunicatorOn) Time.timeScale = 1f;
            Academy.Instance.EnvironmentStep();
        }

        /// <summary>元の盤面を左上にして、右へ・下へ格子状に並べ、カメラを全体が映るように合わせる。</summary>
        private void Replicate(int count)
        {
            var board = _area.GetComponentInChildren<BulletBoard>();
            var cell = board.Size + Vector2.one * _gap;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            var origin = _area.transform.position;
            for (int i = 1; i < count; i++)
            {
                var offset = new Vector3(i % columns * cell.x, -(i / columns) * cell.y, 0f);
                Instantiate(_area, origin + offset, Quaternion.identity).name = $"{_area.name} {i}";
            }

            if (_camera == null) return;
            var first = (Vector2)board.transform.position;
            var center = first + new Vector2((columns - 1) * cell.x, -(rows - 1) * cell.y) * 0.5f;
            var extent = new Vector2(columns * cell.x, rows * cell.y) * 0.5f;
            _camera.transform.position = new Vector3(center.x, center.y, _camera.transform.position.z);
            _camera.orthographicSize = Mathf.Max(extent.y, extent.x / _camera.aspect);
        }
    }
}
