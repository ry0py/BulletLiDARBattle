"""スコアボードを LAN に配信する。

Unity が書く plays.jsonl・replays/<id>.json（Application.persistentDataPath）と index.html を配る。
同じ PC なら http://localhost:8000/ 、別の PC からは表示される http://<IP>:8000/ を開く。
記録の編集・削除は管理画面 http://localhost:8000/admin.html から（この PC からだけ）。

    python Tools/ScoreBoard/serve.py                 # 既定の場所の plays.jsonl を配る
    python Tools/ScoreBoard/serve.py --log <path>    # 別の場所のファイルを配る
"""

import argparse
import http.server
import json
import os
import re
import shutil
import socket
import threading
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPLAY_PATH = re.compile(r"^/replays/\d{8}-\d{6}\.json$")
PLAY_API = re.compile(r"^/api/plays/(\d{8}-\d{6})$")
DIFFICULTIES = ("Easy", "Medium", "Hard")
# 管理画面と編集 API はこの PC からだけ受け付ける（スコアボードは LAN に出ているため）
LOCAL_HOSTS = {"127.0.0.1", "::1"}
LOCK = threading.Lock()
# Unity の Application.persistentDataPath（Company/Product は ProjectSettings の値）
DEFAULT_LOG = Path(os.environ.get("USERPROFILE", "~")).expanduser() / "AppData/LocalLow/DefaultCompany/LidarBattle/plays.jsonl"


def rewrite_plays(log_path: Path, play_id: str, change) -> bool:
    """plays.jsonl の id が play_id の行を change(play) に置き換える（None なら消す）。見つからなければ False。

    直前の内容は plays.jsonl.bak に残す。Unity の追記と混ざらないよう、読んですぐ書き戻す。
    """
    with LOCK:
        if not log_path.exists():
            return False
        out, found = [], False
        for line in log_path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            try:
                play = json.loads(line)
            except ValueError:
                play = None  # 壊れた行はそのまま残す
            if isinstance(play, dict) and play.get("id") == play_id:
                found = True
                play = change(play)
                if play is not None:
                    out.append(json.dumps(play, ensure_ascii=False, separators=(",", ":")))
            else:
                out.append(line)
        if not found:
            return False
        shutil.copy2(log_path, log_path.with_name(log_path.name + ".bak"))
        tmp = log_path.with_name(log_path.name + ".tmp")
        tmp.write_text("".join(line + "\n" for line in out), encoding="utf-8")
        os.replace(tmp, log_path)
        return True


def parse_edit(body: bytes) -> dict:
    """管理画面から来た変更（難易度・被弾回数・デバッグか）を確かめて返す。おかしければ ValueError。"""
    edit = json.loads(body)
    if (not isinstance(edit, dict) or edit.get("difficulty") not in DIFFICULTIES
            or type(edit.get("hits")) is not int or not 0 <= edit["hits"] <= 999
            or type(edit.get("debug")) is not bool):
        raise ValueError("bad edit")
    return {key: edit[key] for key in ("difficulty", "hits", "debug")}


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
            elif REPLAY_PATH.match(path):
                self.send_data(log_path.parent / path.lstrip("/"), "application/json", missing_ok=False)
            elif path.rstrip("/") == "/debug":
                self.send_response(302)  # デバッグ表示は index.html に ?debug を付けたもの
                self.send_header("Location", "/?debug")
                self.end_headers()
            elif path == "/admin.html" and not self.is_local():
                self.send_error(403)
            else:
                super().do_GET()

        def do_POST(self):
            play_id = self.play_api()
            if play_id is None:
                return
            try:
                edit = parse_edit(self.rfile.read(int(self.headers.get("Content-Length", 0))))
            except ValueError:
                self.send_error(400)
                return
            found = rewrite_plays(log_path, play_id, lambda play: {**play, **edit})
            if found:
                edit_replay(replays / f"{play_id}.json", edit)
            self.send_done(found)

        def do_DELETE(self):
            play_id = self.play_api()
            if play_id is None:
                return
            found = rewrite_plays(log_path, play_id, lambda play: None)
            if found:
                discard_replay(replays / f"{play_id}.json")
            self.send_done(found)

        def is_local(self) -> bool:
            return self.client_address[0] in LOCAL_HOSTS

        def play_api(self):
            """編集 API の宛先のプレイ id。この PC からの JSON の要求でなければ断って None。"""
            match = PLAY_API.match(self.path)
            if match is None:
                self.send_error(404)
            elif not self.is_local():
                self.send_error(403)
            elif self.command == "POST" and self.headers.get_content_type() != "application/json":
                self.send_error(415)  # 他のサイトからのフォーム送信で書き換えられないように
            else:
                return match.group(1)
            return None

        def send_done(self, found: bool):
            if not found:
                self.send_error(404)
                return
            self.send_response(204)
            self.end_headers()

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
    print(f"管理画面:   http://localhost:{args.port}/admin.html （この PC からだけ）")
    for ip in lan_addresses():
        print(f"別のPCから: http://{ip}:{args.port}/")

    server = http.server.ThreadingHTTPServer(("0.0.0.0", args.port), make_handler(args.log))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
