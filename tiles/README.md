Place one or more `.mbtiles` files in this directory for TileServer GL.

Current file:
- `zurich_switzerland.mbtiles` (covers Zurich area only)

To use Ostrava tiles:
1. Add an Ostrava or Czech Republic extract MBTiles file in this folder.
2. Remove or rename `zurich_switzerland.mbtiles` so TileServer does not auto-pick Zurich.
3. Restart TileServer:
	- `docker-compose up -d tileserver`

Active style endpoint:
- `http://localhost:8081/styles/basic-preview/style.json`
