using System;
using System.Drawing;
using System.IO;
using System.Linq; 
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using eft_where_am_i.Classes;
using System.Runtime.InteropServices;

namespace eft_where_am_i
{
    public partial class WhereAmI : UserControl
    {
        private readonly SettingsHandler settingsHandler; // SettingsHandler 인스턴스
        private AppSettings appSettings; // AppSettings 참조
        private JavaScriptExecutor jsExecutor;
        private QuestRepository questRepository;
        private FloorManager floorManager;
        private sealed class MapOption
        {
            public string value { get; set; } = string.Empty;
            public string label { get; set; } = string.Empty;
        }

        private string siteUrl;
        private bool whereAmIClick = false;
        private string screenshotPath;
        private FileSystemWatcher watcher;
        private LogWatcherService logWatcher;

        private bool chkAutoScreenshot;
        private bool isFloorEditMode = false;
        private GlobalHotkeyManager hotkeyManager;

        // 게임이 활성 상태일 때 스크린샷 키를 대신 눌러주는 서비스
        private AutoScreenshotService autoScreenshot;

        // 폰에서 맵을 볼 수 있게 화면을 내보내는 서버 + 화면 캡처 타이머
        private MobileRadarServer radarServer;
        private System.Windows.Forms.Timer radarCaptureTimer;
        private bool radarCaptureInFlight;

        public WhereAmI()
        {
            InitializeComponent();
            settingsHandler = SettingsHandler.Instance;             // 싱글톤 인스턴스 사용
            settingsHandler.SettingsChanged += OnSettingsChanged;   // 세팅 변경될 때마다 호출됨
            LoadSettings();                                         // 동기작업
            appSettings ??= settingsHandler.GetSettings();
            ApplyTheme();
            ApplyTranslations();
            siteUrl = $"https://tarkov-market.com/maps/{appSettings.latest_map}";

            // Load 이벤트 핸들러 등록
            this.Load += WhereAmI_Load;
        }

        private async void WhereAmI_Load(object? sender, EventArgs e)
        {
            try
            {
                // 1. UI용 WebView2 초기화
                await InitializeWebViewUI();

                // 2. 콘텐츠용 WebView2 초기화
                await InitializeWebViewContent();

                // 3. 모든 WebView가 초기화된 후에 jsExecutor 생성
                jsExecutor = new JavaScriptExecutor(webView2);
                questRepository = new QuestRepository();
                floorManager = new FloorManager();

                // 4. 앱 시작 시 패널을 강제로 열어둠
                await RestorePanelVisibilityAsync(appSettings.latest_map, forceOpen: true);

                // 5. LogWatcher 초기화
                InitializeLogWatcher();

                // 6. 모든 준비가 끝난 후 WmiInitialize 호출
                WmiInitialize();

                // 퀘스트 복원/리스너 주입은 NavigationCompleted 핸들러에서 처리

                // 6. 글로벌 핫키 매니저 초기화 (EFT 활성 시 Ctrl+Numpad로 층 전환)
                hotkeyManager = new GlobalHotkeyManager();
                hotkeyManager.FloorHotkeyPressed += OnFloorHotkeyPressed;

                // 7. 자동 스크린샷 / 모바일 레이더
                InitializeAutoScreenshot();
                InitializeMobileRadar();
            }
            catch (Exception ex)
            {
                // Initialize... 메서드에서 throw한 예외를 여기서 최종 처리
                MessageBox.Show($"WebView 초기화 중 심각한 오류 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnSettingsChanged(AppSettings updatedSettings)
        {
            // 새로운 설정 반영
            appSettings = updatedSettings;

            // 화면 갱신 (언어/경로 등 UI 업데이트)
            LoadSettings();
            ApplyTheme();
            ApplyTranslations();

            // 설정 화면에서 바꾼 값을 실행 중인 서비스에 반영합니다.
            if (autoScreenshot != null)
            {
                autoScreenshot.SetKeyFromName(appSettings.auto_screenshot_key);
                autoScreenshot.IntervalSeconds = appSettings.auto_screenshot_interval_sec;
            }
            string language = appSettings.language;

            // webView2_panel_ui.CoreWebView2가 null이 아닌지 확인하여
            // 컨트롤이 초기화되었을 때만 스크립트를 실행합니다.
            if (webView2_panel_ui.CoreWebView2 != null)
            {
                try
                {
                    await webView2_panel_ui.ExecuteScriptAsync($"setLanguage({JavaScriptExecutor.JsLiteral(language)})");
                    string mapListJson = Newtonsoft.Json.JsonConvert.SerializeObject(GetMapListForLanguage(language));
                    await webView2_panel_ui.ExecuteScriptAsync($"populateMapList({JavaScriptExecutor.JsLiteral(mapListJson)}, {JavaScriptExecutor.JsLiteral(appSettings.latest_map)})");
                    await webView2_panel_ui.ExecuteScriptAsync($"setTheme({JavaScriptExecutor.JsLiteral(appSettings.theme_mode)})");
                }
                catch (Exception ex)
                {
                    // 초기화 직후 드물게 발생하는 예외를 대비한 방어 코드
                    AppLogger.Warn("WhereAmI", $"설정 변경 반영 실패: {ex.Message}");
                }
            }
            // 만약 null이라면 (아직 초기화 전),
            // 어차피 InitializeWebViewUI()의 NavigationCompleted 이벤트 핸들러가
            // 나중에 최신 appSettings 값을 읽어 언어를 설정할 것이므로
            // 여기서 별도로 처리할 필요가 없습니다.
        }


        private void ApplyTheme()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ApplyTheme));
                return;
            }

            this.BackColor = AppTheme.Background;
            panel1.BackColor = AppTheme.Background;
            AppTheme.StyleCheckBox(checkBoxHide);
        }

