using eft_where_am_i.Classes;

namespace eft_where_am_i.Tests;

public class MapCatalogTests
{
    [Theory]
    [InlineData("woods_preset", "woods")]
    [InlineData("bigmap", "customs")]
    [InlineData("shopping_mall", "interchange")]
    [InlineData("rezerv_base_preset", "reserve")]
    [InlineData("tarkovstreets", "streets")]
    [InlineData("factory4_night", "factory")]
    [InlineData("sandbox_high_preset", "ground-zero")]
    [InlineData("laboratory", "lab")]
    public void 알려진_번들_이름을_slug_로_바꾼다(string bundle, string expected)
    {
        Assert.True(MapCatalog.TryResolveSlug(bundle, out var slug));
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData("terminal_preset", "terminal")]
    [InlineData("icebreaker_preset", "icebreaker")]
    [InlineData("labyrinth", "labyrinth")]
    [InlineData("ground_zero", "ground-zero")]
    public void 표에_없어도_접미사를_떼어_slug_를_찾는다(string bundle, string expected)
    {
        // terminal / icebreaker 는 예전에 표에 없어서 자동 감지가 안 됐습니다.
        // 접미사 정규화 폴백이 이런 신규 맵을 잡아줍니다.
        Assert.True(MapCatalog.TryResolveSlug(bundle, out var slug));
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("some_unknown_map")]
    public void 알_수_없는_이름은_실패한다(string bundle)
    {
        Assert.False(MapCatalog.TryResolveSlug(bundle, out _));
    }

    [Fact]
    public void 접미사만_남는_이름을_빈_문자열로_만들지_않는다()
    {
        // "_preset" 을 다 떼면 빈 문자열이 되는 입력에서 무한 루프나 오탐이 없어야 합니다.
        Assert.False(MapCatalog.TryResolveSlug("preset", out _));
        Assert.False(MapCatalog.TryResolveSlug("_preset", out _));
    }

    [Fact]
    public void UI_목록의_모든_slug_는_영문_이름을_가진다()
    {
        foreach (var slug in MapCatalog.Slugs)
        {
            string name = MapCatalog.GetDisplayName(slug, "en");
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.NotEqual(slug, name);   // slug 를 그대로 돌려주면 번역 누락입니다.
        }
    }

    [Fact]
    public void UI_목록의_모든_slug_는_한국어_이름을_가진다()
    {
        foreach (var slug in MapCatalog.Slugs)
        {
            string ko = MapCatalog.GetDisplayName(slug, "ko");
            string en = MapCatalog.GetDisplayName(slug, "en");

            Assert.False(string.IsNullOrWhiteSpace(ko));
            Assert.NotEqual(en, ko);
        }
    }

    [Fact]
    public void 모르는_언어는_영문으로_떨어진다()
    {
        Assert.Equal("Customs", MapCatalog.GetDisplayName("customs", "ja"));
    }

    [Fact]
    public void 신규_맵도_UI_목록에_들어_있다()
    {
        Assert.Contains("terminal", MapCatalog.Slugs);
        Assert.Contains("icebreaker", MapCatalog.Slugs);
    }
}
