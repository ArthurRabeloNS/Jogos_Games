using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace SteveCube
{
    // Rodada 2: hotbar simples, Ender Pearl e TNT.
    // Os itens do jogo (ingredientes) continuam sob controle do Overcooked; este componente
    // só controla a visibilidade do objeto segurado e registra seu slot visual.
    [BepInPlugin("com.seunome.oc2.steveround2", "Steve Cube - Round 2", "0.1.0")]
    public class SteveHotbar : BaseUnityPlugin
    {
        public static int SelectedSlot = 1; // 1 madeira | 2 TNT | 3 perola | 4 picareta | 5 obsidiana | 6 foguete | 7-9 itens do jogo
        static readonly Dictionary<string, int> IngredientSlots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        static readonly string[] FixedNames = { "Madeira", "TNT", "Ender Pearl", "Picareta", "Obsidiana", "Foguete" };
        static SteveHotbar _instance;
        string _lastHeld = "";
        float _throwCooldown;

        // HUD estilo Minecraft
        ConfigEntry<int> _hudScaleCfg;
        ConfigEntry<bool> _hudExtrasCfg, _hudNumbersCfg;
        int _xpPoints;                 // +1 por item pego (so visual)
        int _tipSlot = -1;
        string _tipName = "";
        float _tipAt = -10f;

        void Awake()
        {
            _instance = this;

            _zoomCfg = Config.Bind("Camera", "Zoom", DefaultZoom, "0 = camera original do jogo, 1 = bem perto. Tambem muda em jogo com as teclas abaixo.");
            _followCfg = Config.Bind("Camera", "SeguirSteve", true, "A camera centraliza no Steve (suave). Desligue para so aproximar sem seguir.");
            _zoomInKey = Config.Bind("Camera", "TeclaAproximar", KeyCode.Equals, "Segure para aproximar (o + do teclado numerico tambem funciona)");
            _zoomOutKey = Config.Bind("Camera", "TeclaAfastar", KeyCode.Minus, "Segure para afastar (o - do teclado numerico tambem funciona)");
            _resetKey = Config.Bind("Camera", "TeclaResetar", KeyCode.Alpha0, "Volta o zoom para o padrao");
            _followKey = Config.Bind("Camera", "TeclaSeguir", KeyCode.V, "Liga/desliga seguir o Steve");
            _hudScaleCfg = Config.Bind("HUD", "Escala", 0, "0 = automatica (inteira, nitida). 2..8 = fixa.");
            _hudExtrasCfg = Config.Bind("HUD", "MostrarVidaFomeXP", true, "Armadura, coracoes, fome e barra de XP acima da hotbar (so visual)");
            _hudNumbersCfg = Config.Bind("HUD", "MostrarNumeros", true, "Numero do slot (1-9) no canto de cada slot");
            _zoom = Mathf.Clamp01(_zoomCfg.Value);
            _follow = _followCfg.Value;
            Camera.onPreCull += CamPreCull;
            Camera.onPostRender += CamPostRender;

            Logger.LogInfo("Steve Cube Round 2 pronto: 1-9/roda = hotbar; T = Ender Pearl (slot 3); B = madeira(1) TNT(2) obsidiana(5) foguete(6); 7-9 = itens do jogo.");
        }

        void Update()
        {
            UpdateCamera();
            if (Time.timeScale <= 0f) return;
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SelectedSlot = i + 1;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f) SelectedSlot = SelectedSlot <= 1 ? 9 : SelectedSlot - 1;
            else if (wheel < -0.01f) SelectedSlot = SelectedSlot >= 9 ? 1 : SelectedSlot + 1;

            _throwCooldown -= Time.deltaTime;
            if (SelectedSlot == 3 && Input.GetKeyDown(KeyCode.T) && _throwCooldown <= 0f)
            {
                ThrowPearl();
                _throwCooldown = 0.6f;
            }

            UpdateHeldIngredient();

            string nowName = SlotName(SelectedSlot);
            if (SelectedSlot != _tipSlot || nowName != _tipName)
            {
                _tipSlot = SelectedSlot;
                _tipName = nowName;
                _tipAt = Time.unscaledTime;       // mostra o nome do item por ~2 s, como o Minecraft
            }
        }

        // =====================================================================
        //  CAMERA: zoom e seguir o Steve
        //  A camera do jogo continua mandando; so deslocamos a posicao dela logo antes de cada desenho
        //  (onPreCull) e devolvemos logo depois (onPostRender). Assim os scripts do jogo nunca veem a
        //  posicao alterada e nao brigam com a gente.
        // =====================================================================
        const float DefaultZoom = 0.5f;
        const float MinDistanceFactor = 0.3f;      // com zoom = 1, a camera fica a 30% da distancia original
        ConfigEntry<float> _zoomCfg;
        ConfigEntry<bool> _followCfg;
        ConfigEntry<KeyCode> _zoomInKey, _zoomOutKey, _resetKey, _followKey;
        float _zoom;
        bool _follow, _zoomDirty, _focusInit, _camLogged;
        float _camScan;
        Vector3 _focus;
        static Transform _camTarget;
        static int _worldMask;
        readonly Dictionary<Camera, Vector3> _camPos = new Dictionary<Camera, Vector3>();
        readonly Dictionary<Camera, float> _camOrtho = new Dictionary<Camera, float>();

        void UpdateCamera()
        {
            float dt = Time.unscaledDeltaTime;
            RestoreCameras();   // garantia: nada fica deslocado entre quadros

            _camScan -= dt;
            if (_camScan <= 0f || _camTarget == null)
            {
                _camScan = 0.5f;
                Transform p = FindPlayer();
                if (p != _camTarget) { _camTarget = p; _focusInit = false; }
                if (_camTarget != null)
                {
                    // so mexe nas cameras que desenham o mundo (chef/bancadas); UI e outras ficam intactas
                    int mask = 1 << _camTarget.gameObject.layer;
                    GameObject ws = GameObject.Find("Design/Work Surfaces");
                    if (ws != null) mask |= 1 << ws.layer;
                    _worldMask = mask;
                    if (!_camLogged)
                    {
                        _camLogged = true;   // uma vez: lista as cameras para facilitar o diagnostico
                        foreach (Camera cam in Camera.allCameras)
                            Logger.LogInfo("[camera] " + cam.name + " ortografica=" + cam.orthographic + " textura=" + (cam.targetTexture != null)
                                + " desenha-o-mundo=" + (((cam.cullingMask & mask) != 0) ? "SIM" : "nao"));
                    }
                }
            }
            if (_camTarget == null) return;

            Vector3 want = _camTarget.position + Vector3.up * 0.5f;
            if (!_focusInit) { _focus = want; _focusInit = true; }
            else _focus = Vector3.Lerp(_focus, want, 1f - Mathf.Exp(-8f * dt));

            float dir = 0f;
            if (Input.GetKey(_zoomInKey.Value) || Input.GetKey(KeyCode.KeypadPlus)) dir += 1f;
            if (Input.GetKey(_zoomOutKey.Value) || Input.GetKey(KeyCode.KeypadMinus)) dir -= 1f;
            if (dir != 0f)
            {
                _zoom = Mathf.Clamp01(_zoom + dir * 0.5f * dt);
                _zoomDirty = true;
            }
            else if (_zoomDirty)
            {
                _zoomDirty = false;
                _zoomCfg.Value = _zoom;     // grava no config quando solta a tecla
            }
            if (Input.GetKeyDown(_resetKey.Value))
            {
                _zoom = DefaultZoom;
                _zoomCfg.Value = _zoom;
            }
            if (Input.GetKeyDown(_followKey.Value))
            {
                _follow = !_follow;
                _followCfg.Value = _follow;
                Logger.LogInfo("Camera seguindo o Steve: " + (_follow ? "ligado" : "desligado"));
            }
        }

        void CamPreCull(Camera c)
        {
            if (_camTarget == null || c == null) return;
            if (!c.enabled || c.targetTexture != null || c.cameraType != CameraType.Game) return;
            if ((c.cullingMask & _worldMask) == 0) return;
            if (_zoom <= 0.001f && !_follow) return;
            if (_camPos.ContainsKey(c)) return;

            Transform t = c.transform;
            Vector3 origPos = t.position;
            Vector3 fwd = t.forward;
            float factor = Mathf.Lerp(1f, MinDistanceFactor, _zoom);

            if (c.orthographic)
            {
                _camOrtho[c] = c.orthographicSize;
                c.orthographicSize = c.orthographicSize * factor;
                if (_follow)
                {
                    Vector3 d = _focus - origPos;
                    t.position = origPos + (d - Vector3.Dot(d, fwd) * fwd);   // so desloca no plano da camera
                }
                _camPos[c] = origPos;
                return;
            }

            float dist = Vector3.Dot(_focus - origPos, fwd);     // distancia ate o Steve ao longo da visao
            if (dist < 1f) return;
            Vector3 dolly = origPos + fwd * (dist * (1f - factor));          // so aproxima
            Vector3 centered = _focus - fwd * (dist * factor);               // aproxima e centraliza no Steve
            t.position = _follow ? centered : dolly;
            _camPos[c] = origPos;
        }

        void CamPostRender(Camera c)
        {
            if (c == null) return;
            Vector3 p;
            if (_camPos.TryGetValue(c, out p))
            {
                c.transform.position = p;
                _camPos.Remove(c);
            }
            float o;
            if (_camOrtho.TryGetValue(c, out o))
            {
                c.orthographicSize = o;
                _camOrtho.Remove(c);
            }
        }

        void RestoreCameras()
        {
            if (_camPos.Count == 0 && _camOrtho.Count == 0) return;
            foreach (Camera c in new List<Camera>(_camPos.Keys))
                if (c != null) c.transform.position = _camPos[c];
            foreach (Camera c in new List<Camera>(_camOrtho.Keys))
                if (c != null) c.orthographicSize = _camOrtho[c];
            _camPos.Clear();
            _camOrtho.Clear();
        }

        void OnDestroy()
        {
            Camera.onPreCull -= CamPreCull;
            Camera.onPostRender -= CamPostRender;
            RestoreCameras();
        }

        void UpdateHeldIngredient()
        {
            Transform player = FindPlayer();
            if (player == null) return;
            SteveChef chef = player.GetComponent<SteveChef>();
            if (chef == null) return;

            string held = (chef.HeldName ?? "").Trim();
            if (held.Length > 0 && !String.Equals(_lastHeld, held, StringComparison.OrdinalIgnoreCase)) _xpPoints++;
            if (_lastHeld.Length > 0 && !String.Equals(_lastHeld, held, StringComparison.OrdinalIgnoreCase))
                IngredientSlots.Remove(_lastHeld); // ao entregar/cortar/soltar, libera o slot do ingrediente anterior
            if (held.Length > 0 && !IngredientSlots.ContainsKey(held))
            {
                int free = -1;
                for (int slot = 7; slot <= 9; slot++)
                {
                    bool used = false;
                    foreach (KeyValuePair<string, int> kv in IngredientSlots)
                        if (kv.Value == slot) { used = true; break; }
                    if (!used) { free = slot; break; }
                }
                if (free > 0)
                {
                    IngredientSlots[held] = free;
                    Logger.LogInfo("Ingrediente registrado na hotbar: " + held + " (slot " + free + ")");
                }
            }

            _lastHeld = held;
            bool showHeld = held.Length > 0 && IngredientSlots.ContainsKey(held) && SelectedSlot == IngredientSlots[held];
            chef.SetHeldItemVisible(showHeld);
        }

        public static int SlotForIngredient(string name)
        {
            if (String.IsNullOrEmpty(name)) return -1;
            int slot;
            return IngredientSlots.TryGetValue(name.Trim(), out slot) ? slot : -1;
        }

        static Transform FindPlayer()
        {
            GameObject chefs = GameObject.Find("Chefs");
            if (chefs == null) return null;
            Transform player = chefs.transform.Find("Player 1");
            if (player != null && player.gameObject.activeInHierarchy) return player;
            foreach (Transform child in chefs.transform)
                if (child.gameObject.activeInHierarchy) return child;
            return null;
        }

        static void ThrowPearl()
        {
            Transform player = FindPlayer();
            if (player == null) return;
            Vector3 direction = player.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            direction.Normalize();

            GameObject pearl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pearl.name = "Steve_EnderPearl";
            pearl.transform.position = player.position + Vector3.up * 0.8f + direction * 0.8f;
            pearl.transform.localScale = Vector3.one * 0.22f;
            Renderer renderer = pearl.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = SteveFx.LitMaterial();
                SteveFx.Tint(renderer, new Color(0.10f, 0.62f, 0.55f, 1f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // halo translucido em volta da perola (so visual, sem collider)
            GameObject halo = new GameObject("Halo");
            halo.transform.SetParent(pearl.transform, false);
            halo.transform.localScale = Vector3.one * 1.9f;
            halo.AddComponent<MeshFilter>().sharedMesh = SteveFx.SphereMesh();
            MeshRenderer haloR = halo.AddComponent<MeshRenderer>();
            haloR.sharedMaterial = SteveFx.FxMaterial();
            haloR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            haloR.receiveShadows = false;
            SteveFx.Tint(haloR, new Color(0.30f, 1f, 0.85f, 0.30f));

            Rigidbody body = pearl.AddComponent<Rigidbody>();
            body.mass = 0.15f;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.velocity = direction * 12f + Vector3.up * 3.5f;
            PearlProjectile projectile = pearl.AddComponent<PearlProjectile>();
            projectile.Owner = player;
            projectile.LaunchTime = Time.time;

            Collider[] playerCols = player.GetComponentsInChildren<Collider>(true);
            Collider pearlCol = pearl.GetComponent<Collider>();
            for (int i = 0; i < playerCols.Length; i++)
                if (playerCols[i] != null && pearlCol != null) Physics.IgnoreCollision(pearlCol, playerCols[i], true);
        }

        public static GameObject PlaceTnt(Vector3 position, int layer, float size)
        {
            // Raiz = so o collider (1x1x1 * size). O visual fica num filho, para o "inchaco" do pavio
            // nao empurrar o chef nem as bancadas vizinhas.
            GameObject tnt = new GameObject("SteveBlock_TNT");
            tnt.transform.position = position;
            tnt.transform.localScale = Vector3.one * size;
            tnt.layer = layer;
            tnt.AddComponent<BoxCollider>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(tnt.transform, false);
            visual.layer = layer;
            visual.AddComponent<MeshFilter>().sharedMesh = SteveTnt.GetMesh();
            MeshRenderer mr = visual.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Material mat = SteveTnt.NewMaterial();
            mr.sharedMaterial = mat;

            tnt.AddComponent<SteveBlockTag>();
            TntFuse fuse = tnt.AddComponent<TntFuse>();
            fuse.FuseSeconds = 4f;
            fuse.Radius = Mathf.Max(2f, size * 2.5f);
            fuse.Visual = visual.transform;
            fuse.Mat = mat;
            return tnt;
        }

        // Slot 6: foguete no chao
        public static GameObject PlaceRocket(Vector3 groundPos)
        {
            return SteveRocket.Create(groundPos);
        }

        static bool IsGameplayActive()
        {
            // A hotbar so aparece quando o Steve do Player 1 ja foi criado numa partida.
            // Isso evita desenhar a UI em menus, loading screens e telas de selecao.
            GameObject chefs = GameObject.Find("Chefs");
            if (chefs == null || !chefs.activeInHierarchy) return false;
            Transform player = chefs.transform.Find("Player 1");
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            if (player.GetComponent<SteveChef>() == null) return false;
            return Time.timeScale > 0f;
        }

        static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        // ------------------------------------------------------------------ HUD estilo Minecraft
        static string IngredientAt(int slot)
        {
            foreach (KeyValuePair<string, int> kv in IngredientSlots)
                if (kv.Value == slot) return kv.Key;
            return null;
        }

        int HudScale()
        {
            int s = _hudScaleCfg != null ? _hudScaleCfg.Value : 0;
            if (s <= 0) s = Mathf.RoundToInt(Screen.height / 270f);      // 1080p -> 4 (pixels inteiros = nitido)
            return Mathf.Clamp(s, 2, 8);
        }

        static void Ring(float x, float y, float w, float h, float t, Color c)
        {
            Fill(new Rect(x, y, w, t), c);
            Fill(new Rect(x, y + h - t, w, t), c);
            Fill(new Rect(x, y + t, t, h - 2f * t), c);
            Fill(new Rect(x + w - t, y + t, t, h - 2f * t), c);
        }

        static GUIStyle _hudLevelStyle, _hudNumStyle, _hudTipStyle;
        static GUIStyle HudStyle(ref GUIStyle st, TextAnchor anchor)
        {
            if (st == null)
            {
                st = new GUIStyle(GUI.skin.label);
                st.alignment = anchor;
                st.fontStyle = FontStyle.Bold;
                st.wordWrap = false;
                st.clipping = TextClipping.Overflow;
                st.padding = new RectOffset(0, 0, 0, 0);
                st.normal.textColor = Color.white;
            }
            return st;
        }

        static void ShadowLabel(Rect r, string text, GUIStyle st, Color c, float off)
        {
            GUI.color = new Color(0f, 0f, 0f, c.a);
            GUI.Label(new Rect(r.x + off, r.y + off, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
        }

        static void OutlineLabel(Rect r, string text, GUIStyle st, Color c, float off)
        {
            GUI.color = new Color(0f, 0f, 0f, c.a);
            GUI.Label(new Rect(r.x - off, r.y, r.width, r.height), text, st);
            GUI.Label(new Rect(r.x + off, r.y, r.width, r.height), text, st);
            GUI.Label(new Rect(r.x, r.y - off, r.width, r.height), text, st);
            GUI.Label(new Rect(r.x, r.y + off, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
        }

        void DrawSlot(float sx, float sy, int S, int slot)
        {
            // slot "afundado": sombra em cima/esquerda, brilho embaixo/direita, miolo cinza
            Fill(new Rect(sx + S, sy + S, 18f * S, 18f * S), new Color(0.215f, 0.215f, 0.215f, 1f));
            Fill(new Rect(sx + 2f * S, sy + 2f * S, 17f * S, 17f * S), Color.white);
            Fill(new Rect(sx + 2f * S, sy + 2f * S, 16f * S, 16f * S), new Color(0.545f, 0.545f, 0.545f, 1f));

            Rect ir = new Rect(sx + 2f * S, sy + 2f * S, 16f * S, 16f * S);
            if (slot == 1) HudArt.Draw(HudArt.Planks, ir, Color.white);
            else if (slot == 2) HudArt.Draw(HudArt.Tnt, ir, Color.white);
            else if (slot == 3) HudArt.Draw(HudArt.Pearl, ir, Color.white);
            else if (slot == 4) HudArt.Draw(HudArt.Pickaxe, ir, Color.white);
            else if (slot == 5) HudArt.Draw(HudArt.Obsidian, ir, Color.white);
            else if (slot == 6) HudArt.Draw(HudArt.Rocket, ir, Color.white);
            else
            {
                string ing = IngredientAt(slot);
                if (ing != null) HudArt.Draw(HudArt.CubeMold, ir, HudArt.IngredientColor(ing));
            }

            if (_hudNumbersCfg == null || _hudNumbersCfg.Value)
            {
                GUIStyle ns = HudStyle(ref _hudNumStyle, TextAnchor.UpperLeft);
                ns.fontSize = Mathf.Max(8, Mathf.RoundToInt(3.2f * S));
                ShadowLabel(new Rect(ir.x + 1.5f * S, ir.y + 0.5f * S, 8f * S, 8f * S), slot.ToString(), ns,
                    new Color(1f, 1f, 1f, 0.85f), Mathf.Max(1f, S * 0.4f));
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!IsGameplayActive()) return;
            if (!HudArt.Ensure()) return;

            int S = HudScale();
            float W = 182f * S, Hh = 22f * S;
            float left = Mathf.Round((Screen.width - W) * 0.5f);
            float T = Screen.height - Hh - 3f * S;
            Color old = GUI.color;

            // ---- hotbar: contorno preto + corpo cinza-claro + 9 slots ----
            Fill(new Rect(left, T, W, Hh), new Color(0.05f, 0.05f, 0.05f, 0.96f));
            Fill(new Rect(left + S, T + S, W - 2f * S, Hh - 2f * S), new Color(0.78f, 0.78f, 0.78f, 0.96f));
            for (int i = 1; i <= 9; i++)
                DrawSlot(left + (1 + 20 * (i - 1)) * S, T + S, S, i);

            // ---- moldura de selecao (branca, por cima dos vizinhos, como no Minecraft) ----
            int sel = Mathf.Clamp(SelectedSlot, 1, 9);
            float rx = left + (1 + 20 * (sel - 1)) * S, ry = T + S;
            Ring(rx - 4f * S, ry - 4f * S, 28f * S, 28f * S, S, Color.black);
            Ring(rx - 3f * S, ry - 3f * S, 26f * S, 26f * S, 2f * S, Color.white);
            Ring(rx - 1f * S, ry - 1f * S, 22f * S, 22f * S, S, Color.black);

            float stackTop = T;
            if (_hudExtrasCfg == null || _hudExtrasCfg.Value)
            {
                // ---- barra de XP segmentada + nivel ----
                float xpY = T - 7f * S;
                Fill(new Rect(left, xpY, W, 5f * S), new Color(0f, 0f, 0f, 0.92f));
                int level = _xpPoints / 10;
                int filled = Mathf.RoundToInt((_xpPoints % 10) / 10f * 20f);
                for (int k = 0; k < 20; k++)
                {
                    Color c = k < filled ? new Color(0.50f, 1f, 0.125f, 1f) : new Color(0.22f, 0.22f, 0.22f, 1f);
                    Fill(new Rect(left + (1 + 9 * k) * S, xpY + S, 8f * S, 3f * S), c);
                }
                GUIStyle ls = HudStyle(ref _hudLevelStyle, TextAnchor.MiddleCenter);
                ls.fontSize = 7 * S;
                OutlineLabel(new Rect(left + W * 0.5f - 30f * S, xpY - 9.5f * S, 60f * S, 10f * S), level.ToString(), ls,
                    new Color(0.50f, 1f, 0.125f, 1f), Mathf.Max(1f, S * 0.5f));

                // ---- armadura, coracoes (esquerda) e fome (direita; esvazia pela esquerda) ----
                float hy = xpY - 10f * S;
                for (int i = 0; i < 10; i++)
                {
                    float hx = left + i * 8f * S;
                    HudArt.Draw(HudArt.ArmorFull, new Rect(hx, hy - 10f * S, 9f * S, 9f * S), Color.white);
                    HudArt.Draw(HudArt.HeartFull, new Rect(hx, hy, 9f * S, 9f * S), Color.white);
                    HudArt.Draw(HudArt.FoodFull, new Rect(left + W - 9f * S - i * 8f * S, hy, 9f * S, 9f * S), Color.white);
                }
                stackTop = hy - 10f * S;
            }

            // ---- nome do item ao trocar de slot (some em ~2 s) ----
            float age = Time.unscaledTime - _tipAt;
            if (age < 2.2f && _tipName.Length > 0 && _tipName != "Vazio")
            {
                float a = Mathf.Clamp01((2.2f - age) / 0.6f);
                GUIStyle ts = HudStyle(ref _hudTipStyle, TextAnchor.MiddleCenter);
                ts.fontSize = Mathf.RoundToInt(5.5f * S);
                ShadowLabel(new Rect(left + W * 0.5f - 90f * S, stackTop - 13f * S, 180f * S, 10f * S), _tipName, ts,
                    new Color(1f, 1f, 1f, a), Mathf.Max(1f, S * 0.5f));
            }

            GUI.color = old;
        }

        static GUIStyle _centerStyle, _numberStyle;
        static GUIStyle CenterStyle()
        {
            if (_centerStyle == null)
            {
                _centerStyle = new GUIStyle(GUI.skin.label);
                _centerStyle.alignment = TextAnchor.MiddleCenter;
                _centerStyle.wordWrap = true;
                _centerStyle.fontSize = 11;
                _centerStyle.clipping = TextClipping.Clip;
                _centerStyle.padding = new RectOffset(0, 0, 0, 0);
            }
            return _centerStyle;
        }

        static GUIStyle NumberStyle()
        {
            if (_numberStyle == null)
            {
                _numberStyle = new GUIStyle(GUI.skin.label);
                _numberStyle.alignment = TextAnchor.UpperLeft;
                _numberStyle.fontStyle = FontStyle.Bold;
                _numberStyle.fontSize = 14;
                _numberStyle.wordWrap = false;
                _numberStyle.clipping = TextClipping.Overflow;
                _numberStyle.padding = new RectOffset(0, 0, 0, 0);
            }
            return _numberStyle;
        }

        static string ShortName(string name)
        {
            if (name == null) return "";
            return name.Length > 18 ? name.Substring(0, 17) + "." : name;
        }

        static string SlotName(int slot)
        {
            if (slot <= 6) return FixedNames[slot - 1];
            foreach (KeyValuePair<string, int> kv in IngredientSlots)
                if (kv.Value == slot) return kv.Key;
            return "Vazio";
        }
    }

    // =====================================================================
    //  ENDER PEARL
    // =====================================================================
    public class PearlProjectile : MonoBehaviour
    {
        public Transform Owner;
        public float LaunchTime;
        bool _done;
        float _trail;

        void Update()
        {
            if (_done) return;
            if (Time.time - LaunchTime > 8f || transform.position.y < -30f)
            {
                Destroy(gameObject);
                return;
            }
            _trail -= Time.deltaTime;
            if (_trail <= 0f)
            {
                _trail = 0.035f;
                SteveFx.PearlTrail(transform.position);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_done || Owner == null) return;
            _done = true;

            ContactPoint[] contacts = collision != null ? collision.contacts : new ContactPoint[0];
            Vector3 impact = transform.position;
            Vector3 normal = Vector3.up;
            if (contacts.Length > 0)
            {
                impact = contacts[0].point;
                normal = contacts[0].normal;
            }

            // Nunca teleporta para o ponto de impacto (pode ser parede, topo de bancada ou fora do mapa).
            // Procura, em aneis ao redor do impacto, um ponto de piso valido: dentro dos limites do mapa,
            // na altura do piso onde o chef esta, com apoio ao redor, sem nada no volume do chef.
            Vector3 destination;
            if (!PearlLanding.TryFind(Owner, transform, impact, normal, out destination))
            {
                SteveFx.Log("Ender Pearl cancelada: nao ha piso seguro perto do impacto.");
                SteveFx.TeleportBurst(impact, true);
                Destroy(gameObject);
                return;
            }

            Vector3 from = Owner.position;
            PearlLanding.Teleport(Owner, destination);
            SteveFx.TeleportBurst(from + Vector3.up * 0.6f, false);
            SteveFx.TeleportBurst(destination + Vector3.up * 0.6f, false);
            SteveFx.Log("Ender Pearl: teleportado " + Vector3.Distance(from, destination).ToString("0.0") + " m.");
            Destroy(gameObject);
        }
    }

    static class PearlLanding
    {
        const float ChefRadius = 0.35f;       // folga lateral exigida no destino
        const float ChefHeight = 1.25f;       // altura do volume que precisa estar livre
        const float GroundTolerance = 0.30f;  // o piso do destino tem que estar na altura do piso do chef
        static readonly float[] Rings = { 0f, 0.45f, 0.9f, 1.35f, 1.8f, 2.2f };
        static MethodInfo _sync;
        static bool _syncLooked;

        // ignora gatilhos, o proprio chef (e o que ele segura) e a propria perola
        static bool Ignored(Collider c, Transform owner, Transform pearl)
        {
            if (c == null || c.isTrigger) return true;
            Transform t = c.transform;
            if (owner != null && t.IsChildOf(owner)) return true;
            if (pearl != null && t.IsChildOf(pearl)) return true;
            return false;
        }

        static bool NearestHit(Vector3 origin, Vector3 dir, float dist, Transform owner, Transform pearl, out RaycastHit best)
        {
            best = default(RaycastHit);
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            float bestD = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (Ignored(hits[i].collider, owner, pearl)) continue;
                if (hits[i].distance < bestD) { bestD = hits[i].distance; best = hits[i]; found = true; }
            }
            return found;
        }

        // piso plano, na altura do piso do chef, visto de cima (raio nasce acima de bancadas/blocos,
        // entao topo de bancada, de bloco ou de TNT e rejeitado pela diferenca de altura)
        static bool FloorAt(float x, float z, float groundY, Transform owner, Transform pearl, out RaycastHit hit)
        {
            if (!NearestHit(new Vector3(x, groundY + 1.8f, z), Vector3.down, 3.6f, owner, pearl, out hit)) return false;
            if (hit.normal.y < 0.9f) return false;
            return Mathf.Abs(hit.point.y - groundY) <= GroundTolerance;
        }

        static bool Validate(Vector3 xz, float groundY, Vector3 origin, bool checkLos, bool hasBounds, Bounds mb,
                             Transform owner, Transform pearl, out Vector3 floorPoint)
        {
            floorPoint = Vector3.zero;

            // 1) dentro dos limites do mapa (bancadas + estacoes), com folga do tamanho do chef
            if (hasBounds)
            {
                if (xz.x < mb.min.x + ChefRadius || xz.x > mb.max.x - ChefRadius) return false;
                if (xz.z < mb.min.z + ChefRadius || xz.z > mb.max.z - ChefRadius) return false;
            }

            // 2) piso no ponto
            RaycastHit floor;
            if (!FloorAt(xz.x, xz.z, groundY, owner, pearl, out floor)) return false;

            // 3) apoio ao redor: rejeita bordas, buracos e frestas
            const float m = 0.3f;
            float[] dx = { m, -m, 0f, 0f };
            float[] dz = { 0f, 0f, m, -m };
            for (int i = 0; i < 4; i++)
            {
                RaycastHit side;
                if (!FloorAt(xz.x + dx[i], xz.z + dz[i], groundY, owner, pearl, out side)) return false;
                if (Mathf.Abs(side.point.y - floor.point.y) > 0.2f) return false;
            }

            // 4) volume do chef livre (inclui outros chefs, blocos, TNT, itens no chao)
            Vector3 p0 = floor.point + Vector3.up * (ChefRadius + 0.1f);
            Vector3 p1 = floor.point + Vector3.up * (ChefHeight - ChefRadius);
            Collider[] cs = Physics.OverlapCapsule(p0, p1, ChefRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cs.Length; i++)
                if (!Ignored(cs[i], owner, pearl)) return false;

            // 5) sem parede/bancada entre o impacto e o destino (nao atravessa paredes finas)
            if (checkLos)
            {
                Vector3 a = new Vector3(origin.x, groundY + 0.9f, origin.z);
                Vector3 b = new Vector3(xz.x, groundY + 0.9f, xz.z);
                Vector3 d = b - a;
                float len = d.magnitude;
                if (len > 0.01f)
                {
                    RaycastHit block;
                    if (NearestHit(a, d / len, len, owner, pearl, out block)) return false;
                }
            }

            floorPoint = floor.point;
            return true;
        }

        public static bool TryFind(Transform owner, Transform pearl, Vector3 impact, Vector3 normal, out Vector3 destination)
        {
            destination = Vector3.zero;

            // referencia de piso: onde o chef esta agora. Mede tambem a distancia pivo->piso,
            // assim nao dependemos de o pivo estar nos pes ou no meio do corpo.
            float groundY, pivot;
            RaycastHit g;
            if (NearestHit(owner.position + Vector3.up * 0.6f, Vector3.down, 4f, owner, pearl, out g))
            {
                groundY = g.point.y;
                pivot = Mathf.Clamp(owner.position.y - groundY, 0f, 2f);
            }
            else
            {
                pivot = 0.75f;
                groundY = owner.position.y - pivot;
            }

            Bounds mb;
            bool hasBounds = SteveBlocks.TryGetMapBounds(out mb);

            // empurra o ponto de partida para fora da superficie atingida
            Vector3 flat = new Vector3(normal.x, 0f, normal.z);
            Vector3 origin = impact;
            if (flat.sqrMagnitude > 0.01f) origin += flat.normalized * 0.35f;

            for (int r = 0; r < Rings.Length; r++)
            {
                float radius = Rings[r];
                int count = radius <= 0f ? 1 : (radius <= 0.95f ? 8 : 12);
                bool found = false;
                Vector3 bestFloor = Vector3.zero;
                float bestDist = float.MaxValue;

                for (int a = 0; a < count; a++)
                {
                    float ang = a * (Mathf.PI * 2f / count) + r * 0.7f;
                    Vector3 xz = new Vector3(origin.x + Mathf.Cos(ang) * radius, 0f, origin.z + Mathf.Sin(ang) * radius);
                    Vector3 floorPoint;
                    if (!Validate(xz, groundY, origin, radius > 0f, hasBounds, mb, owner, pearl, out floorPoint)) continue;

                    // dentro do mesmo anel, prefere o lado de onde o chef lancou
                    Vector3 dd = floorPoint - owner.position;
                    dd.y = 0f;
                    float d = dd.sqrMagnitude;
                    if (d < bestDist) { bestDist = d; bestFloor = floorPoint; found = true; }
                }

                if (found)
                {
                    destination = bestFloor + Vector3.up * (pivot + 0.03f);
                    return true;
                }
            }
            return false;
        }

        public static void Teleport(Transform owner, Vector3 dest)
        {
            CharacterController cc = owner.GetComponent<CharacterController>();
            bool ccWasOn = cc != null && cc.enabled;
            if (ccWasOn) cc.enabled = false;

            List<Rigidbody> bodies = new List<Rigidbody>();
            Rigidbody up = owner.GetComponentInParent<Rigidbody>();
            if (up != null) bodies.Add(up);
            Rigidbody[] down = owner.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < down.Length; i++)
                if (down[i] != null && !bodies.Contains(down[i])) bodies.Add(down[i]);

            owner.position = dest;
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody b = bodies[i];
                if (b == null || b.isKinematic) continue;
                if (b.transform == owner) b.position = dest;
                b.velocity = Vector3.zero;
                b.angularVelocity = Vector3.zero;
            }
            if (ccWasOn) cc.enabled = true;
            SyncTransforms();

            // Reaplica nos proximos passos de fisica (o jogo pode escrever a posicao antiga logo depois)
            TeleportSettle settle = owner.GetComponent<TeleportSettle>();
            if (settle == null) settle = owner.gameObject.AddComponent<TeleportSettle>();
            settle.Begin(dest, bodies.ToArray());
        }

        static void SyncTransforms()
        {
            if (!_syncLooked)
            {
                _syncLooked = true;
                _sync = typeof(Physics).GetMethod("SyncTransforms", BindingFlags.Public | BindingFlags.Static);
            }
            if (_sync != null) _sync.Invoke(null, null);
        }
    }

    public class TeleportSettle : MonoBehaviour
    {
        Vector3 _dest;
        Rigidbody[] _bodies;
        int _fixedLeft;
        float _age;

        public void Begin(Vector3 dest, Rigidbody[] bodies)
        {
            _dest = dest;
            _bodies = bodies;
            _fixedLeft = 3;
            _age = 0f;
        }

        void FixedUpdate()
        {
            if (_fixedLeft <= 0) return;
            _fixedLeft--;
            transform.position = _dest;
            if (_bodies == null) return;
            for (int i = 0; i < _bodies.Length; i++)
            {
                Rigidbody b = _bodies[i];
                if (b == null || b.isKinematic) continue;
                if (b.transform == transform) b.position = _dest;
                b.velocity = Vector3.zero;
                b.angularVelocity = Vector3.zero;
            }
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age < 0.2f) return;
            float d = Vector3.Distance(transform.position, _dest);
            if (d > 1.5f)
                SteveFx.Log("AVISO: o jogo devolveu o chef para longe do destino do teleporte (" + d.ToString("0.0")
                    + " m). Ligue Debug/GerarDump no config e envie o chef_dump2.txt para achar o componente que controla a posicao.");
            Destroy(this);
        }
    }

    // =====================================================================
    //  TNT
    // =====================================================================
    public class TntFuse : MonoBehaviour
    {
        public float FuseSeconds = 4f;
        public float Radius = 2f;
        public Transform Visual;
        public Material Mat;

        float _elapsed, _flashClock, _spark, _smoke;
        bool _flashOn, _detonated;
        Texture2D _normalTex, _flashTex;

        void Start()
        {
            _normalTex = SteveTnt.GetNormalTex();
            _flashTex = SteveTnt.GetFlashTex();
        }

        // usado pela reacao em cadeia: faz outra TNT explodir logo
        public void Ignite(float remainingSeconds)
        {
            if (_detonated) return;
            _elapsed = Mathf.Max(_elapsed, FuseSeconds - Mathf.Max(0.05f, remainingSeconds));
        }

        void Update()
        {
            if (_detonated) return;
            float dt = Time.deltaTime;
            _elapsed += dt;
            float t = Mathf.Clamp01(_elapsed / FuseSeconds);

            // pisca em branco, cada vez mais rapido (como a TNT acesa do Minecraft)
            _flashClock += dt * Mathf.Lerp(2.5f, 15f, t * t);
            bool flash = (Mathf.FloorToInt(_flashClock) % 2) == 1;
            if (flash != _flashOn && Mat != null)
            {
                _flashOn = flash;
                Mat.mainTexture = flash ? _flashTex : _normalTex;
            }

            // incha de leve perto do fim (so o visual; o collider continua do mesmo tamanho)
            float s = 1f + 0.12f * t * t + 0.025f * Mathf.Sin(_elapsed * 20f) * t;
            if (Visual != null) Visual.localScale = Vector3.one * s;

            // faiscas e fumaca saindo do topo
            Vector3 top = transform.position + Vector3.up * (transform.lossyScale.y * 0.5f * s + 0.02f);
            _spark -= dt;
            if (_spark <= 0f) { _spark = 0.05f; SteveFx.FuseSpark(top); }
            _smoke -= dt;
            if (_smoke <= 0f) { _smoke = 0.2f; SteveFx.FuseSmoke(top); }

            if (_elapsed >= FuseSeconds) Detonate();
        }

        void OnDestroy()
        {
            if (Mat != null) Destroy(Mat);
        }

        void Detonate()
        {
            _detonated = true;
            Vector3 center = transform.position;
            Collider[] hits = Physics.OverlapSphere(center, Radius);
            HashSet<GameObject> seen = new HashSet<GameObject>();
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;
                SteveBlockTag tag = hits[i].GetComponentInParent<SteveBlockTag>();
                if (tag == null) continue;
                GameObject go = tag.gameObject;
                if (go == gameObject || !seen.Add(go)) continue;
                if (go.GetComponent<SteveObsidianTag>() != null) continue;   // obsidiana resiste a explosao

                TntFuse other = go.GetComponent<TntFuse>();
                if (other != null)
                {
                    // reacao em cadeia: a vizinha explode logo em seguida (mais perto = mais rapido)
                    float f = Mathf.Clamp01(Vector3.Distance(center, go.transform.position) / Mathf.Max(0.1f, Radius));
                    other.Ignite(Mathf.Lerp(0.12f, 0.4f, f));
                    continue;
                }

                SteveFx.Chips(go.transform.position, new Color(0.72f, 0.57f, 0.34f, 1f), new Color(0.43f, 0.32f, 0.17f, 1f), 8, 0.14f);
                Destroy(go);
            }

            SteveBlocks.DetonateWorkSurfaces(center, Radius);
            SteveFx.Explosion(center, Radius);
            Destroy(gameObject);
        }
    }

    // Textura (pixel art 16x16 por face) e malha da TNT, geradas por codigo.
    // Atlas 64x16: [lado claro][lado escuro][topo][fundo]. O "escuro" existe porque alguns shaders sem
    // iluminacao deixariam todas as faces com o mesmo brilho.
    static class SteveTnt
    {
        static Texture2D _normal, _flash;
        static Mesh _mesh;

        static readonly string[] LetterT = { "111", "010", "010", "010", "010" };
        static readonly string[] LetterN = { "1001", "1101", "1011", "1001", "1001" };

        public static Texture2D GetNormalTex()
        {
            if (_normal == null) _normal = BuildAtlas(false);
            return _normal;
        }

        public static Texture2D GetFlashTex()
        {
            if (_flash == null) _flash = BuildAtlas(true);
            return _flash;
        }

        public static Material NewMaterial()
        {
            Shader sh = FindTextureShader();
            Material m = sh != null ? new Material(sh) : SteveFx.FallbackMaterial();
            m.mainTexture = GetNormalTex();
            return m;
        }

        static Shader FindTextureShader()
        {
            // 1) Unlit/Texture: cores exatas, sem depender de luz
            string[] first = { "Unlit/Texture", "Mobile/Unlit (Supports Lightmap)" };
            for (int i = 0; i < first.Length; i++)
            {
                Shader s = Shader.Find(first[i]);
                if (s != null && s.isSupported) return s;
            }
            // 2) o mesmo shader que o Steve ja esta usando (sabidamente existe na build e aceita textura)
            try
            {
                Material steve = SteveAssets.GetMaterial(null);
                if (steve != null && steve.shader != null) return steve.shader;
            }
            catch { }
            // 3) shaders classicos
            string[] rest = { "Legacy Shaders/Diffuse", "Diffuse", "Mobile/Diffuse", "Sprites/Default" };
            for (int i = 0; i < rest.Length; i++)
            {
                Shader s = Shader.Find(rest[i]);
                if (s != null && s.isSupported) return s;
            }
            return null;
        }

        static float H(int x, int y, int seed)
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 255) / 255f;
        }

        static bool IsLetter(int x, int y)
        {
            int row = y - 5;
            if (row < 0 || row > 4) return false;
            if (x >= 2 && x <= 4) return LetterT[row][x - 2] == '1';
            if (x >= 6 && x <= 9) return LetterN[row][x - 6] == '1';
            if (x >= 11 && x <= 13) return LetterT[row][x - 11] == '1';
            return false;
        }

        static Color Shade(Color c, float bright, bool flash)
        {
            float b = flash ? Mathf.Lerp(bright, 1f, 0.5f) : bright;
            return new Color(Mathf.Clamp01(c.r * b), Mathf.Clamp01(c.g * b), Mathf.Clamp01(c.b * b), 1f);
        }

        static void PaintSide(Color[] px, int tile, float bright, bool flash)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float n = (H(x, y, 3) - 0.5f) * 0.07f;
                    Color c;
                    bool letter = IsLetter(x, y);
                    if (y >= 4 && y <= 10)
                    {
                        // faixa branca com "TNT"
                        c = letter ? new Color(0.13f, 0.11f, 0.12f) : new Color(0.93f + n, 0.91f + n, 0.87f + n);
                    }
                    else if (y == 3 || y == 11)
                    {
                        c = new Color(0.42f, 0.05f, 0.04f);              // linha escura em volta da faixa
                    }
                    else
                    {
                        // bastoes de dinamite: vermelho com divisoes verticais escuras
                        c = (x % 4 == 3) ? new Color(0.50f + n, 0.06f, 0.05f) : new Color(0.82f + n, 0.14f + n * 0.5f, 0.11f);
                        if (y == 0 || y == 15) c = new Color(c.r * 0.68f, c.g * 0.68f, c.b * 0.68f);   // cordas de cima/baixo
                        else if (y == 1 || y == 14) c = new Color(c.r * 0.85f, c.g * 0.85f, c.b * 0.85f);
                    }
                    if (flash) c = Color.Lerp(c, Color.white, letter ? 0.55f : 0.78f);
                    px[(15 - y) * 64 + tile * 16 + x] = Shade(c, bright, flash);
                }
            }
        }

        static void PaintTop(Color[] px, int tile, float bright, bool flash)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float n = (H(x, y, 9) - 0.5f) * 0.08f;
                    int ring = Mathf.Min(Mathf.Min(x, 15 - x), Mathf.Min(y, 15 - y));   // 0 = borda externa
                    float ax = Mathf.Abs(x - 7.5f), ay = Mathf.Abs(y - 7.5f);
                    Color c;
                    if (ring <= 1)
                    {
                        // moldura de bastoes vermelhos, igual as laterais
                        c = ring == 0 ? new Color(0.55f + n, 0.07f, 0.05f) : new Color(0.84f + n, 0.16f, 0.11f);
                    }
                    else
                    {
                        c = new Color(0.60f + n, 0.60f + n, 0.62f + n);                     // placa cinza
                        if (Mathf.Abs(ax - ay) < 0.9f) c = new Color(0.16f, 0.16f, 0.18f);   // diagonais escuras saindo do centro
                        else if ((ax < 1.6f && ay > 2.6f) || (ay < 1.6f && ax > 2.6f))
                            c = new Color(0.86f + n, 0.36f + n, 0.14f);                      // detalhes laranja-avermelhados
                        if (ax < 1.1f && ay < 1.1f) c = new Color(0.05f, 0.05f, 0.06f);      // centro preto (pavio)
                    }
                    if (flash) c = Color.Lerp(c, Color.white, 0.78f);
                    px[(15 - y) * 64 + tile * 16 + x] = Shade(c, bright, flash);
                }
            }
        }

        static Texture2D BuildAtlas(bool flash)
        {
            Color[] px = new Color[64 * 16];
            PaintSide(px, 0, 1.00f, flash);
            PaintSide(px, 1, 0.80f, flash);
            PaintTop(px, 2, 1.00f, flash);
            PaintTop(px, 3, 0.62f, flash);
            Texture2D t = new Texture2D(64, 16, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        // cubo unitario (-0.5..0.5) com um "tile" do atlas por face
        static readonly int[] FRONT  = { +1,-1,+1,  +1,+1,+1,  -1,+1,+1,  -1,-1,+1 };
        static readonly int[] BACK   = { -1,-1,-1,  -1,+1,-1,  +1,+1,-1,  +1,-1,-1 };
        static readonly int[] RIGHT  = { +1,-1,-1,  +1,+1,-1,  +1,+1,+1,  +1,-1,+1 };
        static readonly int[] LEFT   = { -1,-1,+1,  -1,+1,+1,  -1,+1,-1,  -1,-1,-1 };
        static readonly int[] TOP    = { +1,+1,+1,  +1,+1,-1,  -1,+1,-1,  -1,+1,+1 };
        static readonly int[] BOTTOM = { +1,-1,-1,  +1,-1,+1,  -1,-1,+1,  -1,-1,-1 };

        static void Face(List<Vector3> vs, List<Vector2> uv, List<int> tr, int[] s, int tile)
        {
            int i = vs.Count;
            for (int k = 0; k < 4; k++)
                vs.Add(new Vector3(s[k * 3] * 0.5f, s[k * 3 + 1] * 0.5f, s[k * 3 + 2] * 0.5f));
            float u0 = tile * 0.25f + 0.002f, u1 = tile * 0.25f + 0.248f;
            uv.Add(new Vector2(u0, 0f));      // BL
            uv.Add(new Vector2(u0, 1f));      // TL
            uv.Add(new Vector2(u1, 1f));      // TR
            uv.Add(new Vector2(u1, 0f));      // BR
            tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
            tr.Add(i); tr.Add(i + 2); tr.Add(i + 3);
        }

        public static Mesh GetMesh()
        {
            if (_mesh != null) return _mesh;
            List<Vector3> vs = new List<Vector3>(24);
            List<Vector2> uv = new List<Vector2>(24);
            List<int> tr = new List<int>(36);
            Face(vs, uv, tr, FRONT, 0);
            Face(vs, uv, tr, BACK, 0);
            Face(vs, uv, tr, RIGHT, 1);
            Face(vs, uv, tr, LEFT, 1);
            Face(vs, uv, tr, TOP, 2);
            Face(vs, uv, tr, BOTTOM, 3);
            Mesh m = new Mesh();
            m.name = "SteveTntCube";
            m.SetVertices(vs);
            m.SetUVs(0, uv);
            m.SetTriangles(tr, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;
            _mesh = m;
            return _mesh;
        }
    }

    // =====================================================================
    //  EFEITOS (particulas feitas de cubos/esferas, sem collider)
    // =====================================================================
    // =====================================================================
    //  FOGUETE: fica no chao com o pavio aceso, decola e estoura em fogos coloridos
    // =====================================================================
    public class RocketFlight : MonoBehaviour
    {
        public Color Tint = Color.red;
        public float Delay = 0.9f;
        public float Height = 6f;
        float _t, _spark, _speed, _startY;
        bool _flying;

        void Start()
        {
            _startY = transform.position.y;
            Height = Random.Range(5f, 7.5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            _spark -= dt;
            Vector3 tail = transform.position + Vector3.up * 0.05f;

            if (!_flying)
            {
                // pavio aceso: solta faiscas e treme de leve
                if (_spark <= 0f) { _spark = 0.04f; SteveFx.RocketSpark(tail); }
                transform.localRotation = Quaternion.Euler(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                if (_t >= Delay) { _flying = true; _speed = 3f; transform.localRotation = Quaternion.identity; }
                return;
            }

            _speed = Mathf.Min(13f, _speed + 18f * dt);
            transform.position += Vector3.up * _speed * dt;
            if (_spark <= 0f) { _spark = 0.02f; SteveFx.RocketSpark(tail); }

            if (transform.position.y - _startY >= Height)
            {
                SteveFx.FireworkBurst(transform.position + Vector3.up * 0.3f, Tint);
                Destroy(gameObject);
            }
        }
    }

    static class SteveRocket
    {
        static readonly Color[] Palette =
        {
            new Color(0.86f, 0.20f, 0.17f, 1f), new Color(1f, 0.60f, 0.10f, 1f), new Color(1f, 0.90f, 0.25f, 1f),
            new Color(0.25f, 0.85f, 0.30f, 1f), new Color(0.20f, 0.85f, 0.90f, 1f), new Color(0.30f, 0.45f, 1f, 1f),
            new Color(0.70f, 0.35f, 1f, 1f), new Color(1f, 0.40f, 0.75f, 1f)
        };

        static void Part(Transform parent, Vector3 pos, Vector3 scale, Color c)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider col = g.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);                    // so visual, nao empurra ninguem
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = SteveFx.LitMaterial();
            SteveFx.Tint(r, c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static GameObject Create(Vector3 ground)
        {
            Color tint = Palette[Random.Range(0, Palette.Length)];
            GameObject root = new GameObject("Steve_Rocket");
            root.transform.position = ground;
            Part(root.transform, new Vector3(0f, 0.34f, 0f), new Vector3(0.14f, 0.44f, 0.14f), new Color(0.95f, 0.93f, 0.88f, 1f)); // tubo
            Part(root.transform, new Vector3(0f, 0.26f, 0f), new Vector3(0.155f, 0.10f, 0.155f), tint);                              // faixa
            Part(root.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.10f, 0.12f, 0.10f), tint);                                // ponta
            Part(root.transform, new Vector3(0f, 0.70f, 0f), new Vector3(0.05f, 0.06f, 0.05f), tint);                                // bico
            Part(root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.035f, 0.36f, 0.035f), new Color(0.55f, 0.38f, 0.18f, 1f)); // vareta
            RocketFlight f = root.AddComponent<RocketFlight>();
            f.Tint = tint;
            return root;
        }
    }

    public class FxPiece : MonoBehaviour
    {
        public Vector3 Vel;
        public Vector3 Spin;
        public Vector3 Stretch = Vector3.one;
        public float Gravity = -10f, Drag, Life = 0.6f, Age;
        public float StartScale = 0.2f, EndScale = 0.05f;
        public Color C0 = Color.white, C1 = Color.white;
        public bool Fade, EaseOut;
        Renderer _r;
        MaterialPropertyBlock _mpb;

        public void Refresh()
        {
            if (_r == null) _r = GetComponent<Renderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            float k = Mathf.Clamp01(Age / Mathf.Max(0.01f, Life));
            Color c = Color.Lerp(C0, C1, k);
            if (Fade) c.a = Mathf.Lerp(C0.a, 0f, k);
            _mpb.SetColor("_Color", c);
            if (_r != null) _r.SetPropertyBlock(_mpb);
            float e = EaseOut ? 1f - (1f - k) * (1f - k) : k;
            transform.localScale = Vector3.Scale(Stretch, Vector3.one * Mathf.Lerp(StartScale, EndScale, e));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Age += dt;
            if (Age >= Life) { Destroy(gameObject); return; }
            Vel.y += Gravity * dt;
            if (Drag > 0f) Vel *= Mathf.Max(0f, 1f - Drag * dt);
            transform.position += Vel * dt;
            if (Spin.sqrMagnitude > 0f) transform.Rotate(Spin * dt, Space.Self);
            Refresh();
        }
    }

    static class SteveFx
    {
        static Mesh _cube, _sphere;
        static Material _lit, _fx;

        public static void Log(string msg)
        {
            if (Plugin.Log != null) Plugin.Log.LogInfo(msg);
            else Debug.Log(msg);
        }

        // ---------- malhas e materiais (com fallback, nunca new Material(null)) ----------
        static Mesh Prim(PrimitiveType type)
        {
            GameObject tmp = GameObject.CreatePrimitive(type);
            Mesh m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return m;
        }

        public static Mesh CubeMesh() { if (_cube == null) _cube = Prim(PrimitiveType.Cube); return _cube; }
        public static Mesh SphereMesh() { if (_sphere == null) _sphere = Prim(PrimitiveType.Sphere); return _sphere; }

        public static Material FallbackMaterial()
        {
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(tmp);
            return m;
        }

        static Shader FindWithColor(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Shader s = Shader.Find(names[i]);
                if (s == null || !s.isSupported) continue;
                Material probe = new Material(s);
                bool ok = probe.HasProperty("_Color");
                Object.DestroyImmediate(probe);
                if (ok) return s;
            }
            return null;
        }

        // material com iluminacao, para objetos solidos (perola, estilhacos)
        public static Material LitMaterial()
        {
            if (_lit != null) return _lit;
            Shader s = FindWithColor(new string[] { "Legacy Shaders/Diffuse", "Diffuse", "Mobile/Diffuse", "Unlit/Color", "Sprites/Default" });
            _lit = s != null ? new Material(s) : FallbackMaterial();
            _lit.hideFlags = HideFlags.HideAndDontSave;
            return _lit;
        }

        // material sem luz e com transparencia quando possivel, para fogo/fumaca/flash
        public static Material FxMaterial()
        {
            if (_fx != null) return _fx;
            Shader s = FindWithColor(new string[] { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Unlit/Color", "Legacy Shaders/Diffuse", "Diffuse" });
            _fx = s != null ? new Material(s) : FallbackMaterial();
            _fx.hideFlags = HideFlags.HideAndDontSave;
            return _fx;
        }

        public static void Tint(Renderer r, Color c)
        {
            MaterialPropertyBlock b = new MaterialPropertyBlock();
            b.SetColor("_Color", c);
            r.SetPropertyBlock(b);
        }

        static FxPiece Spawn(Mesh mesh, Material mat, Vector3 pos, float s0, float s1, Color c0, Color c1,
                             Vector3 vel, float gravity, float drag, float life, bool fade)
        {
            GameObject go = new GameObject("Steve_Fx");
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            FxPiece p = go.AddComponent<FxPiece>();
            p.StartScale = s0; p.EndScale = s1;
            p.C0 = c0; p.C1 = c1;
            p.Vel = vel; p.Gravity = gravity; p.Drag = drag;
            p.Life = life; p.Fade = fade;
            p.Refresh();
            return p;
        }

        static Vector3 Rand(float mag)
        {
            return new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * mag;
        }

        static void Orb(Vector3 pos, float s0, float s1, Color c0, Color c1, float life, Vector3 stretch)
        {
            FxPiece p = Spawn(SphereMesh(), FxMaterial(), pos, s0, s1, c0, c1, Vector3.zero, 0f, 0f, life, true);
            p.Stretch = stretch;
            p.EaseOut = true;
            p.Refresh();
        }

        // ---------- explosao ----------
        public static void Explosion(Vector3 c, float r)
        {
            // clarao branco-amarelo, bola de fogo laranja e onda de choque no chao
            Orb(c, 0.4f, r * 1.5f, new Color(1f, 1f, 0.9f, 0.95f), new Color(1f, 0.95f, 0.5f, 0.95f), 0.16f, Vector3.one);
            Orb(c, 0.3f, r * 1.9f, new Color(1f, 0.75f, 0.2f, 0.85f), new Color(0.9f, 0.25f, 0.05f, 0.85f), 0.38f, Vector3.one);
            Orb(new Vector3(c.x, c.y - 0.42f, c.z), 0.4f, r * 2.3f, new Color(1f, 0.95f, 0.8f, 0.5f), new Color(1f, 0.7f, 0.3f, 0.5f), 0.42f, new Vector3(1f, 0.05f, 1f));

            float k = Mathf.Max(0.6f, r / 2.5f);

            // fogo: cubinhos amarelo -> vermelho escuro, subindo
            for (int i = 0; i < 28; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.1f;
                Color a = Color.Lerp(new Color(1f, 0.92f, 0.35f, 1f), new Color(1f, 0.55f, 0.1f, 1f), Random.value);
                Spawn(CubeMesh(), FxMaterial(), c + dir * Random.Range(0f, 0.4f) * k, Random.Range(0.15f, 0.34f), 0.05f,
                      a, new Color(0.5f, 0.07f, 0.03f, 1f), dir * Random.Range(2f, 6.5f) * k, 1.5f, 2.5f,
                      Random.Range(0.45f, 0.85f), true).Spin = Rand(300f);
            }

            // fumaca: cubos cinza que crescem e somem
            for (int i = 0; i < 16; i++)
            {
                Vector3 vel = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(0.8f, 2.4f), Random.Range(-1.8f, 1.8f)) * k;
                float g = Random.Range(0.28f, 0.45f);
                Spawn(CubeMesh(), FxMaterial(), c + Rand(0.35f * k), g, g * Random.Range(2.2f, 3.2f),
                      new Color(0.42f, 0.41f, 0.40f, 0.75f), new Color(0.15f, 0.15f, 0.15f, 0.75f), vel, 0.6f, 1.4f,
                      Random.Range(0.9f, 1.5f), true).Spin = Rand(120f);
            }

            // estilhacos da caixa (vermelho, creme e cinza) caindo com gravidade
            Color[] chips = { new Color(0.80f, 0.12f, 0.10f, 1f), new Color(0.93f, 0.90f, 0.85f, 1f), new Color(0.22f, 0.22f, 0.22f, 1f) };
            for (int i = 0; i < 14; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.9f + 0.35f;
                Color cc = chips[Random.Range(0, chips.Length)];
                Spawn(CubeMesh(), LitMaterial(), c + dir * 0.2f, Random.Range(0.09f, 0.18f), 0.07f, cc, cc,
                      dir * Random.Range(3f, 7f) * k, -16f, 0.2f, Random.Range(1.0f, 1.5f), false).Spin = Rand(540f);
            }
        }

        // lascas que saem de blocos destruidos
        public static void Chips(Vector3 pos, Color a, Color b, int n, float size)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.3f;
                Color cc = Random.value < 0.5f ? a : b;
                Spawn(CubeMesh(), LitMaterial(), pos + Rand(0.25f), Random.Range(size * 0.7f, size * 1.2f), size * 0.4f, cc, cc,
                      dir * Random.Range(2f, 4.5f), -14f, 0.2f, Random.Range(0.7f, 1.1f), false).Spin = Rand(480f);
            }
        }

        // ---------- pavio da TNT ----------
        public static void FuseSpark(Vector3 pos)
        {
            Vector3 vel = new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(1.2f, 2.4f), Random.Range(-0.7f, 0.7f));
            Spawn(CubeMesh(), FxMaterial(), pos + Rand(0.04f), 0.07f, 0.02f,
                  new Color(1f, 0.92f, 0.4f, 1f), new Color(1f, 0.3f, 0.05f, 1f), vel, -4f, 0.5f, 0.35f, true);
        }

        public static void FuseSmoke(Vector3 pos)
        {
            Spawn(CubeMesh(), FxMaterial(), pos + Rand(0.05f), 0.10f, 0.28f,
                  new Color(0.55f, 0.55f, 0.55f, 0.6f), new Color(0.25f, 0.25f, 0.25f, 0.6f),
                  new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 0.9f), Random.Range(-0.2f, 0.2f)), 0f, 0.8f, 0.7f, true)
                  .Spin = Rand(90f);
        }

        // ---------- foguete ----------
        public static void RocketSpark(Vector3 pos)
        {
            Vector3 vel = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-3f, -1f), Random.Range(-0.5f, 0.5f));
            Spawn(CubeMesh(), FxMaterial(), pos + Rand(0.03f), 0.09f, 0.02f,
                  new Color(1f, 0.95f, 0.5f, 1f), new Color(1f, 0.35f, 0.05f, 1f), vel, -2f, 0.6f, 0.45f, true);
        }

        public static void FireworkBurst(Vector3 c, Color main)
        {
            Color second = Color.Lerp(main, Color.white, 0.55f);
            Orb(c, 0.3f, 2.2f, new Color(1f, 1f, 0.95f, 0.85f), new Color(main.r, main.g, main.b, 0f), 0.2f, Vector3.one);
            for (int i = 0; i < 52; i++)       // esfera de polvora colorida
            {
                Vector3 dir = Random.onUnitSphere;
                Color cc = (i % 3 == 0) ? second : main;
                Spawn(CubeMesh(), FxMaterial(), c + dir * 0.1f, Random.Range(0.12f, 0.2f), 0.02f,
                      cc, new Color(cc.r * 0.45f, cc.g * 0.45f, cc.b * 0.45f, 1f),
                      dir * Random.Range(3.5f, 6.5f), -3.5f, 1.4f, Random.Range(0.9f, 1.5f), true).Spin = Rand(300f);
            }
            for (int i = 0; i < 22; i++)       // brilhinhos que ficam caindo devagar
            {
                Vector3 dir = Random.onUnitSphere;
                Spawn(CubeMesh(), FxMaterial(), c + dir * 0.4f, 0.07f, 0.01f, Color.white, main,
                      dir * Random.Range(0.8f, 2.2f), -1.2f, 0.9f, Random.Range(1.2f, 2f), true);
            }
        }

        // ---------- perola ----------
        public static void PearlTrail(Vector3 pos)
        {
            Spawn(CubeMesh(), FxMaterial(), pos + Rand(0.05f), 0.07f, 0.01f,
                  new Color(0.35f, 1f, 0.85f, 0.9f), new Color(0.45f, 0.25f, 0.9f, 0.9f), Rand(0.25f), 0f, 1f, 0.4f, true);
        }

        public static void TeleportBurst(Vector3 pos, bool fizzle)
        {
            Color a = fizzle ? new Color(0.6f, 0.6f, 0.6f, 0.9f) : new Color(0.35f, 1f, 0.85f, 0.95f);
            Color b = fizzle ? new Color(0.25f, 0.25f, 0.25f, 0.9f) : new Color(0.55f, 0.25f, 0.95f, 0.95f);
            int n = fizzle ? 8 : 18;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                Spawn(CubeMesh(), FxMaterial(), pos + dir * 0.15f, Random.Range(0.07f, 0.14f), 0.01f, a, b,
                      dir * Random.Range(1.2f, 3.2f), -1.5f, 1.8f, Random.Range(0.4f, 0.7f), true).Spin = Rand(300f);
            }
            if (!fizzle)
                Orb(pos, 0.2f, 1.3f, new Color(0.75f, 1f, 0.95f, 0.55f), new Color(0.5f, 0.3f, 1f, 0.55f), 0.25f, Vector3.one);
        }
    }
}
