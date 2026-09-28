using UnityEngine;
using UnityEngine.Playables;

namespace LidarBattle.UI
{
    public class DialogueBehaviour : PlayableBehaviour
    {
        public string Text;

        private DialogueBox _box;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // 表示は Play 中だけ。クリップに入った最初のフレームで 1 回だけ出す。
            if (!Application.isPlaying || _box != null || info.effectiveWeight <= 0f) return;
            if (playerData is not DialogueBox box) return;

            _box = box;
            _box.Show(Text);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (_box == null) return;
            _box.Hide();
            _box = null;
        }
    }
}
