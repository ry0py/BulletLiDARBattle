"""スコアボードを LAN に配信する。

Unity が書く plays.jsonl・replays/<id>.json・live.json（Application.persistentDataPath）と index.html を配る。
同じ PC なら http://localhost:8000/ 、別の PC からは表示される http://<IP>:8000/ を開く。
LiDAR の点群（難易度選択中とプレイ中の視界）は別の PC で http://<IP>:8000/live を開いて出す（live.html）。
記録の編集・削除・仮データの作成は管理画面 http://localhost:8000/admin.html （別の PC からは http://<IP>:8000/admin.html）から。

    python Tools/ScoreBoard/serve.py                 # 既定の場所の plays.jsonl を配る
    python Tools/ScoreBoard/serve.py --log <path>    # 別の場所のファイルを配る
"""

import argparse
import http.server
import json
import math
import os
import random
import re
import shutil
import socket
import threading
import time
from datetime import datetime, timedelta
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPLAY_PATH = re.compile(r"^/replays/\d{8}-\d{6}\.json$")
PLAY_ID = re.compile(r"^\d{8}-\d{6}$")
PLAY_API = re.compile(r"^/api/plays/(\d{8}-\d{6}|new|delete)$")  # id は編集・削除、new は仮データ、delete はまとめて削除
DIFFICULTIES = ("Easy", "Medium", "Hard")
LOCK = threading.Lock()
# Unity の Application.persistentDataPath（Company/Product は ProjectSettings の値）
# live.json（難易度選択中・プレイ中の点群）がこれより古ければゲームが動いていないとみなす（Unity が落ちたときなど）
LIVE_STALE_SECONDS = 3
IDLE = b'{"state":"idle"}'
DEFAULT_LOG = Path(os.environ.get("USERPROFILE", "~")).expanduser() / "AppData/LocalLow/DefaultCompany/LidarBattle/plays.jsonl"


def rewrite_plays(log_path: Path, ids: set, change) -> set:
    """plays.jsonl の id が ids に入る行を change(play) に置き換える（None なら消す）。見つかった id を返す。

    直前の内容は plays.jsonl.bak に残す。Unity の追記と混ざらないよう、読んですぐ書き戻す。
    """
    with LOCK:
        if not log_path.exists():
            return set()
        out, found = [], set()
        for line in log_path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            try:
                play = json.loads(line)
            except ValueError:
                play = None  # 壊れた行はそのまま残す
            if isinstance(play, dict) and play.get("id") in ids:
                found.add(play["id"])
                play = change(play)
                if play is not None:
                    out.append(json.dumps(play, ensure_ascii=False, separators=(",", ":")))
            else:
                out.append(line)
        if not found:
            return found
        shutil.copy2(log_path, log_path.with_name(log_path.name + ".bak"))
        tmp = log_path.with_name(log_path.name + ".tmp")
        tmp.write_text("".join(line + "\n" for line in out), encoding="utf-8")
        os.replace(tmp, log_path)
        return found


def parse_edit(edit) -> dict:
    """管理画面から来た値（難易度・被弾回数・デバッグか）を確かめて返す。おかしければ ValueError。"""
    if (not isinstance(edit, dict) or edit.get("difficulty") not in DIFFICULTIES
            or type(edit.get("hits")) is not int or not 0 <= edit["hits"] <= 999
            or type(edit.get("debug")) is not bool):
        raise ValueError("bad edit")
    return {key: edit[key] for key in ("difficulty", "hits", "debug")}


def parse_ids(body) -> set:
    """まとめて削除するプレイの id。おかしければ ValueError。"""
    ids = body.get("ids") if isinstance(body, dict) else None
    if not isinstance(ids, list) or not ids or not all(isinstance(i, str) and PLAY_ID.match(i) for i in ids):
        raise ValueError("bad ids")
    return set(ids)


