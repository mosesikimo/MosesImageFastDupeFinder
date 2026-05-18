🚀 FastImageDupe v1.00 Architecture Blueprint / 系統架構藍圖

📌 Project Overview / 專案概述

FastImageDupe is a high-performance image deduplication and directory merging tool designed to process hundreds of thousands of images. It combines an ultra-fast 8x8 pHash blind scan with a high-precision 16x16 (256-bit XOR) secondary verification. It features smart directory group analysis via an O(N) reverse indexing engine, context-aware scenario modes (Personal Album vs. Internet Download), dynamic UI multi-group merging, multi-level recovery management, and full bilingual (EN/ZH) support.

FastImageDupe 是一套專為數十萬張海量圖片設計的「極速去重複與目錄合併工具」。
結合「8x8 pHash 極速盲掃」與「16x16 (256-bit XOR) 高精度二次校驗」，並具備專業 O(N) 反向映射群組分析、場景感知掃描模式 (個人相簿嚴格模式 vs. 網路寬鬆模式)、動態 UI 多群組歸併、多層級復原管理，以及全域雙語 (中/英) 支援。

📂 Directory Structure / 專案工作樹狀結構

FastImageDupe/
│
├── FastImageDupe.csproj             # Project File / 專案檔
│
├── MainWindow.xaml                  # Main UI / 主介面 (支援輕度/專業模式切換)
├── MainWindow.xaml.cs               # Main UI Logic / 主介面邏輯 (完美 UI 綁定解耦合與參數穿透)
│
├── RecoveryWindow.xaml              # Recovery UI / 復原管理介面 (支援防鎖死預覽縮圖)
├── RecoveryWindow.xaml.cs           # Recovery Logic / 復原邏輯 (多層次批次選取、失效紀錄清理、防幽靈紀錄)
│
└── Core/                            # Core Logic Layer / 核心邏輯層
    │
    ├── Models/                      # 📦 Data Models / 資料模型
    │   └── Models.cs                # POCO classes (包含四種群組動態 UI 狀態切換)
    │
    ├── Engines/                     # ⚙️ Core Engines / 核心運算引擎
    │   ├── ScanEngine.cs            # Phase 1: Fast BFS Directory Scan / 目錄極速巡覽
    │   ├── ImageHashEngine.cs       # Phase 2: Image Decoding (場景感知 pHash & XOR) / 圖片解析
    │   └── GroupAnalysisEngine.cs   # Phase 3: Directory Collision / O(N) 極速大群組與正則特徵分析
    │
    ├── Helpers/                     # 🛠️ Utilities / 輔助工具箱
    │   ├── CommonHelper.cs          # General utilities (假刪除防護與資源回收機制)
    │   ├── PathHelper.cs            # Long Path (\\?\) Support / 長路徑支援與規範化
    │   └── DirectoryFilter.cs       # Smart Filter / 智慧過濾排除非媒體目錄
    │
    └── Managers/                    # 🗃️ Global State Managers / 全域狀態管理
        ├── RecoveryManager.cs       # Recovery state & API / 復原紀錄與系統資源回收桶串接
        └── I18nManager.cs           # Global i18n & Config / 全域多國語系管理與組態記憶


🧬 Core Modules / 核心模組與演算法

1. Image Hash Engine / 影像雜湊引擎 (ImageHashEngine.cs)

導入了 「Context-Aware Tolerance (場景感知容錯)」 機制，將演算法依據使用者情境動態分流：

🔘 個人相簿 (Personal Album - 嚴格模式)

關閉旋轉/翻轉容錯： 拔除 0/90/180/270 度判定，直的照片與橫的照片特徵強制不共用。

長寬比例 (Aspect Ratio) 防護網： 若兩張圖片比例誤差 > 1%，直接判定不同。

極限 XOR 容錯： 將 diffThreshold 設為 0，256-bit 灰階特徵必須 100% 完美吻合。

【設計目的】：防禦手機連拍。即使是微小手震、ISO 雜訊或構圖微調的相近照片，都能被系統精準放過，絕對不誤殺珍貴回憶。

⚪ 網路下載 (Internet Download - 寬鬆模式)

啟動萬向特徵： 呼叫 GetCanonicalHash，無視圖片被旋轉或鏡像翻轉。

無視長寬比例： 跳過比例檢查，強制依賴 16x16 縮圖核心進行模糊搜尋。

彈性 XOR 容錯： 將 diffThreshold 放寬至 5。

【設計目的】：抓出網路上被輕微畫質壓縮、微調色調、被裁切或是加上了小浮水印的迷因梗圖與素材。

2. Group Analysis Engine / 目錄分析與智慧決策 (GroupAnalysisEngine.cs)

O(N) Reverse Indexing (反向映射): 利用 O(N) 字典映射，將極大量的重複圖片瞬間向上聚合為「目錄對目錄 (Dir vs. Dir)」的實體關聯，消滅傳統巢狀迴圈造成的系統卡死瓶頸。

Smart Categorization & Regex Patterning (四大群組型態智慧分類):

Same Dir (同目錄): 隱藏搬移選項，強制「僅處理重複圖片」。

Normal Cross-Dir (一般跨目錄): UI 預設為「不分類型全搬移」，協助快速合併目錄。

Multi-Group Sequential/Grouped (多群組-特徵明確): 透過正則表達式 (Regex) 抓出前綴或流水號 (如 IMG_001)。UI 動態切換文字，預設為安全的「搬移重複 + 特徵相鄰檔案」。

Multi-Group Random (多群組-無特徵): 退回最保守的「僅處理重複圖片」，防止雜亂檔案被錯誤牽連。

3. Engine Parameter Passthrough / 引擎參數穿透設計 (MainWindow.xaml.cs)

Decoupled Multi-Group Merging: 解決 UI 與底層邏輯脫鉤的問題。將畫面上的 RadioButton 狀態 (僅重複/全搬移/僅多媒體) 強制以參數型式打穿至 ProcessMultiGroupMergeCoreAsync 底層引擎。賦予使用者最高決定權，允許覆蓋系統預設安全行為，執行最暴力的整廠掃尾合併。

4. Recovery Management / 多層次復原管理系統 (RecoveryWindow & RecoveryManager)

Multi-Level Batching / 三層批次追蹤: 所有操作皆綁定三層唯一識別碼 (SmartBatchId, GroupBatchId, PersonalBatchId)，實現極度精確的 UI 同批次反向勾選與復原。

Lock-Free Preview / 防鎖死預覽: 隔離區預覽圖全面改用 FileStream 與 BitmapCacheOption.OnLoad 動態載入。徹底解決 WPF 預設影像快取導致實體檔案被咬死 (File Locked) 而無法復原的致命問題。

🛡️ Safety & Stability / 系統安全與穩定機制

Soft Delete / 實體假刪除防護: 系統預設所有移除動作皆為「假刪除」，自動將檔案移至硬碟同區的 FastImageDupeDel。唯有在復原管理員中按下「徹底丟棄」，才會呼叫 Windows Shell API (SHFileOperation) 將檔案送入「系統資源回收桶」。

Thread Safety / 跨執行緒安全: 背景掃描與智慧合併 (Task.Run) 嚴格執行「UI 解耦合綁定模式」。在處理數十萬筆資料前主動切斷 ItemsSource，計算完成後再一次性推回 UI，徹底杜絕 WPF 繪圖風暴與全域當機。

Memory Management / 記憶體釋放: 圖片物件強制執行 Freeze() 並主動解參照；深度架構優化完全避免在第一與第二階段引發垃圾回收 (GC Leak) 崩潰風暴。