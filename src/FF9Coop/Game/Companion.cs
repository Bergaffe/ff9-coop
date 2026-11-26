using System;
using System.Collections.Generic;
using FF9Coop.Input;
using Memoria;
using Memoria.Data;
using UnityEngine;
using XInputDotNetPure;

namespace FF9Coop.Game
{
    /// <summary>
    /// Sichtbare Begleiter-Figur im Feld für den Spieler, der nicht Leader ist.
    ///
    /// Bewusst KEIN Event-Objekt: Für die Event-Engine existiert die Figur nicht. Sie löst also
    /// keine Ausgänge, Kämpfe oder Skripte aus und kann keine Szene kaputt machen.
    /// Modell, Animationen und Shader werden wie in FieldMap.AddPlayer erzeugt, damit
    /// Moguri-Texturen und Memoria-Shader automatisch greifen.
    ///
    /// Regeln (siehe docs/design.md, "Leader + Leine"):
    /// - sichtbar nur im Feld, solange der Spieler Kontrolle hat (keine Cutscene, kein Menü)
    /// - bewegt sich auf begehbaren Dreiecken des Laufgitters
    /// - zu weit weg oder Kartenwechsel → wird zum Leader zurückgeholt
    /// - steht derselbe Charakter als NPC in der Szene → anderer Party-Charakter oder kein Begleiter
    /// </summary>
    internal static class Companion
    {
        /// <summary>
        /// Feldmodell je Charakter. Hauptcharaktere: GEO_MAIN_F0_*, Gast-/Zusatzcharaktere (Blank, Beatrix,
        /// z. B. spielbar durch AlternateFantasy) GEO_SUB_F0_*. Über [CompanionModels] in der INI erweiterbar,
        /// etwa für Charaktere aus anderen Mods. Animationen heißen gleich mit "ANH" statt "GEO".
        /// </summary>
        private static readonly Dictionary<CharacterId, String> s_modelCode = new Dictionary<CharacterId, String>
        {
            { CharacterId.Zidane, "GEO_MAIN_F0_ZDN" }, { CharacterId.Vivi, "GEO_MAIN_F0_VIV" }, { CharacterId.Garnet, "GEO_MAIN_F0_GRN" },
            { CharacterId.Steiner, "GEO_MAIN_F0_STN" }, { CharacterId.Freya, "GEO_MAIN_F0_FRJ" }, { CharacterId.Quina, "GEO_MAIN_F0_KUI" },
            { CharacterId.Eiko, "GEO_MAIN_F0_EIK" }, { CharacterId.Amarant, "GEO_MAIN_F0_SLM" },
            { CharacterId.Cinna, "GEO_SUB_F0_CNA" }, { CharacterId.Marcus, "GEO_SUB_F0_MRC" }, { CharacterId.Blank, "GEO_SUB_F0_BLN" },
            { CharacterId.Beatrix, "GEO_SUB_F0_BTX" },
        };
        private static readonly HashSet<String> s_knownModels = new HashSet<String>(s_modelCode.Values);

        /// <summary>Feldmodell (z. B. "GEO_MAIN_F0_VIV") eines spielbaren Charakters.</summary>
        internal static Boolean TryGetModelCode(CharacterId id, out String code) => s_modelCode.TryGetValue(id, out code);

        /// <summary>Überschreibt/ergänzt Modelle aus der INI ([CompanionModels] Beatrix = GEO_SUB_F0_BTX).</summary>
        internal static void SetModel(CharacterId id, String geo)
        {
            s_modelCode[id] = geo;
            s_knownModels.Add(geo);
        }

        /// <summary>"GEO_MAIN_F0_VIV" + "WALK" → "ANH_MAIN_F0_VIV_WALK".</summary>
        internal static String AnimName(String geo, String suffix) => "ANH" + geo.Substring(3) + "_" + suffix;

        /// <summary>Anzeigename für das HUD (Spielername des Charakters).</summary>
        internal static String DisplayName(String geo)
        {
            foreach (KeyValuePair<CharacterId, String> kv in s_modelCode)
                if (kv.Value == geo)
                    return Ui.CoopMenu.CharacterName(kv.Key);
            return geo;
        }

        private const Single MaxStepHeight = 250f;    // maximaler Höhenunterschied pro Schritt (verhindert Etagen-Sprünge)
        private const Single SpawnBehind = 120f;      // Abstand hinter dem Leader beim (Wieder-)Erscheinen

