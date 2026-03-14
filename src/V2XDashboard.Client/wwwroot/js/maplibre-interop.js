window.v2xMap = (() => {
    let map = null;
    let pendingEntities = [];
    let pendingCorrelations = [];
    let popup = null;

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

    function bindInteractions() {
        map.on("mouseenter", "entities-layer", () => {
            map.getCanvas().style.cursor = "pointer";
        });

        map.on("mouseleave", "entities-layer", () => {
            map.getCanvas().style.cursor = "";
        });

        map.on("click", "entities-layer", (event) => {
            const feature = event.features && event.features[0];
            if (!feature || !feature.geometry || !feature.geometry.coordinates) {
                return;
            }

            closePopup();

            const props = feature.properties || {};
            const coordinates = feature.geometry.coordinates;
            const html = [
                `<div><strong>${escapeHtml(props.messageType || "Entity")}</strong></div>`,
                `<div>Station: ${escapeHtml(props.stationId || "n/a")}</div>`,
                `<div>Intersection: ${escapeHtml(props.intersectionName || props.intersectionId || "n/a")}</div>`,
                `<div>Type: ${escapeHtml(props.entityType || "n/a")}</div>`,
                `<div>Speed: ${escapeHtml(props.speed ?? "n/a")}</div>`,
                `<div>Heading: ${escapeHtml(props.heading ?? "n/a")}</div>`,
                `<div>Time: ${escapeHtml(props.generationTime || "n/a")}</div>`
            ].join("");

            popup = new maplibregl.Popup({ closeButton: true, closeOnClick: true })
                .setLngLat(coordinates)
                .setHTML(html)
                .addTo(map);
        });

        map.on("mouseenter", "correlations-layer", () => {
            map.getCanvas().style.cursor = "pointer";
        });

        map.on("mouseleave", "correlations-layer", () => {
            map.getCanvas().style.cursor = "";
        });

        map.on("click", "correlations-layer", (event) => {
            const feature = event.features && event.features[0];
            if (!feature || !event.lngLat) {
                return;
            }

            closePopup();

            const props = feature.properties || {};
            const html = [
                "<div><strong>Correlation</strong></div>",
                `<div>Type: ${escapeHtml(props.correlationType || "n/a")}</div>`,
                `<div>Request: ${escapeHtml(props.requestId || "n/a")}</div>`
            ].join("");

            popup = new maplibregl.Popup({ closeButton: true, closeOnClick: true })
                .setLngLat(event.lngLat)
                .setHTML(html)
                .addTo(map);
        });
    }

    function ensureEntityLayers() {
        if (!map.getSource("entities-source")) {
            map.addSource("entities-source", {
                type: "geojson",
                data: {
                    type: "FeatureCollection",
                    features: []
                }
            });

            map.addLayer({
                id: "entities-layer",
                type: "circle",
                source: "entities-source",
                paint: {
                    "circle-radius": [
                        "case",
                        ["==", ["get", "entityType"], "RSU"],
                        7,
                        5
                    ],
                    "circle-color": [
                        "case",
                        ["==", ["get", "messageType"], "CAM"],
                        "#22c55e",
                        ["==", ["get", "messageType"], "SREM"],
                        "#8b5cf6",
                        ["==", ["get", "messageType"], "DENM"],
                        "#ef4444",
                        ["==", ["get", "messageType"], "SSEM"],
                        "#f97316",
                        ["==", ["get", "messageType"], "SPATEM"],
                        "#f59e0b",
                        ["==", ["get", "messageType"], "MAPEM"],
                        "#0ea5e9",
                        "#64748b"
                    ],
                    "circle-stroke-width": 1,
                    "circle-stroke-color": "#0f172a"
                }
            });
        }
    }

    function ensureCorrelationLayers() {
        if (!map.getSource("correlations-source")) {
            map.addSource("correlations-source", {
                type: "geojson",
                data: {
                    type: "FeatureCollection",
                    features: []
                }
            });

            map.addLayer({
                id: "correlations-layer",
                type: "line",
                source: "correlations-source",
                paint: {
                    "line-width": 2,
                    "line-opacity": 0.8,
                    "line-color": [
                        "case",
                        ["==", ["get", "correlationType"], "strict"],
                        "#16a34a",
                        "#dc2626"
                    ]
                }
            });
        }
    }

    function toEntityFeatures(entities) {
        return entities
            .filter(e => Number.isFinite(e.latitude) && Number.isFinite(e.longitude))
            .map(e => ({
                type: "Feature",
                geometry: {
                    type: "Point",
                    coordinates: [e.longitude, e.latitude]
                },
                properties: {
                    entityType: e.entityType,
                    messageType: e.messageType,
                    stationId: e.stationId,
                    intersectionId: e.intersectionId,
                    intersectionName: e.intersectionName,
                    speed: e.speed,
                    heading: e.heading,
                    generationTime: e.generationTime
                }
            }));
    }

    function toCorrelationFeatures(correlations) {
        return correlations
            .filter(c => Number.isFinite(c.sremLatitude) && Number.isFinite(c.sremLongitude) && Number.isFinite(c.ssemLatitude) && Number.isFinite(c.ssemLongitude))
            .map(c => ({
                type: "Feature",
                geometry: {
                    type: "LineString",
                    coordinates: [
                        [c.sremLongitude, c.sremLatitude],
                        [c.ssemLongitude, c.ssemLatitude]
                    ]
                },
                properties: {
                    correlationType: c.correlationType,
                    requestId: c.requestId
                }
            }));
    }

    return {
        initializeMap: function (containerId, styleUrl, centerLongitude, centerLatitude, zoom, minZoom, maxZoom) {
            if (map) {
                map.remove();
            }

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
                ensureEntityLayers();
                ensureCorrelationLayers();
                bindInteractions();

                if (pendingEntities.length > 0) {
                    const entitySource = map.getSource("entities-source");
                    entitySource.setData({
                        type: "FeatureCollection",
                        features: toEntityFeatures(pendingEntities)
                    });
                }

                if (pendingCorrelations.length > 0) {
                    const correlationSource = map.getSource("correlations-source");
                    correlationSource.setData({
                        type: "FeatureCollection",
                        features: toCorrelationFeatures(pendingCorrelations)
                    });
                }
            });
        },

        setEntities: function (entities) {
            pendingEntities = entities || [];

            if (!map || !map.isStyleLoaded()) {
                return;
            }

            ensureEntityLayers();

            const source = map.getSource("entities-source");
            source.setData({
                type: "FeatureCollection",
                features: toEntityFeatures(pendingEntities)
            });
        },

        setCorrelations: function (correlations) {
            pendingCorrelations = correlations || [];

            if (!map || !map.isStyleLoaded()) {
                return;
            }

            ensureCorrelationLayers();

            const source = map.getSource("correlations-source");
            source.setData({
                type: "FeatureCollection",
                features: toCorrelationFeatures(pendingCorrelations)
            });
        },

        disposeMap: function () {
            if (!map) {
                return;
            }

            map.remove();
            map = null;
            pendingEntities = [];
            pendingCorrelations = [];
            closePopup();
        }
    };
})();
