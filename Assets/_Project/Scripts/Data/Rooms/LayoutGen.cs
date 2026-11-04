using System.Collections.Generic;

namespace RulesShift
{
    /// <summary>
    /// Procedural room layouts (same ASCII legend as Layouts). Every combat room of a run is new:
    /// size, pillars, crate piles and pits change, always symmetric and always passable.
    /// Seeded by the run, so the Daily run is the same for everyone.
    /// </summary>
    public static class LayoutGen
    {
        static readonly int[] Widths = { 26, 28, 30, 30, 32, 34 };

        public static string[] Generate(System.Random rnd)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var g = TryGenerate(rnd);
                if (g != null) return g;
            }
            return Layouts.Get(0);
        }

        static string[] TryGenerate(System.Random rnd)
        {
            int W = Widths[rnd.Next(Widths.Length)];
            int H = 15 + rnd.Next(6);
            int k = System.Math.Max(1, W * H / 280);   // bigger rooms get more cover
            var g = new char[W, H];   // g[x, row], row 0 = top (next to the doors)
            for (int x = 0; x < W; x++)
            for (int r = 0; r < H; r++) g[x, r] = '.';

            int style = rnd.Next(5);
            switch (style)
            {
                case 0: Pillars(g, W, H, rnd); break;
                case 1: Clusters(g, W, H, rnd, 'X', k * (3 + rnd.Next(3))); break;
                case 2: Clusters(g, W, H, rnd, 'h', k * (2 + rnd.Next(3))); Clusters(g, W, H, rnd, 'C', k * (1 + rnd.Next(2))); break;
                case 3: CenterBlock(g, W, H, rnd); Clusters(g, W, H, rnd, 'X', k - 1 + rnd.Next(2)); break;
                default:
                    Clusters(g, W, H, rnd, 'C', k * (1 + rnd.Next(2)));
                    Clusters(g, W, H, rnd, 'X', k * (1 + rnd.Next(3)));
                    Clusters(g, W, H, rnd, 'h', k * rnd.Next(2));
                    break;
            }

            int px = W / 2, pr = H - 2;
            var reach = Flood(g, W, H, px, pr);
            // every door must be reachable
            int off = W >= 24 ? 6 : 4;
            foreach (int dx in new[] { W / 2, W / 2 - off, W / 2 + off })
                if (!reach[dx, 0]) return null;
            // closed pockets become crates
            int free = 0;
            for (int x = 0; x < W; x++)
            for (int r = 0; r < H; r++)
            {
                if (!Solid(g[x, r]) && !reach[x, r]) g[x, r] = 'X';
                if (!Solid(g[x, r])) free++;
            }
            if (free < W * H * 0.6f) return null;

            // enemy spawns: far from the hero, spread out, not hugging obstacles
            var cells = new List<(int x, int r)>();
            for (int r = 1; r < H - 5; r++)
            for (int x = 1; x < W - 1; x++)
                if (g[x, r] == '.' && Open(g, W, H, x, r)) cells.Add((x, r));
            Shuffle(cells, rnd);
            var spawns = new List<(int x, int r)>();
            foreach (var c in cells)
            {
                bool near = false;
                foreach (var s in spawns)
                    if (System.Math.Abs(s.x - c.x) + System.Math.Abs(s.r - c.r) < 4) { near = true; break; }
                if (near) continue;
                spawns.Add(c);
                if (spawns.Count >= 14) break;
            }
            if (spawns.Count < 8) return null;
            foreach (var s in spawns) g[s.x, s.r] = 'E';

            // a bit of decor
            int skulls = rnd.Next(2 + k);
            for (int i = 0; i < cells.Count && skulls > 0; i++)
            {
                var c = cells[cells.Count - 1 - i];
                if (g[c.x, c.r] != '.') continue;
                g[c.x, c.r] = 's';
                skulls--;
            }

            g[px, pr] = 'P';
            var rows = new string[H];
            for (int r = 0; r < H; r++)
            {
                var chars = new char[W];
                for (int x = 0; x < W; x++) chars[x] = g[x, r];
                rows[r] = new string(chars);
            }
            return rows;
        }

        static bool Solid(char c) => c == 'C' || c == 'X' || c == 'h';

        /// <summary>Rows next to the doors and the hero's start stay empty.</summary>
        static bool Reserved(int x, int r, int W, int H) =>
            r < 2 || r > H - 4 || x < 1 || x > W - 2;

        static bool Open(char[,] g, int W, int H, int x, int r)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dr = -1; dr <= 1; dr++)
            {
                int nx = x + dx, nr = r + dr;
                if (nx < 0 || nr < 0 || nx >= W || nr >= H) continue;
                if (Solid(g[nx, nr])) return false;
            }
            return true;
        }

        /// <summary>Places a block and its mirror if the area plus a 1-cell margin is free.</summary>
        static bool Place(char[,] g, int W, int H, int x, int r, int w, int h, char ch)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                int bx = pass == 0 ? x : W - x - w;
                for (int dx = -1; dx <= w; dx++)
                for (int dr = -1; dr <= h; dr++)
                {
                    int nx = bx + dx, nr = r + dr;
                    if (nx < 0 || nr < 0 || nx >= W || nr >= H) continue;
                    bool inside = dx >= 0 && dr >= 0 && dx < w && dr < h;
                    if (inside && Reserved(nx, nr, W, H)) return false;
                    if (Solid(g[nx, nr])) return false;
                }
            }
            for (int pass = 0; pass < 2; pass++)
            {
                int bx = pass == 0 ? x : W - x - w;
                for (int dx = 0; dx < w; dx++)
                for (int dr = 0; dr < h; dr++) g[bx + dx, r + dr] = ch;
            }
            return true;
        }

        static void Pillars(char[,] g, int W, int H, System.Random rnd)
        {
            int stepX = 4 + rnd.Next(2), stepR = 3 + rnd.Next(2);
            int startX = 2 + rnd.Next(2);
            for (int r = 3; r < H - 4; r += stepR)
            for (int x = startX; x < W / 2 - 1; x += stepX)
                Place(g, W, H, x, r, 1, 1, 'C');
        }

        static void Clusters(char[,] g, int W, int H, System.Random rnd, char ch, int count)
        {
            int placed = 0;
            for (int t = 0; t < 60 + 20 * count && placed < count; t++)
            {
                int w = 1 + rnd.Next(ch == 'C' ? 1 : 3), h = 1 + rnd.Next(2);
                int x = 1 + rnd.Next(System.Math.Max(1, W / 2 - w - 1));
                int r = 2 + rnd.Next(System.Math.Max(1, H - 6 - h));
                if (Place(g, W, H, x, r, w, h, ch)) placed++;
            }
        }

        static void CenterBlock(char[,] g, int W, int H, System.Random rnd)
        {
            // a broken ring in the middle: cover to dance around
            char ch = rnd.Next(2) == 0 ? 'X' : 'h';
            int cx = W / 2, cr = H / 2 - 1;
            int half = 2 + rnd.Next(2);
            for (int dx = -half; dx < half; dx++)
            {
                if (dx == -1 || dx == 0) continue;   // gap in the middle
                int x = cx + dx;
                if (!Reserved(x, cr - 1, W, H)) g[x, cr - 1] = ch;
                if (!Reserved(x, cr + 2, W, H)) g[x, cr + 2] = ch;
            }
            Place(g, W, H, 3, 3, 1, 1, 'C');
            Place(g, W, H, 3, H - 6, 1, 1, 'C');
        }

        static bool[,] Flood(char[,] g, int W, int H, int sx, int sr)
        {
            var seen = new bool[W, H];
            var q = new Queue<(int, int)>();
            q.Enqueue((sx, sr));
            seen[sx, sr] = true;
            while (q.Count > 0)
            {
                var (x, r) = q.Dequeue();
                foreach (var (dx, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, nr = r + dr;
                    if (nx < 0 || nr < 0 || nx >= W || nr >= H || seen[nx, nr] || Solid(g[nx, nr])) continue;
                    seen[nx, nr] = true;
                    q.Enqueue((nx, nr));
                }
            }
            return seen;
        }

        static void Shuffle<T>(List<T> list, System.Random rnd)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = rnd.Next(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
