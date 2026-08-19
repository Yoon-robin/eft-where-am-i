namespace eft_where_am_i.Classes
{
    public class Constants
    {   
        // tarkov-market 의 DOM 셀렉터는 SelectorConfig 로 옮겼습니다.
        // (사이트 마크업이 바뀌어도 assets/selectors.json 만 고치면 되도록)

        /*
         * The following code (ADD_DIRECTION_INDICATORS_SCRIPT) is from 'Tarkov-Client' by 'byeong1'
         * and is licensed under the MIT License.
         * GitHub: https://github.com/byeong1/Tarkov-Client
         *
         * MIT License
         *
         * Copyright (c) 2025 byeong1
         *
         * Permission is hereby granted, free of charge, to any person obtaining a copy
         * of this software and associated documentation files (the "Software"), to deal
         * in the Software without restriction, including without limitation the rights
         * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
         * copies of the Software, and to permit persons to whom the Software is
         * furnished to do so, subject to the following conditions:
         *
         * The above copyright notice and this permission notice shall be included in all
         * copies or substantial portions of the Software.
         *
         * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
         * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
         * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
         * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
         * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
         * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
         * SOFTWARE.
         */
        /// <summary>
        /// 방향 표시기를 추가하는 스크립트
        /// </summary>
        public const string ADD_DIRECTION_INDICATORS_SCRIPT =
            @"     
(function () {
    'use strict';

    // 사용자가 제공한 SVG 데이터 URL 직접 사용
    const svgDataUrl = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCA4IDEwIj48ZyB0cmFuc2Zvcm09InRyYW5zbGF0ZSg0LCA0KSBzY2FsZSgwLjcpIHRyYW5zbGF0ZSgtNCwgLTQpIj48cG9seWdvbiBwb2ludHM9IjQsMCA3LjUsOCAwLjUsOCIgZmlsbD0iIzhhMmJlMiIgc3Ryb2tlPSIjNzBhODAwIiBzdHJva2Utd2lkdGg9IjAuNSIvPjwvZz48L3N2Zz4=';

    function injectStyle() {
        const style = document.createElement('style');
        style.id = 'triangle-indicator-style';
        style.textContent = `
        .triangle-indicator {
            position: absolute !important;
            top: 0% !important;
            left: 50% !important;
            width: 25px !important;
            height: 60px !important;
            background-image: url('${svgDataUrl}') !important;
            background-repeat: no-repeat !important;
            background-size: 100% 100% !important;
            pointer-events: none !important;
            z-index: 9999 !important;
            transform: translate(-50%, -65%) !important;
            transform-origin: 50% 100% !important;
            transition: transform 0.1s ease !important;
        }`;

        const existingStyle = document.getElementById('triangle-indicator-style');
        if (existingStyle) existingStyle.remove();
        document.head.appendChild(style);
    }

    function addTriangleToMarker(marker) {
        if (marker.querySelector('.triangle-indicator')) {
            return;
        }

        const triangle = document.createElement('div');
        triangle.className = 'triangle-indicator';

        const inputEl = marker.querySelector('input[type=text]');
        if(inputEl){
            const updateArrow = () => {
                const val = inputEl.value.trim();
                if(!val) return;

                // 로그 형태: posX,posY,posZ_quatX,quatY,quatZ,quatW_speed
                const parts = val.split('_');
                if(parts.length < 3) return;
                const quat = parts[2].split(',').map(Number);
                if(quat.length < 4) return;

                // 쿼터니언 → forward → deg
                const x=quat[0], y=quat[1], z=quat[2], w=quat[3];
                const fx = 2*(x*z + w*y);
                const fz = 1 - 2*(x*x + y*y);
                const deg = Math.atan2(fx, fz) * 180 / Math.PI;

                triangle.style.transform = `translate(-50%, -65%) rotate(${deg}deg)`;
            };

            inputEl.addEventListener('input', updateArrow);
            updateArrow(); // 초기값 적용
        }

        marker.appendChild(triangle);
    }

    function initMarkers() {
        const markers = document.querySelectorAll('.marker');
        if (markers.length === 0) {
            // .marker가 없으면 다른 선택자 시도
            const altMarkers = document.querySelectorAll('#map > div');
            altMarkers.forEach(addTriangleToMarker);
        } else {
            markers.forEach(addTriangleToMarker);
        }
    }

    injectStyle();

    // SPA (Single Page Application) 라우팅 시 #map 엘리먼트가 아예 지워지고 새로 생성됩니다.
    // 기존처럼 #map에 붙여놓으면 MutationObserver가 같이 죽어버리므로 절대 지워지지 않는 document.body를 감시합니다.
    const container = document.body;
    const observer = new MutationObserver(mutations => {
        for (const mutation of mutations) {
            if (mutation.type === 'childList') {
                mutation.addedNodes.forEach(node => {
                    if (!(node instanceof HTMLElement)) return;

                    if (node.classList && node.classList.contains('marker')) {
                        addTriangleToMarker(node);
                    } else {
                        node.querySelectorAll('.marker, #map > div').forEach(addTriangleToMarker);
                    }
                });
            }
        }
    });

    observer.observe(container, {
        childList: true,
        subtree: true,
    });

    // 2초 후에 마커 초기화 시도
    setTimeout(initMarkers, 2000);
})();";

        /// <summary>
        /// 층 판정 스크립트. 페이지가 로드될 때마다 상시 주입됩니다.
        ///
        /// 좌표계 주의:
        ///  - zone.polygon 은 <b>맵 CSS 픽셀 좌표</b>입니다 (에디터가 마커의 left/top 기준으로 찍음).
        ///  - zone.z_min/z_max 는 <b>게임 좌표의 높이(y)</b> 범위입니다 (타르코프는 Unity라 y가 높이).
        /// 따라서 폴리곤 판정은 반드시 브라우저 쪽 마커 픽셀 위치로 해야 하며,
        /// 게임 좌표를 그대로 폴리곤에 넣으면 안 됩니다.
        /// </summary>
        public const string FLOOR_DETECTION_SCRIPT =
            @"
(function() {
    'use strict';

    // 마커의 CSS left/top = 맵 픽셀 좌표계. zone.polygon 과 같은 좌표계입니다.
    window.__getMarkerPosition = function() {
        var markers = document.querySelectorAll('.marker');
        if (markers.length === 0) return null;
        var marker = markers[markers.length - 1];
        var style = window.getComputedStyle(marker);
        return {
            x: parseFloat(style.left) || 0,
            y: parseFloat(style.top) || 0
        };
    };

    // Ray casting 알고리즘
    window.__isPointInPolygon = function(px, py, polygon) {
        if (!polygon || polygon.length < 3) return false;
        var inside = false;
        for (var i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
            var xi = polygon[i].x, yi = polygon[i].y;
            var xj = polygon[j].x, yj = polygon[j].y;
            if (((yi > py) !== (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi)) {
                inside = !inside;
            }
        }
        return inside;
    };

    // 마커가 속한 zone 을 찾습니다. gameY 는 게임 좌표의 높이입니다.
    window.__getMarkerZone = function(zones, gameY) {
        var markerPos = window.__getMarkerPosition();
        if (!markerPos || !zones) return null;

        for (var i = 0; i < zones.length; i++) {
            var zone = zones[i];
            if (!window.__isPointInPolygon(markerPos.x, markerPos.y, zone.polygon)) continue;

            var inHole = false;
            if (zone.holes) {
                for (var h = 0; h < zone.holes.length; h++) {
                    if (window.__isPointInPolygon(markerPos.x, markerPos.y, zone.holes[h])) {
                        inHole = true;
                        break;
                    }
                }
            }
            if (inHole) continue;

            if (gameY < zone.z_min || gameY >= zone.z_max) continue;

            return zone;
        }
        return null;
    };

    // 현재 페이지에 실제로 존재하는 층 레이어 라벨 목록 (진단/에디터용)
    window.__getFloorLayerLabels = function() {
        var labels = [];
        var inputs = document.querySelectorAll('.no-wrap input[name=layers]');
        for (var i = 0; i < inputs.length; i++) {
            var parent = inputs[i].parentNode;
            if (!parent) continue;
            var text = (parent.innerText || '').trim();
            if (text) labels.push(text);
        }
        return labels;
    };

    // C# 에서 호출하는 통합 진입점. JSON 문자열을 반환합니다.
    window.__detectFloor = function(zones, gameY) {
        var pos = window.__getMarkerPosition();
        if (!pos) {
            return JSON.stringify({ ok: false, reason: 'no-marker', labels: window.__getFloorLayerLabels() });
        }

        var zone = window.__getMarkerZone(zones, gameY);
        return JSON.stringify({
            ok: true,
            floor: zone ? (zone.floor_label || zone.name || null) : null,
            markerX: pos.x,
            markerY: pos.y,
            labels: window.__getFloorLayerLabels()
        });
    };
})();";

        /// <summary>
        /// 맵 위에 폴리곤 오버레이와 클릭 핸들러를 추가합니다.
        /// 픽셀 좌표 기반 - 마커의 CSS left/top과 동일한 좌표계 사용
        /// </summary>
        public const string POLYGON_OVERLAY_SCRIPT =
            @"
(function() {
    'use strict';

    // ============================================================
    // CLEANUP: Remove existing handlers and overlay before re-init
    // ============================================================
    if (window.__floorOverlayMouseDownHandler) {
        document.removeEventListener('mousedown', window.__floorOverlayMouseDownHandler, true);
        window.__floorOverlayMouseDownHandler = null;
    }
    if (window.__floorOverlayMouseUpHandler) {
        document.removeEventListener('mouseup', window.__floorOverlayMouseUpHandler, true);
        window.__floorOverlayMouseUpHandler = null;
    }

    const existing = document.getElementById('floor-polygon-overlay');
    if (existing) existing.remove();

    // Find map container
    const mapContainer = document.querySelector('#map') || document.querySelector('.map-container') || document.querySelector('#map-layer');
    if (!mapContainer) { console.error('[PolygonOverlay] Map container not found'); return; }

    // Create SVG overlay - large fixed size to cover map coordinate space
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.id = 'floor-polygon-overlay';
    svg.setAttribute('width', '10000');
    svg.setAttribute('height', '10000');
    svg.style.cssText = 'position:absolute;top:0;left:0;z-index:9990;pointer-events:none;overflow:visible;';

    mapContainer.appendChild(svg);

    // Store reference for editor
    window.__floorOverlaySvg = svg;
    window.__floorMapContainer = mapContainer;

    // __getMarkerPosition / __isPointInPolygon / __getMarkerZone 은
    // FLOOR_DETECTION_SCRIPT 에서 상시 주입되므로 여기서 다시 정의하지 않습니다.

    // Convert screen coordinates to map CSS coordinates (same as marker left/top)
    window.__screenToMapCoords = function(screenX, screenY) {
        var markers = document.querySelectorAll('.marker');
        if (markers.length === 0) return null;
        var marker = markers[markers.length - 1];
        var mapContainer = window.__floorMapContainer;

        // Get map container's transform scale
        var scaleX = 1, scaleY = 1;
        if (mapContainer) {
            var mapTransform = window.getComputedStyle(mapContainer).transform;
            if (mapTransform && mapTransform !== 'none') {
                var matrix = new DOMMatrix(mapTransform);
                scaleX = matrix.a;
                scaleY = matrix.d;
            }
        }

        // Get marker's screen position (center)
        var markerRect = marker.getBoundingClientRect();
        var markerScreenX = markerRect.left + markerRect.width / 2;
        var markerScreenY = markerRect.top + markerRect.height / 2;

        // Get marker's CSS position
        var style = window.getComputedStyle(marker);
        var markerCssX = parseFloat(style.left) || 0;
        var markerCssY = parseFloat(style.top) || 0;

        // Convert screen to CSS coordinates with scale correction
        // clickCss = markerCss + (clickScreen - markerScreen) / scale
        return {
            x: markerCssX + (screenX - markerScreenX) / scaleX,
            y: markerCssY + (screenY - markerScreenY) / scaleY
        };
    };

    // ============================================================
    // Render existing zones (BLUE color) - direct pixel coordinates
    // ============================================================
    window.__renderFloorZones = function(zones) {
        // Clear existing polygons (but keep edit vertices)
        svg.querySelectorAll('.floor-zone-polygon, .floor-zone-label, .floor-zone-hole').forEach(el => el.remove());

        if (!zones || !Array.isArray(zones)) return;

        zones.forEach(function(zone, zi) {
            if (!zone.polygon || zone.polygon.length < 3) return;

            // Build path: outer polygon (direct pixel coordinates)
            let pathD = 'M';
            zone.polygon.forEach(function(pt, i) {
                pathD += (i === 0 ? '' : ' L') + ' ' + pt.x + ' ' + pt.y;
            });
            pathD += ' Z';

            // Add holes (reverse winding)
            if (zone.holes && zone.holes.length > 0) {
                zone.holes.forEach(function(hole) {
                    if (!hole || hole.length < 3) return;
                    pathD += ' M';
                    for (let h = hole.length - 1; h >= 0; h--) {
                        pathD += (h === hole.length - 1 ? '' : ' L') + ' ' + hole[h].x + ' ' + hole[h].y;
                    }
                    pathD += ' Z';
                });
            }

            const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.setAttribute('d', pathD);
            path.setAttribute('fill', 'rgba(0,100,255,0.15)');
            path.setAttribute('stroke', '#3498db');
            path.setAttribute('stroke-width', '2');
            path.setAttribute('fill-rule', 'evenodd');
            path.setAttribute('data-zone-index', zi);
            path.classList.add('floor-zone-polygon');
            svg.appendChild(path);

            // Label at polygon center
            if (zone.polygon.length > 0) {
                const center = zone.polygon.reduce(function(acc, pt) {
                    return { x: acc.x + pt.x / zone.polygon.length, y: acc.y + pt.y / zone.polygon.length };
                }, { x: 0, y: 0 });

                const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                text.setAttribute('x', center.x);
                text.setAttribute('y', center.y);
                text.setAttribute('fill', '#3498db');
                text.setAttribute('font-size', '14');
                text.setAttribute('font-weight', 'bold');
                text.setAttribute('text-anchor', 'middle');
                text.classList.add('floor-zone-label');
                text.textContent = zone.name || zone.floor_label || ('Zone ' + zi);
                svg.appendChild(text);
            }

            // Render holes with different styling (dashed cyan)
            if (zone.holes) {
                zone.holes.forEach(function(hole, hi) {
                    if (!hole || hole.length < 3) return;
                    let holePath = 'M';
                    hole.forEach(function(pt, i) {
                        holePath += (i === 0 ? '' : ' L') + ' ' + pt.x + ' ' + pt.y;
                    });
                    holePath += ' Z';

                    const holeSvg = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                    holeSvg.setAttribute('d', holePath);
                    holeSvg.setAttribute('fill', 'none');
                    holeSvg.setAttribute('stroke', '#00bcd4');
                    holeSvg.setAttribute('stroke-width', '2');
                    holeSvg.setAttribute('stroke-dasharray', '5,5');
                    holeSvg.classList.add('floor-zone-hole');
                    svg.appendChild(holeSvg);
                });
            }
        });
    };

    // ============================================================
    // Redraw edit vertices - direct pixel coordinates
    // Scale-aware: vertices grow larger when zoomed in
    // ============================================================
    window.__redrawEditVertices = function() {
        // Remove existing edit markers
        svg.querySelectorAll('.floor-edit-vertex').forEach(el => el.remove());

        const verts = window.__floorCurrentVertices || [];
        if (verts.length === 0) return;

        // Get current scale for counter-scaling (keep constant screen size)
        var scale = 1;
        if (mapContainer) {
            var mapTransform = window.getComputedStyle(mapContainer).transform;
            if (mapTransform && mapTransform !== 'none') {
                var matrix = new DOMMatrix(mapTransform);
                scale = Math.max(matrix.a, 0.1); // Use scaleX, minimum 0.1
            }
        }

        // Counter-scale: divide by scale to maintain constant screen size
        const baseRadius = 6;
        const baseStroke = 2;
        const radius = baseRadius / scale;
        const strokeWidth = baseStroke / scale;

        // Draw vertices and lines (direct pixel coordinates)
        verts.forEach(function(v, i) {
            // Draw circle
            const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', v.x);
            circle.setAttribute('cy', v.y);
            circle.setAttribute('r', radius);
            circle.setAttribute('fill', window.__floorDrawMode === 'hole' ? '#00bcd4' : '#e74c3c');
            circle.setAttribute('stroke', '#fff');
            circle.setAttribute('stroke-width', strokeWidth);
            circle.classList.add('floor-edit-vertex');
            svg.appendChild(circle);

            // Draw line to previous vertex
            if (i > 0) {
                const prev = verts[i - 1];
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', prev.x);
                line.setAttribute('y1', prev.y);
                line.setAttribute('x2', v.x);
                line.setAttribute('y2', v.y);
                line.setAttribute('stroke', window.__floorDrawMode === 'hole' ? '#00bcd4' : '#e74c3c');
                line.setAttribute('stroke-width', strokeWidth);
                line.classList.add('floor-edit-vertex');
                svg.appendChild(line);
            }
        });

        // If 3+ vertices, draw preview polygon (closing line)
        if (verts.length >= 3) {
            const first = verts[0];
            const last = verts[verts.length - 1];
            const closingLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            closingLine.setAttribute('x1', last.x);
            closingLine.setAttribute('y1', last.y);
            closingLine.setAttribute('x2', first.x);
            closingLine.setAttribute('y2', first.y);
            closingLine.setAttribute('stroke', window.__floorDrawMode === 'hole' ? '#00bcd4' : '#e74c3c');
            closingLine.setAttribute('stroke-width', strokeWidth);
            closingLine.setAttribute('stroke-dasharray', (4 / scale) + ',' + (4 / scale));
            closingLine.classList.add('floor-edit-vertex');
            svg.appendChild(closingLine);
        }
    };

    // ============================================================
    // Click handler for adding vertices
    // ============================================================
    window.__floorEditClickEnabled = false;
    window.__floorCurrentVertices = window.__floorCurrentVertices || [];

    let __mouseDownPos = null;
    const DRAG_THRESHOLD = 5;

    const mouseDownHandler = function(e) {
        if (!window.__floorEditClickEnabled) return;

        // Ignore clicks on editor panel
        const editorPanel = document.getElementById('floor-editor-panel');
        if (editorPanel && editorPanel.contains(e.target)) return;

        // Only track if inside mapContainer
        const containerRect = mapContainer.getBoundingClientRect();
        if (e.clientX < containerRect.left || e.clientX > containerRect.right ||
            e.clientY < containerRect.top || e.clientY > containerRect.bottom) return;

        __mouseDownPos = { x: e.clientX, y: e.clientY };
    };

    const mouseUpHandler = function(e) {
        if (!window.__floorEditClickEnabled || !__mouseDownPos) return;

        const editorPanel = document.getElementById('floor-editor-panel');
        if (editorPanel && editorPanel.contains(e.target)) {
            __mouseDownPos = null;
            return;
        }

        // Check drag distance
        const dx = e.clientX - __mouseDownPos.x;
        const dy = e.clientY - __mouseDownPos.y;
        const distance = Math.sqrt(dx * dx + dy * dy);
        __mouseDownPos = null;

        if (distance > DRAG_THRESHOLD) return;

        // Convert click screen position to map CSS coordinates
        const mapCoords = window.__screenToMapCoords(e.clientX, e.clientY);
        if (!mapCoords) {
            console.warn('[FloorEdit] No marker found for coordinate conversion');
            return;
        }

        // Add vertex at clicked position (in map CSS coordinate space)
        const newVertex = { x: Math.round(mapCoords.x), y: Math.round(mapCoords.y) };
        window.__floorCurrentVertices.push(newVertex);
        console.log('[FloorEdit] Vertex added at click position:', newVertex, 'Total:', window.__floorCurrentVertices.length);

        // Redraw all vertices
        window.__redrawEditVertices();

        // Notify C#
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(JSON.stringify({
                action: 'polygon-vertex-added',
                x: newVertex.x,
                y: newVertex.y,
                vertexCount: window.__floorCurrentVertices.length
            }));
        }

        // Update UI
        if (window.__floorEditorRender) window.__floorEditorRender();
    };

    // Register handlers (only once per init)
    document.addEventListener('mousedown', mouseDownHandler, true);
    document.addEventListener('mouseup', mouseUpHandler, true);
    window.__floorOverlayMouseDownHandler = mouseDownHandler;
    window.__floorOverlayMouseUpHandler = mouseUpHandler;

    window.__enableFloorEditClick = function(enable) {
        window.__floorEditClickEnabled = enable;
    };

    window.__clearEditVertices = function() {
        svg.querySelectorAll('.floor-edit-vertex').forEach(d => d.remove());
        window.__floorCurrentVertices = [];
    };

    window.__undoLastVertex = function() {
        if (window.__floorCurrentVertices.length === 0) return;
        window.__floorCurrentVertices.pop();
        window.__redrawEditVertices();
    };

    window.__floorDrawMode = window.__floorDrawMode || 'zone';

    // ============================================================
    // Watch for zoom changes and redraw vertices
    // ============================================================
    var lastScale = 1;
    var zoomObserver = new MutationObserver(function(mutations) {
        if (!mapContainer) return;
        var mapTransform = window.getComputedStyle(mapContainer).transform;
        var currentScale = 1;
        if (mapTransform && mapTransform !== 'none') {
            var matrix = new DOMMatrix(mapTransform);
            currentScale = matrix.a;
        }
        if (Math.abs(currentScale - lastScale) > 0.01) {
            lastScale = currentScale;
            // Redraw vertices with new scale
            if (window.__floorCurrentVertices && window.__floorCurrentVertices.length > 0) {
                window.__redrawEditVertices();
            }
        }
    });
    zoomObserver.observe(mapContainer, { attributes: true, attributeFilter: ['style'] });
})();";

        /// <summary>
        /// 데드존 Auto-Pan 스크립트: 마커가 뷰포트 경계 밖에 있으면 자동으로 맵을 팬합니다.
        /// </summary>
        public const string DEAD_ZONE_AUTO_PAN_SCRIPT =
            @"
(function() {
    'use strict';

    // 1. 원치 않는 요소 제거
    // 1-1. 제거할 요소 selector 목록 (개발자 도구에서 selector 복사해서 추가)
    var selectors = [
        '#__nuxt > div > div > div.page-content > div > div > div.panel_right > div.user-layers-panel.mb-5.collapsed',
        '#__nuxt > div > div > div.page-content > div > div > div.panel_right > div.squad-panel.mb-5.collapsed',
        '#__nuxt > div > div > div.cookie-consent',
        '#__nuxt > div > div > div.footer-wrap',
        '#__nuxt > div > div > header'
    ];

    // 1-2. 등록된 selector 요소 제거 + 오버스크롤 차단
    function removeUnwantedElements() {
        for (var i = 0; i < selectors.length; i++) {
            var sel = selectors[i];
            try {
                document.querySelectorAll(sel).forEach(el => el.remove());
            } catch (e) {
                console.warn('[deadZoneAutoPan] Invalid selector:', sel, e);
            }
        }
    }
    removeUnwantedElements();

    // 1-3. DOM 변경 감지 시 재실행 (debounced)
    var removeScheduled = false;
    var unwantedObserver = new MutationObserver(function() {
        if (removeScheduled) return;
        removeScheduled = true;
        requestAnimationFrame(function() {
            removeScheduled = false;
            removeUnwantedElements();
        });
    });
    if (document.body) {
        unwantedObserver.observe(document.body, { childList: true, subtree: true });
    }

    window.__deadZoneAutoPan = function(deadZonePercent) {
        // 2. 마커 찾기
        var markers = document.querySelectorAll('.marker');
        if (markers.length === 0) return JSON.stringify({ panned: false, reason: 'no-marker' });
        var marker = markers[markers.length - 1];
        var markerRect = marker.getBoundingClientRect();
        var markerCX = markerRect.left + markerRect.width / 2;
        var markerCY = markerRect.top + markerRect.height / 2;

        // 3. 뷰포트 계산 (#map의 부모 또는 #map 자체)
        var mapEl = document.querySelector('#map');
        if (!mapEl) return JSON.stringify({ panned: false, reason: 'no-map' });
        var viewportEl = mapEl.parentElement || mapEl;
        var viewportRect = viewportEl.getBoundingClientRect();

        // 4. 패널 감지 (tarkov-market 사이드 패널)
        var effectiveLeft = viewportRect.left;
        var effectiveRight = viewportRect.right;
        var effectiveTop = viewportRect.top;
        var effectiveBottom = viewportRect.bottom;

        var questPanel = document.querySelector('div.items.scroll');
        if (questPanel) {
            var panelParent = questPanel.closest('.panel') || questPanel.parentElement;
            var panelRect = panelParent ? panelParent.getBoundingClientRect() : questPanel.getBoundingClientRect();

            if (panelRect.width > 50 && panelRect.height > 50) {
                var panelCenterX = panelRect.left + panelRect.width / 2;
                var viewportCenterX = viewportRect.left + viewportRect.width / 2;
                if (panelCenterX > viewportCenterX) {
                    effectiveRight = Math.min(effectiveRight, panelRect.left);
                } else {
                    effectiveLeft = Math.max(effectiveLeft, panelRect.right);
                }
            }
        }

        // 5. deadZonePercent 기반 내부 경계 사각형 계산
        var effectiveWidth = effectiveRight - effectiveLeft;
        var effectiveHeight = effectiveBottom - effectiveTop;
        var inset = (100 - deadZonePercent) / 200;
        var boundLeft = effectiveLeft + effectiveWidth * inset;
        var boundRight = effectiveRight - effectiveWidth * inset;
        var boundTop = effectiveTop + effectiveHeight * inset;
        var boundBottom = effectiveBottom - effectiveHeight * inset;

        // 6. 마커가 경계 안에 있으면 팬 불필요
        if (markerCX >= boundLeft && markerCX <= boundRight &&
            markerCY >= boundTop && markerCY <= boundBottom) {
            return JSON.stringify({ panned: false, reason: 'inside-boundary' });
        }

        // 7. 이동량 계산
        var dx = 0, dy = 0;
        if (markerCX < boundLeft) dx = markerCX - boundLeft;
        else if (markerCX > boundRight) dx = markerCX - boundRight;
        if (markerCY < boundTop) dy = markerCY - boundTop;
        else if (markerCY > boundBottom) dy = markerCY - boundBottom;

        // 8. pointer 이벤트 시뮬레이션으로 맵 팬
        var panTarget = mapEl;
        var startX = viewportRect.left + viewportRect.width / 2;
        var startY = viewportRect.top + viewportRect.height / 2;
        var endX = startX - dx;
        var endY = startY - dy;

        var pointerOpts = { bubbles: true, cancelable: true, pointerId: 1, pointerType: 'mouse', isPrimary: true, button: 0, buttons: 1 };

        // pointerdown
        pointerOpts.clientX = startX;
        pointerOpts.clientY = startY;
        panTarget.dispatchEvent(new PointerEvent('pointerdown', pointerOpts));

        // pointermove in 5 steps
        var steps = 5;
        for (var s = 1; s <= steps; s++) {
            var t = s / steps;
            pointerOpts.clientX = startX + (endX - startX) * t;
            pointerOpts.clientY = startY + (endY - startY) * t;
            panTarget.dispatchEvent(new PointerEvent('pointermove', pointerOpts));
        }

        // pointerup
        pointerOpts.clientX = endX;
        pointerOpts.clientY = endY;
        pointerOpts.buttons = 0;
        panTarget.dispatchEvent(new PointerEvent('pointerup', pointerOpts));

        try {
            panTarget.dispatchEvent(new PointerEvent('pointercancel', Object.assign({}, pointerOpts)));
        } catch (e) {}

        try {
            panTarget.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, clientX: endX, clientY: endY, button: 0 }));
        } catch (e) {}

        return JSON.stringify({ panned: true, dx: Math.round(dx), dy: Math.round(dy), method: 'pointer' });
    };

    window.__deadZoneAutoPanCSS = function(deadZonePercent, animate) {
        // CSS transform 직접 수정 폴백 (animate: true면 애니메이션 적용)
        var markers = document.querySelectorAll('.marker');
        if (markers.length === 0) return JSON.stringify({ panned: false, reason: 'no-marker' });
        var marker = markers[markers.length - 1];
        var markerRect = marker.getBoundingClientRect();
        var markerCX = markerRect.left + markerRect.width / 2;
        var markerCY = markerRect.top + markerRect.height / 2;

        var mapEl = document.querySelector('#map');
        if (!mapEl) return JSON.stringify({ panned: false, reason: 'no-map' });
        var viewportEl = mapEl.parentElement || mapEl;
        var viewportRect = viewportEl.getBoundingClientRect();

        var effectiveLeft = viewportRect.left;
        var effectiveRight = viewportRect.right;
        var effectiveTop = viewportRect.top;
        var effectiveBottom = viewportRect.bottom;

        var questPanel = document.querySelector('div.items.scroll');
        if (questPanel) {
            var panelParent = questPanel.closest('.panel') || questPanel.parentElement;
            var panelRect = panelParent ? panelParent.getBoundingClientRect() : questPanel.getBoundingClientRect();
            if (panelRect.width > 50 && panelRect.height > 50) {
                var panelCenterX = panelRect.left + panelRect.width / 2;
                var viewportCenterX = viewportRect.left + viewportRect.width / 2;
                if (panelCenterX > viewportCenterX) {
                    effectiveRight = Math.min(effectiveRight, panelRect.left);
                } else {
                    effectiveLeft = Math.max(effectiveLeft, panelRect.right);
                }
            }
        }

        var effectiveWidth = effectiveRight - effectiveLeft;
        var effectiveHeight = effectiveBottom - effectiveTop;
        var inset = (100 - deadZonePercent) / 200;
        var boundLeft = effectiveLeft + effectiveWidth * inset;
        var boundRight = effectiveRight - effectiveWidth * inset;
        var boundTop = effectiveTop + effectiveHeight * inset;
        var boundBottom = effectiveBottom - effectiveHeight * inset;

        if (markerCX >= boundLeft && markerCX <= boundRight &&
            markerCY >= boundTop && markerCY <= boundBottom) {
            return JSON.stringify({ panned: false, reason: 'inside-boundary' });
        }

        var dx = 0, dy = 0;
        if (markerCX < boundLeft) dx = markerCX - boundLeft;
        else if (markerCX > boundRight) dx = markerCX - boundRight;
        if (markerCY < boundTop) dy = markerCY - boundTop;
        else if (markerCY > boundBottom) dy = markerCY - boundBottom;

        // DOMMatrix로 현재 transform 읽기 → translate 조정
        var currentTransform = window.getComputedStyle(mapEl).transform;
        var matrix = new DOMMatrix(currentTransform && currentTransform !== 'none' ? currentTransform : 'matrix(1,0,0,1,0,0)');
        matrix.e -= dx;
        matrix.f -= dy;

        // 애니메이션 적용
        if (animate) {
            mapEl.style.transition = 'transform 0.3s ease-out';
            mapEl.style.transform = matrix.toString();
            // 애니메이션 완료 후 transition 제거
            setTimeout(function() {
                mapEl.style.transition = '';
            }, 350);
        } else {
            mapEl.style.transform = matrix.toString();
        }

        return JSON.stringify({ panned: true, dx: Math.round(dx), dy: Math.round(dy), method: 'css', animated: !!animate });
    };

    window.__deadZoneCalc = function(deadZonePercent) {
        // Pure calculation: returns pan coordinates without executing any events
        var markers = document.querySelectorAll('.marker');
        if (markers.length === 0) return JSON.stringify({ needsPan: false, reason: 'no-marker' });
        var marker = markers[markers.length - 1];
        var markerRect = marker.getBoundingClientRect();
        var markerCX = markerRect.left + markerRect.width / 2;
        var markerCY = markerRect.top + markerRect.height / 2;

        var mapEl = document.querySelector('#map');
        if (!mapEl) return JSON.stringify({ needsPan: false, reason: 'no-map' });
        var viewportEl = mapEl.parentElement || mapEl;
        var viewportRect = viewportEl.getBoundingClientRect();

        var effectiveLeft = viewportRect.left;
        var effectiveRight = viewportRect.right;
        var effectiveTop = viewportRect.top;
        var effectiveBottom = viewportRect.bottom;

        var questPanel = document.querySelector('div.items.scroll');
        if (questPanel) {
            var panelParent = questPanel.closest('.panel') || questPanel.parentElement;
            var panelRect = panelParent ? panelParent.getBoundingClientRect() : questPanel.getBoundingClientRect();
            if (panelRect.width > 50 && panelRect.height > 50) {
                var panelCenterX = panelRect.left + panelRect.width / 2;
                var viewportCenterX = viewportRect.left + viewportRect.width / 2;
                if (panelCenterX > viewportCenterX) {
                    effectiveRight = Math.min(effectiveRight, panelRect.left);
                } else {
                    effectiveLeft = Math.max(effectiveLeft, panelRect.right);
                }
            }
        }

        var effectiveWidth = effectiveRight - effectiveLeft;
        var effectiveHeight = effectiveBottom - effectiveTop;
        var inset = (100 - deadZonePercent) / 200;
        var boundLeft = effectiveLeft + effectiveWidth * inset;
        var boundRight = effectiveRight - effectiveWidth * inset;
        var boundTop = effectiveTop + effectiveHeight * inset;
        var boundBottom = effectiveBottom - effectiveHeight * inset;

        if (markerCX >= boundLeft && markerCX <= boundRight &&
            markerCY >= boundTop && markerCY <= boundBottom) {
            return JSON.stringify({ needsPan: false, reason: 'inside-boundary' });
        }

        var dx = 0, dy = 0;
        if (markerCX < boundLeft) dx = markerCX - boundLeft;
        else if (markerCX > boundRight) dx = markerCX - boundRight;
        if (markerCY < boundTop) dy = markerCY - boundTop;
        else if (markerCY > boundBottom) dy = markerCY - boundBottom;

        var startX = effectiveLeft + effectiveWidth / 2;
        var startY = effectiveTop + effectiveHeight / 2;
        var endX = startX - dx;
        var endY = startY - dy;

        return JSON.stringify({
            needsPan: true,
            reason: 'outside-boundary',
            dx: Math.round(dx),
            dy: Math.round(dy),
            startX: Math.round(startX),
            startY: Math.round(startY),
            endX: Math.round(endX),
            endY: Math.round(endY)
        });
    };
})();";

        /// <summary>
        /// 맵 위 플로팅 에디터 UI 패널을 삽입합니다.
        /// </summary>
        public const string FLOOR_EDITOR_UI_SCRIPT =
            @"
