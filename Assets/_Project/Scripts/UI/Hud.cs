using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RulesShift
{
    /// <summary>
    /// In-game UI in the dungeon style. Kept clean: hearts, relics and run info stay in the corners;
    /// the room card shows for a couple of seconds and fades away; the objective line fades when
    /// nothing changes; first-time hints appear in a small panel and leave on their own.
    /// Menus live in MenuUI.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class Hud : MonoBehaviour
    {
        public static Hud I { get; private set; }

        const float ObjectiveHold = 4f;
        const float HintHold = 6.5f;

        RectTransform root;
        RectTransform leftPlate, rightPlate, relicRow;
        Image[] hearts;
        Text objective, counter, lawChip, toast, scoreText, shieldText;
        CanvasGroup objectiveGroup;
        RectTransform card;
        CanvasGroup cardGroup;
        Text cardLabel, cardTitle, cardRule, cardExtra;
        Image bannerL, bannerR;
        Coroutine cardRoutine, toastRoutine, hintRoutine;
        GameObject bossBar;
        Image bossFill;
        Text bossLaw, bossName;
        RectTransform beat;
        Image beatDot, beatRing;
        GameObject upgradePanel;
        Action<Upgrade> onPick;
        Upgrade[] options;
        Image fade, hurtFlash;
        RectTransform hintPanel, notePanel;
        Text noteTitle, noteText;
        CanvasGroup noteGroup;
        float noteAlpha;
        bool noteShown;
        CanvasGroup hintGroup;
        Text hintText;
        readonly Queue<string> hints = new Queue<string>();
        int lastHalves = -1, lastMax = -1, targetScore, relicCount = -1;
        float hurtAlpha, objectiveAge, scoreMul = 1f;
        string objectiveRaw = "";
        Texture2D currentCursor;

        void Awake()
        {
            I = this;
            UiKit.Init();
            Build();
            if (!GetComponent<MenuUI>()) gameObject.AddComponent<MenuUI>();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        /// <summary>Builds the HUD again (after a language change). Only used from the menus.</summary>
        public void Rebuild()
        {
            StopAllCoroutines();
            cardRoutine = toastRoutine = hintRoutine = null;
            hints.Clear();
            if (root) Destroy(root.gameObject);
            lastHalves = lastMax = relicCount = -1;
            options = null;
            onPick = null;
            Build();
            SetFade(0f);
            fade.gameObject.SetActive(false);
        }

        // ---------------- public API ----------------

        public static void Toast(string text, Color color)
        {
            if (I == null) return;
            if (I.toastRoutine != null) I.StopCoroutine(I.toastRoutine);
            I.toastRoutine = I.StartCoroutine(I.ToastRoutine(text, color));
        }

        /// <summary>Shows hint.&lt;id&gt; once ever (if hints are on). Queued, a few seconds each.</summary>
        public static void Hint(string id)
        {
            if (I == null || !SaveSystem.Data.hints || !Loc.Has("hint." + id)) return;
            if (!SaveSystem.FirstTime(id)) return;
            SaveSystem.Save();
            I.hints.Enqueue(Loc.T("hint." + id));
            if (I.hintRoutine == null) I.hintRoutine = I.StartCoroutine(I.HintRoutine());
        }

        /// <summary>A note or a line of dialogue on parchment (the secret room). Stays until HideNote.</summary>
        public void ShowNote(string title, string text)
        {
            noteTitle.text = title;
            noteText.text = text;
            if (!noteShown) UiAnim.Pop(notePanel, 0.9f, 0.2f);
            noteShown = true;
            notePanel.gameObject.SetActive(true);
        }

        public void HideNote() => noteShown = false;

        public IEnumerator FadeTo(float target, float time)
        {
            fade.gameObject.SetActive(true);
            float start = fade.color.a;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(start, target, t / time));
                yield return null;
            }
            SetFade(target);
            fade.gameObject.SetActive(target > 0f);
        }

        void SetFade(float a) => fade.color = new Color(0f, 0f, 0f, a);

        public void SetRoomCounter(string text) => counter.text = text;

        /// <summary>Objective line; it fades out when it hasn't changed for a few seconds.</summary>
        public void SetObjective(string text)
        {
            text = text ?? "";
            if (text == objectiveRaw) return;
            objectiveRaw = text;
            objective.text = text;
            objectiveAge = 0f;
        }

        /// <summary>The score is shown in the pause and end screens; the HUD keeps only coins.</summary>
        public void SetScore(int score, float mul)
        {
            targetScore = score;
            scoreMul = mul;
        }

        public void SetCoins(int coins)
        {
            if (coins > shownCoins && shownCoins >= 0) UiAnim.Punch(scoreText.transform, 0.15f, 0.18f);
            shownCoins = coins;
            scoreText.text = coins.ToString();
        }

        int shownCoins = -1;

        public void ShowBossBar(bool show)
        {
            bossBar.SetActive(show);
            if (show) UiAnim.Appear((RectTransform)bossBar.transform, 0f, new Vector2(0f, -60f), 0.35f);
        }

        public void ShowCard(string title, string rule, Color color, string label, string extra = "")
        {
            if (cardRoutine != null) StopCoroutine(cardRoutine);
            cardRoutine = StartCoroutine(CardRoutine(label, title, rule, extra, color, string.IsNullOrEmpty(extra) ? 2.2f : 3f));
        }

        public void ShowLawChange(string title, string rule, Color color)
        {
            string extra = Rules.Extra != Law.None ? Loc.F("hud.doubleLawShort", Rules.Info(Rules.Extra).name) : "";
            if (cardRoutine != null) StopCoroutine(cardRoutine);
            cardRoutine = StartCoroutine(CardRoutine(Loc.T("hud.lawChanged"), title, rule, extra, color, 1.6f));
        }

        public void ShowCombo(ComboDef c)
        {
            var a = Relics.Get(c.a);
            var b = Relics.Get(c.b);
            if (cardRoutine != null) StopCoroutine(cardRoutine);
            cardRoutine = StartCoroutine(CardRoutine(Loc.T("hud.combo"), c.Name, c.Desc, $"{a.Name} + {b.Name}", UiKit.Gold, 2.6f));
            relicCount = -1;
        }

        public void ShowUpgrades(Upgrade[] opts, Action<Upgrade> pick)
        {
            options = opts;
            onPick = pick;
            upgradePanel.SetActive(true);
            BuildUpgradeCards();
            UiAnim.FadeIn(upgradePanel.GetComponent<CanvasGroup>(), 0.15f);
        }

        // ---------------- update ----------------

        void Update()
        {
            var p = PlayerController.I;
            var state = RoomManager.I ? RoomManager.I.State : GameState.Menu;
            bool playing = p != null && state != GameState.Menu;

            leftPlate.gameObject.SetActive(playing);
            rightPlate.gameObject.SetActive(playing);
            relicRow.gameObject.SetActive(playing);
            if (playing)
            {
                UpdateHearts(p);
                UpdateRelics(p);
            }
            else { lastHalves = -1; lastMax = -1; relicCount = -1; }

            if (playing)
            {
                shieldText.enabled = p.ShieldReady;
                lawChip.text = Rules.Label;
                lawChip.color = Rules.Info(Rules.Current).color;

            }

            // objective: fully visible right after a change, then fades away
            objectiveAge += Time.unscaledDeltaTime;
            float oa = objectiveAge < ObjectiveHold ? 1f : Mathf.Clamp01(1f - (objectiveAge - ObjectiveHold) / 0.6f);
            objective.enabled = playing && objectiveRaw.Length > 0 && oa > 0.01f;
            objectiveGroup.alpha = oa;

            if (!playing && card.gameObject.activeSelf) card.gameObject.SetActive(false);
            if (!playing && hintPanel.gameObject.activeSelf && state == GameState.Menu)
            {
                hints.Clear();
                if (hintRoutine != null) { StopCoroutine(hintRoutine); hintRoutine = null; }
                hintPanel.gameObject.SetActive(false);
            }
            // keep the hint above the boss bar
            hintPanel.anchoredPosition = new Vector2(16f, bossBar.activeSelf ? 118f : 16f);

            var boss = Bosses.Current;
            if (bossBar.activeSelf && boss != null)
            {
                bossFill.fillAmount = Mathf.MoveTowards(bossFill.fillAmount, boss.Health01, Time.unscaledDeltaTime * 0.8f);
                bossName.text = boss.BossName;
                bossLaw.text = boss.StatusLine;
                bossLaw.color = boss.StatusColor;
            }
            else if (bossBar.activeSelf && Bosses.Defeated) bossFill.fillAmount = 0f;

            bool showBeat = playing && Rules.BeatMove;
            beat.gameObject.SetActive(showBeat);
            if (showBeat)
            {
                float b = MusicPlayer.Beat;
                float f = b - Mathf.Floor(b);
                float pulse = Mathf.Exp(-f * 7f);
                beatDot.rectTransform.localScale = Vector3.one * (0.45f + pulse * 0.55f);
                bool on = PlayerController.OnBeat();
                beatDot.color = on ? new Color(0.3f, 1f, 1f) : new Color(0.6f, 0.4f, 0.8f, 0.6f);
                beatRing.color = on ? new Color(1f, 0.3f, 0.9f) : new Color(0.5f, 0.3f, 0.6f, 0.6f);
            }

            noteAlpha = Mathf.MoveTowards(noteAlpha, noteShown && playing ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            noteGroup.alpha = noteAlpha;
            if (noteAlpha <= 0f && notePanel.gameObject.activeSelf) notePanel.gameObject.SetActive(false);

            hurtAlpha = Mathf.MoveTowards(hurtAlpha, 0f, Time.unscaledDeltaTime * 1.6f);
            hurtFlash.enabled = hurtAlpha > 0.01f;
            hurtFlash.color = new Color(0.8f, 0.05f, 0.05f, hurtAlpha);

            if (upgradePanel.activeSelf && options != null)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Pick(0);
                    else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Pick(1);
                    else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Pick(2);
                }
            }

            UpdateCursor(state);
        }

        void UpdateHearts(PlayerController p)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                int full = (i + 1) * 2;
                bool exists = full <= p.maxHalves;
                hearts[i].enabled = exists;
                if (!exists) continue;
                string s = p.halves >= full ? "ui_heart_full" : p.halves == full - 1 ? "ui_heart_half" : "ui_heart_empty";
                hearts[i].sprite = GameAssets.I ? GameAssets.I.D(s) : null;
            }
            if (lastHalves >= 0 && p.halves != lastHalves)
            {
                int idx = Mathf.Clamp((Mathf.Max(p.halves, lastHalves) - 1) / 2, 0, hearts.Length - 1);
                UiAnim.Punch(hearts[idx].transform, p.halves < lastHalves ? 0.45f : 0.3f, 0.3f);
                if (p.halves < lastHalves) hurtAlpha = 0.35f;
            }
            if (p.maxHalves != lastMax)
            {
                int n = Mathf.Clamp((p.maxHalves + 1) / 2, 1, hearts.Length);
                leftPlate.sizeDelta = new Vector2(Mathf.Max(230f, 30f + n * 44f + 60f), 64f);
                shieldText.rectTransform.anchoredPosition = new Vector2(24f + n * 44f + 4f, -20f);
                if (lastMax >= 0 && p.maxHalves > lastMax) UiAnim.Punch(leftPlate, 0.08f, 0.25f);
            }
            lastHalves = p.halves;
            lastMax = p.maxHalves;
        }

        /// <summary>Small icons of the run's relics under the hearts; relics in a combo get a gold dot.</summary>
        void UpdateRelics(PlayerController p)
        {
            var list = p.RelicList;
            if (list.Count == relicCount) return;
            bool grew = relicCount >= 0 && list.Count > relicCount;
            relicCount = list.Count;
            foreach (Transform c in relicRow) Destroy(c.gameObject);
            var inCombo = new HashSet<RelicId>();
            foreach (var c in p.ComboList) { inCombo.Add(Relics.Get(c).a); inCombo.Add(Relics.Get(c).b); }
            for (int i = 0; i < list.Count; i++)
            {
                var def = Relics.Get(list[i]);
                var sprite = GameAssets.I ? GameAssets.I.Any(def.sprite) : null;
                float x = 18f + (i % 12) * 34f, y = -16f - (i / 12) * 34f;
                var img = UiKit.Image(relicRow, def.key, sprite, Color.white, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(30f, 30f));
                img.preserveAspect = true;
                if (inCombo.Contains(def.id))
                    UiKit.Image(img.rectTransform, "Combo", Gfx.Dot, UiKit.Gold, new Vector2(1f, 0f), new Vector2(-2f, 2f), new Vector2(9f, 9f));
                if (grew && i == list.Count - 1) UiAnim.Pop(img.transform, 0.3f, 0.35f);
            }
        }

        void UpdateCursor(GameState state)
        {
            var a = GameAssets.I;
            if (!a) return;
            bool aiming = state == GameState.Playing && (MenuUI.I == null || !MenuUI.I.IsOpen) && !upgradePanel.activeSelf;
            var want = aiming ? a.cursorAim : a.cursorMenu;
            if (want == currentCursor) return;
            currentCursor = want;
            if (want) Cursor.SetCursor(want, aiming ? new Vector2(want.width / 2f, want.height / 2f) : Vector2.zero, CursorMode.Auto);
            else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        void Pick(int i)
        {
            if (options == null || i < 0 || i >= options.Length) return;
            var u = options[i];
            options = null;
            upgradePanel.SetActive(false);
            var cb = onPick;
            onPick = null;
            cb?.Invoke(u);
        }

        // ---------------- routines ----------------

        /// <summary>The room card: drops in, stays a couple of seconds, fades out completely.</summary>
        IEnumerator CardRoutine(string label, string title, string rule, string extra, Color color, float hold)
        {
            cardLabel.text = label;
            cardTitle.text = title;
            cardTitle.color = color;
            cardRule.text = rule;
            cardExtra.text = extra;
            var banner = UiKit.BannerFor(color);
            bannerL.sprite = bannerR.sprite = banner;
            bannerL.enabled = bannerR.enabled = banner;
            bool hasExtra = !string.IsNullOrEmpty(extra);
            card.sizeDelta = new Vector2(820f, hasExtra ? 262f : 228f);
            card.gameObject.SetActive(true);

            Vector2 center = new Vector2(0f, -150f);
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                card.anchoredPosition = Vector2.LerpUnclamped(center + new Vector2(0f, 40f), center, UiAnim.EaseOutBack(k));
                card.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, UiAnim.EaseOutBack(k));
                cardGroup.alpha = Mathf.Clamp01(k * 2f);
                yield return null;
            }
            card.anchoredPosition = center;
            card.localScale = Vector3.one;
            cardGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.45f;
                cardGroup.alpha = 1f - k;
                card.anchoredPosition = center + new Vector2(0f, Mathf.Round(k * 8f) * 3f);
                yield return null;
            }
            card.gameObject.SetActive(false);
            cardRoutine = null;
        }

        IEnumerator ToastRoutine(string text, Color color)
        {
            toast.text = text;
            toast.transform.localScale = Vector3.one * 0.6f;
            for (float t = 0f; t < 1.9f; t += Time.unscaledDeltaTime)
            {
                float a = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1.9f - t) * 3f);
                toast.color = new Color(color.r, color.g, color.b, a * color.a);
                toast.rectTransform.anchoredPosition = new Vector2(0f, 190f + Mathf.Round(t * 10f / 3f) * 3f);
                toast.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, UiAnim.EaseOutBack(t / 0.2f));
                yield return null;
            }
            toast.color = Color.clear;
            toastRoutine = null;
        }

        IEnumerator HintRoutine()
        {
            // let the room card speak first
            yield return new WaitForSecondsRealtime(cardRoutine != null ? 2.6f : 0.4f);
            while (hints.Count > 0)
            {
                hintText.text = hints.Dequeue();
                hintPanel.gameObject.SetActive(true);
                Sfx.Play("select", 0.4f, 1.3f);
                for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
                {
                    float k = t / 0.25f;
                    hintGroup.alpha = k;
                    hintPanel.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, UiAnim.EaseOutBack(k));
                    yield return null;
                }
                hintGroup.alpha = 1f;
                hintPanel.localScale = Vector3.one;
                // reading time grows with the text
                float hold = Mathf.Clamp(HintHold * 0.5f + hintText.text.Length * 0.05f, 4f, 9f);
                for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;
                for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
                {
                    hintGroup.alpha = 1f - t / 0.4f;
                    yield return null;
                }
                hintPanel.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            hintRoutine = null;
        }

        // ---------------- building ----------------

        void Build()
        {
            root = UiKit.Canvas(transform, "HUD", 10);

            hurtFlash = UiKit.Image(root, "HurtFlash", Gfx.Vignette, Color.clear, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Stretch(hurtFlash.rectTransform);
            hurtFlash.enabled = false;

            // left plate: hearts + shield
            var lp = UiKit.Plate(root, "LeftPlate", new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(230f, 64f));
            leftPlate = lp.rectTransform;
            leftPlate.pivot = new Vector2(0f, 1f);
            hearts = new Image[6];
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i] = UiKit.Image(leftPlate, "Heart" + i, null, Color.white, new Vector2(0f, 1f), new Vector2(38f + i * 44f, -32f), new Vector2(39f, 36f));
                hearts[i].preserveAspect = true;
            }
            shieldText = UiKit.Text(leftPlate, Loc.T("hud.shield"), 16, new Color(0.6f, 0.85f, 1f), TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(200f, -20f), new Vector2(110f, 30f), true);
            shieldText.rectTransform.pivot = new Vector2(0f, 1f);

            // relics under the hearts
            relicRow = UiKit.Rect(root, "Relics", new Vector2(0f, 1f), new Vector2(14f, -80f), new Vector2(420f, 70f));
            relicRow.pivot = new Vector2(0f, 1f);

            objective = UiKit.Text(root, "", 30, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(22f, -120f), new Vector2(760f, 72f));
            objective.rectTransform.pivot = new Vector2(0f, 1f);
            objectiveGroup = objective.gameObject.AddComponent<CanvasGroup>();
            objectiveGroup.blocksRaycasts = false;

            // right plate: room counter, law, score
            var rp = UiKit.Plate(root, "RightPlate", new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(380f, 112f));
            rightPlate = rp.rectTransform;
            rightPlate.pivot = new Vector2(1f, 1f);
            counter = UiKit.Text(rightPlate, "", 16, UiKit.Cream, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-22f, -26f), new Vector2(340f, 26f), true);
            counter.rectTransform.pivot = new Vector2(1f, 0.5f);
            lawChip = UiKit.Text(rightPlate, "", 26, Color.white, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-22f, -56f), new Vector2(340f, 30f));
            lawChip.rectTransform.pivot = new Vector2(1f, 0.5f);
            var coin = GameAssets.I ? GameAssets.I.Anim("coin_anim") : new Sprite[0];
            if (coin.Length > 0)
            {
                var c = UiKit.Image(rightPlate, "Coin", coin[0], Color.white, new Vector2(1f, 1f), new Vector2(-230f, -86f), new Vector2(18f, 21f));
                c.preserveAspect = true;
                UiSpriteAnim.Add(c, coin, 8f);
            }
            // coins: icon + number on the left of the third row
            scoreText = UiKit.Free(UiKit.Text(rightPlate, "0", 16, UiKit.Gold, TextAnchor.MiddleLeft, new Vector2(1f, 1f), new Vector2(-212f, -86f), new Vector2(190f, 24f), true));
            scoreText.rectTransform.pivot = new Vector2(0f, 0.5f);
            shownCoins = -1;

            // room / law card with two banners in the law's colour
            var cardImage = UiKit.Panel(root, "Card", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(820f, 228f));
            card = cardImage.rectTransform;
            card.pivot = new Vector2(0.5f, 1f);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.blocksRaycasts = false;
            // inside the ornate frame (about 52 UI units wide)
            bannerL = UiKit.Image(card, "BannerL", null, Color.white, new Vector2(0f, 1f), new Vector2(92f, -92f), new Vector2(48f, 48f));
            bannerR = UiKit.Image(card, "BannerR", null, Color.white, new Vector2(1f, 1f), new Vector2(-92f, -92f), new Vector2(48f, 48f));
            cardLabel = UiKit.Text(card, "", 16, UiKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(560f, 26f), true);
            cardTitle = UiKit.Text(card, "", 32, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(560f, 48f), true);
            cardRule = UiKit.Text(card, "", 32, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(700f, 44f));
            cardExtra = UiKit.Text(card, "", 26, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -194f), new Vector2(700f, 32f));
            card.gameObject.SetActive(false);

            toast = UiKit.Text(root, "", 16, Color.clear, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1000f, 40f), true);

            // boss bar: skull + stone frame + red pixel fill
            bossBar = new GameObject("BossBar", typeof(RectTransform));
            bossBar.transform.SetParent(root, false);
            var bb = (RectTransform)bossBar.transform;
            bb.anchorMin = bb.anchorMax = new Vector2(0.5f, 0f);
            bb.anchoredPosition = new Vector2(0f, 52f);
            bb.sizeDelta = new Vector2(660f, 30f);
            var a = GameAssets.I;
            bool da = a && a.DarkAges;
            var back = UiKit.Image(bb, "Back", a && a.uiBarBack ? a.uiBarBack : Gfx.White, a && a.uiBarBack ? Color.white : Color.black,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, da ? 28f : 30f));
            back.type = da ? Image.Type.Tiled : Image.Type.Sliced;
            back.pixelsPerUnitMultiplier = da ? 0.25f : UiKit.PixelScale;   // Dark Ages bar: 4 UI units per pixel
            bossFill = UiKit.Image(bb, "Fill", a && a.uiBarFill ? a.uiBarFill : Gfx.White, a && a.uiBarFill ? Color.white : new Color(0.85f, 0.15f, 0.2f),
                new Vector2(0.5f, 0.5f), Vector2.zero, da ? new Vector2(570f, 12f) : new Vector2(642f, 15f));
            bossFill.type = Image.Type.Filled;
            bossFill.fillMethod = Image.FillMethod.Horizontal;
            var skull = UiKit.Icon(bb, a ? a.D("skull") : null, new Vector2(-350f, 0f), 3f);
            skull.rectTransform.anchorMin = skull.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bossName = UiKit.Title(bb, Loc.T("hud.boss"), 16, new Color(1f, 0.6f, 0.55f), new Vector2(0f, 34f), new Vector2(660f, 28f));
            bossLaw = UiKit.Label(bb, "", 26, Color.white, new Vector2(0f, -32f), new Vector2(660f, 28f));
            bossBar.SetActive(false);

            // beat indicator
            var beatGo = new GameObject("Beat", typeof(RectTransform));
            beatGo.transform.SetParent(root, false);
            beat = (RectTransform)beatGo.transform;
            beat.anchorMin = beat.anchorMax = new Vector2(0.5f, 0f);
            beat.anchoredPosition = new Vector2(0f, 130f);
            beat.sizeDelta = new Vector2(70f, 70f);
            beatRing = UiKit.Image(beat, "Ring", Gfx.Ring, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            beatDot = UiKit.Image(beat, "Dot", Gfx.Dot, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            UiKit.Title(beat, Loc.T("hud.beat"), 16, new Color(1f, 1f, 1f, 0.75f), new Vector2(0f, -56f), new Vector2(300f, 26f));
            beatGo.SetActive(false);

            // hint panel: the sage from the classic tileset gives advice
            var hp = UiKit.Parchment(root, "Hint", new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(580f, 112f));
            hintPanel = hp.rectTransform;
            hintPanel.pivot = new Vector2(0f, 0f);
            hintGroup = hintPanel.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = false;
            var sage = UiKit.Icon(hintPanel, a ? a.C("npc_sage") : null, new Vector2(0f, 0f), 4f);
            sage.rectTransform.anchorMin = sage.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            sage.rectTransform.anchoredPosition = new Vector2(52f, 0f);
            UiBob.Add(sage, 2f, 2f);
            bool parchment = UiKit.HasParchment;
            hintText = UiKit.Text(hintPanel, "", 26, parchment ? UiKit.Ink : UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(450f, 84f));
            if (parchment) UiKit.NoShadow(hintText);
            hintText.rectTransform.pivot = new Vector2(0f, 0.5f);
            hintPanel.gameObject.SetActive(false);

            // notes in the secret room
            var np = UiKit.Parchment(root, "Note", new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(760f, 190f));
            notePanel = np.rectTransform;
            notePanel.pivot = new Vector2(0.5f, 0f);
            noteGroup = notePanel.gameObject.AddComponent<CanvasGroup>();
            noteGroup.blocksRaycasts = false;
            bool ink = UiKit.HasParchment;
            noteTitle = UiKit.Text(notePanel, "", 16, ink ? new Color(0.45f, 0.25f, 0.12f) : UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(680f, 28f), true);
            noteText = UiKit.Text(notePanel, "", 28, ink ? UiKit.Ink : UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(690f, 112f));
            if (ink) { UiKit.NoShadow(noteTitle); UiKit.NoShadow(noteText); }
            notePanel.gameObject.SetActive(false);
            noteShown = false;
            noteAlpha = 0f;

            // reward choice
            upgradePanel = UiKit.Screen(root, "Upgrades", 0.6f);
            UiKit.Title(upgradePanel.transform, Loc.T("hud.pick"), 32, UiKit.Gold, new Vector2(0f, 236f), new Vector2(900f, 52f));
            UiKit.Label(upgradePanel.transform, Loc.T("hud.pickHint"), 26, UiKit.Dim, new Vector2(0f, -236f), new Vector2(900f, 30f));
            upgradePanel.SetActive(false);
            hintPanel.SetSiblingIndex(upgradePanel.transform.GetSiblingIndex());   // hints stay readable over the reward choice

            // fade on top of the HUD
            fade = UiKit.Image(root, "Fade", Gfx.White, Color.black, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Stretch(fade.rectTransform);
            fade.gameObject.SetActive(true);
        }

        void BuildUpgradeCards()
        {
            foreach (Transform c in upgradePanel.transform)
                if (c.name.StartsWith("Option")) Destroy(c.gameObject);
            int n = options.Length;
            float spacing = n >= 3 ? 330f : 360f;
            Selectable first = null;
            var p = PlayerController.I;
            for (int i = 0; i < n; i++)
            {
                var u = options[i];
                float x = (i - (n - 1) / 2f) * spacing;
                int idx = i;
                var btn = UiKit.Button(upgradePanel.transform, "", new Vector2(x, 0f), new Vector2(310f, 380f), () => Pick(idx));
                btn.name = "Option" + i;
                if (first == null) first = btn;
                var rt = (RectTransform)btn.transform;

                UiKit.Title(rt, Loc.T(u.IsRelic ? "hud.relic" : "hud.flask"), 16, u.IsRelic ? u.relic.color : UiKit.Dim, new Vector2(0f, 160f), new Vector2(270f, 26f));
                var sprite = GameAssets.I ? GameAssets.I.Any(u.sprite) : null;
                float scale = 6f;
                if (sprite) scale = Mathf.Min(6f, 108f / Mathf.Max(sprite.rect.width, sprite.rect.height));
                var icon = UiKit.Icon(rt, sprite, new Vector2(0f, 74f), Mathf.Max(2f, Mathf.Floor(scale)));
                UiBob.Add(icon, 6f, 3f);

                UiKit.Title(rt, u.title, 16, u.IsRelic ? u.relic.color : UiKit.Gold, new Vector2(0f, -14f), new Vector2(280f, 44f));
                UiKit.Paragraph(rt, u.desc, 28, UiKit.Cream, new Vector2(0f, -78f), new Vector2(270f, 80f));

                var partner = u.IsRelic && p != null ? Relics.Partner(u.relic.id, p.RelicSet) : null;
                if (partner != null)
                {
                    var tag = UiKit.Title(rt, Loc.F("hud.comboWith", partner.Name), 16, UiKit.Gold, new Vector2(0f, -136f), new Vector2(280f, 26f));
                    UiAnim.Pop(tag.transform, 0.6f, 0.3f, 0.3f + 0.06f * i);
                }
                if (!UiKit.KeyIcon(rt, (i + 1).ToString(), new Vector2(0f, -166f)))
                    UiKit.Title(rt, (i + 1).ToString(), 16, UiKit.Dim, new Vector2(0f, -168f), new Vector2(60f, 24f));
                UiAnim.Appear(rt, 0.06f * i, new Vector2(0f, -50f), 0.3f);
            }
            UiKit.Select(first);
        }
    }
}
