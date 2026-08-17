using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Cube.App
{
    public readonly struct AppLocale
    {
        public readonly string Code;
        public readonly string NativeName;

        public AppLocale(string code, string nativeName)
        {
            Code = code;
            NativeName = nativeName;
        }
    }

    /// <summary>
    /// Small runtime localization layer. Korean source copy is used as the stable key so
    /// screens can stay readable, while English is the guaranteed fallback for every locale.
    /// </summary>
    public static class LocalizationService
    {
        const string SystemCode = "";
        static readonly AppLocale[] Locales =
        {
            new AppLocale("ko", "한국어"),
            new AppLocale("en", "English"),
            new AppLocale("ja", "日本語"),
            new AppLocale("zh-CN", "简体中文"),
            new AppLocale("zh-TW", "繁體中文（台灣）"),
            new AppLocale("zh-HK", "繁體中文（香港）"),
            new AppLocale("es", "Español"),
            new AppLocale("fr", "Français"),
            new AppLocale("de", "Deutsch"),
            new AppLocale("pt", "Português"),
            new AppLocale("ru", "Русский"),
            new AppLocale("vi", "Tiếng Việt"),
            new AppLocale("id", "Bahasa Indonesia"),
            new AppLocale("th", "ไทย"),
        };

        static Dictionary<string, Dictionary<string, string>> _tables;
        static Dictionary<string, string> _englishFallback;
        static Dictionary<string, Dictionary<string, string>> Tables
        {
            get
            {
                if (_tables == null) _tables = BuildTables();
                return _tables;
            }
        }
        static Dictionary<string, string> EnglishFallback
        {
            get
            {
                if (_englishFallback != null) return _englishFallback;
                _englishFallback = new Dictionary<string, string>(StringComparer.Ordinal);
                string[] rows = EnglishFallbackCatalog.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < rows.Length; i++)
                {
                    string[] cells = rows[i].TrimEnd('\r').Split('\t');
                    if (cells.Length == 2) _englishFallback[Unescape(cells[0])] = Unescape(cells[1]);
                }
                return _englishFallback;
            }
        }
        static string _systemLocale = "en";
        static bool _initialized;

        public static event Action Changed;
        public static IReadOnlyList<AppLocale> Supported => Locales;
        public static string OverrideCode => AppSettings.LanguageCode;
        public static bool UsesSystemLanguage => string.IsNullOrEmpty(OverrideCode);
        public static string CurrentCode => UsesSystemLanguage ? _systemLocale : Normalize(OverrideCode);
        public static string CurrentName => NameOf(CurrentCode);
        public static string SystemLanguageName => NameOf(_systemLocale);

        public static void Init()
        {
            _systemLocale = DetectSystemLocale();
            _initialized = true;
        }

        public static void SetLanguage(string code)
        {
            if (!_initialized) Init();
            string normalized = string.IsNullOrWhiteSpace(code) ? SystemCode : Normalize(code);
            if (AppSettings.LanguageCode == normalized) return;
            AppSettings.LanguageCode = normalized;
            Changed?.Invoke();
        }

        public static string NameOf(string code)
        {
            string normalized = Normalize(code);
            for (int i = 0; i < Locales.Length; i++)
                if (Locales[i].Code == normalized) return Locales[i].NativeName;
            return "English";
        }

        /// 번역 누락 테스트가 카탈로그 전체를 훑어보기 위한 통로.
        public static ICollection<string> CatalogKeysForTests => Tables["en"].Keys;

        /// 영어 폴백 표의 키. 여기에만 있고 카탈로그에 없으면 한국어·영어가 아닌
        /// 언어에서 영어가 그대로 나온다. 테스트가 그 상태를 잡는다.
        public static ICollection<string> EnglishFallbackKeysForTests => EnglishFallback.Keys;

        public static string T(string source)
        {
            if (string.IsNullOrEmpty(source) || CurrentCode == "ko") return source;
            if (TryLookup(CurrentCode, source, out var translated)) return translated;
            if (TryLookup("en", source, out translated)) return translated;
            if (EnglishFallback.TryGetValue(source, out translated)) return translated;
            return TranslatePatterns(source, CurrentCode);
        }

        public static string F(string source, params object[] args)
            => string.Format(CultureInfo.CurrentCulture, T(source), args);

        public static void RefreshAll(Transform root)
        {
            if (root == null) return;
            var labels = root.GetComponentsInChildren<LocalizedText>(true);
            for (int i = 0; i < labels.Length; i++) labels[i].Refresh();
        }

        static bool TryLookup(string code, string source, out string value)
        {
            value = null;
            return Tables.TryGetValue(code, out var table)
                && table.TryGetValue(source, out value)
                && !string.IsNullOrEmpty(value);
        }

        static string TranslatePatterns(string source, string code)
        {
            if (!ContainsKorean(source)) return source;
            Match m;
            if ((m = Regex.Match(source, @"^(\d+)/7 단계 완료$")).Success)
                return F("{0}/7 단계 완료", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^(\d+) / (\d+) 완료$")).Success)
                return F("{0} / {1} 완료", m.Groups[1].Value, m.Groups[2].Value);
            if ((m = Regex.Match(source, @"^(\d+)단계를 먼저 마쳐 주세요\.$")).Success)
                return F("{0}단계를 먼저 마쳐 주세요.", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^(\d+)단계 · (.+)$")).Success)
                return F("{0}단계 · {1}", m.Groups[1].Value, T(m.Groups[2].Value));
            if ((m = Regex.Match(source, @"^(\d+)×(\d+) 연습$")).Success)
                return F("{0}×{1} 연습", m.Groups[1].Value, m.Groups[2].Value);
            if ((m = Regex.Match(source, @"^(\d+)개$")).Success)
                return F("{0}개", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^(\d+)수$")).Success)
                return F("{0}수", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^(\d+)×(\d+) · 기록 (\d+)개$")).Success)
                return F("{0}×{1} · 기록 {2}개", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
            if ((m = Regex.Match(source, @"^최근 (\d+)개$")).Success)
                return F("최근 {0}개", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^(\d+) / 6면$")).Success)
                return F("{0} / 6면", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^(\d+)개 면을 저장했습니다$")).Success)
                return F("{0}개 면을 저장했습니다", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^54칸을 읽었습니다 · 신뢰도 (\d+)%$")).Success)
                return F("54칸을 읽었습니다 · 신뢰도 {0}%", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^색상 안정화 중\s+(\d+) / (\d+)$")).Success)
                return F("색상 안정화 중 {0} / {1}", m.Groups[1].Value, m.Groups[2].Value);
            if ((m = Regex.Match(source, @"^(\d+)/6\s+(.+)면 촬영 · 가운데 (.+)$")).Success)
                return F("{0}/6 {1}면 촬영 · 가운데 {2}", m.Groups[1].Value,
                    T(m.Groups[2].Value), T(m.Groups[3].Value));
            if ((m = Regex.Match(source, @"^통과했습니다\. (\d+)단계가 열렸습니다\.$")).Success)
                return F("통과했습니다. {0}단계가 열렸습니다.", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^(.+) — 공식을 쓴 뒤 다시 살펴보세요\.$")).Success)
                return F("{0} — 공식을 쓴 뒤 다시 살펴보세요.", T(m.Groups[1].Value));
            if ((m = Regex.Match(source, @"^(.+) — 위층만 돌리면 맞습니다\.$")).Success)
                return F("{0} — 위층만 돌리면 맞습니다.", T(m.Groups[1].Value));
            if ((m = Regex.Match(source, @"^(.+) — 자세를 맞춘 뒤 공식을 한 번 씁니다\.$")).Success)
                return F("{0} — 자세를 맞춘 뒤 공식을 한 번 씁니다.", T(m.Groups[1].Value));
            if ((m = Regex.Match(source, @"^(.+) — 공식을 (\d+)번 써야 하는 경우입니다\.$")).Success)
                return F("{0} — 공식을 {1}번 써야 하는 경우입니다.", T(m.Groups[1].Value), m.Groups[2].Value);
            if ((m = Regex.Match(source, @"^(.+) — 조각 하나를 더 맞춥니다\. \((\d+)/4\)$")).Success)
                return F("{0} — 조각 하나를 더 맞춥니다. ({1}/4)", T(m.Groups[1].Value), m.Groups[2].Value);

            // CubeValidator가 돌려주는 실패 사유. Cube.Core는 UnityEngine을 참조하지
            // 않기로 못 박아 둔 어셈블리라 그쪽에서 번역할 수 없다. 이미 한국어로
            // 조립돼 넘어오므로 여기서 되돌려 읽는다.
            if ((m = Regex.Match(source, @"^색 번호 (\d+)는 없는 색이다$")).Success)
                return F("색 번호 {0}는 없는 색이다", m.Groups[1].Value);
            if ((m = Regex.Match(source, @"^(.+) 칸이 (\d+)개다\. 각 색은 9개여야 한다$")).Success)
                return F("{0} 칸이 {1}개다. 각 색은 9개여야 한다", T(m.Groups[1].Value), m.Groups[2].Value);
            if ((m = Regex.Match(source, @"^같은 모서리 조각이 두 번 나온다 \((.+)\)$")).Success)
                return F("같은 모서리 조각이 두 번 나온다 ({0})", TranslateColorList(m.Groups[1].Value));
            if ((m = Regex.Match(source, @"^실제 큐브에 없는 모서리다 \((.+)\)$")).Success)
                return F("실제 큐브에 없는 모서리다 ({0})", TranslateColorList(m.Groups[1].Value));
            if ((m = Regex.Match(source, @"^같은 엣지 조각이 두 번 나온다 \((.+)\)$")).Success)
                return F("같은 엣지 조각이 두 번 나온다 ({0})", TranslateColorList(m.Groups[1].Value));
            if ((m = Regex.Match(source, @"^실제 큐브에 없는 엣지다 \((.+)\)$")).Success)
                return F("실제 큐브에 없는 엣지다 ({0})", TranslateColorList(m.Groups[1].Value));

            return EnglishFallback.TryGetValue(source, out var fallback) ? fallback : source;
        }

        /// "노란색·흰색·초록색"처럼 가운뎃점으로 이어 붙인 색 이름을 하나씩 번역한다.
        static string TranslateColorList(string joined)
        {
            string[] parts = joined.Split('·');
            for (int i = 0; i < parts.Length; i++) parts[i] = T(parts[i].Trim());
            return string.Join("·", parts);
        }

        static bool ContainsKorean(string value)
        {
            for (int i = 0; i < value.Length; i++)
                if (value[i] >= '\uac00' && value[i] <= '\ud7a3') return true;
            return false;
        }

        static string DetectSystemLocale()
        {
            string tag = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var locale = new AndroidJavaClass("java.util.Locale"))
                using (var current = locale.CallStatic<AndroidJavaObject>("getDefault"))
                    tag = current.Call<string>("toLanguageTag");
            }
            catch (Exception) { }
#endif
            if (string.IsNullOrWhiteSpace(tag)) tag = CultureInfo.CurrentUICulture.Name;
            if (string.IsNullOrWhiteSpace(tag)) tag = Application.systemLanguage.ToString();
            return Normalize(tag);
        }

        internal static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "en";
            string tag = code.Replace('_', '-').ToLowerInvariant();
            if (tag.StartsWith("zh-hk") || tag.StartsWith("zh-hant-hk")) return "zh-HK";
            if (tag.StartsWith("zh-tw") || tag.StartsWith("zh-hant")) return "zh-TW";
            if (tag.StartsWith("zh") || tag.Contains("chinesesimplified")) return "zh-CN";
            if (tag.Contains("chinesetraditional")) return "zh-TW";
            for (int i = 0; i < Locales.Length; i++)
            {
                string candidate = Locales[i].Code;
                if (tag == candidate.ToLowerInvariant() || tag.StartsWith(candidate.ToLowerInvariant() + "-"))
                    return candidate;
            }
            return "en";
        }

        static Dictionary<string, Dictionary<string, string>> BuildTables()
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            for (int i = 1; i < Header.Length; i++)
                result[Header[i]] = new Dictionary<string, string>(StringComparer.Ordinal);

            string[] rows = Catalog.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int row = 0; row < rows.Length; row++)
            {
                string[] cells = rows[row].TrimEnd('\r').Split('\t');
                if (cells.Length != Header.Length) continue;
                string key = Unescape(cells[0]);
                for (int col = 1; col < Header.Length; col++)
                    result[Header[col]][key] = Unescape(cells[col]);
            }
            return result;
        }

        static string Unescape(string value) => value.Replace("\\n", "\n");

        static readonly string[] Header =
        {
            "ko", "en", "ja", "zh-CN", "zh-TW", "zh-HK", "es", "fr", "de", "pt", "ru", "vi", "id", "th"
        };

        // Keep each row at exactly fourteen translations. Tabs are deliberate separators.
        const string Catalog = @"언어	Language	言語	语言	語言	語言	Idioma	Langue	Sprache	Idioma	Язык	Ngôn ngữ	Bahasa	ภาษา
시스템 기본값	System default	システム設定	跟随系统	跟隨系統	跟隨系統	Predeterminado del sistema	Langue du système	Systemstandard	Padrão do sistema	Как в системе	Theo hệ thống	Bawaan sistem	ตามระบบ
설정	Settings	設定	设置	設定	設定	Ajustes	Paramètres	Einstellungen	Configurações	Настройки	Cài đặt	Pengaturan	การตั้งค่า
화면	Display	表示	显示	顯示	顯示	Pantalla	Affichage	Anzeige	Tela	Экран	Hiển thị	Tampilan	หน้าจอ
테마	Theme	テーマ	主题	主題	主題	Tema	Thème	Design	Tema	Тема	Giao diện	Tema	ธีม
다크	Dark	ダーク	深色	深色	深色	Oscuro	Sombre	Dunkel	Escuro	Тёмная	Tối	Gelap	มืด
라이트	Light	ライト	浅色	淺色	淺色	Claro	Clair	Hell	Claro	Светлая	Sáng	Terang	สว่าง
다크와 라이트를 전환해요	Switch between dark and light	ダークとライトを切替	切换深色和浅色	切換深色與淺色	切換深色與淺色	Cambia entre oscuro y claro	Basculer clair/sombre	Hell und dunkel wechseln	Alternar claro e escuro	Смена светлой и тёмной темы	Đổi giao diện sáng/tối	Ganti tema terang/gelap	สลับธีมมืดและสว่าง
큐브 스킨	Cube skins	キューブスキン	魔方皮肤	魔方外觀	魔方外觀	Diseños del cubo	Skins du cube	Würfel-Skins	Skins do cubo	Скины куба	Giao diện khối	Skin kubus	สกินลูกบาศก์
색상, 질감, 캐릭터를 골라요	Choose colors, textures and characters	色・質感・キャラを選択	选择颜色、纹理和角色	選擇顏色、材質和角色	選擇顏色、材質和角色	Elige colores, texturas y personajes	Choisir couleurs, textures et personnages	Farben, Texturen und Figuren wählen	Escolha cores, texturas e personagens	Выберите цвета, текстуры и персонажей	Chọn màu, chất liệu và nhân vật	Pilih warna, tekstur, dan karakter	เลือกสี พื้นผิว และตัวละคร
소리	Sound	サウンド	声音	聲音	聲音	Sonido	Son	Ton	Som	Звук	Âm thanh	Suara	เสียง
배경음	Background music	BGM	背景音乐	背景音樂	背景音樂	Música de fondo	Musique de fond	Hintergrundmusik	Música de fundo	Фоновая музыка	Nhạc nền	Musik latar	เพลงพื้นหลัง
집중을 돕는 잔잔한 음악	Calm music to help you focus	集中できる穏やかな音楽	帮助专注的舒缓音乐	幫助專注的舒緩音樂	幫助專注的舒緩音樂	Música suave para concentrarte	Musique douce pour se concentrer	Ruhige Musik zum Konzentrieren	Música calma para se concentrar	Спокойная музыка для концентрации	Nhạc nhẹ giúp tập trung	Musik tenang untuk fokus	เพลงเบาๆ ช่วยให้มีสมาธิ
큐브 효과음	Cube sound	キューブ効果音	魔方音效	魔方音效	魔方音效	Sonido del cubo	Son du cube	Würfelklang	Som do cubo	Звук кубика	Âm thanh khối	Suara kubus	เสียงลูกบาศก์
기본·말랑 팝·실제 소리를 골라요	Choose classic, cute pop or realistic	標準・ポップ・リアルから選択	选择经典、可爱或真实音效	選擇經典、可愛或真實音效	選擇經典、可愛或真實音效	Clásico, pop tierno o realista	Classique, pop mignon ou réaliste	Klassisch, Pop oder realistisch	Clássico, pop fofo ou realista	Классический, милый или реалистичный	Chọn cơ bản, dễ thương hoặc chân thực	Pilih klasik, pop lucu, atau realistis	เลือกแบบคลาสสิก ป๊อปน่ารัก หรือสมจริง
연습	Practice	練習	练习	練習	練習	Práctica	Entraînement	Übung	Prática	Практика	Luyện tập	Latihan	ฝึก
15초 인스펙션	15-second inspection	15秒インスペクション	15秒观察	15秒觀察	15秒觀察	Inspección de 15 segundos	Inspection de 15 secondes	15-Sekunden-Inspektion	Inspeção de 15 segundos	15 секунд на осмотр	Quan sát 15 giây	Inspeksi 15 detik	ตรวจสอบ 15 วินาที
섞은 뒤 미리 살펴볼 시간을 줘요	Preview time after scrambling	スクランブル後に確認時間を設定	打乱后提供观察时间	打亂後提供觀察時間	打亂後提供觀察時間	Tiempo para observar tras mezclar	Temps d'observation après mélange	Bedenkzeit nach dem Mischen	Tempo para observar após misturar	Время осмотра после перемешивания	Thời gian quan sát sau khi trộn	Waktu melihat setelah diacak	มีเวลาดูก่อนเริ่มหลังสับ
노테이션 버튼	Move buttons	手順ボタン	转动按钮	轉動按鈕	轉動按鈕	Botones de movimientos	Boutons de mouvements	Zugschaltflächen	Botões de movimentos	Кнопки ходов	Nút thao tác	Tombol gerakan	ปุ่มหมุน
화면 버튼으로도 회전할 수 있어요	Turn the cube with on-screen buttons	画面ボタンでも回せます	也可用屏幕按钮转动	也可用螢幕按鈕轉動	也可用螢幕按鈕轉動	Gira con los botones en pantalla	Tourner avec les boutons à l'écran	Mit Bildschirmtasten drehen	Gire com os botões na tela	Поворот кнопками на экране	Xoay bằng nút trên màn hình	Putar dengan tombol layar	หมุนด้วยปุ่มบนหน้าจอ
회전 속도	Turn speed	回転速度	转动速度	轉動速度	轉動速度	Velocidad de giro	Vitesse de rotation	Drehgeschwindigkeit	Velocidade de giro	Скорость поворота	Tốc độ xoay	Kecepatan putar	ความเร็วการหมุน
큐브가 돌아가는 시간을 조절해요	Adjust how fast the cube turns	回転時間を調整	调整魔方转动速度	調整魔方轉動速度	調整魔方轉動速度	Ajusta la velocidad del cubo	Régler la vitesse du cube	Drehgeschwindigkeit einstellen	Ajuste a velocidade do cubo	Настройте скорость кубика	Điều chỉnh tốc độ xoay	Atur kecepatan kubus	ปรับความเร็วการหมุน
바꾼 설정은 자동으로 저장돼요	Changes are saved automatically	変更は自動保存されます	更改会自动保存	變更會自動儲存	變更會自動儲存	Los cambios se guardan automáticamente	Les changements sont enregistrés automatiquement	Änderungen werden automatisch gespeichert	As alterações são salvas automaticamente	Изменения сохраняются автоматически	Thay đổi được lưu tự động	Perubahan disimpan otomatis	บันทึกการเปลี่ยนแปลงอัตโนมัติ
켬	On	オン	开	開	開	Sí	Activé	An	Ligado	Вкл.	Bật	Aktif	เปิด
끔	Off	オフ	关	關	關	No	Désactivé	Aus	Desligado	Выкл.	Tắt	Mati	ปิด
기본	Classic	標準	经典	經典	經典	Clásico	Classique	Klassisch	Clássico	Обычный	Cơ bản	Klasik	คลาสสิก
말랑 팝	Cute pop	やわらかポップ	可爱弹音	可愛彈音	可愛彈音	Pop tierno	Pop mignon	Sanfter Pop	Pop fofo	Милый поп	Pop dễ thương	Pop lucu	ป๊อปน่ารัก
실제 큐브	Real cube	リアルキューブ	真实魔方	真實魔方	真實魔方	Cubo real	Cube réel	Echter Würfel	Cubo real	Настоящий кубик	Khối thật	Kubus nyata	ลูกบาศก์จริง
즉시	Instant	即時	立即	立即	立即	Instantáneo	Instantané	Sofort	Instantâneo	Мгновенно	Tức thì	Instan	ทันที
큐브 연습장	3D Cube	3Dキューブ	3D魔方	3D魔方	3D魔方	Cubo 3D	Cube 3D	3D-Würfel	Cubo 3D	3D-кубик	Khối 3D	Kubus 3D	ลูกบาศก์ 3D
연습 시작	Start practice	練習を始める	开始练习	開始練習	開始練習	Empezar práctica	Commencer	Übung starten	Iniciar prática	Начать практику	Bắt đầu luyện tập	Mulai latihan	เริ่มฝึก
이어하기	Continue	続きから	继续	繼續	繼續	Continuar	Continuer	Fortsetzen	Continuar	Продолжить	Tiếp tục	Lanjutkan	ทำต่อ
배우기	Learn	学ぶ	学习	學習	學習	Aprender	Apprendre	Lernen	Aprender	Обучение	Học	Belajar	เรียนรู้
실물 큐브	Real cube	実物キューブ	实体魔方	實體魔方	實體魔方	Cubo real	Cube réel	Echter Würfel	Cubo real	Настоящий кубик	Khối thật	Kubus nyata	ลูกบาศก์จริง
촬영해서 넣기	Scan with camera	カメラで読み取る	相机扫描	相機掃描	相機掃描	Escanear con cámara	Scanner avec la caméra	Mit Kamera scannen	Escanear com câmera	Сканировать камерой	Quét bằng camera	Pindai dengan kamera	สแกนด้วยกล้อง
기록	Records	記録	记录	記錄	記錄	Historial	Historique	Rekorde	Registros	Результаты	Thành tích	Catatan	สถิติ
연습 기록	Practice history	練習履歴	练习记录	練習記錄	練習記錄	Historial de práctica	Séances passées	Übungsverlauf	Histórico de prática	История практики	Lịch sử luyện tập	Riwayat latihan	ประวัติการฝึก
색상 · 캐릭터	Colors · Characters	色・キャラクター	颜色 · 角色	顏色 · 角色	顏色 · 角色	Colores · Personajes	Couleurs · Personnages	Farben · Figuren	Cores · Personagens	Цвета · Персонажи	Màu · Nhân vật	Warna · Karakter	สี · ตัวละคร
오늘도\n한 번 맞춰볼까요?	Ready to solve\na cube today?	今日も\n揃えてみよう！	今天也来\n还原一次吧！	今天也來\n還原一次吧！	今天也來\n還原一次吧！	¿Resolvemos\nun cubo hoy?	On résout\nun cube ?	Heute einen\nWürfel lösen?	Vamos resolver\num cubo hoje?	Соберём кубик\nсегодня?	Hôm nay cùng\ngiải khối nhé?	Siap menyusun\nkubus hari ini?	วันนี้มา\nแก้ลูกบาศก์กันไหม?
차근차근 연습하면\n누구나 완성할 수 있어요	Practice step by step.\nAnyone can solve it.	少しずつ練習すれば\n誰でも揃えられます	一步步练习，\n每个人都能完成	一步步練習，\n每個人都能完成	一步步練習，\n每個人都能完成	Practica paso a paso.\nTodos pueden lograrlo.	Avec de la pratique,\ntout le monde y arrive.	Schritt für Schritt üben.\nJeder kann es schaffen.	Pratique passo a passo.\nTodos conseguem.	Тренируйтесь постепенно.\nПолучится у каждого.	Luyện từng bước.\nAi cũng làm được.	Latihan bertahap.\nSemua bisa menyelesaikan.	ฝึกทีละขั้น\nทุกคนทำได้
7단계 코스	7-stage course	7ステップコース	7步课程	7步課程	7步課程	Curso de 7 etapas	Cours en 7 étapes	7-Schritte-Kurs	Curso de 7 etapas	Курс из 7 этапов	Khóa 7 bước	Kursus 7 tahap	คอร์ส 7 ขั้น
처음부터 차근차근 배워요	Learn step by step from the start	最初から順に学ぼう	从头逐步学习	從頭逐步學習	從頭逐步學習	Aprende paso a paso desde el inicio	Apprendre pas à pas depuis le début	Von Anfang an Schritt für Schritt	Aprenda passo a passo desde o início	Учитесь с самого начала	Học từng bước từ đầu	Belajar bertahap dari awal	เรียนทีละขั้นตั้งแต่ต้น
공식 모아보기	Algorithm library	手順一覧	公式合集	公式合集	公式合集	Biblioteca de algoritmos	Bibliothèque d'algorithmes	Algorithmus-Sammlung	Biblioteca de algoritmos	Библиотека алгоритмов	Thư viện công thức	Pustaka algoritma	คลังสูตร
3×3 입문 코스	3×3 beginner course	3×3入門コース	3×3入门课程	3×3入門課程	3×3入門課程	Curso 3×3 para principiantes	Cours débutant 3×3	3×3-Anfängerkurs	Curso iniciante 3×3	Курс 3×3 для начинающих	Khóa 3×3 nhập môn	Kursus pemula 3×3	คอร์ส 3×3 สำหรับผู้เริ่มต้น
완료	Complete	完了	完成	完成	完成	Completado	Terminé	Fertig	Concluído	Готово	Hoàn thành	Selesai	เสร็จ
시작	Start	開始	开始	開始	開始	Empezar	Commencer	Start	Iniciar	Начать	Bắt đầu	Mulai	เริ่ม
열림	Open	利用可能	已开放	已開放	已開放	Disponible	Disponible	Offen	Disponível	Открыто	Đã mở	Terbuka	เปิดแล้ว
잠김	Locked	ロック	未解锁	未解鎖	未解鎖	Bloqueado	Verrouillé	Gesperrt	Bloqueado	Закрыто	Đã khóa	Terkunci	ล็อก
단계 학습	Lesson	ステップ学習	阶段学习	階段學習	階段學習	Lección	Leçon	Lektion	Lição	Урок	Bài học	Pelajaran	บทเรียน
직접 돌려 보며 따라와요	Turn it yourself and follow along	自分で回して進めよう	亲手转动并跟着学习	親手轉動並跟著學習	親手轉動並跟著學習	Gíralo tú mismo y sigue los pasos	Tournez-le vous-même et suivez	Selbst drehen und folgen	Gire você mesmo e acompanhe	Поворачивайте сами и следуйте	Tự xoay và làm theo	Putar sendiri dan ikuti	หมุนเองแล้วทำตาม
코치의 설명	Coach's guide	コーチの説明	教练说明	教練說明	教練說明	Guía del entrenador	Conseil du coach	Anleitung	Guia do treinador	Подсказка тренера	Hướng dẫn	Panduan pelatih	คำแนะนำโค้ช
이전	Previous	前へ	上一步	上一步	上一步	Anterior	Précédent	Zurück	Anterior	Назад	Trước	Sebelumnya	ก่อนหน้า
다음	Next	次へ	下一步	下一步	下一步	Siguiente	Suivant	Weiter	Próximo	Далее	Tiếp	Berikutnya	ถัดไป
바로 써보는 공식	Algorithms to try	すぐ使える手順	立即可用的公式	立即可用的公式	立即可用的公式	Algoritmos para practicar	Algorithmes à essayer	Algorithmen zum Üben	Algoritmos para praticar	Алгоритмы для практики	Công thức thực hành	Algoritma untuk dicoba	สูตรสำหรับฝึก
연습하기	Practice	練習する	练习	練習	練習	Practicar	S'entraîner	Üben	Praticar	Практика	Luyện tập	Latihan	ฝึก
힌트	Hint	ヒント	提示	提示	提示	Pista	Indice	Hinweis	Dica	Подсказка	Gợi ý	Petunjuk	คำใบ้
처음으로	Reset	最初へ	重置	重設	重設	Reiniciar	Réinitialiser	Zurücksetzen	Reiniciar	Сначала	Đặt lại	Atur ulang	เริ่มใหม่
다음 단계로	Next stage	次のステップへ	下一阶段	下一階段	下一階段	Siguiente etapa	Étape suivante	Nächste Stufe	Próxima etapa	Следующий этап	Bước tiếp theo	Tahap berikutnya	ขั้นถัดไป
큐브 미리보기	Cube preview	キューブプレビュー	魔方预览	魔方預覽	魔方預覽	Vista previa del cubo	Aperçu du cube	Würfelvorschau	Prévia do cubo	Предпросмотр кубика	Xem trước khối	Pratinjau kubus	ตัวอย่างลูกบาศก์
카드를 누르면 이곳에서 천천히 보여줘요	Tap a card to watch it here	カードを押すとここで再生します	点击卡片在此演示	點按卡片在此示範	點按卡片在此示範	Toca una tarjeta para verlo aquí	Touchez une carte pour voir ici	Karte antippen, um sie hier zu sehen	Toque em um cartão para ver aqui	Нажмите карточку для показа	Chạm thẻ để xem tại đây	Ketuk kartu untuk melihat di sini	แตะการ์ดเพื่อดูที่นี่
배워 둔 공식	Learned algorithms	学んだ手順	已学公式	已學公式	已學公式	Algoritmos aprendidos	Algorithmes appris	Gelernte Algorithmen	Algoritmos aprendidos	Изученные алгоритмы	Công thức đã học	Algoritma yang dipelajari	สูตรที่เรียนแล้ว
전개도	Net view	展開図	展开图	展開圖	展開圖	Vista desplegada	Vue dépliée	Netzansicht	Vista aberta	Развёртка	Hình khai triển	Tampilan jaring	แผนภาพคลี่
직접 조작	Manual control	自分で操作	手动操作	手動操作	手動操作	Control manual	Contrôle manuel	Manuelle Steuerung	Controle manual	Ручное управление	Tự điều khiển	Kontrol manual	ควบคุมเอง
섞기	Scramble	スクランブル	打乱	打亂	打亂	Mezclar	Mélanger	Mischen	Embaralhar	Перемешать	Trộn	Acak	สับ
되돌리기	Undo	元に戻す	撤销	復原	復原	Deshacer	Annuler	Rückgängig	Desfazer	Отменить	Hoàn tác	Urungkan	ย้อนกลับ
초기화	Reset	リセット	重置	重設	重設	Reiniciar	Réinitialiser	Zurücksetzen	Reiniciar	Сброс	Đặt lại	Atur ulang	รีเซ็ต
확인	Confirm	確認	确认	確認	確認	Confirmar	Confirmer	Bestätigen	Confirmar	Подтвердить	Xác nhận	Konfirmasi	ยืนยัน
취소	Cancel	キャンセル	取消	取消	取消	Cancelar	Annuler	Abbrechen	Cancelar	Отмена	Hủy	Batal	ยกเลิก
계속	Continue	続ける	继续	繼續	繼續	Continuar	Continuer	Weiter	Continuar	Продолжить	Tiếp tục	Lanjutkan	ดำเนินการต่อ
최근 기록	Recent records	最近の記録	最近记录	最近記錄	最近記錄	Registros recientes	Résultats récents	Letzte Rekorde	Registros recentes	Последние результаты	Thành tích gần đây	Catatan terbaru	สถิติล่าสุด
이 세션 기록 지우기	Clear this session	この記録を削除	清除此组记录	清除此組記錄	清除此組記錄	Borrar esta sesión	Effacer cette session	Diese Sitzung löschen	Limpar esta sessão	Очистить сессию	Xóa phiên này	Hapus sesi ini	ล้างเซสชันนี้
아직 기록이 없어요	No records yet	まだ記録がありません	暂无记录	暫無記錄	暫無記錄	Aún no hay registros	Aucun résultat	Noch keine Rekorde	Ainda sem registros	Пока нет результатов	Chưa có thành tích	Belum ada catatan	ยังไม่มีสถิติ
큐브 자동 인식	Cube scanner	キューブスキャン	魔方扫描	魔方掃描	魔方掃描	Escáner de cubo	Scanner de cube	Würfelscanner	Scanner de cubo	Сканер кубика	Quét khối	Pemindai kubus	สแกนลูกบาศก์
카메라 준비 중…	Preparing camera…	カメラを準備中…	正在准备相机…	正在準備相機…	正在準備相機…	Preparando cámara…	Préparation de la caméra…	Kamera wird vorbereitet…	Preparando câmera…	Подготовка камеры…	Đang chuẩn bị camera…	Menyiapkan kamera…	กำลังเตรียมกล้อง…
촬영된 면	Captured faces	撮影した面	已拍摄面	已拍攝面	已拍攝面	Caras capturadas	Faces capturées	Aufgenommene Seiten	Faces capturadas	Снятые грани	Các mặt đã chụp	Sisi yang dipindai	ด้านที่ถ่ายแล้ว
촬영을 시작해 주세요	Start scanning	撮影を始めてください	开始拍摄	開始拍攝	開始拍攝	Empieza a escanear	Commencez le scan	Scan starten	Comece a escanear	Начните сканирование	Bắt đầu quét	Mulai memindai	เริ่มสแกน
다음 면 촬영	Scan next face	次の面を撮影	拍摄下一面	拍攝下一面	拍攝下一面	Escanear siguiente cara	Scanner la face suivante	Nächste Seite scannen	Escanear próxima face	Снять следующую грань	Quét mặt tiếp theo	Pindai sisi berikutnya	สแกนด้านถัดไป
색상 수정	Edit colors	色を修正	修改颜色	修改顏色	修改顏色	Editar colores	Corriger les couleurs	Farben bearbeiten	Editar cores	Изменить цвета	Sửa màu	Edit warna	แก้ไขสี
잘못 읽힌 칸만 고쳐 주세요	Correct only misread stickers	誤認識したマスだけ修正	只需修正识别错误的色块	只需修正辨識錯誤的色塊	只需修正辨識錯誤的色塊	Corrige solo las casillas incorrectas	Corrigez seulement les cases erronées	Nur falsch erkannte Felder korrigieren	Corrija apenas os adesivos errados	Исправьте только неверные клетки	Chỉ sửa ô nhận sai	Perbaiki hanya kotak yang salah	แก้เฉพาะช่องที่อ่านผิด
3D 큐브로 시작	Start in 3D	3Dキューブで開始	进入3D魔方	進入3D魔方	進入3D魔方	Empezar en 3D	Commencer en 3D	In 3D starten	Iniciar em 3D	Открыть в 3D	Bắt đầu ở chế độ 3D	Mulai dalam 3D	เริ่มแบบ 3D
촬영 화면으로	Back to camera	撮影画面へ	返回相机	返回相機	返回相機	Volver a la cámara	Retour à la caméra	Zurück zur Kamera	Voltar à câmera	Назад к камере	Về camera	Kembali ke kamera	กลับไปที่กล้อง
다시 촬영	Scan again	撮り直す	重新拍摄	重新拍攝	重新拍攝	Escanear de nuevo	Scanner à nouveau	Erneut scannen	Escanear novamente	Снять заново	Quét lại	Pindai ulang	สแกนใหม่
색상 고르기	Choose color	色を選ぶ	选择颜色	選擇顏色	選擇顏色	Elegir color	Choisir une couleur	Farbe wählen	Escolher cor	Выберите цвет	Chọn màu	Pilih warna	เลือกสี
고르는 즉시 큐브에 입혀져요	Applied to the cube instantly	選ぶとすぐ反映されます	选择后立即应用	選擇後立即套用	選擇後立即套用	Se aplica al instante	Appliqué instantanément	Wird sofort angewendet	Aplicado instantaneamente	Применяется сразу	Áp dụng ngay lập tức	Langsung diterapkan	ใช้กับลูกบาศก์ทันที
그림 배치	Image layout	画像レイアウト	图片布局	圖片配置	圖片配置	Diseño de imagen	Disposition de l'image	Bildlayout	Layout da imagem	Размещение рисунка	Bố cục hình	Tata letak gambar	การจัดวางภาพ
조각마다 반복	Repeat per sticker	各マスで繰り返す	每格重复	每格重複	每格重複	Repetir por casilla	Répéter par case	Pro Feld wiederholen	Repetir por adesivo	Повторять на каждой клетке	Lặp mỗi ô	Ulang per kotak	ทำซ้ำทุกช่อง
한 면 전체	Whole face	面全体	整面图片	整面圖片	整面圖片	Cara completa	Face entière	Ganze Seite	Face inteira	На всю грань	Toàn mặt	Satu sisi penuh	เต็มหนึ่งด้าน
그림 한 장을 3×3 조각으로 나눠 보여줘요	Split one image across the 3×3 face	1枚を3×3に分けて表示	一张图片分布在3×3整面	一張圖片分佈在3×3整面	一張圖片分佈在3×3整面	Divide una imagen en la cara 3×3	Répartit une image sur la face 3×3	Ein Bild über die 3×3-Seite verteilen	Divide uma imagem na face 3×3	Разделить картинку на грань 3×3	Chia một ảnh trên mặt 3×3	Bagi satu gambar pada sisi 3×3	แบ่งภาพเดียวบนด้าน 3×3
스킨 고르기	Choose a skin	スキンを選ぶ	选择皮肤	選擇外觀	選擇外觀	Elegir un diseño	Choisir un skin	Skin wählen	Escolher skin	Выбрать скин	Chọn giao diện	Pilih skin	เลือกสกิน
연습 중인 큐브 상태는 그대로 유지됩니다	Your current cube state is preserved	練習中の状態は維持されます	当前魔方状态会保留	目前魔方狀態會保留	目前魔方狀態會保留	Se conserva el estado actual	L'état actuel est conservé	Der aktuelle Zustand bleibt erhalten	O estado atual é preservado	Текущее состояние сохранится	Trạng thái hiện tại được giữ nguyên	Kondisi kubus tetap tersimpan	สถานะลูกบาศก์ปัจจุบันจะถูกเก็บไว้
이미지 스킨 안내	Image skin guide	画像スキンの案内	图片皮肤说明	圖片外觀說明	圖片外觀說明	Guía de diseños con imagen	Guide des skins image	Hinweis zu Bild-Skins	Guia de skins com imagem	О скинах с рисунком	Hướng dẫn giao diện ảnh	Panduan skin gambar	คำแนะนำสกินภาพ
확인했어요	Got it	わかりました	知道了	知道了	知道了	Entendido	Compris	Verstanden	Entendi	Понятно	Đã hiểu	Mengerti	เข้าใจแล้ว
앱에서 사용할 언어를 골라요	Choose the app language	アプリの言語を選択	选择应用语言	選擇應用程式語言	選擇應用程式語言	Elige el idioma de la app	Choisissez la langue de l'app	App-Sprache wählen	Escolha o idioma do app	Выберите язык приложения	Chọn ngôn ngữ ứng dụng	Pilih bahasa aplikasi	เลือกภาษาของแอป
언어 선택	Choose language	言語を選択	选择语言	選擇語言	選擇語言	Elegir idioma	Choisir la langue	Sprache wählen	Escolher idioma	Выбор языка	Chọn ngôn ngữ	Pilih bahasa	เลือกภาษา
닫기	Close	閉じる	关闭	關閉	關閉	Cerrar	Fermer	Schließen	Fechar	Закрыть	Đóng	Tutup	ปิด
위	Top	上	上	上	上	Arriba	Haut	Oben	Cima	Верх	Trên	Atas	บน
아래	Bottom	下	下	下	下	Abajo	Bas	Unten	Baixo	Низ	Dưới	Bawah	ล่าง
앞	Front	前	前	前	前	Frente	Avant	Vorne	Frente	Перед	Trước	Depan	หน้า
뒤	Back	後	后	後	後	Atrás	Arrière	Hinten	Trás	Зад	Sau	Belakang	หลัง
왼쪽	Left	左	左	左	左	Izquierda	Gauche	Links	Esquerda	Лево	Trái	Kiri	ซ้าย
오른쪽	Right	右	右	右	右	Derecha	Droite	Rechts	Direita	Право	Phải	Kanan	ขวา
노란색	Yellow	黄色	黄色	黃色	黃色	Amarillo	Jaune	Gelb	Amarelo	Жёлтый	Vàng	Kuning	เหลือง
흰색	White	白	白色	白色	白色	Blanco	Blanc	Weiß	Branco	Белый	Trắng	Putih	ขาว
초록색	Green	緑	绿色	綠色	綠色	Verde	Vert	Grün	Verde	Зелёный	Xanh lá	Hijau	เขียว
파란색	Blue	青	蓝色	藍色	藍色	Azul	Bleu	Blau	Azul	Синий	Xanh dương	Biru	น้ำเงิน
빨간색	Red	赤	红色	紅色	紅色	Rojo	Rouge	Rot	Vermelho	Красный	Đỏ	Merah	แดง
주황색	Orange	オレンジ	橙色	橙色	橙色	Naranja	Orange	Orange	Laranja	Оранжевый	Cam	Oranye	ส้ม
흰 십자	White cross	白い十字	白色十字	白色十字	白色十字	Cruz blanca	Croix blanche	Weißes Kreuz	Cruz branca	Белый крест	Dấu cộng trắng	Tanda plus putih	กากบาทสีขาว
첫 층 완성	First layer	一段目完成	完成第一层	完成第一層	完成第一層	Primera capa	Première couche	Erste Ebene	Primeira camada	Первый слой	Hoàn thành tầng đầu	Lapisan pertama	ชั้นแรก
가운데 층	Middle layer	中段	中间层	中間層	中間層	Capa central	Couche du milieu	Mittlere Ebene	Camada do meio	Средний слой	Tầng giữa	Lapisan tengah	ชั้นกลาง
노란 십자	Yellow cross	黄色い十字	黄色十字	黃色十字	黃色十字	Cruz amarilla	Croix jaune	Gelbes Kreuz	Cruz amarela	Жёлтый крест	Dấu cộng vàng	Tanda plus kuning	กากบาทสีเหลือง
노란 면	Yellow face	黄色い面	黄色面	黃色面	黃色面	Cara amarilla	Face jaune	Gelbe Seite	Face amarela	Жёлтая грань	Mặt vàng	Sisi kuning	ด้านสีเหลือง
모서리 자리 맞추기	Position corners	角の位置合わせ	角块归位	角塊歸位	角塊歸位	Colocar esquinas	Placer les coins	Ecken positionieren	Posicionar cantos	Расставить углы	Đặt góc đúng chỗ	Posisikan sudut	จัดตำแหน่งมุม
마지막 조각	Final pieces	最後のピース	最后的色块	最後的色塊	最後的色塊	Piezas finales	Dernières pièces	Letzte Teile	Peças finais	Последние элементы	Mảnh cuối	Bagian terakhir	ชิ้นสุดท้าย
모서리 넣기	Insert corner	角を入れる	插入角块	插入角塊	插入角塊	Insertar esquina	Insérer un coin	Ecke einsetzen	Inserir canto	Вставить угол	Đưa góc vào	Masukkan sudut	ใส่มุม
오른쪽으로 넣기	Insert right	右に入れる	向右插入	向右插入	向右插入	Insertar a la derecha	Insérer à droite	Rechts einsetzen	Inserir à direita	Вставить вправо	Đưa sang phải	Masukkan ke kanan	ใส่ทางขวา
왼쪽으로 넣기	Insert left	左に入れる	向左插入	向左插入	向左插入	Insertar a la izquierda	Insérer à gauche	Links einsetzen	Inserir à esquerda	Вставить влево	Đưa sang trái	Masukkan ke kiri	ใส่ทางซ้าย
십자 만들기	Make the cross	十字を作る	制作十字	製作十字	製作十字	Formar la cruz	Former la croix	Kreuz bilden	Formar a cruz	Собрать крест	Tạo dấu cộng	Buat tanda plus	สร้างกากบาท
수네	Sune	スーン	Sune	Sune	Sune	Sune	Sune	Sune	Sune	Сунэ	Sune	Sune	ซูน
모서리 돌리기	Cycle corners	角を入れ替える	轮换角块	輪換角塊	輪換角塊	Ciclar esquinas	Permuter les coins	Ecken tauschen	Ciclar cantos	Переставить углы	Đổi vị trí góc	Putar sudut	สลับมุม
조각 돌리기	Cycle edges	辺を入れ替える	轮换棱块	輪換邊塊	輪換邊塊	Ciclar aristas	Permuter les arêtes	Kanten tauschen	Ciclar arestas	Переставить рёбра	Đổi vị trí cạnh	Putar tepi	สลับขอบ
최고	Best	ベスト	最佳	最佳	最佳	Mejor	Meilleur	Bestzeit	Melhor	Лучшее	Tốt nhất	Terbaik	ดีที่สุด
① 초록 앞면으로 기준 잡기	① Start with the green front face	① 緑の前面から始める	① 从绿色前面开始	① 從綠色前面開始	① 從綠色前面開始	① Empieza con la cara verde al frente	① Commencez par la face verte devant	① Mit der grünen Vorderseite beginnen	① Comece com a face verde à frente	① Начните с зелёной передней грани	① Bắt đầu với mặt xanh lá phía trước	① Mulai dengan sisi hijau di depan	① เริ่มด้วยด้านสีเขียวไว้ด้านหน้า
위 노랑 · 아래 흰색 · 왼쪽 빨강 · 오른쪽 주황	Top yellow · bottom white · left red · right orange	上 黄 · 下 白 · 左 赤 · 右 オレンジ	上黄 · 下白 · 左红 · 右橙	上黃 · 下白 · 左紅 · 右橙	上黃 · 下白 · 左紅 · 右橙	Arriba amarillo · abajo blanco · izquierda rojo · derecha naranja	Haut jaune · bas blanc · gauche rouge · droite orange	Oben Gelb · unten Weiß · links Rot · rechts Orange	Cima amarelo · baixo branco · esquerda vermelho · direita laranja	Сверху жёлтый · снизу белый · слева красный · справа оранжевый	Trên vàng · dưới trắng · trái đỏ · phải cam	Atas kuning · bawah putih · kiri merah · kanan oranye	บนเหลือง · ล่างขาว · ซ้ายแดง · ขวาส้ม
반사광을 피하고 격자에 맞춘 뒤 약 1초간 고정해 주세요.	Avoid glare, align the grid, and hold still for about one second.	反射を避け、枠に合わせて約1秒止めてください。	避开反光，对准网格并保持约1秒。	避開反光，對準格線並保持約1秒。	避開反光，對準格線並保持約1秒。	Evita reflejos, alinea la cuadrícula y mantén el cubo quieto un segundo.	Évitez les reflets, alignez la grille et restez immobile une seconde.	Reflexionen vermeiden, am Raster ausrichten und etwa eine Sekunde stillhalten.	Evite reflexos, alinhe à grade e segure por cerca de um segundo.	Избегайте бликов, совместите с сеткой и держите неподвижно около секунды.	Tránh ánh chói, căn theo lưới và giữ yên khoảng một giây.	Hindari pantulan, sejajarkan dengan kisi, lalu tahan sekitar satu detik.	หลีกเลี่ยงแสงสะท้อน จัดให้ตรงกริด แล้วถือค้างประมาณหนึ่งวินาที
{0}/7 단계 완료	{0}/7 stages complete	{0}/7ステップ完了	已完成{0}/7步	已完成{0}/7步	已完成{0}/7步	{0}/7 etapas completadas	{0}/7 étapes terminées	{0}/7 Schritte fertig	{0}/7 etapas concluídas	Пройдено {0}/7 этапов	Hoàn thành {0}/7 bước	{0}/7 tahap selesai	เสร็จ {0}/7 ขั้น
{0} / {1} 완료	{0} / {1} complete	{0} / {1} 完了	完成 {0} / {1}	完成 {0} / {1}	完成 {0} / {1}	{0} / {1} completado	{0} / {1} terminé	{0} / {1} fertig	{0} / {1} concluído	Готово {0} / {1}	Hoàn thành {0} / {1}	{0} / {1} selesai	เสร็จ {0} / {1}
{0}단계를 먼저 마쳐 주세요.	Complete stage {0} first.	先にステップ{0}を完了してください。	请先完成第{0}步。	請先完成第{0}步。	請先完成第{0}步。	Completa primero la etapa {0}.	Terminez d'abord l'étape {0}.	Bitte zuerst Stufe {0} abschließen.	Conclua primeiro a etapa {0}.	Сначала пройдите этап {0}.	Hãy hoàn thành bước {0} trước.	Selesaikan tahap {0} terlebih dahulu.	กรุณาทำขั้น {0} ให้เสร็จก่อน
{0}단계 · {1}	Stage {0} · {1}	ステップ{0} · {1}	第{0}步 · {1}	第{0}步 · {1}	第{0}步 · {1}	Etapa {0} · {1}	Étape {0} · {1}	Stufe {0} · {1}	Etapa {0} · {1}	Этап {0} · {1}	Bước {0} · {1}	Tahap {0} · {1}	ขั้น {0} · {1}
{0}×{1} 연습	{0}×{1} Practice	{0}×{1} 練習	{0}×{1}练习	{0}×{1}練習	{0}×{1}練習	Práctica {0}×{1}	Entraînement {0}×{1}	{0}×{1}-Übung	Prática {0}×{1}	Практика {0}×{1}	Luyện tập {0}×{1}	Latihan {0}×{1}	ฝึก {0}×{1}
{0}개	{0} items	{0}個	{0}个	{0}個	{0}個	{0} elementos	{0} éléments	{0} Einträge	{0} itens	{0} шт.	{0} mục	{0} item	{0} รายการ
{0}수	{0} moves	{0}手	{0}步	{0}步	{0}步	{0} movimientos	{0} mouvements	{0} Züge	{0} movimentos	{0} ходов	{0} bước	{0} langkah	{0} ครั้ง
{0}×{1} · 기록 {2}개	{0}×{1} · {2} records	{0}×{1} · 記録{2}件	{0}×{1} · {2}条记录	{0}×{1} · {2}筆記錄	{0}×{1} · {2}筆記錄	{0}×{1} · {2} registros	{0}×{1} · {2} résultats	{0}×{1} · {2} Rekorde	{0}×{1} · {2} registros	{0}×{1} · записей: {2}	{0}×{1} · {2} thành tích	{0}×{1} · {2} catatan	{0}×{1} · {2} สถิติ
최근 {0}개	Latest {0}	最新{0}件	最近{0}条	最近{0}筆	最近{0}筆	Últimos {0}	{0} derniers	Letzte {0}	Últimos {0}	Последние {0}	{0} mục gần đây	{0} terbaru	ล่าสุด {0} รายการ
{0} / 6면	{0} / 6 faces	{0} / 6面	{0} / 6面	{0} / 6面	{0} / 6面	{0} / 6 caras	{0} / 6 faces	{0} / 6 Seiten	{0} / 6 faces	{0} / 6 граней	{0} / 6 mặt	{0} / 6 sisi	{0} / 6 ด้าน
{0}개 면을 저장했습니다	Saved {0} faces	{0}面を保存しました	已保存{0}面	已儲存{0}面	已儲存{0}面	Se guardaron {0} caras	{0} faces enregistrées	{0} Seiten gespeichert	{0} faces salvas	Сохранено граней: {0}	Đã lưu {0} mặt	{0} sisi tersimpan	บันทึกแล้ว {0} ด้าน
54칸을 읽었습니다 · 신뢰도 {0}%	54 stickers read · {0}% confidence	54マス認識 · 信頼度{0}%	已识别54格 · 置信度{0}%	已辨識54格 · 信心度{0}%	已辨識54格 · 信心度{0}%	54 casillas leídas · {0}% de confianza	54 cases lues · confiance {0}%	54 Felder erkannt · {0}% sicher	54 adesivos lidos · {0}% de confiança	Распознано 54 клетки · точность {0}%	Đã đọc 54 ô · độ tin cậy {0}%	54 kotak terbaca · keyakinan {0}%	อ่าน 54 ช่อง · ความมั่นใจ {0}%
색상 안정화 중 {0} / {1}	Stabilizing colors {0} / {1}	色を安定化中 {0} / {1}	正在稳定颜色 {0} / {1}	正在穩定顏色 {0} / {1}	正在穩定顏色 {0} / {1}	Estabilizando colores {0} / {1}	Stabilisation des couleurs {0} / {1}	Farben werden stabilisiert {0} / {1}	Estabilizando cores {0} / {1}	Стабилизация цвета {0} / {1}	Đang ổn định màu {0} / {1}	Menstabilkan warna {0} / {1}	กำลังปรับสีให้คงที่ {0} / {1}
{0}/6 {1}면 촬영 · 가운데 {2}	{0}/6 Scan {1} · center {2}	{0}/6 {1}面を撮影 · 中央{2}	{0}/6 拍摄{1}面 · 中心{2}	{0}/6 拍攝{1}面 · 中央{2}	{0}/6 拍攝{1}面 · 中央{2}	{0}/6 Escanea {1} · centro {2}	{0}/6 Scanner {1} · centre {2}	{0}/6 {1} scannen · Mitte {2}	{0}/6 Digitalize {1} · centro {2}	{0}/6 Снимите {1} · центр {2}	{0}/6 Quét mặt {1} · tâm {2}	{0}/6 Pindai {1} · tengah {2}	{0}/6 สแกนด้าน{1} · กลาง{2}
통과했습니다. {0}단계가 열렸습니다.	Passed! Stage {0} is unlocked.	合格！ステップ{0}が開きました。	通过！第{0}步已解锁。	通過！第{0}步已解鎖。	通過！第{0}步已解鎖。	¡Superado! Se desbloqueó la etapa {0}.	Réussi ! L'étape {0} est débloquée.	Geschafft! Stufe {0} ist freigeschaltet.	Concluído! A etapa {0} foi liberada.	Готово! Этап {0} открыт.	Đã qua! Bước {0} được mở.	Berhasil! Tahap {0} terbuka.	ผ่านแล้ว! ปลดล็อกขั้น {0}
{0} — 공식을 쓴 뒤 다시 살펴보세요.	{0} — Apply the algorithm, then check again.	{0} — 手順を行ってから確認しましょう。	{0} — 执行公式后再检查。	{0} — 執行公式後再檢查。	{0} — 執行公式後再檢查。	{0} — Aplica el algoritmo y vuelve a revisar.	{0} — Appliquez l'algorithme puis vérifiez.	{0} — Algorithmus ausführen und erneut prüfen.	{0} — Faça o algoritmo e verifique novamente.	{0} — Выполните алгоритм и проверьте снова.	{0} — Thực hiện công thức rồi kiểm tra lại.	{0} — Jalankan algoritma lalu periksa lagi.	{0} — ทำตามสูตรแล้วตรวจอีกครั้ง
{0} — 위층만 돌리면 맞습니다.	{0} — Only turn the top layer to align it.	{0} — 上面を回すだけで揃います。	{0} — 只需转动顶层即可。	{0} — 只需轉動頂層即可。	{0} — 只需轉動頂層即可。	{0} — Solo gira la capa superior.	{0} — Tournez seulement la couche du haut.	{0} — Nur die obere Ebene drehen.	{0} — Gire apenas a camada superior.	{0} — Поверните только верхний слой.	{0} — Chỉ cần xoay tầng trên.	{0} — Cukup putar lapisan atas.	{0} — หมุนเฉพาะชั้นบน
{0} — 자세를 맞춘 뒤 공식을 한 번 씁니다.	{0} — Align it, then apply the algorithm once.	{0} — 向きを合わせて手順を1回行います。	{0} — 调整方向后执行一次公式。	{0} — 調整方向後執行一次公式。	{0} — 調整方向後執行一次公式。	{0} — Alinéalo y aplica el algoritmo una vez.	{0} — Alignez puis appliquez l'algorithme une fois.	{0} — Ausrichten und den Algorithmus einmal ausführen.	{0} — Alinhe e faça o algoritmo uma vez.	{0} — Выровняйте и выполните алгоритм один раз.	{0} — Căn chỉnh rồi thực hiện công thức một lần.	{0} — Sejajarkan lalu jalankan algoritma sekali.	{0} — จัดแนวแล้วทำสูตรหนึ่งครั้ง
{0} — 공식을 {1}번 써야 하는 경우입니다.	{0} — Apply the algorithm {1} times.	{0} — 手順を{1}回行います。	{0} — 需要执行公式{1}次。	{0} — 需要執行公式{1}次。	{0} — 需要執行公式{1}次。	{0} — Aplica el algoritmo {1} veces.	{0} — Appliquez l'algorithme {1} fois.	{0} — Algorithmus {1}-mal ausführen.	{0} — Faça o algoritmo {1} vezes.	{0} — Выполните алгоритм {1} раз.	{0} — Thực hiện công thức {1} lần.	{0} — Jalankan algoritma {1} kali.	{0} — ทำสูตร {1} ครั้ง
{0} — 조각 하나를 더 맞춥니다. ({1}/4)	{0} — Solve one more piece. ({1}/4)	{0} — もう1つ揃えます。({1}/4)	{0} — 再还原一块。({1}/4)	{0} — 再還原一塊。({1}/4)	{0} — 再還原一塊。({1}/4)	{0} — Resuelve una pieza más. ({1}/4)	{0} — Placez une pièce de plus. ({1}/4)	{0} — Noch ein Teil lösen. ({1}/4)	{0} — Resolva mais uma peça. ({1}/4)	{0} — Соберите ещё один элемент. ({1}/4)	{0} — Giải thêm một mảnh. ({1}/4)	{0} — Selesaikan satu bagian lagi. ({1}/4)	{0} — แก้อีกหนึ่งชิ้น ({1}/4)
그래도 이 면으로 저장	Save this face anyway	この面のまま保存	仍按此面保存	仍以此面儲存	仍以此面儲存	Guardar esta cara igualmente	Enregistrer cette face quand même	Seite trotzdem speichern	Salvar esta face mesmo assim	Всё равно сохранить грань	Vẫn lưu mặt này	Tetap simpan sisi ini	บันทึกด้านนี้ต่อไป
위의 작은 면은 촬영한 원본색입니다. 누르면 다시 촬영할 수 있습니다.	The small face above shows the captured colors. Tap it to scan again.	上の小さな面は撮影した元の色です。タップすると撮り直せます。	上方小图是拍摄到的原始颜色。点击可重新拍摄。	上方小圖是拍攝到的原始顏色。點按可重新拍攝。	上方小圖是拍攝到的原始顏色。點按可重新拍攝。	La cara pequeña de arriba muestra los colores capturados. Tócala para escanear de nuevo.	La petite face ci-dessus montre les couleurs capturées. Touchez-la pour rescanner.	Die kleine Seite oben zeigt die aufgenommenen Farben. Zum erneuten Scannen antippen.	A face pequena acima mostra as cores capturadas. Toque para escanear novamente.	Маленькая грань сверху — снятые цвета. Нажмите, чтобы снять заново.	Mặt nhỏ phía trên là màu đã chụp. Chạm để quét lại.	Sisi kecil di atas menampilkan warna hasil pindai. Ketuk untuk memindai ulang.	ด้านเล็กด้านบนคือสีที่ถ่ายไว้ แตะเพื่อสแกนใหม่
이대로는 맞출 수 없습니다.	This cube cannot be solved.	この状態では揃えられません。	当前状态无法还原。	目前狀態無法還原。	目前狀態無法還原。	Este cubo no se puede resolver.	Ce cube ne peut pas être résolu.	Dieser Würfel ist nicht lösbar.	Este cubo não pode ser resolvido.	Такой кубик собрать невозможно.	Không thể giải khối này.	Kubus ini tidak bisa diselesaikan.	ลูกบาศก์นี้แก้ไม่ได้
이미 다 맞췄습니다.	Already solved.	すでに揃っています。	已经还原完成。	已經還原完成。	已經還原完成。	Ya está resuelto.	Déjà résolu.	Bereits gelöst.	Já está resolvido.	Уже собрано.	Đã giải xong.	Sudah selesai.	แก้เสร็จแล้ว
카메라 권한을 확인하고 있습니다…	Checking camera permission…	カメラの権限を確認しています…	正在确认相机权限…	正在確認相機權限…	正在確認相機權限…	Comprobando el permiso de cámara…	Vérification de l'autorisation caméra…	Kameraberechtigung wird geprüft…	Verificando a permissão da câmera…	Проверяем доступ к камере…	Đang kiểm tra quyền camera…	Memeriksa izin kamera…	กำลังตรวจสอบสิทธิ์กล้อง…
카메라는 {0}으로 읽었지만 오인식일 수 있어요. 실제로 {1}이 맞다면 버튼을 한 번 더 누르세요.	The camera read {0}, but it may be wrong. If it really is {1}, tap the button again.	カメラは{0}と認識しましたが、誤りの可能性があります。実際に{1}なら、もう一度ボタンを押してください。	相机识别为{0}，但可能有误。如果确实是{1}，请再按一次按钮。	相機辨識為{0}，但可能有誤。若確實是{1}，請再按一次按鈕。	相機辨識為{0}，但可能有誤。若確實是{1}，請再按一次按鈕。	La cámara leyó {0}, pero puede ser un error. Si de verdad es {1}, toca el botón otra vez.	La caméra a lu {0}, mais c'est peut-être une erreur. Si c'est bien {1}, appuyez de nouveau.	Die Kamera hat {0} erkannt, das kann falsch sein. Wenn es wirklich {1} ist, erneut tippen.	A câmera leu {0}, mas pode estar errado. Se for mesmo {1}, toque no botão novamente.	Камера распознала {0}, но это может быть ошибкой. Если это действительно {1}, нажмите кнопку ещё раз.	Camera đọc là {0}, nhưng có thể sai. Nếu đúng là {1}, hãy nhấn nút thêm lần nữa.	Kamera membaca {0}, tetapi bisa saja salah. Jika benar {1}, ketuk tombol sekali lagi.	กล้องอ่านเป็น {0} แต่อาจผิดพลาด หากเป็น {1} จริง ให้กดปุ่มอีกครั้ง
직접 조작 · {0}\n첫 동작: {1} · {2}	Manual · {0}\nFirst move: {1} · {2}	手動操作 · {0}\n最初の動き: {1} · {2}	手动操作 · {0}\n第一步: {1} · {2}	手動操作 · {0}\n第一步: {1} · {2}	手動操作 · {0}\n第一步: {1} · {2}	Manual · {0}\nPrimer movimiento: {1} · {2}	Manuel · {0}\nPremier mouvement : {1} · {2}	Manuell · {0}\nErster Zug: {1} · {2}	Manual · {0}\nPrimeiro movimento: {1} · {2}	Вручную · {0}\nПервый ход: {1} · {2}	Thủ công · {0}\nBước đầu: {1} · {2}	Manual · {0}\nGerakan pertama: {1} · {2}	ควบคุมเอง · {0}\nท่าแรก: {1} · {2}
삭제 준비됨 · 한 번 더 누르면 {0}×{1} 기록 {2}개를 모두 지워요.	Ready to delete · Tap again to erase all {2} {0}×{1} records.	削除の準備完了 · もう一度押すと{0}×{1}の記録{2}件をすべて削除します。	已准备删除 · 再按一次将清除全部{2}条{0}×{1}记录。	已準備刪除 · 再按一次將清除全部{2}筆{0}×{1}記錄。	已準備刪除 · 再按一次將清除全部{2}筆{0}×{1}記錄。	Listo para borrar · Toca otra vez para eliminar los {2} registros de {0}×{1}.	Prêt à supprimer · Appuyez encore pour effacer les {2} résultats {0}×{1}.	Bereit zum Löschen · Erneut tippen, um alle {2} {0}×{1}-Rekorde zu löschen.	Pronto para apagar · Toque de novo para apagar os {2} registros {0}×{1}.	Готово к удалению · Нажмите ещё раз, чтобы удалить все записи {0}×{1}: {2}.	Sẵn sàng xóa · Nhấn lần nữa để xóa toàn bộ {2} thành tích {0}×{1}.	Siap dihapus · Ketuk lagi untuk menghapus semua {2} catatan {0}×{1}.	พร้อมลบ · แตะอีกครั้งเพื่อลบสถิติ {0}×{1} ทั้ง {2} รายการ
큐브 전체 방향 바꾸기	Rotate the whole cube	キューブ全体を回す	转动整个魔方	轉動整個魔方	轉動整個魔方	Girar todo el cubo	Tourner tout le cube	Ganzen Würfel drehen	Girar o cubo inteiro	Повернуть весь кубик	Xoay cả khối	Putar seluruh kubus	หมุนลูกบาศก์ทั้งลูก
오른쪽 면을 시계 방향으로 한 칸	Turn the right face clockwise	右面を時計回りに90度	顺时针转动右面	順時針轉動右面	順時針轉動右面	Gira la cara derecha en sentido horario	Tourner la face droite dans le sens horaire	Die rechte Seite im Uhrzeigersinn drehen	Gire a face direita no sentido horário	Поверните правую грань по часовой стрелке	Xoay mặt phải theo chiều kim đồng hồ	Putar sisi kanan searah jarum jam	หมุนด้านขวาตามเข็มนาฬิกา
오른쪽 면을 반시계 방향으로 한 칸	Turn the right face counterclockwise	右面を反時計回りに90度	逆时针转动右面	逆時針轉動右面	逆時針轉動右面	Gira la cara derecha en sentido antihorario	Tourner la face droite dans le sens antihoraire	Die rechte Seite gegen den Uhrzeigersinn drehen	Gire a face direita no sentido anti-horário	Поверните правую грань против часовой стрелки	Xoay mặt phải ngược chiều kim đồng hồ	Putar sisi kanan berlawanan jarum jam	หมุนด้านขวาทวนเข็มนาฬิกา
오른쪽 면을 반 바퀴	Turn the right face 180°	右面を180度	转动右面180度	轉動右面180度	轉動右面180度	Gira la cara derecha 180°	Tourner la face droite de 180°	Die rechte Seite um 180° drehen	Gire a face direita 180°	Поверните правую грань на 180°	Xoay mặt phải 180°	Putar sisi kanan 180°	หมุนด้านขวา 180 องศา
왼쪽 면을 시계 방향으로 한 칸	Turn the left face clockwise	左面を時計回りに90度	顺时针转动左面	順時針轉動左面	順時針轉動左面	Gira la cara izquierda en sentido horario	Tourner la face gauche dans le sens horaire	Die linke Seite im Uhrzeigersinn drehen	Gire a face esquerda no sentido horário	Поверните левую грань по часовой стрелке	Xoay mặt trái theo chiều kim đồng hồ	Putar sisi kiri searah jarum jam	หมุนด้านซ้ายตามเข็มนาฬิกา
왼쪽 면을 반시계 방향으로 한 칸	Turn the left face counterclockwise	左面を反時計回りに90度	逆时针转动左面	逆時針轉動左面	逆時針轉動左面	Gira la cara izquierda en sentido antihorario	Tourner la face gauche dans le sens antihoraire	Die linke Seite gegen den Uhrzeigersinn drehen	Gire a face esquerda no sentido anti-horário	Поверните левую грань против часовой стрелки	Xoay mặt trái ngược chiều kim đồng hồ	Putar sisi kiri berlawanan jarum jam	หมุนด้านซ้ายทวนเข็มนาฬิกา
왼쪽 면을 반 바퀴	Turn the left face 180°	左面を180度	转动左面180度	轉動左面180度	轉動左面180度	Gira la cara izquierda 180°	Tourner la face gauche de 180°	Die linke Seite um 180° drehen	Gire a face esquerda 180°	Поверните левую грань на 180°	Xoay mặt trái 180°	Putar sisi kiri 180°	หมุนด้านซ้าย 180 องศา
위쪽 면을 시계 방향으로 한 칸	Turn the top face clockwise	上面を時計回りに90度	顺时针转动顶面	順時針轉動頂面	順時針轉動頂面	Gira la cara superior en sentido horario	Tourner la face du haut dans le sens horaire	Die obere Seite im Uhrzeigersinn drehen	Gire a face de cima no sentido horário	Поверните верхнюю грань по часовой стрелке	Xoay mặt trên theo chiều kim đồng hồ	Putar sisi atas searah jarum jam	หมุนด้านบนตามเข็มนาฬิกา
위쪽 면을 반시계 방향으로 한 칸	Turn the top face counterclockwise	上面を反時計回りに90度	逆时针转动顶面	逆時針轉動頂面	逆時針轉動頂面	Gira la cara superior en sentido antihorario	Tourner la face du haut dans le sens antihoraire	Die obere Seite gegen den Uhrzeigersinn drehen	Gire a face de cima no sentido anti-horário	Поверните верхнюю грань против часовой стрелки	Xoay mặt trên ngược chiều kim đồng hồ	Putar sisi atas berlawanan jarum jam	หมุนด้านบนทวนเข็มนาฬิกา
위쪽 면을 반 바퀴	Turn the top face 180°	上面を180度	转动顶面180度	轉動頂面180度	轉動頂面180度	Gira la cara superior 180°	Tourner la face du haut de 180°	Die obere Seite um 180° drehen	Gire a face de cima 180°	Поверните верхнюю грань на 180°	Xoay mặt trên 180°	Putar sisi atas 180°	หมุนด้านบน 180 องศา
아래쪽 면을 시계 방향으로 한 칸	Turn the bottom face clockwise	下面を時計回りに90度	顺时针转动底面	順時針轉動底面	順時針轉動底面	Gira la cara inferior en sentido horario	Tourner la face du bas dans le sens horaire	Die untere Seite im Uhrzeigersinn drehen	Gire a face de baixo no sentido horário	Поверните нижнюю грань по часовой стрелке	Xoay mặt dưới theo chiều kim đồng hồ	Putar sisi bawah searah jarum jam	หมุนด้านล่างตามเข็มนาฬิกา
아래쪽 면을 반시계 방향으로 한 칸	Turn the bottom face counterclockwise	下面を反時計回りに90度	逆时针转动底面	逆時針轉動底面	逆時針轉動底面	Gira la cara inferior en sentido antihorario	Tourner la face du bas dans le sens antihoraire	Die untere Seite gegen den Uhrzeigersinn drehen	Gire a face de baixo no sentido anti-horário	Поверните нижнюю грань против часовой стрелки	Xoay mặt dưới ngược chiều kim đồng hồ	Putar sisi bawah berlawanan jarum jam	หมุนด้านล่างทวนเข็มนาฬิกา
아래쪽 면을 반 바퀴	Turn the bottom face 180°	下面を180度	转动底面180度	轉動底面180度	轉動底面180度	Gira la cara inferior 180°	Tourner la face du bas de 180°	Die untere Seite um 180° drehen	Gire a face de baixo 180°	Поверните нижнюю грань на 180°	Xoay mặt dưới 180°	Putar sisi bawah 180°	หมุนด้านล่าง 180 องศา
앞면을 시계 방향으로 한 칸	Turn the front face clockwise	前面を時計回りに90度	顺时针转动前面	順時針轉動前面	順時針轉動前面	Gira la cara frontal en sentido horario	Tourner la face avant dans le sens horaire	Die vordere Seite im Uhrzeigersinn drehen	Gire a face frontal no sentido horário	Поверните переднюю грань по часовой стрелке	Xoay mặt trước theo chiều kim đồng hồ	Putar sisi depan searah jarum jam	หมุนด้านหน้าตามเข็มนาฬิกา
앞면을 반시계 방향으로 한 칸	Turn the front face counterclockwise	前面を反時計回りに90度	逆时针转动前面	逆時針轉動前面	逆時針轉動前面	Gira la cara frontal en sentido antihorario	Tourner la face avant dans le sens antihoraire	Die vordere Seite gegen den Uhrzeigersinn drehen	Gire a face frontal no sentido anti-horário	Поверните переднюю грань против часовой стрелки	Xoay mặt trước ngược chiều kim đồng hồ	Putar sisi depan berlawanan jarum jam	หมุนด้านหน้าทวนเข็มนาฬิกา
앞면을 반 바퀴	Turn the front face 180°	前面を180度	转动前面180度	轉動前面180度	轉動前面180度	Gira la cara frontal 180°	Tourner la face avant de 180°	Die vordere Seite um 180° drehen	Gire a face frontal 180°	Поверните переднюю грань на 180°	Xoay mặt trước 180°	Putar sisi depan 180°	หมุนด้านหน้า 180 องศา
뒷면을 시계 방향으로 한 칸	Turn the back face clockwise	後面を時計回りに90度	顺时针转动后面	順時針轉動後面	順時針轉動後面	Gira la cara trasera en sentido horario	Tourner la face arrière dans le sens horaire	Die hintere Seite im Uhrzeigersinn drehen	Gire a face traseira no sentido horário	Поверните заднюю грань по часовой стрелке	Xoay mặt sau theo chiều kim đồng hồ	Putar sisi belakang searah jarum jam	หมุนด้านหลังตามเข็มนาฬิกา
뒷면을 반시계 방향으로 한 칸	Turn the back face counterclockwise	後面を反時計回りに90度	逆时针转动后面	逆時針轉動後面	逆時針轉動後面	Gira la cara trasera en sentido antihorario	Tourner la face arrière dans le sens antihoraire	Die hintere Seite gegen den Uhrzeigersinn drehen	Gire a face traseira no sentido anti-horário	Поверните заднюю грань против часовой стрелки	Xoay mặt sau ngược chiều kim đồng hồ	Putar sisi belakang berlawanan jarum jam	หมุนด้านหลังทวนเข็มนาฬิกา
뒷면을 반 바퀴	Turn the back face 180°	後面を180度	转动后面180度	轉動後面180度	轉動後面180度	Gira la cara trasera 180°	Tourner la face arrière de 180°	Die hintere Seite um 180° drehen	Gire a face traseira 180°	Поверните заднюю грань на 180°	Xoay mặt sau 180°	Putar sisi belakang 180°	หมุนด้านหลัง 180 องศา
가운데 칸에 같은 색이 두 번 나온다	Two centers have the same color	中央のマスに同じ色が2つあります	中心块出现了两个相同颜色	中心塊出現兩個相同顏色	中心塊出現兩個相同顏色	Dos centros tienen el mismo color	Deux centres ont la même couleur	Zwei Mittelsteine haben dieselbe Farbe	Dois centros têm a mesma cor	Два центра одного цвета	Hai ô tâm trùng màu	Dua kotak tengah berwarna sama	ช่องกลางมีสีซ้ำกันสองช่อง
모서리 하나가 돌아간 채로 끼워져 있다. 눈으로는 알기 어려운 상태다	One corner is twisted in place — hard to spot by eye.	角が1つねじれて入っています。見た目では気づきにくい状態です。	有一个角块被拧转后装入，肉眼很难发现。	有一個角塊被扭轉後裝入，肉眼很難發現。	有一個角塊被扭轉後裝入，肉眼很難發現。	Una esquina está girada en su sitio; es difícil de ver.	Un coin est vrillé sur place — difficile à repérer à l'œil.	Eine Ecke ist verdreht eingesetzt — mit bloßem Auge kaum zu erkennen.	Um canto está torcido no lugar — difícil de perceber a olho nu.	Один угол вставлен повёрнутым — на глаз это почти незаметно.	Một góc bị xoay lệch khi lắp — rất khó nhận ra bằng mắt.	Satu sudut terpasang terpuntir — sulit terlihat dengan mata.	มุมหนึ่งถูกบิดขณะประกอบ — สังเกตด้วยตาได้ยาก
엣지 하나가 뒤집힌 채로 끼워져 있다. 눈으로는 알기 어려운 상태다	One edge is flipped in place — hard to spot by eye.	辺が1つ裏返しで入っています。見た目では気づきにくい状態です。	有一个棱块被翻转后装入，肉眼很难发现。	有一個邊塊被翻轉後裝入，肉眼很難發現。	有一個邊塊被翻轉後裝入，肉眼很難發現。	Una arista está volteada en su sitio; es difícil de ver.	Une arête est retournée sur place — difficile à repérer à l'œil.	Eine Kante ist verkehrt eingesetzt — mit bloßem Auge kaum zu erkennen.	Uma aresta está invertida no lugar — difícil de perceber a olho nu.	Одно ребро вставлено перевёрнутым — на глаз это почти незаметно.	Một cạnh bị lật khi lắp — rất khó nhận ra bằng mắt.	Satu tepi terpasang terbalik — sulit terlihat dengan mata.	ขอบหนึ่งถูกพลิกขณะประกอบ — สังเกตด้วยตาได้ยาก
조각 두 개가 서로 자리를 바꾼 채로 끼워져 있다	Two pieces are swapped with each other.	2つのピースが入れ替わって入っています。	有两块互换了位置。	有兩塊互換了位置。	有兩塊互換了位置。	Dos piezas están intercambiadas entre sí.	Deux pièces sont interverties.	Zwei Teile sind miteinander vertauscht.	Duas peças estão trocadas entre si.	Два элемента поменяны местами.	Hai mảnh bị hoán đổi vị trí.	Dua bagian tertukar posisi.	ชิ้นสองชิ้นสลับตำแหน่งกัน
색 번호 {0}는 없는 색이다	Color number {0} does not exist	色番号{0}は存在しません	颜色编号{0}不存在	顏色編號{0}不存在	顏色編號{0}不存在	El color número {0} no existe	La couleur numéro {0} n'existe pas	Farbnummer {0} gibt es nicht	A cor número {0} não existe	Цвета с номером {0} не существует	Không có màu số {0}	Warna nomor {0} tidak ada	ไม่มีสีหมายเลข {0}
{0} 칸이 {1}개다. 각 색은 9개여야 한다	There are {1} {0} stickers. Each color needs exactly 9.	{0}のマスが{1}個あります。各色は9個必要です。	{0}有{1}格。每种颜色必须是9格。	{0}有{1}格。每種顏色必須是9格。	{0}有{1}格。每種顏色必須是9格。	Hay {1} casillas {0}. Cada color debe tener 9.	Il y a {1} cases {0}. Chaque couleur doit en avoir 9.	Es gibt {1} Felder in {0}. Jede Farbe braucht genau 9.	Há {1} adesivos {0}. Cada cor precisa de 9.	Клеток «{0}»: {1}. Каждого цвета должно быть 9.	Có {1} ô {0}. Mỗi màu phải có đúng 9 ô.	Ada {1} kotak {0}. Setiap warna harus 9.	มีช่อง{0} {1} ช่อง แต่ละสีต้องมี 9 ช่อง
같은 모서리 조각이 두 번 나온다 ({0})	The same corner appears twice ({0})	同じ角が2回出てきます（{0}）	同一个角块出现了两次（{0}）	同一個角塊出現兩次（{0}）	同一個角塊出現兩次（{0}）	La misma esquina aparece dos veces ({0})	Le même coin apparaît deux fois ({0})	Dieselbe Ecke kommt zweimal vor ({0})	O mesmo canto aparece duas vezes ({0})	Один и тот же угол встречается дважды ({0})	Cùng một góc xuất hiện hai lần ({0})	Sudut yang sama muncul dua kali ({0})	มุมเดียวกันปรากฏสองครั้ง ({0})
실제 큐브에 없는 모서리다 ({0})	This corner does not exist on a real cube ({0})	実際のキューブに存在しない角です（{0}）	实体魔方上不存在这个角块（{0}）	實體魔方上不存在這個角塊（{0}）	實體魔方上不存在這個角塊（{0}）	Esta esquina no existe en un cubo real ({0})	Ce coin n'existe pas sur un vrai cube ({0})	Diese Ecke gibt es an einem echten Würfel nicht ({0})	Este canto não existe em um cubo real ({0})	Такого угла нет на настоящем кубике ({0})	Góc này không có trên khối thật ({0})	Sudut ini tidak ada pada kubus asli ({0})	มุมนี้ไม่มีอยู่บนลูกบาศก์จริง ({0})
같은 엣지 조각이 두 번 나온다 ({0})	The same edge appears twice ({0})	同じ辺が2回出てきます（{0}）	同一个棱块出现了两次（{0}）	同一個邊塊出現兩次（{0}）	同一個邊塊出現兩次（{0}）	La misma arista aparece dos veces ({0})	La même arête apparaît deux fois ({0})	Dieselbe Kante kommt zweimal vor ({0})	A mesma aresta aparece duas vezes ({0})	Одно и то же ребро встречается дважды ({0})	Cùng một cạnh xuất hiện hai lần ({0})	Tepi yang sama muncul dua kali ({0})	ขอบเดียวกันปรากฏสองครั้ง ({0})
실제 큐브에 없는 엣지다 ({0})	This edge does not exist on a real cube ({0})	実際のキューブに存在しない辺です（{0}）	实体魔方上不存在这个棱块（{0}）	實體魔方上不存在這個邊塊（{0}）	實體魔方上不存在這個邊塊（{0}）	Esta arista no existe en un cubo real ({0})	Cette arête n'existe pas sur un vrai cube ({0})	Diese Kante gibt es an einem echten Würfel nicht ({0})	Esta aresta não existe em um cubo real ({0})	Такого ребра нет на настоящем кубике ({0})	Cạnh này không có trên khối thật ({0})	Tepi ini tidak ada pada kubus asli ({0})	ขอบนี้ไม่มีอยู่บนลูกบาศก์จริง ({0})
중심색 확인이 필요해요	Check the center color	中心の色を確認してください	需要确认中心色	需要確認中心色	需要確認中心色	Revisa el color central	Vérifiez la couleur centrale	Mittelfarbe prüfen	Verifique a cor central	Проверьте цвет центра	Cần kiểm tra màu tâm	Periksa warna tengah	ต้องตรวจสอบสีตรงกลาง
색상 인식 완료	Colors recognized	色の認識完了	颜色识别完成	顏色辨識完成	顏色辨識完成	Colores reconocidos	Couleurs reconnues	Farben erkannt	Cores reconhecidas	Цвета распознаны	Đã nhận diện màu	Warna dikenali	รู้จำสีเสร็จแล้ว
카메라가 아직 준비되지 않았어요	The camera is not ready yet	カメラの準備ができていません	相机尚未准备好	相機尚未準備好	相機尚未準備好	La cámara aún no está lista	La caméra n'est pas encore prête	Die Kamera ist noch nicht bereit	A câmera ainda não está pronta	Камера ещё не готова	Camera chưa sẵn sàng	Kamera belum siap	กล้องยังไม่พร้อม
앞면부터 순서대로 촬영해 주세요	Scan the faces in order, starting with the front	前面から順に撮影してください	请从前面开始按顺序拍摄	請從前面開始依序拍攝	請從前面開始依序拍攝	Escanea las caras en orden, empezando por la frontal	Scannez les faces dans l'ordre, en commençant par l'avant	Seiten der Reihe nach scannen, beginnend vorne	Escaneie as faces em ordem, começando pela frontal	Снимайте грани по порядку, начиная с передней	Quét lần lượt các mặt, bắt đầu từ mặt trước	Pindai sisi secara berurutan, mulai dari depan	สแกนแต่ละด้านตามลำดับ เริ่มจากด้านหน้า
안내된 순서대로 여섯 면을 촬영합니다.	Scan all six faces in the order shown.	案内の順に6面を撮影します。	按提示顺序拍摄六个面。	依提示順序拍攝六個面。	依提示順序拍攝六個面。	Escanea las seis caras en el orden indicado.	Scannez les six faces dans l'ordre indiqué.	Scannen Sie alle sechs Seiten in der angezeigten Reihenfolge.	Escaneie as seis faces na ordem indicada.	Снимите все шесть граней в указанном порядке.	Quét đủ sáu mặt theo thứ tự hướng dẫn.	Pindai keenam sisi sesuai urutan yang ditampilkan.	สแกนทั้งหกด้านตามลำดับที่แสดง
방향이 섞이지 않도록 현재 파란 테두리의 면을 먼저 저장합니다.	Save the face outlined in blue first so the orientation stays consistent.	向きがずれないよう、青枠の面から先に保存します。	为避免方向混乱，请先保存蓝框标示的面。	為避免方向混亂，請先儲存藍框標示的面。	為避免方向混亂，請先儲存藍框標示的面。	Guarda primero la cara con borde azul para no perder la orientación.	Enregistrez d'abord la face encadrée en bleu pour garder l'orientation.	Speichern Sie zuerst die blau umrandete Seite, damit die Ausrichtung stimmt.	Salve primeiro a face com contorno azul para manter a orientação.	Сначала сохраните грань в синей рамке, чтобы не сбить ориентацию.	Lưu mặt viền xanh trước để không lệch hướng.	Simpan sisi bergaris biru lebih dulu agar orientasi tidak tertukar.	บันทึกด้านที่มีกรอบสีน้ำเงินก่อน เพื่อไม่ให้ทิศทางสลับกัน
아래에서 색을 고른 뒤 전개도의 칸을 누르면 바뀝니다.	Pick a color below, then tap a square on the net to change it.	下で色を選び、展開図のマスをタップすると変わります。	在下方选择颜色后，点击展开图的方格即可修改。	在下方選擇顏色後，點按展開圖的方格即可修改。	在下方選擇顏色後，點按展開圖的方格即可修改。	Elige un color abajo y toca una casilla del desplegado para cambiarla.	Choisissez une couleur en bas, puis touchez une case du patron pour la modifier.	Unten eine Farbe wählen, dann ein Feld im Netz antippen.	Escolha uma cor abaixo e toque em um quadrado da planificação.	Выберите цвет внизу и нажмите клетку развёртки, чтобы изменить её.	Chọn màu bên dưới rồi chạm vào ô trên hình khai triển để đổi.	Pilih warna di bawah, lalu ketuk kotak pada jaring untuk mengubahnya.	เลือกสีด้านล่าง แล้วแตะช่องบนแผนภาพคลี่เพื่อเปลี่ยน
가운데 칸은 각 면의 기준색이라 고정되어 있습니다.	The center square is each face's reference color, so it is fixed.	中央のマスは各面の基準色なので変更できません。	中心块是每个面的基准色，无法更改。	中心塊是每個面的基準色，無法更改。	中心塊是每個面的基準色，無法更改。	La casilla central es el color de referencia de cada cara y no se puede cambiar.	La case centrale est la couleur de référence de chaque face : elle est fixe.	Das mittlere Feld ist die Referenzfarbe jeder Seite und bleibt fest.	O quadrado central é a cor de referência de cada face e não muda.	Центральная клетка задаёт цвет грани и не меняется.	Ô tâm là màu chuẩn của mỗi mặt nên không thể đổi.	Kotak tengah adalah warna acuan tiap sisi, jadi tidak bisa diubah.	ช่องกลางคือสีอ้างอิงของแต่ละด้าน จึงเปลี่ยนไม่ได้
섞기 버튼으로 시작 · 두 손가락으로 시점 조절	Tap Scramble to start · Use two fingers to rotate the view	スクランブルで開始 · 2本指で視点を変更	点击打乱开始 · 双指调整视角	點按打亂開始 · 雙指調整視角	點按打亂開始 · 雙指調整視角	Toca Mezclar para empezar · Gira la vista con dos dedos	Touchez Mélanger pour commencer · Deux doigts pour pivoter la vue	Zum Start auf Mischen tippen · Ansicht mit zwei Fingern drehen	Toque em Embaralhar para começar · Gire a vista com dois dedos	Нажмите «Перемешать» · Двумя пальцами меняйте ракурс	Nhấn Trộn để bắt đầu · Dùng hai ngón để xoay góc nhìn	Ketuk Acak untuk mulai · Putar tampilan dengan dua jari	แตะสับเพื่อเริ่ม · ใช้สองนิ้วหมุนมุมมอง
힌트를 누르면 다음 동작을 설명해 드려요. 큐브는 자동으로 움직이지 않습니다.	Tap Hint to see the next move explained. The cube will not move on its own.	ヒントを押すと次の動きを説明します。キューブは自動では動きません。	点击提示会说明下一步。魔方不会自动转动。	點按提示會說明下一步。魔方不會自動轉動。	點按提示會說明下一步。魔方不會自動轉動。	Toca Pista para ver el siguiente movimiento. El cubo no se mueve solo.	Touchez Indice pour voir le mouvement suivant. Le cube ne bouge pas tout seul.	Auf Hinweis tippen, um den nächsten Zug zu sehen. Der Würfel dreht sich nicht von selbst.	Toque em Dica para ver o próximo movimento. O cubo não se move sozinho.	Нажмите «Подсказка», чтобы увидеть следующий ход. Кубик сам не крутится.	Nhấn Gợi ý để xem bước tiếp theo. Khối sẽ không tự xoay.	Ketuk Petunjuk untuk melihat gerakan berikutnya. Kubus tidak bergerak sendiri.	แตะคำใบ้เพื่อดูท่าถัดไป ลูกบาศก์จะไม่หมุนเอง
촬영한 실물 큐브를 이어서 풀고 있어요	Continuing with the cube you scanned	撮影した実物キューブの続きです	正在继续还原拍摄的实体魔方	正在繼續還原拍攝的實體魔方	正在繼續還原拍攝的實體魔方	Continuando con el cubo que escaneaste	Vous continuez avec le cube scanné	Weiter mit dem gescannten Würfel	Continuando com o cubo escaneado	Продолжаем со снятым кубиком	Đang tiếp tục với khối bạn đã quét	Melanjutkan kubus yang kamu pindai	กำลังแก้ลูกบาศก์ที่สแกนไว้ต่อ
색상 풀이는 끝났어요. 마지막으로 그림 가운데 방향만 맞추는 공식입니다.	The colors are solved. One last algorithm aligns the picture centers.	色は揃いました。最後に絵の中心の向きを合わせる手順です。	颜色已还原。最后一个公式用于对齐图案中心方向。	顏色已還原。最後一個公式用於對齊圖案中心方向。	顏色已還原。最後一個公式用於對齊圖案中心方向。	Los colores están resueltos. Un último algoritmo alinea los centros del dibujo.	Les couleurs sont résolues. Un dernier algorithme oriente les centres de l'image.	Die Farben sind gelöst. Ein letzter Algorithmus richtet die Bildmitten aus.	As cores estão resolvidas. Um último algoritmo alinha os centros da imagem.	Цвета собраны. Последний алгоритм выравнивает центры рисунка.	Đã giải xong màu. Còn một công thức cuối để chỉnh hướng tâm hình.	Warna sudah selesai. Satu algoritma terakhir meluruskan pusat gambar.	แก้สีเสร็จแล้ว เหลือสูตรสุดท้ายสำหรับจัดทิศทางกลางภาพ
새로 섞을까요?	Scramble again?	もう一度スクランブルしますか？	要重新打乱吗？	要重新打亂嗎？	要重新打亂嗎？	¿Mezclar de nuevo?	Mélanger à nouveau ?	Neu mischen?	Embaralhar de novo?	Перемешать заново?	Trộn lại nhé?	Acak ulang?	สับใหม่ไหม?
흰색 십자 조각을 하나씩 맞춰요	Solve the white cross one piece at a time	白い十字を1つずつ揃えます	逐块完成白色十字	逐塊完成白色十字	逐塊完成白色十字	Resuelve la cruz blanca pieza por pieza	Résolvez la croix blanche pièce par pièce	Das weiße Kreuz Stück für Stück lösen	Monte a cruz branca peça por peça	Собирайте белый крест по одному элементу	Ghép dấu cộng trắng từng mảnh một	Susun tanda plus putih satu per satu	แก้กากบาทสีขาวทีละชิ้น
힌트를 누르면 지금 상태에서 필요한 조작을 알려드려요	Tap Hint for the move you need right now	ヒントを押すと今必要な操作を教えます	点击提示会告诉你当前需要的操作	點按提示會告訴你目前需要的操作	點按提示會告訴你目前需要的操作	Toca Pista para saber qué movimiento necesitas ahora	Touchez Indice pour connaître le mouvement à faire maintenant	Auf Hinweis tippen für den jetzt nötigen Zug	Toque em Dica para saber o movimento necessário agora	Нажмите «Подсказка», чтобы узнать нужный сейчас ход	Nhấn Gợi ý để biết thao tác cần làm lúc này	Ketuk Petunjuk untuk tahu gerakan yang dibutuhkan sekarang	แตะคำใบ้เพื่อดูท่าที่ต้องทำตอนนี้
첫 연습을 마치면 최고 기록과 평균이\n여기에 차곡차곡 쌓여요.	Finish your first solve and your best time\nand averages will collect here.	最初の練習を終えると、ベストと平均が\nここにたまっていきます。	完成第一次练习后，最佳成绩和平均\n会记录在这里。	完成第一次練習後，最佳成績和平均\n會記錄在這裡。	完成第一次練習後，最佳成績和平均\n會記錄在這裡。	Termina tu primera resolución y aquí se irán\nguardando tu mejor tiempo y tus promedios.	Terminez votre premier résolu : votre meilleur temps\net vos moyennes s'accumuleront ici.	Nach dem ersten Lösen sammeln sich hier\ndeine Bestzeit und Durchschnitte.	Conclua sua primeira resolução e seu melhor tempo\ne médias vão se acumular aqui.	Завершите первую сборку — лучшее время\nи средние появятся здесь.	Hoàn thành lần giải đầu tiên, thành tích tốt nhất\nvà trung bình sẽ hiện ở đây.	Selesaikan latihan pertama, waktu terbaik\ndan rata-rata akan terkumpul di sini.	ทำครั้งแรกให้เสร็จ แล้วเวลาที่ดีที่สุด\nและค่าเฉลี่ยจะสะสมที่นี่
새 기록을 기다리는 중	Waiting for a new record	新しい記録を待っています	等待新记录	等待新記錄	等待新記錄	Esperando un nuevo registro	En attente d'un nouveau résultat	Warte auf einen neuen Rekord	Aguardando um novo registro	Ждём новый результат	Đang chờ thành tích mới	Menunggu catatan baru	รอสถิติใหม่
정말 모두 지우기	Erase everything	本当にすべて削除	确认全部清除	確認全部清除	確認全部清除	Borrar todo de verdad	Tout effacer	Wirklich alles löschen	Apagar tudo mesmo	Удалить всё	Xóa tất cả	Hapus semuanya	ลบทั้งหมดจริงๆ
배우기는 3×3부터 시작합니다. 3×3을 골라 주세요.	Lessons start with the 3×3. Please choose 3×3.	学習は3×3から始まります。3×3を選んでください。	学习从3×3开始，请选择3×3。	學習從3×3開始，請選擇3×3。	學習從3×3開始，請選擇3×3。	Las lecciones empiezan con el 3×3. Elige 3×3.	Les leçons commencent par le 3×3. Choisissez 3×3.	Der Kurs beginnt mit dem 3×3. Bitte 3×3 wählen.	As lições começam com o 3×3. Escolha 3×3.	Обучение начинается с 3×3. Выберите 3×3.	Bài học bắt đầu từ 3×3. Hãy chọn 3×3.	Pelajaran dimulai dari 3×3. Silakan pilih 3×3.	บทเรียนเริ่มที่ 3×3 กรุณาเลือก 3×3
공식 카드를 누르면 큐브에서 보여줍니다.	Tap an algorithm card to see it on the cube.	手順カードを押すとキューブで再生します。	点击公式卡片，会在魔方上演示。	點按公式卡片，會在魔方上示範。	點按公式卡片，會在魔方上示範。	Toca una tarjeta de algoritmo para verlo en el cubo.	Touchez une carte d'algorithme pour la voir sur le cube.	Auf eine Algorithmus-Karte tippen, um sie am Würfel zu sehen.	Toque em um cartão de algoritmo para vê-lo no cubo.	Нажмите карточку алгоритма, чтобы увидеть его на кубике.	Chạm thẻ công thức để xem trên khối.	Ketuk kartu algoritma untuk melihatnya di kubus.	แตะการ์ดสูตรเพื่อดูบนลูกบาศก์
‘한 면 전체’에서는 색상을 모두 맞춘 뒤 그림의 위·아래·좌·우 방향을 맞추는 공식이 한 번 더 나옵니다.\n‘조각마다 반복’은 일반 큐브처럼 색상만 맞추면 끝나요.	With “Whole face”, after the colors are solved one more algorithm orients the picture.\nWith “Repeat per sticker”, you are done once the colors match, like a normal cube.	「面全体」では色を揃えたあと、絵の上下左右を合わせる手順がもう一度必要です。\n「各マスで繰り返す」は普通のキューブと同じく色を揃えれば完成です。	选择“整面图片”时，颜色还原后还需一个公式来对齐图案方向。\n选择“每格重复”则和普通魔方一样，颜色还原即完成。	選擇「整面圖片」時，顏色還原後還需一個公式來對齊圖案方向。\n選擇「每格重複」則和一般魔方一樣，顏色還原即完成。	選擇「整面圖片」時，顏色還原後還需一個公式來對齊圖案方向。\n選擇「每格重複」則和一般魔方一樣，顏色還原即完成。	Con «Cara completa», tras resolver los colores hace falta un algoritmo más para orientar el dibujo.\nCon «Repetir por casilla», terminas al cuadrar los colores, como en un cubo normal.	Avec « Face entière », une fois les couleurs résolues, un algorithme de plus oriente l’image.\nAvec « Répéter par case », c’est fini dès que les couleurs correspondent, comme sur un cube normal.	Bei „Ganze Seite“ folgt nach den Farben noch ein Algorithmus, der das Bild ausrichtet.\nBei „Pro Feld wiederholen“ bist du fertig, sobald die Farben stimmen — wie bei einem normalen Würfel.	Com “Face inteira”, depois das cores é preciso mais um algoritmo para orientar a imagem.\nCom “Repetir por adesivo”, termina quando as cores batem, como num cubo normal.	В режиме «На всю грань» после сборки цветов нужен ещё один алгоритм, чтобы развернуть рисунок.\nВ режиме «Повторять на каждой клетке» всё готово, как только совпали цвета.	Với “Toàn mặt”, sau khi xong màu còn một công thức nữa để chỉnh hướng hình.\nVới “Lặp mỗi ô”, chỉ cần khớp màu là xong, như khối thường.	Dengan “Satu sisi penuh”, setelah warna selesai masih ada satu algoritma untuk mengarahkan gambar.\nDengan “Ulang per kotak”, selesai begitu warnanya cocok, seperti kubus biasa.	แบบ “เต็มหนึ่งด้าน” เมื่อแก้สีครบแล้วยังต้องใช้สูตรอีกหนึ่งเพื่อจัดทิศทางภาพ\nแบบ “ทำซ้ำทุกช่อง” เพียงแก้สีให้ครบก็เสร็จ เหมือนลูกบาศก์ทั่วไป
반시계	Reverse	反時計	逆时针	逆時針	逆時針	Inverso	Inverse	Umkehren	Inverso	Обратно	Ngược	Balik	ทวนเข็ม
가운데 오른쪽	Middle right	中段・右	中层向右	中層向右	中層向右	Centro derecha	Milieu droite	Mitte rechts	Meio à direita	Средний правый	Giữa phải	Tengah kanan	กลางขวา
가운데 왼쪽	Middle left	中段・左	中层向左	中層向左	中層向左	Centro izquierda	Milieu gauche	Mitte links	Meio à esquerda	Средний левый	Giữa trái	Tengah kiri	กลางซ้าย
가운데 층 조각을 오른쪽으로	Move a middle-layer edge to the right	中段の辺を右に入れる	把中层棱块送到右边	把中層邊塊送到右邊	把中層邊塊送到右邊	Lleva una arista del centro a la derecha	Amener une arête du milieu à droite	Eine Kante der mittleren Ebene nach rechts bringen	Leve uma aresta do meio para a direita	Переместить ребро среднего слоя вправо	Đưa cạnh tầng giữa sang phải	Pindahkan tepi lapisan tengah ke kanan	ย้ายขอบชั้นกลางไปทางขวา
가운데 층 조각을 왼쪽으로	Move a middle-layer edge to the left	中段の辺を左に入れる	把中层棱块送到左边	把中層邊塊送到左邊	把中層邊塊送到左邊	Lleva una arista del centro a la izquierda	Amener une arête du milieu à gauche	Eine Kante der mittleren Ebene nach links bringen	Leve uma aresta do meio para a esquerda	Переместить ребро среднего слоя влево	Đưa cạnh tầng giữa sang trái	Pindahkan tepi lapisan tengah ke kiri	ย้ายขอบชั้นกลางไปทางซ้าย
안티수네	Anti-Sune	アンチスーン	反Sune	反Sune	反Sune	Anti-Sune	Anti-Sune	Anti-Sune	Anti-Sune	Анти-Сунэ	Anti-Sune	Anti-Sune	แอนติซูน
티 공식	T algorithm	Tの手順	T公式	T公式	T公式	Algoritmo T	Algorithme T	T-Algorithmus	Algoritmo T	Алгоритм T	Công thức T	Algoritma T	สูตร T
수네의 반대 방향	The mirror of Sune	スーンの逆向き	Sune的反方向	Sune的反方向	Sune的反方向	El reflejo de Sune	Le miroir du Sune	Die Spiegelung von Sune	O espelho do Sune	Зеркальный Сунэ	Bản đối xứng của Sune	Kebalikan Sune	ท่ากลับด้านของซูน
첫 층 모서리를 아래로 넣을 때	Use when inserting a first-layer corner	1段目の角を下に入れるとき	把首层角块插入底层时	把首層角塊插入底層時	把首層角塊插入底層時	Úsalo al insertar una esquina de la primera capa	À utiliser pour insérer un coin de la première couche	Zum Einsetzen einer Ecke der ersten Ebene	Use ao inserir um canto da primeira camada	Когда вставляете угол первого слоя	Dùng khi đưa góc tầng đầu xuống	Gunakan saat memasukkan sudut lapisan pertama	ใช้เมื่อใส่มุมของชั้นแรก
조각을 오른쪽 자리로 내릴 때	Use when inserting an edge to the right	辺を右の位置に下ろすとき	把棱块送入右侧位置时	把邊塊送入右側位置時	把邊塊送入右側位置時	Úsalo al bajar una arista a la derecha	À utiliser pour insérer une arête à droite	Zum Einsetzen einer Kante nach rechts	Use ao encaixar uma aresta à direita	Когда опускаете ребро вправо	Dùng khi đưa cạnh xuống vị trí bên phải	Gunakan saat menurunkan tepi ke kanan	ใช้เมื่อใส่ขอบลงตำแหน่งขวา
조각을 왼쪽 자리로 내릴 때	Use when inserting an edge to the left	辺を左の位置に下ろすとき	把棱块送入左侧位置时	把邊塊送入左側位置時	把邊塊送入左側位置時	Úsalo al bajar una arista a la izquierda	À utiliser pour insérer une arête à gauche	Zum Einsetzen einer Kante nach links	Use ao encaixar uma aresta à esquerda	Когда опускаете ребро влево	Dùng khi đưa cạnh xuống vị trí bên trái	Gunakan saat menurunkan tepi ke kiri	ใช้เมื่อใส่ขอบลงตำแหน่งซ้าย
위 면에 십자를 만들 때	Use when making the cross on top	上面に十字を作るとき	在顶面制作十字时	在頂面製作十字時	在頂面製作十字時	Úsalo al formar la cruz de arriba	À utiliser pour former la croix du haut	Zum Bilden des Kreuzes oben	Use ao formar a cruz em cima	Когда собираете крест сверху	Dùng khi tạo dấu cộng ở mặt trên	Gunakan saat membuat tanda plus di atas	ใช้เมื่อสร้างกากบาทด้านบน
위 면을 노랗게 채울 때	Use when filling the top face with yellow	上面を黄色で埋めるとき	把顶面全部变黄时	把頂面全部變黃時	把頂面全部變黃時	Úsalo al llenar de amarillo la cara superior	À utiliser pour remplir la face du haut en jaune	Zum Gelbfärben der oberen Seite	Use ao preencher a face de cima de amarelo	Когда закрашиваете верх жёлтым	Dùng khi phủ vàng toàn mặt trên	Gunakan saat memenuhi sisi atas dengan kuning	ใช้เมื่อทำให้ด้านบนเป็นสีเหลืองทั้งหมด
위층 모서리 자리를 맞출 때	Use when placing the top-layer corners	上段の角の位置を合わせるとき	调整顶层角块位置时	調整頂層角塊位置時	調整頂層角塊位置時	Úsalo al colocar las esquinas de arriba	À utiliser pour placer les coins de la couche du haut	Zum Positionieren der Ecken der obersten Ebene	Use ao posicionar os cantos da camada de cima	Когда расставляете углы верхнего слоя	Dùng khi đặt góc tầng trên đúng chỗ	Gunakan saat menempatkan sudut lapisan atas	ใช้เมื่อจัดตำแหน่งมุมชั้นบน
마지막 조각들을 맞출 때	Use for the final edges	最後のピースを合わせるとき	还原最后的棱块时	還原最後的邊塊時	還原最後的邊塊時	Úsalo para las últimas aristas	À utiliser pour les dernières arêtes	Für die letzten Kanten	Use para as últimas arestas	Для последних рёбер	Dùng cho những mảnh cuối cùng	Gunakan untuk bagian terakhir	ใช้กับชิ้นสุดท้าย
넣을 모서리를 오른쪽 위 앞에 두고, 들어갈 때까지 반복	Place the corner at front-top-right and repeat until it drops in	入れる角を右上手前に置き、入るまで繰り返す	把要插入的角块放到右上前方，重复直到归位	把要插入的角塊放到右上前方，重複直到歸位	把要插入的角塊放到右上前方，重複直到歸位	Coloca la esquina arriba-derecha-frontal y repite hasta que entre	Placez le coin en haut-avant-droite et répétez jusqu'à insertion	Die Ecke vorne oben rechts platzieren und wiederholen, bis sie sitzt	Coloque o canto na frente-cima-direita e repita até encaixar	Поставьте угол вверху спереди справа и повторяйте, пока не встанет	Đặt góc ở phía trước trên bên phải và lặp đến khi vào đúng chỗ	Letakkan sudut di depan-atas-kanan dan ulangi sampai masuk	วางมุมไว้ที่หน้า-บน-ขวา แล้วทำซ้ำจนกว่าจะเข้าที่
점·한 줄·ㄱ자 어느 경우든 이걸 반복	Repeat this for a dot, a line, or an L shape	点・一本線・L字のいずれでもこれを繰り返す	无论是点、一条线还是L形，都重复这个公式	無論是點、一條線還是L形，都重複這個公式	無論是點、一條線還是L形，都重複這個公式	Repítelo tanto para el punto como para la línea o la L	Répétez pour un point, une ligne ou un L	Bei Punkt, Linie oder L-Form dasselbe wiederholen	Repita para ponto, linha ou formato de L	Повторяйте при точке, линии или уголке	Lặp lại dù là chấm, một vạch hay hình chữ L	Ulangi baik untuk titik, garis, maupun bentuk L	ทำซ้ำไม่ว่าจะเป็นจุด เส้นตรง หรือรูปตัว L
노란 면을 채울 때까지 반복	Repeat until the top face is all yellow	上面が黄色になるまで繰り返す	重复直到顶面全黄	重複直到頂面全黃	重複直到頂面全黃	Repite hasta que la cara superior sea toda amarilla	Répétez jusqu'à ce que la face du haut soit toute jaune	Wiederholen, bis die obere Seite ganz gelb ist	Repita até a face de cima ficar toda amarela	Повторяйте, пока верх не станет полностью жёлтым	Lặp lại đến khi mặt trên vàng hoàn toàn	Ulangi sampai sisi atas kuning semua	ทำซ้ำจนด้านบนเป็นสีเหลืองทั้งหมด
제자리인 모서리를 왼쪽 앞에 두고 반복	Keep a correct corner at front-left and repeat	正しい位置の角を左手前に置いて繰り返す	把已归位的角块放在左前方并重复	把已歸位的角塊放在左前方並重複	把已歸位的角塊放在左前方並重複	Deja una esquina correcta al frente-izquierda y repite	Gardez un coin correct en avant-gauche et répétez	Eine korrekte Ecke vorne links lassen und wiederholen	Mantenha um canto correto na frente-esquerda e repita	Оставьте верный угол спереди слева и повторяйте	Giữ góc đã đúng ở phía trước bên trái và lặp lại	Biarkan sudut yang sudah benar di depan-kiri lalu ulangi	วางมุมที่ถูกต้องไว้หน้า-ซ้าย แล้วทำซ้ำ
맞은 면을 뒤로 보내고 사용	Send the completed side to the back, then use it	揃った面を後ろに回して使う	把已完成的面转到后方再使用	把已完成的面轉到後方再使用	把已完成的面轉到後方再使用	Manda la cara ya resuelta atrás y úsalo	Placez la face terminée à l'arrière, puis utilisez-le	Die fertige Seite nach hinten drehen und anwenden	Mande a face pronta para trás e use	Отправьте готовую сторону назад и применяйте	Đưa mặt đã xong ra sau rồi dùng	Putar sisi yang sudah selesai ke belakang lalu gunakan	หมุนด้านที่เสร็จแล้วไปด้านหลัง แล้วใช้
자주 쓰는 마지막 층 공식	Common last-layer algorithms	よく使う最終段の手順	常用的顶层公式	常用的頂層公式	常用的頂層公式	Algoritmos habituales de la última capa	Algorithmes courants de la dernière couche	Häufige Algorithmen der letzten Ebene	Algoritmos comuns da última camada	Частые алгоритмы последнего слоя	Công thức tầng cuối thường dùng	Algoritma lapisan terakhir yang sering dipakai	สูตรชั้นสุดท้ายที่ใช้บ่อย
큐브를 흰 면이 아래로 가게 잡습니다. 앞으로 이 방향을 계속 유지합니다.	Hold the cube with the white face down, and keep it that way from now on.	白い面を下にして持ちます。これ以降もこの向きを保ちます。	把白色面朝下拿着，之后一直保持这个方向。	把白色面朝下拿著，之後一直保持這個方向。	把白色面朝下拿著，之後一直保持這個方向。	Sujeta el cubo con la cara blanca abajo y mantén esa orientación.	Tenez le cube face blanche en bas et gardez cette orientation.	Halte den Würfel mit der weißen Seite nach unten und behalte das bei.	Segure o cubo com a face branca para baixo e mantenha assim.	Держите кубик белой гранью вниз и не меняйте это положение.	Cầm khối với mặt trắng hướng xuống và giữ nguyên hướng này.	Pegang kubus dengan sisi putih di bawah dan pertahankan arah itu.	ถือลูกบาศก์ให้ด้านสีขาวอยู่ล่าง และคงทิศทางนี้ไว้ตลอด
아래 면에 흰색 십자를 만듭니다. 십자를 이루는 네 조각은 두 가지 색을 갖고 있습니다. 흰색은 아래를 향하고, 나머지 색은 옆면 가운데 색과 맞아야 합니다.	Build a white cross on the bottom. Each of the four edges has two colors: white must face down, and the other color must match the center of its side.	下の面に白い十字を作ります。十字を作る4つの辺は2色を持っています。白は下を向き、もう一方の色は側面の中央の色と合わせます。	在底面做出白色十字。组成十字的四个棱块各有两种颜色：白色朝下，另一种颜色要与所在侧面的中心色一致。	在底面做出白色十字。組成十字的四個邊塊各有兩種顏色：白色朝下，另一種顏色要與所在側面的中心色一致。	在底面做出白色十字。組成十字的四個邊塊各有兩種顏色：白色朝下，另一種顏色要與所在側面的中心色一致。	Forma una cruz blanca abajo. Cada una de las cuatro aristas tiene dos colores: el blanco va hacia abajo y el otro debe coincidir con el centro de su cara.	Formez une croix blanche en bas. Chacune des quatre arêtes a deux couleurs : le blanc vers le bas, l'autre doit correspondre au centre de sa face.	Baue unten ein weißes Kreuz. Jede der vier Kanten hat zwei Farben: Weiß zeigt nach unten, die andere Farbe muss zum Mittelstein ihrer Seite passen.	Monte uma cruz branca embaixo. Cada uma das quatro arestas tem duas cores: o branco fica para baixo e a outra cor deve casar com o centro da sua face.	Соберите белый крест снизу. У каждого из четырёх рёбер два цвета: белый смотрит вниз, а второй должен совпасть с центром своей грани.	Tạo dấu cộng trắng ở mặt dưới. Bốn cạnh tạo nên dấu cộng đều có hai màu: màu trắng hướng xuống, màu còn lại phải khớp với ô tâm của mặt bên.	Buat tanda plus putih di sisi bawah. Keempat tepi penyusunnya punya dua warna: putih menghadap bawah, warna lainnya harus cocok dengan kotak tengah sisinya.	สร้างกากบาทสีขาวที่ด้านล่าง ขอบทั้งสี่ที่ประกอบเป็นกากบาทมีสองสี สีขาวหันลง ส่วนอีกสีต้องตรงกับสีกลางของด้านนั้น
예를 들어 흰-초록 조각은 흰색이 아래, 초록색이 초록 가운데가 있는 면을 향하게 놓습니다.	For example, put the white-green edge with white facing down and green facing the side with the green center.	たとえば白と緑の辺は、白を下に、緑を緑の中央がある面に向けて置きます。	例如白绿棱块要让白色朝下，绿色朝向中心是绿色的那一面。	例如白綠邊塊要讓白色朝下，綠色朝向中心是綠色的那一面。	例如白綠邊塊要讓白色朝下，綠色朝向中心是綠色的那一面。	Por ejemplo, coloca la arista blanco-verde con el blanco abajo y el verde hacia la cara del centro verde.	Par exemple, placez l'arête blanc-vert avec le blanc en bas et le vert vers la face au centre vert.	Zum Beispiel: Die weiß-grüne Kante mit Weiß nach unten und Grün zur Seite mit dem grünen Mittelstein.	Por exemplo, ponha a aresta branco-verde com o branco para baixo e o verde voltado à face de centro verde.	Например, ребро бело-зелёное: белым вниз, зелёным к грани с зелёным центром.	Ví dụ cạnh trắng-xanh lá: đặt màu trắng hướng xuống, màu xanh hướng về mặt có tâm xanh.	Misalnya tepi putih-hijau: putih menghadap bawah, hijau menghadap sisi bertengah hijau.	เช่น ขอบขาว-เขียว ให้สีขาวหันลง และสีเขียวหันไปยังด้านที่มีสีกลางเป็นเขียว
이 단계는 공식 없이 눈으로 찾아 옮깁니다. 한 조각씩 위로 올린 뒤 자리를 맞추고 아래로 내리면 됩니다. 다른 조각을 망가뜨렸다면 되돌리고 다시 해보세요.	This step needs no algorithm — just look and move. Bring one edge to the top, line it up, then bring it down. If you break another piece, undo and try again.	この段階は手順なしで、目で見て動かします。1つずつ上に上げ、位置を合わせてから下ろします。他のピースを崩したら、戻してやり直しましょう。	这一步不需要公式，靠眼睛观察移动即可。把棱块逐个转到顶层，对准位置后再放下去。如果破坏了其他块，撤销后重来。	這一步不需要公式，靠眼睛觀察移動即可。把邊塊逐個轉到頂層，對準位置後再放下去。如果破壞了其他塊，復原後重來。	這一步不需要公式，靠眼睛觀察移動即可。把邊塊逐個轉到頂層，對準位置後再放下去。如果破壞了其他塊，復原後重來。	Este paso no necesita algoritmo: basta con mirar y mover. Sube una arista arriba, alinéala y bájala. Si rompes otra pieza, deshaz e inténtalo de nuevo.	Cette étape ne demande aucun algorithme : observez et déplacez. Montez une arête en haut, alignez-la, puis descendez-la. Si vous cassez une autre pièce, annulez et recommencez.	Dieser Schritt braucht keinen Algorithmus — schauen und bewegen genügt. Bringe eine Kante nach oben, richte sie aus und setze sie nach unten. Wenn du ein anderes Teil zerstörst, mach es rückgängig und versuche es erneut.	Esta etapa não precisa de algoritmo: basta olhar e mover. Leve uma aresta para cima, alinhe e desça. Se estragar outra peça, desfaça e tente de novo.	На этом шаге алгоритм не нужен — просто смотрите и двигайте. Поднимите ребро наверх, совместите и опустите. Если сломали другой элемент, отмените и повторите.	Bước này không cần công thức, chỉ cần nhìn và di chuyển. Đưa từng cạnh lên trên, canh đúng vị trí rồi hạ xuống. Nếu làm hỏng mảnh khác, hãy hoàn tác và thử lại.	Langkah ini tidak butuh algoritma — cukup lihat dan pindahkan. Naikkan satu tepi ke atas, sejajarkan, lalu turunkan. Jika merusak bagian lain, urungkan dan ulangi.	ขั้นนี้ไม่ต้องใช้สูตร เพียงมองแล้วขยับ ยกขอบขึ้นไปด้านบนทีละชิ้น จัดให้ตรงตำแหน่งแล้วค่อยลงมา หากทำชิ้นอื่นเสีย ให้ย้อนกลับแล้วลองใหม่
십자를 만들었으면 이제 아래 층 네 모서리를 채웁니다.	With the cross done, now fill in the four bottom-layer corners.	十字ができたら、次は下段の4つの角を埋めます。	做好十字后，接着填满底层的四个角块。	做好十字後，接著填滿底層的四個角塊。	做好十字後，接著填滿底層的四個角塊。	Con la cruz lista, ahora rellena las cuatro esquinas de la capa inferior.	La croix terminée, remplissez maintenant les quatre coins de la couche du bas.	Wenn das Kreuz steht, füllst du jetzt die vier Ecken der unteren Ebene.	Com a cruz pronta, agora preencha os quatro cantos da camada de baixo.	Крест собран — теперь заполните четыре угла нижнего слоя.	Xong dấu cộng, giờ điền nốt bốn góc của tầng dưới.	Setelah tanda plus jadi, kini isi keempat sudut lapisan bawah.	เมื่อได้กากบาทแล้ว ต่อไปเติมมุมทั้งสี่ของชั้นล่าง
흰색이 들어간 모서리 조각을 찾아 위층으로 올리고, 그 조각이 들어갈 자리 바로 위에 오게 돌립니다.	Find a corner that contains white, bring it to the top layer, and turn it so it sits right above the slot it belongs in.	白を含む角を探して上段に上げ、その角が入る場所の真上に来るように回します。	找到带白色的角块，把它转到顶层，再转到它该去的位置正上方。	找到帶白色的角塊，把它轉到頂層，再轉到它該去的位置正上方。	找到帶白色的角塊，把它轉到頂層，再轉到它該去的位置正上方。	Busca una esquina que tenga blanco, súbela a la capa de arriba y gírala hasta que quede justo encima de su hueco.	Trouvez un coin contenant du blanc, montez-le en haut et tournez pour le placer juste au-dessus de son emplacement.	Suche eine Ecke mit Weiß, bringe sie in die obere Ebene und drehe sie genau über ihren Platz.	Ache um canto com branco, leve-o à camada de cima e gire até ficar logo acima do lugar dele.	Найдите угол с белым, поднимите его на верхний слой и поверните так, чтобы он оказался прямо над своим местом.	Tìm góc có màu trắng, đưa lên tầng trên rồi xoay sao cho nó nằm ngay phía trên vị trí cần vào.	Cari sudut yang punya warna putih, bawa ke lapisan atas, lalu putar hingga tepat di atas tempatnya.	หามุมที่มีสีขาว ยกขึ้นไปชั้นบน แล้วหมุนให้อยู่เหนือช่องที่มันต้องลงพอดี
그 다음 아래 공식을 자리가 맞을 때까지 반복합니다. 한 번, 세 번, 또는 다섯 번 만에 들어갑니다.	Then repeat the algorithm below until it drops into place — it takes one, three, or five repeats.	そのあと下の手順を、入るまで繰り返します。1回、3回、または5回で入ります。	然后重复下面的公式，直到它归位——一次、三次或五次就会进去。	然後重複下面的公式，直到它歸位——一次、三次或五次就會進去。	然後重複下面的公式，直到它歸位——一次、三次或五次就會進去。	Después repite el algoritmo de abajo hasta que entre: hacen falta una, tres o cinco repeticiones.	Répétez ensuite l'algorithme ci-dessous jusqu'à insertion : une, trois ou cinq fois.	Wiederhole dann den Algorithmus unten, bis sie einrastet — ein-, drei- oder fünfmal.	Depois repita o algoritmo abaixo até encaixar: uma, três ou cinco vezes.	Затем повторяйте алгоритм ниже, пока угол не встанет — один, три или пять раз.	Sau đó lặp công thức bên dưới đến khi góc vào chỗ — một, ba hoặc năm lần.	Lalu ulangi algoritma di bawah sampai masuk — satu, tiga, atau lima kali.	จากนั้นทำสูตรด้านล่างซ้ำจนกว่าจะเข้าที่ — ใช้หนึ่ง สาม หรือห้าครั้ง
공식이 아래 십자를 망가뜨리는 것처럼 보여도 괜찮습니다. 반복하면 제자리로 돌아옵니다.	It may look like the algorithm is breaking the cross below — that's fine. Keep repeating and it comes back.	手順が下の十字を崩すように見えても大丈夫です。繰り返せば元に戻ります。	公式看起来像会破坏底部十字，但没关系，重复下去就会复原。	公式看起來像會破壞底部十字，但沒關係，重複下去就會復原。	公式看起來像會破壞底部十字，但沒關係，重複下去就會復原。	Puede parecer que el algoritmo rompe la cruz de abajo, pero no pasa nada: al repetir vuelve a su sitio.	L'algorithme semble casser la croix du bas, mais c'est normal : en répétant, elle revient.	Es sieht aus, als würde der Algorithmus das Kreuz unten zerstören — das ist in Ordnung. Beim Wiederholen kommt es zurück.	Pode parecer que o algoritmo quebra a cruz de baixo, mas tudo bem: repetindo, ela volta.	Может показаться, что алгоритм ломает крест внизу — это нормально, при повторении он вернётся.	Có thể trông như công thức làm hỏng dấu cộng bên dưới, nhưng không sao — lặp lại là nó trở về.	Mungkin tampak algoritma merusak tanda plus di bawah, tapi tidak apa-apa. Jika diulang, tanda plusnya kembali.	อาจดูเหมือนสูตรทำให้กากบาทด้านล่างเสีย แต่ไม่เป็นไร ทำซ้ำแล้วจะกลับมาเหมือนเดิม
아래 두 줄이 끝났습니다. 이제 가운데 층의 네 조각을 채웁니다.	The bottom two rows are done. Now fill the four middle-layer edges.	下の2段が終わりました。次は中段の4つの辺を埋めます。	底下两层完成了。接着填满中层的四个棱块。	底下兩層完成了。接著填滿中層的四個邊塊。	底下兩層完成了。接著填滿中層的四個邊塊。	Las dos filas de abajo están listas. Ahora rellena las cuatro aristas del centro.	Les deux rangées du bas sont finies. Remplissez maintenant les quatre arêtes du milieu.	Die unteren zwei Reihen stehen. Jetzt die vier Kanten der mittleren Ebene füllen.	As duas fileiras de baixo estão prontas. Agora preencha as quatro arestas do meio.	Два нижних ряда готовы. Теперь заполните четыре ребра среднего слоя.	Hai hàng dưới đã xong. Giờ điền bốn cạnh của tầng giữa.	Dua baris bawah selesai. Sekarang isi keempat tepi lapisan tengah.	สองแถวล่างเสร็จแล้ว ต่อไปเติมขอบทั้งสี่ของชั้นกลาง
위층에서 노란색이 없는 조각을 찾습니다. 그 조각의 앞면 색이 앞면 가운데 색과 맞도록 위층을 돌립니다.	Find a top-layer edge with no yellow. Turn the top layer until that edge's front color matches the front center.	上段で黄色を含まない辺を探します。その辺の手前の色が前面の中央と合うように上段を回します。	在顶层找一个不含黄色的棱块。转动顶层，让它朝前的颜色与前面的中心色一致。	在頂層找一個不含黃色的邊塊。轉動頂層，讓它朝前的顏色與前面的中心色一致。	在頂層找一個不含黃色的邊塊。轉動頂層，讓它朝前的顏色與前面的中心色一致。	Busca una arista de arriba sin amarillo. Gira la capa superior hasta que su color frontal coincida con el centro frontal.	Trouvez une arête du haut sans jaune. Tournez la couche du haut pour que sa couleur avant corresponde au centre avant.	Suche oben eine Kante ohne Gelb. Drehe die obere Ebene, bis ihre Vorderfarbe zum vorderen Mittelstein passt.	Ache uma aresta de cima sem amarelo. Gire a camada superior até a cor da frente dela casar com o centro da frente.	Найдите вверху ребро без жёлтого. Крутите верхний слой, пока его передний цвет не совпадёт с передним центром.	Tìm cạnh ở tầng trên không có màu vàng. Xoay tầng trên sao cho màu mặt trước của nó khớp với ô tâm phía trước.	Cari tepi di lapisan atas yang tanpa kuning. Putar lapisan atas sampai warna depannya cocok dengan tengah depan.	หาขอบชั้นบนที่ไม่มีสีเหลือง หมุนชั้นบนจนสีด้านหน้าของขอบตรงกับสีกลางด้านหน้า
조각이 오른쪽으로 내려가야 하면 첫 번째 공식, 왼쪽으로 내려가야 하면 두 번째 공식을 씁니다.	If the edge needs to go down to the right, use the first algorithm; to the left, use the second.	辺を右に下ろすなら1つ目の手順、左に下ろすなら2つ目の手順を使います。	如果棱块要往右下去就用第一个公式，往左就用第二个。	如果邊塊要往右下去就用第一個公式，往左就用第二個。	如果邊塊要往右下去就用第一個公式，往左就用第二個。	Si la arista debe bajar a la derecha usa el primer algoritmo; si baja a la izquierda, el segundo.	Si l'arête doit descendre à droite, utilisez le premier algorithme ; à gauche, le second.	Muss die Kante nach rechts hinunter, nimm den ersten Algorithmus; nach links den zweiten.	Se a aresta precisa descer à direita use o primeiro algoritmo; à esquerda, o segundo.	Если ребро должно уйти вниз вправо — первый алгоритм, влево — второй.	Nếu cạnh cần xuống bên phải thì dùng công thức thứ nhất, xuống bên trái thì dùng công thức thứ hai.	Jika tepi harus turun ke kanan pakai algoritma pertama; ke kiri pakai yang kedua.	หากขอบต้องลงทางขวาให้ใช้สูตรแรก หากลงทางซ้ายให้ใช้สูตรที่สอง
넣을 자리에 이미 엉뚱한 조각이 들어 있으면, 아무 공식이나 한 번 써서 그 조각을 위로 빼낸 뒤 다시 하세요.	If the wrong edge is already in the slot, run either algorithm once to lift it out, then start over.	入れたい場所にすでに違う辺が入っている場合は、どちらかの手順を1回使って上に出してからやり直します。	如果目标位置已经有错误的棱块，先用任一公式做一次把它顶出去，再重来。	如果目標位置已經有錯誤的邊塊，先用任一公式做一次把它頂出去，再重來。	如果目標位置已經有錯誤的邊塊，先用任一公式做一次把它頂出去，再重來。	Si en el hueco ya hay una arista equivocada, aplica cualquiera de los dos algoritmos una vez para sacarla y vuelve a empezar.	Si une mauvaise arête occupe déjà l'emplacement, appliquez l'un des algorithmes une fois pour la faire sortir, puis recommencez.	Sitzt schon die falsche Kante im Platz, führe einen der Algorithmen einmal aus, um sie herauszuholen, und beginne neu.	Se a aresta errada já estiver no lugar, use qualquer um dos algoritmos uma vez para tirá-la e recomece.	Если в гнезде уже стоит не то ребро, выполните любой из алгоритмов один раз, чтобы вытолкнуть его, и начните заново.	Nếu vị trí đó đã có cạnh sai, hãy dùng một trong hai công thức một lần để đẩy nó lên, rồi làm lại.	Jika slotnya sudah terisi tepi yang salah, jalankan salah satu algoritma sekali untuk mengeluarkannya, lalu ulangi.	หากช่องนั้นมีขอบผิดอยู่แล้ว ให้ใช้สูตรใดสูตรหนึ่งหนึ่งครั้งเพื่อดันออกมา แล้วเริ่มใหม่
아래 두 층이 끝났습니다. 남은 건 맨 위층뿐입니다.	The bottom two layers are complete. Only the top layer is left.	下の2段が完成しました。残るは最上段だけです。	底下两层完成了，只剩顶层。	底下兩層完成了，只剩頂層。	底下兩層完成了，只剩頂層。	Las dos capas de abajo están completas. Solo queda la de arriba.	Les deux couches du bas sont terminées. Il ne reste que celle du haut.	Die unteren zwei Ebenen sind fertig. Es bleibt nur die oberste.	As duas camadas de baixo estão completas. Só falta a de cima.	Два нижних слоя готовы. Остался только верхний.	Hai tầng dưới đã xong. Chỉ còn tầng trên cùng.	Dua lapisan bawah selesai. Tinggal lapisan atas.	สองชั้นล่างเสร็จแล้ว เหลือแค่ชั้นบนสุด
위 면의 노란색 모양을 봅니다. 점 하나, 한 줄, 또는 ㄱ자 모양일 겁니다.	Look at the yellow shape on top: it will be a dot, a line, or an L.	上面の黄色い形を見ます。点1つ、一本線、またはL字のいずれかです。	观察顶面的黄色形状：点、一条线，或者L形。	觀察頂面的黃色形狀：點、一條線，或者L形。	觀察頂面的黃色形狀：點、一條線，或者L形。	Mira la forma amarilla de arriba: será un punto, una línea o una L.	Regardez la forme jaune sur le dessus : un point, une ligne ou un L.	Sieh dir die gelbe Form oben an: ein Punkt, eine Linie oder ein L.	Olhe a forma amarela em cima: será um ponto, uma linha ou um L.	Посмотрите на жёлтую фигуру сверху: точка, линия или уголок.	Nhìn hình vàng ở mặt trên: sẽ là một chấm, một vạch, hoặc hình chữ L.	Lihat bentuk kuning di atas: berupa titik, garis, atau huruf L.	ดูรูปสีเหลืองด้านบน จะเป็นจุด เส้นตรง หรือรูปตัว L
한 줄이면 그 줄이 좌우로 눕도록 돌리고, ㄱ자면 꺾인 부분이 왼쪽 위로 가게 돌린 뒤 공식을 씁니다.	For a line, turn it so it lies left-to-right. For an L, turn it so the corner points to the upper left, then run the algorithm.	一本線なら左右に寝るように回し、L字なら折れた部分が左上に来るように回してから手順を使います。	如果是一条线，转到横向；如果是L形，把拐角转到左上方，然后使用公式。	如果是一條線，轉到橫向；如果是L形，把拐角轉到左上方，然後使用公式。	如果是一條線，轉到橫向；如果是L形，把拐角轉到左上方，然後使用公式。	Si es una línea, gírala hasta que quede horizontal; si es una L, pon el vértice arriba a la izquierda y aplica el algoritmo.	Pour une ligne, placez-la à l'horizontale ; pour un L, mettez l'angle en haut à gauche, puis appliquez l'algorithme.	Bei einer Linie drehe sie waagerecht; beim L zeigt die Ecke nach links oben — dann den Algorithmus ausführen.	Se for linha, deixe-a na horizontal; se for L, ponha o vértice no canto superior esquerdo e aplique o algoritmo.	Линию поверните горизонтально; у уголка разверните вершину влево-вверх, затем выполните алгоритм.	Nếu là một vạch, xoay cho nằm ngang; nếu là chữ L, xoay cho góc gập hướng lên trái rồi dùng công thức.	Jika garis, putar sampai mendatar; jika huruf L, arahkan sikunya ke kiri atas, lalu jalankan algoritma.	ถ้าเป็นเส้นตรง ให้หมุนจนวางแนวนอน ถ้าเป็นตัว L ให้หมุนให้มุมหักชี้ไปบนซ้าย แล้วใช้สูตร
점 하나면 공식을 세 번까지 반복하면 십자가 생깁니다. 지금은 모서리 색이 맞지 않아도 괜찮습니다.	For a dot, repeat the algorithm up to three times and the cross appears. The side colors don't need to match yet.	点1つなら手順を最大3回繰り返すと十字ができます。今は側面の色が合っていなくても大丈夫です。	如果是点，最多重复三次公式就会出现十字。此时侧面颜色不对也没关系。	如果是點，最多重複三次公式就會出現十字。此時側面顏色不對也沒關係。	如果是點，最多重複三次公式就會出現十字。此時側面顏色不對也沒關係。	Si es un punto, repite el algoritmo hasta tres veces y aparecerá la cruz. De momento no importa que los lados no coincidan.	Pour un point, répétez l'algorithme jusqu'à trois fois et la croix apparaît. Peu importe si les côtés ne correspondent pas encore.	Beim Punkt bis zu dreimal wiederholen, dann entsteht das Kreuz. Die Seitenfarben dürfen noch nicht passen.	Se for ponto, repita o algoritmo até três vezes e a cruz aparece. Por ora não importa se as laterais não batem.	При точке повторите алгоритм до трёх раз — появится крест. Боковые цвета пока могут не совпадать.	Nếu là một chấm, lặp công thức tối đa ba lần sẽ có dấu cộng. Lúc này màu các mặt bên chưa cần khớp.	Jika titik, ulangi algoritma sampai tiga kali dan tanda plus akan muncul. Warna sisi belum perlu cocok.	ถ้าเป็นจุด ให้ทำสูตรซ้ำได้ถึงสามครั้งจะเกิดกากบาท ตอนนี้สีด้านข้างยังไม่ต้องตรงก็ได้
십자가 생겼으니 위 면 전체를 노란색으로 채웁니다.	Now that the cross is there, fill the whole top face with yellow.	十字ができたので、上面全体を黄色で埋めます。	十字已经出现，现在把整个顶面变成黄色。	十字已經出現，現在把整個頂面變成黃色。	十字已經出現，現在把整個頂面變成黃色。	Ya tienes la cruz; ahora llena de amarillo toda la cara superior.	La croix est faite : remplissez maintenant toute la face du haut en jaune.	Das Kreuz steht — jetzt die ganze obere Seite gelb machen.	A cruz está pronta; agora preencha toda a face de cima de amarelo.	Крест собран — теперь сделайте всю верхнюю грань жёлтой.	Đã có dấu cộng, giờ phủ vàng toàn bộ mặt trên.	Tanda plus sudah jadi, kini penuhi seluruh sisi atas dengan kuning.	เมื่อได้กากบาทแล้ว ให้ทำด้านบนทั้งหมดเป็นสีเหลือง
노란색이 위를 향한 모서리가 몇 개인지 셉니다. 0개, 1개, 2개 중 하나입니다.	Count the corners with yellow facing up — it will be zero, one, or two.	黄色が上を向いている角の数を数えます。0個、1個、2個のいずれかです。	数一数黄色朝上的角块有几个：0个、1个或2个。	數一數黃色朝上的角塊有幾個：0個、1個或2個。	數一數黃色朝上的角塊有幾個：0個、1個或2個。	Cuenta las esquinas con amarillo hacia arriba: serán cero, una o dos.	Comptez les coins dont le jaune est vers le haut : zéro, un ou deux.	Zähle die Ecken mit Gelb nach oben — es sind null, eine oder zwei.	Conte os cantos com amarelo para cima: zero, um ou dois.	Посчитайте углы с жёлтым вверх — их будет ноль, один или два.	Đếm số góc có màu vàng hướng lên: sẽ là 0, 1 hoặc 2.	Hitung sudut yang kuningnya menghadap atas: nol, satu, atau dua.	นับมุมที่สีเหลืองหันขึ้น จะมี 0, 1 หรือ 2 มุม
1개라면 그 모서리를 왼쪽 아래에 두고 공식을 씁니다. 0개나 2개라면 아무 데서나 한 번 쓰고 다시 세어 보세요.	If there is one, put that corner at the lower left and run the algorithm. If there are zero or two, run it once from anywhere and count again.	1個ならその角を左下に置いて手順を使います。0個か2個ならどこからでも1回使って、もう一度数えます。	如果是1个，把那个角块放到左下方再使用公式。如果是0个或2个，随便做一次后重新数。	如果是1個，把那個角塊放到左下方再使用公式。如果是0個或2個，隨便做一次後重新數。	如果是1個，把那個角塊放到左下方再使用公式。如果是0個或2個，隨便做一次後重新數。	Si hay una, ponla abajo a la izquierda y aplica el algoritmo. Si hay cero o dos, aplícalo una vez desde cualquier posición y vuelve a contar.	S'il y en a un, placez ce coin en bas à gauche et appliquez l'algorithme. S'il y en a zéro ou deux, appliquez-le une fois n'importe où puis recomptez.	Bei einer: Diese Ecke nach unten links legen und den Algorithmus ausführen. Bei null oder zwei: einmal von irgendwo ausführen und neu zählen.	Se houver um, ponha esse canto embaixo à esquerda e aplique o algoritmo. Se houver zero ou dois, aplique uma vez de qualquer posição e conte de novo.	Если один — поставьте этот угол влево-вниз и выполните алгоритм. Если ноль или два — выполните один раз из любого положения и пересчитайте.	Nếu có 1, đặt góc đó ở dưới bên trái rồi dùng công thức. Nếu có 0 hoặc 2, cứ làm một lần từ vị trí bất kỳ rồi đếm lại.	Jika ada satu, letakkan sudut itu di kiri bawah lalu jalankan algoritma. Jika nol atau dua, jalankan sekali dari mana saja lalu hitung ulang.	ถ้ามี 1 มุม ให้วางมุมนั้นไว้ล่างซ้ายแล้วใช้สูตร ถ้ามี 0 หรือ 2 ให้ทำหนึ่งครั้งจากตำแหน่งใดก็ได้แล้วนับใหม่
이 공식은 아래 두 층을 건드리지 않습니다. 위 면이 노랗게 될 때까지 반복하면 됩니다.	This algorithm leaves the bottom two layers alone. Just repeat until the top face is yellow.	この手順は下の2段に影響しません。上面が黄色になるまで繰り返せば大丈夫です。	这个公式不会影响底下两层。重复到顶面全黄即可。	這個公式不會影響底下兩層。重複到頂面全黃即可。	這個公式不會影響底下兩層。重複到頂面全黃即可。	Este algoritmo no toca las dos capas de abajo. Repite hasta que la cara superior sea amarilla.	Cet algorithme ne touche pas les deux couches du bas. Répétez jusqu'à ce que la face du haut soit jaune.	Dieser Algorithmus lässt die unteren zwei Ebenen unberührt. Einfach wiederholen, bis die obere Seite gelb ist.	Este algoritmo não mexe nas duas camadas de baixo. Repita até a face de cima ficar amarela.	Этот алгоритм не трогает два нижних слоя. Повторяйте, пока верх не станет жёлтым.	Công thức này không ảnh hưởng hai tầng dưới. Cứ lặp đến khi mặt trên vàng hết.	Algoritma ini tidak mengganggu dua lapisan bawah. Ulangi saja sampai sisi atas kuning.	สูตรนี้ไม่กระทบสองชั้นล่าง ทำซ้ำจนด้านบนเป็นสีเหลืองก็พอ
위 면이 노랗게 됐지만 옆면 색은 아직 어긋나 있습니다.	The top is yellow now, but the side colors still don't line up.	上面は黄色になりましたが、側面の色はまだ揃っていません。	顶面已经全黄，但侧面颜色还没对齐。	頂面已經全黃，但側面顏色還沒對齊。	頂面已經全黃，但側面顏色還沒對齊。	La cara de arriba ya es amarilla, pero los colores laterales aún no cuadran.	Le dessus est jaune, mais les couleurs des côtés ne correspondent pas encore.	Oben ist alles gelb, aber die Seitenfarben stimmen noch nicht.	O topo já está amarelo, mas as cores das laterais ainda não batem.	Верх стал жёлтым, но боковые цвета ещё не совпадают.	Mặt trên đã vàng, nhưng màu các mặt bên vẫn chưa khớp.	Sisi atas sudah kuning, tetapi warna sampingnya belum sejajar.	ด้านบนเป็นสีเหลืองแล้ว แต่สีด้านข้างยังไม่ตรงกัน
네 모서리 중 이미 제자리에 있는 것이 있는지 찾습니다. 옆면 두 색이 각각 그 면 가운데 색과 맞으면 제자리입니다.	Look for a corner that is already in the right spot — that means both of its side colors match their centers.	4つの角のうち、すでに正しい位置にあるものを探します。側面2色がそれぞれの中央の色と合っていれば正しい位置です。	看看四个角块里有没有已经归位的：它两侧的颜色都与所在面的中心色一致，就是归位了。	看看四個角塊裡有沒有已經歸位的：它兩側的顏色都與所在面的中心色一致，就是歸位了。	看看四個角塊裡有沒有已經歸位的：它兩側的顏色都與所在面的中心色一致，就是歸位了。	Busca si alguna de las cuatro esquinas ya está en su sitio: sus dos colores laterales coinciden con sus centros.	Cherchez un coin déjà bien placé : ses deux couleurs latérales correspondent aux centres.	Suche eine Ecke, die schon richtig sitzt — beide Seitenfarben passen zu ihren Mittelsteinen.	Procure um canto que já esteja no lugar certo: as duas cores laterais dele batem com os centros.	Найдите угол, который уже стоит верно: оба его боковых цвета совпадают со своими центрами.	Tìm xem có góc nào đã đúng vị trí chưa: hai màu bên của nó khớp với ô tâm tương ứng.	Cari sudut yang sudah benar posisinya: kedua warna sampingnya cocok dengan kotak tengahnya.	หามุมที่อยู่ถูกตำแหน่งแล้ว คือสีสองด้านของมันตรงกับสีกลางของด้านนั้น
제자리인 모서리를 왼쪽 앞에 두고 공식을 씁니다. 제자리가 하나도 없으면 아무 데서나 한 번 쓰면 하나가 맞습니다.	Put that correct corner at the front left and run the algorithm. If none is correct, run it once from anywhere and one will land.	正しい位置の角を左手前に置いて手順を使います。1つも正しくなければ、どこからでも1回使えば1つ揃います。	把已归位的角块放到左前方再使用公式。如果一个都没有归位，随便做一次就会有一个归位。	把已歸位的角塊放到左前方再使用公式。如果一個都沒有歸位，隨便做一次就會有一個歸位。	把已歸位的角塊放到左前方再使用公式。如果一個都沒有歸位，隨便做一次就會有一個歸位。	Coloca esa esquina correcta al frente-izquierda y aplica el algoritmo. Si no hay ninguna correcta, aplícalo una vez y aparecerá una.	Placez ce coin correct en avant-gauche et appliquez l'algorithme. Si aucun n'est correct, appliquez-le une fois et un se placera.	Lege die korrekte Ecke nach vorne links und führe den Algorithmus aus. Ist keine korrekt, führe ihn einmal irgendwo aus — dann passt eine.	Ponha esse canto certo na frente-esquerda e aplique o algoritmo. Se nenhum estiver certo, aplique uma vez e um vai se encaixar.	Поставьте верный угол вперёд-влево и выполните алгоритм. Если верных нет, выполните один раз откуда угодно — один встанет.	Đặt góc đã đúng ở phía trước bên trái rồi dùng công thức. Nếu chưa góc nào đúng, cứ làm một lần là sẽ có một góc vào đúng chỗ.	Letakkan sudut yang benar itu di depan-kiri lalu jalankan algoritma. Jika belum ada yang benar, jalankan sekali dan satu akan pas.	วางมุมที่ถูกต้องไว้หน้า-ซ้ายแล้วใช้สูตร หากยังไม่มีมุมใดถูก ให้ทำหนึ่งครั้งจากตำแหน่งใดก็ได้ แล้วจะมีมุมหนึ่งเข้าที่
이 공식은 왼쪽 앞 모서리를 그대로 두고 나머지 셋을 돌립니다. 다 맞을 때까지 반복하세요.	This algorithm keeps the front-left corner in place and cycles the other three. Repeat until all four are right.	この手順は左手前の角をそのままにして、残り3つを入れ替えます。すべて揃うまで繰り返してください。	这个公式保持左前角块不动，轮换其余三个。重复到四个都归位为止。	這個公式保持左前角塊不動，輪換其餘三個。重複到四個都歸位為止。	這個公式保持左前角塊不動，輪換其餘三個。重複到四個都歸位為止。	Este algoritmo deja fija la esquina frontal izquierda y rota las otras tres. Repite hasta que las cuatro estén bien.	Cet algorithme laisse le coin avant-gauche en place et permute les trois autres. Répétez jusqu'à ce que les quatre soient bons.	Dieser Algorithmus lässt die Ecke vorne links stehen und tauscht die anderen drei. Wiederhole, bis alle vier stimmen.	Este algoritmo mantém o canto da frente-esquerda e cicla os outros três. Repita até os quatro ficarem certos.	Этот алгоритм оставляет угол спереди слева и меняет местами остальные три. Повторяйте, пока не встанут все четыре.	Công thức này giữ nguyên góc trước bên trái và hoán vị ba góc còn lại. Lặp đến khi cả bốn đều đúng.	Algoritma ini menahan sudut depan-kiri dan memutar tiga lainnya. Ulangi sampai keempatnya benar.	สูตรนี้คงมุมหน้า-ซ้ายไว้ แล้วสลับอีกสามมุม ทำซ้ำจนครบทั้งสี่มุม
마지막입니다. 모서리는 다 맞았고 그 사이 조각들만 남았습니다.	Last step. The corners are all set — only the edges between them are left.	最後です。角はすべて揃い、その間の辺だけが残っています。	最后一步。角块都已归位，只剩它们之间的棱块。	最後一步。角塊都已歸位，只剩它們之間的邊塊。	最後一步。角塊都已歸位，只剩它們之間的邊塊。	Último paso. Las esquinas ya están; solo quedan las aristas entre ellas.	Dernière étape. Les coins sont en place ; il ne reste que les arêtes entre eux.	Letzter Schritt. Die Ecken sitzen — nur die Kanten dazwischen fehlen noch.	Última etapa. Os cantos estão prontos; faltam só as arestas entre eles.	Последний шаг. Углы на местах — остались только рёбра между ними.	Bước cuối. Các góc đã đúng hết, chỉ còn các cạnh ở giữa.	Langkah terakhir. Semua sudut sudah benar, tinggal tepi di antaranya.	ขั้นสุดท้าย มุมเข้าที่ครบแล้ว เหลือแค่ขอบระหว่างมุม
이미 맞은 면이 하나 있는지 찾습니다. 있으면 그 면을 뒤로 보냅니다.	See if one side is already complete. If so, turn it to the back.	すでに揃っている面が1つあるか探します。あればその面を後ろに回します。	看看有没有一面已经完成。如果有，把它转到后方。	看看有沒有一面已經完成。如果有，把它轉到後方。	看看有沒有一面已經完成。如果有，把它轉到後方。	Mira si ya hay una cara completa. Si la hay, ponla atrás.	Regardez si une face est déjà complète. Si oui, placez-la à l'arrière.	Schau, ob eine Seite schon fertig ist. Wenn ja, drehe sie nach hinten.	Veja se já existe uma face completa. Se sim, mande-a para trás.	Проверьте, есть ли уже готовая сторона. Если да — отправьте её назад.	Xem thử đã có mặt nào hoàn chỉnh chưa. Nếu có, đưa mặt đó ra sau.	Lihat apakah sudah ada satu sisi yang lengkap. Jika ada, putar ke belakang.	ดูว่ามีด้านใดเสร็จแล้วหรือยัง ถ้ามี ให้หมุนด้านนั้นไปด้านหลัง
공식을 쓰고, 다 맞지 않았으면 한 번 더 씁니다.	Run the algorithm; if it isn't solved yet, run it once more.	手順を使い、揃わなければもう一度使います。	使用公式；如果还没还原，再做一次。	使用公式；如果還沒還原，再做一次。	使用公式；如果還沒還原，再做一次。	Aplica el algoritmo; si aún no está resuelto, aplícalo otra vez.	Appliquez l'algorithme ; si ce n'est pas fini, recommencez une fois.	Führe den Algorithmus aus; ist es noch nicht gelöst, noch einmal.	Aplique o algoritmo; se ainda não estiver resolvido, aplique mais uma vez.	Выполните алгоритм; если ещё не собрано, выполните ещё раз.	Dùng công thức; nếu chưa xong thì làm thêm một lần nữa.	Jalankan algoritma; jika belum selesai, jalankan sekali lagi.	ใช้สูตร หากยังไม่เสร็จให้ทำอีกครั้ง
맞은 면이 하나도 없으면 아무 데서나 한 번 쓰면 하나가 생깁니다. 그 다음 다시 하세요.	If no side is complete, run it once from anywhere and one will appear. Then start again.	揃っている面が1つもなければ、どこからでも1回使えば1面できます。そのあとやり直してください。	如果一面都没完成，随便做一次就会出现一面，然后再重来。	如果一面都沒完成，隨便做一次就會出現一面，然後再重來。	如果一面都沒完成，隨便做一次就會出現一面，然後再重來。	Si no hay ninguna cara completa, aplícalo una vez desde cualquier sitio y aparecerá una. Luego repite.	Si aucune face n'est complète, appliquez-le une fois n'importe où et une apparaîtra. Puis recommencez.	Ist keine Seite fertig, führe ihn einmal irgendwo aus — dann entsteht eine. Danach von vorn.	Se nenhuma face estiver completa, aplique uma vez de qualquer lugar e uma vai aparecer. Depois recomece.	Если готовых сторон нет, выполните один раз откуда угодно — появится одна. Затем начните заново.	Nếu chưa mặt nào hoàn chỉnh, cứ làm một lần từ vị trí bất kỳ là sẽ có một mặt. Sau đó làm lại.	Jika belum ada sisi yang lengkap, jalankan sekali dari mana saja dan satu akan muncul. Lalu ulangi.	หากยังไม่มีด้านใดเสร็จ ให้ทำหนึ่งครั้งจากตำแหน่งใดก็ได้ แล้วจะมีด้านหนึ่งเสร็จ จากนั้นเริ่มใหม่
클래식	Classic	クラシック	经典	經典	經典	Clásico	Classique	Klassisch	Clássico	Классический	Cổ điển	Klasik	คลาสสิก
파스텔	Pastel	パステル	粉彩	粉彩	粉彩	Pastel	Pastel	Pastell	Pastel	Пастель	Pastel	Pastel	พาสเทล
비비드	Vivid	ビビッド	鲜艳	鮮豔	鮮豔	Vivo	Vif	Kräftig	Vívido	Яркий	Rực rỡ	Cerah	สดใส
톤다운	Muted	おちつき	低饱和	低飽和	低飽和	Apagado	Sobre	Gedeckt	Suave	Приглушённый	Trầm	Kalem	โทนหม่น
다크스틸	Dark steel	ダークスチール	暗钢	暗鋼	暗鋼	Acero oscuro	Acier sombre	Dunkelstahl	Aço escuro	Тёмная сталь	Thép tối	Baja gelap	เหล็กเข้ม
우드	Wood	ウッド	木纹	木紋	木紋	Madera	Bois	Holz	Madeira	Дерево	Gỗ	Kayu	ไม้
말랑 친구들	Squishy Pals	もちもちフレンズ	软软伙伴	軟軟夥伴	軟軟夥伴	Amigos Blanditos	Copains Tout Doux	Knuddelfreunde	Amigos Fofinhos	Мягкие друзья	Những Người Bạn Mềm Mại	Teman Empuk	เพื่อนนุ่มนิ่ม
문라이트 리조트	Moonlight Resort	ムーンライトリゾート	月光度假村	月光度假村	月光度假村	Resort a la Luz de la Luna	Resort au Clair de Lune	Mondschein-Resort	Resort ao Luar	Лунный курорт	Khu Nghỉ Dưỡng Ánh Trăng	Resor Cahaya Bulan	รีสอร์ตแสงจันทร์
별빛 모험단	Starlight Crew	スターライト冒険隊	星光冒险队	星光冒險隊	星光冒險隊	Escuadrón Estelar	Équipage des Étoiles	Sternenlicht-Crew	Tripulação Estelar	Звёздный отряд	Đội Thám Hiểm Ánh Sao	Kru Cahaya Bintang	ทีมผจญภัยแสงดาว
여름 바캉스	Summer Holiday	サマーバカンス	夏日假期	夏日假期	夏日假期	Vacaciones de Verano	Vacances d'Été	Sommerurlaub	Férias de Verão	Летние каникулы	Kỳ Nghỉ Hè	Liburan Musim Panas	วันหยุดฤดูร้อน
왼쪽부터 순서대로 끝까지 실행하세요 · {0}	Do these in order, left to right · {0}	左から順に最後まで実行してください · {0}	从左到右按顺序做完 · {0}	從左到右按順序做完 · {0}	從左到右按順序做完 · {0}	Hazlos en orden, de izquierda a derecha · {0}	Faites-les dans l'ordre, de gauche à droite · {0}	Der Reihe nach von links nach rechts ausführen · {0}	Faça na ordem, da esquerda para a direita · {0}	Выполняйте по порядку, слева направо · {0}	Làm lần lượt từ trái sang phải · {0}	Lakukan berurutan dari kiri ke kanan · {0}	ทำตามลำดับจากซ้ายไปขวา · {0}
중간에 흐트러져 보여도 이 수식을 끝까지 계속하면 됩니다.	It may look scrambled partway through — just finish the sequence.	途中で崩れて見えても、この手順を最後まで続ければ大丈夫です。	中途看起来乱了也没关系，把这串公式做完即可。	中途看起來亂了也沒關係，把這串公式做完即可。	中途看起來亂了也沒關係，把這串公式做完即可。	Puede parecer desordenado a mitad de camino: termina la secuencia.	Cela peut sembler mélangé en cours de route : terminez la séquence.	Zwischendurch sieht es verdreht aus — führe die Folge einfach zu Ende.	Pode parecer embaralhado no meio do caminho: termine a sequência.	В середине может показаться, что всё сбилось — просто доведите последовательность до конца.	Giữa chừng có thể trông lộn xộn — cứ làm hết chuỗi này là được.	Di tengah jalan bisa terlihat berantakan — selesaikan saja urutannya.	ระหว่างทางอาจดูรวน แค่ทำสูตรนี้ให้จบก็พอ
안내와 다른 동작이 들어왔어요. 현재 상태에서 힌트를 다시 눌러 주세요.	That wasn't the move we suggested. Tap Hint again for the current state.	案内と違う操作が入りました。今の状態でヒントをもう一度押してください。	刚才的操作和提示不同。请在当前状态下再按一次提示。	剛才的操作和提示不同。請在目前狀態下再按一次提示。	剛才的操作和提示不同。請在目前狀態下再按一次提示。	Ese no era el movimiento sugerido. Toca Pista otra vez para el estado actual.	Ce n'était pas le mouvement proposé. Touchez Indice à nouveau pour l'état actuel.	Das war nicht der vorgeschlagene Zug. Tippe erneut auf Hinweis für den aktuellen Stand.	Esse não foi o movimento sugerido. Toque em Dica de novo para o estado atual.	Это не тот ход, который мы предложили. Нажмите «Подсказка» ещё раз для текущего состояния.	Đó không phải nước đi được gợi ý. Hãy nhấn Gợi ý lại cho trạng thái hiện tại.	Itu bukan gerakan yang disarankan. Ketuk Petunjuk lagi untuk kondisi sekarang.	ท่านั้นไม่ตรงกับที่แนะนำ แตะคำใบ้อีกครั้งสำหรับสถานะปัจจุบัน
여기까지 잘 따라왔어요. 힌트를 눌러 다음 동작을 확인하세요.	Nicely done so far. Tap Hint for the next move.	ここまでよくできました。ヒントを押して次の操作を確認しましょう。	到这里做得很好。点击提示查看下一步。	到這裡做得很好。點按提示查看下一步。	到這裡做得很好。點按提示查看下一步。	Bien hecho hasta aquí. Toca Pista para el siguiente movimiento.	Bien joué jusqu'ici. Touchez Indice pour le mouvement suivant.	Bis hierher gut gemacht. Tippe auf Hinweis für den nächsten Zug.	Muito bem até aqui. Toque em Dica para o próximo movimento.	Пока всё верно. Нажмите «Подсказка» для следующего хода.	Đến đây rất tốt. Nhấn Gợi ý để xem bước tiếp theo.	Sejauh ini bagus. Ketuk Petunjuk untuk gerakan berikutnya.	มาถึงตรงนี้ทำได้ดี แตะคำใบ้เพื่อดูท่าถัดไป
힌트는 3×3에서만 됩니다.	Hints are available for the 3×3 only.	ヒントは3×3のみ対応です。	提示仅支持 3×3。	提示僅支援 3×3。	提示淨係支援 3×3。	Las pistas solo están disponibles para el 3×3.	Les indices ne sont disponibles que pour le 3×3.	Hinweise gibt es nur für den 3×3.	As dicas estão disponíveis apenas para o 3×3.	Подсказки доступны только для 3×3.	Gợi ý chỉ có cho khối 3×3.	Petunjuk hanya tersedia untuk 3×3.	คำใบ้ใช้ได้เฉพาะ 3×3
현재 맞추던 상태는 새 스크램블로 바뀝니다.	Your current progress will be replaced by a new scramble.	今揃えている状態は新しいスクランブルに置き換わります。	当前进度会被新的打乱替换。	目前進度會被新的打亂取代。	目前進度會被新的打亂取代。	Tu progreso actual se sustituirá por una nueva mezcla.	Votre progression actuelle sera remplacée par un nouveau mélange.	Dein aktueller Fortschritt wird durch eine neue Mischung ersetzt.	Seu progresso atual será substituído por um novo embaralhamento.	Текущий прогресс заменится новым перемешиванием.	Tiến trình hiện tại sẽ được thay bằng một lần trộn mới.	Progres saat ini akan diganti dengan acakan baru.	ความคืบหน้าปัจจุบันจะถูกแทนที่ด้วยการสับใหม่
처음 상태로 돌릴까요?	Reset to the starting state?	最初の状態に戻しますか？	要恢复到初始状态吗？	要恢復到初始狀態嗎？	要回復到初始狀態嗎？	¿Volver al estado inicial?	Revenir à l'état initial ?	Auf den Ausgangszustand zurücksetzen?	Voltar ao estado inicial?	Вернуть в исходное состояние?	Đưa về trạng thái ban đầu?	Kembalikan ke kondisi awal?	ย้อนกลับสู่สถานะเริ่มต้นไหม?
현재 맞추던 큐브 상태와 진행 기록이 지워집니다.	Your current cube state and progress will be cleared.	今のキューブの状態と進行状況が消えます。	当前魔方状态和进度将被清除。	目前魔方狀態和進度將被清除。	目前魔方狀態同進度會被清除。	Se borrarán el estado actual del cubo y tu progreso.	L'état actuel du cube et votre progression seront effacés.	Der aktuelle Würfelzustand und dein Fortschritt werden gelöscht.	O estado atual do cubo e seu progresso serão apagados.	Текущее состояние кубика и прогресс будут стёрты.	Trạng thái khối hiện tại và tiến trình sẽ bị xóa.	Kondisi kubus saat ini dan progresnya akan dihapus.	สถานะลูกบาศก์ปัจจุบันและความคืบหน้าจะถูกล้าง
저장한 연습 상태를 이어서 시작합니다.	Continuing from your saved practice state.	保存した練習状態から続けます。	从已保存的练习状态继续。	從已儲存的練習狀態繼續。	從已儲存的練習狀態繼續。	Continuando desde tu práctica guardada.	Reprise à partir de votre entraînement enregistré.	Weiter mit deinem gespeicherten Übungsstand.	Continuando do seu treino salvo.	Продолжаем с сохранённого состояния тренировки.	Tiếp tục từ trạng thái luyện tập đã lưu.	Melanjutkan dari kondisi latihan yang tersimpan.	ทำต่อจากสถานะฝึกที่บันทึกไว้
큐브를 다 맞췄습니다.	You solved the cube!	キューブが揃いました！	魔方还原完成！	魔方還原完成！	魔方還原完成！	¡Has resuelto el cubo!	Vous avez résolu le cube !	Du hast den Würfel gelöst!	Você resolveu o cubo!	Кубик собран!	Bạn đã giải xong khối!	Kamu berhasil menyelesaikan kubus!	คุณแก้ลูกบาศก์สำเร็จแล้ว!
흰색이 들어간 두 색 조각을 찾으세요. 흰색을 아래로 보내고, 옆 색은 같은 색 센터에 맞춥니다. 연습하기를 누르면 현재 상태의 조작도 알려드려요.	Find a two-color edge containing white. Send the white side down and match the other color to its center. Tap Practice and we'll guide you from the current state.	白を含む2色の辺を探します。白を下に向け、もう一方の色を同じ色の中央に合わせます。「練習する」を押すと今の状態に合わせて案内します。	找到带白色的双色棱块。把白色朝下，另一种颜色对准同色中心块。点击“练习”，我们会根据当前状态指导你。	找到帶白色的雙色邊塊。把白色朝下，另一種顏色對準同色中心塊。點按「練習」，我們會依目前狀態指導你。	找到帶白色的雙色邊塊。把白色朝下，另一種顏色對準同色中心塊。點按「練習」，我們會依目前狀態指導你。	Busca una arista de dos colores con blanco. Pon el blanco hacia abajo y haz coincidir el otro color con su centro. Toca Practicar y te guiamos desde el estado actual.	Trouvez une arête bicolore contenant du blanc. Placez le blanc vers le bas et faites correspondre l'autre couleur à son centre. Touchez S'entraîner et nous vous guiderons depuis l'état actuel.	Suche eine zweifarbige Kante mit Weiß. Weiß nach unten drehen und die andere Farbe zu ihrem Mittelstein ausrichten. Tippe auf Üben — wir führen dich vom aktuellen Stand aus.	Ache uma aresta de duas cores com branco. Deixe o branco para baixo e case a outra cor com o centro dela. Toque em Praticar e guiamos você a partir do estado atual.	Найдите двухцветное ребро с белым. Поверните белым вниз, а второй цвет совместите с его центром. Нажмите «Практика» — подскажем, исходя из текущего состояния.	Tìm cạnh hai màu có màu trắng. Đưa mặt trắng xuống dưới và khớp màu còn lại với ô tâm cùng màu. Nhấn Luyện tập, chúng tôi sẽ hướng dẫn từ trạng thái hiện tại.	Cari tepi dua warna yang memuat putih. Arahkan putih ke bawah dan cocokkan warna satunya dengan kotak tengah sewarna. Ketuk Latihan, kami pandu dari kondisi sekarang.	หาขอบสองสีที่มีสีขาว หันสีขาวลงล่าง แล้วจับอีกสีให้ตรงกับช่องกลางสีเดียวกัน แตะฝึก แล้วเราจะแนะนำจากสถานะปัจจุบัน
흰색이 들어간 두 색 조각을 찾으세요. 힌트를 누르면 다음 조작을 알려드려요.	Find a two-color edge containing white. Tap Hint for the next move.	白を含む2色の辺を探します。ヒントを押すと次の操作を教えます。	找到带白色的双色棱块。点击提示查看下一步。	找到帶白色的雙色邊塊。點按提示查看下一步。	找到帶白色的雙色邊塊。點按提示查看下一步。	Busca una arista de dos colores con blanco. Toca Pista para el siguiente movimiento.	Trouvez une arête bicolore contenant du blanc. Touchez Indice pour le mouvement suivant.	Suche eine zweifarbige Kante mit Weiß. Tippe auf Hinweis für den nächsten Zug.	Ache uma aresta de duas cores com branco. Toque em Dica para o próximo movimento.	Найдите двухцветное ребро с белым. Нажмите «Подсказка» для следующего хода.	Tìm cạnh hai màu có màu trắng. Nhấn Gợi ý để xem bước tiếp theo.	Cari tepi dua warna yang memuat putih. Ketuk Petunjuk untuk gerakan berikutnya.	หาขอบสองสีที่มีสีขาว แตะคำใบ้เพื่อดูท่าถัดไป
흰색 모서리를 들어갈 자리 위에 놓으세요. 막히면 힌트에서 조작 순서를 확인하세요.	Put the white corner above the slot it belongs in. If you get stuck, check the move order in Hint.	白い角を入る場所の真上に置きます。詰まったらヒントで操作順を確認しましょう。	把白色角块放到它该去的位置正上方。卡住时可在提示中查看操作顺序。	把白色角塊放到它該去的位置正上方。卡住時可在提示中查看操作順序。	把白色角塊放到它該去的位置正上方。卡住時可在提示中查看操作順序。	Coloca la esquina blanca justo encima de su hueco. Si te atascas, mira el orden en Pista.	Placez le coin blanc juste au-dessus de son emplacement. Si vous bloquez, consultez l'ordre dans Indice.	Lege die weiße Ecke genau über ihren Platz. Wenn du feststeckst, sieh dir die Zugfolge im Hinweis an.	Ponha o canto branco logo acima do lugar dele. Se travar, veja a ordem dos movimentos na Dica.	Поставьте белый угол прямо над его местом. Если застряли, посмотрите порядок ходов в подсказке.	Đặt góc trắng ngay phía trên vị trí của nó. Nếu bí, xem thứ tự thao tác trong Gợi ý.	Letakkan sudut putih tepat di atas tempatnya. Jika buntu, lihat urutan gerakan di Petunjuk.	วางมุมสีขาวไว้เหนือช่องที่มันต้องลง หากติด ให้ดูลำดับท่าในคำใบ้
위층에서 노란색 없는 조각을 찾으세요. 힌트가 오른쪽·왼쪽 공식을 골라드려요.	Find a top-layer edge without yellow. Hint will pick the right or left algorithm for you.	上段で黄色を含まない辺を探します。ヒントが右・左どちらの手順かを選びます。	在顶层找一个不含黄色的棱块。提示会替你挑选向右或向左的公式。	在頂層找一個不含黃色的邊塊。提示會替你挑選向右或向左的公式。	在頂層找一個不含黃色的邊塊。提示會替你挑選向右或向左的公式。	Busca una arista de arriba sin amarillo. La pista elegirá el algoritmo derecho o izquierdo.	Trouvez une arête du haut sans jaune. L'indice choisira l'algorithme droit ou gauche.	Suche oben eine Kante ohne Gelb. Der Hinweis wählt den rechten oder linken Algorithmus.	Ache uma aresta de cima sem amarelo. A dica escolhe o algoritmo da direita ou da esquerda.	Найдите вверху ребро без жёлтого. Подсказка выберет правый или левый алгоритм.	Tìm cạnh tầng trên không có màu vàng. Gợi ý sẽ chọn công thức phải hoặc trái cho bạn.	Cari tepi lapisan atas tanpa kuning. Petunjuk akan memilihkan algoritma kanan atau kiri.	หาขอบชั้นบนที่ไม่มีสีเหลือง คำใบ้จะเลือกสูตรขวาหรือซ้ายให้
위 면 모양을 확인한 뒤 공식을 따라 하세요. 막히면 힌트에서 현재 상태의 조작을 확인하세요.	Check the shape on top, then follow the algorithm. If you get stuck, Hint shows the move for your current state.	上面の形を確認してから手順を行います。詰まったらヒントで今の状態の操作を確認しましょう。	先看清顶面的形状再照公式做。卡住时可在提示中查看当前状态的操作。	先看清頂面的形狀再照公式做。卡住時可在提示中查看目前狀態的操作。	先看清頂面的形狀再照公式做。卡住時可在提示中查看目前狀態的操作。	Comprueba la forma de arriba y luego sigue el algoritmo. Si te atascas, la pista muestra el movimiento para tu estado.	Vérifiez la forme du dessus puis suivez l'algorithme. Si vous bloquez, l'indice montre le mouvement adapté.	Sieh dir die Form oben an und führe dann den Algorithmus aus. Wenn du feststeckst, zeigt der Hinweis den passenden Zug.	Confira a forma no topo e siga o algoritmo. Se travar, a dica mostra o movimento para o seu estado.	Посмотрите на фигуру сверху и выполните алгоритм. Если застряли, подсказка покажет нужный ход.	Xem hình ở mặt trên rồi làm theo công thức. Nếu bí, Gợi ý sẽ chỉ thao tác cho trạng thái hiện tại.	Periksa bentuk di sisi atas lalu ikuti algoritmanya. Jika buntu, Petunjuk menunjukkan gerakan untuk kondisimu.	ดูรูปด้านบนแล้วทำตามสูตร หากติด คำใบ้จะบอกท่าสำหรับสถานะปัจจุบัน
{0} · {1}\n{2} · 연습 중에는 현재 상태에 맞는 조작을 알려드려요.	{0} · {1}\n{2} · While practising, we'll show the move for your current state.	{0} · {1}\n{2} · 練習中は今の状態に合わせた操作を案内します。	{0} · {1}\n{2} · 练习时会根据当前状态提示操作。	{0} · {1}\n{2} · 練習時會依目前狀態提示操作。	{0} · {1}\n{2} · 練習時會按目前狀態提示操作。	{0} · {1}\n{2} · Durante la práctica te mostramos el movimiento para tu estado.	{0} · {1}\n{2} · Pendant l'entraînement, nous montrons le mouvement adapté.	{0} · {1}\n{2} · Beim Üben zeigen wir den Zug für deinen aktuellen Stand.	{0} · {1}\n{2} · Durante a prática, mostramos o movimento para o seu estado.	{0} · {1}\n{2} · Во время практики покажем ход для текущего состояния.	{0} · {1}\n{2} · Khi luyện tập, chúng tôi sẽ chỉ thao tác cho trạng thái hiện tại.	{0} · {1}\n{2} · Saat berlatih, kami tunjukkan gerakan untuk kondisimu.	{0} · {1}\n{2} · ระหว่างฝึก เราจะบอกท่าสำหรับสถานะปัจจุบัน
② 앞면에서 윗면을 카메라 쪽으로	② From the front, tilt the top face toward the camera	② 前面から上面をカメラ側へ	② 从前面把顶面转向相机	② 從前面把頂面轉向相機	② 由前面將頂面轉向相機	② Desde el frente, gira la cara superior hacia la cámara	② Depuis l'avant, basculez la face du haut vers la caméra	② Von vorn die obere Seite zur Kamera kippen	② Da frente, incline a face de cima para a câmera	② От передней грани поверните верх к камере	② Từ mặt trước, nghiêng mặt trên về phía camera	② Dari depan, miringkan sisi atas ke arah kamera	② จากด้านหน้า เอียงด้านบนเข้าหากล้อง
③ 앞면에서 아랫면을 카메라 쪽으로	③ From the front, tilt the bottom face toward the camera	③ 前面から下面をカメラ側へ	③ 从前面把底面转向相机	③ 從前面把底面轉向相機	③ 由前面將底面轉向相機	③ Desde el frente, gira la cara inferior hacia la cámara	③ Depuis l'avant, basculez la face du bas vers la caméra	③ Von vorn die untere Seite zur Kamera kippen	③ Da frente, incline a face de baixo para a câmera	③ От передней грани поверните низ к камере	③ Từ mặt trước, nghiêng mặt dưới về phía camera	③ Dari depan, miringkan sisi bawah ke arah kamera	③ จากด้านหน้า เอียงด้านล่างเข้าหากล้อง
④ 앞면에서 왼쪽 면을 카메라 쪽으로	④ From the front, turn the left face toward the camera	④ 前面から左面をカメラ側へ	④ 从前面把左面转向相机	④ 從前面把左面轉向相機	④ 由前面將左面轉向相機	④ Desde el frente, gira la cara izquierda hacia la cámara	④ Depuis l'avant, tournez la face gauche vers la caméra	④ Von vorn die linke Seite zur Kamera drehen	④ Da frente, vire a face esquerda para a câmera	④ От передней грани поверните левую грань к камере	④ Từ mặt trước, xoay mặt trái về phía camera	④ Dari depan, putar sisi kiri ke arah kamera	④ จากด้านหน้า หมุนด้านซ้ายเข้าหากล้อง
⑤ 앞면에서 오른쪽 면을 카메라 쪽으로	⑤ From the front, turn the right face toward the camera	⑤ 前面から右面をカメラ側へ	⑤ 从前面把右面转向相机	⑤ 從前面把右面轉向相機	⑤ 由前面將右面轉向相機	⑤ Desde el frente, gira la cara derecha hacia la cámara	⑤ Depuis l'avant, tournez la face droite vers la caméra	⑤ Von vorn die rechte Seite zur Kamera drehen	⑤ Da frente, vire a face direita para a câmera	⑤ От передней грани поверните правую грань к камере	⑤ Từ mặt trước, xoay mặt phải về phía camera	⑤ Dari depan, putar sisi kanan ke arah kamera	⑤ จากด้านหน้า หมุนด้านขวาเข้าหากล้อง
⑥ 노란색을 위로 둔 채 뒤로 180°	⑥ Keep yellow on top and turn 180° to the back	⑥ 黄色を上にしたまま後ろへ180°	⑥ 保持黄色朝上，向后转180°	⑥ 保持黃色朝上，向後轉180°	⑥ 保持黃色向上，向後轉180°	⑥ Con el amarillo arriba, gira 180° hacia atrás	⑥ En gardant le jaune en haut, tournez de 180° vers l'arrière	⑥ Gelb oben lassen und 180° nach hinten drehen	⑥ Com o amarelo em cima, gire 180° para trás	⑥ Оставив жёлтый сверху, поверните на 180° назад	⑥ Giữ màu vàng ở trên và xoay 180° ra sau	⑥ Biarkan kuning di atas lalu putar 180° ke belakang	⑥ ให้สีเหลืองอยู่ด้านบน แล้วหมุนไปด้านหลัง 180°
위 파랑 · 아래 초록 · 왼쪽 빨강 · 오른쪽 주황	Top blue · bottom green · left red · right orange	上 青 · 下 緑 · 左 赤 · 右 オレンジ	上蓝 · 下绿 · 左红 · 右橙	上藍 · 下綠 · 左紅 · 右橙	上藍 · 下綠 · 左紅 · 右橙	Arriba azul · abajo verde · izquierda rojo · derecha naranja	Haut bleu · bas vert · gauche rouge · droite orange	Oben Blau · unten Grün · links Rot · rechts Orange	Cima azul · baixo verde · esquerda vermelho · direita laranja	Сверху синий · снизу зелёный · слева красный · справа оранжевый	Trên xanh dương · dưới xanh lá · trái đỏ · phải cam	Atas biru · bawah hijau · kiri merah · kanan oranye	บนน้ำเงิน · ล่างเขียว · ซ้ายแดง · ขวาส้ม
위 초록 · 아래 파랑 · 왼쪽 빨강 · 오른쪽 주황	Top green · bottom blue · left red · right orange	上 緑 · 下 青 · 左 赤 · 右 オレンジ	上绿 · 下蓝 · 左红 · 右橙	上綠 · 下藍 · 左紅 · 右橙	上綠 · 下藍 · 左紅 · 右橙	Arriba verde · abajo azul · izquierda rojo · derecha naranja	Haut vert · bas bleu · gauche rouge · droite orange	Oben Grün · unten Blau · links Rot · rechts Orange	Cima verde · baixo azul · esquerda vermelho · direita laranja	Сверху зелёный · снизу синий · слева красный · справа оранжевый	Trên xanh lá · dưới xanh dương · trái đỏ · phải cam	Atas hijau · bawah biru · kiri merah · kanan oranye	บนเขียว · ล่างน้ำเงิน · ซ้ายแดง · ขวาส้ม
위 노랑 · 아래 흰색 · 왼쪽 초록 · 오른쪽 파랑	Top yellow · bottom white · left green · right blue	上 黄 · 下 白 · 左 緑 · 右 青	上黄 · 下白 · 左绿 · 右蓝	上黃 · 下白 · 左綠 · 右藍	上黃 · 下白 · 左綠 · 右藍	Arriba amarillo · abajo blanco · izquierda verde · derecha azul	Haut jaune · bas blanc · gauche vert · droite bleu	Oben Gelb · unten Weiß · links Grün · rechts Blau	Cima amarelo · baixo branco · esquerda verde · direita azul	Сверху жёлтый · снизу белый · слева зелёный · справа синий	Trên vàng · dưới trắng · trái xanh lá · phải xanh dương	Atas kuning · bawah putih · kiri hijau · kanan biru	บนเหลือง · ล่างขาว · ซ้ายเขียว · ขวาน้ำเงิน
위 노랑 · 아래 흰색 · 왼쪽 파랑 · 오른쪽 초록	Top yellow · bottom white · left blue · right green	上 黄 · 下 白 · 左 青 · 右 緑	上黄 · 下白 · 左蓝 · 右绿	上黃 · 下白 · 左藍 · 右綠	上黃 · 下白 · 左藍 · 右綠	Arriba amarillo · abajo blanco · izquierda azul · derecha verde	Haut jaune · bas blanc · gauche bleu · droite vert	Oben Gelb · unten Weiß · links Blau · rechts Grün	Cima amarelo · baixo branco · esquerda azul · direita verde	Сверху жёлтый · снизу белый · слева синий · справа зелёный	Trên vàng · dưới trắng · trái xanh dương · phải xanh lá	Atas kuning · bawah putih · kiri biru · kanan hijau	บนเหลือง · ล่างขาว · ซ้ายน้ำเงิน · ขวาเขียว
위 노랑 · 아래 흰색 · 왼쪽 주황 · 오른쪽 빨강	Top yellow · bottom white · left orange · right red	上 黄 · 下 白 · 左 オレンジ · 右 赤	上黄 · 下白 · 左橙 · 右红	上黃 · 下白 · 左橙 · 右紅	上黃 · 下白 · 左橙 · 右紅	Arriba amarillo · abajo blanco · izquierda naranja · derecha rojo	Haut jaune · bas blanc · gauche orange · droite rouge	Oben Gelb · unten Weiß · links Orange · rechts Rot	Cima amarelo · baixo branco · esquerda laranja · direita vermelho	Сверху жёлтый · снизу белый · слева оранжевый · справа красный	Trên vàng · dưới trắng · trái cam · phải đỏ	Atas kuning · bawah putih · kiri oranye · kanan merah	บนเหลือง · ล่างขาว · ซ้ายส้ม · ขวาแดง
이 면 다시 촬영	Rescan this face	この面を撮り直す	重新拍摄此面	重新拍攝此面	重新拍攝呢一面	Volver a escanear esta cara	Rescanner cette face	Diese Seite erneut scannen	Escanear esta face de novo	Снять эту грань заново	Quét lại mặt này	Pindai ulang sisi ini	สแกนด้านนี้ใหม่
카메라 권한이 꺼져 있습니다. 휴대폰 설정에서 직접 허용해 주세요.	Camera permission is off. Please allow it in your phone settings.	カメラの権限がオフです。端末の設定で許可してください。	相机权限已关闭。请在手机设置中开启。	相機權限已關閉。請在手機設定中開啟。	相機權限已關閉。請在手機設定中開啟。	El permiso de cámara está desactivado. Actívalo en los ajustes del teléfono.	L'autorisation caméra est désactivée. Activez-la dans les réglages du téléphone.	Die Kameraberechtigung ist aus. Bitte in den Telefoneinstellungen erlauben.	A permissão da câmera está desativada. Ative nas configurações do telefone.	Доступ к камере выключен. Разрешите его в настройках телефона.	Quyền camera đang tắt. Hãy bật trong cài đặt điện thoại.	Izin kamera nonaktif. Aktifkan di pengaturan ponsel.	สิทธิ์กล้องถูกปิดอยู่ กรุณาอนุญาตในการตั้งค่าโทรศัพท์
카메라 촬영은 연결된 휴대폰에서 사용할 수 있습니다.	Camera scanning is available on a phone with a camera.	カメラ撮影はカメラのある端末で使えます。	相机拍摄需要在带相机的手机上使用。	相機拍攝需要在帶相機的手機上使用。	相機拍攝需要在帶相機的手機上使用。	El escaneo con cámara está disponible en un teléfono con cámara.	Le scan par caméra est disponible sur un téléphone équipé d'une caméra.	Das Scannen mit der Kamera funktioniert auf einem Telefon mit Kamera.	A digitalização por câmera está disponível em um telefone com câmera.	Съёмка камерой доступна на телефоне с камерой.	Quét bằng camera chỉ dùng được trên điện thoại có camera.	Pemindaian kamera tersedia di ponsel yang punya kamera.	การสแกนด้วยกล้องใช้ได้บนโทรศัพท์ที่มีกล้อง
아래 면에 흰 십자를 만들 조각을 위층으로 올린 뒤 자리를 맞추고 내리세요.	Bring an edge for the white cross to the top layer, line it up, then drop it down.	下面の白い十字を作る辺を上段に上げ、位置を合わせてから下ろします。	把用于底面白十字的棱块转到顶层，对准位置后放下去。	把用於底面白十字的邊塊轉到頂層，對準位置後放下去。	把用於底面白十字的邊塊轉到頂層，對準位置後放下去。	Sube al nivel superior una arista para la cruz blanca, alinéala y bájala.	Montez en haut une arête de la croix blanche, alignez-la, puis descendez-la.	Bringe eine Kante fürs weiße Kreuz nach oben, richte sie aus und setze sie ab.	Leve para cima uma aresta da cruz branca, alinhe e desça.	Поднимите наверх ребро для белого креста, совместите и опустите.	Đưa cạnh dùng cho dấu cộng trắng lên tầng trên, canh đúng rồi hạ xuống.	Naikkan tepi untuk tanda plus putih ke lapisan atas, sejajarkan, lalu turunkan.	ยกขอบที่ใช้ทำกากบาทขาวขึ้นชั้นบน จัดให้ตรงแล้วค่อยลง
흰색이 들어간 모서리를 위층으로 빼낸 뒤 들어갈 자리 위에 놓고 공식을 쓰세요.	Lift a corner containing white to the top, place it above its slot, then run the algorithm.	白を含む角を上段に出し、入る場所の真上に置いてから手順を使います。	把带白色的角块转到顶层，放到它该去的位置正上方，再用公式。	把帶白色的角塊轉到頂層，放到它該去的位置正上方，再用公式。	把帶白色的角塊轉到頂層，放到它該去的位置正上方，再用公式。	Saca al nivel superior una esquina con blanco, ponla sobre su hueco y aplica el algoritmo.	Faites remonter un coin contenant du blanc, placez-le au-dessus de son emplacement, puis appliquez l'algorithme.	Hole eine Ecke mit Weiß nach oben, stelle sie über ihren Platz und führe den Algorithmus aus.	Leve para cima um canto com branco, ponha sobre o lugar dele e aplique o algoritmo.	Выведите наверх угол с белым, поставьте над его местом и выполните алгоритм.	Đưa góc có màu trắng lên tầng trên, đặt ngay trên vị trí của nó rồi dùng công thức.	Angkat sudut yang memuat putih ke atas, letakkan di atas slotnya, lalu jalankan algoritma.	ยกมุมที่มีสีขาวขึ้นชั้นบน วางไว้เหนือช่องของมัน แล้วใช้สูตร
노란색이 없는 조각을 위층에서 찾아 앞면 색을 맞춘 뒤 공식을 쓰세요.	Find a top-layer edge without yellow, match its front color, then run the algorithm.	上段で黄色を含まない辺を探し、手前の色を合わせてから手順を使います。	在顶层找不含黄色的棱块，对准正面颜色后再用公式。	在頂層找不含黃色的邊塊，對準正面顏色後再用公式。	在頂層找不含黃色的邊塊，對準正面顏色後再用公式。	Busca arriba una arista sin amarillo, cuadra su color frontal y aplica el algoritmo.	Trouvez en haut une arête sans jaune, alignez sa couleur avant, puis appliquez l'algorithme.	Suche oben eine Kante ohne Gelb, richte ihre Vorderfarbe aus und führe den Algorithmus aus.	Ache em cima uma aresta sem amarelo, alinhe a cor da frente e aplique o algoritmo.	Найдите вверху ребро без жёлтого, совместите его передний цвет и выполните алгоритм.	Tìm cạnh tầng trên không có màu vàng, khớp màu mặt trước rồi dùng công thức.	Cari tepi lapisan atas tanpa kuning, cocokkan warna depannya, lalu jalankan algoritma.	หาขอบชั้นบนที่ไม่มีสีเหลือง จัดสีด้านหน้าให้ตรง แล้วใช้สูตร
다음 단계 설명을 다시 읽어 보세요.	Try reading the next stage's explanation again.	次のステップの説明をもう一度読んでみましょう。	再读一遍下一步的说明。	再讀一遍下一步的說明。	再讀一遍下一步的說明。	Vuelve a leer la explicación de la siguiente etapa.	Relisez l'explication de l'étape suivante.	Lies die Erklärung der nächsten Stufe noch einmal.	Leia novamente a explicação da próxima etapa.	Перечитайте объяснение следующего этапа.	Hãy đọc lại phần giải thích của bước tiếp theo.	Coba baca lagi penjelasan tahap berikutnya.	ลองอ่านคำอธิบายของขั้นถัดไปอีกครั้ง
펴기	Expand	開く	展开	展開	展開	Mostrar	Ouvrir	Aufklappen	Expandir	Показать	Mở	Buka	ขยาย
접기	Collapse	閉じる	收起	收起	收起	Ocultar	Réduire	Zuklappen	Recolher	Скрыть	Thu gọn	Tutup	ย่อ
안내	Guide	案内	指南	指南	指南	Guía	Guide	Anleitung	Guia	Подсказка	Hướng dẫn	Panduan	คำแนะนำ
경로 다시 계산	Recalculating	経路を再計算	重新计算路线	重新計算路線	重新計算路線	Recalculando	Recalcul en cours	Neu berechnen	Recalculando	Пересчёт	Đang tính lại	Menghitung ulang	กำลังคำนวณใหม่
묶음 완료	Sequence done	まとめ完了	本组完成	本組完成	本組完成	Secuencia completada	Séquence terminée	Folge fertig	Sequência concluída	Серия выполнена	Xong chuỗi	Rangkaian selesai	จบชุดแล้ว
그림 공식 보기	View picture algorithm	絵柄の手順を見る	查看图案公式	查看圖案公式	查看圖案公式	Ver algoritmo de imagen	Voir l'algorithme d'image	Bild-Algorithmus ansehen	Ver algoritmo da imagem	Показать алгоритм рисунка	Xem công thức hình	Lihat algoritma gambar	ดูสูตรภาพ
학습 목록으로	Back to lessons	学習リストへ	返回学习列表	返回學習列表	返回學習列表	Volver a las lecciones	Retour aux leçons	Zurück zu den Lektionen	Voltar às lições	К списку уроков	Về danh sách bài học	Kembali ke pelajaran	กลับไปที่บทเรียน
색상 큐브 완성!	Colors solved!	色が揃いました！	颜色还原完成！	顏色還原完成！	顏色還原完成！	¡Colores resueltos!	Couleurs résolues !	Farben gelöst!	Cores resolvidas!	Цвета собраны!	Đã xong màu!	Warna selesai!	แก้สีครบแล้ว!
색상은 모두 맞았어요. 이미지 스킨은 그림의 위·아래·좌·우 방향까지 맞춰야 최종 완성입니다.	All the colors match. With a picture skin you also need to orient the image the right way up to finish.	色はすべて揃いました。絵柄スキンは絵の上下左右の向きまで合わせて完成です。	颜色已全部还原。图案皮肤还需把图片的上下左右方向也对齐才算完成。	顏色已全部還原。圖案外觀還需把圖片的上下左右方向也對齊才算完成。	顏色已全部還原。圖案外觀還需把圖片的上下左右方向也對齊才算完成。	Todos los colores coinciden. Con un diseño de imagen, además debes orientar el dibujo correctamente para terminar.	Toutes les couleurs correspondent. Avec un skin illustré, il faut encore orienter l'image dans le bon sens pour finir.	Alle Farben stimmen. Bei einem Bild-Skin muss zum Abschluss auch die Bildausrichtung passen.	Todas as cores batem. Com um skin de imagem, ainda é preciso orientar o desenho para concluir.	Все цвета совпали. У скина с рисунком нужно ещё развернуть рисунок правильной стороной вверх.	Tất cả màu đã khớp. Với giao diện hình, bạn còn phải chỉnh đúng hướng của hình mới hoàn tất.	Semua warna sudah cocok. Untuk skin bergambar, arah gambarnya juga harus benar agar selesai.	สีตรงกันครบแล้ว สำหรับสกินภาพ ต้องจัดทิศทางของภาพให้ถูกด้วยจึงจะเสร็จสมบูรณ์
같은 그림을 모든 조각에 반복해서 보여줘요	Shows the same image repeated on every sticker	同じ絵柄をすべてのマスに繰り返して表示します	在每一格上重复显示同一张图片	在每一格上重複顯示同一張圖片	在每一格上重複顯示同一張圖片	Muestra la misma imagen repetida en cada casilla	Affiche la même image répétée sur chaque case	Zeigt dasselbe Bild auf jedem Feld wiederholt	Mostra a mesma imagem repetida em cada adesivo	Показывает один и тот же рисунок на каждой клетке	Hiện cùng một hình lặp lại trên mọi ô	Menampilkan gambar yang sama berulang di tiap kotak	แสดงภาพเดียวกันซ้ำในทุกช่อง
카메라 권한이 필요합니다. 휴대폰 설정에서 권한을 허용해 주세요.	Camera permission is required. Please allow it in your phone settings.	カメラの権限が必要です。端末の設定で許可してください。	需要相机权限。请在手机设置中允许。	需要相機權限。請在手機設定中允許。	需要相機權限。請在手機設定中允許。	Se necesita permiso de cámara. Actívalo en los ajustes del teléfono.	L'autorisation caméra est nécessaire. Activez-la dans les réglages du téléphone.	Die Kameraberechtigung wird benötigt. Bitte in den Telefoneinstellungen erlauben.	É necessária a permissão da câmera. Permita nas configurações do telefone.	Требуется доступ к камере. Разрешите его в настройках телефона.	Cần quyền camera. Hãy cho phép trong cài đặt điện thoại.	Izin kamera diperlukan. Aktifkan di pengaturan ponsel.	ต้องใช้สิทธิ์กล้อง กรุณาอนุญาตในการตั้งค่าโทรศัพท์
사용 가능한 카메라를 찾지 못했습니다.	No usable camera was found.	使用できるカメラが見つかりませんでした。	未找到可用的相机。	找不到可用的相機。	找不到可用的相機。	No se encontró ninguna cámara disponible.	Aucune caméra utilisable n'a été trouvée.	Es wurde keine nutzbare Kamera gefunden.	Nenhuma câmera utilizável foi encontrada.	Не найдено доступной камеры.	Không tìm thấy camera khả dụng.	Tidak ditemukan kamera yang bisa dipakai.	ไม่พบกล้องที่ใช้งานได้";

        // Detailed coaching copy falls back to English when a locale-specific sentence is
        // not available yet. This keeps every supported locale usable without exposing keys.
        const string EnglishFallbackCatalog = @"큐브를 흰 면이 아래로 가게 잡습니다. 앞으로 이 방향을 계속 유지합니다.	Hold the cube with white on the bottom and keep this orientation.
아래 면에 흰색 십자를 만듭니다. 십자를 이루는 네 조각은 두 가지 색을 갖고 있습니다. 흰색은 아래를 향하고, 나머지 색은 옆면 가운데 색과 맞아야 합니다.	Make a white cross on the bottom. Match each edge's side color with its center.
예를 들어 흰-초록 조각은 흰색이 아래, 초록색이 초록 가운데가 있는 면을 향하게 놓습니다.	For example, place the white-green edge with white down and green facing the green center.
이 단계는 공식 없이 눈으로 찾아 옮깁니다. 한 조각씩 위로 올린 뒤 자리를 맞추고 아래로 내리면 됩니다. 다른 조각을 망가뜨렸다면 되돌리고 다시 해보세요.	This step needs no algorithm. Move one edge at a time, align it, then bring it down.
십자를 만들었으면 이제 아래 층 네 모서리를 채웁니다.	After the cross, fill the four bottom-layer corners.
흰색이 들어간 모서리 조각을 찾아 위층으로 올리고, 그 조각이 들어갈 자리 바로 위에 오게 돌립니다.	Find a corner with white, bring it to the top, and place it above its target.
그 다음 아래 공식을 자리가 맞을 때까지 반복합니다. 한 번, 세 번, 또는 다섯 번 만에 들어갑니다.	Repeat the algorithm until the corner is inserted. It may take one, three, or five repeats.
공식이 아래 십자를 망가뜨리는 것처럼 보여도 괜찮습니다. 반복하면 제자리로 돌아옵니다.	It may look like the cross is breaking; keep repeating and it will return.
아래 두 줄이 끝났습니다. 이제 가운데 층의 네 조각을 채웁니다.	The bottom two rows are ready. Now fill the four middle-layer edges.
위층에서 노란색이 없는 조각을 찾습니다. 그 조각의 앞면 색이 앞면 가운데 색과 맞도록 위층을 돌립니다.	Find a top edge without yellow and align its front color with the front center.
조각이 오른쪽으로 내려가야 하면 첫 번째 공식, 왼쪽으로 내려가야 하면 두 번째 공식을 씁니다.	Use the first algorithm to insert right, or the second to insert left.
넣을 자리에 이미 엉뚱한 조각이 들어 있으면, 아무 공식이나 한 번 써서 그 조각을 위로 빼낸 뒤 다시 하세요.	If the wrong edge is in the slot, use either algorithm once to lift it out, then retry.
아래 두 층이 끝났습니다. 남은 건 맨 위층뿐입니다.	The bottom two layers are complete. Only the top layer remains.
위 면의 노란색 모양을 봅니다. 점 하나, 한 줄, 또는 ㄱ자 모양일 겁니다.	Look at the yellow shape on top: a dot, a line, or an L shape.
한 줄이면 그 줄이 좌우로 눕도록 돌리고, ㄱ자면 꺾인 부분이 왼쪽 위로 가게 돌린 뒤 공식을 씁니다.	Hold a line horizontally. Hold an L in the upper-left, then run the algorithm.
점 하나면 공식을 세 번까지 반복하면 십자가 생깁니다. 지금은 모서리 색이 맞지 않아도 괜찮습니다.	For a dot, repeat up to three times to make a cross. Side colors can wait.
십자가 생겼으니 위 면 전체를 노란색으로 채웁니다.	Now fill the entire top face with yellow.
노란색이 위를 향한 모서리가 몇 개인지 셉니다. 0개, 1개, 2개 중 하나입니다.	Count top-facing yellow corners: zero, one, or two.
1개라면 그 모서리를 왼쪽 아래에 두고 공식을 씁니다. 0개나 2개라면 아무 데서나 한 번 쓰고 다시 세어 보세요.	With one, place it at lower-left and run the algorithm. With zero or two, run it once and count again.
이 공식은 아래 두 층을 건드리지 않습니다. 위 면이 노랗게 될 때까지 반복하면 됩니다.	This preserves the bottom layers. Repeat until the top is yellow.
위 면이 노랗게 됐지만 옆면 색은 아직 어긋나 있습니다.	The top is yellow, but the side colors are not aligned yet.
네 모서리 중 이미 제자리에 있는 것이 있는지 찾습니다. 옆면 두 색이 각각 그 면 가운데 색과 맞으면 제자리입니다.	Find a corner already in its correct position by matching both side colors to their centers.
제자리인 모서리를 왼쪽 앞에 두고 공식을 씁니다. 제자리가 하나도 없으면 아무 데서나 한 번 쓰면 하나가 맞습니다.	Put a correct corner at front-left and run the algorithm. If none is correct, run it once from anywhere.
이 공식은 왼쪽 앞 모서리를 그대로 두고 나머지 셋을 돌립니다. 다 맞을 때까지 반복하세요.	This keeps the front-left corner and cycles the other three. Repeat until all match.
마지막입니다. 모서리는 다 맞았고 그 사이 조각들만 남았습니다.	Final step: the corners are correct and only the edges remain.
이미 맞은 면이 하나 있는지 찾습니다. 있으면 그 면을 뒤로 보냅니다.	Find a completed side and place it at the back.
공식을 쓰고, 다 맞지 않았으면 한 번 더 씁니다.	Run the algorithm; if needed, run it once more.
맞은 면이 하나도 없으면 아무 데서나 한 번 쓰면 하나가 생깁니다. 그 다음 다시 하세요.	If no side is complete, run it once from anywhere, then place the completed side at the back.
넣을 모서리를 오른쪽 위 앞에 두고, 들어갈 때까지 반복	Place the corner at front-top-right and repeat until inserted.
조각을 오른쪽 자리로 내릴 때	Use when inserting an edge to the right.
조각을 왼쪽 자리로 내릴 때	Use when inserting an edge to the left.
점·한 줄·ㄱ자 어느 경우든 이걸 반복	Repeat for a dot, line, or L shape.
노란 면을 채울 때까지 반복	Repeat until the top face is yellow.
제자리인 모서리를 왼쪽 앞에 두고 반복	Keep a correct corner at front-left and repeat.
맞은 면을 뒤로 보내고 사용	Place a completed side at the back before using.
배우기는 3×3부터 시작합니다. 3×3을 골라 주세요.	Lessons start with 3×3. Please select 3×3.
흰색 십자 조각을 하나씩 맞춰요	Solve the white cross one edge at a time.
힌트를 누르면 지금 상태에서 필요한 조작을 알려드려요	Tap Hint to see the moves needed for the current state.
힌트를 누르면 다음 동작을 설명해 드려요. 큐브는 자동으로 움직이지 않습니다.	Tap Hint to see the next moves. The cube will not move automatically.
섞기 버튼으로 시작 · 두 손가락으로 시점 조절	Tap Scramble to start · Use two fingers to adjust the view.
촬영한 실물 큐브를 이어서 풀고 있어요	Continuing the scanned real cube.
가운데 칸은 각 면의 기준색이라 고정되어 있습니다.	Center stickers are fixed because they define each face.
안내된 순서대로 여섯 면을 촬영합니다.	Scan all six faces in the guided order.
반사광을 피하고 격자에 맞춘 뒤 약 1초간 고정해 주세요.	Avoid glare, align the grid, and hold still for about one second.
방향이 섞이지 않도록 현재 파란 테두리의 면을 먼저 저장합니다.	Save the blue-outlined face first so orientations stay consistent.
카메라가 아직 준비되지 않았어요	The camera is not ready yet.
중심색 확인이 필요해요	Please check the center color.
색상 인식 완료	Color scan complete.
앞면부터 순서대로 촬영해 주세요	Scan each face in order, starting with the front.
카메라 권한이 필요합니다. 휴대폰 설정에서 권한을 허용해 주세요.	Camera permission is required. Allow it in your phone settings.
사용 가능한 카메라를 찾지 못했습니다.	No available camera was found.
같은 그림을 모든 조각에 반복해서 보여줘요	Repeat the same image on every sticker.
색상은 모두 맞았어요. 이미지 스킨은 그림의 위·아래·좌·우 방향까지 맞춰야 최종 완성입니다.	All colors are solved. For image skins, also align the image orientation.
색상 큐브 완성!	Colors solved!
그림 공식 보기	Show image algorithm
경로 다시 계산	Recalculate solution
묶음 완료	Sequence complete
안내	Guide
학습 목록으로	Back to lessons
새 기록을 기다리는 중	Waiting for your first record
첫 연습을 마치면 최고 기록과 평균이\n여기에 차곡차곡 쌓여요.	Finish a practice solve to start building your best times and averages here.
반시계	Counterclockwise
가운데 오른쪽	Middle layer · right
가운데 왼쪽	Middle layer · left
안티수네	Anti-Sune
티 공식	T permutation
첫 층 모서리를 아래로 넣을 때	Insert a first-layer corner
가운데 층 조각을 오른쪽으로	Insert a middle-layer edge to the right
가운데 층 조각을 왼쪽으로	Insert a middle-layer edge to the left
위 면에 십자를 만들 때	Make a cross on the top face
위 면을 노랗게 채울 때	Orient the yellow face
수네의 반대 방향	The reverse of Sune
위층 모서리 자리를 맞출 때	Position the top-layer corners
마지막 조각들을 맞출 때	Solve the final edges
자주 쓰는 마지막 층 공식	A common last-layer algorithm
공식 카드를 누르면 큐브에서 보여줍니다.	Tap an algorithm card to see it on the cube.
① 초록 앞면으로 기준 잡기	① Start with the green front face
위 노랑 · 아래 흰색 · 왼쪽 빨강 · 오른쪽 주황	Top yellow · bottom white · left red · right orange
아래에서 색을 고른 뒤 전개도의 칸을 누르면 바뀝니다.	Choose a color below, then tap a sticker in the net to change it.
클래식	Classic
톤다운	Muted
파스텔	Pastel
다크스틸	Dark Steel
비비드	Vivid
우드	Wood
말랑 친구들	Kawaii Pals
문라이트 리조트	Moonlight Resort
별빛 모험단	Starlight Crew
여름 바캉스	Summer Holiday
‘한 면 전체’에서는 색상을 모두 맞춘 뒤 그림의 위·아래·좌·우 방향을 맞추는 공식이 한 번 더 나옵니다.\n‘조각마다 반복’은 일반 큐브처럼 색상만 맞추면 끝나요.	Whole-face images need one final orientation algorithm after the colors are solved. Repeat-per-sticker skins finish when the colors are solved.
펴기	Expand
접기	Collapse";
    }

    /// Keeps runtime-assigned Text.text values localized without every screen needing a
    /// separate refresh hook. A changed value becomes the new Korean source on the next frame.
    public sealed class LocalizedText : MonoBehaviour
    {
        Text _text;
        string _source;
        string _rendered;

        public void Bind(string source)
        {
            _text = GetComponent<Text>();
            _source = source ?? "";
            Render();
        }

        void LateUpdate()
            => Refresh();

        public void Refresh()
        {
            if (_text == null) _text = GetComponent<Text>();
            if (_text != null && _text.text != _rendered)
            {
                _source = _text.text;
                Render();
            }
        }

        void Render()
        {
            if (_text == null) return;
            _rendered = LocalizationService.T(_source);
            _text.text = _rendered;
        }
    }
}
