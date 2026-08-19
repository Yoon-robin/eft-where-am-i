using System;
using System.Text.RegularExpressions;

namespace eft_where_am_i.Classes
{
    /// <summary>게임 로그에서 뽑아낸 서버 주소</summary>
    public readonly record struct ServerEndpoint(string Ip, string Port)
    {
        public static readonly ServerEndpoint None = new ServerEndpoint(null, null);

        public bool HasIp => !string.IsNullOrEmpty(Ip);
        public bool HasPort => !string.IsNullOrEmpty(Port);
    }

    /// <summary>
    /// EFT 로그에서 매칭된 서버의 IP 와 포트를 읽습니다.
    ///
    /// 실제 줄 형태:
    /// <code>
    /// ...|Debug|application|TRACE-NetworkGameCreate profileStatus: 'Profileid: ..., Status: Busy,
    /// RaidMode: Online, Ip: 173.201.39.97, Port: 17005, Location: factory4_day, ...'
    /// </code>
    /// </summary>
    public static class ServerLogParser
    {
        // 포트가 없는 줄도 있을 수 있어 포트 부분은 선택으로 둡니다.
        private static readonly Regex EndpointRegex = new Regex(
            @"Ip:\s*(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})(?:\s*,\s*Port:\s*(\d{1,5}))?",
            RegexOptions.Compiled);

        /// <summary>한 줄에서 서버 주소를 읽습니다. 없으면 <see cref="ServerEndpoint.None"/>.</summary>
        public static ServerEndpoint ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return ServerEndpoint.None;

            var match = EndpointRegex.Match(line);
            if (!match.Success) return ServerEndpoint.None;

            return new ServerEndpoint(
                match.Groups[1].Value,
                match.Groups[2].Success ? match.Groups[2].Value : null);
        }
    }
}
