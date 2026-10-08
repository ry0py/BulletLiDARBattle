using UnityEngine;

namespace LidarBattle.Flow
{
    /// <summary>シーンをまたいで引き継ぐ値。選択シーンで書き、バトルシーンで読む。</summary>
    public static class GameSession
    {
        private const string DebugModeKey = "DebugMode";

        public static Difficulty Difficulty = Difficulty.Easy;

        /// <summary>
        /// 記録に残すだけで、今はこれで変わる動作は無い。ユーザー設定シーン（UserSettingsScene）で切り替える。
        /// アプリを閉じても残るよう PlayerPrefs に保存する（既定 true）。
        /// </summary>
        public static bool DebugMode
        {
            get => PlayerPrefs.GetInt(DebugModeKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(DebugModeKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
