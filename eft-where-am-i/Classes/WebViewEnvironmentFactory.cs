using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace eft_where_am_i.Classes
{
    /// <summary>
    /// WebView2 사용자 데이터 폴더를 관리합니다.
    ///
    /// 예전에는 실행할 때마다 <c>%TEMP%\MyAppWebView2*\{새 GUID}</c> 를 만들고 지우지 않아
    /// 실행 횟수만큼 프로필이 쌓였습니다. 이제는 프로필별 고정 폴더를 재사용합니다.
    /// </summary>
    public static class WebViewEnvironmentFactory
    {
        /// <summary>콘텐츠(tarkov-market) 뷰</summary>
        public const string ProfileContent = "Content";

        /// <summary>메인 화면 하단 패널 UI</summary>
        public const string ProfilePanelUi = "PanelUI";

        /// <summary>설정 화면 UI</summary>
        public const string ProfileSettings = "Settings";

        private static string ProfileRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "eft-where-am-i",
            "WebView2");

        /// <summary>
        /// 지정한 프로필용 WebView2 환경을 만듭니다.
        ///
        /// 고정 폴더는 프로세스 하나만 점유할 수 있으므로, 앱을 두 개 띄우는 등으로
        /// 점유에 실패하면 일회용 폴더로 폴백합니다. (두 번째 인스턴스도 뜨긴 뜹니다)
        /// </summary>
        public static async Task<CoreWebView2Environment> CreateAsync(string profileName)
        {
            string stablePath = Path.Combine(ProfileRoot, profileName);

            try
            {
                Directory.CreateDirectory(stablePath);
                return await CoreWebView2Environment.CreateAsync(null, stablePath);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("WebView2",
                    $"고정 프로필 폴더를 쓸 수 없어 임시 폴더로 대체합니다 ({profileName}): {ex.Message}");

                string fallbackPath = Path.Combine(ProfileRoot, "_transient", $"{profileName}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(fallbackPath);
                return await CoreWebView2Environment.CreateAsync(null, fallbackPath);
            }
        }

        /// <summary>
        /// 구버전이 남긴 <c>%TEMP%\MyAppWebView2*</c> 와 이전 실행의 일회용 폴더를 정리합니다.
        /// 실패는 무시합니다 (사용 중인 폴더는 다음 실행 때 지워집니다).
        /// </summary>
        public static void CleanupStaleProfiles()
        {
            foreach (var dir in EnumerateStaleDirectories())
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    AppLogger.Info("WebView2", $"오래된 WebView2 프로필 삭제: {dir}");
                }
                catch
                {
                    // 다른 프로세스가 쓰는 중이면 건너뜁니다.
                }
            }
        }

        private static string[] EnumerateStaleDirectories()
        {
            var results = new System.Collections.Generic.List<string>();

            try
            {
                string temp = Path.GetTempPath();
                if (Directory.Exists(temp))
                    results.AddRange(Directory.GetDirectories(temp, "MyAppWebView2*"));
            }
            catch (Exception ex)
            {
                AppLogger.Debug("WebView2", $"임시 폴더 열거 실패: {ex.Message}");
            }

            try
            {
                string transient = Path.Combine(ProfileRoot, "_transient");
                if (Directory.Exists(transient))
                    results.AddRange(Directory.GetDirectories(transient));
            }
            catch (Exception ex)
            {
                AppLogger.Debug("WebView2", $"일회용 프로필 열거 실패: {ex.Message}");
            }

            return results.ToArray();
        }
    }
}