        private static GameObject s_go;
        private static Animation s_anim;
        private static String s_code;
        private static FieldMap s_map;
        private static Int32 s_mapNo = -1;
        private static Vector3 s_pos;
        private static Single s_yaw;
        private static String s_currentClip;
        private static Boolean s_loggedMeshCheck;
        private static Int32 s_errors;
        private static String s_wanted;
        private static Single s_nextChoose;

        public static Boolean IsVisible => s_go != null && s_go.activeSelf;
        public static String CharacterCode => s_code;
        /// <summary>Position im Koordinatenraum der Feldkarte (wie FieldMapActorController.curPos).</summary>
        public static Vector3 LocalPosition => s_pos;
        public static Vector3 WorldPosition => s_go != null ? s_go.transform.position : Vector3.zero;
        /// <summary>Blickrichtung in Grad, gleiche Konvention wie Actor.rotAngle[1].</summary>
        public static Single Yaw => s_yaw;
        /// <summary>Zeitpunkt (realtimeSinceStartup), seit dem der Begleiter wieder sichtbar ist.</summary>
        public static Single ShownSince { get; private set; }

        public static void Update()
        {
            CoopConfig cfg = CoopMod.Config;
            if (!cfg.CompanionEnabled || cfg.PlayerCount < 2 || s_errors >= 3)
            {
                Hide();
                return;
            }

            try
            {
                UpdateInternal(cfg);
            }
            catch (Exception ex)
            {
                s_errors++;
                CoopLog.Error(ex, $"Companion error ({s_errors}/3)" + (s_errors >= 3 ? " – companion disabled" : ""));
                Destroy();
            }
        }

        private static void UpdateInternal(CoopConfig cfg)
        {
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            EventEngine events = PersistenSingleton<EventEngine>.Instance;
            FieldMap map = events != null ? events.fieldmap : null;
            // The companion started this conversation/shop itself: stay visible and stand still in front
            // of the NPC until the game hands control back (otherwise it would vanish while talking).
            if (InteractionOwner.Player != 0 && IsVisible && map != null && s_map == map
                && FF9StateSystem.Common.FF9.fldMapNo == s_mapNo
                && ui != null && ui.State != UIManager.UIState.BattleHUD)
            {
                PlayClip("IDLE");
                return;
            }

            Boolean inField = ui != null && ui.State == UIManager.UIState.FieldHUD && map != null
                              && map.playerController != null && map.walkMesh != null;

            if (!inField)
            {
                Hide();
                return;
            }

            // Kartenwechsel: alte Figur gehört zur alten Karte → neu aufbauen
            Int32 mapNo = FF9StateSystem.Common.FF9.fldMapNo;
            if (s_map != map || s_mapNo != mapNo)
            {
                Destroy();
                s_map = map;
                s_mapNo = mapNo;
                s_loggedMeshCheck = false;
            }

            Boolean userControl = events.GetUserControl();
            // Auswahl nur alle 0,5 s neu prüfen (durchsucht alle Event-Objekte der Szene)
            if (userControl && (s_go == null || Time.realtimeSinceStartup >= s_nextChoose))
            {
                s_wanted = ChooseCharacter(map);
                s_nextChoose = Time.realtimeSinceStartup + 0.5f;
            }
            String wanted = userControl ? s_wanted : s_code;
            if (wanted == null)
            {
                Destroy();
                return;
            }
            if (s_go == null || s_code != wanted)
            {
                if (!userControl)
                    return; // erst erzeugen, wenn der Spieler wieder Kontrolle hat
                Spawn(map, wanted);
            }

            // In Cutscenes ausblenden. Danach geht es an derselben Stelle weiter (oder per Leine zurück).
            if (!userControl)
            {
                Hide();
                return;
            }
            if (!s_go.activeSelf)
            {
                s_go.SetActive(true);
                ShownSince = Time.realtimeSinceStartup;
            }

            Move(cfg, map);
        }

        // ---------------- Auswahl ----------------

