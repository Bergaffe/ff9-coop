using System;
using Memoria;
using Memoria.Data;
using UnityEngine;

namespace FF9Coop.Game
{
    /// <summary>
    /// Begleiter auf der Weltkarte (eigene 3D-Engine, siehe WMWorld/ff9.cs).
    ///
    /// Aufbau wie WMWorld.addWMActorOnly/addGameObjectToWMActor:
    ///   Halter-Objekt in WMWorld.TranslatingObjectsGroup (wird beim "Umwickeln" der Welt mitverschoben)
    ///   └ Modell mit localRotation (0,0,180) und Skalierung 1/256
    /// Bodenhöhe und Begehbarkeit kommen aus dem Spiel selbst: ff9.w_cellHit (Strahl auf das Gelände)
    /// und ff9.w_movementCheckTopographID mit der Gelände-Maske "zu Fuß" (w_moveCHRControl[0].limit).
    /// Sichtbar nur, solange der Leader zu Fuß unterwegs ist (nicht auf Chocobo/Luftschiff).
    /// </summary>
    internal static class WorldCompanion
    {
        private const Single ModelScale = 1f / 256f;
        private const Single SpawnBehind = 1.5f;     // Welt-Einheiten (ein Block = 64)
        private const Single SpeedPerTick = 112f / 256f; // speed_move "zu Fuß" (112) in Welt-Einheiten

        private static GameObject s_holder;
        private static GameObject s_model;
        private static Animation s_anim;
        private static String s_code;
        private static String s_clip;
        private static Single s_yaw;
        private static Single s_nextChoose;
        private static String s_wanted;
        private static Int32 s_errors;

        public static Boolean IsVisible => s_holder != null && s_holder.activeSelf;
        public static String CharacterCode => s_code;

        public static void Update()
        {
            CoopConfig cfg = CoopMod.Config;
            if (!cfg.CompanionEnabled || !cfg.WorldCompanionEnabled || cfg.PlayerCount < 2 || s_errors >= 3)
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
                CoopLog.Error(ex, $"World map companion error ({s_errors}/3)" + (s_errors >= 3 ? " – disabled" : ""));
                Destroy();
            }
        }

        private static void UpdateInternal(CoopConfig cfg)
        {
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            EventEngine events = PersistenSingleton<EventEngine>.Instance;
            WMWorld world = ff9.world;
            WMActor leader = ff9.w_moveActorPtr;
            if (ui == null || ui.State != UIManager.UIState.WorldHUD || world == null || leader == null || world.TranslatingObjectsGroup == null)
            {
                Hide();
                return;
            }

            Boolean onFoot = ff9.w_moveCHRControl_No == 0;
            Boolean control = events != null && events.GetUserControl();
            if (!onFoot || !control)
            {
                Hide();
                return;
            }

            if (s_holder == null || Time.realtimeSinceStartup >= s_nextChoose)
            {
                s_wanted = ChooseCharacter(leader);
                s_nextChoose = Time.realtimeSinceStartup + 0.5f;
            }
            if (s_wanted == null)
            {
                Destroy();
                return;
            }
            // Halter wurde mit der Weltkarte zerstört (Unity-"null") oder anderer Charakter gewünscht
            if (s_holder == null || s_code != s_wanted || s_holder.transform.parent != world.TranslatingObjectsGroup)
                Spawn(world, leader, s_wanted);
            if (s_holder == null)
                return;
            if (!s_holder.activeSelf)
            {
                s_holder.SetActive(true);
                PlaceBehind(leader); // nach Chocobo/Luftschiff/Cutscene neben dem Leader wieder auftauchen
            }

            Move(cfg, world, leader);
        }

        private static String ChooseCharacter(WMActor leader)
        {
            String leaderCode = Companion.ModelCodeOf(leader.originalActor);
            CoopConfig cfg = CoopMod.Config;
            PLAYER[] members = FF9StateSystem.Common.FF9.party.member;

            if (cfg.CompanionCharacter != CharacterId.NONE && Companion.TryGetModelCode(cfg.CompanionCharacter, out String preferred) && preferred != leaderCode)
                foreach (PLAYER p in members)
                    if (p != null && p.Index == cfg.CompanionCharacter)
                        return preferred;

            foreach (PLAYER p in members)
                if (p != null && Companion.TryGetModelCode(p.Index, out String code) && code != leaderCode)
                    return code;
            return null;
        }

        private static void Spawn(WMWorld world, WMActor leader, String code)
        {
            Destroy();
            String geo = code; // vollständiger Modellname, z. B. GEO_MAIN_F0_VIV oder GEO_SUB_F0_BTX
            GameObject model = ModelFactory.CreateModel(geo, false, true, Configuration.Graphics.WorldSmoothTexture);
            if (model == null)
            {
                CoopLog.Warning($"World map companion model {geo} not found");
                return;
            }

            GameObject holder = new GameObject("FF9Coop_CompanionWM");
            holder.transform.parent = world.TranslatingObjectsGroup;
            model.transform.parent = holder.transform;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            model.transform.localScale = Vector3.one * ModelScale;

            s_anim = model.GetComponent<Animation>() ?? model.AddComponent<Animation>();
            foreach (String suffix in new[] { "IDLE", "WALK", "RUN" })
                AnimationFactory.AddAnimWithAnimatioName(model, Companion.AnimName(code, suffix));
            foreach (AnimationState state in s_anim)
                state.wrapMode = WrapMode.Loop;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                    ModelFactory.SetMatFilter(m, Configuration.Graphics.WorldSmoothTexture);
            NoCulling(model);

            s_holder = holder;
            s_model = model;
            s_code = code;
            s_clip = null;
            PlaceBehind(leader);
            CoopLog.Message($"World map companion {code} appears");
        }

