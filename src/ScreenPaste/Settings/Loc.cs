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
        Add("tray.customSnip","Custom snip…",        "自訂截圖…",      "カスタムキャプチャ…",  "사용자 지정 캡처…", "Capture personnalisée…", "Benutzerdefinierte Aufnahme…", "Captura personalizada…");
        Add("tray.paste",    "Paste from clipboard", "貼上剪貼簿",     "クリップボードから貼り付け", "클립보드에서 붙여넣기", "Coller le presse-papiers", "Aus Zwischenablage einfügen", "Pegar del portapapeles");
        Add("tray.hidePins", "Hide / show pins",     "隱藏／顯示貼圖", "ピン留め画像を隠す／表示", "고정 이미지 숨기기/표시", "Masquer / afficher les images épinglées", "Angeheftete Bilder aus-/einblenden", "Ocultar / mostrar imágenes fijadas");
        Add("tray.pinsClickable","Make all pins clickable", "讓所有貼圖恢復可點擊", "すべてのピン留め画像をクリック可能にする", "모든 고정 이미지를 클릭 가능하게", "Rendre toutes les images épinglées cliquables", "Alle angehefteten Bilder wieder anklickbar machen", "Hacer clicables todas las imágenes fijadas");
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
        Add("msg.hotkeysFail","These hotkeys could not be registered (invalid or in use by another app): {0}", "以下熱鍵註冊失敗（無效或被其他程式占用）：{0}", "次のホットキーを登録できません（無効か他のアプリが使用中）：{0}", "다음 단축키를 등록하지 못했습니다(잘못되었거나 다른 앱이 사용 중): {0}", "Ces raccourcis n'ont pas pu être enregistrés (invalides ou utilisés par une autre application) : {0}", "Diese Tastenkürzel konnten nicht registriert werden (ungültig oder von einer anderen App belegt): {0}", "No se pudieron registrar estos atajos (inválidos u ocupados por otra aplicación): {0}");
        Add("msg.historyCleared","Snip history cleared", "已清除截圖歷史", "キャプチャ履歴を消去しました", "캡처 기록을 지웠습니다", "Historique des captures effacé", "Aufnahmeverlauf gelöscht", "Historial de capturas borrado");
        Add("paste.nothing", "The clipboard holds nothing that can be pinned.", "剪貼簿中沒有可以貼上的內容。", "クリップボードに固定できる内容がありません。", "클립보드에 고정할 수 있는 내용이 없습니다.", "Le presse-papiers ne contient rien à épingler.", "Die Zwischenablage enthält nichts zum Anheften.", "El portapapeles no contiene nada que se pueda fijar.");

        // Custom snip dialog (Shift+F1)
        Add("custom.title",  "Custom snip",          "自訂截圖",       "カスタムキャプチャ",   "사용자 지정 캡처",  "Capture personnalisée", "Benutzerdefinierte Aufnahme", "Captura personalizada");
        Add("custom.hint",   "Position and size in screen pixels. The snip opens once the delay is over.", "位置與大小以螢幕像素為單位，延遲結束後開始截圖。", "位置とサイズは画面のピクセル単位です。遅延の後にキャプチャが始まります。", "위치와 크기는 화면 픽셀 단위입니다. 지연 시간이 지나면 캡처가 시작됩니다.", "Position et taille en pixels d'écran. La capture s'ouvre une fois le délai écoulé.", "Position und Größe in Bildschirmpixeln. Die Aufnahme beginnt nach der Verzögerung.", "Posición y tamaño en píxeles de pantalla. La captura se abre al terminar el retardo.");
        Add("custom.left",   "Left (px)",            "左 (px)",       "左 (px)",           "왼쪽 (px)",       "Gauche (px)",       "Links (px)",        "Izquierda (px)");
        Add("custom.top",    "Top (px)",             "上 (px)",       "上 (px)",           "위 (px)",         "Haut (px)",         "Oben (px)",         "Arriba (px)");
        Add("custom.width",  "Width (px)",           "寬度 (px)",     "幅 (px)",           "너비 (px)",       "Largeur (px)",      "Breite (px)",       "Ancho (px)");
        Add("custom.height", "Height (px)",          "高度 (px)",     "高さ (px)",         "높이 (px)",       "Hauteur (px)",      "Höhe (px)",         "Alto (px)");
        Add("custom.lastArea","Last area",           "上次的範圍",     "前回の範囲",         "지난 영역",        "Dernière zone",     "Letzter Bereich",   "Última área");
        Add("custom.invalid","Enter whole numbers and a delay in seconds; the area must be at least 1 px and on the screen.", "請輸入整數與延遲秒數；範圍至少 1 像素且須在螢幕上。", "整数と遅延の秒数を入力してください。範囲は 1 px 以上で画面内にある必要があります。", "정수와 지연 시간(초)을 입력하세요. 영역은 1px 이상이고 화면 안에 있어야 합니다.", "Saisissez des nombres entiers et un délai en secondes ; la zone doit faire au moins 1 px et être à l'écran.", "Ganze Zahlen und eine Verzögerung in Sekunden eingeben; der Bereich muss mindestens 1 px groß und auf dem Bildschirm sein.", "Introduce números enteros y un retardo en segundos; el área debe medir al menos 1 px y estar en pantalla.");

        // Recording
        Add("tray.record",   "Record region",        "區域錄影",       "範囲を録画",         "영역 녹화",        "Enregistrer une zone","Bereich aufnehmen","Grabar región");
        Add("tray.recordStop","Stop recording",      "停止錄影",       "録画を停止",         "녹화 중지",        "Arrêter l'enregistrement","Aufnahme stoppen","Detener grabación");
        Add("rec.stop",      "Stop",                 "停止",          "停止",              "중지",            "Arrêter",           "Stopp",             "Detener");
        Add("rec.selectHint","Drag to select · click a window to auto-detect · wheel to adjust · Esc to cancel", "拖曳選取・點擊視窗自動辨識・滾輪調整範圍・Esc 取消", "ドラッグで選択・ウィンドウをクリックで自動検出・ホイールで調整・Esc でキャンセル", "드래그로 선택 · 창 클릭 시 자동 감지 · 휠로 조절 · Esc 취소", "Glissez pour choisir · cliquez une fenêtre pour la détecter · molette pour ajuster · Échap pour annuler", "Ziehen zum Wählen · Fenster anklicken für Auto-Erkennung · Rad zum Anpassen · Esc zum Abbrechen", "Arrastra para elegir · clic en una ventana para detectarla · rueda para ajustar · Esc para cancelar");
        Add("rec.encoding",  "Encoding recording…",  "錄影編碼中…",    "録画をエンコード中…", "녹화 인코딩 중…",  "Encodage en cours…","Aufnahme wird codiert…","Codificando grabación…");
        Add("rec.displayChanged","Display settings changed — recording stopped", "顯示設定已變更 — 已停止錄影", "ディスプレイ設定が変更されました — 録画を停止しました", "디스플레이 설정이 변경됨 — 녹화 중지됨", "Paramètres d'affichage modifiés — enregistrement arrêté", "Anzeigeeinstellungen geändert — Aufnahme gestoppt", "Configuración de pantalla cambiada — grabación detenida");
        Add("rec.audioUnavailable","No audio device — recording without sound", "找不到音訊裝置 — 將以無聲錄影", "音声デバイスがありません — 無音で録画します", "오디오 장치 없음 — 소리 없이 녹화합니다", "Aucun périphérique audio — enregistrement sans son", "Kein Audiogerät — Aufnahme ohne Ton", "Sin dispositivo de audio — grabación sin sonido");
        Add("rec.saved",     "Recording saved: {0}", "錄影已儲存：{0}", "録画を保存しました：{0}", "녹화 저장됨: {0}", "Enregistrement enregistré : {0}", "Aufnahme gespeichert: {0}", "Grabación guardada: {0}");
        Add("rec.failed",    "Recording failed: {0}","錄影失敗：{0}",   "録画に失敗しました：{0}", "녹화 실패: {0}", "Échec de l'enregistrement : {0}", "Aufnahme fehlgeschlagen: {0}", "Error de grabación: {0}");
        Add("rec.noFfmpeg",  "ffmpeg was not found. Region recording needs the bundled ffmpeg.exe.", "找不到 ffmpeg，區域錄影需要隨附的 ffmpeg.exe。", "ffmpeg が見つかりません。範囲録画には同梱の ffmpeg.exe が必要です。", "ffmpeg을 찾을 수 없습니다. 영역 녹화에는 포함된 ffmpeg.exe가 필요합니다.", "ffmpeg introuvable. L'enregistrement nécessite le ffmpeg.exe fourni.", "ffmpeg nicht gefunden. Die Bereichsaufnahme benötigt die mitgelieferte ffmpeg.exe.", "No se encontró ffmpeg. La grabación requiere el ffmpeg.exe incluido.");
        Add("set.recording", "Recording",            "錄影",          "録画",              "녹화",            "Enregistrement",    "Aufnahme",          "Grabación");
        Add("set.record",    "Record region",        "區域錄影",       "範囲録画",           "영역 녹화",        "Enregistrer une zone","Bereich aufnehmen","Grabar región");
        Add("set.recordFormat","Format",             "格式",          "形式",              "형식",            "Format",            "Format",            "Formato");
        Add("set.recordFps", "Frame rate",           "影格率",         "フレームレート",      "프레임 속도",      "Fréquence d'images","Bildrate",          "Fotogramas/s");
        Add("set.recordAudio","Audio",               "聲音",          "音声",              "오디오",          "Audio",             "Audio",             "Audio");
        Add("set.recordAudioHint","Audio is saved only in MP4 — GIF and WebP are silent.", "聲音僅存於 MP4 — GIF 與 WebP 無聲。", "音声は MP4 のみ保存されます — GIF と WebP は無音です。", "오디오는 MP4에만 저장됩니다 — GIF와 WebP는 무음입니다.", "L'audio n'est enregistré qu'en MP4 — GIF et WebP sont muets.", "Audio wird nur in MP4 gespeichert — GIF und WebP sind stumm.", "El audio solo se guarda en MP4 — GIF y WebP son silenciosos.");
        Add("audio.none",    "None",                 "無",            "なし",              "없음",            "Aucun",             "Kein",              "Ninguno");
        Add("audio.system",  "System sound",         "系統聲音",       "システム音",         "시스템 소리",      "Son système",       "Systemklang",       "Sonido del sistema");
        Add("audio.mic",     "Microphone",           "麥克風",         "マイク",            "마이크",          "Microphone",        "Mikrofon",          "Micrófono");
        Add("audio.both",    "System + microphone",  "系統聲音 + 麥克風", "システム音 + マイク", "시스템 소리 + 마이크", "Système + microphone", "System + Mikrofon", "Sistema + micrófono");
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
        Add("tool.line",     "Line / arrow",         "直線 / 箭頭",   "直線 / 矢印",        "직선 / 화살표",    "Ligne / flèche",    "Linie / Pfeil",     "Línea / flecha");
        Add("line.arrowStart","Start arrow",         "起點箭頭",       "始点の矢印",         "시작 화살표",      "Flèche au début",   "Pfeil am Anfang",   "Flecha al inicio");
        Add("line.arrowEnd", "End arrow",            "終點箭頭",       "終点の矢印",         "끝 화살표",        "Flèche à la fin",   "Pfeil am Ende",     "Flecha al final");
        Add("tool.sticker",  "Paste image",          "貼圖",          "画像を貼り付け",      "이미지 붙여넣기",  "Coller une image",  "Bild einfügen",     "Pegar imagen");
        Add("tool.blur",     "Blur",                 "模糊",          "ぼかし",             "흐림",            "Flou",              "Weichzeichnen",     "Desenfoque");
        Add("tool.magnify",  "Magnifier",            "局部放大",      "拡大表示",           "부분 확대",       "Loupe",             "Lupe",              "Lupa");
        Add("magnify.hint",  "drag to frame the area to enlarge; the wheel re-zooms it once selected", "拖曳框出要放大的區域；選取後可以滾輪調倍率", "拡大したい範囲をドラッグで囲む；選択中はホイールで倍率を変更", "확대할 영역을 드래그하세요. 선택 후 휠로 배율 조정", "glissez pour cadrer la zone à agrandir ; molette pour le zoom", "Bereich zum Vergrößern aufziehen; Rad ändert den Zoom", "arrastra para encuadrar el área; la rueda cambia el zoom");
        Add("tool.number",   "Number marker",        "序號標記",       "番号マーカー",        "번호 표시",        "Numérotation",      "Nummernmarke",      "Marcador numérico");
        Add("number.hint",   "click to place the next number; + / - change it", "點一下放下一個序號；+ / - 調整數字", "クリックで次の番号を配置、+ / - で番号を変更", "클릭하면 다음 번호를 놓습니다. + / - 로 번호 변경", "cliquez pour placer le numéro suivant ; + / - pour le changer", "klicken setzt die nächste Nummer; + / - ändert sie", "haz clic para colocar el siguiente número; + / - lo cambian");
        Add("number.next",   "Next",                 "下一個",        "次の番号",           "다음 번호",        "Suivant",           "Nächste",           "Siguiente");
        Add("tool.eraser",   "Eraser",               "橡皮擦",        "消しゴム",           "지우개",          "Gomme",             "Radierer",          "Borrador");
        Add("eraser.hint",   "  drag across what to erase", "  拖過要擦除的筆觸或標註", "  消したい線や注釈の上をドラッグ", "  지울 획이나 주석 위로 드래그", "  glissez sur ce qu'il faut effacer", "  über das zu Löschende ziehen", "  arrastra sobre lo que quieras borrar");
        Add("line.stripHint","click point by point for a line strip; right-click, Enter or a double-click ends it", "逐點點擊可畫折線；按右鍵、Enter 或按兩下結束", "点を順にクリックすると折れ線、右クリック・Enter・ダブルクリックで終了", "점을 차례로 클릭하면 꺾은선, 우클릭·Enter·더블 클릭으로 끝냅니다", "cliquez point par point pour une ligne brisée ; clic droit, Entrée ou double-clic pour finir", "Punkt für Punkt klicken für einen Linienzug; Rechtsklick, Eingabe oder Doppelklick beendet ihn", "haz clic punto a punto para una polilínea; clic derecho, Intro o doble clic para terminar");
        Add("annot.copied",  "annotation copied",    "已複製標註",     "注釈をコピーしました", "주석 복사됨",       "annotation copiée", "Anmerkung kopiert", "anotación copiada");
        Add("tool.tabVariant","Tab: switch type",    "Tab 切換類型",   "Tab で種類を切替",    "Tab: 종류 전환",   "Tab : changer de type","Tab: Typ wechseln", "Tab: cambiar tipo");
        Add("lbl.zoom",      "Zoom",                 "倍率",          "倍率",              "배율",            "Zoom",              "Zoom",              "Zoom");
        Add("lbl.border",    "Border",               "邊框",          "枠線",              "테두리",          "Bordure",           "Rahmen",            "Borde");
        Add("magnify.connector","Connector",         "連接線",    "引き線",            "연결선",          "Trait de liaison",  "Verbindung",        "Conector");
        Add("magnify.shadow","Shadow",               "陰影",          "影",                "그림자",          "Ombre",             "Schatten",          "Sombra");
        Add("magnify.smooth","Smooth",               "平滑",          "滑らか",            "부드럽게",      "Lissage",           "Glätten",          "Suavizado");
        Add("magnify.withAnnotations","With annotations","含標註",  "注釈を含む",        "주석 포함",      "Avec annotations",  "Mit Anmerkungen",   "Con anotaciones");

        // Actions (hotkey appended in code)
        Add("action.undo",   "Undo",                 "復原",          "元に戻す",           "실행 취소",        "Annuler",           "Rückgängig",        "Deshacer");
        Add("action.redo",   "Redo",                 "重做",          "やり直し",           "다시 실행",        "Rétablir",          "Wiederholen",       "Rehacer");
        Add("action.copy",   "Copy",                 "複製",          "コピー",             "복사",            "Copier",            "Kopieren",          "Copiar");
        Add("action.save",   "Save",                 "儲存",          "保存",              "저장",            "Enregistrer",       "Speichern",         "Guardar");
        Add("action.pin",    "Pin to screen",        "釘選到螢幕",     "画面に固定",         "화면에 고정",      "Épingler à l'écran","Am Bildschirm anheften","Fijar en pantalla");
        Add("action.close",  "Close",                "關閉",          "閉じる",             "닫기",            "Fermer",            "Schließen",         "Cerrar");
        Add("action.clearAll","Clear all annotations","清除所有標註",  "すべての注釈を消去",   "모든 주석 지우기",  "Effacer toutes les annotations","Alle Anmerkungen löschen","Borrar todas las anotaciones");
        Add("action.shiftQuickSave","Shift+click: quick save", "Shift+點擊：快速儲存", "Shift+クリックでクイック保存", "Shift+클릭: 빠른 저장", "Maj+clic : enregistrement rapide", "Umschalt+Klick: Schnellspeichern", "Mayús+clic: guardado rápido");
        Add("size.copied",   "size copied",          "已複製尺寸",     "サイズをコピーしました", "크기 복사됨",     "taille copiée",     "Größe kopiert",     "tamaño copiado");
        Add("size.copyFailed","size not copied",     "無法複製尺寸",   "サイズをコピーできませんでした", "크기를 복사하지 못함", "taille non copiée", "Größe nicht kopiert", "tamaño no copiado");
        Add("print.failed",  "Could not print: {0}", "無法列印：{0}",   "印刷できませんでした：{0}", "인쇄하지 못했습니다: {0}", "Impossible d'imprimer : {0}", "Drucken fehlgeschlagen: {0}", "No se pudo imprimir: {0}");
        Add("copyFile.failed","Could not copy the capture as a file.", "無法將截圖複製為檔案。", "キャプチャをファイルとしてコピーできませんでした。", "캡처를 파일로 복사하지 못했습니다.", "Impossible de copier la capture en tant que fichier.", "Die Aufnahme konnte nicht als Datei kopiert werden.", "No se pudo copiar la captura como archivo.");

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
        Add("color.removeHint","Custom color — right-click to remove", "自訂顏色 — 右鍵點擊移除", "カスタム色 — 右クリックで削除", "사용자 지정 색 — 우클릭으로 삭제", "Couleur personnalisée — clic droit pour supprimer", "Eigene Farbe — Rechtsklick zum Entfernen", "Color personalizado — clic derecho para eliminar");

        // Magnifier readout
        Add("mag.copy",      "C: copy color",        "C 複製色彩值",   "C でカラー値をコピー", "C: 색상 값 복사",  "C : copier la couleur","C: Farbwert kopieren","C: copiar color");
        Add("mag.format",    "Shift: HEX / RGB / HSV", "Shift 切換 HEX / RGB / HSV", "Shift で HEX / RGB / HSV 切替", "Shift: HEX / RGB / HSV 전환", "Maj : HEX / RGB / HSV","Umschalt: HEX/RGB/HSV","Mayús: HEX / RGB / HSV");
        Add("mag.picked",    "Alt+C: color picked",  "Alt+C 已取色",   "Alt+C で色を取得",    "Alt+C: 색 가져옴",  "Alt+C : couleur prélevée","Alt+C: Farbe übernommen","Alt+C: color tomado");

        // Detection mode on the size chip while framing (Tab switches)
        Add("detect.element","Element",              "元素",          "要素",              "요소",            "Élément",           "Element",           "Elemento");
        Add("detect.window", "Window",               "視窗",          "ウィンドウ",         "창",              "Fenêtre",           "Fenster",           "Ventana");

        // Discard-capture confirmation
        Add("dlg.discardTitle","Cancel capture",     "取消截圖",       "キャプチャを中止",     "캡처 취소",        "Annuler la capture","Aufnahme abbrechen","Cancelar captura");
        Add("dlg.discardMsg","Discard this capture? Your annotations will be lost.", "要取消這次截圖嗎？未儲存的標註將會遺失。", "このキャプチャを破棄しますか？注釈は失われます。", "이 캡처를 취소할까요? 주석이 사라집니다.", "Abandonner cette capture ? Vos annotations seront perdues.", "Diese Aufnahme verwerfen? Ihre Anmerkungen gehen verloren.", "¿Descartar esta captura? Se perderán tus anotaciones.");
        Add("dlg.discardYes","Discard",              "放棄截圖",       "破棄",              "취소",            "Abandonner",        "Verwerfen",         "Descartar");
        Add("dlg.discardNo", "Keep editing",         "繼續編輯",       "編集を続ける",        "계속 편집",        "Continuer l'édition","Weiter bearbeiten","Seguir editando");
        Add("dlg.dontAsk",   "Don't ask again",      "下次不再詢問",   "今後は確認しない",     "다시 묻지 않음",    "Ne plus demander",  "Nicht mehr fragen", "No volver a preguntar");
        // Right-click that would drop the annotations to choose the area again
        Add("dlg.reselectTitle","Choose again",      "重新選取",       "範囲を選び直す",      "다시 선택",        "Choisir à nouveau", "Neu auswählen",     "Elegir de nuevo");
        Add("dlg.reselectMsg","Discard the annotations and choose the area again?", "要捨棄標註並重新選取截圖範圍嗎？", "注釈を破棄して範囲を選び直しますか？", "주석을 삭제하고 영역을 다시 선택할까요?", "Abandonner les annotations et choisir à nouveau la zone ?", "Anmerkungen verwerfen und den Bereich neu auswählen?", "¿Descartar las anotaciones y elegir el área de nuevo?");
        Add("dlg.reselectYes","Choose again",        "重新選取",       "選び直す",           "다시 선택",        "Choisir à nouveau", "Neu auswählen",     "Elegir de nuevo");
        Add("dlg.historyTitle","Snip history",       "截圖歷史",       "キャプチャ履歴",      "캡처 기록",        "Historique des captures", "Aufnahmeverlauf", "Historial de capturas");
        Add("dlg.historyMsg","Discard the annotations and show another snip?", "要捨棄標註並切換到其他截圖嗎？", "注釈を破棄して別のキャプチャを表示しますか？", "주석을 삭제하고 다른 캡처를 표시할까요?", "Abandonner les annotations et afficher une autre capture ?", "Anmerkungen verwerfen und eine andere Aufnahme anzeigen?", "¿Descartar las anotaciones y mostrar otra captura?");
        Add("dlg.historyYes","Discard",              "捨棄標註",       "破棄",              "삭제",            "Abandonner",        "Verwerfen",         "Descartar");
        Add("set.confirmDiscard","Ask before discarding an annotated capture", "取消含標註的截圖前先詢問", "注釈付きキャプチャの破棄前に確認", "주석이 있는 캡처 취소 전 확인", "Confirmer avant d'abandonner une capture annotée", "Vor dem Verwerfen einer beschrifteten Aufnahme fragen", "Preguntar antes de descartar una captura anotada");

        // Color picker
        Add("cp.title",      "Choose color",         "選擇顏色",       "色の選択",           "색 선택",         "Choisir une couleur","Farbe wählen",     "Elegir color");
        Add("cp.opacity",    "Opacity",              "透明度",        "不透明度",           "투명도",          "Opacité",           "Deckkraft",         "Opacidad");
        Add("common.ok",     "OK",                   "確定",          "OK",                "확인",            "OK",                "OK",                "Aceptar");
        Add("common.cancel", "Cancel",               "取消",          "キャンセル",         "취소",            "Annuler",           "Abbrechen",         "Cancelar");
        Add("common.save",   "Save",                 "儲存",          "保存",              "저장",            "Enregistrer",       "Speichern",         "Guardar");

        // Pin window (right-click / Menu key; each row also shows the keys that do the same)
        Add("pin.copy",      "Copy",                 "複製",          "コピー",             "복사",            "Copier",            "Kopieren",          "Copiar");
        Add("pin.copyFile",  "Copy as file",         "複製為檔案",     "ファイルとしてコピー", "파일로 복사",      "Copier en tant que fichier", "Als Datei kopieren", "Copiar como archivo");
        Add("pin.save",      "Save…",                "儲存…",         "保存…",             "저장…",           "Enregistrer…",      "Speichern…",        "Guardar…");
        Add("pin.quickSave", "Quick save",           "快速儲存",       "クイック保存",        "빠른 저장",        "Enregistrement rapide", "Schnellspeichern", "Guardado rápido");
        Add("pin.print",     "Print…",               "列印…",         "印刷…",             "인쇄…",           "Imprimer…",         "Drucken…",          "Imprimir…");
        Add("pin.rotateCw",  "Rotate clockwise",     "順時針旋轉",     "時計回りに回転",      "시계 방향으로 회전", "Rotation horaire",  "Im Uhrzeigersinn drehen", "Girar a la derecha");
        Add("pin.rotateCcw", "Rotate counterclockwise", "逆時針旋轉",  "反時計回りに回転",    "시계 반대 방향으로 회전", "Rotation antihoraire", "Gegen den Uhrzeigersinn drehen", "Girar a la izquierda");
        Add("pin.flipH",     "Flip horizontally",    "水平翻轉",       "左右反転",           "좌우 반전",        "Retourner horizontalement", "Horizontal spiegeln", "Voltear horizontalmente");
        Add("pin.flipV",     "Flip vertically",      "垂直翻轉",       "上下反転",           "상하 반전",        "Retourner verticalement", "Vertikal spiegeln", "Voltear verticalmente");
        Add("pin.grayscale", "Grayscale",            "灰階",          "グレースケール",      "회색조",          "Niveaux de gris",   "Graustufen",        "Escala de grises");
        Add("pin.invert",    "Invert colors",        "反轉色彩",       "色を反転",           "색상 반전",        "Inverser les couleurs", "Farben umkehren", "Invertir colores");
        Add("pin.opacityUp", "Increase opacity",     "提高不透明度",   "不透明度を上げる",    "불투명도 높이기",   "Augmenter l'opacité", "Deckkraft erhöhen", "Aumentar la opacidad");
        Add("pin.opacityDown","Decrease opacity",    "降低不透明度",   "不透明度を下げる",    "불투명도 낮추기",   "Diminuer l'opacité", "Deckkraft verringern", "Reducir la opacidad");
        Add("pin.reset",     "Reset size and opacity (100%)", "重設大小與不透明度 (100%)", "サイズと不透明度をリセット (100%)", "크기와 불투명도 초기화 (100%)", "Taille et opacité à 100 %", "Größe und Deckkraft zurücksetzen (100%)", "Restablecer tamaño y opacidad (100%)");
        Add("pin.close",     "Close",                "關閉",          "閉じる",             "닫기",            "Fermer",            "Schließen",         "Cerrar");
        Add("pin.destroy",   "Destroy",              "銷毀",          "破棄",              "폐기",            "Détruire",          "Vernichten",        "Destruir");
        Add("pin.dblClickClose","Close on double-click", "按兩下關閉", "ダブルクリックで閉じる", "더블 클릭으로 닫기", "Fermer par double-clic", "Per Doppelklick schließen", "Cerrar con doble clic");
        // The first double-click on a pin (asked once)
        Add("pin.dblClickTitle","Close pin",         "關閉貼圖",       "ピン留め画像を閉じる", "고정 이미지 닫기", "Fermer l'image épinglée", "Angeheftetes Bild schließen", "Cerrar imagen fijada");
        Add("pin.dblClickMsg","A double-click closes the pin. Close this one?\n\nA closed pin can be brought back with the paste hotkey. To keep pins open on a double-click, untick \"Close on double-click\" in the pin's right-click menu.", "按兩下貼圖會將它關閉。要關閉這張貼圖嗎？\n\n關閉的貼圖可用貼上熱鍵叫回。若不想按兩下就關閉，請在貼圖的右鍵選單取消勾選「按兩下關閉」。", "ダブルクリックするとピン留め画像を閉じます。この画像を閉じますか？\n\n閉じた画像は貼り付けのホットキーで呼び戻せます。ダブルクリックで閉じないようにするには、右クリックメニューの「ダブルクリックで閉じる」のチェックを外してください。", "더블 클릭하면 고정 이미지가 닫힙니다. 이 이미지를 닫을까요?\n\n닫은 이미지는 붙여넣기 단축키로 다시 불러올 수 있습니다. 더블 클릭으로 닫지 않으려면 오른쪽 클릭 메뉴에서 '더블 클릭으로 닫기'를 해제하세요.", "Un double-clic ferme l'image épinglée. La fermer ?\n\nUne image fermée peut être rappelée avec le raccourci de collage. Pour qu'un double-clic ne la ferme plus, décochez « Fermer par double-clic » dans le menu du clic droit.", "Ein Doppelklick schließt das angeheftete Bild. Jetzt schließen?\n\nEin geschlossenes Bild lässt sich mit dem Einfüge-Hotkey zurückholen. Damit ein Doppelklick es nicht mehr schließt, entfernen Sie im Rechtsklickmenü das Häkchen bei „Per Doppelklick schließen“.", "Un doble clic cierra la imagen fijada. ¿Cerrarla?\n\nUna imagen cerrada se puede recuperar con el atajo de pegar. Para que el doble clic no la cierre, desmarca «Cerrar con doble clic» en el menú del clic derecho.");
        Add("pin.saveFailed","Could not save the image: {0}", "無法儲存圖片：{0}", "画像を保存できませんでした：{0}", "이미지를 저장하지 못했습니다: {0}", "Impossible d'enregistrer l'image : {0}", "Das Bild konnte nicht gespeichert werden: {0}", "No se pudo guardar la imagen: {0}");
        Add("pin.paste",     "Paste from clipboard", "貼上剪貼簿內容", "クリップボードから貼り付け", "클립보드에서 붙여넣기", "Coller le presse-papiers", "Aus Zwischenablage einfügen", "Pegar del portapapeles");
        Add("pin.copyText",  "Copy text",            "複製文字",       "テキストをコピー",    "텍스트 복사",      "Copier le texte",   "Text kopieren",     "Copiar texto");
        Add("pin.thumbnail", "Thumbnail",            "縮圖",          "サムネイル",          "썸네일",          "Miniature",         "Miniaturansicht",   "Miniatura");

        // Settings window
        Add("set.title",     "ScreenPaste Settings", "ScreenPaste 設定","ScreenPaste 設定",  "ScreenPaste 설정","Paramètres ScreenPaste","ScreenPaste-Einstellungen","Ajustes de ScreenPaste");
        Add("set.hotkeys",   "Hotkeys",              "熱鍵",          "ホットキー",         "단축키",          "Raccourcis",        "Tastenkürzel",      "Atajos");
        Add("set.hotkeyHint","Click a field and press the desired key combo; Delete clears it.", "點選欄位後直接按下想要的按鍵組合；Delete 可清除。", "欄をクリックして希望のキーを押します。Delete で消去。", "칸을 클릭하고 원하는 키 조합을 누르세요. Delete로 지웁니다.", "Cliquez puis appuyez sur la combinaison ; Suppr pour effacer.", "Feld anklicken und Tastenkombination drücken; Entf löscht.", "Haz clic y pulsa la combinación; Supr para borrar.");
        Add("set.capture",   "Capture",              "截圖",          "キャプチャ",         "캡처",            "Capture",           "Aufnehmen",         "Capturar");
        Add("set.quickSave", "Quick save",           "快速儲存",       "クイック保存",        "빠른 저장",        "Enreg. rapide",     "Schnellspeichern",  "Guardado rápido");
        Add("set.print",     "Print",                "列印",          "印刷",              "인쇄",            "Imprimer",          "Drucken",           "Imprimir");
        // Global hotkeys beyond capture and record (Snipaste's defaults; the extra snip modes start unassigned)
        Add("set.snipCopy",  "Snip and copy",        "截圖並複製",     "キャプチャしてコピー", "캡처 후 복사",     "Capturer et copier", "Aufnehmen und kopieren", "Capturar y copiar");
        Add("set.customSnip","Custom snip",          "自訂截圖",       "カスタムキャプチャ",   "사용자 지정 캡처",  "Capture personnalisée", "Benutzerdefinierte Aufnahme", "Captura personalizada");
        Add("set.paste",     "Paste / bring back pin", "貼上／叫回貼圖", "貼り付け／画像を復元", "붙여넣기 / 이미지 복원", "Coller / rappeler l'image", "Einfügen / Bild zurückholen", "Pegar / recuperar imagen");
        Add("set.hidePins",  "Hide / show pins",     "隱藏／顯示貼圖", "ピン留め画像を隠す／表示", "고정 이미지 숨기기/표시", "Masquer / afficher les images", "Bilder aus-/einblenden", "Ocultar / mostrar imágenes");
        Add("set.clickThrough","Pin click-through",  "貼圖滑鼠穿透",   "ピン留め画像のクリック透過", "고정 이미지 클릭 통과", "Image épinglée traversable", "Bild klickdurchlässig", "Imagen fijada traspasable");
        Add("set.moreHotkeys","More hotkeys",        "更多熱鍵",       "その他のホットキー",   "추가 단축키",      "Plus de raccourcis", "Weitere Tastenkürzel", "Más atajos");
        Add("set.snipPin",   "Snip and pin",         "截圖並釘選",     "キャプチャして固定",   "캡처 후 고정",     "Capturer et épingler", "Aufnehmen und anheften", "Capturar y fijar");
        Add("set.snipCopyPin","Snip, copy and pin",  "截圖、複製並釘選", "キャプチャしてコピー・固定", "캡처 후 복사·고정", "Capturer, copier et épingler", "Aufnehmen, kopieren und anheften", "Capturar, copiar y fijar");
        Add("set.lastAreaCopy","Copy the last area again", "再次複製上次的範圍", "前回の範囲をもう一度コピー", "지난 영역 다시 복사", "Recopier la dernière zone", "Letzten Bereich erneut kopieren", "Copiar de nuevo la última área");
        Add("set.fullScreenCopy","Copy the whole screen", "複製整個螢幕", "画面全体をコピー",   "전체 화면 복사",   "Copier tout l'écran", "Ganzen Bildschirm kopieren", "Copiar toda la pantalla");
        Add("set.activeWindow","Snip the active window", "截取使用中的視窗", "アクティブウィンドウをキャプチャ", "활성 창 캡처", "Capturer la fenêtre active", "Aktives Fenster aufnehmen", "Capturar la ventana activa");
        Add("set.activeScreen","Snip the screen under the cursor", "截取滑鼠所在的螢幕", "カーソルのある画面をキャプチャ", "커서가 있는 화면 캡처", "Capturer l'écran sous le curseur", "Bildschirm unter dem Cursor aufnehmen", "Capturar la pantalla bajo el cursor");
        Add("set.delayedSnip","Delayed snip",        "延遲截圖",       "遅延キャプチャ",      "지연 캡처",        "Capture différée",  "Verzögerte Aufnahme", "Captura retardada");
        Add("set.clearSnipHistory","Clear snip history", "清除截圖歷史", "キャプチャ履歴を消去", "캡처 기록 지우기", "Effacer l'historique des captures", "Aufnahmeverlauf löschen", "Borrar historial de capturas");
        Add("set.snipDelay", "Delay (seconds)",      "延遲（秒）",     "遅延（秒）",         "지연(초)",         "Délai (secondes)",  "Verzögerung (Sekunden)", "Retardo (segundos)");
        // The configurable in-app gestures, and gestures set twice
        Add("set.inAppShortcuts","In-app shortcuts", "截圖與貼圖內的快捷鍵", "アプリ内ショートカット", "앱 내 단축키", "Raccourcis dans l'application", "Kürzel in der App", "Atajos en la aplicación");
        Add("set.inAppHint", "Used while snipping and on pins. Snipaste's other keys (R, F5, Ctrl+A …) are fixed.", "截圖與貼圖時使用。Snipaste 的其他按鍵（R、F5、Ctrl+A…）固定不變。", "キャプチャ中とピン留め画像で使います。Snipaste のその他のキー（R、F5、Ctrl+A…）は固定です。", "캡처 중과 고정 이미지에서 사용합니다. Snipaste의 다른 키(R, F5, Ctrl+A…)는 고정입니다.", "Utilisés pendant la capture et sur les images épinglées. Les autres touches de Snipaste (R, F5, Ctrl+A…) sont fixes.", "Gelten beim Aufnehmen und für angeheftete Bilder. Die übrigen Snipaste-Tasten (R, F5, Strg+A …) sind fest.", "Se usan al capturar y en las imágenes fijadas. Las demás teclas de Snipaste (R, F5, Ctrl+A…) son fijas.");
        Add("conflict.global","{0} is set for {1}; only one of them can work.", "{0} 同時設定給 {1}，只有一個會生效。", "{0} が {1} に重複しています。有効になるのは一つだけです。", "{0}이(가) {1}에 중복 지정되어 하나만 작동합니다.", "{0} est attribué à {1} ; un seul fonctionnera.", "{0} ist {1} zugewiesen; nur eines davon funktioniert.", "{0} está asignado a {1}; solo uno funcionará.");
        Add("conflict.globalInApp","{0} ({1}) is a global hotkey, so snips and pins no longer get this key.", "{0}（{1}）是全域熱鍵，截圖與貼圖中將無法使用此按鍵。", "{0}（{1}）はグローバルホットキーのため、キャプチャ中とピン留め画像ではこのキーが使えなくなります。", "{0}({1})은(는) 전역 단축키이므로 캡처와 고정 이미지에서 이 키를 쓸 수 없습니다.", "{0} ({1}) est un raccourci global : la capture et les images épinglées ne reçoivent plus cette touche.", "{0} ({1}) ist ein globales Tastenkürzel; Aufnahme und angeheftete Bilder erhalten diese Taste nicht mehr.", "{0} ({1}) es un atajo global: la captura y las imágenes fijadas ya no reciben esta tecla.");
        Add("conflict.inApp","{0} is set for {1}; only the first one runs.", "{0} 同時設定給 {1}，只會執行第一個。", "{0} が {1} に重複しています。最初のものだけが実行されます。", "{0}이(가) {1}에 중복 지정되어 첫 번째만 실행됩니다.", "{0} est attribué à {1} ; seul le premier s'exécute.", "{0} ist {1} zugewiesen; nur das erste wird ausgeführt.", "{0} está asignado a {1}; solo se ejecuta el primero.");
        Add("conflict.builtIn","{0} ({1}) replaces a built-in key of snips or pins.", "{0}（{1}）會取代截圖或貼圖的內建按鍵。", "{0}（{1}）はキャプチャまたはピン留め画像の既定のキーを置き換えます。", "{0}({1})이(가) 캡처나 고정 이미지의 기본 키를 대신합니다.", "{0} ({1}) remplace une touche intégrée de la capture ou des images épinglées.", "{0} ({1}) ersetzt eine eingebaute Taste der Aufnahme oder der angehefteten Bilder.", "{0} ({1}) sustituye una tecla integrada de la captura o de las imágenes fijadas.");
        // What Enter / a double-click / a middle-click inside the chosen area does
        Add("set.outputActions","Enter & mouse actions", "Enter 與滑鼠動作", "Enter とマウスの動作", "Enter 및 마우스 동작", "Entrée et souris", "Eingabetaste & Maus", "Intro y ratón");
        Add("set.outputActionsHint","What each does inside the chosen area while annotating; a double-click counts only with no tool selected.", "標註時在已選取範圍內的動作；按兩下僅在未選擇工具時有效。", "注釈中に選択範囲内で行う動作です。ダブルクリックはツール未選択時のみ有効です。", "주석 중 선택 영역 안에서의 동작입니다. 더블 클릭은 도구를 선택하지 않았을 때만 적용됩니다.", "Action dans la zone choisie pendant l'annotation ; le double-clic ne compte que sans outil sélectionné.", "Aktion im gewählten Bereich beim Beschriften; der Doppelklick gilt nur ohne gewähltes Werkzeug.", "Acción dentro del área elegida al anotar; el doble clic solo cuenta sin herramienta seleccionada.");
        Add("set.enterAction","Enter key",           "Enter 鍵",      "Enter キー",         "Enter 키",        "Touche Entrée",     "Eingabetaste",      "Tecla Intro");
        Add("set.dblClickAction","Double-click",     "按兩下",        "ダブルクリック",      "더블 클릭",        "Double-clic",       "Doppelklick",       "Doble clic");
        Add("set.middleClickAction","Middle-click",  "滑鼠中鍵",       "中クリック",         "가운데 클릭",      "Clic milieu",       "Mittelklick",       "Clic central");
        Add("out.none",      "Do nothing",           "不執行動作",     "何もしない",         "동작 없음",        "Ne rien faire",     "Nichts tun",        "No hacer nada");
        Add("out.copy",      "Copy to clipboard",    "複製到剪貼簿",   "クリップボードにコピー", "클립보드에 복사", "Copier dans le presse-papiers", "In die Zwischenablage kopieren", "Copiar al portapapeles");
        Add("out.save",      "Save as…",             "另存新檔…",      "名前を付けて保存…",   "다른 이름으로 저장…", "Enregistrer sous…", "Speichern unter…",  "Guardar como…");
        Add("out.copyAndPin","Copy and pin",         "複製並釘選",     "コピーして固定",      "복사 후 고정",     "Copier et épingler","Kopieren und anheften","Copiar y fijar");
        Add("set.appearance","Appearance & startup", "外觀與啟動",     "外観と起動",         "모양 및 시작",     "Apparence & démarrage","Aussehen & Start", "Apariencia e inicio");
        Add("set.language",  "Language",             "語言",          "言語",              "언어",            "Langue",            "Sprache",           "Idioma");
        Add("set.theme",     "Theme",                "主題",          "テーマ",             "테마",            "Thème",             "Design",            "Tema");
        Add("set.annotation","Annotation defaults",  "標註預設值",      "注釈の既定値",       "주석 기본값",   "Valeurs par défaut des annotations", "Anmerkungs-Standards", "Valores predeterminados de anotación");
        Add("set.blurKind",  "Blur type",            "模糊類型",       "ぼかしの種類",       "흐림 종류",       "Type de flou",      "Weichzeichner-Typ", "Tipo de desenfoque");
        Add("set.blurKindHint","Switching it on the capture toolbar updates this to your last choice", "在截圖工具列切換後，會記住最後一次的選擇", "截圖ツールバーで切り替えると、最後の選択がここに記録されます", "캐처 도구 모음에서 바꾸면 마지막 선택이 여기에 기록됩니다", "Le changer dans la barre de capture enregistre ici votre dernier choix", "Ändern in der Aufnahme-Leiste speichert hier die letzte Wahl", "Cambiarlo en la barra de captura guarda aquí tu última elección");
        // The pin options (Settings > Pins)
        Add("set.pins",      "Pins",                 "貼圖",          "ピン留め画像",        "고정 이미지",      "Images épinglées",  "Angeheftete Bilder", "Imágenes fijadas");
        Add("set.pinZoomAtCursor","Mouse wheel zooms around the cursor (off: from the top-left corner)", "滾輪縮放以滑鼠游標為中心（關閉則固定左上角）", "ホイールのズームをカーソル位置を中心にする（オフ：左上を固定）", "휠 확대/축소를 커서 위치 기준으로 (끄면 왼쪽 위 고정)", "La molette zoome autour du curseur (sinon depuis le coin supérieur gauche)", "Mausrad zoomt um den Mauszeiger (aus: von der linken oberen Ecke)", "La rueda amplía alrededor del cursor (si no, desde la esquina superior izquierda)");
        Add("set.pinDblClick","Double-click on a pin", "按兩下貼圖",   "ピン留め画像をダブルクリック", "고정 이미지 더블 클릭", "Double-clic sur une image", "Doppelklick auf ein Bild", "Doble clic en una imagen");
        Add("set.pinDblClickHint","Shift+double-click always toggles the thumbnail. A closed pin comes back with the paste hotkey.", "Shift+按兩下一律切換縮圖。關閉的貼圖可用貼上熱鍵叫回。", "Shift+ダブルクリックは常にサムネイルを切り替えます。閉じた画像は貼り付けのホットキーで呼び戻せます。", "Shift+더블 클릭은 항상 썸네일을 전환합니다. 닫은 이미지는 붙여넣기 단축키로 다시 불러올 수 있습니다.", "Maj+double-clic bascule toujours la miniature. Une image fermée se rappelle avec le raccourci de collage.", "Umschalt+Doppelklick schaltet immer die Miniaturansicht um. Ein geschlossenes Bild lässt sich mit dem Einfüge-Hotkey zurückholen.", "Mayús+doble clic siempre alterna la miniatura. Una imagen cerrada se recupera con el atajo de pegar.");
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