# 仮データの盤面（Unity の BulletBoard と同じ）と、軌跡の間隔・長さ
TEST_BOARD = [-2.5, -3.4, 5.0, 3.2]
TEST_PATH_INTERVAL, TEST_SECONDS = 0.1, 60


def create_test_play(log_path: Path, fields: dict) -> str:
    """管理画面からの仮データ。今の時刻で 1 プレイを足す。カードと QR を試せるよう、
    それらしい軌跡と被弾の位置も作って replays/<id>.json に置く。作った id を返す。"""
    replays = log_path.parent / "replays"
    with LOCK:
        replays.mkdir(parents=True, exist_ok=True)
        text = log_path.read_text(encoding="utf-8") if log_path.exists() else ""
        now = datetime.now().replace(microsecond=0)
        while f"{now:%Y%m%d-%H%M%S}" in text or (replays / f"{now:%Y%m%d-%H%M%S}.json").exists():
            now += timedelta(seconds=1)  # id（秒まで）が重ならないように
        play_id = f"{now:%Y%m%d-%H%M%S}"
        head = {"id": play_id, "time": now.isoformat(), **fields}

        # ゆらゆら動く軌跡（盤面の中で 2 つの周期を重ねる）
        bx, by, bw, bh = TEST_BOARD
        a, b = random.uniform(0.15, 0.35), random.uniform(0.2, 0.45)
        steps = int(TEST_SECONDS / TEST_PATH_INTERVAL)
        path = [[round(bx + bw * (0.5 + 0.4 * math.sin(a * i * TEST_PATH_INTERVAL * 2)), 2),
                 round(by + bh * (0.5 + 0.4 * math.sin(b * i * TEST_PATH_INTERVAL * 2 + 1)), 2)] for i in range(steps)]
        hit_events = []
        for t in sorted(random.uniform(0, TEST_SECONDS) for _ in range(fields["hits"])):
            x, y = path[min(steps - 1, int(t / TEST_PATH_INTERVAL))]
            hit_events.append({"t": round(t, 2), "x": x, "y": y, "bullet": "Test", "pattern": "Test"})

        replay = {**head, "board": TEST_BOARD, "hitEvents": hit_events, "pathInterval": TEST_PATH_INTERVAL, "path": path}
        (replays / f"{play_id}.json").write_text(json.dumps(replay, separators=(",", ":")) + "\n", encoding="utf-8")
        with log_path.open("a", encoding="utf-8") as f:  # Unity と同じく、詳細を書いてから要約を足す
            f.write(json.dumps(head, ensure_ascii=False, separators=(",", ":")) + "\n")
    return play_id


def edit_replay(replay: Path, edit: dict):
    """カードは replays/<id>.json から描くので、難易度などをそちらにも反映する（被弾の記録 hitEvents は消さない）。"""
    if not replay.exists():
        return
    with LOCK:
        data = json.loads(replay.read_text(encoding="utf-8"))
        data.update(edit)
        replay.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def discard_replay(replay: Path):
    """消さずに replays/deleted/ へ移す（間違えて消したときに戻せるように）。"""
    if replay.exists():
        trash = replay.parent / "deleted"
        trash.mkdir(exist_ok=True)
        os.replace(replay, trash / replay.name)


