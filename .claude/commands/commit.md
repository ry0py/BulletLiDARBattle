---
description: 変更内容の diff を読んでコミットメッセージを考え、コミットする
argument-hint: "[補足（任意）: メッセージに含めたい意図など]"
allowed-tools: Bash(git status:*), Bash(git diff:*), Bash(git log:*), Bash(git add:*), Bash(git commit:*), Bash(git show:*)
---

現在の変更をコミットしてください。テスト・ビルド・コンパイル確認は不要です。
ユーザーの補足（あれば）: $ARGUMENTS

## 手順

1. 状況把握（並列で実行してよい）
   - `git status --short`
   - `git diff`（未ステージ）と `git diff --cached`（ステージ済み）
   - `git log --oneline -10`（メッセージの言語・粒度を合わせる参考）
2. **diff を実際に読む**。ファイル名や統計だけで判断しない。
   - `.unity` / `.prefab` / `.asset` など巨大な YAML は `git diff --stat` と
     `git diff --patience` で「何の GameObject / フィールドが増減したか」を把握する
     （`m_Name:` や変更されたフィールド名を拾う）。
   - 未追跡ファイルは中身を確認する。
   - 目的が分からない変更があれば推測で書かず、メッセージ作成前にユーザーに確認する。
3. コミット対象を決める
   - 基本は全変更を対象にする（`git add -A`）。
   - ただし秘密情報（パスワード・トークン・`.env` 等）や、`Library/`・`Temp/`・`Logs/`・
     `*.csproj`/`*.slnx` などの生成物が混ざっていれば含めず、ユーザーに伝える。
   - 無関係な変更が複数の目的に分かれている場合は、分割コミットを提案してから進める。
4. コミットメッセージを考える
   - **日本語**。1 行目は「何をなぜ変えたか」を 50 字程度で要約（例: `ステータス UI とコマンドメニューを削除`）。
   - 変更が複数にわたる場合は空行の後に箇条書きで主な変更点を書く（層・ファイル単位で簡潔に）。
   - `wip` や `いろいろ修正` のような中身の無いメッセージにしない。
   - 末尾に空行を挟んで次の行を付ける:
     `Co-Authored-By: Claude <noreply@anthropic.com>`
5. コミットする
   - メッセージは HEREDOC で渡す（`git commit -F - <<'EOF' ... EOF`）。
   - `--no-verify` や `--amend` は使わない。push もしない。
   - フックで失敗したら原因を報告し、勝手に回避しない。
6. `git log --oneline -1` と `git status --short` で結果を確認し、
   コミットしたメッセージと対象ファイルの要約をユーザーに報告する。
