# ARCamera 画像向き調査: XRCpuImage → MediaPipe Image 変換

## 1. 座標系の整理

### 1.1 XRCpuImage のセンサーネイティブ形式

モバイルデバイスのカメラセンサーは **物理的に常に横向き (Landscape)** で画像を出力する。
AR Foundation が `ARCameraManager.TryAcquireLatestCpuImage()` で返す `XRCpuImage` はこのセンサーネイティブデータをそのまま保持している。

```
cpuImage.width  = センサーの横解像度 (例: 1920)
cpuImage.height = センサーの縦解像度 (例: 1080)
```

デバイスを Portrait で持っていても、`cpuImage.width > cpuImage.height` となる。

### 1.2 XRCpuImage.Convert() 出力の原点規約

`XRCpuImage.Convert()` の出力は Unity `Texture2D.LoadRawTextureData()` に直接渡せる形式になっている。
Unity Texture2D は **OpenGL 規約（左下原点、Y軸上向き）** を採用している。

```
Unity Texture2D / XRCpuImage.Convert() 出力:

row[H-1] ← 画像の上端（被写体の頭部など）
row[H-2]
...
row[1]
row[0]  ← 先頭アドレス = 画像の下端（被写体の足元など）

原点: 左下 / Y軸: 上向き
```

`WebCamera.cs` の `CopyFlippedVertically` コメントがこれを明示している:
> `GetPixels32` returns with the origin at the bottom-left (Y-axis up), but MediaPipe expects the origin at the top-left (Y-axis down).

### 1.3 MediaPipe Image の原点規約

MediaPipe Image は **標準画像規約（左上原点、Y軸下向き）** を採用している。

```
MediaPipe Image:

row[0]  ← 先頭アドレス = 画像の上端（左上が (0,0)）
row[1]
...
row[H-2]
row[H-1] ← 画像の下端（右下が (1,1)）

原点: 左上 / Y軸: 下向き
```

### 1.4 Y軸方向のミスマッチ（現行の未修正バグ）

`XRCpuImage.Convert()` の出力は左下原点だが、MediaPipe は左上原点を期待する。
**垂直方向の反転（`Transformation.MirrorX`）** が常に必要。

`WebCamera` は `CopyFlippedVertically` で明示的に対処している。
現在の `ARCamera` は `Transformation.None` または `Transformation.MirrorY` のみを使用しており、**Y軸反転が未実施**。

---

## 2. XRCpuImage.Transformation の能力と制限

```csharp
[Flags]
public enum Transformation : int
{
    None   = 0,
    MirrorX = 1,  // X軸を基準に反転 = 上下反転（垂直フリップ）
    MirrorY = 2,  // Y軸を基準に反転 = 左右反転（水平フリップ）
}
```

| Transformation | 効果 |
|---|---|
| `None` | 変換なし |
| `MirrorX` | 上下反転（Y軸補正に使用） |
| `MirrorY` | 左右反転（フロントカメラのミラー補正に使用） |
| `MirrorX \| MirrorY` | 上下左右反転 = 180°回転と等価 |

**重要な制限: 90° / 270° 回転はサポートされていない。**
Portrait モードで必要なピクセルの 90° 回転は `XRCpuImage.Transformation` だけでは実現できない。

---

## 3. 画面向き別に必要な変換

センサーネイティブ方向を **LandscapeLeft が基準** として整理する。

Portrait モードを例に取ると、センサー画像では被写体が横向きに見える（Landscape 画像の中で 90° 回転している）。

| Screen.orientation | センサー→正立 | Y反転 | フロント追加 |
|---|---|---|---|
| LandscapeLeft（基準） | 不要 | MirrorX | MirrorY |
| Portrait | **90° CW** | MirrorX | MirrorY |
| LandscapeRight | **180°** | MirrorX (= MirrorX\|MirrorY で代替可) | MirrorY |
| PortraitUpsideDown | **270° CW** | MirrorX | MirrorY |

---

## 4. TryAcquireMediaPipeImage 内での対応方法

### 方法 A: MediaPipe の `rotationDegrees` を活用（推奨）

`PoseTrackingTask` / `HandTrackingTask` / `FaceTrackingTask` はすでに `ImageProcessingOptions(rotationDegrees: 0)` を使用している。
`rotationDegrees` に実際の回転量を設定することで、**ピクセルデータを回転させることなく** MediaPipe 側に処理させることができる。

**変換方針:**
- `XRCpuImage.Convert()` はセンサーネイティブ向き (Landscape) のまま変換する
- `outputDimensions` は **向きに依らずセンサー解像度ベース**（Portrait 時に width/height を swap しない）
- `Transformation.MirrorX` を常に付与してY軸補正する
- フロントカメラには追加で `Transformation.MirrorY` を付与する
- `Screen.orientation` に応じた `rotationDegrees` を `ImageProcessingOptions` に渡す

```csharp
// TryAcquireMediaPipeImage 内での ConversionParams
var transformation = XRCpuImage.Transformation.MirrorX; // Y軸補正（常に必要）
if (_arCameraManager.currentFacingDirection == CameraFacingDirection.User)
{
    transformation |= XRCpuImage.Transformation.MirrorY; // フロントカメラのミラー
}

width  = cpuImage.width  / _downscaleFactor;
height = cpuImage.height / _downscaleFactor;
// ※ Portrait 時でも width/height は swap しない（センサーの物理寸法のまま）

var conversionParams = new XRCpuImage.ConversionParams
{
    inputRect      = new RectInt(0, 0, cpuImage.width, cpuImage.height),
    outputDimensions = new Vector2Int(width, height),
    outputFormat   = TextureFormat.RGBA32,
    transformation = transformation,
};
```

