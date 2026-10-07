using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.UI
{
    /// <summary>
    /// 立ち絵を動かすトラック。バインド先は PortraitView。
    /// トラックを複数置くと動きが足し合わされる（例: 後半ずっとふわふわ ＋ 技のときだけ震える）。
    /// </summary>
    [TrackColor(0.9f, 0.4f, 0.7f)]
    [TrackClipType(typeof(PortraitMotionClip))]
    [TrackBindingType(typeof(PortraitView))]
    public class PortraitMotionTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            // Timeline 上で動き方がわかるよう、クリップ名を動き方の名前にそろえる。
            foreach (var clip in GetClips())
                if (clip.asset is PortraitMotionClip motion) clip.displayName = motion.Motion.ToString();
            return ScriptPlayable<PortraitMotionMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public class PortraitMotionMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // 表示は Play 中だけ（他のトラックと同じ）。
            if (!Application.isPlaying || playerData is not PortraitView view) return;

            var offset = Vector2.zero;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f) continue;
                var input = (ScriptPlayable<PortraitMotionBehaviour>)playable.GetInput(i);
                offset += input.GetBehaviour().Offset((float)input.GetTime()) * weight;
            }
            view.AddOffset(offset);
        }
    }
}
