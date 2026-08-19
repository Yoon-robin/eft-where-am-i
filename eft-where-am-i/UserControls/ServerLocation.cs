using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using eft_where_am_i.Classes;

namespace eft_where_am_i
{
    public partial class ServerLocation : UserControl
    {
        private string OFFLINE_MSG = "오프라인 / 매칭안됨";
        private dynamic _langStrings;

        /// <summary>ip-api.com 응답</summary>
        private sealed class GeoInfo
        {
            [JsonProperty("status")] public string Status { get; set; }
            [JsonProperty("message")] public string Message { get; set; }
            [JsonProperty("country")] public string Country { get; set; }
            [JsonProperty("regionName")] public string RegionName { get; set; }
            [JsonProperty("city")] public string City { get; set; }

            public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
        }

        // IP 별 위치 정보를 저장하는 캐시 (앱 구동 중 유지)
        private readonly Dictionary<string, GeoInfo> _geoCache = new Dictionary<string, GeoInfo>();

        // 로그 파일별 IP 파싱 결과 캐시. 파일이 바뀌지 않았으면 다시 스캔하지 않습니다.
        private readonly Dictionary<string, (long length, DateTime lastWrite, string ip)> _logIpCache =
            new Dictionary<string, (long, DateTime, string)>(StringComparer.OrdinalIgnoreCase);

        // 매 줄마다 Regex 를 새로 만들지 않도록 컴파일된 정적 인스턴스를 씁니다.
        private static readonly Regex IpRegex = new Regex(
            @"Ip:\s?(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})", RegexOptions.Compiled);

        // WebClient 는 폐기된 API 라 HttpClient 로 교체했습니다.
        // ip-api.com 무료 플랜은 HTTPS 를 지원하지 않아 http 를 씁니다.
        // 서버 위치 조회용 공개 정보만 오가며 사용자 데이터는 전송하지 않습니다.
        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EFT-Where-Am-I-Desktop-App");
            return client;
        }

