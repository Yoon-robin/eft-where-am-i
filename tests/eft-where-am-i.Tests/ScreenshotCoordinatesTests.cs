using System.Globalization;
using eft_where_am_i.Classes;

namespace eft_where_am_i.Tests;

public class ScreenshotCoordinatesTests
{
    // 실제 EFT 스크린샷 파일명.
    // 형식: yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_speed
    private const string RealFileName =
        "2026-01-10[03-59]_-318.44, 24.84, -107.49_0.00000, 0.82497, 0.00000, 0.56518_3.98 (0)";

    [Fact]
    public void 실제_파일명에서_세_좌표를_모두_읽는다()
    {
        Assert.True(ScreenshotCoordinates.TryParse(RealFileName, out var coords));

        Assert.Equal(-318.44, coords.X, precision: 2);
        Assert.Equal(24.84, coords.Height, precision: 2);
        Assert.Equal(-107.49, coords.Z, precision: 2);
    }

    [Fact]
    public void 높이는_두_번째_값이다()
    {
        // 타르코프는 Unity 기반이라 y 가 높이입니다.
        // 이 순서가 뒤집히면 층 판정이 통째로 깨집니다.
        Assert.True(ScreenshotCoordinates.TryParse(RealFileName, out var coords));

        Assert.Equal(24.84, coords.Height, precision: 2);
        Assert.NotEqual(coords.Height, coords.Z);
    }

    [Fact]
    public void 지하_좌표의_음수_높이를_읽는다()
    {
        const string underground =
            "2026-01-10[03-59]_120.00, -18.75, 44.10_0.0, 0.0, 0.0, 1.0_0.00 (0)";

        Assert.True(ScreenshotCoordinates.TryParse(underground, out var coords));
        Assert.Equal(-18.75, coords.Height, precision: 2);
    }

    [Fact]
    public void 확장자가_붙어_있어도_파싱된다()
    {
        Assert.True(ScreenshotCoordinates.TryParse(RealFileName + ".png", out var coords));
        Assert.Equal(-318.44, coords.X, precision: 2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("스크린샷")]
    [InlineData("2026-01-10[03-59]")]                       // 좌표 블록 없음
    [InlineData("2026-01-10[03-59]_12.0, 34.0_0,0,0,1")]    // 좌표가 2개뿐
    [InlineData("2026-01-10[03-59]_a, b, c_0,0,0,1")]       // 숫자가 아님
    public void 형식이_다르면_실패한다(string fileName)
    {
        Assert.False(ScreenshotCoordinates.TryParse(fileName, out _));
    }

    [Fact]
    public void 쉼표를_소수점으로_쓰는_로캘에서도_같은_값을_읽는다()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // 독일어 로캘은 소수점이 쉼표라, 로캘을 따르면 "-318.44" 를 -31844 로 읽습니다.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.True(ScreenshotCoordinates.TryParse(RealFileName, out var coords));
            Assert.Equal(-318.44, coords.X, precision: 2);
            Assert.Equal(24.84, coords.Height, precision: 2);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
