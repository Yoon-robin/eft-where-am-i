using eft_where_am_i.Classes;
using Newtonsoft.Json;

namespace eft_where_am_i.Tests;

public class AppSettingsTests
{
    [Fact]
    public void 저장하고_다시_읽어도_스크린샷_후보_경로가_늘어나지_않는다()
    {
        // Newtonsoft 는 기본적으로 컬렉션을 "교체"가 아니라 "추가"로 역직렬화합니다.
        // 프로퍼티에 기본값 목록이 있으면 저장/로드를 반복할 때마다 4 -> 8 -> 12 개로 불어납니다.
        var settings = new AppSettings();
        int expected = settings.screenshot_paths_list.Count;

        for (int i = 0; i < 3; i++)
        {
            string json = JsonConvert.SerializeObject(settings);
            settings = JsonConvert.DeserializeObject<AppSettings>(json);
        }

        Assert.Equal(expected, settings.screenshot_paths_list.Count);
    }

    [Fact]
    public void 저장하고_다시_읽어도_패널_상태가_중복되지_않는다()
    {
        var settings = new AppSettings();
        settings.panel_hidden_per_map["customs"] = true;

        string json = JsonConvert.SerializeObject(settings);
        var reloaded = JsonConvert.DeserializeObject<AppSettings>(json);

        Assert.Single(reloaded.panel_hidden_per_map);
        Assert.True(reloaded.panel_hidden_per_map["customs"]);
    }

    [Fact]
    public void 기본_스크린샷_후보_경로에_중복이_없다()
    {
        var candidates = AppSettings.DefaultScreenshotPathCandidates();
        Assert.Equal(candidates.Count, candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void 기본_후보_경로에_한국어_문서_폴더가_포함된다()
    {
        var candidates = AppSettings.DefaultScreenshotPathCandidates();
        Assert.Contains(candidates, p => p.Contains("문서"));
        Assert.Contains(candidates, p => p.Contains("OneDrive"));
    }
}
