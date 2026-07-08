using System.Globalization;

namespace ScreenPaste.Settings;

/// <summary>A selectable UI language.</summary>
public readonly record struct LangOption(string Code, string Native, string FlagCode);

/// <summary>
/// Lightweight string localization. Call <see cref="Init"/> once at startup (and again
/// when the language changes), then use <see cref="T"/> to look up UI strings.
/// Simplified Chinese is intentionally excluded.
/// </summary>
public static class Loc
{
    public static readonly LangOption[] Languages =
    {
        new("zh-Hant", "繁體中文", "tw"),
        new("en", "English", "us"),
        new("ja", "日本語", "jp"),
        new("ko", "한국어", "kr"),
        new("fr", "Français", "fr"),
        new("de", "Deutsch", "de"),
        new("es", "Español", "es"),
    };

    private const string Fallback = "en";
    public static string Current { get; private set; } = "zh-Hant";

    // key -> (langCode -> text)
    private static readonly Dictionary<string, Dictionary<string, string>> Table = new();

    static Loc() => Seed();

    /// <summary>Resolve the effective language ("System" follows the OS UI culture).</summary>
    public static void Init(string? setting)
    {
        Current = Resolve(setting);
    }

    public static string Resolve(string? setting)
    {
        if (!string.IsNullOrWhiteSpace(setting) &&
            !setting.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            return Normalize(setting);
        }

        var c = CultureInfo.CurrentUICulture;
        // Traditional Chinese regions only; Simplified falls back to English.
        if (c.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            var n = c.Name.ToLowerInvariant();
            if (n.Contains("hant") || n.Contains("tw") || n.Contains("hk") || n.Contains("mo"))
                return "zh-Hant";
            return Fallback;
        }
        return Normalize(c.TwoLetterISOLanguageName);
    }

    private static string Normalize(string code)
    {
        code = code.Trim();
        foreach (var l in Languages)
            if (l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) return l.Code;
        // match by two-letter prefix (e.g. "ja-JP" -> "ja")
        var two = code.Length >= 2 ? code[..2].ToLowerInvariant() : code;
        foreach (var l in Languages)
            if (l.Code.StartsWith(two, StringComparison.OrdinalIgnoreCase)) return l.Code;
        return Fallback;
    }

    /// <summary>Look up a localized string; falls back to English then the key itself.</summary>
    public static string T(string key)
    {
        if (Table.TryGetValue(key, out var langs))
        {
            if (langs.TryGetValue(Current, out var s)) return s;
            if (langs.TryGetValue(Fallback, out var f)) return f;
        }
        return key;
    }

    /// <summary>Localized string with string.Format arguments.</summary>
    public static string T(string key, params object[] args) => string.Format(T(key), args);

    private static void Add(string key, string en, string zh, string ja, string ko, string fr, string de, string es)
        => Table[key] = new Dictionary<string, string>
        {
            ["en"] = en, ["zh-Hant"] = zh, ["ja"] = ja, ["ko"] = ko, ["fr"] = fr, ["de"] = de, ["es"] = es,
        };

