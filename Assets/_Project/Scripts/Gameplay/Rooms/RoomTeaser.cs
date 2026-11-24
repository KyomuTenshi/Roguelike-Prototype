using System.Collections;
using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// The teaser of the next game, A Child Beneath the Crown: the scene after the true ending
    /// (a shard of the crown of rules falls into another world, the Queen's son finds the girl)
    /// and the white feathers from that world, found now and then in cleared rooms.
    /// </summary>
    public partial class RoomManager
    {
        static readonly Color SonColor = new Color(0.82f, 0.78f, 0.7f);
        static readonly Color GirlColor = new Color(1f, 0.93f, 0.78f);
        static readonly Color GirlMindColor = new Color(0.6f, 0.95f, 1f);
        static readonly Color ShardColor = new Color(1f, 0.85f, 0.45f);
        static string SonName => Loc.T("teaser.name.son");
        static string GirlName => Loc.T("teaser.name.girl");

        // ---------------- the scene after the true ending ----------------

        /// <summary>
        /// Starts and ends on black. A scene of its own, so skipping the ending doesn't skip it.
        /// backHome: put the bright room of the true ending back behind the black (the results open over it).
        /// </summary>
        IEnumerator CrownTeaser(bool backHome)
        {
            Cutscene.Begin();
            if (MusicPlayer.I) MusicPlayer.I.Stop();
            bool playerOn = player && player.gameObject.activeSelf;
            bool pixieOn = Pixie.I && Pixie.I.gameObject.activeSelf;
            yield return Cutscene.Wait(1f);
            Sfx.Play("blink", 0.5f, 0.6f);
            yield return Cutscene.Narrate(Loc.T("teaser.1"));

            // another world behind the black: a field at night, lit by the moon
            ClearRoom();
            room = Room.Build(Layouts.Get(6), Theme.Classic, Law.None, transform);
            Fx.Root = room.Content;
            Rules.Apply(Law.None);
            if (StyleController.I) StyleController.I.Apply(Law.Crypt, true);
            if (player) player.gameObject.SetActive(false);
            if (Pixie.I) Pixie.I.gameObject.SetActive(false);
            if (Hud.I) Hud.I.ShowBossBar(false);
            int W = room.W, H = room.H;
            Vector2 crater = room.BossSpawn ?? new Vector2(W / 2f, H / 2f + 1.5f);
            LightUtil.Point(room.Content, new Vector2(W / 2f, H + 2f), new Color(0.45f, 0.55f, 1f), 16f, 0.4f);
            if (CameraFollow.I) CameraFollow.I.bounds = room.Bounds;
            Vector2 sky = new Vector2(4f, H + 6f);
            Cutscene.Look(Vector2.Lerp(sky, crater, 0.3f));
            if (CameraFollow.I) CameraFollow.I.Snap();
            yield return Cutscene.FadeBlack(0f, 1.6f);
            yield return Cutscene.Wait(0.5f);

            // the shard falls
            yield return FallingShard(sky, crater);
            Sfx.Play("boom", 0.9f, 0.5f);
            Fx.Anim("epicboom", crater, 0.9f);
            if (CameraFollow.I) CameraFollow.I.Shake(0.7f);
            yield return Cutscene.Flash(1f, 0.12f);
            Battlefield(crater);   // the dead are there when the light fades
            Cutscene.Look(crater);
            yield return Cutscene.Flash(0f, 1.4f);
            if (MusicPlayer.I) MusicPlayer.I.Play("teaser");
            yield return Cutscene.Narrate(Loc.T("teaser.2"));

            // the Queen's son walks in among them
            var a = GameAssets.I;
            var son = CutActor.Create(room.Content, "SonActor", new Vector2(crater.x - 0.6f, 0.8f),
                a ? a.Anim("knight_m_idle_anim") : new Sprite[0], a ? a.Anim("knight_m_run_anim") : new Sprite[0]);
            son.Body.color = new Color(0.85f, 0.85f, 0.9f);
            LightUtil.Point(son.transform, new Vector3(0f, 0.8f, 0f), new Color(1f, 0.8f, 0.55f), 2.6f, 0.8f);
            Cutscene.Look(son.Feet + Vector2.up * 1.5f);
            var walk = StartCoroutine(son.WalkTo(crater + new Vector2(-0.6f, -2.8f), 1.5f));
            StartCoroutine(Cutscene.LookFollow(son.transform, 4f));
            yield return Cutscene.Narrate(Loc.T("teaser.3"));
            yield return walk;

            // the only one alive rises from the crater
            Cutscene.Look(crater + new Vector2(0f, -1f));
            yield return Cutscene.Wait(0.8f);
            var girl = CutActor.Create(room.Content, "GirlActor", crater + Vector2.down * 0.2f,
                a ? a.Anim("angel_idle_anim") : new Sprite[0], a ? a.Anim("angel_run_anim") : new Sprite[0]);
            girl.Body.color = new Color(1f, 1f, 1f, 0f);
            var halo = LightUtil.Point(girl.transform, new Vector3(0f, 0.7f, 0f), new Color(0.85f, 0.95f, 1f), 3f, 0f);
            Fx.Loop("sparkle", girl.transform, new Vector2(0f, 0.8f), 0.3f, 39);
            Sfx.Play("heal", 0.6f, 0.7f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.5f)
            {
                if (Cutscene.Skipped) break;
                girl.Body.color = new Color(1f, 1f, 1f, t);
                if (halo) halo.intensity = 1.2f * t;
                yield return null;
            }
            girl.Body.color = Color.white;
            if (halo) halo.intensity = 1.2f;
            girl.Face(true);
            son.Face(false);
            yield return Cutscene.Narrate(Loc.T("teaser.4"));
            yield return Cutscene.Say(GirlName, Loc.T("teaser.girl.1"), GirlColor);
            yield return Cutscene.Say(SonName, Loc.T("teaser.son.1"), SonColor);
            yield return Cutscene.Say(GirlName, Loc.T("teaser.girl.2"), GirlColor);
            yield return Cutscene.Say(SonName, Loc.T("teaser.son.2"), SonColor);

            // her sealed magic weighs the odds for her
            Fx.Burst(girl.Feet + Vector2.up * 0.8f, GirlMindColor, 18, 2.5f, 0.9f, 1.4f);
            Sfx.Play("wake", 0.6f, 0.8f);
            if (halo) halo.color = GirlMindColor;
            yield return Cutscene.Say("???", Loc.T("teaser.ai.1"), GirlMindColor);
            if (halo) halo.color = new Color(0.85f, 0.95f, 1f);
            yield return Cutscene.Say(GirlName, Loc.T("teaser.girl.3"), GirlColor);
            yield return girl.WalkTo(son.Feet + new Vector2(0.9f, 0.2f), 1.2f);
            girl.Face(true);
            yield return Cutscene.Say(SonName, Loc.T("teaser.son.3"), SonColor);
            yield return Cutscene.Wait(0.6f);

            yield return Cutscene.FadeBlack(1f, 1.4f);
            Cutscene.UI.HideLine();
            yield return Cutscene.Narrate(Loc.T("teaser.q"));
            Sfx.Play("boom", 0.7f, 0.6f);
            yield return Cutscene.UI.Card(Loc.T("teaser.label"), "A CHILD BENEATH THE CROWN", 4f);
            SaveSystem.Data.teaserSeen = true;
            SaveSystem.Save();

            // behind the black: back to where it was
            ClearRoom();
            if (StyleController.I) StyleController.I.Apply(Law.None, true);
            if (backHome) ShowEndingRoom(false);
            if (player) player.gameObject.SetActive(playerOn);
            if (Pixie.I) Pixie.I.gameObject.SetActive(pixieOn);
            Cutscene.End(false);
        }

        /// <summary>A golden light with the crown on it falls from the sky, faster and faster; the camera follows.</summary>
        IEnumerator FallingShard(Vector2 from, Vector2 to)
        {
            var go = new GameObject("Shard");
            go.transform.SetParent(room.Content, false);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.7f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Gfx.Orb;
            sr.color = ShardColor;
            sr.sortingOrder = 40;
            if (Fx.Unlit) sr.sharedMaterial = Fx.Unlit;
            Fx.Loop("crown", go.transform, new Vector2(0f, 0.5f), 0.4f, 41);
            LightUtil.Point(go.transform, Vector3.zero, ShardColor, 4f, 1.6f);
            Sfx.Play("law", 0.6f, 0.6f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.6f)
            {
                if (Cutscene.Skipped) break;
                Vector2 p = Vector2.Lerp(from, to, t * t);
                go.transform.position = p;
                Cutscene.Look(p);
                if (Random.value < 0.7f) Fx.Burst(p, ShardColor, 2, 0.8f, 0.5f, 1f);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>The crater, fire, and the Queen's soldiers lying around it with their swords in the ground.</summary>
        void Battlefield(Vector2 crater)
        {
            var a = GameAssets.I;
            room.Prop("hole", crater + new Vector2(-0.5f, -0.5f), 0);
            Fx.Loop("fire", room.Content, crater + new Vector2(-1.1f, 0.2f), 0.35f, 5);
            Fx.Loop("fire", room.Content, crater + new Vector2(1.2f, -0.3f), 0.3f, 5);
            LightUtil.Point(room.Content, crater, new Color(1f, 0.55f, 0.3f), 5.5f, 1.1f);

            var body = a ? a.Anim("knight_m_idle_anim") : new Sprite[0];
            Vector2[] dead =
            {
                new Vector2(-4.2f, -1.5f), new Vector2(3.8f, -2.2f), new Vector2(-2.6f, -4.6f),
                new Vector2(5.6f, 0.8f), new Vector2(-6.2f, 0.4f), new Vector2(2.4f, -5.2f)
            };
            for (int i = 0; i < dead.Length && body.Length > 0; i++)
            {
                var sr = room.PropSprite(body[0], crater + dead[i], 0, "Fallen");
                if (!sr) continue;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 90f : -90f);
                sr.color = new Color(0.5f, 0.47f, 0.5f);
            }

            string[] swords = { "weapon_rusty_sword", "weapon_regular_sword", "weapon_knight_sword", "weapon_rusty_sword" };
            Vector2[] graves = { new Vector2(-3f, -0.6f), new Vector2(4.6f, -1.1f), new Vector2(-5.4f, -2.6f), new Vector2(1.4f, -4.2f) };
            for (int i = 0; i < swords.Length; i++)
            {
                var sr = room.PropSprite(a ? a.D(swords[i]) : null, crater + graves[i], 0, "Sword");
                if (!sr) continue;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, 180f + (i % 2 == 0 ? 12f : -10f));   // blade down, in the ground
                sr.color = new Color(0.75f, 0.75f, 0.8f);
            }
            room.Prop("skull", crater + new Vector2(-1.8f, -3.2f), 0);
            room.Prop("skull", crater + new Vector2(6.4f, -3.4f), 0);
        }

        /// <summary>The codex: watch the teaser again.</summary>
        public void PreviewTeaser()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            if (MenuUI.I) MenuUI.I.Hide();
            StartCoroutine(TeaserRoutine());
        }

        IEnumerator TeaserRoutine()
        {
            bool inRun = player != null && State != GameState.Menu;
            State = GameState.Won;
            busy = true;
            yield return Cutscene.FadeBlack(1f, 0.6f);
            yield return CrownTeaser(false);
            if (inRun) finished = true;
            busy = false;
            ShowBackdrop();
            if (MenuUI.I) MenuUI.I.ShowStory();
            yield return LiftEndingBlack();
        }

        // ---------------- white feathers: the other world leaks into this one ----------------

        public const int FeatherCount = 4;
        static readonly Color FeatherColor = new Color(0.92f, 0.97f, 1f);
        bool featherThisRun;

        public static int FeathersFound
        {
            get
            {
                int n = 0;
                for (int i = 0; i < FeatherCount; i++) if ((SaveSystem.Data.feathers & (1 << i)) != 0) n++;
                return n;
            }
        }

        /// <summary>
        /// Now and then a cleared combat room keeps a white feather (from the second floor, one a run, in story order).
        /// Uses its own randomness, so a Daily run goes the same for everyone.
        /// </summary>
        void TrySpawnFeather()
        {
            if (featherThisRun || floor < 1 || !def.Combat || def.world != World.None) return;
            if (FeathersFound >= FeatherCount || Random.value > 0.14f) return;
            SpawnFeather();
        }

        void SpawnFeather()
        {
            int index = 0;
            while (index < FeatherCount && (SaveSystem.Data.feathers & (1 << index)) != 0) index++;
            if (index >= FeatherCount || !room) return;
            featherThisRun = true;

            Vector2 pos = room.NearestFree(new Vector2(Random.Range(3f, room.W - 3f), Random.Range(3f, room.H - 4f)));
            var a = GameAssets.I;
            Sprite icon = a ? a.Icon(7) : null;   // Raven icon 7: a white quill
            var sr = room.PropSprite(icon ? icon : Gfx.Orb, pos, 0, "Feather");
            if (sr)
            {
                sr.color = FeatherColor;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, -25f);
                sr.transform.localScale = Vector3.one * 1.2f;
            }
            Fx.Loop("sparkle", room.Content, pos + Vector2.up * 0.4f, 0.25f, 39);
            LightUtil.Point(room.Content, pos + Vector2.up * 0.3f, FeatherColor, 2.2f, 0.9f);
            Sfx.Play("blink", 0.4f, 1.4f);

            int bit = index;
            LoreNote.Create(room.Content, pos, 1.3f, "feather.title", "feather." + bit, () =>
            {
                bool first = SaveSystem.Data.feathers == 0;
                SaveSystem.Data.feathers |= 1 << bit;
                SaveSystem.Save();
                Hud.Toast(Loc.F("toast.feather", FeathersFound, FeatherCount), FeatherColor);
                Sfx.Play("heal", 0.5f, 1.3f);
                if (first) StartCoroutine(FeatherPixie());
            });
        }

        /// <summary>The first feather ever: Pixie doesn't know this texture.</summary>
        IEnumerator FeatherPixie()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            if (State != GameState.Playing || busy || Cutscene.Playing || !Pixie.I) yield break;
            Cutscene.Begin();
            Pixie.I.Ping();
            yield return Cutscene.Say(Pixie.Name, Loc.T("pixie.feather"), Pixie.Color);
            Cutscene.End();
        }
    }
}
