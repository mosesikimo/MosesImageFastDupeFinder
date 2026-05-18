#nullable enable
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;

namespace FastImageDupe.Core
{
    public static class I18nManager
    {
        public static bool IsEnglish { get; set; } = false;

        public static string AppVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor:D2}" : "1.00";
            }
        }

        private static readonly Dictionary<string, string> ZhDict = new() {
            {"MainWindow_Title", "FastImageDupe - 極速圖片去重複工具"},
            {"StatusReady", "準備就緒"},
            {"MsgScanFinished", "掃描完成！"},
            {"MsgCanceled", "掃描已中斷！顯示目前為止的結果。"},
            {"MsgTotal", "總檔案數："},
            {"MsgSuccess", "成功特徵："},
            {"MsgFail", "失敗跳過："},
            {"MsgFoundDupe", "發現重複："},
            
            {"RbPersonalAlbum", "🔘 個人相簿 (嚴格模式)"},
            {"RbInternetMode", "⚪ 網路下載 (寬鬆模式)"},
            {"MsgScanModeConfirm", "目前選擇的掃描模式為：\n\n【{0}】\n\n此模式將決定系統比對重複圖片的嚴格程度，是否確定以此模式執行掃描？"},
            {"TitleScanModeConfirm", "確認掃描模式"},

            {"MoveModeDuplicatesOnly_Normal", "🎯 僅處理與搬移「重複圖片」"},
            {"MoveModeDuplicatesOnly_Pattern", "🎯 搬移重複圖片 ＋「符合特徵區段的相鄰檔案」"},
            {"MoveModeAll", "📦 來源目錄「不分類型全搬移」(含子目錄)"},
            {"MoveModeMedia", "📦 來源目錄「僅搬移多媒體」(含子目錄)"},
            
            {"BtnPreviewPlan", "👁️ 預覽計畫"},
            {"BtnMergeSelected", "⚡ 執行合併 (移至選取目標)"},
            {"BtnMultiGroupPreview", "👁️ 預覽多群歸併"},
            {"BtnCleanSameDir", "🧹 清理同目錄"},
            {"MultiGroupActionSequential", "⚡ 區間流水號歸併"},
            {"MultiGroupActionGrouped", "⚡ 前綴群組歸併"},
            {"MultiGroupActionRandom", "🔍 自行判斷"},

            {"MsgRandomNoPreview", "該目錄特徵為隨機命名，無對應的歸併預覽。"},
            {"MsgNoMatchingPattern", "找不到符合特徵的相鄰檔案。"},
            {"TitleMultiGroupPreview", "🔍 預覽多群歸併 ({0} 個檔案)"},
            {"MsgRandomManualDecision", "該目錄沒有明顯的群組或流水號特徵，請在下方清單中自行判斷與搬移。"},
            {"TitleManualDecision", "自行判斷"},
            {"MsgMultiGroupConfirm", "即將啟動自動特徵歸併！\n系統將自動把符合 [{0}] 特徵區間的檔案\n從:\n{1}\n\n整批搬移至:\n{2}\n\n是否確認搬移？"},
            {"TitleMultiGroupConfirm", "多群歸併確認"},
            {"StatusMultiGroupMoving", "正在執行多群歸併... 已搬移 {0} 個"},
            {"StatusMultiGroupDone", "多群歸併完成，共歸位 {0} 個特徵檔案。"},
            {"ActionMultiGroupMerge", "📦 批次特徵歸併"},
            {"StatusAutoRescan", "🔄 檔案已異動，正在全自動重新掃描以同步狀態..."},

            {"MsgConflict", "(與 {0} 個目錄衝突)"},
            {"LblRes", "解析度:"},
            {"LblSize", "大小:"},
            {"LblName", "檔名:"},
            {"LblDir", "目錄:"},
            {"BtnCancel", "🛑 中斷掃描"},

            {"TxtHeader", "📁 圖片去重複掃描"},
            {"GrpScanSettings", "掃描條件設定"},
            {"TxtTargetPath", "目標路徑:"},
            {"BtnAddPath", "加入..."},
            {"BtnClearPath", "清除"},
            {"ChkSubDir", "包含子目錄"},
            {"TxtExcludePath", "排除目錄:"},
            {"BtnAddExclude", "加入..."},
            {"BtnClearExclude", "清除"},
            {"BtnScan", "🚀 開始掃描"},
            {"BtnLang", "🌐 English"},
            {"BtnReadMe", "📖 說明 (Read Me)"},
            {"BtnRecovery", "🔄 復原管理"},
            {"BtnSmartSelect", "🧹 智慧一鍵處理"},
            
            {"BtnToggleToSimple", "🔄 切換輕度模式"},
            {"BtnToggleToPro", "🔄 切換專業模式"},
            {"BtnDeleteChecked", "🗑️ 隔離勾選的檔案 (批次)"},

            {"TxtSafeTipTop", "🛡️ 安心提示：所有刪除皆為「假刪除」，隨時可還原！"},
            
            {"GrpDirGroupSettings", "📂 操作說明：請選擇保留目錄，以及檔案移動方式"},
            {"ChkSelectAllGroups", "全選"},
            
            {"ColBigGroupId", "大群組 ID"},
            {"ColDirA", "目錄 A"},
            {"ColDirB", "目錄 B"},
            {"ColMoveMode", "合併搬移模式"},
            {"ColDupeCount", "重複數量"},
            {"ColAction", "操作"},
            
            {"ColFileBigGroupId", "大群ID"},
            {"ColMatchId", "小群ID"},
            {"ColSize", "檔案大小"},
            {"ColRes", "解析度"},
            {"ColDir", "所在目錄"},
            {"ColFileName", "檔案名稱"},
            
            {"MenuOpenDir", "📂 開啟所在目錄"},
            {"MenuOpenFile", "🖼️ 開啟檔案"},

            {"TxtDualPreview", "👁️ 雙視窗同步對比"},
            {"TxtSafeTipBottom", "💡 提示：此刪除僅為「假刪除」移至隔離區，隨時可還原"},
            {"TxtPermanentWarn", "⚠️ 警告：若從系統資源回收桶清空，本軟體將無法再復原該檔案！"},
            {"BtnManualDelete", "🗑️ 隔離右側預覽的圖片"},
            
            {"GrpPreview1", "預覽圖片 1 (對應藍色框)"},
            {"GrpPreview2", "預覽圖片 2 (對應紅色框)"},
            {"RbPreviewA", "選取此圖片"},
            {"RbPreviewB", "選取此圖片"},
            
            {"TxtReadMeTitle", "📖 FastImageDupe 系統說明"},
            {"BtnCloseReadMe", "關閉 (Close)"},

            {"MsgWarning", "警告"},
            {"MsgPrompt", "提示"},
            {"MsgError", "錯誤"},
            {"MsgNoImages", "找不到任何支援的圖片檔案。"},
            {"MsgNoPreviewAction", "此群組沒有任何需要搬移或隔離的檔案動作。"},
            {"TitlePreviewPlan", "🔍 執行預覽計畫 (Preview Plan)"},
            {"TitleMergeReport", "智慧合併清理報告"},
            {"MsgMergeComplete", "處理完成！\n\n📂 成功移動至目標目錄：{0} 個最佳正本\n🗑️ 安全隔離排除 (至 FastImageDupeDel)：{1} 個低畫質副本\n📦 搬移剩餘檔案：{2} 個"},
            {"MsgSelectGroupFirst", "請先勾選至少一個要執行的大群組！"},
            {"TitleSmartProcess", "智慧一鍵處理"},
            {"MsgSmartConfirm", "系統將自動對「已勾選的 {0} 個大群組」執行：\n1. 同目錄自動保留最佳並清理\n2. 跨目錄自動執行合併\n\n確定要一鍵處理這些群組嗎？"},
            {"StatusSmartComplete", "✅ 智慧一鍵處理完成！"},
            {"TitleSmartReport", "智慧處理報告"},
            {"MsgSmartDone", "自動處理完成！\n請點擊上方「復原管理」檢視或撤銷詳細記錄。"},
            {"MsgRecoveryHint", "如果您剛剛進行了「復原」操作，建議重新掃描目錄以確保清單正確。"},
            {"TitleExclusionReport", "📊 排除目錄分析報告 (Exclusion Report Grid)"},
            {"MsgNoExclusions", "目前沒有被自動排除的目錄。"},
            {"MsgPathNotFound", "找不到路徑：\n{0}"},
            {"TitleIsolateComplete", "隔離完成"},
            {"MsgIsolateSuccess", "成功將 {0} 個檔案移至隔離區！"},
            {"MsgSelectFileToIsolate", "請先在左側清單中打勾選擇要隔離的檔案！"},
            {"TitleIsolateConfirm", "批次隔離確認"},
            {"MsgIsolateConfirm", "確定要將這 {0} 個勾選的檔案移至隔離區嗎？"},
            {"MsgSelectRadioFirst", "請先點選 RadioButton 選擇要隔離的圖片 (圖片 1 或 圖片 2)！"},

            {"RecoveryWindow_Title", "🔄 復原與資源回收管理"},
            {"Rec_GrpFilter", "1. 顯示篩選 (不影響勾選狀態)"},
            {"Rec_LblStatus", "紀錄狀態: "},
            {"Rec_ChkNormal", "正常紀錄"},
            {"Rec_ChkHidden", "已隱藏紀錄"},
            {"Rec_LblOpType", "操作類型: "},
            {"Rec_ChkSmart", "智慧"},
            {"Rec_ChkGroup", "群組"},
            {"Rec_ChkPersonal", "個人"},
            {"Rec_GrpBatch", "2. 批次快速勾選 (針對顯示中的清單)"},
            {"Rec_TxtBatchHint", "💡 點選下方清單某一列後，可使用智慧批次選取："},
            {"Rec_BtnSelSmart", "🎯 勾選同屬【智慧】"},
            {"Rec_BtnSelGroup", "🎯 勾選同屬【群組】"},
            {"Rec_BtnSelPersonal", "🎯 勾選同屬【個人】"},
            {"Rec_ColSelectAll", "全選"},
            {"Rec_ColType", "類型"},
            {"Rec_ColSmartId", "智慧ID"},
            {"Rec_ColGroupId", "群組ID"},
            {"Rec_ColPersonalId", "個人ID"},
            {"Rec_ColTime", "時間"},
            {"Rec_ColSource", "原始路徑 (還原目標)"},
            {"Rec_ColDest", "目前路徑 (隔離區)"},
            {"Rec_ColPreview", "隔離區預覽"},
            {"Rec_TxtMissing", "找不到檔案"},
            {"Rec_TipPreview", "實體檔案目前在隔離區內，可安全復原"},
            {"Rec_MenuSmart", "🎯 將同屬【智慧】批次的紀錄打勾"},
            {"Rec_MenuGroup", "🎯 將同屬【群組】批次的紀錄打勾"},
            {"Rec_MenuPersonal", "🎯 將同屬【個人】批次的紀錄打勾"},
            {"Rec_BtnClearMissing", "🧹 清除失效(缺檔)紀錄"},
            {"Rec_BtnHide", "👁️ 隱藏打勾"},
            {"Rec_BtnUnhide", "👁️ 解除隱藏"},
            {"Rec_BtnRecover", "🔄 復原打勾至原路徑"},
            {"Rec_BtnRecycle", "🗑️ 徹底丟入系統資源回收桶"},
            {"Rec_TypeSmart", "智慧"},
            {"Rec_TypeGroup", "群組"},
            {"Rec_TypePersonal", "個人"},
            {"Rec_TypeSingle", "單筆"},
            
            {"Rec_MsgSelectBase", "請先在清單中點選某一列作為基準，系統才能找出同批次的紀錄！"},
            {"Rec_MsgNotSameBatch", "您點選的紀錄並不屬於【{0}】操作，或畫面上找不到其他同批次項目。"},
            {"Rec_MsgSelectHide", "請先打勾要隱藏的紀錄。"},
            {"Rec_MsgHideSuccess", "已將 {0} 筆紀錄標記為隱藏。"},
            {"Rec_MsgSelectUnhide", "請先打勾要解除隱藏的紀錄。"},
            {"Rec_MsgUnhideSuccess", "已將 {0} 筆紀錄解除隱藏。"},
            {"Rec_MsgNotHidden", "您打勾的紀錄本身並非隱藏狀態。"},
            {"Rec_MsgNoMissing", "目前沒有發現任何缺檔的失效紀錄。"},
            {"Rec_TitleClearMissing", "清除缺檔紀錄"},
            {"Rec_MsgClearMissingConfirm", "掃描發現 {0} 筆找不到實體隔離檔案的紀錄。\n(這些檔案可能已被您丟入資源回收桶，或手動從資料夾刪除)\n\n確定要將這些紀錄從清單中清除嗎？\n清除後將無法再從本系統追蹤這些檔案！"},
            {"Rec_MsgClearMissingSuccess", "已成功清除 {0} 筆缺檔紀錄。"},
            {"Rec_TitleClearComplete", "清除完成"},
            {"Rec_MsgSelectRecover", "請先打勾要復原的檔案紀錄。"},
            {"Rec_TitleRecover", "復原"},
            {"Rec_MsgRecoverConfirm", "確定要將這 {0} 個檔案搬回原始路徑嗎？\n(請注意，如果檔案被復原，原處的檔案將被覆寫)"},
            {"Rec_TitleRecoverResult", "復原結果"},
            {"Rec_MsgRecoverPartial", "復原完成: {0} 筆。\n⚠️ 有 {1} 筆檔案復原失敗！\n(可能檔案被其他程式佔用或權限不足)"},
            {"Rec_TitleRecoverComplete", "復原完成"},
            {"Rec_MsgRecoverSuccess", "成功復原 {0} 筆檔案回原始位置。"},
            {"Rec_MsgSelectRecycle", "請先打勾要丟棄的檔案紀錄。"},
            {"Rec_TitleRecycle", "資源回收"},
            {"Rec_MsgRecycleConfirm", "確定要將這 {0} 個檔案徹底丟入 Windows 資源回收桶嗎？\n\n⚠️ 警告：丟入資源回收桶後，本系統將無法再追蹤或復原該圖片！\n(若後續從資源回收桶清空，檔案將永久遺失)"},
            {"Rec_TitleRecycleResult", "清理結果"},
            {"Rec_MsgRecyclePartial", "清理完成: {0} 筆。\n⚠️ 有 {1} 筆檔案丟棄失敗！"},
            {"Rec_TitleRecycleComplete", "清理完成"},
            {"Rec_MsgRecycleSuccess", "已將 {0} 個檔案移至資源回收桶。"},

            {"ReadMeContent", $@"# FastImageDupe v{AppVersion} - 極速圖片去重複工具 🚀

本手冊協助您快速上手，安全地整理海量圖片。

## 📍 步驟一：設定掃描範圍與模式
1. 目標路徑：選擇要掃描的資料夾（預設包含子目錄）。
2. 掃描模式：
   * 個人相簿 (嚴格)：零容錯比對，絕對不會將連拍或不同比例的照片誤判為重複，適合整理珍貴回憶。
   * 網路下載 (寬鬆)：容許輕微畫質壓縮、浮水印或旋轉，適合清理大量網路梗圖。

## 📍 步驟二：關於「搬移模式」與大群組的智慧判斷
系統會自動分析資料夾之間的關係，並動態給予最安全的預設建議與文字：
* 同目錄群組：隱藏搬移選項，僅執行最安全的「保留最佳畫質、隔離副本」。
* 一般跨目錄：預設為「不分類型全搬移」，協助您徹底合併兩個資料夾。
* 多群組 (具備流水號/前綴特徵)：預設為「搬移重複圖片 ＋ 特徵檔案」，精準歸位同一批拍攝的照片，不誤搬雜檔。
* 多群組 (隨機無特徵命名)：退回最保守的「僅處理重複圖片」，確保亂數梗圖不會造成資料夾大亂。
*(註：您隨時可以手動切換 RadioButton 來覆蓋系統建議！)*

## 📍 步驟三：處理重複圖片
* 手動挑選：在清單中打勾或透過預覽視窗確認後，按下「隔離」按鈕。
* 智慧一鍵處理 (推薦)：在上方勾選多個大群組，按下「🧹 智慧一鍵處理」。系統會依據上述的智慧判斷與您的選項，自動歸位最佳正本並隔離多餘副本。

## 📍 步驟四：復原與徹底刪除 (安全機制)
* 🛡️ 假刪除保護：主畫面的「隔離/刪除」皆是將檔案移至該磁碟的 `FastImageDupeDel` 專屬隔離區，絕對不會立刻從硬碟消失。
* 🔄 復原管理：點擊上方「🔄 復原管理」，可隨時將隔離的檔案「復原」回原路徑。確認無誤後，再「徹底丟入資源回收桶」。

⚠️ 使用者特別注意事項：
1. 請勿手動用檔案總管修改 `FastImageDupeDel` 隔離目錄，以免復原紀錄失效。
2. 若在復原管理員中選擇「徹底丟入系統資源回收桶」，本軟體將無法再追蹤與復原該檔案（若要救回需親自去 Windows 資源回收桶撿回）。

---

## 🤝 聯繫作者

若本工具為你省下了時間，或想交流系統架構與技術，歡迎隨時聯繫！
• 開發者：渡川 Moses
• GitHub：https://github.com/mosesikimo
• Email：mosesikimo@gmail.com
• Line ID：mosesikimo
• 座右銘：程式碼說邏輯，我渡思想過川。

## 🧑‍💻 關於作者：拿管工廠的牛刀，來殺你硬碟裡的重複圖片

「我是渡川 Moses，一位擺渡思想的軟體人。」

我在 IT 與 OT 跨域整合打滾了 21 年，專精系統架構與軟硬體整合，近期正著手探索 AI Agent。
我的日常工作不是畫美美的 UI，而是把企業核心的 ERP、PLM 系統，以及數控機台與品保量測儀器全部綁在一起。

「檔案去重複化」，本質上就是一場極限的數據比對與 I/O 戰爭。

我的核心戰場，在於嚴格的流程狀態管理與 LEAN 精實管理，主導過無數複雜的工單、品保與 NC 版控系統；
同時深耕 PLM 與研發量產成本管理，從現場物料節省、備料優化，一路到錙銖必較的節費機制。

早在 20 年前，我就曾為兩座跨國工廠操刀過「資料庫跨廠即時同步」的底層架構；
面對海量實體檔案的備援，我也深知如何拿捏效能與系統負載的平衡——精準排程每日三次的巨量檔案無損對接，絕不盲目耗損網路頻寬。

這些「一旦當機、資料不同步，整廠就會停擺、損失千萬」的巨型商業邏輯，造就了我對系統穩定性的極度苛求。

現在，我把這種從異地備援與嚴苛流程防呆中淬鍊出來的偏執，帶到了這個小巧的圖片整理程式裡。
我把你的圖片當作「跨國交易」在保護——比對不容絲毫誤差，不該發生的意外，連發生的按鈕都沒有。

我相信「程式碼會說話」，更相信好的技術，應當像一條靜水流深的河——表面從容，內裡有力，能載人渡過難關。

這套軟體可能沒有最漂亮的皮囊，但它有一顆經過 21 年企業戰場千錘百鍊的強大心臟。
把你的硬碟交給它，喝杯咖啡，感受一下什麼叫做「絕對精準的數據清理」。

## ✨ 核心技術亮點：降維打擊的底層引擎

多數工程師寫的是單一功能，而我習慣建造自動防呆、精準比對的底層引擎。
當這個程式在掃描你那塞滿幾 TB 圖片的硬碟時，背後運作的是企業級的併發控制邏輯：

💥 獨家降維打擊引擎 (NEW)
以 O(N) 字典映射與純粹的 256-bit 位元運算取代傳統 O(N³) 巢狀迴圈，徹底拔除不必要的 RGB 記憶體轉換。即便處理 10 萬張以上的高相似度圖片，記憶體與 CPU 的消耗依然趨近於零，實現真正的極速秒殺。

🏎️ 獨家極速管線
採用 C# 平行運算與無鎖化架構。完美榨乾現代多核心 CPU 與 SSD 的極限，絕不吃光記憶體，更不會跑到一半卡死。

🧠 智慧特徵快取
指紋掃描過一次即永久記憶。無視檔案被改得面目全非或隨意搬移，第二次掃描直接觸發「快取秒殺」。

🛡️ 極限容錯與防呆
遇到壞檔自動跳過防卡死；隔離與復原採用多層級批次追蹤與防幽靈紀錄機制，確保任何操作都能安全撤銷。"}
        };

        private static readonly Dictionary<string, string> EnDict = new() {
            {"MainWindow_Title", "FastImageDupe - High Speed Image Deduplicator"},
            {"StatusReady", "Ready"},
            {"MsgScanFinished", "Scan Finished!"},
            {"MsgCanceled", "Scan Canceled! Showing partial results."},
            {"MsgTotal", "Total Files: "},
            {"MsgSuccess", "Success Hash: "},
            {"MsgFail", "Failed/Skipped: "},
            {"MsgFoundDupe", "Duplicates Found: "},
            
            {"RbPersonalAlbum", "🔘 Personal Album (Strict Mode)"},
            {"RbInternetMode", "⚪ Internet Download (Loose Mode)"},
            {"MsgScanModeConfirm", "The currently selected scan mode is:\n\n[{0}]\n\nThis mode determines how strictly the system compares duplicate images. Are you sure you want to proceed with this mode?"},
            {"TitleScanModeConfirm", "Confirm Scan Mode"},

            {"MoveModeDuplicatesOnly_Normal", "🎯 Process & Move Duplicates Only"},
            {"MoveModeDuplicatesOnly_Pattern", "🎯 Move Duplicates + Feature Matched Files"},
            {"MoveModeAll", "📦 Move all types (incl. sub-dirs)"},
            {"MoveModeMedia", "📦 Move media only (incl. sub-dirs)"},
            
            {"BtnPreviewPlan", "👁️ Preview Plan"},
            {"BtnMergeSelected", "⚡ Execute Merge"},
            {"BtnMultiGroupPreview", "👁️ Preview Multi-Group"},
            {"BtnCleanSameDir", "🧹 Clean Same Dir"},
            {"MultiGroupActionSequential", "⚡ Merge by Sequence"},
            {"MultiGroupActionGrouped", "⚡ Merge by Prefix"},
            {"MultiGroupActionRandom", "🔍 Manual Decision"},

            {"MsgRandomNoPreview", "This directory has random naming features. No corresponding merge preview available."},
            {"MsgNoMatchingPattern", "No adjacent files matching the pattern were found."},
            {"TitleMultiGroupPreview", "🔍 Preview Multi-Group Merge ({0} files)"},
            {"MsgRandomManualDecision", "This directory has no obvious grouping or sequential features. Please decide and move manually in the list below."},
            {"TitleManualDecision", "Manual Decision"},
            {"MsgMultiGroupConfirm", "Starting automatic pattern merge!\nThe system will automatically move files matching the [{0}] pattern\nFrom:\n{1}\n\nBatch move to:\n{2}\n\nAre you sure you want to proceed?"},
            {"TitleMultiGroupConfirm", "Multi-Group Merge Confirm"},
            {"StatusMultiGroupMoving", "Executing multi-group merge... Moved {0}"},
            {"StatusMultiGroupDone", "Multi-group merge complete. {0} feature files returned."},
            {"ActionMultiGroupMerge", "📦 Batch Feature Merge"},
            {"StatusAutoRescan", "🔄 Files have changed, executing full auto-rescan to sync state..."},

            {"MsgConflict", "(Conflicts with {0} dirs)"},
            {"LblRes", "Resolution:"},
            {"LblSize", "Size:"},
            {"LblName", "Name:"},
            {"LblDir", "Dir:"},
            {"BtnCancel", "🛑 Cancel Scan"},

            {"TxtHeader", "📁 Image Deduplication Scan"},
            {"GrpScanSettings", "Scan Settings"},
            {"TxtTargetPath", "Target Paths:"},
            {"BtnAddPath", "Add..."},
            {"BtnClearPath", "Clear"},
            {"ChkSubDir", "Incl. Sub-Dirs"},
            {"TxtExcludePath", "Exclude Paths:"},
            {"BtnAddExclude", "Add..."},
            {"BtnClearExclude", "Clear"},
            {"BtnScan", "🚀 Start Scan"},
            {"BtnLang", "🌐 中文"},
            {"BtnReadMe", "📖 Read Me"},
            {"BtnRecovery", "🔄 Recovery Manager"},
            {"BtnSmartSelect", "🧹 Smart Process All"},
            
            {"BtnToggleToSimple", "🔄 Switch to Simple Mode"},
            {"BtnToggleToPro", "🔄 Switch to Pro Mode"},
            {"BtnDeleteChecked", "🗑️ Isolate Checked Files"},

            {"TxtSafeTipTop", "🛡️ Tip: All deletions are safe 'Soft Deletes' and can be restored!"},
            
            {"GrpDirGroupSettings", "📂 Instructions: Select directory to keep & move mode"},
            {"ChkSelectAllGroups", "All"},
            
            {"ColBigGroupId", "Big Group ID"},
            {"ColDirA", "Directory A"},
            {"ColDirB", "Directory B"},
            {"ColMoveMode", "Move Mode"},
            {"ColDupeCount", "Duplicates"},
            {"ColAction", "Action"},
            
            {"ColFileBigGroupId", "Big ID"},
            {"ColMatchId", "Sub ID"},
            {"ColSize", "Size"},
            {"ColRes", "Resolution"},
            {"ColDir", "Directory"},
            {"ColFileName", "File Name"},
            
            {"MenuOpenDir", "📂 Open Directory"},
            {"MenuOpenFile", "🖼️ Open File"},

            {"TxtDualPreview", "👁️ Dual Window Preview"},
            {"TxtSafeTipBottom", "💡 Tip: 'Deleted' files are safely moved to the isolation zone."},
            {"TxtPermanentWarn", "⚠️ Warning: If emptied from the system Recycle Bin, this software cannot recover the file!"},
            {"BtnManualDelete", "🗑️ Isolate Selected Preview"},
            
            {"GrpPreview1", "Preview Image 1 (Blue)"},
            {"GrpPreview2", "Preview Image 2 (Red)"},
            {"RbPreviewA", "Select this image"},
            {"RbPreviewB", "Select this image"},
            
            {"TxtReadMeTitle", "📖 FastImageDupe System Read Me"},
            {"BtnCloseReadMe", "Close"},

            {"MsgWarning", "Warning"},
            {"MsgPrompt", "Prompt"},
            {"MsgError", "Error"},
            {"MsgNoImages", "No supported image files found."},
            {"MsgNoPreviewAction", "This group has no files that need to be moved or isolated."},
            {"TitlePreviewPlan", "🔍 Preview Plan"},
            {"TitleMergeReport", "Smart Merge Report"},
            {"MsgMergeComplete", "Processing Complete!\n\n📂 Successfully moved to target: {0} best originals\n🗑️ Safely isolated (to FastImageDupeDel): {1} inferior copies\n📦 Remaining files moved: {2}"},
            {"MsgSelectGroupFirst", "Please check at least one big group to execute!"},
            {"TitleSmartProcess", "Smart Process All"},
            {"MsgSmartConfirm", "The system will automatically perform the following on the '{0}' checked big groups:\n1. Auto keep best and clean same directory\n2. Auto merge cross directories\n\nAre you sure you want to process these groups?"},
            {"StatusSmartComplete", "✅ Smart Process Complete!"},
            {"TitleSmartReport", "Smart Process Report"},
            {"MsgSmartDone", "Auto processing complete!\nPlease click 'Recovery Manager' above to view or undo detailed records."},
            {"MsgRecoveryHint", "If you just performed a 'Recovery' operation, it is recommended to rescan directories to ensure the list is accurate."},
            {"TitleExclusionReport", "📊 Exclusion Report Grid"},
            {"MsgNoExclusions", "There are currently no automatically excluded directories."},
            {"MsgPathNotFound", "Path not found:\n{0}"},
            {"TitleIsolateComplete", "Isolation Complete"},
            {"MsgIsolateSuccess", "Successfully moved {0} files to the isolation zone!"},
            {"MsgSelectFileToIsolate", "Please check the files you want to isolate in the left list first!"},
            {"TitleIsolateConfirm", "Batch Isolate Confirmation"},
            {"MsgIsolateConfirm", "Are you sure you want to move these {0} checked files to the isolation zone?"},
            {"MsgSelectRadioFirst", "Please select a RadioButton first (Image 1 or Image 2) to isolate!"},

            {"RecoveryWindow_Title", "🔄 Recovery & Recycle Manager"},
            {"Rec_GrpFilter", "1. Display Filter (Does not affect selection)"},
            {"Rec_LblStatus", "Record Status: "},
            {"Rec_ChkNormal", "Normal Records"},
            {"Rec_ChkHidden", "Hidden Records"},
            {"Rec_LblOpType", "Operation Type: "},
            {"Rec_ChkSmart", "Smart"},
            {"Rec_ChkGroup", "Group"},
            {"Rec_ChkPersonal", "Personal"},
            {"Rec_GrpBatch", "2. Quick Batch Select (For displayed list)"},
            {"Rec_TxtBatchHint", "💡 Click a row in the list below, then use smart batch selection:"},
            {"Rec_BtnSelSmart", "🎯 Select Same [Smart] Batch"},
            {"Rec_BtnSelGroup", "🎯 Select Same [Group] Batch"},
            {"Rec_BtnSelPersonal", "🎯 Select Same [Personal] Batch"},
            {"Rec_ColSelectAll", "All"},
            {"Rec_ColType", "Type"},
            {"Rec_ColSmartId", "Smart ID"},
            {"Rec_ColGroupId", "Group ID"},
            {"Rec_ColPersonalId", "Personal ID"},
            {"Rec_ColTime", "Time"},
            {"Rec_ColSource", "Original Path (Restore Target)"},
            {"Rec_ColDest", "Current Path (Isolation Zone)"},
            {"Rec_ColPreview", "Isolation Preview"},
            {"Rec_TxtMissing", "File Missing"},
            {"Rec_TipPreview", "Physical file is in the isolation zone, safe to recover."},
            {"Rec_MenuSmart", "🎯 Check records from the same [Smart] batch"},
            {"Rec_MenuGroup", "🎯 Check records from the same [Group] batch"},
            {"Rec_MenuPersonal", "🎯 Check records from the same [Personal] batch"},
            {"Rec_BtnClearMissing", "🧹 Clear Missing Records"},
            {"Rec_BtnHide", "👁️ Hide Checked"},
            {"Rec_BtnUnhide", "👁️ Unhide Checked"},
            {"Rec_BtnRecover", "🔄 Recover Checked to Original Path"},
            {"Rec_BtnRecycle", "🗑️ Send to System Recycle Bin"},
            {"Rec_TypeSmart", "Smart"},
            {"Rec_TypeGroup", "Group"},
            {"Rec_TypePersonal", "Personal"},
            {"Rec_TypeSingle", "Single"},
            
            {"Rec_MsgSelectBase", "Please click a row in the list first as a reference to find records in the same batch!"},
            {"Rec_MsgNotSameBatch", "The record you clicked does not belong to a [{0}] operation, or no other items in the same batch were found."},
            {"Rec_MsgSelectHide", "Please check the records you want to hide first."},
            {"Rec_MsgHideSuccess", "Marked {0} records as hidden."},
            {"Rec_MsgSelectUnhide", "Please check the records you want to unhide first."},
            {"Rec_MsgUnhideSuccess", "Unhidden {0} records."},
            {"Rec_MsgNotHidden", "The records you checked are not currently hidden."},
            {"Rec_MsgNoMissing", "No invalid records with missing files were found."},
            {"Rec_TitleClearMissing", "Clear Missing Records"},
            {"Rec_MsgClearMissingConfirm", "Found {0} records with missing physical isolation files.\n(These files may have been sent to the Recycle Bin or manually deleted)\n\nAre you sure you want to clear these records from the list?\nThey cannot be tracked by the system after clearing!"},
            {"Rec_MsgClearMissingSuccess", "Successfully cleared {0} missing records."},
            {"Rec_TitleClearComplete", "Clear Complete"},
            {"Rec_MsgSelectRecover", "Please check the file records you want to recover first."},
            {"Rec_TitleRecover", "Recover"},
            {"Rec_MsgRecoverConfirm", "Are you sure you want to move these {0} files back to their original paths?\n(Note: If recovered, the original files will be overwritten)"},
            {"Rec_TitleRecoverResult", "Recovery Result"},
            {"Rec_MsgRecoverPartial", "Recovery Complete: {0} files.\n⚠️ {1} files failed to recover!\n(File might be locked or insufficient permissions)"},
            {"Rec_TitleRecoverComplete", "Recovery Complete"},
            {"Rec_MsgRecoverSuccess", "Successfully recovered {0} files to their original locations."},
            {"Rec_MsgSelectRecycle", "Please check the file records you want to discard first."},
            {"Rec_TitleRecycle", "Recycle Bin"},
            {"Rec_MsgRecycleConfirm", "Are you sure you want to completely send these {0} files to the Windows Recycle Bin?\n\n⚠️ Warning: Once sent, this system can no longer track or recover the image!\n(If emptied from the recycle bin later, the file is permanently lost)"},
            {"Rec_TitleRecycleResult", "Clean Result"},
            {"Rec_MsgRecyclePartial", "Clean Complete: {0} files.\n⚠️ {1} files failed to discard!"},
            {"Rec_TitleRecycleComplete", "Clean Complete"},
            {"Rec_MsgRecycleSuccess", "Moved {0} files to the Recycle Bin."},

            {"ReadMeContent", $@"# FastImageDupe v{AppVersion} - High Speed Image Deduplicator 🚀

This manual helps you quickly and safely organize massive amounts of images.

## 📍 Step 1: Set Scan Range & Mode
1. Target Paths: Select folders for scanning (includes sub-directories by default).
2. Scan Mode:
   * Personal Album (Strict): Zero tolerance. Absolutely prevents burst photos or images with different aspect ratios from being merged.
   * Internet Download (Loose): Allows slight compression, watermarks, or rotations. Best for cleaning up memes.

## 📍 Step 2: About Smart ""Move Modes"" and Big Groups
The system automatically analyzes relationships between folders and dynamically suggests the safest default actions:
* Same Directory: Move options are hidden. Safely keeps the best quality image and isolates copies.
* Cross-Directory: Defaults to ""Move all types"" to help you completely merge folders.
* Multi-Group (Sequential/Prefix Pattern): Defaults to ""Move Duplicates + Feature Matched Files"", accurately grouping batch photos without moving unrelated files.
* Multi-Group (Random Naming): Falls back to the conservative ""Process Duplicates Only"" to prevent chaotic merging of random images.
*(Note: You can always manually change the RadioButton to override system suggestions!)*

## 📍 Step 3: Handle Duplicates
* Manual Selection: Check items in the list or use the preview windows, then click the 'Isolate' button.
* Smart Process All (Recommended): Check multiple big groups at the top and click '🧹 Smart Process All'. The system automatically keeps the highest quality/largest file and isolates the rest based on the logic above.

## 📍 Step 4: Recovery & Deletion (Safety Mechanism)
* 🛡️ Soft Delete Protection: 'Deletions' in the main window simply move files to a dedicated `FastImageDupeDel` isolation folder. They are not permanently deleted.
* 🔄 Recovery Manager: Click '🔄 Recovery Manager' at the top to 'Recover' isolated files back to their original paths, or 'Send to System Recycle Bin' if you are absolutely sure.

⚠️ Important Notes:
1. Do not manually modify the `FastImageDupeDel` folder via Windows Explorer, as it invalidates recovery records.
2. Once you 'Send to System Recycle Bin', this software can no longer track or recover them.

---

## 🤝 Contact the Author

If this tool saved you time, or if you want to discuss system architecture and technology, feel free to contact me!
• Developer: Moses
• GitHub: https://github.com/mosesikimo
• Email: mosesikimo@gmail.com
• Line ID: mosesikimo
• Motto: ""Code speaks logic, I ferry thoughts across the river.""

## 🧑‍💻 About the Author: Using an enterprise factory-grade sledgehammer to clear out your duplicate images

""I am Moses, a software engineer who ferries thoughts.""

I have been rolling in IT and OT cross-domain integration for 21 years, specializing in system architecture and software-hardware integration, and recently exploring AI Agents.
My daily work isn't drawing pretty UIs, but tying enterprise core ERP, PLM systems, CNC machines, and QA measurement instruments all together.

""Data deduplication"" is essentially an extreme data comparison and I/O war.

My core battlefield lies in strict process state management and LEAN management, having led countless complex work orders, QA, and NC version control systems;
At the same time, I deeply cultivate PLM and R&D mass production cost management, from on-site material saving, material preparation optimization, all the way to meticulous cost-saving mechanisms.

As early as 20 years ago, I orchestrated the underlying architecture of ""real-time database synchronization across factories"" for two multinational factories;
Facing the backup of massive physical files, I also know how to balance performance and system load — precisely scheduling lossless docking of massive files three times a day, absolutely not blindly consuming network bandwidth.

These massive business logics of ""once crashed, data out of sync, the whole factory will stop, losing millions"" have forged my extreme paranoia for system stability.

Now, I brought this paranoia, tempered from remote backup and strict process error-proofing, into this compact image organization program.
I protect your images like ""multinational transactions"" — comparison allows no error, unexpected accidents shouldn't happen, not even the button to trigger them exists.

I believe ""code speaks"", and I believe even more that good technology should be like a deep, quiet river — calm on the surface, powerful inside, capable of carrying people across difficulties.

This software might not have the prettiest skin, but it has a strong heart tempered through 21 years in the enterprise battlefield.
Hand your hard drive over to it, grab a cup of coffee, and experience what ""absolutely precise data cleanup"" means.

## ✨ Core Technology Highlights: Dimensional Strike Underlying Engine

Most engineers write single functions, but I am used to building automatic error-proofing, precisely comparing underlying engines.
When this program is scanning your hard drive filled with TBs of images, what operates behind the scenes is an enterprise-grade concurrency control logic:

💥 Exclusive Dimensional Strike Engine (NEW)
Replaces traditional O(N³) nested loops with O(N) dictionary mapping and pure 256-bit bitwise operations, entirely eliminating unnecessary RGB overhead. Even when processing over 100,000 highly similar images, memory and CPU consumption approach zero, achieving true instant processing.

🏎️ Exclusive Ultra-Fast Pipeline
Adopts C# Parallel Task and lock-free architecture. Perfectly squeezes the limits of modern multi-core CPUs and SSDs, absolutely won't eat up memory, and definitely won't freeze halfway.

🧠 Smart Feature Cache
Fingerprints are permanently remembered once scanned. Ignores files being altered beyond recognition or moved randomly, second scan triggers ""cache instant kill"".

🛡️ Extreme Fault Tolerance
Automatically skips corrupted files without freezing; isolation and recovery use multi-level batch tracking and ghost-record prevention mechanisms, ensuring any operation can be safely undone."}
        };

        public static string Get(string key)
        {
            var dict = IsEnglish ? EnDict : ZhDict;
            return dict.TryGetValue(key, out string? val) && val != null ? val : key;
        }

        public static void TranslateWindow(Window window)
        {
            if (window == null) return;
            
            window.Title = Get(window.GetType().Name + "_Title");

            var fields = window.GetType().GetFields(
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.Public);
            
            var dict = IsEnglish ? EnDict : ZhDict;

            foreach (var field in fields)
            {
                if (dict.TryGetValue(field.Name, out string? translated) && translated != null)
                {
                    object? obj = field.GetValue(window);
                    if (obj == null) continue;

                    if (obj is TextBlock tb) tb.Text = translated;
                    else if (obj is HeaderedContentControl hcc) hcc.Header = translated; 
                    else if (obj is RadioButton rb) rb.Content = translated; 
                    else if (obj is ContentControl cc) { if (cc.Content is string) cc.Content = translated; } 
                    else if (obj is GridViewColumn gvc) gvc.Header = translated;
                    else if (obj is DataGridColumn dgc) dgc.Header = translated;
                    else if (obj is MenuItem mi) mi.Header = translated;
                }
            }
        }
    }
}