    private static void Seed()
    {
        // key,              en,                     zh-Hant,        ja,                 ko,               fr,                  de,                  es
        Add("app.name",      "ScreenPaste",          "ScreenPaste",  "ScreenPaste",      "ScreenPaste",    "ScreenPaste",       "ScreenPaste",       "ScreenPaste");

        // Tray
        Add("tray.capture",  "Capture",              "截圖",          "キャプチャ",         "캡처",            "Capture",           "Aufnehmen",         "Capturar");
        Add("tray.settings", "Settings…",            "設定…",         "設定…",             "설정…",           "Paramètres…",       "Einstellungen…",    "Ajustes…");
        Add("tray.openFolder","Open save folder",    "開啟儲存資料夾", "保存フォルダーを開く", "저장 폴더 열기",   "Ouvrir le dossier", "Speicherordner öffnen","Abrir carpeta");
        Add("tray.exit",     "Exit",                 "結束",          "終了",              "종료",            "Quitter",           "Beenden",           "Salir");
        Add("tray.about",    "About…",               "關於…",         "バージョン情報…",     "정보…",           "À propos…",         "Über…",             "Acerca de…");
        Add("about.tagline", "A lightweight Windows screenshot & annotation tool.", "輕巧的 Windows 截圖與標註工具。", "軽量な Windows スクリーンショット・注釈ツール。", "가볍고 빠른 Windows 캡처·주석 도구.", "Outil léger de capture et d'annotation pour Windows.", "Schlankes Screenshot- & Anmerkungstool für Windows.", "Herramienta ligera de captura y anotación para Windows.");
        Add("about.version", "Version",              "版本",          "バージョン",         "버전",            "Version",           "Version",           "Versión");
        Add("about.repo",    "Project page",         "專案首頁",       "プロジェクトページ",   "프로젝트 페이지",   "Page du projet",    "Projektseite",      "Página del proyecto");
        Add("about.support", "Support the author",   "支持作者",       "作者を支援",         "개발자 후원",      "Soutenir l'auteur", "Autor unterstützen","Apoyar al autor");

        // Updates
        Add("set.updates",   "Updates",              "更新",          "更新",              "업데이트",         "Mises à jour",      "Updates",           "Actualizaciones");
        Add("set.checkStartup","Check for updates on startup", "啟動時檢查更新", "起動時に更新を確認", "시작 시 업데이트 확인", "Vérifier au démarrage", "Beim Start prüfen", "Buscar al iniciar");
        Add("set.checkNow",  "Check now",            "立即檢查更新",   "今すぐ確認",         "지금 확인",        "Vérifier maintenant","Jetzt prüfen",      "Buscar ahora");
        Add("upd.title",     "Software update",      "軟體更新",       "ソフトウェア更新",    "소프트웨어 업데이트","Mise à jour",       "Softwareupdate",    "Actualización");
        Add("upd.available", "A new version {0} is available (current {1}). Update now?", "有新版本 {0}（目前 {1}）。是否要更新？", "新しいバージョン {0} が利用可能です（現在 {1}）。更新しますか？", "새 버전 {0} 사용 가능(현재 {1}). 업데이트할까요?", "Nouvelle version {0} disponible (actuelle {1}). Mettre à jour ?", "Neue Version {0} verfügbar (aktuell {1}). Jetzt aktualisieren?", "Nueva versión {0} disponible (actual {1}). ¿Actualizar?");
        Add("upd.upToDate",  "You're on the latest version ({0}).", "已是最新版本（{0}）。", "最新バージョンです（{0}）。", "최신 버전입니다({0}).", "Vous avez la dernière version ({0}).", "Sie haben die neueste Version ({0}).", "Ya tienes la última versión ({0}).");
        Add("upd.failed",    "Could not check for updates.", "檢查更新失敗。", "更新を確認できませんでした。", "업데이트를 확인할 수 없습니다.", "Échec de la vérification.", "Update-Prüfung fehlgeschlagen.", "No se pudo comprobar.");
        Add("upd.now",       "Update now",           "立即更新",       "今すぐ更新",         "지금 업데이트",    "Mettre à jour",     "Jetzt aktualisieren","Actualizar");
        Add("upd.later",     "Later",                "稍後",          "後で",              "나중에",          "Plus tard",         "Später",            "Más tarde");
        Add("upd.openPage",  "Open page",            "開啟頁面",       "ページを開く",        "페이지 열기",      "Ouvrir la page",    "Seite öffnen",      "Abrir página");
        Add("upd.downloading","Downloading…",        "下載中…",        "ダウンロード中…",     "다운로드 중…",     "Téléchargement…",   "Wird geladen…",     "Descargando…");
        Add("tray.tip",      "ScreenPaste — press {0} to capture", "ScreenPaste — 按 {0} 截圖", "ScreenPaste — {0} でキャプチャ", "ScreenPaste — {0} 키로 캡처", "ScreenPaste — {0} pour capturer", "ScreenPaste — {0} zum Aufnehmen", "ScreenPaste — {0} para capturar");
        Add("tray.started",  "ScreenPaste is running — press {0} to capture", "ScreenPaste 正在執行中 — 按 {0} 截圖", "ScreenPaste は実行中です — {0} でキャプチャ", "ScreenPaste 실행 중 — {0} 키로 캡처", "ScreenPaste est actif — {0} pour capturer", "ScreenPaste läuft — {0} zum Aufnehmen", "ScreenPaste está activo — {0} para capturar");
        Add("msg.running",   "ScreenPaste is already running (check the system tray).", "ScreenPaste 已經在執行中（請查看系統匣）。", "ScreenPaste は既に実行中です（システムトレイを確認してください）。", "ScreenPaste가 이미 실행 중입니다(시스템 트레이 확인).", "ScreenPaste est déjà en cours d'exécution (voir la barre d'état).", "ScreenPaste läuft bereits (siehe Infobereich).", "ScreenPaste ya se está ejecutando (mira la bandeja).");
        Add("msg.hotkeyFail","Capture hotkey \"{0}\" could not be registered (invalid or in use). You can still capture from the tray.", "截圖熱鍵「{0}」註冊失敗（無效或被占用）。仍可由系統匣截圖。", "キャプチャのホットキー「{0}」を登録できません（無効か使用中）。トレイからは可能です。", "캡처 단축키 \"{0}\" 등록 실패(잘못되었거나 사용 중). 트레이에서 캡처 가능합니다.", "Le raccourci de capture « {0} » n'a pas pu être enregistré (invalide ou occupé). Utilisez la barre d'état.", "Aufnahme-Hotkey „{0}“ konnte nicht registriert werden (ungültig/belegt). Nutzung über Infobereich möglich.", "No se pudo registrar el atajo « {0} » (inválido u ocupado). Puedes capturar desde la bandeja.");
        Add("msg.captureFail","Capture failed: {0}", "截圖失敗：{0}", "キャプチャに失敗しました：{0}", "캡처 실패: {0}", "Échec de la capture : {0}", "Aufnahme fehlgeschlagen: {0}", "Error de captura: {0}");

        // Recording
        Add("tray.record",   "Record region",        "區域錄影",       "範囲を録画",         "영역 녹화",        "Enregistrer une zone","Bereich aufnehmen","Grabar región");
        Add("tray.recordStop","Stop recording",      "停止錄影",       "録画を停止",         "녹화 중지",        "Arrêter l'enregistrement","Aufnahme stoppen","Detener grabación");
        Add("rec.stop",      "Stop",                 "停止",          "停止",              "중지",            "Arrêter",           "Stopp",             "Detener");
        Add("rec.selectHint","Drag to select the recording area · Esc to cancel", "拖曳選取錄影範圍 · Esc 取消", "ドラッグで録画範囲を選択 · Esc でキャンセル", "드래그하여 녹화 영역 선택 · Esc 취소", "Glissez pour choisir la zone · Échap pour annuler", "Ziehen, um den Aufnahmebereich zu wählen · Esc zum Abbrechen", "Arrastra para elegir el área · Esc para cancelar");
        Add("rec.encoding",  "Encoding recording…",  "錄影編碼中…",    "録画をエンコード中…", "녹화 인코딩 중…",  "Encodage en cours…","Aufnahme wird codiert…","Codificando grabación…");
        Add("rec.saved",     "Recording saved: {0}", "錄影已儲存：{0}", "録画を保存しました：{0}", "녹화 저장됨: {0}", "Enregistrement enregistré : {0}", "Aufnahme gespeichert: {0}", "Grabación guardada: {0}");
        Add("rec.failed",    "Recording failed: {0}","錄影失敗：{0}",   "録画に失敗しました：{0}", "녹화 실패: {0}", "Échec de l'enregistrement : {0}", "Aufnahme fehlgeschlagen: {0}", "Error de grabación: {0}");
        Add("rec.noFfmpeg",  "ffmpeg was not found. Region recording needs the bundled ffmpeg.exe.", "找不到 ffmpeg，區域錄影需要隨附的 ffmpeg.exe。", "ffmpeg が見つかりません。範囲録画には同梱の ffmpeg.exe が必要です。", "ffmpeg을 찾을 수 없습니다. 영역 녹화에는 포함된 ffmpeg.exe가 필요합니다.", "ffmpeg introuvable. L'enregistrement nécessite le ffmpeg.exe fourni.", "ffmpeg nicht gefunden. Die Bereichsaufnahme benötigt die mitgelieferte ffmpeg.exe.", "No se encontró ffmpeg. La grabación requiere el ffmpeg.exe incluido.");
        Add("set.recording", "Recording",            "錄影",          "録画",              "녹화",            "Enregistrement",    "Aufnahme",          "Grabación");
        Add("set.record",    "Record region",        "區域錄影",       "範囲録画",           "영역 녹화",        "Enregistrer une zone","Bereich aufnehmen","Grabar región");
        Add("set.recordFormat","Format",             "格式",          "形式",              "형식",            "Format",            "Format",            "Formato");
        Add("set.recordFps", "Frame rate",           "影格率",         "フレームレート",      "프레임 속도",      "Fréquence d'images","Bildrate",          "Fotogramas/s");
        Add("set.recordCursor","Capture cursor",     "錄製滑鼠游標",   "カーソルを含める",    "커서 포함",        "Inclure le curseur","Cursor aufnehmen",  "Incluir cursor");
        Add("set.skipEditor", "Save immediately after recording (skip editor)", "錄完直接儲存（跳過編輯器）", "録画後すぐ保存（エディターをスキップ）", "녹화 후 바로 저장(편집기 건너뛰기)", "Enregistrer immédiatement (sans éditeur)", "Nach Aufnahme sofort speichern (Editor überspringen)", "Guardar al terminar (omitir editor)");

        // Recording editor
        Add("edit.title",    "Edit recording",       "編輯錄影",       "録画を編集",         "녹화 편집",        "Modifier l'enregistrement","Aufnahme bearbeiten","Editar grabación");
        Add("edit.hint",     "Space: play/pause · ←/→: frame step · I/O: set trim in/out", "Space 播放/暫停 · ←→ 逐格 · I/O 設定起點/終點", "Space 再生/一時停止 · ←→ コマ送り · I/O で開始/終了を設定", "Space 재생/일시정지 · ←→ 프레임 이동 · I/O 시작/끝 설정", "Espace : lecture/pause · ←/→ : image par image · I/O : début/fin", "Leertaste: Wiedergabe/Pause · ←/→: Einzelbild · I/O: Start/Ende setzen", "Espacio: reproducir/pausa · ←/→: fotograma · I/O: fijar inicio/fin");
        Add("edit.exporting","Exporting… {0}%",      "匯出中… {0}%",   "エクスポート中… {0}%", "내보내는 중… {0}%", "Exportation… {0} %","Exportieren… {0} %","Exportando… {0}%");
        Add("edit.discard",  "Discard this recording?","要捨棄這段錄影嗎？","この録画を破棄しますか？","이 녹화를 삭제할까요?","Abandonner cet enregistrement ?","Diese Aufnahme verwerfen?","¿Descartar esta grabación?");

        // Tools
        Add("tool.marker",   "Marker",               "麥克筆",        "マーカー",           "마커",            "Marqueur",          "Marker",            "Marcador");
        Add("tool.highlighter","Highlighter",        "螢光筆",        "蛍光ペン",           "형광펜",          "Surligneur",        "Textmarker",        "Resaltador");
        Add("tool.text",     "Text",                 "文字",          "テキスト",           "텍스트",          "Texte",             "Text",              "Texto");
        Add("tool.shape",    "Shape",                "形狀",          "図形",              "도형",            "Forme",             "Form",              "Forma");
        Add("tool.sticker",  "Paste image",          "貼圖",          "画像を貼り付け",      "이미지 붙여넣기",  "Coller une image",  "Bild einfügen",     "Pegar imagen");
        Add("tool.blur",     "Blur",                 "模糊",          "ぼかし",             "흐림",            "Flou",              "Weichzeichnen",     "Desenfoque");

        // Actions (hotkey appended in code)
        Add("action.undo",   "Undo",                 "復原",          "元に戻す",           "실행 취소",        "Annuler",           "Rückgängig",        "Deshacer");
        Add("action.redo",   "Redo",                 "重做",          "やり直し",           "다시 실행",        "Rétablir",          "Wiederholen",       "Rehacer");
        Add("action.copy",   "Copy",                 "複製",          "コピー",             "복사",            "Copier",            "Kopieren",          "Copiar");
        Add("action.save",   "Save",                 "儲存",          "保存",              "저장",            "Enregistrer",       "Speichern",         "Guardar");
        Add("action.pin",    "Pin to screen",        "釘選到螢幕",     "画面に固定",         "화면에 고정",      "Épingler à l'écran","Am Bildschirm anheften","Fijar en pantalla");
        Add("action.close",  "Close",                "關閉",          "閉じる",             "닫기",            "Fermer",            "Schließen",         "Cerrar");

        // Option labels
        Add("lbl.width",     "Width",                "粗細",          "太さ",              "굵기",            "Épaisseur",         "Stärke",            "Grosor");
        Add("lbl.opacity",   "Opacity",              "透明度",        "不透明度",           "투명도",          "Opacité",           "Deckkraft",         "Opacidad");
        Add("lbl.color",     "Color",                "顏色",          "色",                "색",             "Couleur",           "Farbe",             "Color");
        Add("lbl.type",      "Type",                 "類型",          "種類",              "종류",            "Type",              "Typ",               "Tipo");
        Add("lbl.gaussian",  "Gaussian",             "高斯",          "ガウス",             "가우시안",        "Gaussien",          "Gauß",              "Gaussiano");
        Add("lbl.mosaic",    "Mosaic",               "馬賽克",        "モザイク",           "모자이크",        "Mosaïque",          "Mosaik",            "Mosaico");
        Add("lbl.blurStrength","Strength",           "模糊程度",       "強さ",              "강도",            "Intensité",         "Stärke",            "Intensidad");
        Add("lbl.font",      "Font",                 "字體",          "フォント",           "글꼴",            "Police",            "Schrift",           "Fuente");
        Add("lbl.size",      "Size",                 "大小",          "サイズ",             "크기",            "Taille",            "Größe",             "Tamaño");
        Add("lbl.style",     "Style",                "樣式",          "スタイル",           "스타일",          "Style",             "Stil",              "Estilo");
        Add("lbl.shape",     "Shape",                "形狀",          "図形",              "도형",            "Forme",             "Form",              "Forma");
        Add("shape.rect",    "Rectangle",            "方形",          "四角形",             "사각형",          "Rectangle",         "Rechteck",          "Rectángulo");
        Add("shape.rounded", "Rounded",              "圓角",          "角丸",              "둥근 사각형",     "Arrondi",           "Abgerundet",        "Redondeado");
        Add("shape.ellipse", "Ellipse",              "圓形",          "楕円",              "원",             "Ellipse",           "Ellipse",           "Elipse");
        Add("style.outline", "Outline",              "外框",          "枠線",              "테두리",          "Contour",           "Umriss",            "Contorno");
        Add("style.fill",    "Fill",                 "填滿",          "塗りつぶし",          "채우기",          "Rempli",            "Gefüllt",           "Relleno");
        Add("lbl.lineWidth", "Line width",           "線條粗細",       "線の太さ",           "선 굵기",         "Épaisseur",         "Linienstärke",      "Grosor de línea");
        Add("sticker.choose","Choose image…",        "選擇圖片…",      "画像を選択…",         "이미지 선택…",     "Choisir une image…","Bild wählen…",      "Elegir imagen…");
        Add("sticker.hint",  "  drag to move, wheel to resize", "  可拖曳移動、滾輪縮放", "  ドラッグで移動・ホイールで拡大縮小", "  드래그 이동, 휠 크기 조절", "  glisser/molette", "  ziehen/scrollen", "  arrastrar/rueda");
        Add("color.more",    "More colors / enter Hex","更多顏色 / 輸入 Hex", "他の色 / Hex 入力", "더 많은 색 / Hex 입력", "Plus de couleurs / Hex", "Mehr Farben / Hex", "Más colores / Hex");

        // Color picker
        Add("cp.title",      "Choose color",         "選擇顏色",       "色の選択",           "색 선택",         "Choisir une couleur","Farbe wählen",     "Elegir color");
        Add("cp.opacity",    "Opacity",              "透明度",        "不透明度",           "투명도",          "Opacité",           "Deckkraft",         "Opacidad");
        Add("common.ok",     "OK",                   "確定",          "OK",                "확인",            "OK",                "OK",                "Aceptar");
        Add("common.cancel", "Cancel",               "取消",          "キャンセル",         "취소",            "Annuler",           "Abbrechen",         "Cancelar");
        Add("common.save",   "Save",                 "儲存",          "保存",              "저장",            "Enregistrer",       "Speichern",         "Guardar");

        // Pin window
        Add("pin.copy",      "Copy (Ctrl+C)",        "複製 (Ctrl+C)", "コピー (Ctrl+C)",    "복사 (Ctrl+C)",   "Copier (Ctrl+C)",   "Kopieren (Strg+C)", "Copiar (Ctrl+C)");
        Add("pin.save",      "Save…",                "儲存…",         "保存…",             "저장…",           "Enregistrer…",      "Speichern…",        "Guardar…");
        Add("pin.reset",     "Reset zoom (100%)",    "重設縮放 (100%)","ズームをリセット (100%)","확대/축소 초기화 (100%)","Zoom 100 %",   "Zoom zurücksetzen (100%)","Restablecer zoom (100%)");
        Add("pin.close",     "Close (Esc)",          "關閉 (Esc)",    "閉じる (Esc)",       "닫기 (Esc)",      "Fermer (Échap)",    "Schließen (Esc)",   "Cerrar (Esc)");

        // Settings window
        Add("set.title",     "ScreenPaste Settings", "ScreenPaste 設定","ScreenPaste 設定",  "ScreenPaste 설정","Paramètres ScreenPaste","ScreenPaste-Einstellungen","Ajustes de ScreenPaste");
        Add("set.hotkeys",   "Hotkeys",              "熱鍵",          "ホットキー",         "단축키",          "Raccourcis",        "Tastenkürzel",      "Atajos");
        Add("set.hotkeyHint","Click a field and press the desired key combo; Delete clears it.", "點選欄位後直接按下想要的按鍵組合；Delete 可清除。", "欄をクリックして希望のキーを押します。Delete で消去。", "칸을 클릭하고 원하는 키 조합을 누르세요. Delete로 지웁니다.", "Cliquez puis appuyez sur la combinaison ; Suppr pour effacer.", "Feld anklicken und Tastenkombination drücken; Entf löscht.", "Haz clic y pulsa la combinación; Supr para borrar.");
        Add("set.capture",   "Capture",              "截圖",          "キャプチャ",         "캡처",            "Capture",           "Aufnehmen",         "Capturar");
        Add("set.quickSave", "Quick save",           "快速儲存",       "クイック保存",        "빠른 저장",        "Enreg. rapide",     "Schnellspeichern",  "Guardado rápido");
        Add("set.appearance","Appearance & startup", "外觀與啟動",     "外観と起動",         "모양 및 시작",     "Apparence & démarrage","Aussehen & Start", "Apariencia e inicio");
        Add("set.language",  "Language",             "語言",          "言語",              "언어",            "Langue",            "Sprache",           "Idioma");
        Add("set.theme",     "Theme",                "主題",          "テーマ",             "테마",            "Thème",             "Design",            "Tema");
        Add("set.startup",   "Run at startup",       "開機時自動啟動",  "起動時に自動実行",    "시작 시 자동 실행", "Lancer au démarrage","Beim Start ausführen","Ejecutar al inicio");
        Add("set.saveSection","Saving",              "儲存",          "保存",              "저장",            "Enregistrement",    "Speichern",         "Guardado");
        Add("set.saveFolder","Default folder",       "預設資料夾",     "既定のフォルダー",    "기본 폴더",        "Dossier par défaut","Standardordner",    "Carpeta predeterminada");
        Add("set.browse",    "Browse…",              "瀏覽…",         "参照…",             "찾아보기…",        "Parcourir…",        "Durchsuchen…",      "Examinar…");
        Add("theme.system",  "Follow system",        "跟隨系統",       "システムに従う",      "시스템 설정",      "Système",           "System",            "Sistema");
        Add("theme.light",   "Light",                "淺色",          "ライト",             "라이트",          "Clair",             "Hell",              "Claro");
        Add("theme.dark",    "Dark",                 "深色",          "ダーク",             "다크",            "Sombre",            "Dunkel",            "Oscuro");

        // Misc
        Add("img.filter",    "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*", "圖片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|所有檔案 (*.*)|*.*", "画像 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|すべてのファイル (*.*)|*.*", "이미지 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|모든 파일 (*.*)|*.*", "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Tous les fichiers (*.*)|*.*", "Bilder (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Alle Dateien (*.*)|*.*", "Imágenes (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Todos los archivos (*.*)|*.*");
        Add("img.chooseTitle","Choose image",        "選擇圖片",       "画像を選択",         "이미지 선택",      "Choisir une image", "Bild wählen",       "Elegir imagen");
        Add("img.loadFail",  "Could not load this image file.", "無法載入這個圖片檔。", "この画像ファイルを読み込めません。", "이 이미지 파일을 불러올 수 없습니다.", "Impossible de charger cette image.", "Diese Bilddatei konnte nicht geladen werden.", "No se pudo cargar la imagen.");
    }
}
