using LidarBattle.Config;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// 実機の検出器の組み立て (バトルの LidarInputSource と LiDAR 確認シーンの LidarLiveView で共通)。
    /// 円柱の検出 → 盤面の外なら未検出 → 平滑化 の順。盤面の範囲は呼び出し側が region.Region に入れる。
    /// </summary>
    public static class LidarTrackerChain
    {
        public static SmoothedTracker Create(LidarSettings settings, out RegionFilterTracker region)
        {
            var circle = new CircleFitTracker(settings.HeartRadiusM, settings.CircleToleranceM,
                settings.MinCirclePoints, settings.MinCircleScore, settings.FitIterations,
                settings.StepToAngleRad(settings.StartStep), settings.StepToAngleRad(settings.EndStep));
            region = new RegionFilterTracker(circle);
            return new SmoothedTracker(region, settings.FilterMinCutoffHz, settings.FilterBeta,
                settings.FilterDerivCutoffHz, settings.DeadbandM, settings.MaxJumpM, settings.MaxHoldFrames);
        }
    }
}