        public ServerLocation()
        {
            InitializeComponent();

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);

            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null, dataGridViewHistory, new object[] { true });

            LoadLanguage();
            ApplyTheme();
            SettingsHandler.Instance.SettingsChanged += (s) =>
            {
                LoadLanguage();
                ApplyTheme();
            };
        }

        private void LoadLanguage()
        {
            try
            {
                string lang = SettingsHandler.Instance.GetSettings().language;
                if (string.IsNullOrEmpty(lang)) lang = "en";
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations", $"{lang}.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    _langStrings = JsonConvert.DeserializeObject(json);
                }

                if (_langStrings != null)
                {
                    ApplyTranslations();
                }
            }
            catch (Exception) { }
        }

        private string GetString(string key, string fallback)
        {
            if (_langStrings != null && _langStrings[key] != null)
                return _langStrings[key].ToString();
            return fallback;
        }

        private void ApplyTranslations()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ApplyTranslations));
                return;
            }

            groupBox1.Text = GetString("serverLocation_Title", groupBox1.Text);
            lblHistory.Text = GetString("serverLocation_HistoryTitle", lblHistory.Text);
            btnFindLatest.Text = GetString("serverLocation_BtnFindLatest", btnFindLatest.Text);

            colCheck.HeaderText = GetString("serverLocation_ColCheck", colCheck.HeaderText);
            colCheck.Text = GetString("serverLocation_BtnCheck", colCheck.Text);
            colDate.HeaderText = GetString("serverLocation_ColDate", colDate.HeaderText);
            colIp.HeaderText = GetString("serverLocation_ColIp", colIp.HeaderText);
            colCity.HeaderText = GetString("serverLocation_ColCity", colCity.HeaderText);
            colFolder.HeaderText = GetString("serverLocation_ColFolder", colFolder.HeaderText);

            string oldOfflineMsg = OFFLINE_MSG;
            OFFLINE_MSG = GetString("serverLocation_OfflineCol", OFFLINE_MSG);

            // 기존 표에 들어가 있는 오프라인 메시지도 실시간 변경
            if (oldOfflineMsg != OFFLINE_MSG && dataGridViewHistory.Rows.Count > 0)
            {
                foreach (DataGridViewRow row in dataGridViewHistory.Rows)
                {
                    if (row.Cells["colIp"].Value?.ToString() == oldOfflineMsg)
                    {
                        row.Cells["colIp"].Value = OFFLINE_MSG;
                    }
                }
            }

            if (lblCurrentLogFile.Text.Contains("Current Log") || lblCurrentLogFile.Text.Contains("선택된 로그"))
            {
                lblCurrentLogFile.Text = GetString("serverLocation_CurrentLog", "Current Log: ") + "None";
            }

            ClearGeoLocationLabels();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ApplyTheme));
                return;
            }

            Color background = AppTheme.Background;
            Color foreground = AppTheme.Text;
            Color surface = AppTheme.Surface;

            this.BackColor = background;
            splitContainer1.BackColor = background;
            splitContainer1.Panel1.BackColor = background;
            splitContainer1.Panel2.BackColor = background;

            groupBox1.BackColor = surface;
            groupBox1.ForeColor = foreground;

            lblCurrentLogFile.ForeColor = foreground;
            lblHistory.ForeColor = foreground;
            labelIpAddress.ForeColor = foreground;
            labelCountryName.ForeColor = foreground;
            labelRegionName.ForeColor = foreground;
            labelCityName.ForeColor = foreground;

            AppTheme.StyleButton(btnFindLatest, primary: true);

            dataGridViewHistory.BackgroundColor = surface;
            dataGridViewHistory.BorderStyle = BorderStyle.None;
            dataGridViewHistory.DefaultCellStyle.BackColor = surface;
            dataGridViewHistory.DefaultCellStyle.ForeColor = foreground;
            dataGridViewHistory.DefaultCellStyle.SelectionBackColor = AppTheme.GridSelection;
            dataGridViewHistory.DefaultCellStyle.SelectionForeColor = AppTheme.TextStrong;
            dataGridViewHistory.GridColor = AppTheme.Border;
            dataGridViewHistory.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.SurfaceAlt;
            dataGridViewHistory.ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.Muted;
            dataGridViewHistory.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppTheme.SurfaceAlt;
            dataGridViewHistory.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridViewHistory.RowHeadersVisible = false;
            dataGridViewHistory.EnableHeadersVisualStyles = false;

            foreach (Control control in Controls)
            {
                if (control is Label label && label != lblCurrentLogFile && label != lblHistory)
                {
                    label.ForeColor = foreground;
                }
            }
        }

        private async void ServerLocation_Load(object sender, EventArgs e)
        {
            if (this.DesignMode) return;
            await LoadAllHistoryAsync();
        }

        private async void btnFindLatest_Click(object sender, EventArgs e)
        {
            btnFindLatest.Enabled = false;
            btnFindLatest.Text = "...";
            await LoadAllHistoryAsync();
            btnFindLatest.Text = GetString("serverLocation_BtnFindLatest", "최신 접속 갱신");
            btnFindLatest.Enabled = true;
        }

        private async Task LoadAllHistoryAsync()
        {
            try
            {
                string logsFolderPath = SettingsHandler.Instance.GetOrFindLogPath();

                if (string.IsNullOrEmpty(logsFolderPath) || !Directory.Exists(logsFolderPath))
                {
                    return;
                }

                dataGridViewHistory.Rows.Clear();

                string checkLabel = GetString("serverLocation_BtnCheck", "확인하기");

                // 파일 스캔은 백그라운드에서 끝내고 그리드에는 한 번에 반영합니다.
                // (예전에는 폴더 하나당 Invoke 를 한 번씩 호출했습니다)
                var rows = await Task.Run(() =>
                {
                    var collected = new List<object[]>();

                    var dirs = new DirectoryInfo(logsFolderPath)
                        .GetDirectories()
                        .OrderByDescending(d => d.CreationTime)
                        .ToList();

                    foreach (var dir in dirs)
                    {
                        var latestFile = dir.GetFiles("*application*.log")
                            .OrderByDescending(f => f.LastWriteTime)
                            .FirstOrDefault();

                        string ip = OFFLINE_MSG;
                        string cityVal = "";

                        if (latestFile != null)
                        {
                            ip = GetMatchedIpAddress(latestFile.FullName) ?? OFFLINE_MSG;
                        }

                        // 캐시에 있는 경우 도시명 바로 주입
                        if (ip != OFFLINE_MSG && _geoCache.TryGetValue(ip, out var cachedGeo))
                        {
                            cityVal = cachedGeo.City;
                        }

                        collected.Add(new object[]
                        {
                            checkLabel,
                            dir.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            ip,
                            cityVal,
                            dir.Name
                        });
                    }

                    return collected;
                });

                foreach (var row in rows)
                {
                    dataGridViewHistory.Rows.Add(row);
                }

                if (dataGridViewHistory.Rows.Count > 0)
                {
                    var firstRow = dataGridViewHistory.Rows[0];
                    string latestIp = firstRow.Cells["colIp"].Value?.ToString();
                    string folderName = firstRow.Cells["colFolder"].Value?.ToString();

                    lblCurrentLogFile.Text = GetString("serverLocation_CurrentLog", "선택된 로그: ") + $"{folderName} (*application*.log)";

                    if (string.IsNullOrEmpty(latestIp) || !IPAddress.TryParse(latestIp, out _))
                    {
                        ClearGeoLocationLabels();
                        labelIpAddress.Text = GetString("serverLocation_IpText", "IP 주소 : ") + OFFLINE_MSG;
                        labelCountryName.Text = GetString("serverLocation_OfflineInfo", "해당 세션은 오프라인 게임이거나 아직 서버가 잡히지 않았습니다.");
                    }
                    else
                    {
                        await UpdateGeoLocationAsync(latestIp, firstRow);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("ServerLocation", $"접속 기록 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 로그 파일에서 마지막으로 매칭된 서버 IP 를 찾습니다.
        /// 파일 크기와 수정 시각이 그대로면 이전 결과를 재사용합니다.
        /// (갱신할 때마다 로그 폴더 전체를 처음부터 다시 스캔하던 문제)
        /// </summary>
        private string GetMatchedIpAddress(string logFilePath)
        {
            try
            {
                var info = new FileInfo(logFilePath);

                lock (_logIpCache)
                {
                    if (_logIpCache.TryGetValue(logFilePath, out var cached)
                        && cached.length == info.Length
                        && cached.lastWrite == info.LastWriteTimeUtc)
                    {
                        return cached.ip;
                    }
                }

                string matchedIpAddress = null;

                using (var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(fileStream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Match match = IpRegex.Match(line);
                        if (match.Success)
                        {
                            matchedIpAddress = match.Groups[1].Value;
                        }
                    }
                }

                lock (_logIpCache)
                {
                    _logIpCache[logFilePath] = (info.Length, info.LastWriteTimeUtc, matchedIpAddress);
                }

                return matchedIpAddress;
            }
            catch (Exception ex)
            {
                AppLogger.Debug("ServerLocation", $"로그 IP 파싱 실패 ({Path.GetFileName(logFilePath)}): {ex.Message}");
                return null;
            }
        }

        private async void dataGridViewHistory_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == colCheck.Index)
            {
                var row = dataGridViewHistory.Rows[e.RowIndex];
                string ip = row.Cells["colIp"].Value?.ToString();
                string folderName = row.Cells["colFolder"].Value?.ToString();

                lblCurrentLogFile.Text = GetString("serverLocation_CurrentLog", "선택된 로그: ") + $"{folderName} (*application*.log)";

                if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out _))
                {
                    ClearGeoLocationLabels();
                    labelIpAddress.Text = GetString("serverLocation_IpText", "IP 주소 : ") + OFFLINE_MSG;
                    labelCountryName.Text = GetString("serverLocation_OfflineInfo", "해당 세션은 오프라인 게임이거나 아직 매칭된 서버가 없습니다.");
                    row.Cells["colCity"].Value = "-";
                }
                else
                {
                    await UpdateGeoLocationAsync(ip, row);
                }
            }
        }

        private void ClearGeoLocationLabels()
        {
            labelIpAddress.Text = GetString("serverLocation_IpText", "IP 주소 : ");
            labelCountryName.Text = GetString("serverLocation_CountryText", "국가 : ");
            labelRegionName.Text = GetString("serverLocation_RegionText", "지역 : ");
            labelCityName.Text = GetString("serverLocation_CityText", "도시 : ");
        }

        private async Task UpdateGeoLocationAsync(string ipAddress, DataGridViewRow targetRow = null)
        {
            try
            {
                labelIpAddress.Text = GetString("serverLocation_IpText", "IP 주소 : ") + ipAddress;

                if (!IPAddress.TryParse(ipAddress, out _))
                {
                    labelCountryName.Text = GetString("serverLocation_OfflineInfo", "해당 세션은 오프라인 게임이거나 아직 서버가 잡히지 않았습니다.");
                    labelRegionName.Text = GetString("serverLocation_RegionText", "지역 : ");
                    labelCityName.Text = GetString("serverLocation_CityText", "도시 : ");
                    return;
                }

                GeoInfo geoInfo;

                // 캐시 확인
                if (!_geoCache.TryGetValue(ipAddress, out geoInfo))
                {
                    string apiUrl = $"http://ip-api.com/json/{ipAddress}";
                    string response = await Http.GetStringAsync(apiUrl);
                    geoInfo = JsonConvert.DeserializeObject<GeoInfo>(response);

                    if (geoInfo != null && geoInfo.IsSuccess)
                    {
                        _geoCache[ipAddress] = geoInfo;
                    }
                }

                if (geoInfo != null)
                {
                    if (geoInfo.IsSuccess)
                    {
                        labelCountryName.Text = GetString("serverLocation_CountryText", "국가 : ") + geoInfo.Country;
                        labelRegionName.Text = GetString("serverLocation_RegionText", "지역 : ") + geoInfo.RegionName;
                        labelCityName.Text = GetString("serverLocation_CityText", "도시 : ") + geoInfo.City;

                        if (targetRow != null)
                        {
                            targetRow.Cells["colCity"].Value = geoInfo.City;
                        }
                    }
                    else
                    {
                        labelCountryName.Text = GetString("serverLocation_StatusFail", "상태 : 실패 (API 오류)");
                        labelRegionName.Text = GetString("serverLocation_Reason", "사유 : ") + geoInfo.Message;
                        labelCityName.Text = GetString("serverLocation_CityText", "도시 : ");

                        if (targetRow != null)
                        {
                            targetRow.Cells["colCity"].Value = "(실패)";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("ServerLocation", $"위치 조회 실패 ({ipAddress}): {ex.Message}");
                labelCountryName.Text = GetString("serverLocation_Error", "오류 발생");
                labelRegionName.Text = GetString("serverLocation_Error", "오류 발생");
                labelCityName.Text = GetString("serverLocation_Error", "오류 발생");

                if (targetRow != null)
                {
                    targetRow.Cells["colCity"].Value = "(오류)";
                }
            }
        }
    }
}
