# 強化学習で弾をよける AI（ML-Agents）

弾幕を自動でよける AI を Unity ML-Agents の強化学習（PPO）で作る。
学習は GPU 付きの別の Windows PC で行う。このドキュメントだけを見て、その PC で学習を回せることを目標にする。

## 決まっていること（2026-10-14、10-15 に追記）

| 項目 | 決めたこと | 理由・メモ |
|---|---|---|
| 目的 | **勉強用**（2026-10-15 に決定）。まず学習できるか試す。ゲーム本体（展示）には組み込まない | 強化学習の基本を学びながら進める。難しさの物差しにはしないので、AI を人に近づける（反応の遅れ・観測の誤差）ことはしない |
| 学習用 PC | Windows ＋ Unity 6000.3.4f1。このリポジトリを clone して開く | 報酬などを直してすぐ学習し直せる |
| 観測（AI に見せるもの） | **盤面のすべての弾**（位置・速度・大きさ・種類）＋自分の位置など | AI は弾の情報を正確に知っている前提（メタ認知）。ML-Agents の BufferSensor で可変個を扱う |
| 行動 | 9 方向（止まる＋8 方向）。速さはキーボードと同じ。1 秒に 15 回決める | 離散行動がいちばん学習しやすい |
| 学習の順番 | **Easy から、技 1 つずつ**。慣れたら技を混ぜる・難易度を上げる | 最初から全部ランダムだと学習が進みにくい |
| 報酬 | 被弾 1 回 -1 だけ（ほかは 0）から始める | 他の報酬は実験で足す（下の「レッスン」） |
| 手法 | PPO（ML-Agents の既定） | |
| 道順の丸暗記 | **構わない**（2026-10-15 に決定）。弾の乱数の種は固定のまま | 初めて見る弾幕でよけられるか（汎化）は目指さない |

## 強化学習の基本（このゲームに当てはめて）

- **エージェント**: 学習する主体。ここでは SOUL を動かす AI（`DodgeAgent`）。
- **観測 (observation)**: エージェントが毎回受け取る情報。自分の位置と全部の弾の情報。
- **行動 (action)**: エージェントが選ぶもの。9 方向のどれに動くか。
- **報酬 (reward)**: 行動の良し悪しを表す数。被弾したら -1。AI は「報酬の合計が大きくなる」ように行動の選び方を変えていく。
- **エピソード**: 1 回の試行。ここでは技 1 つ（8 秒）。終わったら最初から。
- **方策 (policy)**: 観測 → 行動 を決めるニューラルネットワーク。学習の結果できるのがこれ（`.onnx` ファイル）。
- **ステップ**: AI が 1 回判断すること。1 秒に 15 回なので、8 秒の技 1 回は 120 ステップ。
- **PPO**: 方策を少しずつ（一度に変えすぎないよう）更新していく学習法。ML-Agents の標準。
- **探索と活用**: 最初はでたらめに動いて色々試し（探索）、だんだん良いとわかった動きを多く選ぶ（活用）。
  TensorBoard の `Policy/Entropy` が「どれだけでたらめか」で、学習が進むと下がる。

## 作ったもの

| ファイル | 役割 |
|---|---|
| `Assets/Scripts/Training/DodgeAgent.cs` | エージェント。観測・行動・報酬。`IHeartInputSource` として SOUL を動かす |
| `Assets/Scripts/Training/DodgeArena.cs` | 盤面 1 つの場。技を流し、エピソードの始めに SOUL を置き直す |
| `Assets/Scripts/Training/DodgeTrainingRunner.cs` | 盤面 1 つ分（`DodgeArea`）を格子状に複製して同時に学習させる。ML-Agents を 1 フレーム 1 回進め、学習中は 1 フレーム = 1/60 秒に固定して全力で回す |
| `Assets/Editor/DodgeTrainingBuilder.cs` | メニュー `Tools > LiDAR Battle > Build RL Training Scene` / `Build RL Training Exe` |
| `Assets/Scenes/Test/RLTrainingScene.unity` | 学習用シーン（BattleScene のコピーから会話・人の入力源を外したもの） |
| `Assets/Timelines/Training/{Easy,Medium,Hard}/` | 本番の 12 技を難易度ごとに 8 秒にした Timeline |
| `Tools/RL/config/dodge_easy.yaml` | 学習の設定（ハイパーパラメータと、Unity 側に渡す値） |
| `Tools/RL/config/dodge_easy_curriculum.yaml` | Easy の 12 技を順に学習し、最後に全部混ぜる設定（1 つの `.onnx` で全部よけるのが目標。下の「レッスン」5） |
| `Tools/RL/results/` | 学習結果（git には入れない） |

