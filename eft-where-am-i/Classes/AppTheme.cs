using System;
using System.Drawing;
using System.Windows.Forms;

namespace eft_where_am_i.Classes
{
    /// <summary>
    /// WinForms 화면의 색을 한곳에서 관리합니다.
    ///
    /// html/styles.css 의 디자인 토큰과 같은 값을 씁니다.
    /// 한쪽만 바꾸면 웹 패널과 네이티브 창의 색이 어긋나므로, 색을 바꿀 때는 양쪽을 같이 고쳐주세요.
    /// </summary>
    public static class AppTheme
    {
        /// <summary>현재 설정이 다크 테마인지. (기본값이 다크입니다)</summary>
        public static bool IsDark =>
            !string.Equals(SettingsHandler.Instance.GetSettings().theme_mode, "light",
                StringComparison.OrdinalIgnoreCase);

        // ── 표면 ──────────────────────────────────────
        public static Color Background => IsDark ? FromHex("#0b0b10") : FromHex("#f4f4f8");
        public static Color Surface => IsDark ? FromHex("#181821") : FromHex("#ffffff");
        public static Color SurfaceAlt => IsDark ? FromHex("#20202c") : FromHex("#f4f4fa");
        public static Color SurfaceSunken => IsDark ? FromHex("#0d0d13") : FromHex("#ebebf2");

        // ── 텍스트 ────────────────────────────────────
        public static Color Text => IsDark ? FromHex("#d9d9e3") : FromHex("#24242e");
        public static Color TextStrong => IsDark ? FromHex("#f4f4f8") : FromHex("#101018");
        public static Color Muted => IsDark ? FromHex("#8b8ba3") : FromHex("#6a6a7d");

        // ── 선 ────────────────────────────────────────
        public static Color Border => IsDark ? FromHex("#242433") : FromHex("#dedee8");
        public static Color BorderStrong => IsDark ? FromHex("#33334a") : FromHex("#c9c9d8");

        // ── 강조 (바이올렛) ───────────────────────────
        public static Color Accent => IsDark ? FromHex("#8b6dff") : FromHex("#6d4ee0");
        public static Color AccentBright => IsDark ? FromHex("#a58bff") : FromHex("#7c5cff");
        public static Color AccentSoft => IsDark ? FromHex("#1e1a2e") : FromHex("#efecfd");
        public static Color AccentContrast => Color.White;

        // ── 컨트롤 ────────────────────────────────────
        public static Color ControlBg => IsDark ? FromHex("#262634") : FromHex("#ffffff");
        public static Color ControlHover => IsDark ? FromHex("#323246") : FromHex("#f0f0f6");
        public static Color ControlActive => IsDark ? FromHex("#3c3c54") : FromHex("#e6e6ef");

        public static Color GridSelection => IsDark ? FromHex("#332a55") : FromHex("#e2dbfb");

        /// <summary>일반 버튼에 테마를 입힙니다.</summary>
        public static void StyleButton(Button button, bool primary = false)
        {
            if (button == null) return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = primary ? Accent : ControlBg;
            button.ForeColor = primary ? AccentContrast : Text;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? Accent : BorderStrong;
            button.FlatAppearance.MouseOverBackColor = primary ? AccentBright : ControlHover;
            button.FlatAppearance.MouseDownBackColor = primary ? Accent : ControlActive;
            button.Cursor = Cursors.Hand;
        }

        /// <summary>사이드 메뉴처럼 배경에 녹아드는 버튼.</summary>
        public static void StyleNavButton(Button button, bool selected = false)
        {
            if (button == null) return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = selected ? AccentSoft : Surface;
            button.ForeColor = selected ? AccentBright : Text;
            button.FlatAppearance.BorderSize = selected ? 1 : 0;
            button.FlatAppearance.BorderColor = selected ? Accent : Surface;
            button.FlatAppearance.MouseOverBackColor = selected ? AccentSoft : ControlHover;
            button.FlatAppearance.MouseDownBackColor = ControlActive;
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleCenter;
        }

        public static void StyleCheckBox(CheckBox checkBox)
        {
            if (checkBox == null) return;

            checkBox.FlatStyle = FlatStyle.Flat;
            checkBox.BackColor = SurfaceAlt;
            checkBox.ForeColor = Text;
            checkBox.FlatAppearance.BorderSize = 1;
            checkBox.FlatAppearance.BorderColor = BorderStrong;
            checkBox.FlatAppearance.MouseOverBackColor = ControlHover;
            checkBox.FlatAppearance.MouseDownBackColor = ControlActive;
            checkBox.Cursor = Cursors.Hand;
        }

        private static Color FromHex(string hex)
        {
            return ColorTranslator.FromHtml(hex);
        }
    }
}
