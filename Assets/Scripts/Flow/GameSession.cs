namespace LidarBattle.Flow
{
    /// <summary>シーンをまたいで引き継ぐ値。選択シーンで書き、バトルシーンで読む。</summary>
    public static class GameSession
    {
        public static Difficulty Difficulty = Difficulty.Easy;
        /// <summary>記録に残すだけで、今はこれで変わる動作は無い。本番用に切り替える手段もまだ無い。</summary>
        public static bool DebugMode = true;
    }
}