        private static void PlaceBehind(WMActor leader)
        {
            Single leaderYaw = leader.rot1;
            Vector3 forward = Quaternion.Euler(0f, leaderYaw, 0f) * new Vector3(0f, 0f, -1f);
            Vector3 p = leader.pos - forward * SpawnBehind;
            if (TryGround(ref p, out Int32 id))
                p.y += Slice(leader, id, 1f, true); // z. B. im Wald sofort eingesunken erscheinen
            else
                p = leader.pos;
            s_holder.transform.position = p;
            s_yaw = leaderYaw;
            s_holder.transform.rotation = Quaternion.Euler(0f, s_yaw, 0f);
        }

        private static void Move(CoopConfig cfg, WMWorld world, WMActor leader)
        {
            Vector3 pos = s_holder.transform.position;
            Vector3 toLeader = leader.pos - pos;
            toLeader.y = 0f;
            if (toLeader.magnitude > cfg.WorldLeash)
            {
                PlaceBehind(leader);
                return;
            }

            Int32 player = Companion.CompanionPlayer(cfg);
            Vector2 stick = Companion.ReadMovement(cfg, player, out Boolean run);
            if (stick.sqrMagnitude < 0.04f)
            {
                Play("IDLE");
                return;
            }

            // Stick relativ zur Weltkarten-Kamera
            Camera cam = world.MainCamera;
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            fwd.y = 0f; right.y = 0f;
            fwd.Normalize(); right.Normalize();
            Vector3 dir = right * stick.x + fwd * stick.y;
            Single mag = Mathf.Min(1f, dir.magnitude);
            if (mag < 0.01f)
                return;
            dir /= dir.magnitude;

            Single tps = Configuration.Graphics.WorldTPS > 0 ? Configuration.Graphics.WorldTPS : 30;
            Vector3 step = dir * SpeedPerTick * tps * mag * Time.deltaTime;
            Vector3 next = pos + step;
            Int32 groundId;
            if (!TryGround(ref next, out groundId))
            {
                // an Küsten/Bergen entlanggleiten
                Vector3 alongX = new Vector3(next.x, pos.y, pos.z), alongZ = new Vector3(pos.x, pos.y, next.z);
                if (TryGround(ref alongX, out groundId)) next = alongX;
                else if (TryGround(ref alongZ, out groundId)) next = alongZ;
                else { next = pos; groundId = -1; }
            }
            if (groundId >= 0)
                next.y += Slice(leader, groundId, tps * Time.deltaTime, false);
            s_holder.transform.position = next;

            Single target = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
            s_yaw = Mathf.LerpAngle(s_yaw, target, 0.4f);
            s_holder.transform.rotation = Quaternion.Euler(0f, s_yaw, 0f);
            Play(mag > 0.6f ? "RUN" : "WALK");
        }

        /// <summary>
        /// Bodenhöhe per Spiel-Strahl (ohne Einsinken); false, wenn dort nicht zu Fuß begehbar (Wasser, Berge, …).
        /// </summary>
        private static Boolean TryGround(ref Vector3 p, out Int32 id)
        {
            Vector3 probe = p;
            id = 0;
            ff9.w_cellHit(ref probe, ref id, out Int32 pno, null, out Single height);
            if (pno < 0)
                return false;
            if (!ff9.w_movementCheckTopographID(ff9.w_moveCHRControl[0].limit, id))
                return false;
            p.y = height;
            return true;
        }

        private static Boolean TryGround(ref Vector3 p) => TryGround(ref p, out _);

        private static Single s_slice;

        /// <summary>
        /// Einsinken ins Gelände wie bei der echten Figur (ff9.w_movementSetheight): Im Wald versinkt die Figur
        /// zwischen den Bäumen (sofort), in flachem Wasser sinkt sie sanft ein (0,078 Einheiten pro Tick).
        /// Gleiche Tabelle (w_movementGetSliceHeight) und gleicher slice_type wie der Leader.
        /// </summary>
        private static Single Slice(WMActor leader, Int32 groundId, Single ticks, Boolean immediate)
        {
            Int32 sliceType = 1;
            Actor actor = leader.originalActor;
            if (actor != null && ff9.w_moveCHRStatus != null && actor.index < ff9.w_moveCHRStatus.Length)
                sliceType = ff9.w_moveCHRStatus[actor.index].slice_type;

            Single target = ff9.w_movementGetSliceHeight(sliceType, groundId, out Boolean inForest);
            if (immediate || inForest || Math.Abs(target) < 0.001f)
            {
                s_slice = target;
            }
            else
            {
                Single step = 0.078125f * Math.Max(1f, ticks);
                s_slice = Mathf.MoveTowards(s_slice, target, step);
            }
            return s_slice;
        }

        private static void Play(String suffix)
        {
            if (s_anim == null)
                return;
            String clip = Companion.AnimName(s_code, suffix);
            if (clip == s_clip && s_anim.isPlaying)
                return;
            if (s_anim.GetClip(clip) == null)
                return;
            s_anim.CrossFade(clip, 0.15f);
            s_clip = clip;
        }

        private static void NoCulling(GameObject go)
        {
            Bounds huge = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = false;
                smr.localBounds = huge;
            }
        }

        public static void Hide()
        {
            if (s_holder != null && s_holder.activeSelf)
                s_holder.SetActive(false);
        }

        private static void Destroy()
        {
            if (s_holder != null)
                UnityEngine.Object.Destroy(s_holder);
            s_holder = null;
            s_model = null;
            s_anim = null;
            s_code = null;
        }
    }
}
