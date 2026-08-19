using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace eft_where_am_i.Classes
{
    /// <summary>층 레이어 클릭 결과. 실제 페이지에 어떤 라벨이 있었는지도 함께 알려줍니다.</summary>
    public class FloorClickResult
    {
        public static readonly FloorClickResult Failed = new FloorClickResult();

        public bool Matched { get; set; }

        /// <summary>실제로 클릭된 라벨. 다음번에 이 값을 먼저 시도하도록 학습하는 데 씁니다.</summary>
        public string MatchedName { get; set; }

        /// <summary>페이지에 존재하는 전체 레이어 라벨 목록.</summary>
        public string[] AvailableLabels { get; set; } = Array.Empty<string>();
    }

    public class JavaScriptExecutor
    {
        private readonly WebView2 webView;

        public JavaScriptExecutor(WebView2 webView)
        {
            this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
        }

        /// <summary>
        /// C# 문자열을 안전한 JavaScript 문자열 리터럴로 변환합니다.
        /// 따옴표, 백슬래시, 줄바꿈 등 모든 특수문자를 올바르게 이스케이프하여
        /// JS 코드 인젝션을 방지합니다. null 입력도 안전하게 처리합니다.
        /// </summary>
        public static string JsLiteral(string value)
        {
            return JsonConvert.SerializeObject(value ?? string.Empty);
        }

        /// <summary>
        /// WebView2 초기화 완료 대기
        /// </summary>
        private async Task EnsureWebViewInitializedAsync()
        {
            if (webView.CoreWebView2 == null)
            {
                await webView.EnsureCoreWebView2Async(null);
            }
        }

        /// <summary>
        /// JavaScript 코드를 실행합니다.
        /// </summary>
        /// <param name="script">실행할 JavaScript 코드</param>
        public async Task ExecuteScriptAsync(string script)
        {
            try
            {
                await EnsureWebViewInitializedAsync(); // 초기화 대기

                if (webView.CoreWebView2 != null)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch (Exception ex)
            {
                // 게임이 전체화면일 때 모달을 띄우면 포커스를 뺏기므로 로그로만 남깁니다.
                AppLogger.Error("JS", $"스크립트 실행 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 특정 버튼을 클릭하는 JavaScript 코드 실행
        /// </summary>
        /// <param name="selector">버튼의 CSS 셀렉터</param>
        public async Task ClickButtonAsync(string selector)
        {
            string Selector = JsLiteral(selector);
            string script = $@"
                var button = document.querySelector({Selector});
                if (button) {{
                    button.click();
                    console.log('Button clicked');
                }} else {{
                    console.log('Button not found');
                }}";
            await ExecuteScriptAsync(script);
        }

        public async Task<bool> CheckInputAble()
        {
            string script = $@"
            (function() {{
                // 설정된 입력창 셀렉터를 우선 사용
                var preferred = document.querySelector({JsLiteral(SelectorConfig.LocationInput)});
                if (preferred) {{
                    const isVisibleP = preferred.offsetParent !== null;
                    const isEnabledP = !preferred.disabled && !preferred.readOnly;
                    if (isVisibleP && isEnabledP) return true;
                }}
                const buttons = document.querySelectorAll('button');
                for (const btn of buttons) {{
                    if (btn.textContent.trim() === 'Where am i?') {{
                        const parent = btn.parentElement;
                        if (!parent) return false;

                        const input = parent.querySelector('input');
                        if (!input) return false;

                        const isVisible = input.offsetParent !== null;
                        const isEnabled = !input.disabled && !input.readOnly;

                        return isVisible && isEnabled;
                    }}
                }}
                return false;
            }})()
            ";
            try
            {
                string result = await webView.ExecuteScriptAsync(script);
                return result.Trim().ToLower() == "true";
            }
            catch (Exception ex)
            {
                AppLogger.Error("JS", $"CheckInputAble 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 텍스트 입력 필드에 값을 설정하는 JavaScript 코드 실행
        /// Vue/React 호환 방식으로 nativeInputValueSetter + InputEvent를 사용합니다.
        /// </summary>
        /// <param name="selector">입력 필드의 CSS 셀렉터</param>
        /// <param name="value">설정할 값</param>
        public async Task SetInputValueAsync(string selector, string value)
        {
            string Selector = JsLiteral(selector);
            string escapedValue = JsLiteral(value);
            string script = $@"
                (function() {{
                    var input = document.querySelector({Selector});

                    // 설정된 입력창 셀렉터를 우선 사용
                    try {{
                        var preferred = document.querySelector({JsLiteral(SelectorConfig.LocationInput)});
                        if (preferred) input = preferred;
                    }} catch(e) {{}}

                    // Fallbacks: try to locate input near a 'Where' button, placeholder inputs, or any text input
                    if (!input) {{
                        try {{
                            var buttons = document.querySelectorAll('button');
                            for (var i=0;i<buttons.length;i++) {{
                                var t = (buttons[i].textContent || '').toLowerCase();
                                if (t.indexOf('where') !== -1 || t.indexOf('where am') !== -1) {{
                                    var p = buttons[i].parentElement || buttons[i].closest('div');
                                    if (p) {{
                                        input = p.querySelector('input, input[type=text]');
                                        if (input) break;
                                    }}
                                }}
                            }}
                        }} catch (e) {{ console.log('Fallback button search failed: ' + e.message); }}
                    }}

                    if (!input) {{
                        input = document.querySelector('input[placeholder]') || document.querySelector('input[type=text]') || document.querySelector('input');
                    }}

                    if (!input) {{ console.log('Input not found (all fallbacks)'); return; }}

                    // Use native setter to bypass Vue/React getter/setter
                    var nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                    nativeSetter.call(input, {escapedValue});

                    // Dispatch multiple events for framework compatibility
                    try {{
                        input.dispatchEvent(new InputEvent('input', {{ bubbles: true, cancelable: true, inputType: 'insertText', data: {escapedValue} }}));
                    }} catch(e) {{
                        // Some browsers/environments may not support InputEvent constructor
                        var ev = document.createEvent('Event'); ev.initEvent('input', true, true); input.dispatchEvent(ev);
                    }}
                    input.dispatchEvent(new Event('change', {{ bubbles: true }}));

                    // Also try focus/blur to trigger validation
                    try {{ input.focus(); input.blur(); }} catch(e) {{}}

                    console.log('Input value set to: ' + input.value);
                }})();";
            await ExecuteScriptAsync(script);
        }

        /// <summary>
        /// 퀘스트 컨테이너가 로드될 때까지 폴링하며 대기합니다.
        /// Nuxt/SPA 페이지의 동적 렌더링 완료를 감지합니다.
        /// </summary>
        /// <param name="timeoutMs">최대 대기 시간 (밀리초)</param>
        /// <returns>컨테이너 로드 성공 여부</returns>
        public async Task<bool> WaitForQuestContainerAsync(int timeoutMs = 15000, System.Threading.CancellationToken cancellationToken = default)
        {
            string script = @"
            (function() {
                const container = document.querySelector('div.items.scroll');
                return container && container.children.length > 0;
            })()";

            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await EnsureWebViewInitializedAsync();
                    if (webView.CoreWebView2 == null)
                    {
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    string result = await webView.CoreWebView2.ExecuteScriptAsync(script);
                    if (result.Trim().ToLower() == "true")
                    {
                        return true;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AppLogger.Debug("JS", $"WaitForQuestContainer 폴링 오류: {ex.Message}");
                }

                await Task.Delay(500, cancellationToken);
            }

            AppLogger.Warn("JS", $"퀘스트 컨테이너 대기 시간 초과 ({timeoutMs}ms)");
            return false;
        }

        /// <summary>
        /// 퀘스트 이름으로 해당 퀘스트를 우클릭하여 선택합니다.
        /// </summary>
        public async Task SelectQuestByNameAsync(string questName)
        {
            string escapedName = JsLiteral(questName);
            string script = $@"
            (function() {{
                const container = document.querySelector('div.items.scroll');
                if (!container) {{
                    return;
                }}

                const items = container.querySelectorAll('div.no-wrap.d-flex, div.no-wrap, .quest, .quest-item, [data-quest-name]');
                const isSelectedState = (el) => {{
                    if (!el) return false;
                    if (el.getAttribute('data-quest-selected') === 'true') return true;
                    if (el.classList.contains('selected')) return true;
                    if (el.classList.contains('active')) return true;
                    if (el.getAttribute('aria-selected') === 'true') return true;
                    if (el.getAttribute('data-selected') === 'true') return true;
                    if (el.getAttribute('data-state') === 'selected') return true;
                    if (el.getAttribute('aria-pressed') === 'true') return true;
                    return false;
                }};

                for (const item of items) {{
                    const span = item.querySelector('span:not(.alt)');
                    if (!span) continue;
                    if (span.innerText.trim() !== {escapedName}) continue;

                    if (!isSelectedState(item)) {{
                        const event = new MouseEvent('contextmenu', {{
                            bubbles: true,
                            cancelable: true,
                            button: 2,
                            view: window
                        }});
                        item.dispatchEvent(event);
                    }}
                    return;
                }}
            }})();";

            await ExecuteScriptAsync(script);
        }

        /// <summary>
        /// 여러 층 이름 후보를 한 번의 JS 호출로 시도합니다. 순서대로 매칭하여 첫 번째 매칭을 클릭합니다.
        /// 매칭에 실패하면 페이지에 실제로 존재하는 레이어 라벨을 로그로 남깁니다.
        /// (floor_db 의 층 이름과 tarkov-market 의 라벨이 어긋났을 때 진단용)
        /// </summary>
        public async Task<FloorClickResult> ClickFloorByFirstMatchAsync(string[] floorNames)
        {
            if (floorNames == null || floorNames.Length == 0) return FloorClickResult.Failed;

            try
            {
                await EnsureWebViewInitializedAsync();
                if (webView.CoreWebView2 == null) return FloorClickResult.Failed;

                string jsArray = "[" + string.Join(",", floorNames.Select(JsLiteral)) + "]";

                string script = $@"
                (function() {{
                    var names = {jsArray};
                    var inputs = document.querySelectorAll('.no-wrap input[name=layers]');
                    var labels = [];
                    for (var i = 0; i < inputs.length; i++) {{
                        var parent = inputs[i].parentNode;
                        labels.push(parent ? (parent.innerText || '').trim() : '');
                    }}
                    for (var n = 0; n < names.length; n++) {{
                        for (var i = 0; i < inputs.length; i++) {{
                            if (labels[i].includes(names[n])) {{
                                inputs[i].click();
                                return JSON.stringify({{ matched: true, name: names[n], labels: labels }});
                            }}
                        }}
                    }}
                    return JSON.stringify({{ matched: false, labels: labels }});
                }})()";

                string raw = await webView.CoreWebView2.ExecuteScriptAsync(script);
                if (string.IsNullOrEmpty(raw) || raw == "null") return FloorClickResult.Failed;

                string json = JsonConvert.DeserializeObject<string>(raw);
                if (string.IsNullOrEmpty(json)) return FloorClickResult.Failed;

                var obj = JObject.Parse(json);
                bool matched = obj["matched"]?.Value<bool>() ?? false;

                string[] labels = obj["labels"]?
                    .Select(t => t.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray() ?? Array.Empty<string>();

                if (!matched)
                {
                    AppLogger.Warn("Floor",
                        $"층 이름 매칭 실패. 후보=[{string.Join(", ", floorNames)}] / 페이지 라벨=[{string.Join(", ", labels)}]");
                }

                return new FloorClickResult
                {
                    Matched = matched,
                    MatchedName = obj["name"]?.ToString(),
                    AvailableLabels = labels,
                };
            }
            catch (Exception ex)
            {
                AppLogger.Error("Floor", $"ClickFloorByFirstMatch 실패: {ex.Message}");
                return FloorClickResult.Failed;
            }
        }

        /// <summary>
        /// 현재 마커가 어느 층 zone 에 있는지 브라우저 쪽에서 판정합니다.
        ///
        /// 폴리곤(zone.polygon)은 맵 CSS 픽셀 좌표라서 C# 이 가진 게임 좌표로는 판정할 수 없습니다.
        /// 마커의 픽셀 위치를 아는 브라우저에 위임하고, 높이 판정에 쓰는 게임 y 좌표만 넘깁니다.
        /// </summary>
        /// <param name="zonesJson">해당 맵의 zones 배열 JSON</param>
        /// <param name="gameY">스크린샷 파일명에서 파싱한 게임 y 좌표(= 높이)</param>
        /// <returns>판정된 층 이름. 판정 불가 시 null</returns>
        public async Task<string> DetectFloorAsync(string zonesJson, double gameY)
        {
            try
            {
                await EnsureWebViewInitializedAsync();
                if (webView.CoreWebView2 == null) return null;

                string script = string.Format(
                    CultureInfo.InvariantCulture,
                    "(function(){{ return window.__detectFloor ? window.__detectFloor(JSON.parse({0}), {1}) : null; }})()",
                    JsLiteral(zonesJson ?? "[]"),
                    gameY);

                string raw = await webView.CoreWebView2.ExecuteScriptAsync(script);
                if (string.IsNullOrEmpty(raw) || raw == "null")
                {
                    AppLogger.Warn("Floor", "__detectFloor 가 주입되지 않았습니다.");
                    return null;
                }

                string json = JsonConvert.DeserializeObject<string>(raw);
                if (string.IsNullOrEmpty(json)) return null;

                var obj = JObject.Parse(json);
                if (!(obj["ok"]?.Value<bool>() ?? false))
                {
                    AppLogger.Debug("Floor", $"층 판정 불가: {obj["reason"]}");
                    return null;
                }

                string floor = obj["floor"]?.ToString();
                AppLogger.Debug("Floor",
                    $"판정 결과 floor={floor ?? "(none)"} marker=({obj["markerX"]}, {obj["markerY"]}) gameY={gameY.ToString(CultureInfo.InvariantCulture)}");

                return string.IsNullOrWhiteSpace(floor) ? null : floor;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Floor", $"DetectFloor 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 맵 위에 폴리곤 오버레이 + 에디터 UI를 주입합니다.
        /// </summary>
        public async Task EnableFloorEditModeAsync(string existingZonesJson, string floorsJson)
        {
            // Inject overlay
            await ExecuteScriptAsync(Constants.POLYGON_OVERLAY_SCRIPT);

            // Set existing zones data
            string escapedZonesJson = JsLiteral(existingZonesJson ?? "[]");
            await ExecuteScriptAsync($"window.__floorEditorZones = JSON.parse({escapedZonesJson});");

            // Set floors data for select dropdown
            string escapedFloorsJson = JsLiteral(floorsJson ?? "[]");
            await ExecuteScriptAsync($"window.__floorEditorFloors = JSON.parse({escapedFloorsJson});");

            // Render existing zones
            await ExecuteScriptAsync("if(window.__renderFloorZones) window.__renderFloorZones(window.__floorEditorZones);");

            // Inject editor UI
            await ExecuteScriptAsync(Constants.FLOOR_EDITOR_UI_SCRIPT);
        }

        /// <summary>
        /// 폴리곤 오버레이 + 에디터 UI를 제거합니다.
        /// </summary>
        public async Task DisableFloorEditModeAsync()
        {
            await ExecuteScriptAsync(@"
            (function() {
                var overlay = document.getElementById('floor-polygon-overlay');
                if (overlay) overlay.remove();
                var panel = document.getElementById('floor-editor-panel');
                if (panel) panel.remove();
                window.__floorEditClickEnabled = false;
                window.__floorEditorZones = [];
                window.__floorCurrentVertices = [];
            })();");
        }

        /// <summary>
        /// CDP (Chrome DevTools Protocol)를 사용하여 trusted 마우스 드래그를 실행합니다.
        /// isTrusted: true 이벤트를 생성하여 tarkov-market의 맵 핸들러가 인식합니다.
        /// </summary>
        /// <param name="animate">true면 각 단계 사이에 딜레이를 주어 부드러운 애니메이션 효과</param>
        private async Task<bool> CdpMouseDragAsync(double startX, double startY, double endX, double endY, int steps = 10, bool animate = true)
        {
            try
            {
                var cdp = webView.CoreWebView2;

                // mousePressed at start position
                await cdp.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                    string.Format(CultureInfo.InvariantCulture,
                        "{{\"type\":\"mousePressed\",\"x\":{0},\"y\":{1},\"button\":\"left\",\"clickCount\":1}}",
                        startX, startY));

                // mouseMoved in steps (with optional delay for animation)
                int delayMs = animate ? 20 : 0;
                for (int s = 1; s <= steps; s++)
                {
                    double t = (double)s / steps;
                    double cx = startX + (endX - startX) * t;
                    double cy = startY + (endY - startY) * t;
                    await cdp.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                        string.Format(CultureInfo.InvariantCulture,
                            "{{\"type\":\"mouseMoved\",\"x\":{0},\"y\":{1},\"button\":\"left\",\"buttons\":1}}",
                            cx, cy));

                    if (delayMs > 0 && s < steps)
                        await Task.Delay(delayMs);
                }

                // mouseReleased at end position
                await cdp.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                    string.Format(CultureInfo.InvariantCulture,
                        "{{\"type\":\"mouseReleased\",\"x\":{0},\"y\":{1},\"button\":\"left\",\"clickCount\":1}}",
                        endX, endY));

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("AutoPan", $"CDP 드래그 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 마커가 데드존 밖에 있으면 자동으로 맵을 팬합니다.
        /// 1차: __deadZoneCalc()로 좌표 계산 → CDP trusted mouse drag
        /// 2차: CDP 실패 시 __deadZoneAutoPanCSS() CSS fallback (애니메이션 지원)
        /// </summary>
        /// <param name="deadZonePercent">내부 경계 비율 (93 = 가장자리 3.5% 여백)</param>
        /// <param name="animate">CSS fallback 시 애니메이션 적용 여부</param>
        /// <returns>팬 실행 여부</returns>
        public async Task<bool> AutoPanToMarkerAsync(int deadZonePercent = 93, bool animate = true)
        {
            try
            {
                await EnsureWebViewInitializedAsync();
                if (webView.CoreWebView2 == null) return false;

                // Step 1: __deadZoneCalc()로 팬 필요 여부 및 좌표 계산
                string calcScript = $"(function(){{ return window.__deadZoneCalc ? window.__deadZoneCalc({deadZonePercent}) : JSON.stringify({{ needsPan: false, reason: 'not-injected' }}); }})()";
                string calcRaw = await webView.CoreWebView2.ExecuteScriptAsync(calcScript);

                if (string.IsNullOrEmpty(calcRaw) || calcRaw == "null")
                    return false;

                string calcJson = JsonConvert.DeserializeObject<string>(calcRaw);
                var calcObj = JObject.Parse(calcJson);
                bool needsPan = calcObj["needsPan"]?.Value<bool>() ?? false;
                string reason = calcObj["reason"]?.ToString() ?? "";

                if (!needsPan)
                {
                    // inside-boundary 또는 no-marker/no-map
                    return false;
                }

                double dx = calcObj["dx"]?.Value<double>() ?? 0;
                double dy = calcObj["dy"]?.Value<double>() ?? 0;
                double startX = calcObj["startX"]?.Value<double>() ?? 0;
                double startY = calcObj["startY"]?.Value<double>() ?? 0;
                double endX = calcObj["endX"]?.Value<double>() ?? 0;
                double endY = calcObj["endY"]?.Value<double>() ?? 0;

                // Step 2: CDP trusted mouse drag 시도 (애니메이션 옵션 포함)
                bool cdpSuccess = await CdpMouseDragAsync(startX, startY, endX, endY, steps: 10, animate: animate);
                if (cdpSuccess)
                {
                    AppLogger.Debug("AutoPan", $"CDP 패닝: dx={dx}, dy={dy}");
                    return true;
                }

                // Step 3: CDP 실패 시 CSS transform fallback (애니메이션 옵션 포함)
                string animateJs = animate ? "true" : "false";
                string fallbackScript = $"(function(){{ return window.__deadZoneAutoPanCSS ? window.__deadZoneAutoPanCSS({deadZonePercent}, {animateJs}) : JSON.stringify({{ panned: false, reason: 'not-injected' }}); }})()";
                string fallbackRaw = await webView.CoreWebView2.ExecuteScriptAsync(fallbackScript);

                if (!string.IsNullOrEmpty(fallbackRaw) && fallbackRaw != "null")
                {
                    string fallbackJson = JsonConvert.DeserializeObject<string>(fallbackRaw);
                    var fallbackObj = JObject.Parse(fallbackJson);
                    bool fallbackPanned = fallbackObj["panned"]?.Value<bool>() ?? false;

                    if (fallbackPanned)
                    {
                        AppLogger.Debug("AutoPan", $"CSS 폴백 패닝: dx={fallbackObj["dx"]}, dy={fallbackObj["dy"]}, animated={animate}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                AppLogger.Error("AutoPan", $"패닝 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 패널이 현재 숨겨져 있는지 확인합니다.
        /// 버튼 텍스트가 "Show panels"를 포함하면 패널이 숨겨진 상태입니다.
        /// </summary>
        public async Task<bool> IsPanelHiddenAsync()
        {
            try
            {
                await EnsureWebViewInitializedAsync();
                if (webView.CoreWebView2 == null) return false;

                string script = $@"
                (function() {{
                    var btn = document.querySelector({JsLiteral(SelectorConfig.HideShowPanelButton)});
                    if (!btn) return 'false';

                    var text = (btn.textContent || '').toLowerCase();
                    var isHidden = text.includes('show pannels') || text.includes('show panels') || text.includes('show panel');
                    return isHidden ? 'true' : 'false';
                }})()";

                string result = await webView.CoreWebView2.ExecuteScriptAsync(script);
                return result?.Trim('"') == "true";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 패널이 닫혀 있으면 빠르게 열어줍니다.
        /// </summary>
        public async Task<bool> OpenPanelIfHiddenAsync(int attempts = 3, int delayMs = 100)
        {
            try
            {
                await EnsureWebViewInitializedAsync();
                if (webView.CoreWebView2 == null) return false;

                for (int i = 0; i < attempts; i++)
                {
                    if (!await IsPanelHiddenAsync())
                    {
                        return true;
                    }

                    string script = $@"
                    (function() {{
                        var btn = document.querySelector({JsLiteral(SelectorConfig.HideShowPanelButton)});
                        if (!btn) return 'not-found';
                        btn.click();
                        return 'clicked';
                    }})();";

                    await webView.CoreWebView2.ExecuteScriptAsync(script);
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs);
                    }
                }

                return !await IsPanelHiddenAsync();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 퀘스트 컨테이너에 우클릭 리스너를 주입하여 퀘스트 선택/해제를 감지합니다.
        /// 퀘스트가 토글되면 WebMessage로 C#에 전송합니다.
        /// </summary>
        public async Task InjectQuestClickListenerAsync()
        {
            string script = """
            (function() {
                const container = document.querySelector('div.items.scroll');
                if (!container || container.dataset.questListenerAttached) return;
                container.dataset.questListenerAttached = 'true';

                const isSelectedState = (el) => {
                    if (!el) return false;
                    // Check data attribute as source of truth
                    if (el.getAttribute('data-quest-selected') === 'true') return true;
                    if (el.classList.contains('selected')) return true;
                    if (el.classList.contains('active')) return true;
                    if (el.getAttribute('aria-selected') === 'true') return true;
                    if (el.getAttribute('data-selected') === 'true') return true;
                    if (el.getAttribute('data-state') === 'selected') return true;
                    if (el.getAttribute('aria-pressed') === 'true') return true;
                    return false;
                };

                const findQuestItem = (target) => {
                    if (!target || target === container) return null;
                    let current = target;
                    while (current && current !== container) {
                        if (current.matches('div.no-wrap.d-flex, div.no-wrap, .quest, .quest-item, [data-quest-name]')) {
                            return current;
                        }
                        const parent = current.parentElement;
                        if (!parent || parent === current) break;
                        current = parent;
                    }
                    return target.closest('div.no-wrap.d-flex, div.no-wrap, .quest, .quest-item, [data-quest-name]');
                };

                const clearSelectionForItem = (item) => {
                    if (!item) return;
                    
                    // Remove all selection markers
                    item.removeAttribute('data-quest-selected');
                    item.classList.remove('selected', 'active');
                    item.removeAttribute('aria-selected');
                    item.removeAttribute('data-selected');
                    item.removeAttribute('data-state');
                    item.removeAttribute('aria-pressed');
                };

                const lastSentState = new WeakMap();
                let pendingTimer = null;

                const sendQuestState = (item) => {
                    if (!item || window.__questRestoreInProgress) return;

                    const span = item.querySelector('span:not(.alt)');
                    if (!span) return;

                    const questName = span.innerText.trim();
                    const wasSelected = lastSentState.get(item);
                    const isSelected = isSelectedState(item);
                    if (wasSelected === isSelected) return;
                    lastSentState.set(item, isSelected);

                    console.log('[Quest Save]', questName, 'isSelected:', isSelected);

                    window.chrome.webview.postMessage(JSON.stringify({
                        action: 'quest-toggled',
                        questName: questName,
                        isSelected: isSelected
                    }));
                };

                const queueQuestState = (item) => {
                    if (!item || window.__questRestoreInProgress) return;
                    if (pendingTimer) clearTimeout(pendingTimer);
                    pendingTimer = setTimeout(() => sendQuestState(item), 180);
                };

                const handlePotentialToggle = (item) => {
                    if (!item || window.__questRestoreInProgress) return;
                    queueQuestState(item);
                };

                container.addEventListener('contextmenu', function(e) {
                    const item = findQuestItem(e.target);
                    if (!item) return;
                    handlePotentialToggle(item);
                }, true);

                container.addEventListener('mousedown', function(e) {
                    if (e.button !== 2) return;
                    const item = findQuestItem(e.target);
                    if (!item) return;
                    handlePotentialToggle(item);
                }, true);

                const observer = new MutationObserver((mutations) => {
                    for (const mutation of mutations) {
                        if (mutation.type !== 'attributes') continue;
                        const item = findQuestItem(mutation.target);
                        if (!item) continue;
                        if (['class', 'aria-selected', 'data-selected', 'aria-pressed', 'data-state', 'data-quest-selected'].includes(mutation.attributeName)) {
                            queueQuestState(item);
                        }
                    }
                });

                container.querySelectorAll('div.no-wrap.d-flex, div.no-wrap, .quest, .quest-item, [data-quest-name]').forEach((item) => {
                    observer.observe(item, {
                        attributes: true,
                        attributeFilter: ['class', 'aria-selected', 'data-selected', 'aria-pressed', 'data-state', 'data-quest-selected']
                    });
                });

                observer.observe(container, {
                    childList: true,
                    subtree: true
                });

                console.log('[Quest] Observer listener attached');
            })()
            """;

            await ExecuteScriptAsync(script);
        }
    }
}
