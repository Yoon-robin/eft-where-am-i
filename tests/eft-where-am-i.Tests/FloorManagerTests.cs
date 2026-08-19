using System;
using System.IO;
using eft_where_am_i.Classes;

namespace eft_where_am_i.Tests;

public class FloorManagerTests : IDisposable
{
    private readonly string _dbPath;

    public FloorManagerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"floor_db_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private FloorManager CreateManager(string json)
    {
        File.WriteAllText(_dbPath, json);
        return new FloorManager(_dbPath);
    }

    private const string TwoFloorDb = """
    {
      "reserve": {
        "default_floor": "Ground",
        "floors": [
          { "name": "Ground", "z_min": -5.0, "z_max": 100.0 },
          { "name": "Underground", "z_min": -50.0, "z_max": -5.0 }
        ],
        "zones": []
      }
    }
    """;

    [Fact]
    public void 지상_높이는_Ground_로_판정된다()
    {
        var manager = CreateManager(TwoFloorDb);
        Assert.Equal("Ground", manager.GetFloorNameByHeight("reserve", 24.84));
    }

    [Fact]
    public void 지하_높이는_Underground_로_판정된다()
    {
        var manager = CreateManager(TwoFloorDb);
        Assert.Equal("Underground", manager.GetFloorNameByHeight("reserve", -18.75));
    }

    [Fact]
    public void 경계값은_z_min_쪽이_포함이다()
    {
        var manager = CreateManager(TwoFloorDb);

        Assert.Equal("Ground", manager.GetFloorNameByHeight("reserve", -5.0));   // [z_min, z_max)
        Assert.Equal("Underground", manager.GetFloorNameByHeight("reserve", -5.1));
    }

    [Fact]
    public void 범위를_벗어난_높이는_null_이다()
    {
        var manager = CreateManager(TwoFloorDb);
        Assert.Null(manager.GetFloorNameByHeight("reserve", 999.0));
        Assert.Null(manager.GetFloorNameByHeight("reserve", -999.0));
    }

    [Fact]
    public void 수평_좌표를_높이_자리에_넣으면_판정되지_않는다()
    {
        // 이 케이스가 원래 버그였습니다. 스크린샷의 z(-107.49, 수평)를
        // 높이 자리에 넘기면 어떤 층에도 걸리지 않습니다.
        var manager = CreateManager(TwoFloorDb);
        Assert.Null(manager.GetFloorNameByHeight("reserve", -107.49));
    }

    [Fact]
    public void 등록되지_않은_맵은_null_이다()
    {
        var manager = CreateManager(TwoFloorDb);
        Assert.Null(manager.GetFloorNameByHeight("streets", 10.0));
        Assert.Null(manager.GetDefaultFloor("streets"));
    }

    [Fact]
    public void zones_가_비어_있으면_HasZones_는_거짓이다()
    {
        var manager = CreateManager(TwoFloorDb);
        Assert.False(manager.HasZones("reserve"));
    }

    [Fact]
    public void zones_가_있으면_HasZones_는_참이다()
    {
        const string withZones = """
        {
          "factory": {
            "default_floor": "Ground",
            "floors": [ { "name": "Ground", "z_min": -5.0, "z_max": 100.0 } ],
            "zones": [
              {
                "name": "tunnel",
                "floor_label": "Underground",
                "z_min": -50.0,
                "z_max": -5.0,
                "polygon": [ {"x":0,"y":0}, {"x":10,"y":0}, {"x":10,"y":10} ],
                "holes": []
              }
            ]
          }
        }
        """;

        var manager = CreateManager(withZones);
        Assert.True(manager.HasZones("factory"));
        Assert.Equal("Ground", manager.GetDefaultFloor("factory"));
    }

    [Theory]
    [InlineData("Ground", "Main")]
    [InlineData("Underground", "Basement")]
    [InlineData("Underground", "Bunker")]
    public void 층_이름은_사이트_라벨_별칭으로_확장된다(string configured, string expectedAlias)
    {
        // floor_db 는 Ground/Underground 를 쓰는데 tarkov-market 라벨은
        // Main/Basement 라서, 설정값만으로는 레이어를 못 찾습니다.
        var candidates = FloorManager.GetLabelCandidates(configured);

        Assert.Equal(configured, candidates[0]);   // 설정값이 최우선
        Assert.Contains(expectedAlias, candidates);
    }

    [Fact]
    public void 별칭이_없는_이름은_그대로_하나만_돌려준다()
    {
        var candidates = FloorManager.GetLabelCandidates("Level 4");
        Assert.Equal(new[] { "Level 4" }, candidates);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 빈_층_이름은_후보가_없다(string floorName)
    {
        Assert.Empty(FloorManager.GetLabelCandidates(floorName));
    }

    [Fact]
    public void 후보에_중복이_없다()
    {
        var candidates = FloorManager.GetLabelCandidates("Basement");
        Assert.Equal(candidates.Length, candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
