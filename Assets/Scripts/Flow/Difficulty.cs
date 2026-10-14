namespace LidarBattle.Flow
{
    public enum Difficulty { Easy, Medium, Hard }

    public static class DifficultyExtensions
    {
        /// <summary>画面に出す難易度名（来場者に分かりやすいよう、ひらがな中心）。</summary>
        public static string Label(this Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => "かんたん",
            Difficulty.Medium => "中くらい",
            _ => "むずかしい",
        };
    }
}
