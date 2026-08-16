using System;
using System.Collections.Generic;
using System.Globalization;
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

        static bool TryLookup(string code, string source, out string value)
        {
            value = null;
            return Tables.TryGetValue(code, out var table)
                && table.TryGetValue(source, out value)
                && !string.IsNullOrEmpty(value);
        }

        static string TranslatePatterns(string source, string code)
        {
            // Move notation, numbers and times are language-neutral. For previously unseen
            // Korean copy, English remains the safe, readable fallback instead of a broken key.
            if (!ContainsKorean(source)) return source;
            string value = source;
            if (code == "en")
            {
                value = value.Replace("단계 완료", " stages complete")
                    .Replace("단계를 먼저 마쳐 주세요.", "Please complete the previous stage first.")
                    .Replace("완료", "Complete").Replace("시작", "Start")
                    .Replace("열림", "Open").Replace("잠김", "Locked")
                    .Replace("수", " moves").Replace("개", " items")
                    .Replace("면", " faces");
            }
            return value;
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
연습 기록	Practice history	練習履歴	练习记录	練習記錄	練習記錄	Historial de práctica	Historique d'entraînement	Übungsverlauf	Histórico de prática	История практики	Lịch sử luyện tập	Riwayat latihan	ประวัติการฝึก
색상 · 캐릭터	Colors · Characters	色・キャラクター	颜色 · 角色	顏色 · 角色	顏色 · 角色	Colores · Personajes	Couleurs · Personnages	Farben · Figuren	Cores · Personagens	Цвета · Персонажи	Màu · Nhân vật	Warna · Karakter	สี · ตัวละคร
오늘도\n한 번 맞춰볼까요?	Ready to solve\na cube today?	今日も\n揃えてみよう！	今天也来\n还原一次吧！	今天也來\n還原一次吧！	今天也來\n還原一次吧！	¿Resolvemos\nun cubo hoy?	On résout\nun cube aujourd'hui ?	Heute einen\nWürfel lösen?	Vamos resolver\num cubo hoje?	Соберём кубик\nсегодня?	Hôm nay cùng\ngiải khối nhé?	Siap menyusun\nkubus hari ini?	วันนี้มา\nแก้ลูกบาศก์กันไหม?
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
흰 십자	White cross	白い十字	白色十字	白色十字	白色十字	Cruz blanca	Croix blanche	Weißes Kreuz	Cruz branca	Белый крест	Dấu cộng trắng	Salib putih	กากบาทสีขาว
첫 층 완성	First layer	一段目完成	完成第一层	完成第一層	完成第一層	Primera capa	Première couche	Erste Ebene	Primeira camada	Первый слой	Hoàn thành tầng đầu	Lapisan pertama	ชั้นแรก
가운데 층	Middle layer	中段	中间层	中間層	中間層	Capa central	Couche du milieu	Mittlere Ebene	Camada do meio	Средний слой	Tầng giữa	Lapisan tengah	ชั้นกลาง
노란 십자	Yellow cross	黄色い十字	黄色十字	黃色十字	黃色十字	Cruz amarilla	Croix jaune	Gelbes Kreuz	Cruz amarela	Жёлтый крест	Dấu cộng vàng	Salib kuning	กากบาทสีเหลือง
노란 면	Yellow face	黄色い面	黄色面	黃色面	黃色面	Cara amarilla	Face jaune	Gelbe Seite	Face amarela	Жёлтая грань	Mặt vàng	Sisi kuning	ด้านสีเหลือง
모서리 자리 맞추기	Position corners	角の位置合わせ	角块归位	角塊歸位	角塊歸位	Colocar esquinas	Placer les coins	Ecken positionieren	Posicionar cantos	Расставить углы	Đặt góc đúng chỗ	Posisikan sudut	จัดตำแหน่งมุม
마지막 조각	Final pieces	最後のピース	最后的色块	最後的色塊	最後的色塊	Piezas finales	Dernières pièces	Letzte Teile	Peças finais	Последние элементы	Mảnh cuối	Bagian terakhir	ชิ้นสุดท้าย
모서리 넣기	Insert corner	角を入れる	插入角块	插入角塊	插入角塊	Insertar esquina	Insérer un coin	Ecke einsetzen	Inserir canto	Вставить угол	Đưa góc vào	Masukkan sudut	ใส่มุม
오른쪽으로 넣기	Insert right	右に入れる	向右插入	向右插入	向右插入	Insertar a la derecha	Insérer à droite	Rechts einsetzen	Inserir à direita	Вставить вправо	Đưa sang phải	Masukkan ke kanan	ใส่ทางขวา
왼쪽으로 넣기	Insert left	左に入れる	向左插入	向左插入	向左插入	Insertar a la izquierda	Insérer à gauche	Links einsetzen	Inserir à esquerda	Вставить влево	Đưa sang trái	Masukkan ke kiri	ใส่ทางซ้าย
십자 만들기	Make the cross	十字を作る	制作十字	製作十字	製作十字	Formar la cruz	Former la croix	Kreuz bilden	Formar a cruz	Собрать крест	Tạo dấu cộng	Buat salib	สร้างกากบาท
수네	Sune	スーン	Sune	Sune	Sune	Sune	Sune	Sune	Sune	Сунэ	Sune	Sune	ซูน
모서리 돌리기	Cycle corners	角を入れ替える	轮换角块	輪換角塊	輪換角塊	Ciclar esquinas	Permuter les coins	Ecken tauschen	Ciclar cantos	Переставить углы	Đổi vị trí góc	Putar sudut	สลับมุม
조각 돌리기	Cycle edges	辺を入れ替える	轮换棱块	輪換邊塊	輪換邊塊	Ciclar aristas	Permuter les arêtes	Kanten tauschen	Ciclar arestas	Переставить рёбра	Đổi vị trí cạnh	Putar tepi	สลับขอบ";

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
학습 목록으로	Back to lessons";
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
