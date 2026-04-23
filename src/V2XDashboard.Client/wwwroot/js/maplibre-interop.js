window.v2xMap = (() => {
    const layerDefinitions = [
        { name: "CAM", sourceId: "cam-source", layerId: "cam-layer", type: "circle", color: "#22c55e", minZoom: 0, maxZoom: 22 },
        { name: "DENM", sourceId: "denm-source", layerId: "denm-layer", type: "circle", color: "#ef4444", minZoom: 0, maxZoom: 22 },
        { name: "MAPEM", sourceId: "mapem-source", layerId: "mapem-layer", type: "circle", color: "#0ea5e9", minZoom: 0, maxZoom: 22 },
        { name: "SPATEM", sourceId: "spatem-source", layerId: "spatem-layer", type: "circle", color: "#f59e0b", minZoom: 0, maxZoom: 22 },
        { name: "SREM", sourceId: "srem-source", layerId: "srem-layer", type: "circle", color: "#8b5cf6", minZoom: 0, maxZoom: 22 },
        { name: "SSEM", sourceId: "ssem-source", layerId: "ssem-layer", type: "circle", color: "#f97316", minZoom: 0, maxZoom: 22 },
        { name: "Correlations", sourceId: "correlations-source", layerId: "correlations-layer", type: "line", minZoom: 0, maxZoom: 22 },
        { name: "TrafficIntensity", sourceId: "traffic-intensity-source", layerId: "traffic-intensity-layer", type: "line", sourceType: "static-roadref", minZoom: 0, maxZoom: 22 }
    ];

    const popupSchemas = {
        CAM: [
            { title: "Core", fields: [["Time", "generation_time"], ["Station", "station_id"]] },
            { title: "Position", fields: [["Latitude", "latitude"], ["Longitude", "longitude"], ["Altitude", "altitude"]] },
            { title: "Motion", fields: [["Speed", "speed"], ["Heading", "heading"], ["Acceleration", "acceleration"],["Yaw Rate", "yaw_rate"]] },
            { title: "Vehicle", fields: [["Station Type", "station_type"], ["Vehicle Role", "vehicle_role"], ["Vehicle Length", "vehicle_length"], ["Vehicle Width", "vehicle_width"]] },
            { title: "Security", fields: [["Secure Signed", "is_secure_signed"], ["Secure Encrypted", "is_secure_encrypted"], ["Security Protocol", "security_protocol"]] }
        ],
        DENM: [
            { title: "Core", fields: [["Time", "generation_time"], ["Station", "station_id"], ["Cause Code", "cause_code"]] },
            { title: "Position", fields: [["Latitude", "latitude"], ["Longitude", "longitude"], ["Altitude", "altitude"]] },
            { title: "Event", fields: [["Detection Time", "detection_time"], ["Reference Time", "reference_time"], ["Station Type", "station_type"], ["Original Station Type", "original_station_type"]] },
            { title: "Security", fields: [["Secure Signed", "is_secure_signed"], ["Secure Encrypted", "is_secure_encrypted"], ["Security Protocol", "security_protocol"]] }
        ],
        MAPEM: [
            { title: "Core", fields: [["Time", "generation_time"], ["Station", "station_id"], ["Publisher", "publisher_id"]] },
            { title: "Intersection", fields: [["Intersection ID", "intersection_id"], ["Intersection Name", "intersection_name"], ["Latitude", "latitude"], ["Longitude", "longitude"]] },
            { title: "Map", fields: [["Lane Count", "lane_count"], ["Road Width", "road_width"], ["Speed Limit", "speed_limit"]] },
            { title: "Security", fields: [["Secure Signed", "is_secure_signed"], ["Secure Encrypted", "is_secure_encrypted"], ["Security Protocol", "security_protocol"]] }
        ],
        SPATEM: [
            { title: "Core", fields: [["Time", "generation_time"], ["Station", "station_id"], ["Publisher", "publisher_id"]] },
            { title: "Intersection", fields: [["Intersection ID", "intersection_id"], ["Intersection Name", "intersection_name"], ["Latitude", "latitude"], ["Longitude", "longitude"]] },
            { title: "Phase", fields: [["Current Phase", "current_phase"], ["Phase State", "phase_state"], ["Connection Assist", "connection_maneuver_assist_id"]] },
            { title: "Security", fields: [["Secure Signed", "is_secure_signed"], ["Secure Encrypted", "is_secure_encrypted"], ["Security Protocol", "security_protocol"]] }
        ],
        SREM: [
            { title: "Core", fields: [["Time", "generation_time"], ["Station", "station_id"], ["Request ID", "request_id"], ["Requestor ID", "requestor_id"]] },
            { title: "Intersection", fields: [["Intersection ID", "intersection_id"], ["Intersection Name", "intersection_name"], ["Latitude", "latitude"], ["Longitude", "longitude"]] },
            { title: "Request", fields: [["Requested Phase", "requested_phase"], ["Vehicle Type", "vehicle_type"], ["Request Reason", "request_reason"], ["In Bound Lane", "in_bound_lane_id"], ["Out Bound Lane", "out_bound_lane_id"]] },
            { title: "Movement", fields: [["Speed", "speed"], ["Transmission Power", "transmission_power"]] },
            { title: "Context", fields: [["Route Names", "route_names"], ["Transit Schedule", "transit_schedule"], ["Requestor Name", "requestor_name"]] },
            { title: "Security", fields: [["Secure Signed", "is_secure_signed"], ["Secure Encrypted", "is_secure_encrypted"], ["Security Protocol", "security_protocol"]] }
        ],
        SSEM: [
            { title: "Core", fields: [["Time", "generation_time"], ["Station", "station_id"], ["Responder", "responder_id"]] },
            { title: "Intersection", fields: [["Intersection ID", "intersection_id"], ["Intersection Name", "intersection_name"], ["Latitude", "latitude"], ["Longitude", "longitude"]] },
            { title: "Status", fields: [["Status Code", "status_code"], ["Granted Duration", "granted_duration"], ["Request ID Ref", "request_id_ref"], ["Request Station ID Ref", "request_station_id_ref"]] },
            { title: "Security", fields: [["Secure Signed", "is_secure_signed"], ["Secure Encrypted", "is_secure_encrypted"], ["Security Protocol", "security_protocol"]] }
        ],
        Correlations: [
            { title: "Core", fields: [["Correlation ID", "id"], ["Correlation Type", "correlation_type"], ["Confidence", "match_confidence"], ["Request ID", "request_id"]] },
            { title: "Links", fields: [["SREM ID", "srem_id"], ["SSEM ID", "ssem_id"], ["MAPEM ID", "mapem_id"], ["SPATEM ID", "spatem_id"], ["OBU Station", "obu_station_id"], ["RSU Intersection", "rsu_intersection_id"]] },
            { title: "Timing", fields: [["SREM Time", "srem_timestamp"], ["SSEM Time", "ssem_timestamp"], ["Delta ms", "time_delta_ms"], ["Created At", "created_at"]] },
            { title: "Result", fields: [["Request Type", "request_type"], ["Status Code", "status_code"], ["Granted Duration", "granted_duration"]] },
            { title: "Geometry", fields: [["SREM Latitude", "srem_latitude"], ["SREM Longitude", "srem_longitude"], ["SSEM Latitude", "ssem_latitude"], ["SSEM Longitude", "ssem_longitude"]] }
        ],
        TrafficIntensity: [
            { title: "Road", fields: [["Reference", "ref"], ["Class", "class"], ["Network", "network"]] }
        ]
    };

    let map = null;
    let popup = null;
    let currentFilters = createDefaultFilters();
    let interactionsBound = false;
    let viewportChangedCallback = null;
    let moveEndHandler = null;
    let trafficIntensityStyle = null;
    let trafficIntensityLoadPromise = null;

    const trafficIntensityDataUrl = "/data/traffic-sv-by-roadref.json";

    function createEmptyTrafficIntensityStyle() {
        return {
            colorExpression: ["literal", "rgba(0,0,0,0)"],
            widthExpression: ["literal", 0]
        };
    }

    function normalizeRoadRef(value) {
        if (value === null || value === undefined) {
            return "";
        }

        const trimmed = String(value).trim().toUpperCase();
        if (/^\d+$/.test(trimmed)) {
            return String(parseInt(trimmed, 10));
        }

        return trimmed;
    }

    function buildNormalizedRoadRefExpression() {
        return ["upcase", ["to-string", ["coalesce", ["get", "ref"], ""]]];
    }

    function getTrafficColorByClassId(classId) {
        switch (classId) {
            case "traffic_sv_1":
                return "#fde68a";
            case "traffic_sv_2":
                return "#fbbf24";
            case "traffic_sv_3":
                return "#ffa053";
            case "traffic_sv_4":
                return "#ff6f09";
            case "traffic_sv_5":
                return "#b91c1c";
            default:
                return null;
        }
    }

    function getTrafficWidthByClassId(classId) {
        switch (classId) {
            case "traffic_sv_1":
                return 3.2;
            case "traffic_sv_2":
                return 3.5;
            case "traffic_sv_3":
                return 3.8;
            case "traffic_sv_4":
                return 4.1;
            case "traffic_sv_5":
                return 4.4;
            default:
                return 0;
        }
    }

    function buildTrafficIntensityStyle(payload) {
        const roads = Array.isArray(payload?.roads) ? payload.roads : [];
        if (roads.length === 0) {
            return createEmptyTrafficIntensityStyle();
        }

        const roadRefExpression = buildNormalizedRoadRefExpression();
        const colorExpression = ["match", roadRefExpression];
        const widthExpression = ["match", roadRefExpression];

        for (const road of roads) {
            const ref = normalizeRoadRef(road?.ref);
            const classId = String(road?.classId ?? "").trim();
            const color = getTrafficColorByClassId(classId);
            const width = getTrafficWidthByClassId(classId);

            if (!ref || !color || width <= 0) {
                continue;
            }

            colorExpression.push(ref, color);
            widthExpression.push(ref, width);
        }

        colorExpression.push("rgba(0,0,0,0)");
        widthExpression.push(0);

        return {
            colorExpression,
            widthExpression
        };
    }

    function requestTrafficIntensityStyleLoad() {
        if (trafficIntensityStyle !== null || trafficIntensityLoadPromise) {
            return;
        }

        trafficIntensityLoadPromise = fetch(trafficIntensityDataUrl, { cache: "no-cache" })
            .then((response) => {
                if (!response.ok) {
                    throw new Error(`Traffic intensity dataset unavailable (${response.status}).`);
                }

                return response.json();
            })
            .then((payload) => {
                trafficIntensityStyle = buildTrafficIntensityStyle(payload);

                if (map && map.isStyleLoaded()) {
                    rebuildTileLayers();
                }
            })
            .catch(() => {
                trafficIntensityStyle = createEmptyTrafficIntensityStyle();
            })
            .finally(() => {
                trafficIntensityLoadPromise = null;
            });
    }

    function createDefaultFilters() {
        return {
            visibleLayers: layerDefinitions.map((definition) => definition.name),
            fromTime: null,
            toTime: null,
            isSecureSigned: null,
            isSecureEncrypted: null,
            stationTypes: [],
            vehicleRoles: []
        };
    }

    function escapeHtml(value) {
        if (value === null || value === undefined) {
            return "";
        }

        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/\"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function closePopup() {
        if (popup) {
            popup.remove();
            popup = null;
        }
    }

    function normalizeFilters(filters) {
        const payload = filters || {};
        const visibleLayersInput = payload.visibleLayers ?? payload.VisibleLayers;
        const fromTime = payload.fromTime ?? payload.FromTime ?? null;
        const toTime = payload.toTime ?? payload.ToTime ?? null;
        const isSecureSigned = payload.isSecureSigned ?? payload.IsSecureSigned ?? null;
        const isSecureEncrypted = payload.isSecureEncrypted ?? payload.IsSecureEncrypted ?? null;
        const stationTypesInput = payload.stationTypes ?? payload.StationTypes;
        const vehicleRolesInput = payload.vehicleRoles ?? payload.VehicleRoles;

        const visibleLayers = Array.isArray(visibleLayersInput) && visibleLayersInput.length > 0
            ? visibleLayersInput
            : layerDefinitions.map((definition) => definition.name);

        return {
            visibleLayers,
            fromTime,
            toTime,
            isSecureSigned,
            isSecureEncrypted,
            stationTypes: Array.isArray(stationTypesInput) ? stationTypesInput : [],
            vehicleRoles: Array.isArray(vehicleRolesInput) ? vehicleRolesInput : []
        };
    }

    function buildQuery(filters) {
        const params = new URLSearchParams();

        if (filters.fromTime) {
            params.append("fromTime", filters.fromTime);
        }

        if (filters.toTime) {
            params.append("toTime", filters.toTime);
        }

        if (filters.isSecureSigned !== null && filters.isSecureSigned !== undefined) {
            params.append("isSecureSigned", String(filters.isSecureSigned).toLowerCase());
        }

        if (filters.isSecureEncrypted !== null && filters.isSecureEncrypted !== undefined) {
            params.append("isSecureEncrypted", String(filters.isSecureEncrypted).toLowerCase());
        }

        for (const stationType of filters.stationTypes) {
            params.append("stationTypes", stationType);
        }

        for (const vehicleRole of filters.vehicleRoles) {
            params.append("vehicleRoles", vehicleRole);
        }

        const query = params.toString();
        return query ? `?${query}` : "";
    }

    function buildTileUrl(layerName, filters) {
        const relativeUrl = `/api/tiles/${encodeURIComponent(layerName)}/{z}/{x}/{y}${buildQuery(filters)}`;
        return `${window.location.origin}${relativeUrl}`;
    }

    function getSourceLayerName(definition) {
        if (definition.sourceType === "static-roadref") {
            return "transportation_name";
        }

        return definition.name;
    }

    function isLayerVisible(definition) {
        return currentFilters.visibleLayers.includes(definition.name);
    }

    function removeTileLayers() {
        if (!map) {
            return;
        }

        for (const definition of [...layerDefinitions].reverse()) {
            if (map.getLayer(definition.layerId)) {
                map.removeLayer(definition.layerId);
            }

            if (definition.sourceType === "static-roadref") {
                continue;
            }

            if (map.getSource(definition.sourceId)) {
                map.removeSource(definition.sourceId);
            }
        }
    }

    function buildPointLayer(definition) {
        return {
            id: definition.layerId,
            type: "circle",
            source: definition.sourceId,
            "source-layer": getSourceLayerName(definition),
            minzoom: definition.minZoom,
            maxzoom: definition.maxZoom,
            layout: {
                visibility: isLayerVisible(definition) ? "visible" : "none"
            },
            paint: {
                "circle-radius": ["interpolate", ["linear"], ["zoom"], 5, 3, 12, 5, 18, 8],
                "circle-color": definition.color,
                "circle-stroke-width": 1,
                "circle-stroke-color": "#0f172a",
                "circle-opacity": 0.9
            }
        };
    }

    function buildCorrelationLayer(definition) {
        return {
            id: definition.layerId,
            type: "line",
            source: definition.sourceId,
            "source-layer": getSourceLayerName(definition),
            minzoom: definition.minZoom,
            maxzoom: definition.maxZoom,
            layout: {
                visibility: isLayerVisible(definition) ? "visible" : "none"
            },
            paint: {
                "line-width": ["interpolate", ["linear"], ["zoom"], 5, 1, 12, 2, 18, 4],
                "line-opacity": 0.85,
                "line-color": [
                    "case",
                    ["==", ["get", "correlation_type"], "strict"],
                    "#16a34a",
                    "#dc2626"
                ]
            }
        };
    }

    function buildTrafficIntensityLayer(definition) {
        const style = trafficIntensityStyle ?? createEmptyTrafficIntensityStyle();

        return {
            id: definition.layerId,
            type: "line",
            source: "openmaptiles",
            "source-layer": getSourceLayerName(definition),
            minzoom: definition.minZoom,
            maxzoom: definition.maxZoom,
            filter: [
                "all",
                ["==", "$type", "LineString"],
                ["has", "ref"]
            ],
            layout: {
                visibility: isLayerVisible(definition) ? "visible" : "none",
                "line-cap": "round",
                "line-join": "round"
            },
            paint: {
                "line-color": style.colorExpression,
                "line-width": style.widthExpression,
                "line-opacity": 0.85,
                "line-blur": 0
            }
        };
    }

    function logTrafficIntensityMatchSnapshot(definition) {
        if (!map || !trafficIntensityStyle) {
            return;
        }

        try {
            const sourceLayer = getSourceLayerName(definition);
            const features = map.querySourceFeatures("openmaptiles", { sourceLayer });
            const refsInTile = new Set();

            for (const feature of features) {
                const ref = normalizeRoadRef(feature?.properties?.ref);
                if (ref) {
                    refsInTile.add(ref);
                }
            }

            const colorExpression = trafficIntensityStyle.colorExpression;
            const styleRefs = new Set();
            for (let index = 2; index < colorExpression.length - 1; index += 2) {
                const value = colorExpression[index];
                if (typeof value === "string") {
                    styleRefs.add(value);
                }
            }

            let matched = 0;
            for (const ref of refsInTile) {
                if (styleRefs.has(ref)) {
                    matched += 1;
                }
            }

            console.info("[TrafficIntensity] source refs:", refsInTile.size, "matched refs:", matched, "dataset refs:", styleRefs.size);
        } catch (error) {
            console.warn("[TrafficIntensity] Unable to collect debug snapshot.", error);
        }
    }

    function addTileLayers() {
        if (!map || !map.isStyleLoaded()) {
            return;
        }

        for (const definition of layerDefinitions) {
            if (definition.sourceType === "static-roadref") {
                requestTrafficIntensityStyleLoad();

                const staticLayer = buildTrafficIntensityLayer(definition);
                if (!map.getLayer(definition.layerId)) {
                    map.addLayer(staticLayer);
                    map.once("idle", () => logTrafficIntensityMatchSnapshot(definition));
                }

                continue;
            }

            map.addSource(definition.sourceId, {
                type: "vector",
                tiles: [buildTileUrl(definition.name, currentFilters)],
                tileSize: 512,
                minzoom: 0,
                maxzoom: 22
            });

            const layer = definition.type === "line"
                ? buildCorrelationLayer(definition)
                : buildPointLayer(definition);

            map.addLayer(layer);
        }
    }

    function bindLayerInteractions() {
        if (!map || interactionsBound) {
            return;
        }

        interactionsBound = true;

        for (const definition of layerDefinitions) {
            if (!map.getLayer(definition.layerId)) {
                continue;
            }

            map.on("mouseenter", definition.layerId, () => {
                map.getCanvas().style.cursor = "pointer";
            });

            map.on("mouseleave", definition.layerId, () => {
                map.getCanvas().style.cursor = "";
            });

            map.on("click", definition.layerId, (event) => {
                const feature = event.features && event.features[0];
                if (!feature) {
                    return;
                }

                closePopup();

                const props = feature.properties || {};
                const coordinates = feature.geometry && feature.geometry.type === "Point" && Array.isArray(feature.geometry.coordinates)
                    ? feature.geometry.coordinates
                    : event.lngLat
                        ? [event.lngLat.lng, event.lngLat.lat]
                        : null;

                if (!coordinates) {
                    return;
                }

                const html = buildPopupHtml(definition.name, props);
                popup = new maplibregl.Popup({
                    closeButton: true,
                    closeOnClick: true,
                    className: "v2x-map-popup",
                    maxWidth: "240px"
                })
                    .setLngLat(coordinates)
                    .setHTML(html)
                    .addTo(map);
            });
        }
    }

    function buildPopupHtml(layerName, props) {
        const schema = popupSchemas[layerName] || [];
        const title = `<div class="popup-title">${escapeHtml(layerName)} Details</div>`;

        if (schema.length === 0) {
            const fallbackRows = Object.keys(props || {})
                .sort((a, b) => a.localeCompare(b))
                .map((key) => `<div class="popup-row"><span class="popup-label">${escapeHtml(key)}</span><span class="popup-value">${escapeHtml(formatPopupValue(props[key]))}</span></div>`)
                .join("");
            return `<div class="map-popup">${title}<div class="popup-group">${fallbackRows}</div></div>`;
        }

        const groups = schema.map((group) => {
            const rows = group.fields
                .map(([label, key]) => `<div class="popup-row"><span class="popup-label">${escapeHtml(label)}</span><span class="popup-value">${escapeHtml(formatPopupValue(props[key]))}</span></div>`)
                .join("");

            return `<div class="popup-group"><div class="popup-group-title">${escapeHtml(group.title)}</div>${rows}</div>`;
        }).join("");

        return `<div class="map-popup">${title}${groups}</div>`;
    }

    function formatPopupValue(value) {
        if (value === null || value === undefined) {
            return "N/A";
        }

        if (typeof value === "string") {
            const trimmed = value.trim();
            return trimmed.length === 0 ? "N/A" : trimmed;
        }

        if (typeof value === "boolean") {
            return value ? "Yes" : "No";
        }

        return String(value);
    }

    function rebuildTileLayers() {
        if (!map || !map.isStyleLoaded()) {
            return;
        }

        removeTileLayers();
        addTileLayers();
        bindLayerInteractions();
    }

    function detachViewportChangedListener() {
        if (!map || !moveEndHandler) {
            return;
        }

        map.off("moveend", moveEndHandler);
        moveEndHandler = null;
    }

    function attachViewportChangedListener() {
        if (!map || !viewportChangedCallback || moveEndHandler) {
            return;
        }

        moveEndHandler = () => {
            if (!viewportChangedCallback) {
                return;
            }

            viewportChangedCallback.invokeMethodAsync("OnMapViewportChanged").catch(() => {
            });
        };

        map.on("moveend", moveEndHandler);
    }

    function lngLatToTile(lng, lat, zoom) {
        const normalizedZoom = Math.max(0, zoom | 0);
        const n = Math.pow(2, normalizedZoom);
        const clampedLat = Math.max(-85.05112878, Math.min(85.05112878, lat));
        const latRad = clampedLat * (Math.PI / 180);

        const tileX = Math.floor(((lng + 180) / 360) * n);
        const tileY = Math.floor(
            (1 - (Math.log(Math.tan(latRad) + (1 / Math.cos(latRad))) / Math.PI)) / 2 * n
        );

        return {
            z: normalizedZoom,
            x: Math.max(0, Math.min(n - 1, tileX)),
            y: Math.max(0, Math.min(n - 1, tileY))
        };
    }

    function getCurrentViewBounds() {
        if (!map) {
            return null;
        }

        const bounds = map.getBounds();
        const center = map.getCenter();
        const tileZoom = Math.max(0, Math.floor(map.getZoom()));
        const centerTile = lngLatToTile(center.lng, center.lat, tileZoom);

        return {
            minLatitude: bounds.getSouth(),
            maxLatitude: bounds.getNorth(),
            minLongitude: bounds.getWest(),
            maxLongitude: bounds.getEast(),
            tileZ: centerTile.z,
            tileX: centerTile.x,
            tileY: centerTile.y
        };
    }

    return {
        initializeMap: function (containerId, styleUrl, centerLongitude, centerLatitude, zoom, minZoom, maxZoom) {
            if (map) {
                detachViewportChangedListener();
                map.remove();
            }

            currentFilters = createDefaultFilters();
            interactionsBound = false;
            closePopup();

            map = new maplibregl.Map({
                container: containerId,
                style: styleUrl,
                center: [centerLongitude, centerLatitude],
                zoom,
                minZoom,
                maxZoom
            });

            map.addControl(new maplibregl.NavigationControl(), "top-right");

            map.on("load", () => {
                rebuildTileLayers();
                attachViewportChangedListener();
            });
        },

        applyTileFilters: function (filters) {
            currentFilters = normalizeFilters(filters);

            if (!map || !map.isStyleLoaded()) {
                return;
            }

            rebuildTileLayers();
        },

        forceRefreshTiles: function () {
            if (!map || !map.isStyleLoaded()) {
                return;
            }

            rebuildTileLayers();
        },

        registerViewportChangedCallback: function (dotNetRef) {
            viewportChangedCallback = dotNetRef || null;
            attachViewportChangedListener();
        },

        unregisterViewportChangedCallback: function () {
            viewportChangedCallback = null;
            detachViewportChangedListener();
        },

        getCurrentViewBounds: function () {
            return getCurrentViewBounds();
        },

        disposeMap: function () {
            if (!map) {
                return;
            }

            detachViewportChangedListener();
            closePopup();
            map.remove();
            map = null;
            currentFilters = createDefaultFilters();
            interactionsBound = false;
            viewportChangedCallback = null;
        }
    };
})();
