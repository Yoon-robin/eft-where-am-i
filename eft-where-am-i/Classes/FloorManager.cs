using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace eft_where_am_i.Classes
{
    public class PolygonPoint
    {
        public double x { get; set; }
        public double y { get; set; }
    }

    public class FloorZone
    {
        public string name { get; set; }
        public string floor_label { get; set; }
        public double z_min { get; set; }
        public double z_max { get; set; }
        public List<PolygonPoint> polygon { get; set; } = new List<PolygonPoint>();
        public List<List<PolygonPoint>> holes { get; set; } = new List<List<PolygonPoint>>();
    }

    public class FloorData
    {
        public string name { get; set; }
        public double z_min { get; set; }
        public double z_max { get; set; }
    }

    public class MapFloorConfig
    {
        public List<FloorData> floors { get; set; } = new List<FloorData>();
        public string default_floor { get; set; } = "Ground";
        public List<FloorZone> zones { get; set; } = new List<FloorZone>();
    }

    public class FloorManager
    {
        private readonly string _filePath;
        private Dictionary<string, MapFloorConfig> _floorDb;

        public FloorManager()
            : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "floor_db.json"))
        {
        }

        /// <summary>DB 파일 경로를 직접 지정합니다. (테스트용)</summary>
        public FloorManager(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    _floorDb = JsonConvert.DeserializeObject<Dictionary<string, MapFloorConfig>>(json)
                               ?? new Dictionary<string, MapFloorConfig>();
                }
                else
                {
                    _floorDb = new Dictionary<string, MapFloorConfig>();
                    CreateDefaultDb();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("FloorManager", $"floor DB 로드 실패: {ex.Message}");
                _floorDb = new Dictionary<string, MapFloorConfig>();
            }
        }

        public void Reload()
        {
            Load();
        }

        private void CreateDefaultDb()
        {
            _floorDb["reserve"] = new MapFloorConfig
            {
                default_floor = "Ground",
                floors = new List<FloorData>
                {
                    new FloorData { name = "Ground", z_min = -5.0, z_max = 100.0 },
                    new FloorData { name = "Underground", z_min = -50.0, z_max = -5.0 }
                },
                zones = new List<FloorZone>()
            };

            _floorDb["factory"] = new MapFloorConfig
            {
                default_floor = "Ground",
                floors = new List<FloorData>
                {
                    new FloorData { name = "Ground", z_min = -5.0, z_max = 100.0 },
                    new FloorData { name = "Underground", z_min = -50.0, z_max = -5.0 }
                },
                zones = new List<FloorZone>()
            };

            _floorDb["lab"] = new MapFloorConfig
            {
                default_floor = "Ground",
                floors = new List<FloorData>
                {
                    new FloorData { name = "Ground", z_min = -5.0, z_max = 100.0 }
                },
                zones = new List<FloorZone>()
            };

            _floorDb["streets"] = new MapFloorConfig
            {
                default_floor = "Ground",
                floors = new List<FloorData>
                {
                    new FloorData { name = "Ground", z_min = -5.0, z_max = 100.0 },
                    new FloorData { name = "Underground", z_min = -50.0, z_max = -5.0 }
                },
                zones = new List<FloorZone>()
            };

            Save();
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_floorDb, Formatting.Indented);
                File.WriteAllText(_filePath, json);
                AppLogger.Debug("FloorManager", $"Save completed. File size: {json.Length} bytes");
            }
            catch (Exception ex)
            {
                AppLogger.Error("FloorManager", $"Error saving floor DB: {ex.Message}");
            }
        }

        /// <summary>
        /// 해당 맵에 폴리곤 zone 이 정의되어 있는지 여부.
        /// zone 이 있으면 폴리곤 판정을 브라우저(<c>__detectFloor</c>)에 위임해야 합니다.
        /// </summary>
        public bool HasZones(string mapName)
        {
            return !string.IsNullOrEmpty(mapName)
                   && _floorDb.TryGetValue(mapName, out var config)
                   && config.zones != null
                   && config.zones.Count > 0;
        }

        /// <summary>
        /// 해당 맵의 기본 층 이름. 맵이 등록되어 있지 않으면 null.
        /// </summary>
        public string GetDefaultFloor(string mapName)
        {
            if (string.IsNullOrEmpty(mapName) || !_floorDb.TryGetValue(mapName, out var config))
                return null;

            return config.default_floor ?? "Ground";
        }

        /// <summary>
        /// <b>높이</b>로 해당 맵의 층 이름을 판별합니다.
        ///
        /// 타르코프는 Unity 기반이라 게임 좌표의 <b>y 가 높이</b>입니다.
        /// 스크린샷 파일명의 좌표는 (x, y, z) 순서이므로 두 번째 값을 넘겨야 합니다.
        /// (필드 이름이 z_min/z_max 인 것은 floor_db.json 하위 호환 때문입니다.)
        /// </summary>
        public string GetFloorNameByHeight(string mapName, double height)
        {
            if (string.IsNullOrEmpty(mapName) || !_floorDb.TryGetValue(mapName, out var config))
                return null;

            if (config.floors == null || config.floors.Count == 0)
                return null;

            foreach (var floor in config.floors)
            {
                if (height >= floor.z_min && height < floor.z_max)
                {
                    return floor.name;
                }
            }

            return null;
        }

        /// <summary>
        /// floor_db 의 층 이름을 tarkov-market 의 실제 레이어 라벨 후보로 확장합니다.
        ///
        /// floor_db 는 "Ground"/"Underground" 같은 논리적 이름을 쓰는데
        /// tarkov-market 라벨은 맵마다 "Main", "Basement", "Level 2" 등으로 제각각입니다.
        /// 설정된 이름을 먼저 시도하고, 안 되면 통용되는 별칭을 순서대로 시도합니다.
        /// </summary>
        public static string[] GetLabelCandidates(string floorName)
        {
            if (string.IsNullOrWhiteSpace(floorName))
                return Array.Empty<string>();

            string trimmed = floorName.Trim();

            if (FloorLabelAliases.TryGetValue(trimmed, out var aliases))
            {
                // 설정값을 최우선으로 두고 중복 없이 별칭을 이어붙입니다.
                var candidates = new List<string> { trimmed };
                foreach (var alias in aliases)
                {
                    if (!candidates.Contains(alias, StringComparer.OrdinalIgnoreCase))
                        candidates.Add(alias);
                }
                return candidates.ToArray();
            }

            return new[] { trimmed };
        }

        private static readonly Dictionary<string, string[]> FloorLabelAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ground",      new[] { "Main", "Ground floor", "Level 1", "1st floor" } },
                { "Main",        new[] { "Ground", "Level 1" } },
                { "Underground", new[] { "Basement", "Bunker", "Tunnels", "Underground level", "-1" } },
                { "Basement",    new[] { "Underground", "Bunker", "Tunnels" } },
                { "Bunker",      new[] { "Basement", "Underground", "Tunnels" } },
                { "Second",      new[] { "Level 2", "2nd floor" } },
                { "Third",       new[] { "Level 3", "3rd floor" } },
            };

        /// <summary>
        /// 특정 맵의 zones 데이터를 JSON으로 반환 (에디터용)
        /// </summary>
        public string GetZonesJson(string mapName)
        {
            if (!_floorDb.TryGetValue(mapName, out var config))
                return "[]";

            return JsonConvert.SerializeObject(config.zones ?? new List<FloorZone>(), Formatting.None);
        }

        /// <summary>
        /// 특정 맵의 floors 데이터를 JSON으로 반환 (에디터용)
        /// </summary>
        public string GetFloorsJson(string mapName)
        {
            if (!_floorDb.TryGetValue(mapName, out var config))
                return "[]";

            return JsonConvert.SerializeObject(config.floors ?? new List<FloorData>(), Formatting.None);
        }

        /// <summary>
        /// 특정 맵의 zones 데이터를 JSON에서 업데이트 (에디터용)
        /// </summary>
        public void UpdateZonesFromJson(string mapName, string zonesJson)
        {
            try
            {
                AppLogger.Info("FloorManager", $"UpdateZonesFromJson called for map: {mapName}");
                AppLogger.Debug("FloorManager", $"Zones JSON (first 500 chars): {zonesJson.Substring(0, Math.Min(500, zonesJson.Length))}");

                var zones = JsonConvert.DeserializeObject<List<FloorZone>>(zonesJson)
                            ?? new List<FloorZone>();

                AppLogger.Info("FloorManager", $"Deserialized {zones.Count} zones");

                if (!_floorDb.ContainsKey(mapName))
                {
                    AppLogger.Info("FloorManager", $"Creating new map config for: {mapName}");
                    _floorDb[mapName] = new MapFloorConfig();
                }

                _floorDb[mapName].zones = zones;
                Save();
                AppLogger.Info("FloorManager", $"Zones saved successfully to {_filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("FloorManager", $"Error updating zones from JSON: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 전체 DB를 JSON 문자열로 반환 (에디터용)
        /// </summary>
        public string GetDbJson()
        {
            return JsonConvert.SerializeObject(_floorDb, Formatting.Indented);
        }

        /// <summary>
        /// JSON 문자열로부터 DB를 업데이트합니다 (에디터용)
        /// </summary>
        public void UpdateFromJson(string json)
        {
            try
            {
                _floorDb = JsonConvert.DeserializeObject<Dictionary<string, MapFloorConfig>>(json)
                           ?? new Dictionary<string, MapFloorConfig>();
                Save();
            }
            catch (Exception ex)
            {
                AppLogger.Error("FloorManager", $"JSON 으로부터 DB 갱신 실패: {ex.Message}");
            }
        }
    }
}
