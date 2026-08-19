using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace eft_where_am_i.Classes
{
    internal class SettingsHandler
    {
        private static SettingsHandler _instance;
        public static SettingsHandler Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SettingsHandler();
                return _instance;
            }
        }

        /// <summary>
        /// 설정 파일 경로: %APPDATA%\eft-where-am-i\settings.json
        ///
        /// 설치 폴더가 아니라 사용자 프로필에 저장합니다.
        ///  - Velopack 업데이트가 앱 폴더를 교체해도 설정이 살아남습니다.
        ///  - 작업 디렉터리(CWD)에 의존하지 않습니다. (예전에는 상대 경로 "assets/settings.json" 이라
        ///    바로가기의 "시작 위치"가 다르면 엉뚱한 곳에 설정이 생겼습니다.)
        /// </summary>
        public static string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "eft-where-am-i",
            "settings.json");

        /// <summary>
        /// 구버전이 설정을 저장하던 위치를 찾습니다. 최초 1회 마이그레이션에만 씁니다.
        ///
        /// 구버전은 상대 경로 "assets/settings.json" 에 저장했고,
        /// Velopack 은 버전마다 app-{version} 폴더를 따로 만들기 때문에
        /// 업데이트 직후에는 설정이 <b>이전 버전 폴더</b>에 남아 있습니다.
        /// 그래서 다음 순서로 찾고, 여러 개면 가장 최근에 수정된 것을 씁니다.
        /// </summary>
        private static string FindLegacySettingsFile()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "settings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "assets", "settings.json"),
            };

            // Velopack 레이아웃: ...\eft-where-am-i\app-2.3.6\assets\settings.json
            try
            {
                var parent = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
                if (parent != null && parent.Exists)
                {
                    foreach (var versionDir in parent.GetDirectories("app-*"))
                        candidates.Add(Path.Combine(versionDir.FullName, "assets", "settings.json"));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Settings", $"이전 버전 폴더 탐색 실패: {ex.Message}");
            }

            string newest = null;
            DateTime newestTime = DateTime.MinValue;

            foreach (var path in candidates)
            {
                try
                {
                    if (!File.Exists(path)) continue;

                    var writeTime = File.GetLastWriteTimeUtc(path);
                    if (writeTime > newestTime)
                    {
                        newestTime = writeTime;
                        newest = path;
                    }
                }
                catch
                {
                    // 접근할 수 없는 후보는 건너뜁니다.
                }
            }

            return newest;
        }

        private readonly string _filePath;
        private readonly object _fileLock = new object();
        private AppSettings _settings;
        public event Action<AppSettings> SettingsChanged;

        private SettingsHandler()
        {
            _filePath = SettingsFilePath;
            EnsureDirectoryExists();
            Load();
        }

        private void EnsureDirectoryExists()
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void Load()
        {
            lock (_fileLock)
            {
                try
                {
                    string sourcePath = File.Exists(_filePath)
                        ? _filePath
                        : FindLegacySettingsFile();   // 구버전 설정 마이그레이션

                    if (sourcePath != null)
                    {
                        string json = File.ReadAllText(sourcePath);
                        _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();

                        if (sourcePath != _filePath)
                            AppLogger.Info("Settings", $"구버전 설정을 이전했습니다: {sourcePath} -> {_filePath}");
                    }
                    else
                    {
                        _settings = new AppSettings();
                    }
                }
                catch (Exception ex)
                {
                    // 설정이 깨졌다고 앱을 못 켜면 곤란하므로 기본값으로 복구합니다.
                    AppLogger.Error("Settings", $"설정 로드 실패, 기본값으로 시작합니다: {ex.Message}");
                    _settings = new AppSettings();
                }

                NormalizeSettings(_settings);
                SaveUnlocked();
            }
        }

        /// <summary>
        /// 앱과 함께 배포되는 값(스크린샷 후보 경로 등)이 비어 있으면 기본값으로 되돌립니다.
        /// 예전 설정 파일에서 넘어온 경우를 대비한 방어 코드입니다.
        /// </summary>
        private static void NormalizeSettings(AppSettings settings)
        {
            if (settings.screenshot_paths_list == null || settings.screenshot_paths_list.Count == 0)
            {
                settings.screenshot_paths_list = AppSettings.DefaultScreenshotPathCandidates();
            }
            else
            {
                // 중복 병합 버그로 이미 부풀어 있는 기존 설정 파일을 정리합니다.
                settings.screenshot_paths_list = settings.screenshot_paths_list
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            settings.panel_hidden_per_map ??= new Dictionary<string, bool>();

            // 데드존은 UI 에서 50~99 만 허용합니다.
            if (settings.dead_zone_percent < 50 || settings.dead_zone_percent > 99)
                settings.dead_zone_percent = 93;
        }

        public void Save()
        {
            lock (_fileLock)
            {
                SaveUnlocked();
            }
        }

        private void SaveUnlocked()
        {
            try
            {
                EnsureDirectoryExists();
                string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);

                // 쓰는 도중 앱이 죽어도 기존 설정이 남도록 임시 파일에 쓴 뒤 교체합니다.
                string tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Settings", $"설정 저장 실패: {ex.Message}");
            }
        }

        public T GetValue<T>(Func<AppSettings, T> selector) => selector(_settings);

        public void SetValue<T>(Action<AppSettings> updater)
        {
            updater(_settings);
            Save();
            SettingsChanged?.Invoke(_settings);
        }

        public AppSettings GetSettings() => _settings;

        public void UpdateSettings(AppSettings newSettings)
        {
            _settings = newSettings ?? new AppSettings();
            NormalizeSettings(_settings);
            Save();
            SettingsChanged?.Invoke(_settings);
        }

        #region Screenshot Path

        /// <summary>
        /// 캐시된 스크린샷 경로를 반환하거나, 없으면 탐색 후 캐시합니다.
        /// </summary>
        public string GetOrFindScreenshotPath()
        {
            if (!string.IsNullOrEmpty(_settings.screenshot_path) && Directory.Exists(_settings.screenshot_path))
                return _settings.screenshot_path;

            return ScreenshotPathSearch();
        }

        /// <summary>
        /// 스크린샷 폴더 탐색:
        /// 1. 내 문서/Escape From Tarkov/Screenshots
        /// 2. UserProfile + settings.json의 screenshot_paths_list
        /// </summary>
        public string ScreenshotPathSearch()
        {
            Exception lastKnownError = null;

            // [전략 1] 내 문서 경로
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string[] folderCandidates = { "Escape From Tarkov", "Escape from Tarkov" };

                foreach (string folderName in folderCandidates)
                {
                    string candidatePath = Path.Combine(documents, folderName, "Screenshots");
                    if (Directory.Exists(candidatePath))
                    {
                        SetValue<string>(s => s.screenshot_path = candidatePath);
                        return candidatePath;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Settings", $"내 문서 기반 스크린샷 경로 탐지 실패: {ex.Message}");
                lastKnownError = ex;
            }

            // [전략 2] UserProfile + JSON 목록 기반 탐지 (폴백)
            try
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrEmpty(homeDirectory) && _settings.screenshot_paths_list != null)
                {
                    foreach (string relativePath in _settings.screenshot_paths_list)
                    {
                        string fullPath = Path.Combine(homeDirectory, relativePath);
                        if (Directory.Exists(fullPath))
                        {
                            SetValue<string>(s => s.screenshot_path = fullPath);
                            return fullPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Settings", $"사용자 프로필 기반 스크린샷 경로 탐지 실패: {ex.Message}");
                lastKnownError = ex;
            }

            if (lastKnownError != null)
                throw new Exception($"스크린샷 폴더 탐색 중 오류 발생: {lastKnownError.Message}", lastKnownError);

            throw new DirectoryNotFoundException(
                "모든 경로에서 'Escape From Tarkov/Screenshots' 폴더를 자동으로 탐지하지 못했습니다. 설정에서 수동으로 지정해주세요.");
        }

        #endregion

        #region Log Path

        /// <summary>
        /// 캐시된 로그 경로를 반환하거나, 없으면 탐색 후 캐시합니다.
        /// </summary>
        public string GetOrFindLogPath()
        {
            if (!string.IsNullOrEmpty(_settings.log_path) && Directory.Exists(_settings.log_path))
                return _settings.log_path;

            return LogPathSearch();
        }

        /// <summary>
        /// EFT 로그 폴더 탐색:
        /// 1. 레지스트리 Uninstall 키의 InstallLocation → Logs
        /// 2. %LOCALAPPDATA%\Battlestate Games\EFT\Logs
        /// 3. 실행 중인 EscapeFromTarkov.exe 프로세스 경로 → Logs
        /// </summary>
        public string LogPathSearch()
        {
            // [전략 1] 레지스트리에서 게임 설치 경로 탐지
            try
            {
                string registryPath = GetLogPathFromRegistry();
                if (!string.IsNullOrEmpty(registryPath))
                {
                    SetValue<string>(s => s.log_path = registryPath);
                    return registryPath;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Settings", $"레지스트리 기반 로그 경로 탐지 실패: {ex.Message}");
            }

            // [전략 2] 런처 버전 %LOCALAPPDATA% 경로
            try
            {
                string launcherPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Battlestate Games", "EFT", "Logs");

                if (Directory.Exists(launcherPath))
                {
                    SetValue<string>(s => s.log_path = launcherPath);
                    return launcherPath;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Settings", $"런처 기본 경로 탐지 실패: {ex.Message}");
            }

            // [전략 3] 실행 중인 프로세스에서 탐지
            try
            {
                string processPath = GetLogPathFromProcess();
                if (!string.IsNullOrEmpty(processPath))
                {
                    SetValue<string>(s => s.log_path = processPath);
                    return processPath;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Settings", $"실행 중인 프로세스 기반 경로 탐지 실패: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 레지스트리 Uninstall 키에서 EFT 설치 경로를 찾아 Logs 폴더를 반환합니다.
        /// HKLM\SOFTWARE\WOW6432Node\...\Uninstall\EscapeFromTarkov → InstallLocation
        /// </summary>
        private string GetLogPathFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov"))
                {
                    string installPath = key?.GetValue("InstallLocation")?.ToString();
                    if (string.IsNullOrEmpty(installPath)) return null;

                    string logsPath = Path.Combine(installPath, "Logs");
                    return Directory.Exists(logsPath) ? logsPath : null;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Settings", $"레지스트리 조회 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 실행 중인 EscapeFromTarkov.exe 프로세스 경로에서 Logs 폴더를 반환합니다.
        /// </summary>
        private string GetLogPathFromProcess()
        {
            try
            {
                var processes = Process.GetProcessesByName("EscapeFromTarkov");
                if (processes.Length > 0)
                {
                    string exePath = processes[0].MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        string logsPath = Path.Combine(Path.GetDirectoryName(exePath), "Logs");
                        if (Directory.Exists(logsPath)) return logsPath;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Settings", $"프로세스 조회 실패: {ex.Message}");
            }
            return null;
        }

        #endregion
    }

    public class AppSettings
    {
        public bool auto_screenshot_detection { get; set; } = false;
        public bool auto_map_detection { get; set; } = false;
        public bool auto_panning { get; set; } = true;
        public bool auto_screenshot_cleanup { get; set; } = false;
        public string language { get; set; } = "en";
        public string theme_mode { get; set; } = "dark";
        public string screenshot_path { get; set; } = string.Empty;
        public string log_path { get; set; } = string.Empty;

        // Replace 가 없으면 Newtonsoft 가 기본값 목록에 파일의 항목을 "덧붙여서"
        // 저장할 때마다 후보 경로가 4 -> 8 -> 12 개로 불어납니다.
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> screenshot_paths_list { get; set; } = DefaultScreenshotPathCandidates();
        public string latest_map { get; set; } = "ground-zero";
        public int dead_zone_percent { get; set; } = 93;

        // ── 자동 스크린샷 ──────────────────────────────
        // 게임이 활성 상태일 때 스크린샷 키를 대신 눌러줍니다.
        // 게임에 입력을 주입하는 동작이라 기본값은 꺼짐입니다.
        public bool auto_screenshot_capture { get; set; } = false;
        public int auto_screenshot_interval_sec { get; set; } = 5;
        public string auto_screenshot_key { get; set; } = "PrintScreen";

        // ── 모바일 레이더 ──────────────────────────────
        public bool mobile_radar_enabled { get; set; } = false;
        public int mobile_radar_port { get; set; } = 8787;

        /// <summary>URL 에 들어가는 접근 코드. 한 번 정해지면 폰의 북마크가 계속 유효하도록 유지합니다.</summary>
        public string mobile_radar_token { get; set; } = string.Empty;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, bool> panel_hidden_per_map { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// floor_db 의 논리적 층 이름 -> tarkov-market 의 실제 레이어 라벨.
        /// 실행 중에 관측한 값을 맵별로 학습해 둡니다. (맵 데이터를 추측하지 않기 위함)
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, string> learned_floor_labels { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// %USERPROFILE% 기준 스크린샷 폴더 후보. 자동 탐지 2차 전략에서 사용합니다.
        /// (한국어 Windows 의 "문서", OneDrive 리다이렉트까지 커버)
        /// </summary>
        public static List<string> DefaultScreenshotPathCandidates() => new List<string>
        {
            @"Documents\Escape from Tarkov\Screenshots\",
            @"문서\Escape from Tarkov\Screenshots\",
            @"OneDrive\Documents\Escape from Tarkov\Screenshots\",
            @"OneDrive\문서\Escape from Tarkov\Screenshots\",
        };
    }
}