(function() {
    'use strict';

    // Remove existing editor if present
    const existing = document.getElementById('floor-editor-panel');
    if (existing) existing.remove();

    // State
    window.__floorEditorZones = window.__floorEditorZones || [];
    window.__floorEditorFloors = window.__floorEditorFloors || [];
    window.__floorEditorSelectedZone = -1;
    window.__floorDrawMode = window.__floorDrawMode || 'zone';

    // 페이지의 레이어 라벨을 읽고, floor_db 에 같은 이름이 있으면 그쪽 높이 범위를 씁니다.
    //
    // 예전에는 여기서 무조건 z_min:-999, z_max:999 를 넣었습니다.
    // 그러면 그 폴리곤 안에 있기만 하면 높이와 무관하게 같은 층으로 판정돼서,
    // 같은 자리에 층이 여러 개인 건물을 구분할 수 없었습니다.
    function getFloorsFromMap() {
        const layersContainer = document.querySelector('div.d-flex.h-space-between.layers');
        if (!layersContainer) return [];

        const known = window.__floorEditorFloors || [];
        const floors = [];
        const floorDivs = layersContainer.querySelectorAll('div.no-wrap:not(.bold)');

        floorDivs.forEach(function(div) {
            const label = div.querySelector('label');
            if (!label) return;

            const text = label.textContent.trim();
            if (!text) return;

            const match = known.find(function(f) {
                return f.name && f.name.toLowerCase() === text.toLowerCase();
            });

            floors.push(match
                ? { name: text, z_min: match.z_min, z_max: match.z_max, known: true }
                : { name: text, known: false });
        });

        return floors;
    }

    const panel = document.createElement('div');
    panel.id = 'floor-editor-panel';
    panel.style.cssText = 'position:fixed;top:10px;right:10px;width:340px;background:#1a1a2e;color:#eee;' +
        'border:2px solid #3498db;border-radius:8px;z-index:99999;font-family:monospace;font-size:13px;' +
        'box-shadow:0 4px 20px rgba(0,0,0,0.5);max-height:90vh;overflow-y:auto;';

    function render() {
        const zones = window.__floorEditorZones;
        // Priority: 1. DOM extraction, 2. window.__floorEditorFloors, 3. defaults
        let floors = getFloorsFromMap();
        if (floors.length === 0) {
            floors = window.__floorEditorFloors || [];
        }
        const verts = window.__floorCurrentVertices || [];
        const mode = window.__floorDrawMode;
        const selIdx = window.__floorEditorSelectedZone;

        let html = '<div style=""padding:10px;border-bottom:1px solid #333"">';
        html += '<div style=""font-size:16px;font-weight:bold;color:#3498db;margin-bottom:8px"">Floor Zone Editor</div>';
        html += '<div style=""margin-bottom:8px;padding:4px;background:#1e4620;border-radius:3px;font-size:12px;color:#4ade80"">Click map to add vertex at marker position</div>';

        // Floor select dropdown (from floors data)
        html += '<div style=""margin-bottom:6px""><label>Floor: </label>';
        html += '<select id=""fze-floor"" style=""width:180px;background:#0f3460;color:#eee;border:1px solid #555;padding:4px;border-radius:3px"">';
        if (floors.length === 0) {
            html += '<option value=""Underground"" data-zmin=""-50"" data-zmax=""-5"">Underground (default)</option>';
            html += '<option value=""Ground"" data-zmin=""-5"" data-zmax=""100"">Ground (default)</option>';
        } else {
            floors.forEach(function(f) {
                var zmin = (f.z_min === undefined || f.z_min === null) ? '' : f.z_min;
                var zmax = (f.z_max === undefined || f.z_max === null) ? '' : f.z_max;
                html += '<option value=""' + f.name + '"" data-zmin=""' + zmin + '"" data-zmax=""' + zmax + '"">' + f.name + '</option>';
            });
        }
        html += '</select></div>';

        // 높이(게임 y) 범위. 층이 겹치는 건물은 이 값으로 구분합니다.
        var numStyle = 'width:70px;background:#0f3460;color:#eee;border:1px solid #555;padding:3px 5px;border-radius:3px;font-family:monospace;font-size:12px';
        html += '<div style=""margin-bottom:6px;display:flex;align-items:center;gap:5px;font-size:11px;color:#aaa"">';
        html += '<span>Height (y)</span>';
        html += '<input type=""number"" id=""fze-zmin"" step=""any"" style=""' + numStyle + '"" />';
        html += '<span>~</span>';
        html += '<input type=""number"" id=""fze-zmax"" step=""any"" style=""' + numStyle + '"" />';
        html += '</div>';
        html += '<div id=""fze-zhint"" style=""margin-bottom:6px;font-size:10px;color:#fbbf24;display:none"">이 층의 높이 범위를 모릅니다. 값을 직접 입력해 주세요.</div>';

        // Mode selection
        html += '<div style=""margin-bottom:6px"">';
        html += '<label style=""margin-right:10px""><input type=""radio"" name=""fze-mode"" value=""zone"" ' + (mode === 'zone' ? 'checked' : '') + '/> Draw Zone</label>';
        html += '<label><input type=""radio"" name=""fze-mode"" value=""hole"" ' + (mode === 'hole' ? 'checked' : '') + '/> Draw Hole</label>';
        html += '</div>';

        // Current vertices info + preview
        const vertexColor = mode === 'hole' ? '#00bcd4' : '#e74c3c';
        html += '<div style=""margin-bottom:6px;display:flex;align-items:center;gap:6px"">';
        html += '<span style=""color:#aaa"">Vertices: <b style=""color:' + vertexColor + '"">' + verts.length + '</b></span>';
        if (verts.length >= 3) {
            html += '<span style=""color:#4ade80;font-size:11px"">(polygon ready)</span>';
        }
        html += '</div>';

        // Action buttons
        html += '<div style=""margin-bottom:6px;display:flex;gap:4px"">';
        const completeDisabled = verts.length < 3 ? 'opacity:0.5;cursor:not-allowed' : '';
        html += '<button id=""fze-complete"" style=""flex:1;background:#3498db;color:#fff;border:none;padding:6px 10px;border-radius:3px;cursor:pointer;' + completeDisabled + '"">Complete (' + verts.length + '/3+)</button>';
        html += '<button id=""fze-undo"" style=""background:#0f3460;color:#fff;border:1px solid #555;padding:6px 10px;border-radius:3px;cursor:pointer"">Undo</button>';
        html += '<button id=""fze-clear"" style=""background:#c0392b;color:#fff;border:none;padding:6px 10px;border-radius:3px;cursor:pointer"">Clear</button>';
        html += '</div>';

        html += '</div>'; // end top section

        // Saved zones list
        html += '<div style=""padding:10px;border-bottom:1px solid #333"">';
        html += '<div style=""font-weight:bold;margin-bottom:6px;color:#3498db"">Saved Zones (' + zones.length + '):</div>';

        if (zones.length === 0) {
            html += '<div style=""color:#777;font-style:italic"">No zones defined. Click on map to add vertices.</div>';
        } else {
            zones.forEach(function(z, i) {
                const pts = z.polygon ? z.polygon.length : 0;
                const holes = z.holes ? z.holes.length : 0;
                const selected = (i === selIdx);
                html += '<div style=""padding:6px;margin-bottom:3px;background:' + (selected ? '#0f3460' : '#252540') + ';border-radius:4px;cursor:pointer;border:1px solid ' + (selected ? '#3498db' : 'transparent') + '"" data-zone=""' + i + '"" class=""fze-zone-item"">';
                html += '<div style=""display:flex;justify-content:space-between;align-items:center"">';
                html += '<span><span style=""color:#3498db"">▶</span> ' + (z.name || z.floor_label) + '</span>';
                html += '<span style=""font-size:11px;color:#888"">' + pts + ' pts' + (holes > 0 ? ', ' + holes + ' holes' : '') + '</span>';
                html += '</div>';
                html += '<div style=""margin-top:4px;display:flex;gap:4px"">';
                html += '<button class=""fze-add-hole"" data-zone=""' + i + '"" style=""flex:1;background:#00bcd4;color:#fff;border:none;padding:3px 6px;border-radius:3px;cursor:pointer;font-size:11px"">+ Hole</button>';
                html += '<button class=""fze-delete-zone"" data-zone=""' + i + '"" style=""background:#c0392b;color:#fff;border:none;padding:3px 8px;border-radius:3px;cursor:pointer;font-size:11px"">Delete</button>';
                html += '</div>';
                html += '</div>';
            });
        }
        html += '</div>';

        // Bottom buttons
        html += '<div style=""padding:10px;display:flex;gap:8px"">';
        html += '<button id=""fze-save"" style=""flex:1;background:#27ae60;color:#fff;border:none;padding:8px;border-radius:4px;cursor:pointer;font-weight:bold"">💾 Save All</button>';
        html += '<button id=""fze-cancel"" style=""flex:1;background:#555;color:#fff;border:none;padding:8px;border-radius:4px;cursor:pointer"">Cancel</button>';
        html += '</div>';

        panel.innerHTML = html;
        attachEvents();

        // Update Z range display based on selected floor
        updateZRangeDisplay();
    }

    // 선택한 층에 알려진 높이 범위가 있으면 입력칸을 채우고,
    // 모르면 비워둔 채 직접 입력하라고 안내합니다.
    function updateZRangeDisplay() {
        const floorSelect = document.getElementById('fze-floor');
        const zminInput = document.getElementById('fze-zmin');
        const zmaxInput = document.getElementById('fze-zmax');
        const hint = document.getElementById('fze-zhint');
        if (!floorSelect || !zminInput || !zmaxInput) return;

        const opt = floorSelect.options[floorSelect.selectedIndex];
        if (!opt) return;

        const zmin = opt.getAttribute('data-zmin');
        const zmax = opt.getAttribute('data-zmax');
        const known = zmin !== null && zmin !== '' && zmax !== null && zmax !== '';

        zminInput.value = known ? zmin : '';
        zmaxInput.value = known ? zmax : '';
        if (hint) hint.style.display = known ? 'none' : 'block';
    }

    // 입력칸에서 높이 범위를 읽습니다. 비어 있으면 null 을 돌려줍니다.
    function readZRange() {
        const zminInput = document.getElementById('fze-zmin');
        const zmaxInput = document.getElementById('fze-zmax');
        if (!zminInput || !zmaxInput) return null;

        const zmin = parseFloat(zminInput.value);
        const zmax = parseFloat(zmaxInput.value);
        if (!isFinite(zmin) || !isFinite(zmax)) return null;
        if (zmin >= zmax) return null;

        return { zmin: zmin, zmax: zmax };
    }

    function attachEvents() {
        // Floor select change
        const floorSelect = document.getElementById('fze-floor');
        if (floorSelect) {
            floorSelect.onchange = updateZRangeDisplay;
        }

        // Complete polygon
        const completeBtn = document.getElementById('fze-complete');
        if (completeBtn) {
            completeBtn.onclick = function() {
                const verts = window.__floorCurrentVertices || [];
                if (verts.length < 3) { alert('At least 3 vertices required'); return; }

                const mode = window.__floorDrawMode;
                if (mode === 'zone') {
                    const floorSel = document.getElementById('fze-floor');
                    const opt = floorSel.options[floorSel.selectedIndex];
                    const name = opt.value || 'Unnamed';

                    const range = readZRange();
                    if (!range) {
                        alert('높이(y) 범위를 올바르게 입력해 주세요. (최소값 < 최대값)');
                        return;
                    }

                    window.__floorEditorZones.push({
                        name: name,
                        floor_label: name,
                        z_min: range.zmin,
                        z_max: range.zmax,
                        polygon: verts.slice(),
                        holes: []
                    });
                    console.log('[FloorEdit] Zone added:', name, 'with', verts.length, 'vertices');
                } else if (mode === 'hole') {
                    const selIdx = window.__floorEditorSelectedZone;
                    if (selIdx >= 0 && selIdx < window.__floorEditorZones.length) {
                        if (!window.__floorEditorZones[selIdx].holes) {
                            window.__floorEditorZones[selIdx].holes = [];
                        }
                        window.__floorEditorZones[selIdx].holes.push(verts.slice());
                        console.log('[FloorEdit] Hole added to zone', selIdx);
                    } else {
                        alert('Select a zone first to add a hole');
                        return;
                    }
                }

                window.__clearEditVertices();
                if (window.__renderFloorZones) window.__renderFloorZones(window.__floorEditorZones);
                render();
            };
        }

        // Undo
        const undoBtn = document.getElementById('fze-undo');
        if (undoBtn) {
            undoBtn.onclick = function() {
                if (window.__undoLastVertex) window.__undoLastVertex();
                render();
            };
        }

        // Clear
        const clearBtn = document.getElementById('fze-clear');
        if (clearBtn) {
            clearBtn.onclick = function() {
                if (window.__clearEditVertices) window.__clearEditVertices();
                render();
            };
        }

        // Mode radio buttons
        const radios = panel.querySelectorAll('input[name=""fze-mode""]');
        radios.forEach(function(r) {
            r.onchange = function() {
                window.__floorDrawMode = this.value;
                window.__clearEditVertices();
                render();
            };
        });

        // Zone item click (select)
        const zoneItems = panel.querySelectorAll('.fze-zone-item');
        zoneItems.forEach(function(item) {
            item.onclick = function(e) {
                if (e.target.tagName === 'BUTTON') return;
                window.__floorEditorSelectedZone = parseInt(this.getAttribute('data-zone'));
                render();
            };
        });

        // Add hole buttons
        const addHoleBtns = panel.querySelectorAll('.fze-add-hole');
        addHoleBtns.forEach(function(btn) {
            btn.onclick = function() {
                const idx = parseInt(this.getAttribute('data-zone'));
                window.__floorEditorSelectedZone = idx;
                window.__floorDrawMode = 'hole';
                window.__clearEditVertices();
                render();
            };
        });

        // Delete zone buttons
        const delBtns = panel.querySelectorAll('.fze-delete-zone');
        delBtns.forEach(function(btn) {
            btn.onclick = function() {
                const idx = parseInt(this.getAttribute('data-zone'));
                if (confirm('Delete zone: ' + window.__floorEditorZones[idx].name + '?')) {
                    window.__floorEditorZones.splice(idx, 1);
                    window.__floorEditorSelectedZone = -1;
                    if (window.__renderFloorZones) window.__renderFloorZones(window.__floorEditorZones);
                    render();
                }
            };
        });

        // Save All
        const saveBtn = document.getElementById('fze-save');
        if (saveBtn) {
            saveBtn.onclick = function() {
                console.log('[FloorEdit] Saving zones:', JSON.stringify(window.__floorEditorZones));
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify({
                        action: 'save-floor-zones',
                        data: JSON.stringify(window.__floorEditorZones)
                    }));
                }
            };
        }

        // Cancel
        const cancelBtn = document.getElementById('fze-cancel');
        if (cancelBtn) {
            cancelBtn.onclick = function() {
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify({
                        action: 'exit-floor-edit-mode'
                    }));
                }
            };
        }
    }

    document.body.appendChild(panel);
    window.__enableFloorEditClick(true);
    render();

    // Expose render for external updates
    window.__floorEditorRender = render;
})();";
    }
}