def make_handler(log_path: Path):
    replays = log_path.parent / "replays"

    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, directory=str(HERE), **kwargs)

        def do_GET(self):
            path = self.path.split("?")[0]
            if path == "/plays.jsonl":
                self.send_data(log_path, "application/x-ndjson", missing_ok=True)
            elif path == "/live.json":
                self.send_live(log_path.parent / "live.json")
            elif REPLAY_PATH.match(path):
                self.send_data(log_path.parent / path.lstrip("/"), "application/json", missing_ok=False)
            elif path.rstrip("/") == "/debug":
                self.send_response(302)  # デバッグ表示は index.html に ?debug を付けたもの
                self.send_header("Location", "/?debug")
                self.end_headers()
            elif path.rstrip("/") == "/live":
                self.send_response(302)  # LiDAR の視界は live.html
                self.send_header("Location", "/live.html")
                self.end_headers()
            elif path.rstrip("/") == "/admin":
                self.send_response(302)  # 管理画面は admin.html
                self.send_header("Location", "/admin.html")
                self.end_headers()
            else:
                super().do_GET()

        def do_POST(self):
            target = self.play_api()
            if target is None:
                return
            try:
                body = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))))
                if target == "new":
                    fields = parse_edit(body)
                elif target == "delete":
                    ids = parse_ids(body)
                else:
                    edit = parse_edit(body)
            except ValueError:
                self.send_error(400)
                return

            if target == "new":
                self.send_done(True, {"id": create_test_play(log_path, fields)})
            elif target == "delete":
                self.send_done(bool(self.delete_plays(ids)))
            else:
                found = rewrite_plays(log_path, {target}, lambda play: {**play, **edit})
                if found:
                    edit_replay(replays / f"{target}.json", edit)
                self.send_done(bool(found))

        def do_DELETE(self):
            target = self.play_api()
            if target is None:
                return
            if not PLAY_ID.match(target):
                self.send_error(405)
                return
            self.send_done(bool(self.delete_plays({target})))

        def delete_plays(self, ids: set) -> set:
            found = rewrite_plays(log_path, ids, lambda play: None)
            for play_id in found:
                discard_replay(replays / f"{play_id}.json")
            return found

        def play_api(self):
            """編集 API の宛先（プレイ id か new / delete）。JSON の要求でなければ断って None。"""
            match = PLAY_API.match(self.path)
            if match is None:
                self.send_error(404)
            elif self.command == "POST" and self.headers.get_content_type() != "application/json":
                self.send_error(415)  # 他のサイトからのフォーム送信で書き換えられないように
            else:
                return match.group(1)
            return None

        def send_done(self, found: bool, result: dict = None):
            if not found:
                self.send_error(404)
                return
            if result is None:
                self.send_response(204)
                self.end_headers()
                return
            body = json.dumps(result).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def send_data(self, file: Path, content_type: str, missing_ok: bool):
            if not file.exists() and not missing_ok:
                self.send_error(404)
                return
            body = file.read_bytes() if file.exists() else b""
            self.send_response(200)
            self.send_header("Content-Type", f"{content_type}; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def send_live(self, file: Path):
            """難易度選択中・プレイ中の様子。無い・古いときは待機扱い。
            Unity が置き換えている最中で読めなければ 503（live.html は前の表示のままにする）。"""
            try:
                fresh = time.time() - file.stat().st_mtime < LIVE_STALE_SECONDS
            except OSError:
                fresh = False
            try:
                body = file.read_bytes() if fresh else IDLE
            except OSError:
                self.send_error(503)
                return
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def end_headers(self):
            self.send_header("Cache-Control", "no-store")
            super().end_headers()

        def log_message(self, format, *args):
            pass  # ポーリングのたびに出ると邪魔なので黙らせる

    return Handler


def lan_addresses():
    try:
        return sorted({info[4][0] for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET)})
    except OSError:
        return []


def main():
    parser = argparse.ArgumentParser(description="LiDAR Battle スコアボード")
    parser.add_argument("--port", type=int, default=8000)
    parser.add_argument("--log", type=Path, default=DEFAULT_LOG, help="plays.jsonl の場所")
    args = parser.parse_args()

    print(f"記録ファイル: {args.log}" + ("" if args.log.exists() else "（まだ無い。最初のプレイで作られる）"))
    print(f"このPC:     http://localhost:{args.port}/")
    print(f"管理画面:   http://localhost:{args.port}/admin.html")
    for ip in lan_addresses():
        print(f"別のPCから: http://{ip}:{args.port}/ （管理画面は http://{ip}:{args.port}/admin.html）")

    server = http.server.ThreadingHTTPServer(("0.0.0.0", args.port), make_handler(args.log))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