ゲーム側の変更は最小限: `Bullet.Velocity`（弾の速度。観測用）と `SoulController.Place`（置き直し）を足しただけ。

### 観測の中身

- 自分（4 個）: 位置 x, y（盤面で 0〜1）、無敵中か（0/1）、技の進み具合（0〜1）
- 安置のシート（5 個。2026-10-15 に追加）: 出ているか（0/1）、自分からの相対位置 x, y、大きさ x, y（盤面の大きさで割る。出ていなければ 0）。
  安置の技はシートの外が一度に弾で埋まるので、弾だけ見ていては間に合わない（`easy_all_01` は安置だけまったく覚えなかった）。
- 弾 1 発ごと（9 個 × 最大 128 発、近い順）: 自分からの相対位置 x, y、速度 x, y（盤面の大きさで割る）、
  半径、種類（White / Yellow / Blue / Big の one-hot）。128 発を超えたら遠い弾から捨てる。

### 技の番号（YAML の `attack`）

| 番号 | 技 | 番号 | 技 |
|---|---|---|---|
| 0 | SafeZone（安置） | 6 | GapWallsSide |
| 1 | Corridor（縦の一本道） | 7 | Fireworks（花火→ホーミング） |
| 2 | CorridorSide（横の一本道） | 8 | FreezeAim |
| 3 | HomingFade | 9 | HomingSwarm |
| 4 | Converge（縮む輪） | 10 | SideWalls |
| 5 | GapWalls（すき間のある壁） | 11 | CurveLattice |

並びは `AttackLibraryBuilder.BattleAttacks`。技の中身は bullet-system.md。

## 学習用 PC の準備（最初の 1 回）

1. **Unity**: Unity Hub で **6000.3.4f1** を入れる。このリポジトリを clone して開く
   （初回は ML-Agents パッケージ `com.unity.ml-agents` 4.0.0 も自動で入る）。
   学習用シーンとTimeline はリポジトリに入っているので、そのまま使える。
   BattleScene や技を変えたときだけ `Tools > LiDAR Battle > Build RL Training Scene` で作り直す。
2. **Python 3.10**: ML-Agents（Python 側 `mlagents` 1.1.0）は **Python 3.10.1〜3.10.12 だけ**対応。
   Miniconda を入れて、専用の環境を作るのが簡単。
   ```powershell
   conda create -n mlagents python=3.10.12
   conda activate mlagents
   conda env config vars set PYTHONUTF8=1   # YAML の日本語コメントを読めるようにする（下の注意）
   conda activate mlagents                  # 設定を反映するため入り直す
   ```
   conda の代わりに **uv** でもよい（2026-10-15、RTX 3060 Ti の PC でこの手順で動いた）。以降の `pip install` は `uv pip install` に読み替える:
   ```powershell
   uv venv $HOME\venvs\mlagents --python 3.10.12
   & $HOME\venvs\mlagents\Scripts\Activate.ps1
   [Environment]::SetEnvironmentVariable("PYTHONUTF8", "1", "User")   # 一度だけ。ターミナルを開き直すと効く
   ```
3. **PyTorch（GPU 版）**: GPU に合うものを先に入れる。`nvidia-smi` で GPU を確認する。
   - RTX 20/30/40 系: `pip install torch~=2.2.1 --index-url https://download.pytorch.org/whl/cu121`（ML-Agents の公式手順）
   - RTX 50 系: 古い PyTorch は動かないので新しいものにする: `pip install torch --index-url https://download.pytorch.org/whl/cu128`
     （`mlagents` は `torch>=2.1.1` なので新しくても入る）
   - 確認: `python -c "import torch; print(torch.cuda.is_available())"` が `True`
