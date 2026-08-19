using System;
using System.IO;
using Newtonsoft.Json;

namespace eft_where_am_i.Classes
{
    /// <summary>
    /// tarkov-market.com 의 DOM 셀렉터.
    ///
    /// Nuxt 마크업이 바뀌면 이 셀렉터들이 한꺼번에 깨지는데, 상수로 박아두면
    /// 새 릴리스가 나올 때까지 앱을 쓸 수 없습니다. 그래서 외부 파일로 뺐습니다.
    ///
    /// 우선순위 (뒤에 있는 것이 앞을 덮어씀):
    ///  1. 코드에 내장된 기본값
    ///  2. 앱과 함께 배포되는 assets/selectors.json
    ///  3. %APPDATA%\eft-where-am-i\selectors.json  ← 사용자가 직접 고칠 수 있는 위치
    /// </summary>
    public static class SelectorConfig
    {
        private const string PanelTopRoot =
            "#__nuxt > div > div > div.page-content > div > div > div.panel_top > div";

        private sealed class SelectorFile
        {
            public string hide_show_panel_button { get; set; }
            public string full_screen_button { get; set; }
            public string where_am_i_button { get; set; }
            public string location_input { get; set; }
        }

        private static readonly SelectorFile Defaults = new SelectorFile
        {
            hide_show_panel_button = PanelTopRoot + " > div.mr-15 > button",
            full_screen_button = PanelTopRoot + " > button",
            where_am_i_button = PanelTopRoot + " > div.d-flex.ml-15 > button",
            location_input = PanelTopRoot + " > div:nth-child(4) > input[type=text]",
        };

        private static readonly SelectorFile Current = LoadAll();

        /// <summary>패널 접기/펼치기 버튼</summary>
        public static string HideShowPanelButton => Current.hide_show_panel_button;

        /// <summary>전체화면 버튼</summary>
        public static string FullScreenButton => Current.full_screen_button;

        /// <summary>"Where am i?" 버튼</summary>
        public static string WhereAmIButton => Current.where_am_i_button;

        /// <summary>스크린샷 파일명을 넣는 입력창</summary>
        public static string LocationInput => Current.location_input;

        /// <summary>사용자가 직접 수정할 수 있는 오버라이드 파일 경로</summary>
        public static string UserOverridePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "eft-where-am-i",
            "selectors.json");

        private static string BundledPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "assets", "selectors.json");

        private static SelectorFile LoadAll()
        {
            var result = new SelectorFile
            {
                hide_show_panel_button = Defaults.hide_show_panel_button,
                full_screen_button = Defaults.full_screen_button,
                where_am_i_button = Defaults.where_am_i_button,
                location_input = Defaults.location_input,
            };

            Overlay(result, TryLoad(BundledPath));
            Overlay(result, TryLoad(UserOverridePath));

            return result;
        }

        private static void Overlay(SelectorFile target, SelectorFile source)
        {
            if (source == null) return;

            if (!string.IsNullOrWhiteSpace(source.hide_show_panel_button))
                target.hide_show_panel_button = source.hide_show_panel_button;
            if (!string.IsNullOrWhiteSpace(source.full_screen_button))
                target.full_screen_button = source.full_screen_button;
            if (!string.IsNullOrWhiteSpace(source.where_am_i_button))
                target.where_am_i_button = source.where_am_i_button;
            if (!string.IsNullOrWhiteSpace(source.location_input))
                target.location_input = source.location_input;
        }

        private static SelectorFile TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;

                var loaded = JsonConvert.DeserializeObject<SelectorFile>(File.ReadAllText(path));
                if (loaded != null)
                    AppLogger.Info("Selectors", $"셀렉터 파일 적용: {path}");

                return loaded;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Selectors", $"셀렉터 파일 로드 실패 ({path}): {ex.Message}");
                return null;
            }
        }
    }
}
