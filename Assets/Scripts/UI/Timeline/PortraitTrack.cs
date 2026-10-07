using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.UI
{
    /// <summary>立ち絵の表情を切り替えるトラック。クリップの無いところはベース。バインド先は PortraitView。</summary>
    [TrackColor(1f, 0.6f, 0.3f)]
    [TrackClipType(typeof(PortraitClip))]
    [TrackBindingType(typeof(PortraitView))]
    public class PortraitTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            // Timeline 上で表情がわかるよう、クリップ名を表情名にそろえる。
            foreach (var clip in GetClips())
                if (clip.asset is PortraitClip portrait) clip.displayName = portrait.Expression.ToString();
            return ScriptPlayable<PortraitMixerBehaviour>.Create(graph, inputCount);
        }
    }

    /// <summary>
    /// いま掛かっているクリップの表情を出す。クリップの出入りを個別に拾うと、隣り合うクリップで
    /// 「前のクリップの終了」と「次のクリップの開始」の順が決まらないため、トラック全体で 1 つに決める。
    /// </summary>
    public class PortraitMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // 表示は Play 中だけ（DialogueTrack と同じ）。
            if (!Application.isPlaying || playerData is not PortraitView view) return;

            var expression = PortraitExpression.Base;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInputWeight(i) <= 0f) continue;
                expression = ((ScriptPlayable<PortraitBehaviour>)playable.GetInput(i)).GetBehaviour().Expression;
            }
            if (expression != view.Expression) view.Show(expression);
        }
    }
}