4. **ML-Agents（Python 側）**:
   ```powershell
   pip install mlagents==1.1.0
   pip install "setuptools<70"
   mlagents-learn --help
   ```
   - `setuptools<70` は必須。新しい setuptools には `pkg_resources` が無く、`mlagents-learn` が
     `ModuleNotFoundError: No module named 'pkg_resources'` で落ちる（2026-10-14 に確認）。
   - `PYTHONUTF8=1` が無いと、日本語版 Windows では YAML を cp932 で読もうとして
     `There was an error decoding Config file ... Make sure your file is save using UTF-8` で落ちる（同日に確認）。
     conda を使わない場合は、毎回 PowerShell で `$env:PYTHONUTF8 = "1"` してから実行する。
   - `grpcio` のビルドで失敗したら `conda install "grpcio=1.48.2" -c conda-forge` してから入れ直す。

> 速さの目安（2026-10-14、開発用ノート PC・CPU だけで測った値）:
> - 弾幕を動かす側（Unity）: exe 1 つで実時間の約 4 倍。`--num-envs` で並列にすると CPU のコア数に応じて増える。
> - 2026-10-15、盤面を 16 面並べた（RTX 3060 Ti・i5-13400F の PC）: **Editor だけで 5000 ステップ約 18 秒**（盤面 1 つのときの約 20 倍）。
> - 学習の更新（Python）: 4096 ステップごとに約 70 秒かかった。弾を最大 128 発ぶんアテンションで見ているので重い。
>   ここは **GPU がよく効く**はず。学習用 PC で `--torch-device cuda`（既定で GPU があれば使う）と `cpu` を比べるとよい。
>   それでも遅ければ `Build RL Training Scene` の `MaxBullets`（`DodgeTrainingBuilder`）を 64 などに減らす。

## 動作確認（学習の前に）

1. Unity で `Assets/Scenes/Test/RLTrainingScene` を開いて Play。
2. Python につながっていないので、AI の代わりに**キーボード（矢印 / WASD）**で SOUL が動く（`DodgeAgent.Heuristic`）。
   盤面は `DodgeTrainingRunner` の `Editor Areas`（既定 16）の数だけ並び、全部が同じキーで動く。1 にすると 1 面だけ。
3. 8 秒ごとに技が最初からやり直されれば OK。Inspector の `DodgeAgent` の `Difficulty` / `Attack` で流す技を変えられる
   （YAML から学習するときは YAML の値が優先）。

## 学習を回す

作業フォルダはリポジトリの一番上。`conda activate mlagents` してから。

### 盤面を並べる（`num_areas`）

1 つの Unity（Editor でも exe でも）の中に盤面を何面も並べ、全部のエージェントの経験をまとめて学習する（ロボットの学習動画で
たくさんのロボットが同時に歩いているのと同じ考え方）。面の数は YAML の `env_settings.num_areas`（既定 16）か、コマンドの `--num-areas`。
盤面 1 つ分は `DodgeArea`（盤面・SOUL・弾・Timeline・エージェント）にまとまっていて、`DodgeTrainingRunner` が Play の始めに右・下へ複製し、
カメラを全体が映るよう合わせる（ML-Agents の `TrainingAreaReplicator` は奥行き方向にも並べて 2D カメラで重なるので使わない）。
exe を並べる `--num-envs` と掛け算になる（例: 16 面 × exe 4 つ = 64 面）。

### A. Unity Editor で回す（最初はこちら。様子が見える）

```powershell
mlagents-learn Tools/RL/config/dodge_easy.yaml --run-id=easy_a0_01 --results-dir=Tools/RL/results
```
「Listening on port 5004. Start training by pressing the Play button」と出たら、Unity で RLTrainingScene の **Play** を押す。
止めるときはターミナルで Ctrl+C（そこまでの `.onnx` が保存される）。
Editor は描画もするので exe より遅い。盤面を 16 面並べればこれだけでもそれなりに速い（上の速さの目安）。Unity のウィンドウが裏に回ると
遅くなるので、前に出しておく（または Preferences > General > Interaction Mode を No Throttling に）。

### B. exe で並列に回す（速い。本格的に回すとき）

1. Unity で `Tools > LiDAR Battle > Build RL Training Exe` → `Builds/RLTraining/BulletDodge.exe` ができる。
   C# を変えたら作り直す（YAML だけの変更なら作り直し不要）。
