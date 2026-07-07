# ScreenPaste

仿 Snipaste 的 Windows 截圖標註工具，以 **C# + WPF (.NET 9)** 打造。

## 功能

- **截取**
  - 整個畫面（多螢幕 / 混合 DPI）
  - 視窗自動辨識：滑鼠移到視窗上會自動描出窗體範圍，點擊即截取
  - 拖曳自訂矩形範圍
- **標註**（選取後在滑鼠旁彈出**圖示工具列**）
  - 麥克筆：實心筆觸，可調粗細 / 顏色 / 透明度
  - 螢光筆：半透明疊色，可調粗細 / 顏色 / 透明度
  - 文字：可選**字體 / 大小 / 顏色 / 樣式（正常、粗體、斜體、刪除線）**，點擊放置後直接輸入
  - 模糊：同一個工具內切換**高斯**或**馬賽克**，並可調**模糊程度**
  - 調色盤可按 **＋** 叫出色彩選擇器（輸入 Hex / RGB 滑桿 / **透明度**），視窗自動出現在工具列上／下方
- **復原 / 重做**：`Ctrl+Z` / `Ctrl+Y`（復原僅用熱鍵，工具列只保留重做鈕）
- **輸出**
  - 複製到剪貼簿（`Ctrl+C`）
  - 儲存為 PNG / JPG（`Ctrl+S`）／快速儲存到預設資料夾（`Ctrl+Shift+S`）
  - 釘選到螢幕（Pin）：浮動於最上層，可拖曳移動、滾輪縮放、右鍵選單
- **ESC 行為**
  - 編輯階段按 ESC → 清除標註、退回重新框選
  - 框選階段按 ESC → 取消截圖
  - 釘選多張時：ESC 先關閉**目前聚焦**的那張；若無任何釘選被聚焦，則一次關閉全部
- **外觀主題**：淺色 / 深色 / 跟隨系統（系統匣選單切換），套用於工具列、色彩選擇器與系統匣選單
- **啟動方式**
  - 全域熱鍵（預設 **F1**）
  - 系統匣圖示（單擊選單、雙擊截圖）
  - 可設定**開機自動啟動**（系統匣選單勾選）

## 建置與執行

需求：Windows 10/11、.NET 9 SDK。

```bash
dotnet build
dotnet run --project src/ScreenPaste
```

程式啟動後常駐系統匣。按 **F1**（或雙擊系統匣圖示）開始截圖。

## 操作流程

1. 按 F1 → 畫面凍結、變暗。
2. 滑鼠移到某視窗 → 自動描框；點一下截取整個視窗。或按住拖曳框選自訂範圍。
3. 選取後在選區旁出現工具列：選工具 → 調整參數 → 在選區上標註。
4. 按「複製 / 儲存 / 釘選」輸出，或 `Esc` / 「關閉」取消。

## 設定

設定檔位於 `%AppData%\ScreenPaste\settings.json`（系統匣選單 →「設定檔…」可直接開啟）。
可調整筆刷預設、模糊程度、預設儲存資料夾，以及**所有熱鍵**。

熱鍵以易讀字串設定（例如 `F1`、`Ctrl+Shift+A`、`Ctrl+Z`）：

| 設定鍵 | 說明 | 預設 |
|--------|------|------|
| `CaptureHotkey` | 全域截圖熱鍵 | `F1` |
| `UndoHotkey` | 復原 | `Ctrl+Z` |
| `RedoHotkey` | 重做 | `Ctrl+Y` |
| `CopyHotkey` | 複製到剪貼簿 | `Ctrl+C` |
| `SaveHotkey` | 另存新檔 | `Ctrl+S` |
| `QuickSaveHotkey` | 快速儲存到預設資料夾 | `Ctrl+Shift+S` |

修改後重新啟動程式（或截圖）即生效。

## 已知限制

- 單一覆蓋層橫跨「不同 DPI」的多台螢幕時，WPF 只能以單一 DPI 繪製整個視窗，
  極端混合 DPI 情境下選取框可能有輕微偏差；同 DPI 多螢幕與單螢幕皆精準。
- 高斯模糊區塊邊緣可能有極輕微暈邊（BlurEffect 取樣特性）。

## 架構

| 區域 | 檔案 |
|------|------|
| 啟動 / 系統匣 / 熱鍵 | `App.xaml.cs`, `TrayIconFactory.cs`, `Native/HotkeyManager.cs` |
| Win32 擷取 / 視窗偵測 | `Native/NativeMethods.cs`, `Native/ScreenCapture.cs`, `Native/WindowEnumerator.cs` |
| 擷取覆蓋層 + 編輯器 | `Capture/CaptureOverlayWindow.xaml(.cs)` |
| 工具 / 復原重做 | `Editor/ToolKind.cs`, `Editor/EditHistory.cs` |
| 模糊 / 合成 | `Rendering/BlurEffects.cs`, `Rendering/Compositor.cs` |
| 輸出 / 釘選 | `Output/ClipboardService.cs`, `Output/FileSaveService.cs`, `Output/PinWindow.cs` |
| 設定 | `Settings/AppSettings.cs` |
