using UnityEngine.Timeline;

namespace UndertaleLiDAR.Battle
{
    /// <summary>弾を撃つトラック。バインド先に BulletSystem を指定し、ShotClip を並べる。</summary>
    [TrackColor(1f, 0.3f, 0.3f)]
    [TrackClipType(typeof(ShotClip))]
    [TrackBindingType(typeof(BulletSystem))]
    public class ShotTrack : TrackAsset
    {
    }
}
