using System;
using System.Globalization;

namespace eft_where_am_i.Classes
{
    /// <summary>
    /// EFT 스크린샷 파일명에서 플레이어 좌표를 파싱합니다.
    ///
    /// 형식: yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_speed
    /// 예시: 2026-01-10[03-59]_-318.44, 24.84, -107.49_0.00000, 0.82497, 0.00000, 0.56518_3.98 (0)
    ///
    /// 타르코프는 Unity 기반이라 <b>y 가 높이</b>이고 (x, z) 가 지도 평면입니다.
    /// 층 판정에는 반드시 <see cref="Coordinates.Height"/> 를 써야 합니다.
    /// </summary>
    public static class ScreenshotCoordinates
    {
        public readonly struct Coordinates
        {
            public Coordinates(double x, double height, double z)
            {
                X = x;
                Height = height;
                Z = z;
            }

            /// <summary>지도 평면의 가로축</summary>
            public double X { get; }

            /// <summary>높이 (게임 좌표의 y)</summary>
            public double Height { get; }

            /// <summary>지도 평면의 세로축</summary>
            public double Z { get; }
        }

        /// <summary>
        /// 파일명(확장자 유무 무관)에서 좌표를 파싱합니다. 형식이 다르면 false 를 반환합니다.
        /// </summary>
        public static bool TryParse(string fileName, out Coordinates coordinates)
        {
            coordinates = default;

            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // 좌표 블록은 첫 번째 '_' 뒤에 옵니다.
            string[] parts = fileName.Split('_');
            if (parts.Length < 2)
                return false;

            string[] values = parts[1].Split(',');
            if (values.Length < 3)
                return false;

            if (!TryParseInvariant(values[0], out double x)) return false;
            if (!TryParseInvariant(values[1], out double height)) return false;
            if (!TryParseInvariant(values[2], out double z)) return false;

            coordinates = new Coordinates(x, height, z);
            return true;
        }

        private static bool TryParseInvariant(string value, out double result)
        {
            return double.TryParse(
                value.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}
