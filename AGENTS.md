# Solarmix — Spatial Audio Mixer

## 專案概述
Unity 6 (6000.4.3f1) 空間音訊混音器。以太陽系為視覺介面，每個行星是一個 FM 合成器音源，透過 MeshRIR 空間化引擎輸出雙耳立體聲。目標平台：iOS、Android、macOS（React 網頁版規劃中）。

**Active branch:** `meshRIR`
**Repo:** https://github.com/panstudiollrl-dev/Solarmix-spatial-audio

---

## 核心架構

### 音訊鏈
```
FMSynthesizer (per planet)
  → IPlanetSpatializer.ProcessSample()
      → MeshRIRSpatializer (雙耳空間化 + flyby 效果)
  → SunAudioListener (AudioListener on camera)
```

### 主要腳本

| 檔案 | 角色 |
|------|------|
| `Assets/Scripts/FMSynthesizer.cs` | FM 合成器，每個行星一個實例。輸出 mono 樣本給 spatializer。包含 Doppler、LFO、per-planet 音色定義 |
| `Assets/Scripts/MeshRIRSpatializer.cs` | 雙耳空間化引擎。IDW 插值 + 近場 flyby 效果 + 壓縮限制器 |
| `Assets/Scripts/SolarSystemUI.cs` | 全部 UI 邏輯。行星選單、Tune 面板、SPACE 滑桿、Solo/Mute |
| `Assets/Scripts/SolarSystemManager.cs` | 管理所有行星實例（`Planets` / `Synths` lists）|
| `Assets/Scripts/CameraController.cs` | 軌道相機。`static Blocked` flag 防止拖 UI 時旋轉相機 |
| `Assets/Scripts/PlanetOrbit.cs` | 行星軌道邏輯（circle / fig8 / rose / lissajous）|
| `Assets/Scripts/IPlanetSpatializer.cs` | 介面：`void ProcessSample(float mono, out float left, out float right)` |
| `Assets/Scripts/SpatialAudioNpy.cs` | 讀取 MeshRIR .npy 資料集（StreamingAssets/MeshRIR/）|
| `Assets/Editor/MobileBuild.cs` | batchmode build script：`BuildIOS` / `BuildAndroid` / `BuildWebGL` |

---

## MeshRIRSpatializer 重點參數

```csharp
[Range(0,1)] float rate        // 方向追蹤速度
[Range(0,1)] float depth       // Reverb 殘響長度
[Range(0,1)] float energy      // 濕訊號量（Reverb）
[Range(0,1)] float material    // 吸音係數（Damp）：0=反射，1=吸音
[Range(0,1)] float density     // 擴散殘響密度
[Range(0,1)] float flySense    // flyby 觸發靈敏度（0=只有極快才觸發）
[Range(0,2)] float flyStrength // flyby 效果強度（遮蔽深度、亮度）
```

**Flyby 機制：**
- `ω = tanSpeed / clampedDist`（物理角速度，dist=0 時用 MinDist=2f 夾緊）
- Peak-hold envelope：attack 0.72 / decay 0.04（效果與視覺同步）
- 遠耳遮蔽：`dynShadow = Lerp(0.36, 0.02, effectiveT)`
- 近耳亮化：HP filter α=0.82，+60% 混入

**Elevation 感知：**
- 仰角增益 ±22%
- 頻譜傾斜：仰角 → +55% HP（亮），俯角 → −28% HP（暗）

---

## FMSynthesizer 行星音色

| 行星 | 音色類型 | PlanetVolume |
|------|----------|-------------|
| Mercury | Bell | 1.08 |
| Venus | SoftMetal | 0.82 |
| Earth | Pad | 0.92 |
| Mars | Pulse | 0.98 |
| Jupiter | Rich | 1.34 |
| Saturn | Shimmer | 1.18 |
| Uranus | Ice / SoftMetal | 0.85 |
| Neptune | Deep | 1.42 |
| Pluto | Sparse | 1.48 |

---

## UI 邏輯重點

- **Solo toggle**：再按一次 SOLO 恢復 pre-solo mute 狀態（用 `manager.Planets/Synths`，不用 `toggles`）
- **SPACE 滑桿**：Reverb（energy）/ Room（depth）/ Damp（material）/ Density / Sense / Force
- **Flame 滑桿**：控制 `lfoRate`（不影響 flyby）
- **初始音量**：`AudioListener.volume = 0.55f`（在 `Start()` 設定）
- **相機鎖定**：`CameraController.Blocked = true` 在選單開啟時

---

## Build

| 平台 | 輸出路徑 | 狀態 |
|------|----------|------|
| iOS | `build/ios-meshrir/` (Xcode project) | ✅ |
| Android | `build/android/SolarmixMeshRIR.apk` | ✅ |
| WebGL | React 網頁版取代（規劃中） | — |

**Build 指令（Unity Editor 要關閉）：**
```bash
/Volumes/ORICO/Unity/6000.4.3f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode \
  -projectPath "/Volumes/ORICO/Solarmix" \
  -executeMethod MobileBuild.BuildIOS \
  -logFile /tmp/unity_ios.log
```

---

## 已知限制

- batchmode 須在 Unity Editor 關閉後執行（SQLite lock 衝突）
- ORICO 外接碟 batchmode 切換 build target 時有 readonly database 問題 → 改由本機 `~/Solarmix` 建 Android
- WebGL batchmode 在 macOS Silicon 有 Bee IPC 不穩定問題 → 改用 React

---

## 規劃中

- React 網頁版（audio worklet + Web Audio API 重新實作空間化）
- 更細緻的 per-planet flyby 參數
- azimuth / elevation 感知持續調整中
