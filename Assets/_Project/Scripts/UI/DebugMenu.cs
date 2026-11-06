#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace RulesShift
{
    /// <summary>
    /// Developer menu on F1 (editor and development builds only): god mode, coins, any relic or active item,
    /// spawn any enemy, change the law, skip rooms, jump into worlds and special rooms. Not part of the game.
    /// </summary>
    public class DebugMenu : MonoBehaviour
    {
        bool open;
        int tab;
        Vector2 scroll;
        bool spawnElite;
        float fps, fpsTimer;
        int frames;
        GUIStyle small;

        static readonly string[] Tabs = { "Игрок", "Реликвии", "Комнаты", "Враги", "Сюжет" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("DebugMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugMenu>();
        }

        void Update()
        {
            frames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f) { fps = frames / fpsTimer; frames = 0; fpsTimer = 0f; }
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) open = !open;
            if (ThreeFingerTap()) open = !open;   // phones: no F1, three fingers at once
        }

        static bool ThreeFingerTap()
        {
            var ts = Touchscreen.current;
            if (ts == null) return false;
            int held = 0;
            bool fresh = false;
            foreach (var t in ts.touches)
            {
                if (t == null || !t.press.isPressed) continue;
                held++;
                if (t.press.wasPressedThisFrame) fresh = true;
            }
            return fresh && held == 3;
        }

        void OnGUI()
        {
            if (!open) return;
            if (small == null) small = new GUIStyle(GUI.skin.button) { fontSize = 11, wordWrap = true };
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var rm = RoomManager.I;
            var p = rm ? rm.DebugPlayer : null;

            GUILayout.BeginArea(new Rect(10, 10, 440, 700), GUI.skin.box);
            GUILayout.Label($"DEBUG (F1)   FPS {fps:0}   врагов {Enemy.All.Count}   " +
                            (p ? $"урон {p.Damage:0.0}  скор. {p.EffectiveRate:0.0}/с  реликвий {p.RelicList.Count}" : ""));
            tab = GUILayout.Toolbar(tab, Tabs);
            if (rm != null && tab == 4)
            {
                // the story works from the main menu too (the prologue and the endings)
                scroll = GUILayout.BeginScrollView(scroll);
                StoryTab(rm, p != null && rm.DebugReady);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (rm == null || p == null || !rm.DebugReady)
            {
                GUILayout.Label("Начни забег, чтобы пользоваться меню.");
                GUILayout.EndArea();
                return;
            }
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: PlayerTab(rm, p); break;
                case 1: RelicsTab(p); break;
                case 2: RoomsTab(rm); break;
                default: EnemiesTab(rm); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void PlayerTab(RoomManager rm, PlayerController p)
        {
            p.godMode = GUILayout.Toggle(p.godMode, " Бессмертие");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+50 монет")) rm.AddCoins(50);
            if (GUILayout.Button("Вылечить")) p.Heal(99);
            if (GUILayout.Button("+1 сердце")) { p.maxHalves = Mathf.Min(PlayerController.MaxHalvesCap, p.maxHalves + 2); p.Heal(2); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Урон ×1.5")) p.damage *= 1.5f;
            if (GUILayout.Button("Скорострельность ×1.3")) p.fireRate *= 1.3f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Активный предмет: " + (p.Active != null ? p.Active.Name + $" ({p.ActiveCharge}/{p.Active.charge})" : "нет"));
            if (GUILayout.Button("Зарядить")) p.ChargeActive(9);
            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (var a in Actives.All)
            {
                if (GUILayout.Button(a.Name, small, GUILayout.Width(100))) p.SetActive(a.id);
                if (++col % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
        }

        void RelicsTab(PlayerController p)
        {
            if (GUILayout.Button("Случайная реликвия"))
            {
                var r = Upgrades.RandomRelic(p);
                if (r != null) p.AddRelic(r.id);
            }
            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (var r in Relics.All)
            {
                bool has = p.Has(r.id);
                GUI.enabled = !has;
                if (GUILayout.Button((has ? "+ " : "") + r.Name, small, GUILayout.Width(100), GUILayout.Height(30))) p.AddRelic(r.id);
                GUI.enabled = true;
                if (++col % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("Комбо: " + p.ComboList.Count + " / " + Relics.Combos.Length);
        }

        void RoomsTab(RoomManager rm)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Убить всех")) rm.DebugKillAll();
            if (GUILayout.Button("Зачистить комнату")) rm.DebugClear();
            if (GUILayout.Button("Следующая комната")) rm.DebugNext();
            GUILayout.EndHorizontal();

            GUILayout.Label("Закон в этой комнате:");
            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (Law l in System.Enum.GetValues(typeof(Law)))
            {
                if (l == Law.Boss) continue;
                if (GUILayout.Button(Rules.Info(l).name, small, GUILayout.Width(100))) rm.SetLaw(l, true);
                if (++col % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Перейти:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Версия 1.0")) rm.DebugWorld(World.Retro);
            if (GUILayout.Button("Пещеры")) rm.DebugWorld(World.Caves);
            if (GUILayout.Button("Босс этажа")) rm.DebugRoom(Objective.Boss);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Испытание")) rm.DebugRoom(Objective.Challenge);
            if (GUILayout.Button("Засада")) rm.DebugRoom(Objective.Ambush);
            if (GUILayout.Button("Тайник")) rm.DebugRoom(Objective.Treasure);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Гача")) rm.DebugRoom(Objective.Gamble);
            if (GUILayout.Button("Лавка")) rm.DebugRoom(Objective.Shop);
            if (GUILayout.Button("Алтарь")) rm.DebugRoom(Objective.Shrine);
            GUILayout.EndHorizontal();
        }

        static readonly string[] NpcIds = { "mio", "doc", "beta", "goro", "angel" };

        void StoryTab(RoomManager rm, bool inRun)
        {
            GUILayout.Label("Пролог и концовки (без сохранения, потом — в меню):");
            if (GUILayout.Button("Пролог: ночь, когда включилась консоль")) { open = false; rm.PreviewPrologue(); }
            for (int i = 0; i < RoomManager.EndingCount; i++)
                if (GUILayout.Button($"Концовка {i + 1}: {Loc.T("ending." + i + ".name")}")) { open = false; rm.PreviewEnding(i, false); }

            GUILayout.Space(6);
            if (!inRun)
            {
                GUILayout.Label("Остальные сцены — во время забега.");
                return;
            }
            GUILayout.Label("Сцены в забеге:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Знакомство с Пикси")) { open = false; rm.DebugPixieIntro(); }
            if (GUILayout.Button("Встреча с Администратором")) { open = false; rm.DebugLawgiver(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Слова у портала: Версия 1.0")) { open = false; rm.DebugWorldIntro(World.Retro); }
            if (GUILayout.Button("Слова у портала: Пещеры")) { open = false; rm.DebugWorldIntro(World.Caves); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Войти в Версию 1.0")) { open = false; rm.DebugWorld(World.Retro); }
            if (GUILayout.Button("Войти в Пещеры")) { open = false; rm.DebugWorld(World.Caves); }
            GUILayout.EndHorizontal();

            GUILayout.Label("Сохранения Амелии (тайные комнаты):");
            GUILayout.BeginHorizontal();
            for (int m = 0; m < RoomManager.MemoryCount; m++)
                if (GUILayout.Button(Loc.T("room.secret." + m), small, GUILayout.Height(34))) { open = false; rm.DebugMemory(m); }
            GUILayout.EndHorizontal();

            GUILayout.Label("Застрявшие игроки (подойди и нажми E; каждая новая встреча — следующая часть истории):");
            GUILayout.BeginHorizontal();
            foreach (var id in NpcIds)
            {
                int met = 0;
                foreach (var x in SaveSystem.Data.npcMeets) if (x == id) met++;
                if (GUILayout.Button($"{Loc.T("npc." + id + ".name")} ({met})", small, GUILayout.Width(80))) rm.DebugNpc(id);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Сбросить встречи с NPC (история с начала)"))
            {
                SaveSystem.Data.npcMeets.Clear();
                SaveSystem.Save();
            }

            GUILayout.Space(6);
            GUILayout.Label("Кодекс (меняет сохранение):");
            if (GUILayout.Button("Открыть в кодексе пролог, все сохранения и концовки"))
            {
                var d = SaveSystem.Data;
                d.prologueSeen = true;
                d.saveFound = true;
                d.saveFiles = (1 << RoomManager.MemoryCount) - 1;
                d.finales = (1 << RoomManager.EndingCount) - 1;
                SaveSystem.Save();
            }
        }

        void EnemiesTab(RoomManager rm)
        {
            spawnElite = GUILayout.Toggle(spawnElite, " Элитный (с особенностью)");
            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (EnemyKind k in System.Enum.GetValues(typeof(EnemyKind)))
            {
                if (GUILayout.Button(k.ToString(), small, GUILayout.Width(100))) rm.DebugSpawn(k, spawnElite);
                if (++col % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Убить всех")) rm.DebugKillAll();
        }
    }
}
#endif