        private void ApplyTranslations()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ApplyTranslations));
                return;
            }

            UpdateFoldButtonText();
        }

        private void UpdateFoldButtonText()
        {
            if (checkBoxHide == null)
            {
                return;
            }

            string key = checkBoxHide.Checked
                ? "whereAmI_ClickToUnfold"
                : "whereAmI_ClickToFold";

            string fallback = checkBoxHide.Checked
                ? "∨ Click to Unfold"
                : "∧ Click to Fold";

            checkBoxHide.Text = GetString(key, fallback);
        }

        private string GetString(string key, string fallback)
        {
            try
            {
                string language = appSettings?.language ?? SettingsHandler.Instance.GetSettings().language;
                if (string.IsNullOrEmpty(language)) language = "en";

                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations", $"{language}.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var values = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (values != null && values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch (Exception)
            {
            }

            return fallback;
        }

        private async Task InitializeWebViewContent()
        {
            CoreWebView2Environment env = null;
            try
            {
                // 고정 프로필 폴더 재사용 (예전처럼 실행마다 임시 폴더를 만들지 않습니다)
                env = await WebViewEnvironmentFactory.CreateAsync(WebViewEnvironmentFactory.ProfileContent);
                await webView2.EnsureCoreWebView2Async(env);
            }
            catch (COMException comEx) when (comEx.ErrorCode == unchecked((int)0x8007139F))
            {
                MessageBox.Show($"웹 콘텐츠용 WebView2 초기화 오류: {comEx.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"웹 콘텐츠용 WebView2 초기화 예외: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }

            // 영구 주입 스크립트 (새로고침 시에도 유지되도록 웹 콘텐츠가 로딩되기 전에 주입)
            await webView2.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(Constants.DEAD_ZONE_AUTO_PAN_SCRIPT);
            await webView2.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(Constants.FLOOR_DETECTION_SCRIPT);

            // WebView2 콘텐츠 메시지 수신 핸들러 등록
            webView2.CoreWebView2.WebMessageReceived += WebView2Content_WebMessageReceived;

            // 페이지 로드 완료 핸들러 등록 (퀘스트 복원/리스너 주입을 페이지 로드 완료 후 수행)
            webView2.NavigationCompleted += WebView2_NavigationCompleted;

            // SPA (Single Page Application) 내부 라우팅 감지 핸들러 등록
            webView2.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;

            // 외부 URL 로드 (예외 처리 포함)
            try
            {
                webView2.Source = new Uri(siteUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"웹 콘텐츠 URL 설정 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private async Task InitializeWebViewUI()
        {
            CoreWebView2Environment env = null;
            try
            {
                env = await WebViewEnvironmentFactory.CreateAsync(WebViewEnvironmentFactory.ProfilePanelUi);
                await webView2_panel_ui.EnsureCoreWebView2Async(env);


                // 가상 호스트 매핑 코드: 예를 들어, 번역 파일들이 저장된 폴더를 매핑
                string translationFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations");
                webView2_panel_ui.CoreWebView2.SetVirtualHostNameToFolderMapping(
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

            // HTML 파일 로드
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html/panel.html");
            if (File.Exists(htmlPath))
            {
                webView2_panel_ui.Source = new Uri(htmlPath);
            }

            webView2_panel_ui.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // HTML이 완전히 로드된 후 명령 전달
            webView2_panel_ui.NavigationCompleted += async (sender, args) =>
            {
                if (args.IsSuccess)
                {
                    try
                    {
                        // 언어 설정 전송
                        string language = appSettings.language;
                        await webView2_panel_ui.ExecuteScriptAsync($"setLanguage({JavaScriptExecutor.JsLiteral(language)})");

                        // 콤보박스에 맵 목록 전송
                        string mapListJson = Newtonsoft.Json.JsonConvert.SerializeObject(GetMapListForLanguage(appSettings.language));
                        await webView2_panel_ui.ExecuteScriptAsync($"populateMapList({JavaScriptExecutor.JsLiteral(mapListJson)}, {JavaScriptExecutor.JsLiteral(appSettings.latest_map)})");

                        // 체크박스 상태 전송
                        await webView2_panel_ui.ExecuteScriptAsync($"setCheckboxState({appSettings.auto_screenshot_detection.ToString().ToLower()})");

                        // 자동 맵 감지 체크박스 상태 전송
                        await webView2_panel_ui.ExecuteScriptAsync($"setAutoMapCheckboxState({appSettings.auto_map_detection.ToString().ToLower()})");

                        // 자동 패닝 체크박스 상태 전송
                        await webView2_panel_ui.ExecuteScriptAsync($"setAutoPanningCheckboxState({appSettings.auto_panning.ToString().ToLower()})");

                        // 자동 스크린샷 촬영 상태 전송
                        await webView2_panel_ui.ExecuteScriptAsync(
                            $"setAutoCaptureState({appSettings.auto_screenshot_capture.ToString().ToLower()}, {appSettings.auto_screenshot_interval_sec})");

                        // 모바일 레이더 상태 전송
                        await webView2_panel_ui.ExecuteScriptAsync(
                            $"setMobileRadarState({appSettings.mobile_radar_enabled.ToString().ToLower()})");
                        PushRadarStatusToUi();

                        // 테마 설정 전송
                        await webView2_panel_ui.ExecuteScriptAsync($"setTheme({JavaScriptExecutor.JsLiteral(appSettings.theme_mode)})");

                        // 스크린샷 자동 삭제 체크박스 상태 전송
                        await webView2_panel_ui.ExecuteScriptAsync(
                            $"setAutoScreenshotCleanupCheckboxState({appSettings.auto_screenshot_cleanup.ToString().ToLower()})");

                        // 디버그 모드 플래그 전송
#if DEBUG
                        await webView2_panel_ui.ExecuteScriptAsync("setDebugMode(true)");
#else
                        await webView2_panel_ui.ExecuteScriptAsync("setDebugMode(false)");
#endif
                    }
                    catch (Exception ex)
                    {
                        // async void 이벤트 핸들러라 여기서 throw 하면 앱이 그대로 죽습니다.
                        AppLogger.Error("WhereAmI", $"패널 UI 초기 상태 전송 실패: {ex.Message}");
                    }
                }
            };
        }

        // 메시지 수신 핸들러
        private List<MapOption> GetMapListForLanguage(string language)
        {
            return MapCatalog.Slugs
                .Select(slug => new MapOption
                {
                    value = slug,
                    label = MapCatalog.GetDisplayName(slug, language)
                })
                .ToList();
        }

        private async void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawMessage = e.WebMessageAsJson.Trim('"').Replace("\\\"", "\"");

                JObject message = JObject.Parse(rawMessage);

                // 안전하게 속성 접근
                string action = message["action"]?.ToString() ?? "";
                string map = message["map"]?.ToString() ?? "";
                string theme = message["theme"]?.ToString() ?? "";
                bool isChecked = message["isChecked"] != null && bool.Parse(message["isChecked"].ToString());
                string url = message["url"]?.ToString() ?? "";

                // 메시지 처리
                switch (action.ToLower())
                {
                    case "map-selected":
                        if (!string.IsNullOrEmpty(map))
                        {
                            HandleMapSelection(map);
                        }
                        break;

                    case "checkbox-updated":
                        appSettings.auto_screenshot_detection = isChecked;
                        SaveSettings();  // 설정 변경 저장
                        UpdateWatcherState(isChecked);
                        break;

                    case "hide-show-panel":
                        btnHideShowPannel_Click(null, null);
                        // 패널 상태 저장 (클릭 처리 후 딜레이를 두고 상태 읽기)
                        _ = SavePanelStateAsync();
                        break;

                    case "full-screen":
                        btnFullScreen_Click(null, null);
                        break;

                    case "force-run":
                        btnForceRun_Click(null, null);
                        break;

                    case "auto-map-toggle":
                        UpdateLogWatcherState(isChecked);
                        break;

                    case "auto-screenshot-capture-toggle":
                        UpdateAutoScreenshotState(isChecked);
                        break;

                    case "auto-screenshot-interval":
                        int interval = message["value"]?.Value<int>() ?? AutoScreenshotService.DefaultIntervalSeconds;
                        appSettings.auto_screenshot_interval_sec = Math.Clamp(
                            interval, AutoScreenshotService.MinIntervalSeconds, AutoScreenshotService.MaxIntervalSeconds);
                        SaveSettings();
                        if (autoScreenshot != null)
                            autoScreenshot.IntervalSeconds = appSettings.auto_screenshot_interval_sec;
                        break;

                    case "mobile-radar-toggle":
                        UpdateMobileRadarState(isChecked);
                        break;

                    case "mobile-radar-status":
                        PushRadarStatusToUi();
                        break;

                    case "copy-radar-url":
                        if (radarServer != null && radarServer.IsRunning)
                        {
                            try
                            {
                                Clipboard.SetText(radarServer.GetPrimaryUrl());
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Warn("Radar", $"주소 복사 실패: {ex.Message}");
                            }
                        }
                        break;

                    case "auto-panning-toggle":
                        appSettings.auto_panning = isChecked;
                        SaveSettings();
                        break;

                    case "auto-screenshot-cleanup-toggle":
                        appSettings.auto_screenshot_cleanup = isChecked;
                        SaveSettings();
                        if (logWatcher != null)
                        {
                            if (isChecked && !appSettings.auto_map_detection)
                                logWatcher.Start();
                            else if (!isChecked && !appSettings.auto_map_detection)
                                logWatcher.Stop();
                        }
                        break;

                    case "theme-updated":
                        if (!string.IsNullOrEmpty(theme))
                        {
                            appSettings.theme_mode = theme;
                            SaveSettings();
                        }
                        break;

                    case "toggle-floor-edit-mode":
                        await ToggleFloorEditModeAsync();
                        break;

                    case "floor-db-updated":
                        floorManager?.Reload();
                        break;

                    case "link-clicked":
                        if (!string.IsNullOrEmpty(url))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                        }
                        break;

                    default:
                        AppLogger.Warn("PanelUI", $"알 수 없는 action: {action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("PanelUI", $"메시지 처리 실패: {ex.Message}");
            }
        }

        private void HandleMapSelection(string selectedMap)
        {
            if (!string.IsNullOrEmpty(selectedMap))
            {
                // 맵 변경 - 퀘스트는 실시간으로 저장되므로 별도 저장 불필요

                appSettings.latest_map = selectedMap;
                SaveSettings();  // 설정 저장
                siteUrl = $"https://tarkov-market.com/maps/{selectedMap}";
                webView2.Source = new Uri(siteUrl);
                whereAmIClick = false;
                WmiInitialize();

                // 퀘스트 복원/리스너 주입은 NavigationCompleted 핸들러에서 처리
            }
        }

        private async void WebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            // jsExecutor가 아직 초기화되지 않은 경우 무시
            if (jsExecutor == null) return;

            // 상시 주입 스크립트 재주입 (새 페이지 로드 시)
            await jsExecutor.ExecuteScriptAsync(Constants.DEAD_ZONE_AUTO_PAN_SCRIPT);
            await jsExecutor.ExecuteScriptAsync(Constants.FLOOR_DETECTION_SCRIPT);

            // 퀘스트 컨테이너 로드 대기 (DOM 준비 완료까지 대기)
            bool containerReady = await jsExecutor.WaitForQuestContainerAsync(15000);

            // 패널 상태 복원: 저장된 값이 있으면 그 상태로, 없으면 첫 실행이므로 열려 있는 상태로 맞춤
            await RestorePanelVisibilityAsync(appSettings.latest_map);

            if (containerReady)
            {
                // 클릭 리스너 주입
                await jsExecutor.InjectQuestClickListenerAsync();
                // 퀘스트 복원
                await RestoreQuestsAsync(appSettings.latest_map);
            }

            // 새로고침의 경우 Where Am I 패널과 방향 표시를 다시 적용
            try
            {
                if (!whereAmIClick)
                {
                    whereAmIClick = true;
                    await jsExecutor.ClickButtonAsync(SelectorConfig.WhereAmIButton);
                    await Task.Delay(300);
                }

                await jsExecutor.ExecuteScriptAsync(Constants.ADD_DIRECTION_INDICATORS_SCRIPT);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("WhereAmI", $"Post-navigation reapply failed: {ex.Message}");
            }
        }

        private async void CoreWebView2_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            // URL 변경 감지: 맵 slug(/maps/{slug})가 실제로 바뀐 경우에만 복원 로직 실행
            string currentUrl = webView2.Source?.ToString();
            string mapName = TryExtractMapNameFromUrl(currentUrl);
            if (string.IsNullOrEmpty(mapName))
            {
                return;
            }

            bool mapChanged = !string.Equals(appSettings.latest_map, mapName, StringComparison.OrdinalIgnoreCase);
            if (!mapChanged)
            {
                // 같은 맵 내부에서 발생한 SourceChanged(예: 퀘스트 이미지 열기)에는 패널 복원을 하지 않음
                return;
            }

            appSettings.latest_map = mapName;
            SaveSettings();

            // UI 패널(WhereAmIPanel)의 ComboBox 상태도 함께 변경
            if (webView2_panel_ui.CoreWebView2 != null)
            {
                _ = webView2_panel_ui.ExecuteScriptAsync($"document.getElementById('mapSelect').value = {JavaScriptExecutor.JsLiteral(mapName)};");
            }

            // SPA 라우팅으로 맵이 실제 변경된 경우에만 각종 초기화 작업 재개
            if (jsExecutor != null)
            {
                bool containerReady = await jsExecutor.WaitForQuestContainerAsync(15000);

                await RestorePanelVisibilityAsync(appSettings.latest_map);

                if (containerReady)
                {
                    await jsExecutor.ExecuteScriptAsync(Constants.ADD_DIRECTION_INDICATORS_SCRIPT);
                    await jsExecutor.InjectQuestClickListenerAsync();
                    await RestoreQuestsAsync(appSettings.latest_map);
                }
            }
        }

        private string TryExtractMapNameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    return null;
                }

                string[] segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    if (string.Equals(segments[i], "maps", StringComparison.OrdinalIgnoreCase))
                    {
                        return segments[i + 1].ToLowerInvariant();
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private async Task RestorePanelVisibilityAsync(string mapName, bool forceOpen = false)
        {
            if (jsExecutor == null)
            {
                return;
            }

            if (forceOpen)
            {
                await jsExecutor.OpenPanelIfHiddenAsync(3, 100);
                return;
            }

            if (appSettings.panel_hidden_per_map.TryGetValue(mapName, out bool isHidden))
            {
                if (isHidden)
                {
                    await jsExecutor.ClickButtonAsync(SelectorConfig.HideShowPanelButton);
                }
                else
                {
                    await jsExecutor.OpenPanelIfHiddenAsync(3, 100);
                }
            }
            else
            {
                await jsExecutor.OpenPanelIfHiddenAsync(3, 100);
            }
        }

        private void LoadSettings()
        {
            try
            {
                appSettings = settingsHandler.GetSettings();                // SettingsHandler에서 설정 로드
                screenshotPath = settingsHandler.GetOrFindScreenshotPath(); // 스크린샷 경로 설정

                chkAutoScreenshot = appSettings.auto_screenshot_detection;
                // 설정값을 기준으로 FileSystemWatcher를 초기화합니다.
                UpdateWatcherState(chkAutoScreenshot);
            }
            catch (Exception ex)
            {
                // 스크린샷 폴더 자동 탐지 실패는 첫 실행에서 흔합니다.
                // 설정 화면의 "Auto Find" 가 별도로 결과를 알려주므로 여기서는 로그만 남깁니다.
                AppLogger.Warn("WhereAmI", $"설정 로드 중 경고: {ex.Message}");
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
                AppLogger.Error("WhereAmI", $"설정 저장 실패: {ex.Message}");
            }
        }

        private string GetLatestFile()
        {
            if (!Directory.Exists(screenshotPath))
            {
                return null; // 경로가 존재하지 않으면 null 반환
            }

            var directoryInfo = new DirectoryInfo(screenshotPath);
            var files = directoryInfo.GetFiles().OrderByDescending(f => f.LastWriteTime).ToList();

            if (files.Count == 0)
            {
                return null; // 파일이 없으면 null 반환
            }

            return files.FirstOrDefault()?.Name;
        }

        private async void WmiInitialize()
        {
            await Task.Delay(4000);
            await jsExecutor.ClickButtonAsync(SelectorConfig.FullScreenButton);
            if (!whereAmIClick)
            {
                whereAmIClick = true;
                await jsExecutor.ClickButtonAsync(SelectorConfig.WhereAmIButton);
                await Task.Delay(500);
            }
            await jsExecutor.ExecuteScriptAsync(Constants.ADD_DIRECTION_INDICATORS_SCRIPT);
            await jsExecutor.ExecuteScriptAsync(Constants.DEAD_ZONE_AUTO_PAN_SCRIPT);
            await jsExecutor.ExecuteScriptAsync(Constants.FLOOR_DETECTION_SCRIPT);
        }

        /// <summary>
        /// 스크린샷 1건 처리를 직렬화합니다. 연사로 찍으면 이전 처리가 끝나기 전에
        /// 다음 처리가 들어와 입력창을 서로 덮어쓰기 때문입니다.
        /// </summary>
        private readonly SemaphoreSlim checkLocationGate = new SemaphoreSlim(1, 1);

        private async Task RunCheckLocationAsync()
        {
            if (!await checkLocationGate.WaitAsync(0))
            {
                AppLogger.Debug("WhereAmI", "이전 위치 확인이 진행 중이라 이번 요청은 건너뜁니다.");
                return;
            }

            try
            {
                await CheckLocationAsync();

                // 타이머(1초)를 기다리지 않고 바로 폰으로 보냅니다.
                // 위치가 갱신된 직후가 화면이 가장 최신인 시점입니다.
                await CaptureRadarFrameAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error("WhereAmI", $"위치 확인 실패: {ex.Message}");
            }
            finally
            {
                checkLocationGate.Release();
            }
        }

        private async Task CheckLocationAsync()
        {
            string screenshot = GetLatestFile();
            if (screenshot == null) return;

            if (!await jsExecutor.CheckInputAble())
            {
                whereAmIClick = true;
                await jsExecutor.ClickButtonAsync(SelectorConfig.WhereAmIButton);
                await Task.Delay(500);
            }

            string filenameWithoutExt = Path.GetFileNameWithoutExtension(screenshot);
            await jsExecutor.SetInputValueAsync("input[type=\"text\"]", filenameWithoutExt);

            // 좌표 파싱 후 자동 층 전환
            await AutoSwitchFloorAsync(filenameWithoutExt);

            // 마커 렌더링 대기 후 데드존 auto-pan (설정이 활성화된 경우에만)
            if (appSettings.auto_panning)
            {
                await Task.Delay(300);
                await jsExecutor.AutoPanToMarkerAsync(appSettings.dead_zone_percent);
            }
        }

        /// <summary>
        /// 층 레이어를 클릭하고, 실제로 매칭된 라벨을 맵별로 기억해 둡니다.
        ///
        /// floor_db 는 "Ground"/"Underground" 같은 논리적 이름을 쓰는데 tarkov-market 라벨은
        /// 맵마다 "Main"/"Basement" 등으로 다릅니다. 맵 데이터를 추측해서 채워 넣는 대신,
        /// 실제로 통한 라벨을 관측해서 다음부터 그것을 먼저 시도합니다.
        /// </summary>
        private async Task ClickFloorAndLearnAsync(string mapName, string floorName)
        {
            string key = $"{mapName}/{floorName}";

            var candidates = new List<string>();
            if (appSettings.learned_floor_labels.TryGetValue(key, out var learned)
                && !string.IsNullOrWhiteSpace(learned))
            {
                candidates.Add(learned);
            }

            foreach (var candidate in FloorManager.GetLabelCandidates(floorName))
            {
                if (!candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(candidate);
            }

            var result = await jsExecutor.ClickFloorByFirstMatchAsync(candidates.ToArray());

            if (result.Matched && !string.IsNullOrWhiteSpace(result.MatchedName))
            {
                if (!appSettings.learned_floor_labels.TryGetValue(key, out var stored)
                    || !string.Equals(stored, result.MatchedName, StringComparison.Ordinal))
                {
                    appSettings.learned_floor_labels[key] = result.MatchedName;
                    SaveSettings();
                    AppLogger.Info("Floor", $"층 라벨 학습: {key} -> {result.MatchedName}");
                }
            }
        }

        #region 자동 스크린샷

        private void InitializeAutoScreenshot()
        {
            autoScreenshot = new AutoScreenshotService
            {
                IntervalSeconds = appSettings.auto_screenshot_interval_sec,
            };
            autoScreenshot.SetKeyFromName(appSettings.auto_screenshot_key);

            if (appSettings.auto_screenshot_capture)
            {
                // 자동 촬영은 스크린샷 감지가 켜져 있어야 의미가 있습니다.
                EnsureScreenshotWatcherForCapture();
                autoScreenshot.Start();
            }
        }

        /// <summary>
        /// 자동 촬영을 켤 때 스크린샷 감지가 꺼져 있으면 같이 켜줍니다.
        /// (찍기만 하고 읽지 않으면 아무 일도 일어나지 않으므로)
        /// </summary>
        private void EnsureScreenshotWatcherForCapture()
        {
            if (appSettings.auto_screenshot_detection) return;

            appSettings.auto_screenshot_detection = true;
            SaveSettings();
            UpdateWatcherState(true);

            if (webView2_panel_ui.CoreWebView2 != null)
                _ = webView2_panel_ui.ExecuteScriptAsync("setCheckboxState(true)");

            AppLogger.Info("AutoScreenshot", "자동 촬영을 위해 스크린샷 감지를 함께 켰습니다.");
        }

        private void UpdateAutoScreenshotState(bool isEnabled)
        {
            appSettings.auto_screenshot_capture = isEnabled;
            SaveSettings();

            if (autoScreenshot == null) return;

            if (isEnabled)
            {
                EnsureScreenshotWatcherForCapture();
                autoScreenshot.Start();
            }
            else
            {
                autoScreenshot.Stop();
            }
        }

        #endregion

        #region 모바일 레이더

        private void InitializeMobileRadar()
        {
            radarServer = new MobileRadarServer();

            // 접근 코드는 한 번 만들어 두고 계속 씁니다. (폰 북마크가 유지되도록)
            if (string.IsNullOrWhiteSpace(appSettings.mobile_radar_token))
            {
                appSettings.mobile_radar_token = MobileRadarServer.GenerateToken();
                SaveSettings();
            }

            // 폰이 실제로 보고 있을 때만 캡처합니다. 아무도 안 보면 아무 일도 하지 않습니다.
            radarCaptureTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            radarCaptureTimer.Tick += async (s, e) => await CaptureRadarFrameAsync();

            if (appSettings.mobile_radar_enabled)
            {
                UpdateMobileRadarState(true);
            }
        }

        private void UpdateMobileRadarState(bool isEnabled)
        {
            appSettings.mobile_radar_enabled = isEnabled;
            SaveSettings();

            if (radarServer == null) return;

            if (isEnabled)
            {
                try
                {
                    radarServer.Start(appSettings.mobile_radar_port, appSettings.mobile_radar_token, appSettings.mobile_radar_require_code);
                    radarCaptureTimer?.Start();
                }
                catch (Exception ex)
                {
                    appSettings.mobile_radar_enabled = false;
                    SaveSettings();

                    MessageBox.Show(
                        $"모바일 레이더를 시작하지 못했습니다.\n\n{ex.Message}\n\n" +
                        $"다른 프로그램이 {appSettings.mobile_radar_port}번 포트를 쓰고 있다면 설정에서 포트를 바꿔주세요.",
                        "모바일 레이더", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                radarCaptureTimer?.Stop();
                radarServer.Stop();
            }

            PushRadarStatusToUi();
        }

        /// <summary>현재 레이더 접속 주소를 패널 UI 로 보냅니다.</summary>
        private void PushRadarStatusToUi()
        {
            if (webView2_panel_ui.CoreWebView2 == null || radarServer == null) return;

            string url = radarServer.IsRunning ? radarServer.GetPrimaryUrl() : string.Empty;
            _ = webView2_panel_ui.ExecuteScriptAsync(
                $"setRadarStatus({(radarServer.IsRunning ? "true" : "false")}, {JavaScriptExecutor.JsLiteral(url)})");
        }

        /// <summary>
        /// WebView2 화면을 JPEG 으로 캡처해 레이더 서버에 게시합니다.
        /// 이미 마커까지 그려진 화면이라 폰에서는 그대로 보기만 하면 됩니다.
        /// </summary>
        private async Task CaptureRadarFrameAsync()
        {
            if (radarServer == null || !radarServer.IsRunning) return;
            if (radarCaptureInFlight) return;
            if (!radarServer.HasRecentClient) return;      // 아무도 안 보면 캡처하지 않음
            if (webView2?.CoreWebView2 == null) return;

            radarCaptureInFlight = true;
            try
            {
                using var buffer = new MemoryStream();
                await webView2.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Jpeg, buffer);

                radarServer.CurrentMapLabel = MapCatalog.GetDisplayName(
                    appSettings.latest_map, appSettings.language);
                radarServer.PublishFrame(buffer.ToArray());
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Radar", $"화면 캡처 실패: {ex.Message}");
            }
            finally
            {
                radarCaptureInFlight = false;
            }
        }

        #endregion

        /// <summary>
        /// 스크린샷 좌표로 현재 층을 판별하고 해당 층 레이어를 클릭합니다.
        ///
        /// 좌표계 주의: 타르코프는 Unity 기반이라 <b>y 가 높이</b>입니다.
        /// 그리고 zone.polygon 은 게임 좌표가 아니라 <b>맵 CSS 픽셀 좌표</b>라서
        /// 폴리곤 판정은 마커 위치를 아는 브라우저(<c>__detectFloor</c>)에 위임합니다.
        /// </summary>
        private async Task AutoSwitchFloorAsync(string filename)
        {
            if (floorManager == null || jsExecutor == null) return;

            try
            {
                if (!ScreenshotCoordinates.TryParse(filename, out var coords))
                {
                    AppLogger.Debug("Floor", $"좌표를 파싱하지 못했습니다: {filename}");
                    return;
                }

                string mapName = appSettings.latest_map;
                string floorName;

                if (floorManager.HasZones(mapName))
                {
                    // 마커가 그려질 때까지 대기한 뒤 브라우저 쪽에서 폴리곤 판정
                    await Task.Delay(500);
                    floorName = await jsExecutor.DetectFloorAsync(floorManager.GetZonesJson(mapName), coords.Height)
                                ?? floorManager.GetDefaultFloor(mapName);
                }
                else
                {
                    // zone 이 없는 맵은 높이 범위만으로 판정
                    floorName = floorManager.GetFloorNameByHeight(mapName, coords.Height);
                    if (!string.IsNullOrEmpty(floorName))
                        await Task.Delay(500);
                }

                if (string.IsNullOrEmpty(floorName)) return;

                await ClickFloorAndLearnAsync(mapName, floorName);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Floor", $"자동 층 전환 실패: {ex.Message}");
            }
        }

        private void UpdateWatcherState(bool isEnabled)
        {
            // bool 필드 값을 새 상태로 업데이트
            chkAutoScreenshot = isEnabled;

            if (isEnabled)
            {
                if (string.IsNullOrEmpty(screenshotPath) || !Directory.Exists(screenshotPath))
                {
                    MessageBox.Show("올바르지 않은 경로입니다. 설정 페이지에서 경로를 확인해주세요.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    chkAutoScreenshot = false; // 상태를 다시 false로
                    appSettings.auto_screenshot_detection = false;
                    SaveSettings();

                    // (중요) UI에도 반영
                    _ = webView2_panel_ui.ExecuteScriptAsync("setCheckboxState(false)");
                    return;
                }

                // 와처가 이미 실행 중이면 중복 생성 방지
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }

                watcher = new FileSystemWatcher
                {
                    Path = screenshotPath,
                    Filter = "*.png",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                };

                watcher.Created += OnScreenshotCreated;
                watcher.EnableRaisingEvents = true;
            }
            else
            {
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= OnScreenshotCreated;
                    watcher.Dispose();
                    watcher = null;
                }
            }

            // (선택사항) 이 메서드에서 직접 설정을 저장하도록 변경
            // appSettings.auto_screenshot_detection = isEnabled;
            // SaveSettings();
        }

        private async void OnScreenshotCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 게임이 파일을 다 쓸 때까지 기다립니다.
                // 예전에는 무조건 500ms 를 쉬었는데, 대부분은 그보다 훨씬 빨리 준비됩니다.
                await WaitForFileReadyAsync(e.FullPath);

                if (IsDisposed || Disposing || !IsHandleCreated) return;

                // WebView2 호출은 UI 스레드에서만 유효합니다.
                if (InvokeRequired)
                    BeginInvoke(new Action(() => _ = RunCheckLocationAsync()));
                else
                    await RunCheckLocationAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error("WhereAmI", $"스크린샷 감지 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 스크린샷 파일이 열릴 때까지 짧게 재시도합니다.
        /// 게임이 아직 쓰고 있는 동안에는 공유 위반이 나므로, 열리는 순간이 곧 완료 시점입니다.
        /// </summary>
        private static async Task WaitForFileReadyAsync(string path, int timeoutMs = 3000)
        {
            var started = DateTime.UtcNow;

            while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length > 0) return;
                }
                catch (IOException)
                {
                    // 아직 게임이 쓰는 중
                }
                catch (Exception)
                {
                    return;   // 접근 권한 등 다른 문제면 그냥 진행
                }

                await Task.Delay(30);
            }
        }

        private async void btnHideShowPannel_Click(object sender, EventArgs e)
        {
            await jsExecutor.ClickButtonAsync(SelectorConfig.HideShowPanelButton);
        }

        private async Task SavePanelStateAsync()
        {
            await Task.Delay(500); // 버튼 클릭 처리 완료 대기
            bool isHidden = await jsExecutor.IsPanelHiddenAsync();
            appSettings.panel_hidden_per_map[appSettings.latest_map] = isHidden;
            SaveSettings();
        }

        private async void btnFullScreen_Click(object sender, EventArgs e)
        {
            await jsExecutor.ClickButtonAsync(SelectorConfig.FullScreenButton);
        }

        private async void btnForceRun_Click(object sender, EventArgs e)
        {
            await RunCheckLocationAsync();
        }

        private async Task RestoreQuestsAsync(string mapName)
        {
            try
            {
                var quests = questRepository.GetQuests(mapName);
                foreach (var questName in quests)
                {
                    await jsExecutor.SelectQuestByNameAsync(questName);
                    await Task.Delay(300); // Wait between selections to avoid race conditions
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Quest", $"퀘스트 복원 실패: {ex.Message}");
            }
        }

        private async Task ToggleFloorEditModeAsync()
        {
            if (isFloorEditMode)
            {
                // Exit edit mode
                AppLogger.Info("FloorEdit", "Exiting floor edit mode");
                await jsExecutor.DisableFloorEditModeAsync();
                isFloorEditMode = false;
                // Update button text
                if (webView2_panel_ui.CoreWebView2 != null)
                {
                    await webView2_panel_ui.ExecuteScriptAsync(
                        "document.getElementById('floorDbEditorButton').textContent = i18next.t('floorDbEditorButton') || 'Edit Floor Zones';");
                }
            }
            else
            {
                // Enter edit mode (no calibration needed - pixel coordinate based)
                AppLogger.Info("FloorEdit", "Entering floor edit mode...");

                // Get existing zones and floors for current map
                string zonesJson = floorManager?.GetZonesJson(appSettings.latest_map) ?? "[]";
                string floorsJson = floorManager?.GetFloorsJson(appSettings.latest_map) ?? "[]";

                // Enable edit mode with overlay + editor UI
                await jsExecutor.EnableFloorEditModeAsync(zonesJson, floorsJson);
                isFloorEditMode = true;

                // Update button text
                if (webView2_panel_ui.CoreWebView2 != null)
                {
                    await webView2_panel_ui.ExecuteScriptAsync(
                        "document.getElementById('floorDbEditorButton').textContent = i18next.t('floorDbEditorExitButton') || 'Exit Edit Mode';");
                }
            }
        }

        /// <summary>
        /// tarkov-market WebView2 콘텐츠에서 오는 메시지 핸들러 (폴리곤 에디터용)
        /// </summary>
        private async void WebView2Content_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string rawMessage = e.WebMessageAsJson.Trim('"').Replace("\\\"", "\"");
                AppLogger.Debug("WebView2Content", $"Message received: {rawMessage}");

                JObject message = JObject.Parse(rawMessage);
                string action = message["action"]?.ToString() ?? "";

                switch (action.ToLower())
                {
                    case "polygon-vertex-added":
                        // Vertex added on map - currently handled in JS, no C# action needed
                        break;

                    case "quest-toggled":
                        string questName = message["questName"]?.ToString();
                        bool isSelected = message["isSelected"]?.Value<bool>() ?? false;

                        if (!string.IsNullOrEmpty(questName))
                        {
                            if (isSelected)
                                questRepository.AddQuest(appSettings.latest_map, questName);
                            else
                                questRepository.RemoveQuest(appSettings.latest_map, questName);
                        }
                        break;

                    case "save-floor-zones":
                        string zonesData = message["data"]?.ToString() ?? "[]";
                        AppLogger.Info("FloorEdit", $"save-floor-zones received for map: {appSettings.latest_map}");
                        AppLogger.Debug("FloorEdit", $"Zones data length: {zonesData.Length}");

                        floorManager?.UpdateZonesFromJson(appSettings.latest_map, zonesData);

                        // Exit edit mode after save
                        await jsExecutor.DisableFloorEditModeAsync();
                        isFloorEditMode = false;
                        if (webView2_panel_ui.CoreWebView2 != null)
                        {
                            await webView2_panel_ui.ExecuteScriptAsync(
                                "document.getElementById('floorDbEditorButton').textContent = i18next.t('floorDbEditorButton') || 'Edit Floor Zones';");
                        }
                        MessageBox.Show("Zones saved successfully!", "Floor Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;

                    case "exit-floor-edit-mode":
                        await jsExecutor.DisableFloorEditModeAsync();
                        isFloorEditMode = false;
                        if (webView2_panel_ui.CoreWebView2 != null)
                        {
                            await webView2_panel_ui.ExecuteScriptAsync(
                                "document.getElementById('floorDbEditorButton').textContent = i18next.t('floorDbEditorButton') || 'Edit Floor Zones';");
                        }
                        break;

                    default:
                        AppLogger.Debug("WebView2Content", $"Unhandled action: {action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("WebView2Content", $"Message handling error: {ex.Message}");
            }
        }

        private void InitializeLogWatcher()
        {
            // Get log path from settings, or auto-detect if not set
            string logPath = settingsHandler.GetOrFindLogPath();

            if (string.IsNullOrEmpty(logPath))
            {
                AppLogger.Warn("LogWatcher", "Log path not found. Auto map detection will not work.");
                return;
            }

            logWatcher = new LogWatcherService(logPath);
            logWatcher.MapDetected += OnMapDetectedFromLog;
            logWatcher.RaidEnded += OnRaidEndedFromLog;
            if (appSettings.auto_map_detection || appSettings.auto_screenshot_cleanup)
            {
                logWatcher.Start();
            }
        }

        private async void OnRaidEndedFromLog()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnRaidEndedFromLog()));
                return;
            }

            if (!appSettings.auto_screenshot_cleanup) return;
            if (string.IsNullOrEmpty(screenshotPath) || !Directory.Exists(screenshotPath)) return;

            await Task.Delay(3000); // 게임이 마지막 스크린샷 쓰기 완료 대기

            try
            {
                var candidates = new DirectoryInfo(screenshotPath).GetFiles("*.png");

                // 이번 레이드에서 생긴 파일만 지웁니다.
                // 예전에는 폴더의 PNG 를 전부 지워서 보관해 둔 스크린샷까지 날아갔습니다.
                if (raidStartedUtc.HasValue)
                {
                    var since = raidStartedUtc.Value;
                    candidates = candidates.Where(f => f.LastWriteTimeUtc >= since).ToArray();
                }
                else
                {
                    AppLogger.Warn("ScreenshotCleanup",
                        "레이드 시작 시각을 몰라 정리를 건너뜁니다. (맵 자동 감지가 꺼져 있었을 수 있습니다)");
                    return;
                }

                if (candidates.Length == 0) return;

                AppLogger.Info("ScreenshotCleanup", $"이번 레이드 스크린샷 {candidates.Length}개 정리");
                int deleted = 0, failed = 0;
                foreach (var file in candidates)
                {
                    try
                    {
                        // 되돌릴 수 있도록 완전 삭제 대신 휴지통으로 보냅니다.
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            file.FullName,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn("ScreenshotCleanup", $"삭제 실패: {file.Name} - {ex.Message}");
                        failed++;
                    }
                }
                AppLogger.Info("ScreenshotCleanup", $"완료. 휴지통으로 이동: {deleted}, 실패: {failed}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("ScreenshotCleanup", $"정리 실패: {ex.Message}");
            }
            finally
            {
                raidStartedUtc = null;
            }
        }

        /// <summary>이번 레이드가 시작된 시각. 스크린샷 자동 정리 범위를 한정하는 데 씁니다.</summary>
        private DateTime? raidStartedUtc;

        private void OnMapDetectedFromLog(string mapName)
        {
            // Ensure we're on the UI thread
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnMapDetectedFromLog(mapName)));
                return;
            }

            // 같은 맵으로 재입장하는 경우도 있으므로 맵 전환 여부와 무관하게 기록합니다.
            raidStartedUtc = DateTime.UtcNow;

            // Only switch if it's a different map
            if (string.Equals(appSettings.latest_map, mapName, StringComparison.OrdinalIgnoreCase))
                return;

            // Update the panel UI dropdown
            if (webView2_panel_ui.CoreWebView2 != null)
            {
                _ = webView2_panel_ui.ExecuteScriptAsync($"document.getElementById('mapSelect').value = {JavaScriptExecutor.JsLiteral(mapName)};");
            }

            HandleMapSelection(mapName);
        }

        private void UpdateLogWatcherState(bool isEnabled)
        {
            appSettings.auto_map_detection = isEnabled;
            SaveSettings();

            if (logWatcher == null) return;

            if (isEnabled)
            {
                logWatcher.Start();
            }
            else
            {
                if (!appSettings.auto_screenshot_cleanup)
                    logWatcher.Stop();
            }
        }

        // Ctrl+Numpad 핫키 → 층 이름 후보 매핑 (순서대로 시도, 첫 매칭 클릭)
        private static readonly Dictionary<int, string[]> FloorHotkeyMap = new Dictionary<int, string[]>
        {
            { 0, new[] { "Basement", "Bunker" } },   // Numpad 0 → 지하
            { 1, new[] { "Main" } },                 // Numpad 1 → Ground/Main
            { 2, new[] { "Level 2" } },              // Numpad 2 → 2층
            { 3, new[] { "Level 3" } },              // Numpad 3 → 3층
            { 4, new[] { "Level 4" } },              // Numpad 4 → 4층
            { 5, new[] { "Level 5" } }               // Numpad 5 → 5층
        };

        private async void OnFloorHotkeyPressed(int keyIndex)
        {
            if (jsExecutor == null) return;

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnFloorHotkeyPressed(keyIndex)));
                return;
            }

            if (FloorHotkeyMap.TryGetValue(keyIndex, out string[] candidates))
            {
                var result = await jsExecutor.ClickFloorByFirstMatchAsync(candidates);
                if (!result.Matched && result.AvailableLabels.Length > 0)
                {
                    AppLogger.Debug("Floor",
                        $"핫키 {keyIndex} 에 해당하는 층이 없습니다. 이 맵의 라벨: [{string.Join(", ", result.AvailableLabels)}]");
                }
            }
        }

        const int MAX_SLIDING_HEIGHT = 138;
        const int MIN_SLIDING_HEIGHT = 0;
        const int STEP_SLIDING = 10;
        int _posSliding = 138;

        private void checkBoxHide_CheckedChanged(object sender, EventArgs e)
        {
            UpdateFoldButtonText();
            timerSliding.Start();
        }

        private void timerSliding_Tick(object sender, EventArgs e)
        {
            if (checkBoxHide.Checked == true)
            {
                _posSliding -= STEP_SLIDING;
                checkBoxHide.Top = _posSliding;
                if (_posSliding <= MIN_SLIDING_HEIGHT)
                    timerSliding.Stop();
            }
            else
            {
                _posSliding += STEP_SLIDING;
                checkBoxHide.Top = _posSliding;
                if (_posSliding >= MAX_SLIDING_HEIGHT)
                    timerSliding.Stop();

            }

            panel1.Height = _posSliding;
        }
    }
}