2. ```powershell
   mlagents-learn Tools/RL/config/dodge_easy.yaml --run-id=easy_a0_02 --results-dir=Tools/RL/results `
     --env=Builds/RLTraining/BulletDodge --num-envs=8 --no-graphics
   ```
   `--num-envs` は CPU のコア数の半分くらいから。初回は Windows のファイアウォールの確認が出るので許可する。

### 同じ run-id でもう一度

- 続きから: `--resume`
- 上書きしてやり直す: `--force`
- 前の結果を初期値にして別の条件で学習: `--initialize-from=<前の run-id>`（観測・行動の形が同じときだけ）

## 進み具合を見る（TensorBoard）

```powershell
tensorboard --logdir Tools/RL/results
```
ブラウザで http://localhost:6006 。見るグラフ:

| グラフ | 意味 | 良い傾向 |
|---|---|---|
| `Environment/Cumulative Reward` | 1 エピソードの報酬の合計 | 上がっていく（被弾 -1 だけなら「-被弾回数」） |
| `Dodge/Hits` | 1 回の技での被弾回数（自作の統計） | 下がっていく |
| `Dodge/NoHit` | 被弾 0 回で終えた割合 | 上がっていく |
| `Dodge/Hits by attack/<技>` | 技ごとの被弾回数（技を混ぜたとき） | 苦手な技がわかる |
| `Policy/Entropy` | 行動のでたらめさ | ゆっくり下がる。すぐ 0 近くまで落ちたら探索不足（`beta` を上げる） |
| `Losses/Value Loss` | 報酬の見積もりの誤差 | 最初上がって、のち落ち着く |

## 学習した AI を見る

1. `Tools/RL/results/<run-id>/Dodge.onnx` を `Assets/ML/` などにコピーする（Unity が読み込む）。
2. RLTrainingScene の `DodgeArea/DodgeAgent` の **Behavior Parameters > Model** にドラッグ（複製される面にも同じモデルが入る）。
3. Python を動かさずに Play → AI が動く（実時間で再生される）。1 面だけじっくり見たいときは `DodgeTrainingRunner` の `Editor Areas` を 1 に。

## パラメータの意味

### Unity 側に渡す値（`environment_parameters`。exe の作り直し不要）

| 名前 | 既定 | 意味 |
|---|---|---|
| `difficulty` | 0 | 0 Easy / 1 Medium / 2 Hard |
| `attack` | 0 | 技の番号。-1 で毎回ランダム |
| `hit_penalty` | 1.0 | 被弾 1 回の罰 |
| `alive_reward` | 0.0 | 判断 1 回ごとの「生きている」報酬 |
| `near_penalty` | 0.0 | グレイズ範囲に弾があるとき、判断 1 回ごとの罰 |
| `end_on_hit` | 0 | 1 なら被弾でエピソード終了 |
| `random_start` | 1 | 1 なら SOUL の開始位置をランダムに（0 なら真ん中） |

`alive_reward` の注意: エピソードが必ず 8 秒続く（`end_on_hit: 0`）なら、全員が同じだけもらうので意味が無い。
`end_on_hit: 1` のときだけ「長く生き残るほど得」になり意味を持つ。

### 学習の設定（`behaviors.Dodge`）

まず変えてみるのはこのあたり（1 回に 1 つだけ変えて比べる）:

| 名前 | 既定 | 上げると | 下げると |
|---|---|---|---|
| `learning_rate` | 3e-4 | 速いが不安定 | 安定するが遅い |
| `beta` | 5e-3 | 色々試す（探索）。行き詰まったら上げる | 早く決まった動きになる |
| `hidden_units` / `num_layers` | 128 / 2 | 複雑な動きを覚えられるが学習は遅い | 速いが覚えきれない |
| `gamma` | 0.99 | 先のことまで気にする | 目先の弾だけ気にする |
| `buffer_size` / `batch_size` | 4096 / 256 | 安定するが更新がまばら | 更新は多いがぶれる |
| `max_steps` | 500000 | 長く学習する | |

ほかの値の意味は YAML のコメントと ML-Agents の公式ドキュメント（Training Configuration File）。

## レッスン（この順に進める）

YAML は実験ごとにコピーして（例 `dodge_easy_a5.yaml`）、run-id に条件を入れると比べやすい。

1. **まず 1 つ学習**: Easy・技 0（SafeZone）・既定の値で 20 万ステップほど。報酬が上がるのを確かめ、`.onnx` を入れて動きを見る。
2. **報酬を変えて比べる**（同じ技で）:
   - a. 既定（被弾 -1 だけ）
   - b. `near_penalty: 0.01`（弾から距離を取るようになるか）
   - c. `end_on_hit: 1`, `alive_reward: 0.01`（「死んだら終わり」にすると学習は速いか）
   TensorBoard では `Dodge/Hits` で比べる（報酬の合計は設定ごとに意味が違うので直接比べない）。
3. **ハイパーパラメータを 1 つずつ変える**: `beta`、`learning_rate`、`hidden_units`。
4. **ほかの技**: Easy の 1〜11 を 1 つずつ。ホーミング（3, 7, 9）や縮む輪（4）が難しいはず。
5. **技を混ぜる**: Easy で `attack: -1`。前の結果から `--initialize-from` で始めてもよい。`Dodge/Hits by attack/` で苦手な技を見る。
   **今の本命（2026-10-15）**: `dodge_easy_curriculum.yaml` で、技 0〜11 を 20 万ステップずつ順に学習し、最後の 120 万ステップは全部を混ぜる
   （合計 360 万ステップ、1 つの `.onnx`）。次の技へは成績に関係なくステップ数で移る（`measure: progress`）。
   順に学習するだけだと前の技を忘れやすいので、最後に混ぜておさらいする。学習率は後の技でも下がらないよう `constant`。
   今どの技かは TensorBoard の `Environment/Lesson Number/attack`、技ごとの成績は `Dodge/Hits by attack/<技>`。
   1 技あたりのステップ数を変えたら `max_steps` と各 `threshold`（= そこまでのステップ数 / `max_steps`）も直す。
   - `easy_all_01`（256×2 層、シートを見せない）: 約 55 分。技 1 つずつなら安置以外は被弾 0.0〜0.2 回まで下がったが、
     安置は 2 回のまま、混ぜた段階でホーミング系（3, 9）と横の壁（6）を忘れた。全体で技 1 回あたり 0.77 回・被弾 0 回は 54%。
   - `easy_all_02`: シートを観測に足し、ネットワークを 512×3 層に。
6. **難易度を上げる**: Medium / Hard。カリキュラム学習（うまくなったら自動で次へ）は次のように書く:
   ```yaml
   environment_parameters:
     attack: -1
     difficulty:
       curriculum:
         - name: Easy
           completion_criteria: { measure: reward, behavior: Dodge, signal_smoothing: true, min_lesson_length: 200, threshold: -0.5 }
           value: 0
         - name: Medium
           completion_criteria: { measure: reward, behavior: Dodge, signal_smoothing: true, min_lesson_length: 200, threshold: -0.5 }
           value: 1
         - name: Hard
           value: 2
   ```

## 注意・制約

- **time_scale は必ず 1**（YAML の `engine_settings`）。速さは「1 フレーム = 1/60 秒に固定して全力で回す」ことで出している。
  ML-Agents の既定の 20 だと 1 フレームが長くなり SOUL の動きが粗くなるので、`DodgeTrainingRunner` が学習中は 1 に戻す。
- 技の弾は乱数の種が固定（`ShotClip` の `_useFixedSeed`）なので、自機狙い以外は毎回同じ弾幕になる。
  AI が「道順を丸暗記」しやすい。`random_start: 1` で少し防いでいる。本当によけられるかは、学習に使っていない技で試すとわかる。
- AI は弾の位置・速度を正確に知っていて、反応の遅れも無いので、人より有利。速さだけはキーボードと同じ。
- 観測・行動の形（`DodgeAgent` の観測の数、`BufferSensor` の大きさ、行動の数）を変えると、前の `.onnx` は使えない。
- 学習用シーンは BattleScene のコピーなので、BattleScene や技を変えたら `Build RL Training Scene` で作り直す。

## まだやらないこと

- ゲーム本体への組み込み（展示のデモ・スコアボードに AI の記録）。やるなら `DodgeAgent` は `IHeartInputSource` なので、
  `HeartInputSelector` の優先順に足せば SOUL を動かせる。
