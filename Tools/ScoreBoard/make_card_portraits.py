"""記録カードに載せる立ち絵の小さい版を作る。

元はゲームの立ち絵 Assets/Art/Portraits/<難易度>/<表情>.png（難易度とキャラの対応はゲームと同じ）。
透明な余白を切り落として縮め、card/portraits/ に置く。カードはその難易度の表情から 1 つをランダムに選ぶ。
立ち絵を差し替えたら実行し、card/portraits/ をコミットして push する（公開ページに載る）。

    python Tools/ScoreBoard/make_card_portraits.py
"""

import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Art/Portraits"
OUT = Path(__file__).resolve().parent / "card/portraits"
DIFFICULTIES = ("Easy", "Medium", "Hard")
MAX_SIZE = 256  # カード（幅 1080）の上で 190px 四方ほどに描く


def main():
    OUT.mkdir(exist_ok=True)
    for old in OUT.glob("*.png"):
        old.unlink()

    portraits = {}
    for difficulty in DIFFICULTIES:
        names = []
        for src in sorted((SOURCE / difficulty).glob("*.png")):
            image = Image.open(src).convert("RGBA")
            image = image.crop(image.getchannel("A").getbbox())
            image.thumbnail((MAX_SIZE, MAX_SIZE), Image.LANCZOS)
            name = f"{difficulty}-{src.stem}.png"
            image.save(OUT / name, optimize=True)
            names.append(name)
        portraits[difficulty] = names
        print(difficulty, names)

    (OUT / "list.js").write_text(
        "// make_card_portraits.py が作る。手で書き換えない。\n"
        f"const PORTRAITS = {json.dumps(portraits, ensure_ascii=False)};\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