        /// <summary>Erster Party-Charakter, der nicht der Leader ist und nicht als NPC in der Szene steht.</summary>
        private static String ChooseCharacter(FieldMap map)
        {
            String leaderCode = ModelCodeOf(map.playerController.originalActor);
            HashSet<String> inScene = ModelsInScene();
            CoopConfig cfg = CoopMod.Config;

            // Wunsch aus der INI zuerst
            if (cfg.CompanionCharacter != CharacterId.NONE && IsInParty(cfg.CompanionCharacter)
                && s_modelCode.TryGetValue(cfg.CompanionCharacter, out String preferred)
                && preferred != leaderCode && !inScene.Contains(preferred))
                return preferred;

            PLAYER[] members = FF9StateSystem.Common.FF9.party.member;
            for (Int32 i = 0; i < members.Length; i++)
            {
                PLAYER p = members[i];
                if (p == null || !s_modelCode.TryGetValue(p.Index, out String code))
                    continue;
                if (code == leaderCode || inScene.Contains(code))
                    continue;
                return code;
            }
            return null;
        }

        private static Boolean IsInParty(CharacterId id)
        {
            foreach (PLAYER p in FF9StateSystem.Common.FF9.party.member)
                if (p != null && p.Index == id)
                    return true;
            return false;
        }

        internal static String ModelCodeOf(PosObj obj)
        {
            if (obj == null || obj.model == UInt16.MaxValue)
                return null;
            String geo = FF9BattleDB.GEO.GetValue(obj.model); // z. B. "GEO_MAIN_F0_ZDN"
            // Nur Modelle spielbarer Charaktere zählen (keine NPCs wie GEO_SUB_F0_BRN, keine Moogles)
            return !String.IsNullOrEmpty(geo) && s_knownModels.Contains(geo) ? geo : null;
        }

        /// <summary>Hauptcharakter-Modelle, die gerade sichtbar als Event-Objekt in der Szene stehen.</summary>
        private static HashSet<String> ModelsInScene()
        {
            HashSet<String> result = new HashSet<String>();
            Dictionary<Int32, FF9Char> chars = FF9StateSystem.Common.FF9.charArray;
            if (chars == null)
                return result;
            foreach (FF9Char c in chars.Values)
            {
                if (c?.evt == null || !IsShown(c.geo))
                    continue;
                String code = ModelCodeOf(c.evt);
                if (code != null)
                    result.Add(code);
            }
            return result;
        }

        /// <summary>FF9 blendet Event-Objekte teils über die Renderer aus, deshalb reicht activeInHierarchy nicht.</summary>
        private static Boolean IsShown(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
                return false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                if (r.enabled)
                    return true;
            return false;
        }

        // ---------------- Erzeugen / Entfernen ----------------

        private static void Spawn(FieldMap map, String code)
        {
            Destroy();
            String geo = code; // vollständiger Modellname, z. B. GEO_MAIN_F0_VIV oder GEO_SUB_F0_BTX
            GameObject go = ModelFactory.CreateModel(geo, false, true, Configuration.Graphics.ElementsSmoothTexture);
            if (go == null)
            {
                CoopLog.Warning($"Companion model {geo} not found");
                return;
            }
            go.name = "FF9Coop_Companion";
            go.transform.parent = map.transform;

            s_anim = go.GetComponent<Animation>() ?? go.AddComponent<Animation>();
            foreach (String suffix in new[] { "IDLE", "WALK", "RUN" })
                AnimationFactory.AddAnimWithAnimatioName(go, Companion.AnimName(code, suffix));
            foreach (AnimationState state in s_anim)
                state.wrapMode = WrapMode.Loop;

            // Wie FieldMap.AddPlayer: gespiegelte Skalierung und Feld-Shader
            go.transform.localScale = new Vector3(-1f, -1f, 1f);
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Shader shader = Memoria.Scripts.ShadersLoader.Find(Configuration.Shaders.FieldCharacterShader);
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.materials)
                {
                    if (shader != null)
                        material.shader = shader;
                    ModelFactory.SetMatFilter(material, Configuration.Graphics.ElementsSmoothTexture);
                }
            }
            NormalSolver.SmoothCharacterMesh(renderers);

