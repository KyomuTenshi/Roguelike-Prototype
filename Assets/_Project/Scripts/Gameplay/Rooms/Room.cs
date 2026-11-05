using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RulesShift
{
    /// <summary>
    /// A room built at runtime from an ASCII layout and the sprites of one tileset.
    /// Floor cell (x, y) covers world [x, x+1] × [y, y+1]; the door is in the top wall.
    /// </summary>
    public class Room : MonoBehaviour
    {
        public int W { get; private set; }
        public int H { get; private set; }
        public Theme Theme { get; private set; }
        public Transform Content { get; private set; }   // enemies, projectiles, effects

        public Vector2 PlayerSpawn { get; private set; }
        public readonly List<Vector2> EnemySpawns = new List<Vector2>();
        public readonly List<Vector2> Buttons = new List<Vector2>();
        public readonly List<Vector2> ItemSpots = new List<Vector2>();   // shop wares, shrine, fountain
        public Vector2? KeyChest { get; private set; }
        public Vector2? BossSpawn { get; private set; }
        public readonly List<Vector2Int> FloorCells = new List<Vector2Int>();

        public bool DoorOpen { get; private set; }
        // room enough above the doors that their signs never hide under the HUD
        // the caves show only the rims of the side plateaus, not the dark beyond them
        public Rect Bounds => Theme == Theme.Cave ? new Rect(-1f, -1.5f, W + 2f, H + 8.5f) : new Rect(-1.5f, -1.5f, W + 3f, H + 8.5f);
        public int DoorCount => doors.Count;
        public Vector2 DoorPos(int i) => new Vector2(doors[i].cx, H);

        class DoorSlot
        {
            public float cx;
            public SpriteRenderer sr, sr2;   // sr2: the second half of a two-tile door (Retro)
            public Sprite openSprite;
        }
        readonly List<DoorSlot> doors = new List<DoorSlot>();
        public Vector2 Center => new Vector2(W / 2f, H / 2f);

        bool[,] blocked;
        bool[,] lava;
        Tilemap floorMap, wallMap, hazardMap, solidMap;
        Tile solidTile, lavaTileA, lavaTileB, warnTile;
        readonly Dictionary<Sprite, Tile> tiles = new Dictionary<Sprite, Tile>();
        float lavaAnim;
        bool lavaFrame;

        // ---------------- building ----------------

        /// <param name="doorCount">1 = one door in the middle, 2 = a choice of two doors.</param>
        /// <param name="modernStyle">Modern theme: wall and floor set suffix ("", "_mint", "_gray"). Cave theme: "fire" or "ice".</param>
        public static Room Build(string[] layout, Theme theme, Law law, Transform parent, int doorCount = 1, string modernStyle = "")
        {
            var go = new GameObject("Room");
            go.transform.SetParent(parent, false);
            var room = go.AddComponent<Room>();
            room.doorCount = Mathf.Clamp(doorCount, 1, 2);
            room.style = modernStyle ?? "";
            room.Make(layout, theme, law);
            return room;
        }

        int doorCount = 1;
        string style = "";

        Sprite S(string name)
        {
            var a = GameAssets.I;
            if (!a) return null;
            if (Theme == Theme.Modern) return a.Modern(name) ?? a.D(name);
            if (Theme == Theme.Retro) return a.Retro(RetroAlias(name)) ?? a.D(name);
            if (Theme == Theme.Cave && name.StartsWith("wall_hole")) return a.CaveDecor(GameAssets.CaveRocks + (name.EndsWith("1") ? 0 : 3)) ?? a.D(name);
            return Theme == Theme.Classic ? (a.C(name) ?? a.D(name)) : a.D(name);
        }

        /// <summary>Names the code asks for in every theme → their SGQ counterparts.</summary>
        static string RetroAlias(string name)
        {
            switch (name)
            {
                case "wall_hole_1": case "wall_hole_2": return "boulder_cracked";
                case "box": return "crate";
                case "boxes_stacked": return "barrel";
                default: return name;
            }
        }

        Tile T(Sprite s)
        {
            if (!s) return null;
            if (tiles.TryGetValue(s, out var t)) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = s;
            t.colliderType = Tile.ColliderType.None;
            tiles[s] = t;
            return t;
        }

        Tilemap NewMap(Transform grid, string name, int order, bool renderer = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            var map = go.AddComponent<Tilemap>();
            map.tileAnchor = new Vector3(0.5f, 0f, 0f);   // sprites have a bottom-center pivot
            if (renderer)
            {
                var r = go.AddComponent<TilemapRenderer>();
                r.sortingOrder = order;
                Gfx.Lit(r);
            }
            return map;
        }

        void Make(string[] layout, Theme theme, Law law)
        {
            Theme = theme;
            H = layout.Length;
            W = layout[0].Length;
            blocked = new bool[W, H];
            lava = new bool[W, H];

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(transform, false);
            gridGo.AddComponent<Grid>();
            floorMap = NewMap(gridGo.transform, "Floor", -100);
            hazardMap = NewMap(gridGo.transform, "Hazard", -95);
            wallMap = NewMap(gridGo.transform, "Walls", -50);
            solidMap = NewMap(gridGo.transform, "Solid", 0, false);
            solidMap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);   // grid colliders exactly on their cells
            solidMap.gameObject.AddComponent<TilemapCollider2D>();

            solidTile = ScriptableObject.CreateInstance<Tile>();
            solidTile.sprite = Gfx.White;
            solidTile.colliderType = Tile.ColliderType.Grid;

            var content = new GameObject("Content");
            content.transform.SetParent(transform, false);
            Content = content.transform;

            var rnd = new System.Random(layout.GetHashCode() ^ (int)law * 7919);
            BuildFloor(rnd);
            BuildWalls(rnd);
            BuildDoors();
            ReadLayout(layout, rnd);
            BuildDecor(law, rnd);
        }

        void BuildFloor(System.Random rnd)
        {
            if (Theme == Theme.Retro) { BuildRetroFloor(rnd); return; }
            if (Theme == Theme.Cave) { BuildCaveFloor(rnd); return; }
            if (Theme == Theme.Modern)
            {
                var parquet = T(S("floor" + style) ?? S("floor"));
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) floorMap.SetTile(new Vector3Int(x, y, 0), parquet);
                return;
            }
            string[] variants = Theme == Theme.Classic
                ? new[] { "floor_stain_1", "floor_stain_2", "floor_stain_3", "floor_light" }
                : new[] { "floor_2", "floor_3", "floor_4", "floor_5", "floor_6", "floor_7", "floor_8" };
            string main = Theme == Theme.Classic ? "floor_plain" : "floor_1";
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                string n = rnd.NextDouble() < (Theme == Theme.Classic ? 0.88 : 0.8) ? main : variants[rnd.Next(variants.Length)];
                floorMap.SetTile(new Vector3Int(x, y, 0), T(S(n)));
            }
        }

        void BuildWalls(System.Random rnd)
        {
            if (Theme == Theme.Modern) { BuildModernWalls(); return; }
            if (Theme == Theme.Retro) { BuildRetroWalls(rnd); return; }
            if (Theme == Theme.Cave) { BuildCaveWalls(rnd); return; }
            bool c = Theme == Theme.Classic;
            var cap = T(S(c ? "wall_top_center" : "wall_top_mid"));
            string[] faces = c ? new[] { "wall_center", "wall_center", "wall_center", "wall_left", "wall_right", "wall_missing_brick_1" }
                               : new[] { "wall_mid", "wall_mid", "wall_mid", "wall_mid", "wall_mid", "wall_mid", "wall_mid", "wall_hole_1" };
            var sideL = T(S(c ? "Wall_outer_w" : "wall_outer_mid_left"));
            var sideR = T(S(c ? "Wall_outer_e" : "wall_outer_mid_right"));
            var capL = T(S(c ? "Wall_outer_nw" : "wall_outer_top_left"));
            var capR = T(S(c ? "Wall_outer_ne" : "wall_outer_top_right"));
            var faceL = T(S(c ? "Wall_outer_sw" : "wall_outer_front_left"));
            var faceR = T(S(c ? "Wall_outer_se" : "wall_outer_front_right"));

            for (int x = 0; x < W; x++)
            {
                var face = T(S(faces[rnd.Next(faces.Length)]));
                wallMap.SetTile(new Vector3Int(x, H, 0), face);        // top wall face
                wallMap.SetTile(new Vector3Int(x, H + 1, 0), cap);     // top wall cap
                wallMap.SetTile(new Vector3Int(x, 0, 0), cap);         // bottom wall cap over the last floor row
                wallMap.SetTile(new Vector3Int(x, -1, 0), T(S(faces[rnd.Next(faces.Length)]))); // bottom wall face
                Solid(x, H); Solid(x, H + 1); Solid(x, -1);
            }
            for (int y = 0; y < H; y++)
            {
                wallMap.SetTile(new Vector3Int(-1, y, 0), sideL);
                wallMap.SetTile(new Vector3Int(W, y, 0), sideR);
                Solid(-1, y); Solid(W, y);
            }
            wallMap.SetTile(new Vector3Int(-1, H, 0), faceL);
            wallMap.SetTile(new Vector3Int(W, H, 0), faceR);
            wallMap.SetTile(new Vector3Int(-1, H + 1, 0), capL);
            wallMap.SetTile(new Vector3Int(W, H + 1, 0), capR);
            wallMap.SetTile(new Vector3Int(-1, -1, 0), faceL);
            wallMap.SetTile(new Vector3Int(W, -1, 0), faceR);
            for (int y = -1; y <= H + 1; y++) { Solid(-1, y); Solid(W, y); }
        }

        /// <summary>A wooden wall two tiles high on top, dark ceiling all around (Modern tiles).</summary>
        void BuildModernWalls()
        {
            var ceiling = T(S("void"));
            for (int x = 0; x < W; x++)
            {
                string side = x == 0 ? "_l" : x == W - 1 ? "_r" : "";
                wallMap.SetTile(new Vector3Int(x, H, 0), T(S("wall_bottom" + side + style) ?? S("wall_bottom" + side)));
                wallMap.SetTile(new Vector3Int(x, H + 1, 0), T(S("wall_top" + side + style) ?? S("wall_top" + side)));
                wallMap.SetTile(new Vector3Int(x, H + 2, 0), ceiling);
                wallMap.SetTile(new Vector3Int(x, -1, 0), ceiling);
                Solid(x, H); Solid(x, H + 1); Solid(x, -1);
            }
            for (int y = -1; y <= H + 2; y++)
            {
                wallMap.SetTile(new Vector3Int(-1, y, 0), ceiling);
                wallMap.SetTile(new Vector3Int(W, y, 0), ceiling);
                Solid(-1, y); Solid(W, y);
            }
        }

        // ---------------- the Retro theme (SGQ Dungeon) ----------------

        string RetroVar(string name, System.Random rnd) => rnd.NextDouble() < 0.5 ? name : name + "_2";

        void BuildRetroFloor(System.Random rnd)
        {
            var main = T(S("floor"));
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var t = rnd.NextDouble() < 0.12 ? T(S("floor_v" + (1 + rnd.Next(8)))) : main;
                floorMap.SetTile(new Vector3Int(x, y, 0), t ?? main);
            }
        }

        /// <summary>A brick mass seen from above all around the room (one tile of face, fill and top edge on the door side).</summary>
        void BuildRetroWalls(System.Random rnd)
        {
            for (int x = 0; x < W; x++)
            {
                wallMap.SetTile(new Vector3Int(x, H, 0), T(S(RetroVar("wall_face", rnd))));
                wallMap.SetTile(new Vector3Int(x, H + 1, 0), T(S(RetroVar("wall_fill", rnd))));
                wallMap.SetTile(new Vector3Int(x, H + 2, 0), T(S(RetroVar("wall_top", rnd))));
                wallMap.SetTile(new Vector3Int(x, -1, 0), T(S(RetroVar("wall_top", rnd))));
                Solid(x, H); Solid(x, H + 1); Solid(x, -1);
            }
            for (int y = 0; y < H; y++)
            {
                wallMap.SetTile(new Vector3Int(-1, y, 0), T(S(RetroVar("wall_right", rnd))));
                wallMap.SetTile(new Vector3Int(W, y, 0), T(S(RetroVar("wall_left", rnd))));
            }
            wallMap.SetTile(new Vector3Int(-1, H, 0), T(S("wall_corner_br")));
            wallMap.SetTile(new Vector3Int(W, H, 0), T(S("wall_corner_bl")));
            wallMap.SetTile(new Vector3Int(-1, H + 1, 0), T(S("wall_right")));
            wallMap.SetTile(new Vector3Int(W, H + 1, 0), T(S("wall_left")));
            wallMap.SetTile(new Vector3Int(-1, H + 2, 0), T(S("wall_corner_tr")));
            wallMap.SetTile(new Vector3Int(W, H + 2, 0), T(S("wall_corner_tl")));
            wallMap.SetTile(new Vector3Int(-1, -1, 0), T(S("wall_corner_tr")));
            wallMap.SetTile(new Vector3Int(W, -1, 0), T(S("wall_corner_tl")));
            for (int y = -1; y <= H + 2; y++) { Solid(-1, y); Solid(W, y); }

            // the brick mass goes on past the edges of the screen
            for (int y = -6; y <= H + 10; y++)
            for (int x = -6; x <= W + 5; x++)
            {
                bool inside = x >= -1 && x <= W && y >= -1 && y <= H + 2;
                if (!inside) wallMap.SetTile(new Vector3Int(x, y, 0), T(S(RetroVar("wall_fill", rnd))));
            }
        }

        /// <summary>Torches and braziers on the wall, clutter on the floor, a mine track with a cart.</summary>
        void RetroDecor(System.Random rnd)
        {
            var a = GameAssets.I;
            if (!a) return;
            int[] spots = { 3, W - 4, 7, W - 8 };
            var glow = new Color(0.75f, 1f, 0.6f);
            for (int i = 0; i < spots.Length; i++)
            {
                int x = spots[i];
                if (x < 1 || x >= W - 1 || NearDoor(x + 0.5f)) continue;
                if (i >= 2 && W < 20) continue;
                bool torch = i % 2 == 0;
                var frames = a.Anim(torch ? "sgq_torch_anim" : "sgq_brazier_anim");
                if (frames.Length == 0) continue;
                var sr = PropSprite(frames[0], new Vector2(x + 0.5f, H + (torch ? 0.1f : 0f)), -39);
                Animate(sr, frames, 8f);
                if (sr) AddTorch(sr, LightUtil.Point(sr.transform, new Vector3(0f, torch ? 0.6f : 1.4f, 0f), glow, 3.2f, 0.8f));
            }

            string[] clutter = { "mushrooms", "rocks", "skull", "fern", "dead_bush", "rock", "ore", "pickaxe" };
            int placed = 0;
            for (int k = 0; k < 40 && placed < 6; k++)
            {
                int x = 1 + rnd.Next(Mathf.Max(1, W - 2)), y = 1 + rnd.Next(Mathf.Max(1, H - 3));
                if (!IsFree(x, y) || Vector2.Distance(new Vector2(x + 0.5f, y), PlayerSpawn) < 2.5f) continue;
                Prop(clutter[rnd.Next(clutter.Length)], new Vector2(x + 0.5f, y + 0.1f), -60);
                placed++;
            }

            // a short mine track along the bottom wall, a cart left on it
            int len = Mathf.Min(7, W / 3);
            int x0 = 1 + rnd.Next(Mathf.Max(1, W - len - 2));
            for (int i = 0; i < len; i++) Prop("rail", new Vector2(x0 + i + 0.5f, 0f), -62);
            var cart = Prop(rnd.NextDouble() < 0.5 ? "cart_side" : "cart", new Vector2(x0 + len - 1.5f, 0.05f), 0);
            PushCart.Attach(cart, x0 + 0.5f, x0 + len - 0.5f);
            if (W >= 20) Prop("cart_full", new Vector2(x0 + 0.5f, 0.05f), -61);
        }

        // ---------------- the Cave theme (RPG Worlds: Caves) ----------------

        static Sprite Cave(int decor) => GameAssets.I ? GameAssets.I.CaveDecor(decor) : null;

        Tile CT(int tx, int ty) => T(GameAssets.I ? GameAssets.I.CaveTile(tx, ty) : null);

        /// <summary>Starts of two-unit tiles covering [0, n]: 0, 2, 4… (the last one overlaps by a unit when n is odd).</summary>
        static List<int> Span(int n)
        {
            var list = new List<int>();
            for (int i = 0; i + 2 <= n; i += 2) list.Add(i);
            if (n % 2 == 1) list.Add(Mathf.Max(0, n - 2));
            return list;
        }

        /// <summary>
        /// Starts of two-unit floor tiles without any overlap: an odd size starts one unit early, under the rim
        /// (overlapping tiles showed a thin seam across the floor).
        /// </summary>
        static List<int> FloorSpan(int n)
        {
            var list = new List<int>();
            for (int i = n % 2 == 1 ? -1 : 0; i < n; i += 2) list.Add(i);
            return list;
        }

        void BuildCaveFloor(System.Random rnd)
        {
            bool fire = style == "fire";
            foreach (int y in FloorSpan(H))
            foreach (int x in FloorSpan(W))
            {
                int tx, ty;
                double r = rnd.NextDouble();
                // only tiles surrounded by ground in the sheet: their edges never show a seam
                if (r < 0.7) { tx = rnd.Next(2); ty = 47; }                                      // bare ground
                else if (r < 0.85) { tx = rnd.Next(4); ty = 43; }                                // pebbles
                else { tx = 5 + rnd.Next(4); ty = fire ? 45 + rnd.Next(2) : 41 + rnd.Next(2); }  // dry or frosted grass
                floorMap.SetTile(new Vector3Int(x, y, 0), CT(tx, ty));
            }
            // under the rims: ground right next to the pit, the dark plateau further out, so the
            // transparent edges of the rock tiles never show the black background
            // (on the same grid as the floor, so nothing overlaps it)
            int x0 = W % 2 == 1 ? -1 : 0, y0 = H % 2 == 1 ? -1 : 0;
            int xEnd = x0 + ((W - x0 + 1) / 2) * 2, yEnd = y0 + ((H - y0 + 1) / 2) * 2;   // first column / row past the floor
            for (int y = y0 - 12; y <= H + 12; y += 2)
            for (int x = x0 - 12; x <= W + 10; x += 2)
            {
                if (x >= x0 && x < xEnd && y >= y0 && y < yEnd) continue;
                bool ring = ((x == x0 - 2 || x == xEnd) && y >= y0 && y < yEnd) || (y == y0 - 2 && x >= x0 && x < xEnd);
                floorMap.SetTile(new Vector3Int(x, y, 0), ring ? CT(rnd.Next(2), 47) : CT(3 + rnd.Next(4), 2 + rnd.Next(3)));
            }
        }

        /// <summary>
        /// The room is a pit between rock plateaus: a cliff with its glowing face above the doors,
        /// rocky rims on the sides and at the bottom. Cave tiles are 32 px = two cells.
        /// </summary>
        void BuildCaveWalls(System.Random rnd)
        {
            int[] mid = { 3, 4, 5, 6 };
            var xs = Span(W);
            foreach (int x in xs) CliffColumn(x, mid[rnd.Next(mid.Length)], rnd);
            CliffColumn(-2, mid[rnd.Next(mid.Length)], rnd);
            CliffColumn(W, mid[rnd.Next(mid.Length)], rnd);
            foreach (int y in Span(H - 1))
            {
                wallMap.SetTile(new Vector3Int(-2, y, 0), CT(7, 2 + rnd.Next(3)));
                wallMap.SetTile(new Vector3Int(W, y, 0), CT(2, 2 + rnd.Next(3)));
            }
            foreach (int x in xs)
            {
                wallMap.SetTile(new Vector3Int(x, -2, 0), CT(mid[rnd.Next(mid.Length)], 0));
                wallMap.SetTile(new Vector3Int(x, -4, 0), CT(3 + rnd.Next(4), 3));
            }
            wallMap.SetTile(new Vector3Int(-2, -2, 0), CT(7, 0));
            wallMap.SetTile(new Vector3Int(W, -2, 0), CT(2, 0));
            wallMap.SetTile(new Vector3Int(-2, -4, 0), CT(4, 3));
            wallMap.SetTile(new Vector3Int(W, -4, 0), CT(5, 3));

            // the dark plateau all around, past the edges of the screen
            for (int y = -10; y <= H + 12; y++)
            for (int x = -10; x <= W + 10; x++)
            {
                bool covered = x >= -2 && x < W + 2 && y >= -4 && y < H + 7;
                if (!covered) wallMap.SetTile(new Vector3Int(x, y, 0), CT(3 + rnd.Next(4), 1 + rnd.Next(4)));
            }

            for (int x = 0; x < W; x++) { Solid(x, H); Solid(x, H + 1); Solid(x, -1); }
            for (int y = -1; y <= H + 1; y++) { Solid(-1, y); Solid(W, y); }
        }

        /// <summary>One column of the cliff above the doors (cx = column in the sheet: 7 left edge, 2 right edge, 3-6 middle).</summary>
        void CliffColumn(int x, int cx, System.Random rnd)
        {
            wallMap.SetTile(new Vector3Int(x, H - 1, 0), CT(cx, 8));   // the cliff's foot
            wallMap.SetTile(new Vector3Int(x, H + 1, 0), CT(cx, 7));   // its face
            wallMap.SetTile(new Vector3Int(x, H + 3, 0), CT(cx, 6));   // its rim
            wallMap.SetTile(new Vector3Int(x, H + 5, 0), CT(cx == 7 || cx == 2 ? cx : 3 + rnd.Next(4), 3));   // the dark plateau
        }

        /// <summary>Rock formations and crystals at the foot of the cliffs, glowing crystals on them, pebbles on the floor.</summary>
        void CaveDecor(System.Random rnd)
        {
            var a = GameAssets.I;
            if (!a) return;
            bool fire = style == "fire";
            var glow = fire ? new Color(1f, 0.5f, 0.2f) : new Color(0.45f, 0.75f, 1f);

            // big formations in the top corners
            var left = PropSprite(Cave(fire ? GameAssets.CaveRedRocks + 1 : GameAssets.CaveBlue + 1), new Vector2(1.3f, H - 0.5f), -39);
            var right = PropSprite(Cave(fire ? GameAssets.CaveRedRocks + 2 : GameAssets.CaveBlue + 3), new Vector2(W - 1.3f, H - 0.5f), -39);
            if (left) LightUtil.Point(left.transform, new Vector3(0f, 1f, 0f), glow, 3.5f, 0.9f);
            if (right) LightUtil.Point(right.transform, new Vector3(0f, 1f, 0f), glow, 3.5f, 0.9f);

            // glowing crystals on the cliff face
            int[] spots = { 4, W - 5, 8, W - 9 };
            for (int i = 0; i < spots.Length; i++)
            {
                int x = spots[i];
                if (x < 2 || x >= W - 2 || NearDoor(x + 0.5f)) continue;
                if (i >= 2 && W < 20) continue;
                var frames = a.CaveLight(rnd.Next(4), rnd.Next(4));
                if (frames.Length == 0) continue;
                var sr = PropSprite(frames[0], new Vector2(x + 0.5f, H + 0.6f), -39);
                Animate(sr, frames, 6f);
                LightUtil.Point(sr.transform, new Vector3(0f, 0.6f, 0f), fire ? new Color(1f, 0.85f, 0.4f) : new Color(0.7f, 1f, 0.6f), 2.6f, 0.9f);
            }

            // crystals and rocks along the side rims
            for (int y = 2; y < H - 2; y += 3 + rnd.Next(2))
            {
                int pick = fire ? GameAssets.CaveRocks + rnd.Next(4) : GameAssets.CaveBlueSmall + rnd.Next(4);
                PropSprite(Cave(pick), new Vector2(-0.35f, y), -39);
                pick = fire ? GameAssets.CaveRocks2 + rnd.Next(4) : GameAssets.CaveBlueSmall + rnd.Next(4);
                var sr = PropSprite(Cave(pick), new Vector2(W + 0.35f, y + 1), -39);
                if (sr) sr.flipX = true;
            }

            // a few pebbles and crystals on the floor (no collision)
            int placed = 0;
            for (int k = 0; k < 40 && placed < 6; k++)
            {
                int x = 1 + rnd.Next(Mathf.Max(1, W - 2)), y = 1 + rnd.Next(Mathf.Max(1, H - 3));
                if (!IsFree(x, y) || Vector2.Distance(new Vector2(x + 0.5f, y), PlayerSpawn) < 2.5f) continue;
                int pick = fire || rnd.NextDouble() < 0.5 ? GameAssets.CaveRocks + 1 + rnd.Next(2) * 2 : GameAssets.CaveBlueSmall + rnd.Next(3);
                PropSprite(Cave(pick), new Vector2(x + 0.5f, y + 0.1f), -60);
                placed++;
            }
        }

        void Solid(int x, int y) => solidMap.SetTile(new Vector3Int(x, y, 0), solidTile);

        void BuildDoors()
        {
            int half = W / 2;
            int offset = W >= 24 ? 6 : 4;
            var centers = doorCount == 2 ? new[] { half - offset, half + offset } : new[] { half };
            bool c = Theme == Theme.Classic || Theme == Theme.Modern || Theme == Theme.Retro;   // they name their door sprites door_closed / door_open
            foreach (int cx in centers)
            {
                var slot = new DoorSlot { cx = cx };
                if (Theme == Theme.Retro)
                {
                    // the doorway is two cells wide: two round gates side by side
                    slot.sr = Prop("door_closed", new Vector2(cx - 0.5f, H), -40);
                    slot.sr2 = Prop("door_closed", new Vector2(cx + 0.5f, H), -40);
                    if (slot.sr2) slot.sr2.flipX = true;
                    slot.openSprite = S("door_open");
                    doors.Add(slot);
                    continue;
                }
                slot.sr = Prop(c ? "door_closed" : "doors_leaf_closed", new Vector2(cx, H), -40);
                slot.openSprite = S(c ? "door_open" : "doors_leaf_open");
                if (!c)
                {
                    Prop("doors_frame_top", new Vector2(cx, H + 2), -40);
                    Prop("doors_frame_left", new Vector2(cx - 1.5f, H), -41);
                    Prop("doors_frame_right", new Vector2(cx + 1.5f, H), -41);
                }
                doors.Add(slot);
            }
        }

        /// <summary>Text above a door: what law waits behind it.</summary>
        public void SetDoorLabel(int i, string text, Color color)
        {
            if (i < 0 || i >= doors.Count) return;
            var pos = new Vector2(doors[i].cx, H + (Theme == Theme.Dungeon || Theme == Theme.Cave ? 3.5f : 2.6f));
            WorldLabel.Create(transform, pos + new Vector2(0.06f, -0.06f), text, new Color(0f, 0f, 0f, 0.85f), 44);
            WorldLabel.Create(transform, pos, text, color, 45);
        }

        /// <summary>This door leads into another world: a swirling glow in the doorway.</summary>
        public void Portal(int i, Color color)
        {
            if (i < 0 || i >= doors.Count) return;
            var at = new Vector2(doors[i].cx, H + 0.9f);
            var fx = Fx.Loop("warp", transform, at, 0.9f, -35) ?? Fx.Loop("sparkle", transform, at, 0.6f, -35);
            if (fx)
            {
                var sr = fx.GetComponent<SpriteRenderer>();
                if (sr) sr.color = new Color(color.r, color.g, color.b, 0.85f);
            }
            LightUtil.Point(transform, new Vector3(doors[i].cx, H + 0.5f, 0f), color, 3.5f, 1.2f);
        }

        public void OpenDoor()
        {
            if (DoorOpen) return;
            DoorOpen = true;
            foreach (var d in doors)
            {
                if (d.sr && d.openSprite) d.sr.sprite = d.openSprite;
                if (d.sr2 && d.openSprite) d.sr2.sprite = d.openSprite;
                int cx = Mathf.RoundToInt(d.cx);
                solidMap.SetTile(new Vector3Int(cx - 1, H, 0), null);
                solidMap.SetTile(new Vector3Int(cx, H, 0), null);
                // light up the doorway a bit so it's readable in dark rooms
                if (Rules.Current == Law.Crypt || Rules.Current == Law.Neon)
                    LightUtil.Point(transform, new Vector3(d.cx, H + 0.5f, 0f), new Color(1f, 0.85f, 0.5f), 3f, 1.2f);
            }
        }

        /// <summary>Index of the open doorway the feet are in, or -1.</summary>
        public int DoorAt(Vector2 feet)
        {
            if (!DoorOpen || feet.y <= H - 0.35f) return -1;
            for (int i = 0; i < doors.Count; i++)
                if (Mathf.Abs(feet.x - doors[i].cx) < 1.1f) return i;
            return -1;
        }

        /// <summary>Closest door to a point (for the "what's behind" hint).</summary>
        public int NearestDoor(Vector2 p, out float distance)
        {
            int best = -1;
            distance = float.MaxValue;
            for (int i = 0; i < doors.Count; i++)
            {
                float d = Vector2.Distance(p, new Vector2(doors[i].cx, H));
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        void ReadLayout(string[] layout, System.Random rnd)
        {
            bool c = Theme == Theme.Classic;
            bool playerFound = false;
            for (int row = 0; row < H; row++)
            for (int x = 0; x < W; x++)
            {
                int y = H - 1 - row;
                char ch = x < layout[row].Length ? layout[row][x] : '.';
                var foot = new Vector2(x + 0.5f, y + 0.2f);
                switch (ch)
                {
                    case 'C':
                        if (Theme == Theme.Cave) PropSprite(Cave(rnd.NextDouble() < 0.5 ? GameAssets.CaveSpires + 2 : GameAssets.CaveSpires + 3), new Vector2(x + 0.5f, y), 0);
                        else Prop("column", new Vector2(x + 0.5f, y), 0);
                        Block(x, y);
                        break;
                    case 'X':
                        // crates break under shots; in the caves they are crystals and magma
                        if (!MakeBreakable(new Vector2Int(x, y), rnd, false)) Block(x, y);
                        break;
                    case 'h':
                        if (c) Prop("box", new Vector2(x + 0.5f, y), 0);
                        else if (Theme == Theme.Retro) Prop("hole", new Vector2(x + 0.5f, y), -95);
                        else if (Theme == Theme.Cave) Prop("hole", new Vector2(x + 0.5f, y), -95);
                        else floorMap.SetTile(new Vector3Int(x, y, 0), T(S("hole")));
                        Block(x, y);
                        break;
                    case 's':
                        if (Theme == Theme.Cave) PropSprite(Cave(GameAssets.CaveRocks + 1), new Vector2(x + 0.5f, y), -60);
                        else Prop("skull", new Vector2(x + 0.5f, y), -60);
                        break;
                    case 'E': EnemySpawns.Add(foot); break;
                    case 'P': PlayerSpawn = foot; playerFound = true; break;
                    case 'K': KeyChest = foot; Block(x, y); break;
                    case 'B': Buttons.Add(new Vector2(x + 0.5f, y + 0.5f)); break;
                    case 'M': BossSpawn = foot; break;
                    case 'S': ItemSpots.Add(new Vector2(x + 0.5f, y + 0.3f)); break;
                }
                if (!blocked[x, y]) FloorCells.Add(new Vector2Int(x, y));
            }
            if (!playerFound) PlayerSpawn = new Vector2(W / 2f, 1.2f);
            if (EnemySpawns.Count == 0) EnemySpawns.Add(new Vector2(W / 2f, H - 2f));
        }

        void Block(int x, int y)
        {
            blocked[x, y] = true;
            Solid(x, y);
        }

        // ---------------- interactive bits ----------------

        class Torch { public SpriteRenderer sr; public UnityEngine.Rendering.Universal.Light2D light; public bool lit = true; }
        readonly List<Torch> torches = new List<Torch>();

        void AddTorch(SpriteRenderer sr, UnityEngine.Rendering.Universal.Light2D light)
        {
            if (sr && light) torches.Add(new Torch { sr = sr, light = light });
        }

        /// <summary>A shot hit the wall: a torch close to it goes out (or lights up again).</summary>
        public void HitTorch(Vector2 p, float radius = 0f)
        {
            foreach (var t in torches)
            {
                if (!t.sr || Vector2.Distance(p, (Vector2)t.sr.transform.position + Vector2.up * 0.6f) > 1.2f + radius) continue;
                t.lit = !t.lit;
                t.light.enabled = t.lit;
                t.sr.color = t.lit ? Color.white : new Color(0.45f, 0.45f, 0.5f);
                var anim = t.sr.GetComponent<SpriteAnimator>();
                if (anim) anim.speed = t.lit ? 1f : 0f;
                if (t.lit) Fx.Anim("fire", (Vector2)t.sr.transform.position + Vector2.up * 0.7f, 0.4f);
                else Fx.Anim("smoke", (Vector2)t.sr.transform.position + Vector2.up * 0.8f, 0.4f);
                Sfx.Play(t.lit ? "dash" : "blink", 0.5f, 1.4f);
                return;
            }
        }

        /// <summary>
        /// A crate, barrel or pot (or, in the caves, a frost crystal / magma rock) that breaks.
        /// env = only the cave crystals. False if this theme has nothing to break here.
        /// </summary>
        public bool MakeBreakable(Vector2Int cell, System.Random rnd, bool env)
        {
            var a = GameAssets.I;
            if (!a || Theme == Theme.Modern) return false;
            Sprite s;
            BreakableKind kind;
            switch (Theme)
            {
                case Theme.Cave:
                    bool fire = style == "fire";
                    kind = fire ? BreakableKind.MagmaRock : BreakableKind.FrostCrystal;
                    s = fire ? a.CaveDecor(GameAssets.CaveRedRocks + 3) : a.CaveDecor(GameAssets.CaveBlue + rnd.Next(4));
                    break;
                case Theme.Retro:
                    if (env) return false;
                    double r = rnd.NextDouble();
                    kind = r < 0.4 ? BreakableKind.Barrel : r < 0.75 ? BreakableKind.Crate : BreakableKind.Pot;
                    s = a.Retro(kind == BreakableKind.Barrel ? "barrel" : kind == BreakableKind.Crate ? "crate" : "cauldron");
                    break;
                case Theme.Classic:
                    if (env) return false;
                    kind = BreakableKind.Crate;
                    s = a.C(rnd.NextDouble() < 0.3 ? "boxes_stacked" : "box") ?? a.D("crate");
                    break;
                default:
                    if (env) return false;
                    kind = BreakableKind.Crate;
                    s = a.D("crate");
                    break;
            }
            return Breakable.Create(this, s, cell, kind) != null;
        }

        /// <summary>A crate broke: the cell is floor again.</summary>
        public void UnblockCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || !blocked[x, y]) return;
            blocked[x, y] = false;
            solidMap.SetTile(new Vector3Int(x, y, 0), null);
            var c = new Vector2Int(x, y);
            if (!FloorCells.Contains(c)) FloorCells.Add(c);
        }

        /// <summary>Furniture placed after the layout: the cell becomes solid.</summary>
        public void BlockCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || blocked[x, y]) return;
            Block(x, y);
            FloorCells.Remove(new Vector2Int(x, y));
        }

        // ---------------- the rift (where an escapee came from) ----------------

        bool riftLeft;
        int riftY = -1;
        SpriteRenderer riftMark;
        public bool HasRift => riftY >= 0;
        public bool RiftOpen { get; private set; }
        public Vector2 RiftPos => new Vector2(riftLeft ? -0.4f : W + 0.4f, riftY + 0.5f);

        /// <summary>A barely visible flicker in a side wall: the tear an escapee came through.</summary>
        public void AddRift(bool left, Color color)
        {
            riftLeft = left;
            riftY = Mathf.Clamp(H / 2 + (left ? -1 : 1), 2, H - 3);
            var go = new GameObject("Rift");
            go.transform.SetParent(transform, false);
            go.transform.position = RiftPos;
            riftMark = go.AddComponent<SpriteRenderer>();
            riftMark.sprite = Gfx.White;
            riftMark.sortingOrder = -38;
            go.transform.localScale = new Vector3(0.18f * Gfx.White.pixelsPerUnit / Gfx.White.rect.width,
                                                  0.8f * Gfx.White.pixelsPerUnit / Gfx.White.rect.height, 1f);
            go.AddComponent<RiftFlicker>().Init(riftMark, color);
        }

        /// <summary>Three hits: the tear opens into a swirling way to the other world.</summary>
        public void OpenRift(Color color)
        {
            if (!HasRift || RiftOpen) return;
            RiftOpen = true;
            if (riftMark) riftMark.transform.localScale *= 3f;
            var fx = Fx.Loop("warp", transform, RiftPos, 0.8f, -37) ?? Fx.Loop("sparkle", transform, RiftPos, 0.6f, -37);
            if (fx)
            {
                var sr = fx.GetComponent<SpriteRenderer>();
                if (sr) sr.color = new Color(color.r, color.g, color.b, 0.9f);
            }
            LightUtil.Point(transform, (Vector3)RiftPos, color, 3f, 1.3f);
        }

        /// <summary>The feet are in front of the open rift.</summary>
        public bool RiftAt(Vector2 feet) =>
            RiftOpen && Mathf.Abs(feet.y - (riftY + 0.3f)) < 0.9f && (riftLeft ? feet.x < 0.75f : feet.x > W - 0.75f);

        // ---------------- the secret passage ----------------

        int secretX = -1;
        readonly List<SpriteRenderer> crack = new List<SpriteRenderer>();
        public bool HasSecret => secretX >= 0;
        public bool SecretOpen { get; private set; }
        /// <summary>Middle of the cracked wall (two cells wide).</summary>
        public Vector2 SecretPos => new Vector2(secretX + 1f, H + 0.5f);

        /// <summary>A cracked piece of the top wall near a corner. False if both corners are blocked.</summary>
        public bool TryAddSecret(bool preferRight)
        {
            if (HasSecret) return true;
            int[] options = preferRight ? new[] { W - 3, 1 } : new[] { 1, W - 3 };
            foreach (int x in options)
            {
                if (!IsFree(x, H - 1) || !IsFree(x + 1, H - 1) || NearDoor(x + 1f)) continue;
                secretX = x;
                crack.Add(Prop("wall_hole_1", new Vector2(x + 0.5f, H), -38));
                crack.Add(Prop("wall_hole_2", new Vector2(x + 1.5f, H), -38));
                return true;
            }
            return false;
        }

        /// <summary>The wall gives way: a dark passage two cells wide.</summary>
        public void OpenSecret()
        {
            if (!HasSecret || SecretOpen) return;
            SecretOpen = true;
            foreach (var sr in crack) if (sr) sr.gameObject.SetActive(false);
            solidMap.SetTile(new Vector3Int(secretX, H, 0), null);
            solidMap.SetTile(new Vector3Int(secretX + 1, H, 0), null);
            var hole = new GameObject("SecretPassage").AddComponent<SpriteRenderer>();
            hole.transform.SetParent(transform, false);
            hole.transform.position = new Vector3(secretX + 1f, H + 0.5f, 0f);
            hole.transform.localScale = new Vector3(2f * Gfx.White.pixelsPerUnit / Gfx.White.rect.width,
                                                    1f * Gfx.White.pixelsPerUnit / Gfx.White.rect.height, 1f);
            hole.sprite = Gfx.White;
            hole.color = new Color(0.05f, 0.04f, 0.07f);
            hole.sortingOrder = -37;
            LightUtil.Point(transform, new Vector3(secretX + 1f, H - 0.2f, 0f), new Color(0.6f, 0.9f, 0.7f), 2.5f, 0.9f);
        }

        /// <summary>The feet walked into the open passage.</summary>
        public bool SecretAt(Vector2 feet) => SecretOpen && feet.y > H - 0.35f && Mathf.Abs(feet.x - (secretX + 1f)) < 0.9f;

        void BuildDecor(Law law, System.Random rnd)
        {
            if (Theme == Theme.Modern) return;   // the secret room is furnished by RoomManager
            if (Theme == Theme.Retro) { RetroDecor(rnd); return; }
            if (Theme == Theme.Cave) { CaveDecor(rnd); return; }
            int[] spots = { 3, W - 4, 7, W - 8 };
            bool c = Theme == Theme.Classic;
            for (int i = 0; i < spots.Length; i++)
            {
                int x = spots[i];
                if (x < 1 || x >= W - 1 || NearDoor(x + 0.5f)) continue;
                if (i >= 2 && W < 20) continue;
                var at = new Vector2(x + 0.5f, H);
                if (c)
                {
                    if (i % 2 == 0)
                    {
                        var t = Prop("torch_1", at + new Vector2(0f, 0.1f), -39);
                        var frames = ClassicAnim("torch_", 8);
                        if (frames.Length > 0) Animate(t, frames, 10f);
                        if (t) AddTorch(t, LightUtil.Point(t.transform, new Vector3(0f, 1f, 0f), new Color(1f, 0.7f, 0.35f), 3.5f, 0.9f));
                    }
                    else Prop("wall_flag_red", at, -39);
                    continue;
                }
                switch (law)
                {
                    case Law.Ice: Fountain(x, "blue"); break;
                    case Law.Lava: Fountain(x, "red"); break;
                    case Law.Pacifist:
                        Prop(i % 2 == 0 ? "wall_goo" : "wall_banner_green", at, -39);
                        if (i % 2 == 0) Prop("wall_goo_base", new Vector2(x + 0.5f, H - 1), -59);
                        break;
                    case Law.Crypt:
                        Prop(i % 2 == 0 ? "wall_hole_2" : "wall_hole_1", at, -39);
                        Prop("skull", new Vector2(x + 0.5f, H - 2), -60);
                        break;
                    default:
                        string[] banners = { "wall_banner_red", "wall_banner_blue", "wall_banner_green", "wall_banner_yellow" };
                        Prop(banners[(i + (int)law) % banners.Length], at, -39);
                        break;
                }
            }
        }

        bool NearDoor(float x)
        {
            foreach (var d in doors) if (Mathf.Abs(x - d.cx) < 3f) return true;
            return false;
        }

        public void Fountain(int x, string color)
        {
            Prop("wall_fountain_top_1", new Vector2(x + 0.5f, H + 1), -39);
            var mid = Prop($"wall_fountain_mid_{color}_anim_f0", new Vector2(x + 0.5f, H), -39);
            Animate(mid, GameAssets.I.Anim($"wall_fountain_mid_{color}_anim"), 6f);
            var basin = Prop($"wall_fountain_basin_{color}_anim_f0", new Vector2(x + 0.5f, H - 1), -59);
            Animate(basin, GameAssets.I.Anim($"wall_fountain_basin_{color}_anim"), 6f);
        }

        Sprite[] ClassicAnim(string prefix, int count)
        {
            var list = new List<Sprite>();
            for (int i = 1; i <= count; i++)
            {
                var s = GameAssets.I ? GameAssets.I.C(prefix + i) : null;
                if (s) list.Add(s);
            }
            return list.ToArray();
        }

        static void Animate(SpriteRenderer sr, Sprite[] frames, float fps)
        {
            if (!sr || frames == null || frames.Length == 0) return;
            var a = sr.gameObject.AddComponent<SpriteAnimator>();
            a.target = sr;
            a.Play(frames, fps);
        }

        /// <summary>A sprite standing on the floor (bottom-center pivot). order 0 = sorted with characters by Y.</summary>
        public SpriteRenderer Prop(string sprite, Vector2 pos, int order) => PropSprite(S(sprite), pos, order, sprite);

        /// <summary>Same as Prop, for a sprite that has no name in the theme (cave decor, animation frames).</summary>
        public SpriteRenderer PropSprite(Sprite s, Vector2 pos, int order, string name = null)
        {
            if (!s) return null;
            var go = new GameObject(name ?? s.name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;   // sort tall props by their base
            Gfx.Lit(sr);
            return sr;
        }

        // ---------------- queries ----------------

        public bool IsFree(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && !blocked[x, y];

        public Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

        /// <summary>Closest unblocked floor cell centre to a point.</summary>
        public Vector2 NearestFree(Vector2 p)
        {
            var best = PlayerSpawn;
            float bestD = float.MaxValue;
            foreach (var c in FloorCells)
            {
                var v = new Vector2(c.x + 0.5f, c.y + 0.3f);
                float d = (v - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        public bool Contains(Vector2 p, float margin = 0f) =>
            p.x > -1f - margin && p.y > -1f - margin && p.x < W + 1f + margin && p.y < H + 2f + margin;

        // ---------------- lava ----------------

        public bool IsLava(Vector2 feet)
        {
            var c = Cell(feet);
            return c.x >= 0 && c.y >= 0 && c.x < W && c.y < H && lava[c.x, c.y];
        }

        public bool IsLavaCell(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < W && c.y < H && lava[c.x, c.y];

        public void Warn(Vector2Int c)
        {
            if (!IsFree(c.x, c.y) || lava[c.x, c.y]) return;
            if (!warnTile) { warnTile = ScriptableObject.CreateInstance<Tile>(); warnTile.sprite = Gfx.Warning; }
            hazardMap.SetTile(new Vector3Int(c.x, c.y, 0), warnTile);
        }

        /// <summary>Removes a warning mark (a boss's strike landed) unless the cell is lava.</summary>
        public void Unwarn(Vector2Int c)
        {
            if (c.x < 0 || c.y < 0 || c.x >= W || c.y >= H || lava[c.x, c.y]) return;
            hazardMap.SetTile(new Vector3Int(c.x, c.y, 0), null);
        }

        public void MakeLava(Vector2Int c)
        {
            if (!IsFree(c.x, c.y) || lava[c.x, c.y]) return;
            if (!lavaTileA)
            {
                lavaTileA = ScriptableObject.CreateInstance<Tile>(); lavaTileA.sprite = Gfx.LavaA;
                lavaTileB = ScriptableObject.CreateInstance<Tile>(); lavaTileB.sprite = Gfx.LavaB;
            }
            lava[c.x, c.y] = true;
            hazardMap.SetTile(new Vector3Int(c.x, c.y, 0), lavaFrame ? lavaTileB : lavaTileA);
        }

        void Update()
        {
            if (!lavaTileA) return;
            lavaAnim += Time.deltaTime;
            if (lavaAnim < 0.35f) return;
            lavaAnim = 0f;
            lavaFrame = !lavaFrame;
            if (lavaFrame) hazardMap.SwapTile(lavaTileA, lavaTileB);
            else hazardMap.SwapTile(lavaTileB, lavaTileA);
        }

        void OnDestroy()
        {
            foreach (var t in tiles.Values) if (t) Destroy(t);
            if (solidTile) Destroy(solidTile);
            if (lavaTileA) Destroy(lavaTileA);
            if (lavaTileB) Destroy(lavaTileB);
            if (warnTile) Destroy(warnTile);
        }
    }
}