```csharp
// rotationDegrees の導出
int rotationDegrees = Screen.orientation switch
{
    ScreenOrientation.Portrait          =>  90,
    ScreenOrientation.LandscapeRight    => 180,
    ScreenOrientation.PortraitUpsideDown => 270,
    _                                   =>   0, // LandscapeLeft
};
// ※ フロントカメラでミラー反転すると回転方向が逆になる可能性がある。要実機検証。
```

**各タスクの `DetectAsync` に `rotationDegrees` を渡す変更が別途必要。**

**メリット:**
- ピクセルデータの回転コストがゼロ（MediaPipe 内部で処理）
- バッファサイズが常に `sensorWidth × sensorHeight` で固定
- 既存の `ImageProcessingOptions` の仕組みをそのまま拡張できる

**デメリット:**
- `PoseTrackingTask` / `HandTrackingTask` / `FaceTrackingTask` の API 変更が必要
- ランドマーク座標は MediaPipe が回転処理した後の正規化座標になるため、スクリーン座標への変換ロジックがシンプルになる（Portrait では Y=0 が画面上端）

---

### 方法 B: CPU でピクセルを回転（方法 A 非対応時の代替）

`XRCpuImage.Convert()` でセンサー向きに変換した後、C# のバッファ操作で 90° 回転を実施する。

```
90° CW 回転 (Landscape W×H → Portrait H×W):
  dst[x][y] = src[H-1-y][x]  (dst は H×W の新バッファ)

270° CW 回転 (Landscape W×H → Portrait H×W):
  dst[x][y] = src[y][W-1-x]
```

この場合、`outputDimensions` は Portrait 時に swap した値を使い、回転後のバッファをそのまま MediaPipe に渡す。

**メリット:**
- タスク側の API 変更不要
- MediaPipe に渡す Image の width/height が表示向きと一致する

**デメリット:**
- 追加の NativeArray バッファが必要（W×H×4 バイト）
- O(W×H) の CPU ループコストが毎フレーム発生
- MirrorX と回転の順序が複雑になる

---

### 方法 C: `ARCameraFrameEventArgs.displayMatrix` の活用

AR Foundation の `frameReceived` イベントで提供される `displayMatrix` は、
センサーの UV 座標 → スクリーン表示 UV 座標 の正確な変換行列（プラットフォーム固有の補正込み）。

この行列から回転角度を抽出し、方法 A / B に利用することができる。

```csharp
_arCameraManager.frameReceived += (args) =>
{
    _displayMatrix = args.displayMatrix; // Matrix4x4
};
```

行列から回転を抽出するには四元数化（`Quaternion.LookRotation` など）が必要で、
プラットフォームごとに符号や軸の扱いが異なるため複雑。

**採用判断:** `Screen.orientation` ベースの方法 A で概ね正確に対処できる。
`displayMatrix` は個体差や特殊センサー向きへの対応が必要になった場合の補完として検討する。

---

## 5. 推奨方針

| 判断ポイント | 推奨 |
|---|---|
| 実装コスト | **方法 A** が最小（タスク API 変更のみ） |
| 実行時パフォーマンス | **方法 A** が最良（ピクセル回転なし） |
| 座標系の整合性 | **方法 A** が明確（MediaPipe 出力が常にスクリーン座標系と一致） |

**推奨: 方法 A を採用し、以下の順で修正する。**

1. `ARCamera.TryAcquireMediaPipeImage` の `Transformation` を `MirrorX` ベースに修正（Y軸補正）
2. `PoseTrackingTask` / `HandTrackingTask` / `FaceTrackingTask` の `DetectAsync` に `rotationDegrees` パラメータを追加
3. `MotionCaptureTaskRunner` で `Screen.orientation` から `rotationDegrees` を導出してタスクに渡す
4. 実機で各向き（Portrait / LandscapeLeft / LandscapeRight / PortraitUpsideDown）のランドマーク位置を目視確認

---

## 6. 検証項目

### 6.1 Y軸反転の確認

`ImageTexture` (= `_nativePixelBuffer` → Texture2D) を RawImage に表示し、
ARCameraBackground の映像と上下の一致を目視確認する。

- **現在（`Transformation.None`）**: 上下反転して表示されるはず
- **修正後（`Transformation.MirrorX`）**: 正しく表示されるはず

### 6.2 Portrait モードの回転確認

Portrait モードで人物を映し、鼻のランドマーク Y 座標を確認する。

- `rotationDegrees = 90` 適用後: 顔正立時に鼻 Y ≈ 0.2～0.4 付近（画面上部）
- 未適用: 鼻 Y ≈ 0.5（横向き画像の中央）

### 6.3 フロントカメラのミラー確認

フロントカメラで右手を上げ、ランドマークが画面右側に現れるかを確認する（鏡像）。

- `Transformation.MirrorX | MirrorY` 適用後: 右手を上げると画面右の手ランドマークが反応
- `Transformation.MirrorX` のみ: 左右が反転して左側が反応

---

## 7. 現在の実装との差分まとめ

| 項目 | 現在の実装 | 修正後 |
|---|---|---|
| Y軸反転 | **なし（バグ）** | `MirrorX` を常に適用 |
| Portrait 回転 | width/height を swap のみ（ピクセル未回転・歪む） | `rotationDegrees = 90` を MediaPipe に渡す |
| LandscapeRight | 変換なし | `rotationDegrees = 180` |
| PortraitUpsideDown | width/height を swap のみ | `rotationDegrees = 270` |
| フロントカメラ | `MirrorY` のみ | `MirrorX \| MirrorY` |
