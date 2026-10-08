using UnityEngine.Timeline;

namespace LidarBattle.Battle
{
    /// <summary>弾を撃つトラック。バインド先に BulletSystem を指定し、ShotClip（と弾を消す ClearClip）を並べる。</summary>
    [TrackColor(1f, 0.3f, 0.3f)]
    [TrackClipType(typeof(ShotClip))]
    [TrackClipType(typeof(ClearClip))]
    [TrackBindingType(typeof(BulletSystem))]
    public class ShotTrack : TrackAsset
    {
    }
}
