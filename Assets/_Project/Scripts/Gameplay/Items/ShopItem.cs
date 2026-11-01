using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Something to take by walking onto it: a shop ware (coins), a shrine relic (one heart)
    /// or the fountain (free heal). A price tag floats under it.
    /// </summary>
    public class ShopItem : MonoBehaviour
    {
        public enum Cost { Coins, Heart, Free }

        Cost cost;
        int price;
        System.Action<PlayerController> onTake;
        SpriteRenderer icon;
        GameObject tag1, tag2;
        float denyTimer, bobPhase;
        bool taken;

        public static ShopItem Create(Room room, Vector2 pos, Sprite sprite, Cost cost, int price, System.Action<PlayerController> onTake, bool floating = true)
        {
            var go = new GameObject("ShopItem");
            go.transform.SetParent(room.Content, false);
            go.transform.position = pos;
            var it = go.AddComponent<ShopItem>();
            it.cost = cost;
            it.price = price;
            it.onTake = onTake;
            it.bobPhase = Random.value * 6f;

            var child = new GameObject("Icon");
            child.transform.SetParent(go.transform, false);
            it.icon = child.AddComponent<SpriteRenderer>();
            it.icon.sprite = sprite ? sprite : Gfx.Orb;
            it.icon.sortingOrder = floating ? 6 : -58;
            if (!floating) it.icon.spriteSortPoint = SpriteSortPoint.Pivot;
            Gfx.Lit(it.icon);
            it.floating = floating;

            if (floating)
            {
                var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
                shadow.transform.SetParent(go.transform, false);
                shadow.sprite = Gfx.Shadow;
                shadow.sortingOrder = -10;
                shadow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                LightUtil.Point(go.transform, new Vector3(0f, 0.6f, 0f), new Color(1f, 0.85f, 0.5f), 1.8f, 0.8f);
            }

            string text = cost == Cost.Coins ? Loc.F("shop.coins", price) : cost == Cost.Heart ? Loc.T("shop.heart") : "";
            if (text.Length > 0)
            {
                Color c = cost == Cost.Coins ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.45f, 0.45f);
                var under = pos + new Vector2(0f, -0.45f);
                it.tag1 = WorldLabel.Create(go.transform, under + new Vector2(0.05f, -0.05f), text, new Color(0f, 0f, 0f, 0.85f), 44, 0.8f).gameObject;
                it.tag2 = WorldLabel.Create(go.transform, under, text, c, 45, 0.8f).gameObject;
            }
            return it;
        }

        bool floating;

        public bool CanAfford(PlayerController p)
        {
            switch (cost)
            {
                case Cost.Coins: return RoomManager.I != null && RoomManager.I.Coins >= price;
                case Cost.Heart: return p.maxHalves > 2;
                default: return true;
            }
        }

        void Update()
        {
            if (taken) return;
            float dt = Time.deltaTime;
            denyTimer -= dt;
            if (floating)
                icon.transform.localPosition = new Vector3(0f, 0.25f + Mathf.Round(Mathf.Sin(Time.time * 3f + bobPhase) * 2f) / 16f, 0f);

            var p = PlayerController.I;
            if (p == null || !p.Alive || p.frozen) return;
            if (Vector2.Distance(p.Feet, transform.position) > 0.75f) return;

            if (!CanAfford(p))
            {
                if (denyTimer <= 0f)
                {
                    denyTimer = 1.5f;
                    Hud.Toast(Loc.T(cost == Cost.Coins ? "shop.noMoney" : "shop.noHearts"), UiKit.Red);
                    Sfx.Play("button", 0.5f, 0.6f);
                }
                return;
            }

            taken = true;
            if (cost == Cost.Coins) RoomManager.I.AddCoins(-price);
            if (cost == Cost.Heart)
            {
                p.maxHalves -= 2;
                p.halves = Mathf.Min(p.halves, p.maxHalves);
                Fx.Burst(p.Position, new Color(1f, 0.25f, 0.3f), 18, 4f, 0.5f, 1.8f);
            }
            Sfx.Play("buy");
            Fx.Burst((Vector2)transform.position + Vector2.up * 0.4f, new Color(1f, 0.9f, 0.5f), 16, 4f, 0.5f, 1.5f);
            Fx.Anim(cost == Cost.Free ? "hearts" : "coins", (Vector2)transform.position + Vector2.up * 0.6f, 0.5f);
            onTake?.Invoke(p);
            if (floating) Destroy(gameObject);
            else
            {
                if (tag1) Destroy(tag1);
                if (tag2) Destroy(tag2);
                icon.color = new Color(0.6f, 0.6f, 0.7f);
            }
        }
    }
}
