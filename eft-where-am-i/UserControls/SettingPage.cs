using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using eft_where_am_i.Classes;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.InteropServices;
using Velopack;
using Velopack.Sources;

namespace eft_where_am_i
{
    public partial class SettingPage : UserControl
    {
        private readonly SettingsHandler settingsHandler; // SettingsHandler 인스턴스
        private AppSettings appSettings; // AppSettings 참조

        public SettingPage()
        {
            InitializeComponent();
            settingsHandler = SettingsHandler.Instance;     // 싱글톤 인스턴스 사용
            settingsHandler.SettingsChanged += OnSettingsChanged;

            LoadSettings();
            // Load 이벤트 핸들러 등록
            this.Load += SettingPage_Load;
        }

        private async void SettingPage_Load(object sender, EventArgs e)
        {
            try
            {
                // 컨트롤이 로드된 후 비동기 초기화 시작
                await InitializeWebViewUI();
            }
            catch (Exception ex)
            {
                // InitializeWebViewUI에서 throw한 예외를 여기서 최종 처리
                MessageBox.Show($"설정 페이지 WebView 초기화 중 심각한 오류 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnSettingsChanged(AppSettings updatedSettings)
        {
            appSettings = updatedSettings; // 로컬 참조 업데이트
                                           // WebView나 UI 갱신 필요 시 호출
            _ = webView2_Settings.ExecuteScriptAsync($"setCheckboxState({appSettings.auto_screenshot_detection.ToString().ToLower()})");
            _ = webView2_Settings.ExecuteScriptAsync($"setLanguage({JavaScriptExecutor.JsLiteral(appSettings.language)})");
            _ = webView2_Settings.ExecuteScriptAsync($"setScreenshotPath({JavaScriptExecutor.JsLiteral(appSettings.screenshot_path)})");
            _ = webView2_Settings.ExecuteScriptAsync($"setLogPath({JavaScriptExecutor.JsLiteral(appSettings.log_path)})");
            _ = webView2_Settings.ExecuteScriptAsync($"setDeadZonePercent({appSettings.dead_zone_percent})");
            _ = webView2_Settings.ExecuteScriptAsync($"setTheme({JavaScriptExecutor.JsLiteral(appSettings.theme_mode)})");
            _ = webView2_Settings.ExecuteScriptAsync(
                $"setAutoCaptureKey({JavaScriptExecutor.JsLiteral(appSettings.auto_screenshot_key)})");
            PushRadarInfo();
        }

        private async Task InitializeWebViewUI()
        {
            CoreWebView2Environment env = null;
            try
            {
                env = await WebViewEnvironmentFactory.CreateAsync(WebViewEnvironmentFactory.ProfileSettings);
                await webView2_Settings.EnsureCoreWebView2Async(env);

                // 가상 호스트 매핑 코드: 예를 들어, 번역 파일들이 저장된 폴더를 매핑
                string translationFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations");
                webView2_Settings.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "appassets",
                    translationFolder,
                    CoreWebView2HostResourceAccessKind.Allow
                );
            }
            catch (COMException comEx) when (comEx.ErrorCode == unchecked((int)0x8007139F))
            {
                MessageBox.Show($"WebView2 초기화 중 오류 발생: {comEx.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 초기화 중 예외 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html/settings.html");
            if (File.Exists(htmlPath))
            {
                webView2_Settings.Source = new Uri(htmlPath);
            }

            webView2_Settings.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // HTML이 완전히 로드된 후 명령 전달
            webView2_Settings.NavigationCompleted += async (sender, args) =>
            {
                if (args.IsSuccess)
                {
                    try
                    {
                        await webView2_Settings.ExecuteScriptAsync($"setCheckboxState({appSettings.auto_screenshot_detection.ToString().ToLower()})");

                        // 언어 설정 전송
                        await webView2_Settings.ExecuteScriptAsync($"setLanguage({JavaScriptExecutor.JsLiteral(appSettings.language)})");

                        // 경로 전송 (JsLiteral 이 백슬래시까지 안전하게 이스케이프합니다)
                        await webView2_Settings.ExecuteScriptAsync($"setScreenshotPath({JavaScriptExecutor.JsLiteral(appSettings.screenshot_path)})");
                        await webView2_Settings.ExecuteScriptAsync($"setLogPath({JavaScriptExecutor.JsLiteral(appSettings.log_path)})");

                        // 데드존 비율 설정 전송
                        await webView2_Settings.ExecuteScriptAsync($"setDeadZonePercent({appSettings.dead_zone_percent})");

                        // 테마 설정 전송
                        await webView2_Settings.ExecuteScriptAsync($"setTheme({JavaScriptExecutor.JsLiteral(appSettings.theme_mode)})");

                        // 자동 촬영 키 / 모바일 레이더 정보 전송
                        await webView2_Settings.ExecuteScriptAsync(
                            $"setAutoCaptureKey({JavaScriptExecutor.JsLiteral(appSettings.auto_screenshot_key)})");
                        PushRadarInfo();

                        // 앱 버전 정보 전송
                        var version = Assembly.GetExecutingAssembly()
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
                        await webView2_Settings.ExecuteScriptAsync($"setCurrentVersion({JavaScriptExecutor.JsLiteral(version)})");

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"JavaScript 명령 전송 중 오류 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        throw;
                    }
                }
            };
        }

        // WebView2 메시지 수신 핸들러
        private async void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawMessage = e.WebMessageAsJson.Trim('"').Replace("\\\"", "\"");
                JObject message = JObject.Parse(rawMessage);

                // 안전하게 속성 접근
                string action = message["action"]?.ToString() ?? "";
                string language = message["language"]?.ToString() ?? "";
                string theme = message["theme"]?.ToString() ?? "";
                string path = message["path"]?.ToString() ?? "";
                string url = message["url"]?.ToString() ?? "";

                // 메시지 처리
                switch (action.ToLower())
                {
                    case "check-update":
                        await CheckForUpdatesAsync();
                        break;

                    case "language-updated":
                        if (!string.IsNullOrEmpty(language))
                        {
                            appSettings.language = language;
                            SaveSettings();  // 설정 저장
                            MessageBox.Show($"Language updated to: {language}", "Language Change", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        break;

                    case "theme-updated":
                        if (!string.IsNullOrEmpty(theme))
                        {
                            appSettings.theme_mode = theme;
                            SaveSettings();
                        }
                        break;

                    case "change-path":
                        SelectScreenshotFolder();
                        break;

                    case "auto-detect-path":
                        try
                        {
                            string detectedPath = settingsHandler.ScreenshotPathSearch();
                            MessageBox.Show($"경로를 찾았습니다:\n{detectedPath}", "자동 탐지 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            // 3. 실패 시 예외 메시지 표시
                            // GetOrFindScreenshotPath가 throw한 예외 메시지를 그대로 보여줍니다.
                            MessageBox.Show(ex.Message, "자동 탐지 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;

                    case "open-folder":
                        if (!string.IsNullOrEmpty(appSettings.screenshot_path) && Directory.Exists(appSettings.screenshot_path))
                        {
                            Process.Start("explorer.exe", appSettings.screenshot_path);
                        }
                        else
                        {
                            MessageBox.Show("Invalid screenshot folder path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        break;

                    case "link-clicked":
                        if (!string.IsNullOrEmpty(url))
                        {
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                        }
                        break;

                    case "change-log-path":
                        SelectLogFolder();
                        break;

                    case "dead-zone-changed":
                        int deadZoneValue = message["value"]?.Value<int>() ?? 93;
                        appSettings.dead_zone_percent = deadZoneValue;
                        SaveSettings();
                        break;

                    case "auto-screenshot-key":
                        string keyName = message["value"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(keyName))
                        {
                            appSettings.auto_screenshot_key = keyName;
                            SaveSettings();
                        }
                        break;

                    case "radar-port":
                        int port = message["value"]?.Value<int>() ?? MobileRadarServer.DefaultPort;
                        if (port < 1024 || port > 65535) port = MobileRadarServer.DefaultPort;

                        if (port != appSettings.mobile_radar_port)
                        {
                            appSettings.mobile_radar_port = port;
                            SaveSettings();
                            PushRadarInfo();

                            if (appSettings.mobile_radar_enabled)
                            {
                                MessageBox.Show(
                                    "포트를 바꿨습니다. 모바일 레이더를 껐다 켜면 새 포트로 다시 시작합니다.\n\n" +
                                    "Port changed. Toggle Mobile Radar off and on to apply.",
                                    "모바일 레이더 / Mobile Radar",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        break;

                    case "radar-require-code":
                        appSettings.mobile_radar_require_code = message["isChecked"]?.Value<bool>() ?? false;
                        if (appSettings.mobile_radar_require_code
                            && string.IsNullOrWhiteSpace(appSettings.mobile_radar_token))
                        {
                            appSettings.mobile_radar_token = MobileRadarServer.GenerateToken();
                        }
                        SaveSettings();
                        PushRadarInfo();
                        MessageBox.Show(
                            "모바일 레이더를 껐다 켜면 적용됩니다.\n\nToggle Mobile Radar off and on to apply.",
                            "모바일 레이더 / Mobile Radar",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;

                    case "radar-regenerate-token":
                        appSettings.mobile_radar_token = MobileRadarServer.GenerateToken();
                        SaveSettings();
                        PushRadarInfo();
                        MessageBox.Show(
                            "새 접근 코드를 만들었습니다. 폰에서 주소를 다시 열어주세요.\n\n" +
                            "A new access code was generated. Reopen the address on your phone.",
                            "모바일 레이더 / Mobile Radar",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;

                    case "auto-detect-log-path":
                        try
                        {
                            string detectedLogPath = settingsHandler.LogPathSearch();
                            if (!string.IsNullOrEmpty(detectedLogPath))
                            {
                                MessageBox.Show($"경로를 찾았습니다:\n{detectedLogPath}", "자동 탐지 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("EFT 로그 폴더를 자동으로 탐지하지 못했습니다. 수동으로 지정해주세요.", "자동 탐지 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "자동 탐지 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;

                    default:
                        AppLogger.Warn("SettingPage", $"알 수 없는 action: {action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("SettingPage", $"메시지 처리 실패: {ex.Message}");
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/karpitony/eft-where-am-i", null, false));
                
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    MessageBox.Show("Cannot check for updates in debugger mode.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion != null)
                {
                    var message = $"새로운 업데이트(v{newVersion.TargetFullRelease.Version})가 있습니다.\n다운로드 및 설치 후 앱을 재시작하시겠습니까?\n\n" +
                                  $"A new update (v{newVersion.TargetFullRelease.Version}) is available.\nWould you like to download, install, and restart the app?";
                                  
                    var result = MessageBox.Show(message, "업데이트 알림 / Update Notification", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        await mgr.DownloadUpdatesAsync(newVersion);
                        mgr.ApplyUpdatesAndRestart(newVersion);
                    }
                }
                else
                {
                    MessageBox.Show("현재 최신 버전을 사용 중입니다.\nYou are using the latest version.", "업데이트 확인 / Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"업데이트 확인 중 오류가 발생했습니다: {ex.Message}", "오류 / Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 모바일 레이더의 포트와 접속 주소를 설정 화면으로 보냅니다.
        ///
        /// 서버 인스턴스는 WhereAmI 화면이 들고 있으므로, 여기서는 설정값과
        /// 이 PC 의 주소만으로 URL 을 만들어 보여줍니다.
        /// </summary>
        private void PushRadarInfo()
        {
            if (webView2_Settings.CoreWebView2 == null) return;

            string host = MobileRadarServer.GetLocalAddresses().FirstOrDefault();
            string url = MobileRadarServer.BuildUrl(
                host, appSettings.mobile_radar_port,
                appSettings.mobile_radar_token, appSettings.mobile_radar_require_code);

            _ = webView2_Settings.ExecuteScriptAsync(
                $"setRadarInfo({appSettings.mobile_radar_port}, {JavaScriptExecutor.JsLiteral(url)}, " +
                $"{appSettings.mobile_radar_require_code.ToString().ToLower()})");
        }

        private void LoadSettings()
        {
            try
            {
                appSettings = settingsHandler.GetSettings(); // SettingsHandler에서 설정 로드
            }
            catch (Exception ex)
            {
                AppLogger.Error("SettingPage", $"설정 로드 실패: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                settingsHandler.UpdateSettings(appSettings); // SettingsHandler를 통해 설정 저장
            }
            catch (Exception ex)
            {
                AppLogger.Error("SettingPage", $"설정 저장 실패: {ex.Message}");
            }
        }

        public async void SelectScreenshotFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "스크린샷 폴더를 선택하세요. Select the screenshot folder.";
                dialog.UseDescriptionForTitle = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    // 설정 업데이트 및 저장
                    appSettings.screenshot_path = selectedPath;
                    SaveSettings();

                    // JavaScript에 업데이트된 경로 전송
                    await webView2_Settings.ExecuteScriptAsync($"setScreenshotPath({JavaScriptExecutor.JsLiteral(selectedPath)})");
                }
            }
        }

        public async void SelectLogFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "EFT 로그 폴더를 선택하세요. Select the EFT logs folder.";
                dialog.UseDescriptionForTitle = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    // 설정 업데이트 및 저장
                    appSettings.log_path = selectedPath;
                    SaveSettings();

                    // JavaScript에 업데이트된 경로 전송
                    await webView2_Settings.ExecuteScriptAsync($"setLogPath({JavaScriptExecutor.JsLiteral(selectedPath)})");
                }
            }
        }

        private void lblHowToUse_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenInBrowser("https://github.com/karpitony/eft-where-am-i/blob/main/README.md");
        }

        private void lblBugReport_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenInBrowser("https://github.com/karpitony/eft-where-am-i/issues");
        }

        /// <summary>
        /// 기본 브라우저로 URL 을 엽니다.
        /// .NET 5+ 부터 Process.Start 의 UseShellExecute 기본값이 false 라서
        /// 이 옵션 없이 URL 을 넘기면 Win32Exception 이 납니다.
        /// </summary>
        private static void OpenInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Error("SettingPage", $"링크 열기 실패 ({url}): {ex.Message}");
            }
        }
    }
}