namespace LidarBattle.Flow
{
    /// <summary>シーンをまたいで引き継ぐ値。選択シーンで書き、バトルシーンで読む。</summary>
    public static class GameSession
    {
        public static Difficulty Difficulty = Difficulty.Easy;
    }
}
