"""スコアボードを LAN に配信する。

Unity が書く plays.jsonl・replays/<id>.json（Application.persistentDataPath）と index.html を配るだけ。
同じ PC なら http://localhost:8000/ 、別の PC からは表示される http://<IP>:8000/ を開く。

    python Tools/ScoreBoard/serve.py                 # 既定の場所の plays.jsonl を配る
    python Tools/ScoreBoard/serve.py --log <path>    # 別の場所のファイルを配る
"""

import argparse
import http.server
import os
import re
import socket
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPLAY_PATH = re.compile(r"^/replays/\d{8}-\d{6}\.json$")
# Unity の Application.persistentDataPath（Company/Product は ProjectSettings の値）
DEFAULT_LOG = Path(os.environ.get("USERPROFILE", "~")).expanduser() / "AppData/LocalLow/DefaultCompany/LidarBattle/plays.jsonl"


def make_handler(log_path: Path):
    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, directory=str(HERE), **kwargs)

        def do_GET(self):
            path = self.path.split("?")[0]
            if path == "/plays.jsonl":
                self.send_data(log_path, "application/x-ndjson", missing_ok=True)
            elif REPLAY_PATH.match(path):
                self.send_data(log_path.parent / path.lstrip("/"), "application/json", missing_ok=False)
            else:
                super().do_GET()

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
    for ip in lan_addresses():
        print(f"別のPCから: http://{ip}:{args.port}/")

    server = http.server.ThreadingHTTPServer(("0.0.0.0", args.port), make_handler(args.log))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
