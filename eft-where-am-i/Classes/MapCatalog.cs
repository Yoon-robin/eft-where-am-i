using System;
using System.Collections.Generic;
using System.Linq;

namespace eft_where_am_i.Classes
{
    /// <summary>
    /// 지원하는 맵 목록의 단일 출처.
    ///
    /// 예전에는 UI 드롭다운 목록(WhereAmI)과 로그 파싱 매핑(LogWatcherService)이
    /// 따로 관리돼서 새 맵을 추가할 때 한쪽만 갱신되는 일이 있었습니다.
    /// (terminal / icebreaker 가 UI 에는 있는데 자동 감지는 안 되던 원인)
    /// </summary>
    public static class MapCatalog
    {
        /// <summary>tarkov-market.com/maps/{slug} 의 slug 목록. UI 표시 순서와 동일합니다.</summary>
        public static readonly IReadOnlyList<string> Slugs = new[]
        {
            "ground-zero",
            "factory",
            "customs",
            "interchange",
            "woods",
            "shoreline",
            "lighthouse",
            "reserve",
            "streets",
            "lab",
            "labyrinth",
            "terminal",
            "icebreaker",
        };

        private static readonly HashSet<string> SlugSet =
            new HashSet<string>(Slugs, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> EnglishNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ground-zero", "Ground Zero" },
                { "factory", "Factory" },
                { "customs", "Customs" },
                { "interchange", "Interchange" },
                { "woods", "Woods" },
                { "shoreline", "Shoreline" },
                { "lighthouse", "Lighthouse" },
                { "reserve", "Reserve" },
                { "streets", "Streets" },
                { "lab", "The Lab" },
                { "labyrinth", "Labyrinth" },
                { "terminal", "Terminal" },
                { "icebreaker", "Icebreaker" },
            };

        private static readonly Dictionary<string, string> KoreanNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ground-zero", "그라운드 제로" },
                { "factory", "공장" },
                { "customs", "세관" },
                { "interchange", "인터체인지" },
                { "woods", "삼림" },
                { "shoreline", "해안선" },
                { "lighthouse", "등대" },
                { "reserve", "리저브" },
                { "streets", "타르코프 시내" },
                { "lab", "연구소" },
                { "labyrinth", "미궁" },
                { "terminal", "터미널" },
                { "icebreaker", "쇄빙선" },
            };

        public static bool IsKnownSlug(string slug)
        {
            return !string.IsNullOrWhiteSpace(slug) && SlugSet.Contains(slug);
        }

        /// <summary>지정한 언어의 맵 표시 이름. 번역이 없으면 영문, 그마저 없으면 slug 를 그대로 돌려줍니다.</summary>
        public static string GetDisplayName(string slug, string language)
        {
            if (string.IsNullOrWhiteSpace(slug)) return string.Empty;

            if (string.Equals(language, "ko", StringComparison.OrdinalIgnoreCase)
                && KoreanNames.TryGetValue(slug, out var korean))
            {
                return korean;
            }

            return EnglishNames.TryGetValue(slug, out var english) ? english : slug;
        }

        /// <summary>
        /// 게임 로그의 씬 번들 이름을 맵 slug 로 변환합니다.
        ///
        /// 1. 알려진 번들 이름은 표에서 직접 찾고,
        /// 2. 표에 없으면 접미사(_preset, _high, _day ...)를 떼어내고 slug 와 대조합니다.
        ///    새 맵이 추가돼도 번들 이름이 slug 를 포함하면 대부분 자동으로 잡힙니다.
        /// </summary>
        public static bool TryResolveSlug(string rawBundleName, out string slug)
        {
            slug = null;
            if (string.IsNullOrWhiteSpace(rawBundleName)) return false;

            if (BundleNameMapping.TryGetValue(rawBundleName, out var mapped))
            {
                slug = mapped;
                return true;
            }

            string normalized = NormalizeBundleName(rawBundleName);
            if (IsKnownSlug(normalized))
            {
                slug = Slugs.First(s => string.Equals(s, normalized, StringComparison.OrdinalIgnoreCase));
                return true;
            }

            return false;
        }

        private static readonly string[] StrippableSuffixes =
        {
            "preset", "high", "low", "day", "night", "base", "scene",
        };

        private static string NormalizeBundleName(string rawBundleName)
        {
            string value = rawBundleName.Trim().ToLowerInvariant();

            // 뒤에서부터 알려진 접미사를 반복적으로 제거합니다. (예: sandbox_high_preset -> sandbox)
            bool stripped = true;
            while (stripped)
            {
                stripped = false;
                foreach (var suffix in StrippableSuffixes)
                {
                    string token = "_" + suffix;
                    if (value.EndsWith(token, StringComparison.Ordinal) && value.Length > token.Length)
                    {
                        value = value.Substring(0, value.Length - token.Length);
                        stripped = true;
                        break;
                    }
                }
            }

            return value.Replace('_', '-');
        }

        /// <summary>게임 로그에서 관측된 씬 번들 이름 → slug 매핑.</summary>
        private static readonly Dictionary<string, string> BundleNameMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "woods_preset", "woods" },
                { "customs_preset", "customs" },
                { "bigmap", "customs" },
                { "shoreline_preset", "shoreline" },
                { "shopping_mall", "interchange" },
                { "rezerv_base_preset", "reserve" },
                { "rezervbase", "reserve" },
                { "lighthouse_preset", "lighthouse" },
                { "city_preset", "streets" },
                { "tarkovstreets", "streets" },
                { "factory_day_preset", "factory" },
                { "factory_night_preset", "factory" },
                { "factory4_day", "factory" },
                { "factory4_night", "factory" },
                { "sandbox_preset", "ground-zero" },
                { "sandbox_high_preset", "ground-zero" },
                { "sandbox", "ground-zero" },
                { "sandbox_high", "ground-zero" },
                { "laboratory_preset", "lab" },
                { "laboratory", "lab" },
                { "labyrinth_preset", "labyrinth" },
            };
    }
}
