"""ハートに貼った ArUco マーカーをカメラで検出し、画像内の位置を UDP で Unity に送る。

受け手は Assets/Scripts/Input/CameraInputSource.cs。
パケットは little-endian float32 x2 = 画像内の位置 (0..1、x 右向き・y 下向き)。
見失ったフレームは NaN を送る (Unity 側が「トラッカーは動いているが見失い中」と判断できるように)。

使い方: python aruco_tracker.py --camera 0 --marker-id 0
必要:   pip install opencv-contrib-python
マーカー: DICT_4X4_50。marker_id0.png を白い余白ごと印刷して貼る。
"""
import argparse
import socket
import struct

import cv2

LOST = struct.pack("<ff", float("nan"), float("nan"))


def find_marker(detector, frame, marker_id):
    """マーカー中心の画像内位置 (0..1) を返す。見つからなければ None。"""
    corners, ids, _ = detector.detectMarkers(frame)
    if ids is None:
        return None
    height, width = frame.shape[:2]
    for quad, found_id in zip(corners, ids.flatten()):
        if found_id == marker_id:
            cx, cy = quad[0].mean(axis=0)
            return float(cx / width), float(cy / height)
    return None


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--camera", type=int, default=0, help="カメラ番号")
    parser.add_argument("--marker-id", type=int, default=0, help="ハートに貼ったマーカーの ID")
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--height", type=int, default=720)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5005, help="CameraInputSource の Port と合わせる")
    parser.add_argument("--no-preview", action="store_true", help="プレビュー窓を出さない")
    args = parser.parse_args()

    capture = cv2.VideoCapture(args.camera)
    if not capture.isOpened():
        raise SystemExit(f"カメラ {args.camera} を開けません")
    capture.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    capture.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)

    detector = cv2.aruco.ArucoDetector(cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_4X4_50))
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)
    print(f"camera {args.camera} -> udp {args.host}:{args.port} (marker id {args.marker_id})", flush=True)

    while True:
        ok, frame = capture.read()
        if not ok:
            raise SystemExit("カメラからフレームを読めません")
        position = find_marker(detector, frame, args.marker_id)
        sock.sendto(struct.pack("<ff", *position) if position else LOST, target)

        if args.no_preview:
            continue
        if position:
            height, width = frame.shape[:2]
            cv2.circle(frame, (int(position[0] * width), int(position[1] * height)), 12, (0, 255, 0), 3)
        cv2.imshow("aruco_tracker (q: quit)", frame)
        if cv2.waitKey(1) & 0xFF == ord("q"):
            break


if __name__ == "__main__":
    main()
