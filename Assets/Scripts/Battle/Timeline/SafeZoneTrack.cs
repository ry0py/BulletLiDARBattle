using UnityEngine.Timeline;

namespace LidarBattle.Battle
{
    /// <summary>安置のシートを出すトラック。バインド先は SafeZoneView。</summary>
    [TrackColor(0.3f, 1f, 0.45f)]
    [TrackClipType(typeof(SafeZoneClip))]
    [TrackBindingType(typeof(SafeZoneView))]
    public class SafeZoneTrack : TrackAsset
    {
    }
}
