using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    /// <summary>
    /// The floor plan: every door of the floor is decided when the floor begins, so the map (Tab) can show the
    /// whole way to the boss and the route can be planned — through the shop, around the ambush, into the portal.
    /// </summary>
    public partial class RoomManager
    {
        List<RoomDef[]> floorPlan = new List<RoomDef[]>();
        int planFloor = -1;
        readonly Dictionary<int, RoomDef> path = new Dictionary<int, RoomDef>();
        int pathFloor = -1;

        public struct MapRow
        {
            public int layer;
            public RoomDef[] rooms;
            public RoomDef taken;     // the room the hero went into on this layer (null: not yet)
            public bool here;         // the hero is on this layer now
        }

        /// <summary>The doors of room `next` on this floor (the boss after the last room).</summary>
        RoomDef[] MainNext(int next, RoomDef current)
        {
            EnsurePlan();
            int i = Mathf.Clamp(next, 1, roomsPerFloor);
            return floorPlan[i];
        }

        void EnsurePlan()
        {
            if (planFloor == floor && floorPlan.Count == roomsPerFloor + 1) return;
            planFloor = floor;
            floorPlan.Clear();
            floorPlan.Add(new RoomDef[0]);   // layer 0: the room the floor starts in
            for (int n = 1; n < roomsPerFloor; n++) floorPlan.Add(PlanLayer(n));
            floorPlan.Add(new[] { BossFor(floor) });
        }

        /// <summary>Remembers the way through the floor (main path only).</summary>
        void NotePath(RoomDef d)
        {
            if (d.world != World.None || d.objective == Objective.Secret) return;
            if (pathFloor != floor) { path.Clear(); pathFloor = floor; }
            path[inFloor] = d;
            if (inFloor == 0 && step > 0) Hud.Hint("map");
        }

        /// <summary>Rows of the map, from the start of the floor to the boss.</summary>
        public List<MapRow> MapRows()
        {
            var rows = new List<MapRow>();
            if (State == GameState.Menu) return rows;
            EnsurePlan();
            bool inWorld = def != null && def.world != World.None;
            for (int i = 0; i < floorPlan.Count; i++)
            {
                path.TryGetValue(i, out var taken);
                var rooms = i == 0 ? (taken != null ? new[] { taken } : new RoomDef[0]) : floorPlan[i];
                rows.Add(new MapRow { layer = i, rooms = rooms, taken = pathFloor == floor ? taken : null, here = !inWorld && i == inFloor });
            }
            return rows;
        }

        static bool MapKey()
        {
            return Binds.MapDown();
        }

        void ToggleMap()
        {
            if (busy || !Hud.I) return;
            Hud.I.ToggleMap();
        }

        /// <summary>The Gravity law: a dark well in the middle of the room that everything falls toward.</summary>
        void GravityWell()
        {
            var sr = room.PropSprite(Gfx.Orb, room.Center - new Vector2(0f, 0.6f), -50, "GravityWell");
            if (!sr) return;
            sr.color = new Color(0.22f, 0.05f, 0.35f, 0.9f);
            sr.transform.localScale = Vector3.one * 3f;
            Gfx.Unlit(sr);
            LightUtil.Point(sr.transform, new Vector3(0f, 0.3f, 0f), new Color(0.65f, 0.35f, 1f), 4.5f, 1.1f);
            Fx.Loop("sparkle", sr.transform, new Vector2(0f, 0.3f), 0.5f, -49);
        }
    }
}
