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
        public Rect Bounds => new Rect(-1.5f, -1.5f, W + 3f, H + 6f);
        public int DoorCount => doors.Count;
        public Vector2 DoorPos(int i) => new Vector2(doors[i].cx, H);

        class DoorSlot
        {
            public float cx;
            public SpriteRenderer sr;
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
        /// <param name="modernStyle">Modern theme only: wall and floor set suffix ("", "_mint", "_gray").</param>
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
            return Theme == Theme.Classic ? (a.C(name) ?? a.D(name)) : a.D(name);
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

        void Solid(int x, int y) => solidMap.SetTile(new Vector3Int(x, y, 0), solidTile);

        void BuildDoors()
        {
            int half = W / 2;
            int offset = W >= 24 ? 6 : 4;
            var centers = doorCount == 2 ? new[] { half - offset, half + offset } : new[] { half };
            bool c = Theme == Theme.Classic || Theme == Theme.Modern;   // both name their door sprites door_closed / door_open
            foreach (int cx in centers)
            {
                var slot = new DoorSlot { cx = cx };
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
            var pos = new Vector2(doors[i].cx, H + (Theme == Theme.Dungeon ? 3.5f : 2.6f));
            WorldLabel.Create(transform, pos + new Vector2(0.06f, -0.06f), text, new Color(0f, 0f, 0f, 0.85f), 44);
            WorldLabel.Create(transform, pos, text, color, 45);
        }

        public void OpenDoor()
        {
            if (DoorOpen) return;
            DoorOpen = true;
            foreach (var d in doors)
            {
                if (d.sr && d.openSprite) d.sr.sprite = d.openSprite;
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
                        Prop("column", new Vector2(x + 0.5f, y), 0);
                        Block(x, y);
                        break;
                    case 'X':
                        Prop(c ? (rnd.NextDouble() < 0.3 ? "boxes_stacked" : "box") : "crate", new Vector2(x + 0.5f, y), 0);
                        Block(x, y);
                        break;
                    case 'h':
                        if (c) Prop("box", new Vector2(x + 0.5f, y), 0);
                        else floorMap.SetTile(new Vector3Int(x, y, 0), T(S("hole")));
                        Block(x, y);
                        break;
                    case 's':
                        Prop("skull", new Vector2(x + 0.5f, y), -60);
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

        /// <summary>Furniture placed after the layout: the cell becomes solid.</summary>
        public void BlockCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || blocked[x, y]) return;
            Block(x, y);
            FloorCells.Remove(new Vector2Int(x, y));
        }

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
                        LightUtil.Point(t.transform, new Vector3(0f, 1f, 0f), new Color(1f, 0.7f, 0.35f), 3.5f, 0.9f);
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
        public SpriteRenderer Prop(string sprite, Vector2 pos, int order)
        {
            var s = S(sprite);
            if (!s) return null;
            var go = new GameObject(sprite);
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
