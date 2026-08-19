using System.Windows.Forms;
using eft_where_am_i.Classes;

namespace eft_where_am_i.Tests;

public class AutoScreenshotServiceTests
{
    [Fact]
    public void 기본_키는_타르코프_기본값인_PrintScreen_이다()
    {
        using var service = new AutoScreenshotService();
        Assert.Equal(Keys.PrintScreen, service.ScreenshotKey);
    }

    [Theory]
    [InlineData("PrintScreen", Keys.PrintScreen)]
    [InlineData("printscreen", Keys.PrintScreen)]
    [InlineData("  F12  ", Keys.F12)]
    [InlineData("Insert", Keys.Insert)]
    public void 키_이름을_파싱한다(string name, Keys expected)
    {
        using var service = new AutoScreenshotService();
        service.SetKeyFromName(name);
        Assert.Equal(expected, service.ScreenshotKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("존재하지않는키")]
    public void 알_수_없는_키_이름은_PrintScreen_으로_돌아간다(string name)
    {
        using var service = new AutoScreenshotService();
        service.SetKeyFromName("F12");
        service.SetKeyFromName(name);
        Assert.Equal(Keys.PrintScreen, service.ScreenshotKey);
    }

    [Theory]
    [InlineData(0, AutoScreenshotService.MinIntervalSeconds)]
    [InlineData(1, AutoScreenshotService.MinIntervalSeconds)]
    [InlineData(-30, AutoScreenshotService.MinIntervalSeconds)]
    [InlineData(999, AutoScreenshotService.MaxIntervalSeconds)]
    [InlineData(5, 5)]
    public void 간격은_허용_범위로_제한된다(int input, int expected)
    {
        using var service = new AutoScreenshotService();
        service.IntervalSeconds = input;
        Assert.Equal(expected, service.IntervalSeconds);
    }

    [Fact]
    public void 시작하지_않은_상태에서_중지해도_안전하다()
    {
        using var service = new AutoScreenshotService();
        service.Stop();
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void 시작과_중지가_상태에_반영된다()
    {
        using var service = new AutoScreenshotService();

        service.Start();
        Assert.True(service.IsRunning);

        service.Start();                 // 중복 호출은 무시되어야 합니다.
        Assert.True(service.IsRunning);

        service.Stop();
        Assert.False(service.IsRunning);
    }
}

public class MobileRadarServerTests
{
    [Fact]
    public void 접근_코드는_8자리이다()
    {
        Assert.Equal(8, MobileRadarServer.GenerateToken().Length);
    }

    [Fact]
    public void 접근_코드는_헷갈리는_문자를_쓰지_않는다()
    {
        // 폰에서 직접 입력하는 값이라 0/O, 1/l 같은 문자는 제외합니다.
        const string forbidden = "01iloO";

        for (int i = 0; i < 200; i++)
        {
            string token = MobileRadarServer.GenerateToken();
            Assert.DoesNotContain(token, c => forbidden.Contains(c));
            Assert.All(token, c => Assert.True(char.IsLetterOrDigit(c)));
        }
    }

    [Fact]
    public void 접근_코드는_매번_다르다()
    {
        var tokens = new HashSet<string>();
        for (int i = 0; i < 100; i++) tokens.Add(MobileRadarServer.GenerateToken());

        // 8자리 * 31종 문자면 충돌 확률이 사실상 0 입니다.
        Assert.Equal(100, tokens.Count);
    }

    [Fact]
    public void 로컬_주소_조회가_예외를_던지지_않는다()
    {
        var addresses = MobileRadarServer.GetLocalAddresses();
        Assert.NotNull(addresses);

        // 주소가 잡혔다면 전부 IPv4 형식이어야 합니다.
        Assert.All(addresses, ip => Assert.True(System.Net.IPAddress.TryParse(ip, out _)));
        Assert.DoesNotContain("127.0.0.1", addresses);
    }

    [Fact]
    public void 시작하지_않은_서버는_실행중이_아니다()
    {
        using var server = new MobileRadarServer();
        Assert.False(server.IsRunning);
        Assert.False(server.HasRecentClient);

        server.Stop();   // 시작 전 중지는 무시되어야 합니다.
        Assert.False(server.IsRunning);
    }
}

public class ScanCodeTests
{
    [Fact]
    public void PrintScreen_은_실제_키보드와_같은_스캔코드를_쓴다()
    {
        // MapVirtualKey 는 0x54(SysRq)를 돌려주지만 실제 키보드는 E0 37 을 보냅니다.
        // 0x54 로 주입하면 키가 아예 먹지 않아 자동 촬영이 조용히 실패합니다.
        Assert.Equal(0x37, AutoScreenshotService.GetScanCode(Keys.PrintScreen));
    }

    [Theory]
    [InlineData(Keys.F9)]
    [InlineData(Keys.F12)]
    [InlineData(Keys.Insert)]
    public void 보정이_필요없는_키는_스캔코드를_구할_수_있다(Keys key)
    {
        Assert.NotEqual(0, AutoScreenshotService.GetScanCode(key));
    }
}
