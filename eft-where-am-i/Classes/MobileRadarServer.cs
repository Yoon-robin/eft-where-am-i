using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eft_where_am_i.Classes
{
    /// <summary>
    /// 같은 네트워크의 휴대폰에서 맵 화면을 볼 수 있게 해주는 초경량 HTTP 서버.
    ///
    /// 데스크톱 WebView2 에 이미 마커까지 그려진 화면이 있으므로, 그 화면을 JPEG 로 캡처해
    /// 그대로 내보냅니다. 맵 이미지를 따로 복제하거나 좌표 변환을 다시 구현할 필요가 없습니다.
    ///
    /// <para>
    /// HttpListener 를 쓰지 않는 이유: localhost 가 아닌 주소에 바인딩하려면 관리자 권한이나
    /// netsh urlacl 등록이 필요합니다. TcpListener 는 그런 제약이 없습니다.
    /// (다만 Windows 방화벽은 최초 1회 허용을 물어봅니다.)
    /// </para>
    /// </summary>
    public class MobileRadarServer : IDisposable
    {
        public const int DefaultPort = 8787;

        /// <summary>이 시간 안에 요청이 있었으면 폰이 보고 있는 것으로 간주합니다.</summary>
        private static readonly TimeSpan ClientActiveWindow = TimeSpan.FromSeconds(12);

        private readonly object _gate = new object();
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private byte[] _currentFrame = Array.Empty<byte>();
        private long _frameId;
        private DateTime _lastClientUtc = DateTime.MinValue;
        private DateTime _lastRejectLogUtc = DateTime.MinValue;
        private bool _disposed;

        public int Port { get; private set; } = DefaultPort;

        /// <summary>URL 에 들어가는 접근 코드. 꺼두면 IP 주소로 바로 접속합니다.</summary>
        public string Token { get; private set; } = string.Empty;

        /// <summary>
        /// 접근 코드를 요구할지 여부.
        /// 끄면 http://192.168.x.x:8787/ 로 바로 들어갈 수 있어 폰에서 입력이 편합니다.
        /// 대신 같은 네트워크에 있는 사람은 누구나 화면을 볼 수 있습니다.
        /// </summary>
        public bool RequireToken { get; private set; }

        public bool IsRunning { get; private set; }

        /// <summary>현재 표시 중인 맵 이름 (폰 화면 상단에 표시)</summary>
        public string CurrentMapLabel { get; set; } = string.Empty;

        /// <summary>최근에 폰이 화면을 받아갔는지. 캡처를 계속할지 판단하는 데 씁니다.</summary>
        public bool HasRecentClient
        {
            get
            {
                lock (_gate)
                {
                    return DateTime.UtcNow - _lastClientUtc < ClientActiveWindow;
                }
            }
        }

        public void Start(int port, string token, bool requireToken)
        {
            lock (_gate)
            {
                if (_disposed || IsRunning) return;

                Port = port <= 0 || port > 65535 ? DefaultPort : port;
                RequireToken = requireToken;
                Token = string.IsNullOrWhiteSpace(token) ? GenerateToken() : token.Trim();

                try
                {
                    _listener = new TcpListener(IPAddress.Any, Port);
                    _listener.Start();
                }
                catch (Exception ex)
                {
                    _listener = null;
                    AppLogger.Error("Radar", $"포트 {Port} 에서 서버를 시작하지 못했습니다: {ex.Message}");
                    throw;
                }

                _cts = new CancellationTokenSource();
                IsRunning = true;
            }

            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
            AppLogger.Info("Radar", $"모바일 레이더 시작: {GetPrimaryUrl()}");

            // 방화벽이 막고 있으면 서버는 멀쩡히 떠도 폰에서 접속되지 않습니다.
            // 원인을 찾기 어려운 상황이라 시작할 때 미리 확인해 둡니다.
            _ = Task.Run(CheckFirewallAsync);
        }

        /// <summary>
        /// 이 포트를 허용하는 인바운드 방화벽 규칙이 있는지 확인합니다.
        /// 규칙이 없으면 폰에서 접속이 안 되므로 로그에 해결 방법을 남깁니다.
        /// </summary>
        private async Task CheckFirewallAsync()
        {
            try
            {
                bool allowed = await Task.Run(() => HasInboundRule(Port));
                if (allowed)
                {
                    AppLogger.Debug("Radar", $"포트 {Port} 인바운드 방화벽 규칙을 확인했습니다.");
                    return;
                }

                FirewallRuleMissing = true;
                AppLogger.Warn("Radar",
                    $"포트 {Port} 을 허용하는 방화벽 규칙이 없습니다. 폰에서 접속되지 않을 수 있습니다. " +
                    $"관리자 PowerShell 에서 다음을 실행하세요: {GetFirewallCommand()}");
            }
            catch (Exception ex)
            {
                AppLogger.Debug("Radar", $"방화벽 확인 실패: {ex.Message}");
            }
        }

        /// <summary>방화벽 규칙이 없어 보이면 true. UI 안내에 씁니다.</summary>
        public bool FirewallRuleMissing { get; private set; }

        /// <summary>사용자가 관리자 권한으로 실행하면 되는 방화벽 등록 명령.</summary>
        public string GetFirewallCommand()
        {
            return $"New-NetFirewallRule -DisplayName \"EFT Where Am I - Mobile Radar\" " +
                   $"-Direction Inbound -Action Allow -Protocol TCP -LocalPort {Port} " +
                   $"-Profile Private -RemoteAddress LocalSubnet";
        }

        private static bool HasInboundRule(int port)
        {
            // netsh 출력에서 해당 포트를 허용하는 인바운드 규칙을 찾습니다.
            // COM 인터롭(NetFwTypeLib)보다 의존성이 적고 권한도 필요 없습니다.
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "advfirewall firewall show rule name=all dir=in",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return true;   // 확인 불가 시 경고하지 않음

            string output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(8000)) return true;

            // 규칙 블록 단위로 끊어서, 허용(Allow)이면서 해당 포트를 담은 블록을 찾습니다.
            string portToken = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var block in output.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (block.IndexOf(portToken, StringComparison.Ordinal) < 0) continue;

                // 로케일에 따라 Allow / 허용 으로 표시됩니다.
                bool isAllow = block.IndexOf("Allow", StringComparison.OrdinalIgnoreCase) >= 0
                            || block.Contains("허용");
                bool isEnabled = block.IndexOf("Yes", StringComparison.OrdinalIgnoreCase) >= 0
                              || block.Contains("예");

                if (isAllow && isEnabled) return true;
            }

            return false;
        }

        public void Stop()
        {
            CancellationTokenSource cts;
            TcpListener listener;

            lock (_gate)
            {
                if (!IsRunning) return;
                IsRunning = false;
                cts = _cts;
                listener = _listener;
                _cts = null;
                _listener = null;
            }

            try { cts?.Cancel(); } catch { }
            try { listener?.Stop(); } catch { }
            try { cts?.Dispose(); } catch { }

            AppLogger.Info("Radar", "모바일 레이더 중지");
        }

        /// <summary>최신 화면을 게시합니다. UI 스레드에서 캡처한 JPEG 바이트를 넘겨주세요.</summary>
        public void PublishFrame(byte[] jpeg)
        {
            if (jpeg == null || jpeg.Length == 0) return;

            lock (_gate)
            {
                _currentFrame = jpeg;
                _frameId++;
            }
        }

        public static string GenerateToken()
        {
            // 폰에서 한 번 입력하면 끝이라 짧고 헷갈리지 않는 문자만 씁니다. (0/O, 1/l 제외)
            const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(8);
            var chars = bytes.Select(b => alphabet[b % alphabet.Length]).ToArray();
            return new string(chars);
        }

        /// <summary>폰 주소창에 넣을 URL. 사설망 주소를 우선으로 고릅니다.</summary>
        public string GetPrimaryUrl()
        {
            string host = GetLocalAddresses().FirstOrDefault() ?? "localhost";
            return BuildUrl(host, Port, Token, RequireToken);
        }

        public IReadOnlyList<string> GetAllUrls()
        {
            return GetLocalAddresses().Select(ip => BuildUrl(ip, Port, Token, RequireToken)).ToList();
        }

        /// <summary>서버가 꺼져 있을 때도 설정값만으로 주소를 만들 수 있게 합니다. (설정 화면용)</summary>
        public static string BuildUrl(string host, int port, string token, bool requireToken)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;

            return requireToken && !string.IsNullOrWhiteSpace(token)
                ? $"http://{host}:{port}/{token}/"
                : $"http://{host}:{port}/";
        }

        /// <summary>
        /// 이 PC 의 사설망 IPv4 주소 목록. 가상 어댑터(Hyper-V, WSL 등)는 뒤로 밀어냅니다.
        /// </summary>
        public static IReadOnlyList<string> GetLocalAddresses()
        {
            var results = new List<(string ip, int rank)>();

            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    bool isPhysical = nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                        or NetworkInterfaceType.Wireless80211
                        or NetworkInterfaceType.GigabitEthernet;

                    bool looksVirtual = nic.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0
                        || nic.Description.IndexOf("hyper-v", StringComparison.OrdinalIgnoreCase) >= 0
                        || nic.Description.IndexOf("vmware", StringComparison.OrdinalIgnoreCase) >= 0
                        || nic.Name.IndexOf("WSL", StringComparison.OrdinalIgnoreCase) >= 0;

                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(addr.Address)) continue;

                        string ip = addr.Address.ToString();
                        if (ip.StartsWith("169.254.", StringComparison.Ordinal)) continue;   // APIPA

                        int rank = 0;
                        if (!IsPrivate(addr.Address)) rank += 4;
                        if (looksVirtual) rank += 2;
                        if (!isPhysical) rank += 1;

                        results.Add((ip, rank));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Radar", $"네트워크 주소 조회 실패: {ex.Message}");
            }

            return results.OrderBy(r => r.rank).Select(r => r.ip).Distinct().ToList();
        }

        private static bool IsPrivate(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168);
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    var listener = _listener;
                    if (listener == null) break;

                    client = await listener.AcceptTcpClientAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) break;
                    AppLogger.Debug("Radar", $"연결 수락 실패: {ex.Message}");
                    continue;
                }

                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                try
                {
                    client.NoDelay = true;
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 15000;

                    // 소켓을 그냥 닫으면 송신 버퍼에 남은 데이터가 버려져서
                    // 큰 JPEG 이 중간에 잘립니다. 보낼 것을 다 보내고 닫도록 합니다.
                    client.LingerState = new LingerOption(true, 10);

                    using var stream = client.GetStream();

                    string requestLine = await ReadRequestLineAsync(stream, token);
                    if (string.IsNullOrEmpty(requestLine)) return;

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2)
                    {
                        await WriteSimpleAsync(stream, 400, "text/plain; charset=utf-8", "Bad Request", token);
                        return;
                    }

                    string method = parts[0];
                    string rawPath = parts[1];

                    if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteSimpleAsync(stream, 405, "text/plain; charset=utf-8", "Method Not Allowed", token);
                        return;
                    }

                    await RouteAsync(stream, rawPath, token);

                    // FIN 을 보내 정상 종료를 알립니다. 이게 없으면 클라이언트가
                    // 응답을 다 받기 전에 연결이 끊긴 것으로 보게 됩니다.
                    try { client.Client.Shutdown(SocketShutdown.Send); } catch { }
                }
                catch (Exception ex)
                {
                    AppLogger.Debug("Radar", $"요청 처리 실패: {ex.Message}");
                }
            }
        }

        private async Task RouteAsync(NetworkStream stream, string rawPath, CancellationToken token)
        {
            // 쿼리스트링 제거
            int q = rawPath.IndexOf('?');
            string path = q >= 0 ? rawPath.Substring(0, q) : rawPath;
            path = path.Trim('/');

            string[] segments = path.Length == 0
                ? Array.Empty<string>()
                : path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            string resource;

            if (!RequireToken)
            {
                // 접근 코드를 쓰지 않는 경우: /  ->  페이지,  /frame.jpg  ->  화면
                TouchClient();

                string first = segments.Length > 0 ? segments[0].ToLowerInvariant() : string.Empty;

                if (KnownResources.Contains(first))
                {
                    resource = first;
                }
                else
                {
                    // 예전에 접근 코드를 쓰던 주소(/코드/, /코드/frame.jpg)로 들어온 경우입니다.
                    // 폰 브라우저는 옛 주소를 계속 자동완성하므로 404 대신 그냥 받아줍니다.
                    resource = segments.Length > 1 ? segments[1].ToLowerInvariant() : string.Empty;
                }

                await ServeResourceAsync(stream, resource, token);
                return;
            }

            if (segments.Length == 0)
            {
                await WriteSimpleAsync(stream, 404, "text/html; charset=utf-8",
                    "<h1>EFT Where Am I</h1><p>주소 끝에 접근 코드를 붙여주세요. 데스크톱 앱의 설정 화면에서 전체 주소를 확인할 수 있습니다.</p>",
                    token);
                return;
            }

            if (!string.Equals(segments[0], Token, StringComparison.Ordinal))
            {
                // 예전 주소를 열어둔 폰이 계속 재시도하면 로그가 도배되므로
                // 같은 내용은 1분에 한 번만 남깁니다.
                var now = DateTime.UtcNow;
                lock (_gate)
                {
                    if (now - _lastRejectLogUtc > TimeSpan.FromMinutes(1))
                    {
                        _lastRejectLogUtc = now;
                        AppLogger.Debug("Radar", "잘못된 접근 코드로 요청이 들어왔습니다. (주소가 바뀌었다면 폰에서 새 주소를 열어주세요)");
                    }
                }

                await WriteSimpleAsync(stream, 403, "text/plain; charset=utf-8", "Forbidden", token);
                return;
            }

            TouchClient();

            resource = segments.Length > 1 ? segments[1].ToLowerInvariant() : string.Empty;
            await ServeResourceAsync(stream, resource, token);
        }

        /// <summary>서버가 실제로 제공하는 경로. 그 외 첫 세그먼트는 접근 코드로 간주합니다.</summary>
        private static readonly HashSet<string> KnownResources =
            new HashSet<string>(StringComparer.Ordinal) { "", "frame.jpg", "status", "favicon.ico" };

        private async Task ServeResourceAsync(NetworkStream stream, string resource, CancellationToken token)
        {
            switch (resource)
            {
                case "":
                    await ServePageAsync(stream, token);
                    break;

                case "frame.jpg":
                    await ServeFrameAsync(stream, token);
                    break;

                case "status":
                    await ServeStatusAsync(stream, token);
                    break;

                case "favicon.ico":
                    await WriteSimpleAsync(stream, 404, "text/plain; charset=utf-8", "", token);
                    break;

                default:
                    await WriteSimpleAsync(stream, 404, "text/plain; charset=utf-8", "Not Found", token);
                    break;
            }
        }

        private void TouchClient()
        {
            lock (_gate)
            {
                _lastClientUtc = DateTime.UtcNow;
            }
        }

        private async Task ServePageAsync(NetworkStream stream, CancellationToken token)
        {
            string html;
            try
            {
                string pagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html", "mobile.html");
                html = File.Exists(pagePath)
                    ? await File.ReadAllTextAsync(pagePath, Encoding.UTF8, token)
                    : "<h1>mobile.html 을 찾을 수 없습니다.</h1>";
            }
            catch (Exception ex)
            {
                html = $"<h1>페이지 로드 실패</h1><p>{WebUtility.HtmlEncode(ex.Message)}</p>";
            }

            await WriteSimpleAsync(stream, 200, "text/html; charset=utf-8", html, token);
        }

        private async Task ServeFrameAsync(NetworkStream stream, CancellationToken token)
        {
            byte[] frame;
            lock (_gate)
            {
                frame = _currentFrame;
            }

            if (frame.Length == 0)
            {
                await WriteSimpleAsync(stream, 503, "text/plain; charset=utf-8", "No frame yet", token);
                return;
            }

            var header = Encoding.UTF8.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: image/jpeg\r\n" +
                $"Content-Length: {frame.Length}\r\n" +
                "Cache-Control: no-store, no-cache, must-revalidate\r\n" +
                "Connection: close\r\n\r\n");

            await stream.WriteAsync(header, token);
            await stream.WriteAsync(frame, token);
            await stream.FlushAsync(token);
        }

        private async Task ServeStatusAsync(NetworkStream stream, CancellationToken token)
        {
            long frameId;
            int size;
            lock (_gate)
            {
                frameId = _frameId;
                size = _currentFrame.Length;
            }

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                frameId,
                frameBytes = size,
                map = CurrentMapLabel ?? string.Empty,
            });

            await WriteSimpleAsync(stream, 200, "application/json; charset=utf-8", json, token);
        }

        private static async Task<string> ReadRequestLineAsync(NetworkStream stream, CancellationToken token)
        {
            var buffer = new byte[1];
            var line = new StringBuilder(256);

            while (line.Length < 8192)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, 1), token);
                if (read == 0) break;

                char c = (char)buffer[0];
                if (c == '\n') break;
                if (c != '\r') line.Append(c);
            }

            return line.ToString();
        }

        private static async Task WriteSimpleAsync(
            NetworkStream stream, int status, string contentType, string body, CancellationToken token)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body);
            string reason = status switch
            {
                200 => "OK",
                400 => "Bad Request",
                403 => "Forbidden",
                404 => "Not Found",
                405 => "Method Not Allowed",
                503 => "Service Unavailable",
                _ => "OK",
            };

            var header = Encoding.UTF8.GetBytes(
                $"HTTP/1.1 {status} {reason}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {payload.Length}\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n\r\n");

            await stream.WriteAsync(header, token);
            await stream.WriteAsync(payload, token);
            await stream.FlushAsync(token);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