            s_go = go;
            s_code = code;
            s_currentClip = null;
            ShownSince = Time.realtimeSinceStartup;
            PlaceBehindLeader(map);
            ForceNonCulling(map, go);
            CoopLog.Message($"Companion {code} appears on map {s_mapNo}");
        }

        /// <summary>
        /// Nachbau von FieldMapActor.ForcedNonCullingMesh: Der Feld-Shader projiziert selbst (PSX-GTE),
        /// deshalb passen Unitys Bounds nicht zur Bildposition. Ohne riesige Bounds blendet das
        /// Frustum-Culling Modellteile (z. B. den Kopf) oder die ganze Figur fälschlich aus.
        /// </summary>
        internal static void ForceNonCulling(FieldMap map, GameObject go)
        {
            Vector3 center = Vector3.zero;
            Camera cam = map.GetMainCamera();
            if (cam != null)
            {
                Single mid = (cam.farClipPlane - cam.nearClipPlane) / 2f + cam.nearClipPlane;
                center = go.transform.InverseTransformPoint(cam.transform.position + cam.transform.forward.normalized * mid);
            }
            Bounds huge = new Bounds(center, Vector3.one * Single.MaxValue * 0.01f);
            foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = false; // sonst würde Unity die Bounds neu berechnen
                smr.localBounds = huge;
            }
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null)
                    mf.sharedMesh.bounds = huge;
        }

        private static void Hide()
        {
            if (s_go != null && s_go.activeSelf)
                s_go.SetActive(false);
        }

        private static void Destroy()
        {
            if (s_go != null)
                UnityEngine.Object.Destroy(s_go);
            s_go = null;
            s_anim = null;
            s_code = null;
        }

        private static void PlaceBehindLeader(FieldMap map)
        {
            FieldMapActorController leader = map.playerController;
            Vector3 lp = leader.curPos;
            Single leaderYaw = leader.originalActor != null ? leader.originalActor.rotAngle[1] : 0f;
            // Blickrichtung des Leaders: Rotation um Y; "hinter" ihm = entgegengesetzt
            Vector3 forward = Quaternion.AngleAxis(leaderYaw, Vector3.up) * new Vector3(0f, 0f, -1f);
            Vector3 candidate = lp - forward * SpawnBehind;

            if (TryGetHeight(map.walkMesh, candidate, lp.y, out Single h))
                candidate.y = h;
            else
                candidate = lp;

            s_pos = candidate;
            s_yaw = leaderYaw;
            ApplyTransform();

            if (!s_loggedMeshCheck)
            {
                s_loggedMeshCheck = true;
                Boolean onMesh = TryGetHeight(map.walkMesh, lp, lp.y, out Single meshY);
                CoopLog.Message($"Walkmesh check: leader y={lp.y:0}, mesh y={(onMesh ? meshY.ToString("0") : "—")}");
            }
        }

        // ---------------- Bewegung ----------------

        private static void Move(CoopConfig cfg, FieldMap map)
        {
            FieldMapActorController leader = map.playerController;

            // Leine: zu weit weg → zurück zum Leader
            Vector3 toLeader = leader.curPos - s_pos;
            toLeader.y = 0f;
            if (toLeader.magnitude > cfg.CompanionLeash)
            {
                PlaceBehindLeader(map);
                return;
            }

            Int32 player = CompanionPlayer(cfg);
            Vector2 stick = ReadMovement(cfg, player, out Boolean run);

            Single tps = Configuration.Graphics.FieldTPS > 0 ? Configuration.Graphics.FieldTPS : 30;
            Single speed = 30f * tps * (run ? 2f : 1f); // Einheiten pro Sekunde, wie der Leader (30 pro Tick)

            if (stick.sqrMagnitude < 0.04f)
            {
                PlayClip("IDLE");
                ApplyTransform();
                return;
            }

            Vector3 dir = new Vector3(stick.x, 0f, stick.y);
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();
            dir = Quaternion.Euler(0f, FF9StateSystem.Field.twist.y, 0f) * dir;

            Vector3 step = dir * speed * Time.deltaTime;
            Vector3 next = s_pos + step;
            CollectBlockers(map);
            // Auf dem Laufgitter bleiben und nicht durch NPCs laufen; an Kanten/NPCs entlanggleiten
            // (erst ganzer Schritt, dann nur X, dann nur Z)
            if (!TryStep(map.walkMesh, next, ref s_pos)
                && !TryStep(map.walkMesh, new Vector3(next.x, s_pos.y, s_pos.z), ref s_pos)
                && !TryStep(map.walkMesh, new Vector3(s_pos.x, s_pos.y, next.z), ref s_pos))
            {
                // blockiert: nur drehen
            }

            Single targetYaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
            s_yaw = Mathf.LerpAngle(s_yaw, targetYaw, 0.4f);
            PlayClip(run && stick.magnitude > 0.5f ? "RUN" : "WALK");
            ApplyTransform();
        }

        // ---------------- Kollision mit NPCs ----------------

        private struct Blocker
        {
            public Single X, Y, Z, Radius;
        }

        private static readonly List<Blocker> s_blockers = new List<Blocker>();
        private static Single s_selfRadius;

        /// <summary>
        /// Sammelt einmal pro Bewegungsschritt alle NPCs, mit denen der Begleiter kollidiert. Regeln wie
        /// WalkMesh.Collision (Modus 0, also Körper): sichtbare Event-Figuren (cid 4), Radius = 4 · collRad,
        /// Kollisions-Flags wie beim Spieler. Der Leader selbst blockiert nicht, sonst könnte er den
        /// Begleiter (den das Spiel nicht kennt) in Engstellen einsperren.
        /// </summary>
        private static void CollectBlockers(FieldMap map)
        {
            s_blockers.Clear();
            EventEngine events = PersistenSingleton<EventEngine>.Instance;
            Actor leader = map.playerController.originalActor;
            s_selfRadius = 4f * Math.Max((Byte)8, leader.collRad);
            Boolean leaderIgnoresNpc = (leader.flags & 4) != 0;

            for (ObjList list = events.GetActiveObjList(); list != null; list = list.next)
            {
                Obj obj = list.obj;
                if (obj == null || obj.cid != 4 || obj == leader)
                    continue;
                Actor npc = (Actor)obj;
                if (npc.collRad == 0 || npc.pos == null || !IsShownCached(npc))
                    continue;
                // Wie im Spiel: keine Kollision, wenn beide Seiten sie per Flag abgeschaltet haben
                if (leaderIgnoresNpc && (npc.flags & 2) != 0)
                    continue;
                s_blockers.Add(new Blocker { X = npc.pos[0], Y = npc.pos[1], Z = npc.pos[2], Radius = 4f * npc.collRad });
            }
        }

        private static readonly Dictionary<Obj, KeyValuePair<Boolean, Single>> s_shownCache = new Dictionary<Obj, KeyValuePair<Boolean, Single>>();

        /// <summary>Sichtbarkeit (alle Renderer durchsuchen) nur alle 0,5 s pro NPC neu prüfen, vermeidet Garbage pro Frame.</summary>
        private static Boolean IsShownCached(Actor npc)
        {
            Single now = Time.realtimeSinceStartup;
            if (s_shownCache.TryGetValue(npc, out KeyValuePair<Boolean, Single> entry) && now < entry.Value)
                return entry.Key;
            Boolean shown = IsShown(npc.go);
            if (s_shownCache.Count > 256)
                s_shownCache.Clear(); // Objekte vergangener Karten nicht ewig festhalten
            s_shownCache[npc] = new KeyValuePair<Boolean, Single>(shown, now + 0.5f);
            return shown;
        }

        /// <summary>
        /// True, wenn der Schritt in einen NPC hineinführt. Steckt der Begleiter schon in einem
        /// (der NPC ist in ihn hineingelaufen), darf er sich herausbewegen, aber nicht tiefer hinein.
        /// </summary>
        private static Boolean BlockedByNpc(Vector3 from, Vector3 to)
        {
            for (Int32 i = 0; i < s_blockers.Count; i++)
            {
                Blocker b = s_blockers[i];
                if (Math.Abs(b.Y - to.y) > 400f) // andere Etage (wie im Spiel: ±400)
                    continue;
                Single r = b.Radius + s_selfRadius;
                Single dxTo = to.x - b.X, dzTo = to.z - b.Z;
                Single distTo = dxTo * dxTo + dzTo * dzTo;
                if (distTo >= r * r)
                    continue;
                Single dxFrom = from.x - b.X, dzFrom = from.z - b.Z;
                Single distFrom = dxFrom * dxFrom + dzFrom * dzFrom;
                if (distTo < distFrom)
                    return true;
            }
            return false;
        }

        private static Boolean TryStep(WalkMesh mesh, Vector3 target, ref Vector3 pos)
        {
            if (BlockedByNpc(pos, target))
                return false;
            if (!TryGetHeight(mesh, target, pos.y, out Single h))
                return false;
            pos = new Vector3(target.x, h, target.z);
            return true;
        }

        /// <summary>
        /// Höhe des begehbaren Dreiecks unter <paramref name="pos"/> (XZ). Bei mehreren Etagen gewinnt das
        /// Dreieck, das der aktuellen Höhe am nächsten ist, und es darf nicht zu weit davon weg sein.
        /// </summary>
        private static Boolean TryGetHeight(WalkMesh mesh, Vector3 pos, Single currentY, out Single height)
        {
            height = 0f;
            Single bestDiff = Single.MaxValue;
            List<WalkMeshTriangle> tris = mesh.tris;
            if (tris == null)
                return false;
            for (Int32 i = 0; i < tris.Count; i++)
            {
                WalkMeshTriangle t = tris[i];
                if ((t.triFlags & 1) == 0 || t.originalVertices == null)
                    continue;
                Vector3 a = t.originalVertices[0], b = t.originalVertices[1], c = t.originalVertices[2];
                if (!Barycentric(pos, a, b, c, out Single u, out Single v, out Single w))
                    continue;
                Single y = a.y * u + b.y * v + c.y * w;
                Single diff = Math.Abs(y - currentY);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    height = y;
                }
            }
            return bestDiff <= MaxStepHeight;
        }

        private static Boolean Barycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Single u, out Single v, out Single w)
        {
            Single v0x = b.x - a.x, v0z = b.z - a.z, v1x = c.x - a.x, v1z = c.z - a.z, v2x = p.x - a.x, v2z = p.z - a.z;
            Single den = v0x * v1z - v1x * v0z;
            u = v = w = 0f;
            if (Math.Abs(den) < 1e-6f)
                return false;
            v = (v2x * v1z - v1x * v2z) / den;
            w = (v0x * v2z - v2x * v0z) / den;
            u = 1f - v - w;
            const Single eps = -0.001f;
            return u >= eps && v >= eps && w >= eps;
        }

        private static void ApplyTransform()
        {
            if (s_go == null)
                return;
            s_go.transform.localPosition = s_pos;
            // Gleiche Konvention wie FieldMapActor: Rotation um die Y-Achse mit rotAngle[1]
            s_go.transform.localRotation = Quaternion.AngleAxis(s_yaw, Vector3.up);
        }

        private static void PlayClip(String suffix)
        {
            if (s_anim == null)
                return;
            String clip = Companion.AnimName(s_code, suffix);
            if (clip == s_currentClip && s_anim.isPlaying)
                return;
            if (s_anim.GetClip(clip) == null)
                return;
            s_anim.CrossFade(clip, 0.15f);
            s_currentClip = clip;
        }

        // ---------------- Eingabe ----------------

        /// <summary>Der erste Spieler, der nicht Leader ist.</summary>
        public static Int32 CompanionPlayer(CoopConfig cfg)
        {
            for (Int32 p = 1; p <= cfg.PlayerCount; p++)
                if (p != cfg.LeaderPlayer)
                    return p;
            return 0;
        }

        /// <summary>
        /// Rohe Eingabe des Begleiter-Spielers. Das Spiel sieht sie im Feld nicht, denn dort ist nur der Leader berechtigt.
        /// </summary>
        internal static Vector2 ReadMovement(CoopConfig cfg, Int32 player, out Boolean run)
        {
            Vector2 move = Vector2.zero;
            run = false;
            Boolean runToggled = FF9StateSystem.Settings.cfg.move == 1UL; // Spieloption "immer laufen"

            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                if (cfg.PadPlayer[pad] != player || !PadInput.IsConnected(pad))
                    continue;
                GamePadState s = PadInput.Current(pad);
                Vector2 stick = new Vector2(s.ThumbSticks.Left.X, s.ThumbSticks.Left.Y);
                if (s.DPad.Left == ButtonState.Pressed) stick.x = -1f;
                if (s.DPad.Right == ButtonState.Pressed) stick.x = 1f;
                if (s.DPad.Up == ButtonState.Pressed) stick.y = 1f;
                if (s.DPad.Down == ButtonState.Pressed) stick.y = -1f;
                if (stick.sqrMagnitude > move.sqrMagnitude)
                {
                    move = stick;
                    run = (s.Buttons.B == ButtonState.Pressed) ^ runToggled;
                }
            }

            if (cfg.KeyboardPlayer == player)
            {
                Vector2 keys = Vector2.zero;
                if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) keys.x -= 1f;
                if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) keys.x += 1f;
                if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) keys.y += 1f;
                if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) keys.y -= 1f;
                if (keys.sqrMagnitude > move.sqrMagnitude)
                {
                    move = keys;
                    run = UnityEngine.Input.GetKey(cfg.CompanionRunKey) ^ runToggled;
                }
            }
            return move;
        }
    }
}
