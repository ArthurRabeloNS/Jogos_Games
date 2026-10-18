using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace SteveCube
{
    [BepInPlugin("com.seunome.oc2.stevecube", "Steve Cube", "0.3.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<float> HeightWorld;
        internal static ConfigEntry<float> Width;
        internal static ConfigEntry<float> OffsetY;
        internal static ConfigEntry<float> YawOffset;
        internal static ConfigEntry<float> WalkSpeed;
        internal static ConfigEntry<bool> ShowTools;
        internal static ConfigEntry<float> HeldScale;
        internal static ConfigEntry<float> HeldSpin;
        internal static ConfigEntry<bool> HideByLayer;
        internal static ConfigEntry<bool> Diag;
        internal static ConfigEntry<bool> DumpCfg;
        float _timer;

        void Awake()
        {
            Log = Logger;

            HeightWorld = Config.Bind("Steve", "AlturaMundo", 1.5f, "Altura total do Steve em unidades do jogo");
            Width = Config.Bind("Steve", "LarguraBlocos", 1.0f, "1 = proporcao exata do Minecraft; suba pra engrossar o Steve");
            OffsetY = Config.Bind("Steve", "AjusteY", 0f, "Sobe/desce o Steve (unidades do jogo) se os pes afundarem ou flutuarem");
            YawOffset = Config.Bind("Steve", "RotacaoY", 0f, "Gire 180 se o Steve aparecer de costas");

            WalkSpeed = Config.Bind("Animacao", "VelocidadeDaCaminhada", 1f, "1 = ritmo da caminhada do Minecraft");
            ShowTools = Config.Bind("Animacao", "FerramentasNaMao", true, "Mostra espada/machado/picareta na mao do Steve enquanto o chef corta");

            HeldScale = Config.Bind("Item", "EscalaNaMao", 0.5f, "Escala do item segurado");
            HeldSpin = Config.Bind("Item", "GiroGrausPorSeg", 70f, "0 = item nao gira");

            HideByLayer = Config.Bind("Ocultar", "OcultarPorLayer", true,
                "Plano B: se o chef original AINDA aparecer, ligue isto (move o corpo dele pra uma camada que nenhuma camera desenha)");

            Diag = Config.Bind("Debug", "Diagnostico", true, "Escreve no log o que ainda esta ligado no chef original");
            DumpCfg = Config.Bind("Debug", "GerarDump", false, "Grava BepInEx/chef_dump2.txt");

            SteveAssets.Load();
            Log.LogInfo("Steve Cube 0.3.1 pronto.");
        }

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.5f;

            GameObject chefs = GameObject.Find("Chefs");
            if (chefs == null) return;

            foreach (Transform player in chefs.transform)
            {
                // Steve substitui apenas o modelo do Player 1; Player 2 permanece original.
                if (!string.Equals(player.name, "Player 1", StringComparison.OrdinalIgnoreCase)) continue;
                if (!player.gameObject.activeInHierarchy) continue;
                if (player.GetComponent<SteveChef>() != null) continue;
                if (player.GetComponent("ChefMeshReplacer") == null) continue;

                try
                {
                    Spawn(player);
                }
                catch (Exception e)
                {
                    Log.LogError("Falha ao criar o Steve em " + player.name + ": " + e);
                }
            }
        }

        void Spawn(Transform player)
        {
            Transform meshRoot = player.Find("Chef/Mesh");
            if (meshRoot == null) meshRoot = player;

            var sc = player.gameObject.AddComponent<SteveChef>();
            sc.Init(player, meshRoot);
            Log.LogInfo("Steve criado em " + SteveDump.PathOf(player) + "  (altura=" + HeightWorld.Value + ")");

            if (DumpCfg.Value) StartCoroutine(DumpLater(player));
        }

        IEnumerator DumpLater(Transform player)
        {
            yield return new WaitForSeconds(3f);
            if (player != null) SteveDump.Write(player);
        }
    }

    // =====================================================================
    //  TEXTURA + MATERIAL
    // =====================================================================
    static class SteveAssets
    {
        public static Texture2D Tex;
        public static Texture2D ToolTex;
        static Material _mat;
        static Material _toolMat;
        static Shader _shader;
        static Color32[] _px;

        public static void Load()
        {
            string path = System.IO.Path.Combine(Paths.PluginPath, "steve.png");
            if (File.Exists(path))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (t.LoadImage(File.ReadAllBytes(path)))
                {
                    Tex = t;
                    Plugin.Log.LogInfo("steve.png carregado: " + t.width + "x" + t.height);
                }
            }
            if (Tex == null) Tex = LoadEmbedded();      // skin embutida na DLL
            if (Tex == null)
            {
                Plugin.Log.LogWarning("steve.png nao encontrado em " + Paths.PluginPath
                    + " -> usando a skin do Steve gerada por codigo (coloque uma skin 64x64 la pra ficar identica).");
                Tex = MakeSteveSkin();
            }
            Tex.filterMode = FilterMode.Point;
            Tex.wrapMode = TextureWrapMode.Clamp;

            ToolTex = MakeToolTex();
        }

        static Texture2D LoadEmbedded()
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(SteveSkinData.Base64);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (t.LoadImage(bytes))
                {
                    Plugin.Log.LogInfo("Usando a skin do Steve embutida na DLL (" + t.width + "x" + t.height
                        + "). Para trocar, coloque um steve.png em " + Paths.PluginPath);
                    return t;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Skin embutida falhou (" + e.Message + "); usando o pintor de reserva.");
            }
            return null;
        }

        // ---------- pintura (origem y)
        static byte B(int v) { return (byte)(v < 0 ? 0 : (v > 255 ? 255 : v)); }
        static Color32 C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }

        static void Rect(int x, int y, int w, int h, Color32 c, int amp)
        {
            for (int j = y; j < y + h; j++)
            {
                for (int i = x; i < x + w; i++)
                {
                    int d = 0;
                    if (amp > 0)
                    {
                        int hs = ((i * 73856093) ^ (j * 19349663)) >> 5;
                        d = ((hs & 0xFF) % (2 * amp + 1)) - amp;   // ruido fixo, igual ao "granulado" do Minecraft
                    }
                    _px[(63 - j) * 64 + i] = new Color32(B(c.r + d), B(c.g + d), B(c.b + d), 255);
                }
            }
        }

        static void Dot(int x, int y, Color32 c)
        {
            _px[(63 - y) * 64 + x] = c;
        }

        static Texture2D MakeSteveSkin()
        {
            _px = new Color32[64 * 64];

            Color32 skin = C(198, 141, 109), skinDk = C(170, 112, 84), nose = C(150, 98, 74);
            Color32 hair = C(74, 50, 34), shirt = C(0, 168, 168), pants = C(60, 62, 160), shoe = C(98, 98, 98);
            Color32 white = C(255, 255, 255), pupil = C(66, 52, 150), beard = C(98, 62, 46);

            // cabeça 
            Rect(0, 0, 32, 16, skin, 5);

            // topo do cabelo
            Rect(8, 0, 8, 8, hair, 6);

            // Franja 
            Rect(8, 8, 8, 2, hair, 6);

            // Costeletas
            Rect(8, 10, 1, 2, hair, 6);
            Rect(15, 10, 1, 2, hair, 6);

            
            // Laterais do cabelo em degraus
            Rect(0, 8, 8, 2, hair, 6);
            Rect(0, 10, 4, 2, hair, 6);
            Rect(0, 12, 3, 2, hair, 6);
            Rect(0, 14, 2, 1, hair, 6);

            Rect(16, 8, 8, 2, hair, 6);
            Rect(20, 10, 4, 2, hair, 6);
            Rect(21, 12, 3, 2, hair, 6);
            Rect(22, 14, 2, 1, hair, 6);


            // Nuca
            Rect(24, 8, 8, 7, hair, 6);

            // rosto
            Dot(9, 12, white); Dot(14, 12, white);
            Dot(10, 12, pupil); Dot(13, 12, pupil);
            Dot(11, 13, nose); Dot(12, 13, nose);
            Rect(10, 14, 4, 1, beard, 0);
            Rect(9, 15, 6, 1, skinDk, 2);

            // corpo 
            Rect(16, 16, 24, 16, shirt, 5);

            // perna direita
            Rect(0, 16, 16, 16, pants, 5);
            Rect(0, 30, 16, 2, shoe, 4);
            Rect(8, 16, 4, 4, shoe, 4);
            Rect(16, 48, 16, 16, pants, 5);
            Rect(16, 62, 16, 2, shoe, 4);
            Rect(24, 48, 4, 4, shoe, 4);

            // braco direito
            Rect(40, 16, 16, 16, skin, 5);
            Rect(44, 16, 4, 4, shirt, 5);
            Rect(40, 20, 16, 4, shirt, 5);
            Rect(32, 48, 16, 16, skin, 5);
            Rect(36, 48, 4, 4, shirt, 5);
            Rect(32, 52, 16, 4, shirt, 5);

            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            t.SetPixels32(_px);
            t.Apply();
            return t;
        }

        static Texture2D MakeToolTex()
        {
            var t = new Texture2D(8, 1, TextureFormat.RGBA32, false);
            Color32[] cs = new Color32[]
            {
                C(120, 86, 48), C(216, 216, 220), C(150, 150, 158), C(128, 128, 128),
                C(250, 220, 60), C(90, 215, 205), C(84, 60, 32), C(30, 30, 30)
            };
            t.SetPixels32(cs);
            t.Apply();
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ---------- shader ----------
        static int ShaderScore(string name)
        {
            string n = name.ToLowerInvariant();
            string[] bad = { "hidden", "sprite", "ui/", "particle", "skybox", "cubemap", "additive", "blend", "alpha",
                "transparent", "fade", "cutout", "outline", "silhouette", "shadow", "depth", "decal", "water", "glass",
                "terrain", "blur", "distort", "mask", "stencil", "projector", "wireframe", "text", "font", "gui",
                "nature", "tree", "grass", "vertexlit" };
            foreach (string b in bad)
            {
                if (n.Contains(b)) return 0;
            }
            int s = 0;
            if (n.Contains("unlit")) s += 4;
            if (n.Contains("texture")) s += 2;
            if (n.Contains("diffuse")) s += 3;
            if (n.Contains("mobile")) s += 1;
            return s;
        }

        static Shader FindShader()
        {
            if (_shader != null) return _shader;

            // 1) shaders opacos 
            string[] preferred = { "Unlit/Texture", "Mobile/Unlit (Supports Lightmap)", "Legacy Shaders/Diffuse",
                "Mobile/Diffuse", "Diffuse", "Standard" };
            foreach (string n in preferred)
            {
                Shader s = Shader.Find(n);
                if (s != null && s.isSupported)
                {
                    _shader = s;
                    Plugin.Log.LogInfo("Shader do Steve: " + n);
                    return s;
                }
            }

            // 2) procura entre os shaders que o jogo ja carregou um opaco
            Shader best = null;
            int bestScore = 0;
            foreach (Shader s in Resources.FindObjectsOfTypeAll<Shader>())
            {
                if (s == null || !s.isSupported) continue;
                int sc = ShaderScore(s.name);
                if (sc <= bestScore) continue;

                Material probe = new Material(s);
                bool ok = probe.HasProperty("_MainTex");
                UnityEngine.Object.Destroy(probe);
                if (ok)
                {
                    best = s;
                    bestScore = sc;
                }
            }
            if (best != null)
            {
                _shader = best;
                Plugin.Log.LogInfo("Shader do Steve (achado entre os do jogo): " + best.name);
                return best;
            }

            // 3) 
            Shader sp = Shader.Find("Sprites/Default");
            if (sp != null)
            {
                _shader = sp;
                Plugin.Log.LogWarning("Nenhum shader opaco achado; usando Sprites/Default.");
            }
            return sp;
        }

        public static Material GetMaterial(Material fallbackFromGame)
        {
            if (_mat != null) return _mat;

            Shader sh = FindShader();
            if (sh != null) _mat = new Material(sh);
            else if (fallbackFromGame != null)
            {
                Plugin.Log.LogWarning("Nenhum shader achado; copiando material do chef: " + fallbackFromGame.shader.name);
                _mat = new Material(fallbackFromGame);
            }
            else _mat = new Material(Shader.Find("Hidden/InternalErrorShader"));

            _mat.mainTexture = Tex;
            if (_mat.HasProperty("_Glossiness")) _mat.SetFloat("_Glossiness", 0f);   // sem brilho "de plastico"
            if (_mat.HasProperty("_Metallic")) _mat.SetFloat("_Metallic", 0f);
            return _mat;
        }

        public static Material GetToolMaterial(Material skinMat)
        {
            if (_toolMat != null) return _toolMat;
            _toolMat = new Material(skinMat);
            _toolMat.mainTexture = ToolTex;
            return _toolMat;
        }
    }

    // =====================================================================
    //  skin do Minecraft + ferramentas
    // =====================================================================
    class SteveRig
    {
        public Transform Root, Head, Body, RArm, LArm, RLeg, LLeg, Hand;
        public Transform[] Tools = new Transform[3];   // 0 espada, 1 machado, 2 picareta
    }

    class ToolBox
    {
        public Vector3 Size, Center;
        public int Tex;
        public ToolBox(float w, float h, float d, float x, float y, float z, int tex)
        {
            Size = new Vector3(w, h, d);
            Center = new Vector3(x, y, z);
            Tex = tex;
        }
    }

    static class SteveBuilder
    {
        // 4 cantos por face
        static readonly int[] FRONT  = { +1,-1,+1,  +1,+1,+1,  -1,+1,+1,  -1,-1,+1 };
        static readonly int[] BACK   = { -1,-1,-1,  -1,+1,-1,  +1,+1,-1,  +1,-1,-1 };
        static readonly int[] RIGHT  = { +1,-1,-1,  +1,+1,-1,  +1,+1,+1,  +1,-1,+1 };
        static readonly int[] LEFT   = { -1,-1,+1,  -1,+1,+1,  -1,+1,-1,  -1,-1,-1 };
        static readonly int[] TOP    = { +1,+1,+1,  +1,+1,-1,  -1,+1,-1,  -1,+1,+1 };
        static readonly int[] BOTTOM = { +1,-1,-1,  +1,-1,+1,  -1,-1,+1,  -1,-1,-1 };

        // Tudo medido em "pixels de skin"
        static float _L = 1f;

        public static SteveRig Build(Transform parent, Material mat, float tw, float th)
        {
            _L = Plugin.Width.Value;
            bool legacy = th <= 32f;
            var rig = new SteveRig();
            rig.Root = new GameObject("SteveRig").transform;
            rig.Root.SetParent(parent, false);

            rig.Head = Part(rig.Root, "Head", mat, 8, 8, 8, 0, 0, new Vector3(0, 24, 0), new Vector3(0, 4, 0), tw, th);
            rig.Body = Part(rig.Root, "Body", mat, 8, 12, 4, 16, 16, new Vector3(0, 18, 0), Vector3.zero, tw, th);
            rig.RArm = Part(rig.Root, "RArm", mat, 4, 12, 4, 40, 16, new Vector3(+6, 24, 0), new Vector3(0, -6, 0), tw, th);
            rig.LArm = Part(rig.Root, "LArm", mat, 4, 12, 4, legacy ? 40 : 32, legacy ? 16 : 48, new Vector3(-6, 24, 0), new Vector3(0, -6, 0), tw, th);
            rig.RLeg = Part(rig.Root, "RLeg", mat, 4, 12, 4, 0, 16, new Vector3(+2, 12, 0), new Vector3(0, -6, 0), tw, th);
            rig.LLeg = Part(rig.Root, "LLeg", mat, 4, 12, 4, legacy ? 0 : 16, legacy ? 16 : 48, new Vector3(-2, 12, 0), new Vector3(0, -6, 0), tw, th);

            // ponta do braco direito 
            var hand = new GameObject("HandAnchor").transform;
            hand.SetParent(rig.RArm, false);
            hand.localPosition = new Vector3(0, -12, 0);
            rig.Hand = hand;
            return rig;
        }

        static Transform Part(Transform parent, string name, Material mat, float w, float h, float d,
                              float u, float v, Vector3 pivot, Vector3 meshOffset, float tw, float th)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pivot.x * _L, pivot.y, pivot.z);
            go.AddComponent<MeshFilter>().sharedMesh = BuildBox(w, h, d, u, v, meshOffset, tw, th);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.receiveShadows = false;
            return go.transform;
        }

        static Mesh BuildBox(float w, float h, float d, float u, float v, Vector3 c, float tw, float th)
        {
            var vs = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var tr = new List<int>(36);
            var half = new Vector3(w / 2f * _L, h / 2f, d / 2f * _L);

            // Layout Minecraft
            Face(vs, uv, tr, c, half, TOP,    u + d,         v,     u + d + w,         v + d,         tw, th);
            Face(vs, uv, tr, c, half, BOTTOM, u + d + w,     v,     u + 2 * w + d,     v + d,         tw, th);
            Face(vs, uv, tr, c, half, RIGHT,  u,             v + d, u + d,             v + d + h,     tw, th);
            Face(vs, uv, tr, c, half, FRONT,  u + d,         v + d, u + d + w,         v + d + h,     tw, th);
            Face(vs, uv, tr, c, half, LEFT,   u + d + w,     v + d, u + 2 * d + w,     v + d + h,     tw, th);
            Face(vs, uv, tr, c, half, BACK,   u + 2 * d + w, v + d, u + 2 * d + 2 * w, v + d + h,     tw, th);

            var m = new Mesh();
            m.name = "SteveBox";
            m.SetVertices(vs);
            m.SetUVs(0, uv);
            m.SetTriangles(tr, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void Face(List<Vector3> vs, List<Vector2> uv, List<int> tr, Vector3 c, Vector3 half, int[] s,
                         float x0, float y0, float x1, float y1, float tw, float th)
        {
            int i = vs.Count;
            for (int k = 0; k < 4; k++)
                vs.Add(c + new Vector3(s[k * 3] * half.x, s[k * 3 + 1] * half.y, s[k * 3 + 2] * half.z));

            uv.Add(new Vector2(x0 / tw, 1f - y1 / th));
            uv.Add(new Vector2(x0 / tw, 1f - y0 / th));
            uv.Add(new Vector2(x1 / tw, 1f - y0 / th));
            uv.Add(new Vector2(x1 / tw, 1f - y1 / th));

            tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
            tr.Add(i); tr.Add(i + 2); tr.Add(i + 3);
        }

        // -caixas coloridas 

        static Mesh SolidBox(Vector3 size, int texIdx)
        {
            var vs = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var tr = new List<int>(36);
            var half = size * 0.5f;
            int[][] faces = { FRONT, BACK, RIGHT, LEFT, TOP, BOTTOM };
            Vector2 t = new Vector2((texIdx + 0.5f) / 8f, 0.5f);

            foreach (int[] s in faces)
            {
                int i = vs.Count;
                for (int k = 0; k < 4; k++)
                    vs.Add(new Vector3(s[k * 3] * half.x, s[k * 3 + 1] * half.y, s[k * 3 + 2] * half.z));
                uv.Add(t); uv.Add(t); uv.Add(t); uv.Add(t);
                tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
                tr.Add(i); tr.Add(i + 2); tr.Add(i + 3);
            }

            var m = new Mesh();
            m.name = "SteveToolBox";
            m.SetVertices(vs);
            m.SetUVs(0, uv);
            m.SetTriangles(tr, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Transform MakeTool(string name, Transform hand, Material mat, ToolBox[] boxes)
        {
            var root = new GameObject(name).transform;
            root.SetParent(hand, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.Euler(70f, 0f, 0f);   

            foreach (ToolBox b in boxes)
            {
                var go = new GameObject("b");
                go.transform.SetParent(root, false);
                go.transform.localPosition = b.Center;
                go.AddComponent<MeshFilter>().sharedMesh = SolidBox(b.Size, b.Tex);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.receiveShadows = false;
            }
            root.gameObject.SetActive(false);
            return root;
        }

        public static void BuildTools(SteveRig rig, Material toolMat)
        {
            // espada 
            rig.Tools[0] = MakeTool("Tool_Sword", rig.Hand, toolMat, new ToolBox[]
            {
                new ToolBox(0.9f, 4f, 1.2f, 0f, 2f, 0f, 6),
                new ToolBox(1.2f, 1f, 4f, 0f, 4.5f, 0f, 6),
                new ToolBox(0.9f, 11f, 1.6f, 0f, 10.5f, 0f, 1)
            });
            // machado
            rig.Tools[1] = MakeTool("Tool_Axe", rig.Hand, toolMat, new ToolBox[]
            {
                new ToolBox(1.2f, 14f, 1.2f, 0f, 7f, 0f, 0),
                new ToolBox(0.9f, 5f, 5f, 0f, 11.5f, 2.5f, 1),
                new ToolBox(0.9f, 5f, 1f, 0f, 11.5f, 5.5f, 2)
            });
            // picareta (martelo)
            rig.Tools[2] = MakeTool("Tool_Pickaxe", rig.Hand, toolMat, new ToolBox[]
            {
                new ToolBox(1.2f, 14f, 1.2f, 0f, 7f, 0f, 0),
                new ToolBox(0.9f, 1.6f, 12f, 0f, 14.2f, 0f, 2),
                new ToolBox(0.9f, 2.4f, 1.6f, 0f, 13.2f, 5.2f, 2),
                new ToolBox(0.9f, 2.4f, 1.6f, 0f, 13.2f, -5.2f, 2)
            });
        }
    }

    // =====================================================================
    //  COMPONENTE POR CHEF 
    // =====================================================================
    [DefaultExecutionOrder(32000)]
    public class SteveChef : MonoBehaviour
    {
    
        static readonly string[] TOOL_NAMES = { "Knife", "Cleaver", "Hatchet", "Hammer" };
        static readonly int[] TOOL_KIND = { 0, 0, 1, 2 };

        static int ToolKindOf(string n)
        {
            for (int i = 0; i < TOOL_NAMES.Length; i++)
                if (n == TOOL_NAMES[i]) return TOOL_KIND[i];
            return -1;
        }

        static readonly PropertyInfo ForceOff = typeof(Renderer).GetProperty("forceRenderingOff");

        public string HeldName = "";

        readonly List<Renderer> _heldHiddenByUs = new List<Renderer>();
        bool _heldWantVisible = true;

        public void SetHeldItemVisible(bool visible)
        {
            _heldWantVisible = visible;
            if (visible) RestoreHeld(); else HideHeldNow();
        }

        void RestoreHeld()
        {
            if (_heldHiddenByUs.Count == 0) return;
            for (int i = 0; i < _heldHiddenByUs.Count; i++)
                if (_heldHiddenByUs[i] != null) _heldHiddenByUs[i].enabled = true;
            _heldHiddenByUs.Clear();
        }

        void HideHeldNow()
        {
            if (_attachment == null || _attachment.childCount == 0)
            {
                _heldHiddenByUs.Clear(); 
                return;
            }
            Renderer[] rs = _attachment.GetChild(0).GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                if (!_heldHiddenByUs.Contains(r)) _heldHiddenByUs.Add(r);
            }
        }

        static readonly List<SteveChef> All = new List<SteveChef>();
        static readonly List<Renderer> Hidden = new List<Renderer>();
        static bool _hooked;

        static void HideList(List<Renderer> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Renderer r = list[i];
                if (r != null && r.enabled) { r.enabled = false; Hidden.Add(r); }
            }
        }

        static void PreCull(Camera cam)
        {
            for (int c = 0; c < All.Count; c++)
            {
                SteveChef ch = All[c];
                if (ch == null) continue;
                HideList(ch._body);
                HideList(ch._toolR);
            }
        }

        static void PostRender(Camera cam)
        {
            for (int i = 0; i < Hidden.Count; i++)
                if (Hidden[i] != null) Hidden[i].enabled = true;   // devolve o estado que o jogo queria
            Hidden.Clear();
        }

        Transform _player, _meshRoot, _attach, _attachment;
        Vector3 _attachPos;
        Quaternion _attachRot;
        bool _attachMoved;
        SteveRig _rig;
        Renderer _plate;

        readonly List<Renderer> _body = new List<Renderer>();      // corpo/cabeca/chapeus do perso original
        readonly List<Renderer> _toolR = new List<Renderer>();     // ferramentas do perso original
        readonly List<int> _toolK = new List<int>();

        float _pixel, _refresh, _t, _amp, _limb, _swingClock, _oneShot, _spin;
        Vector3 _last;
        string _lastHeld = "";
        bool _wasHolding, _holdInit;
        int _hideLayer = -1;
        bool _layerWarned;
        readonly Dictionary<GameObject, int> _origLayer = new Dictionary<GameObject, int>();
        float _diagAt;
        int _diagCount;

        public void Init(Transform player, Transform meshRoot)
        {
            _player = player;
            _meshRoot = meshRoot;

            Material game = null;
            foreach (SkinnedMeshRenderer r in meshRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMaterial != null) { game = r.sharedMaterial; break; }

            Material skinMat = SteveAssets.GetMaterial(game);
            _rig = SteveBuilder.Build(player, skinMat, SteveAssets.Tex.width, SteveAssets.Tex.height);
            SteveBuilder.BuildTools(_rig, SteveAssets.GetToolMaterial(skinMat));

            float parentScale = Mathf.Max(0.0001f, player.lossyScale.y);
            _pixel = Mathf.Max(0.05f, Plugin.HeightWorld.Value) / 32f;     // unidades de mundo por pixel da skin
            _rig.Root.localScale = Vector3.one * (_pixel / parentScale);
            _rig.Root.localPosition = new Vector3(0f, Plugin.OffsetY.Value / parentScale, 0f);
            _rig.Root.localRotation = Quaternion.Euler(0f, Plugin.YawOffset.Value, 0f);

            SetLayer(_rig.Root, player.gameObject.layer);

            Transform plate = player.Find("Chef/Props/m_Plate_wash");
            _plate = plate != null ? plate.GetComponent<Renderer>() : null;

            _last = player.position;
            _diagAt = 2f;
            All.Add(this);
            if (!_hooked)
            {
                _hooked = true;
                Camera.onPreCull += PreCull;
                Camera.onPostRender += PostRender;
                Plugin.Log.LogInfo("forceRenderingOff " + (ForceOff != null ? "disponivel" : "INDISPONIVEL nesta versao do Unity")
                    + " (o esconder usa tambem enabled=false a cada quadro e antes de cada camera).");
            }
            FindAttach();
            Refresh();
        }

        void FindAttach()
        {
            Transform a = null;
            Component carrier = _player.GetComponent("PlayerAttachmentCarrier");
            if (carrier != null)
            {
                FieldInfo f = carrier.GetType().GetField("m_attachPoints",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Transform[] pts = f != null ? f.GetValue(carrier) as Transform[] : null;
                if (pts != null && pts.Length > 0) a = pts[0];
            }
            if (a == null) a = _player.Find("Chef/Skeleton/Base/Attach/Attachment");
            if (a == null || a.parent == null)
            {
                Plugin.Log.LogWarning("Ponto de attach nao encontrado em " + _player.name);
                return;
            }
            _attachment = a;
            _attach = a.parent;
            _attachPos = _attach.localPosition;
            _attachRot = _attach.localRotation;
            Plugin.Log.LogInfo("Attach de " + _player.name + ": " + _attach.name + "/" + a.name);
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }

        static int PickHiddenLayer()
        {
            int used = 0;
            foreach (Camera c in Camera.allCameras) used |= c.cullingMask;
            for (int l = 31; l >= 8; l--)
                if ((used & (1 << l)) == 0) return l;
            return -1;
        }

        static readonly Dictionary<string, bool> Logged = new Dictionary<string, bool>();
        static void LogOnce(string msg)
        {
            if (Logged.ContainsKey(msg)) return;
            Logged[msg] = true;
            Plugin.Log.LogWarning(msg);
        }

        Transform _chefCached;
        bool _hadChef;
        int _emptyTicks;

        void Refresh()
        {
            try { RefreshInner(); }
            catch (Exception e)
            {
                LogOnce("Refresh() falhou em " + _player.name + ": " + e.GetType().Name + " - " + e.Message);
            }
        }

        void RefreshInner()
        {
            _body.Clear();
            _toolR.Clear();
            _toolK.Clear();

            Transform chef = _player.Find("Chef");           
            _chefCached = chef;
            _hadChef = chef != null;
            Transform root = chef != null ? chef : _player;
            _meshRoot = root;

            bool onlySkinned = (root == _player);          
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r == null) continue;
                if (r.transform.IsChildOf(_rig.Root)) continue;     // nunca esconde o proprio Steve
                if (onlySkinned && !(r is SkinnedMeshRenderer)) continue;

                string tn = r.GetType().Name;
                if (tn == "ParticleSystemRenderer" || tn == "TrailRenderer" || tn == "LineRenderer") continue;

                int k = ToolKindOf(r.name);
                if (k >= 0) { _toolR.Add(r); _toolK.Add(k); }
                else _body.Add(r);

                if (ForceOff != null) ForceOff.SetValue(r, true, null);  
            }

            Transform plate = _player.Find("Chef/Props/m_Plate_wash");
            _plate = plate != null ? plate.GetComponent<Renderer>() : null;
            if (_attachment == null)
            {
                _attachMoved = false;
                FindAttach();
            }

            if (_body.Count == 0)
            {
                _refresh = 0.25f;                           
                _emptyTicks++;
                if (_emptyTicks == 20)
                    LogOnce("Nenhuma malha achada em " + SteveDump.PathOf(root) + " depois de 5 s.");
            }
            else _emptyTicks = 0;

            ApplyLayerHide();
        }

        void ApplyLayerHide()
        {
            if (!Plugin.HideByLayer.Value) return;

            if (_hideLayer < 0) _hideLayer = PickHiddenLayer();
            if (_hideLayer < 0)
            {
                if (!_layerWarned)
                {
                    _layerWarned = true;
                    Plugin.Log.LogWarning("OcultarPorLayer: nenhuma camada livre (todas as cameras desenham tudo).");
                }
                return;
            }

            for (int i = 0; i < _body.Count; i++)
            {
                Renderer r = _body[i];
                if (r == null) continue;
                GameObject go = r.gameObject;
                if (go.layer == _hideLayer) continue;
                if (!_origLayer.ContainsKey(go)) _origLayer[go] = go.layer;
                go.layer = _hideLayer;
            }
        }

        static string Pretty(string n)
        {
            n = n.Replace("(Clone)", "").Replace("_", " ").Trim();
            return n.Length == 0 ? "Item" : n;
        }

        void LateUpdate()
        {
            if (_rig == null) return;
            float dt = Time.deltaTime;
            _t += dt;

            if (_hadChef && _chefCached == null) _refresh = 0f;     
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f) { _refresh = 1f; Refresh(); }

            for (int i = 0; i < _body.Count; i++)
            {
                Renderer rb = _body[i];
                if (rb != null && rb.enabled) rb.enabled = false;
            }

            if (!_heldWantVisible) HideHeldNow();

            int toolKind = -1;
            for (int i = 0; i < _toolR.Count; i++)
            {
                Renderer rt = _toolR[i];
                if (rt != null && rt.enabled && rt.gameObject.activeInHierarchy) { toolKind = _toolK[i]; break; }
            }
            bool plate = _plate != null && _plate.enabled && _plate.gameObject.activeInHierarchy;
            bool working = toolKind >= 0 || plate;

            // ---- 3) caminhada, igual ao Minecraft
            float speed = 0f;
            if (dt > 0f)
            {
                Vector3 d = _player.position - _last;
                d.y = 0f;
                speed = Mathf.Min(d.magnitude / dt, 30f);
            }
            _last = _player.position;

            float blockSize = Mathf.Max(0.0001f, _pixel * 16f);              // 1 bloco = 16 px
            float f4 = Mathf.Min(1f, (speed / blockSize / 20f) * 4f);        // blocos por tick * 4
            _amp += (f4 - _amp) * (1f - Mathf.Pow(0.6f, dt * 20f));
            if (_amp < 0.01f && f4 <= 0f) _amp = 0f;
            _limb += _amp * dt * 20f * Plugin.WalkSpeed.Value;
            float ph = _limb * 0.6662f;

            // ---- 4) segurando algo? (o item vira filho de Attach/Attachment) ----
            bool holding = _attachment != null && _attachment.childCount > 0;
            string held = holding ? Pretty(_attachment.GetChild(0).name) : "";
            if (held != _lastHeld)
            {
                _lastHeld = held;
                HeldName = held;
                Plugin.Log.LogInfo(_player.name + (held.Length > 0 ? " segurando: " + held : " soltou o item"));
            }
            if (_holdInit && holding != _wasHolding) _oneShot = 0.3f;     // pegar/soltar 
            _wasHolding = holding;
            _holdInit = true;

            // ---- 5) progresso do balanco (cortar/lavar 
            float swing = -1f;
            if (working)
            {
                _swingClock += dt / 0.4f;
                swing = _swingClock - Mathf.Floor(_swingClock);
            }
            else
            {
                _swingClock = 0f;
                if (_oneShot > 0f)
                {
                    _oneShot -= dt;
                    swing = 1f - Mathf.Clamp01(_oneShot / 0.3f);
                }
            }

            // ---- 6) angulos 
            const float R2D = 57.29578f;
            float age = _t * 20f;
            float idleZ = (Mathf.Cos(age * 0.09f) * 0.05f + 0.05f) * R2D;     // respiracao
            float idleX = (Mathf.Sin(age * 0.067f) * 0.05f) * R2D;

            float rArmX = Mathf.Cos(ph + Mathf.PI) * _amp * R2D + idleX;
            float lArmX = Mathf.Cos(ph) * _amp * R2D - idleX;
            float rLegX = Mathf.Cos(ph) * 1.4f * _amp * R2D;
            float lLegX = Mathf.Cos(ph + Mathf.PI) * 1.4f * _amp * R2D;
            float rArmY = 0f, lArmY = 0f, bodyY = 0f;
            float rArmZ = idleZ, lArmZ = -idleZ;

            if (holding) rArmX = rArmX * 0.5f - 50f;        // segurando: braco direito pra frente
            if (working) rArmX = rArmX * 0.5f - 35f;        // cortando/lavando: braco pronto pra bater

            if (swing >= 0f)
            {
                float f1 = 1f - swing;
                f1 = f1 * f1 * f1 * f1;
                f1 = 1f - f1;
                float f2 = Mathf.Sin(f1 * Mathf.PI);
                float f3 = Mathf.Sin(swing * Mathf.PI) * 0.7f * 0.75f;
                bodyY = Mathf.Sin(Mathf.Sqrt(swing) * Mathf.PI * 2f) * 0.2f * R2D;
                rArmX -= (f2 * 1.2f + f3) * R2D;
                rArmY += bodyY * 2f;
                lArmY += bodyY;
                rArmZ += Mathf.Sin(swing * Mathf.PI) * -0.4f * R2D;
            }

            Quaternion rTarget = Quaternion.Euler(rArmX, rArmY, rArmZ);
            _rig.RArm.localRotation = Quaternion.Slerp(_rig.RArm.localRotation, rTarget, 1f - Mathf.Exp(-20f * dt));
            _rig.LArm.localRotation = Quaternion.Euler(lArmX, lArmY, lArmZ);
            _rig.RLeg.localRotation = Quaternion.Euler(rLegX, 0f, 0f);
            _rig.LLeg.localRotation = Quaternion.Euler(lLegX, 0f, 0f);
            _rig.Body.localRotation = Quaternion.Euler(0f, bodyY, 0f);

            // ---- 7) ferramenta na mao do Steve enquanto o perso corta ----
            bool showTool = Plugin.ShowTools.Value && toolKind >= 0;
            for (int k = 0; k < _rig.Tools.Length; k++)
            {
                Transform tt = _rig.Tools[k];
                if (tt == null) continue;
                bool want = showTool && k == toolKind;
                if (tt.gameObject.activeSelf != want) tt.gameObject.SetActive(want);
            }

            // ---- 8) leva o ponto de attach ate a mao direita do Steve
            if (_attach != null)
            {
                if (holding)
                {
                    _spin += Plugin.HeldSpin.Value * dt;
                    float bob = Mathf.Sin(_t * 2.5f) * _pixel * 0.8f;
                    _attach.position = _rig.Hand.position + Vector3.up * (_pixel * 3f + bob);
                    _attachment.localScale = Vector3.one * Plugin.HeldScale.Value;
                    _attachment.localRotation = Quaternion.Euler(0f, _spin, 0f);
                    _attachMoved = true;
                }
                else if (_attachMoved)
                {
                    _attach.localPosition = _attachPos;
                    _attach.localRotation = _attachRot;
                    _attachment.localScale = Vector3.one;
                    _attachment.localRotation = Quaternion.identity;
                    _attachMoved = false;
                }
            }

            // ---- 9) diagnostico no log -
            if (Plugin.Diag.Value && _diagCount < 2)
            {
                _diagAt -= Time.unscaledDeltaTime;
                if (_diagAt <= 0f)
                {
                    _diagCount++;
                    _diagAt = 4f;
                    Diagnose();
                }
            }
        }

        void Diagnose()
        {
            int stillOn = 0, shown = 0;
            var sb = new StringBuilder();
            foreach (Renderer r in _player.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                if (r.transform.IsChildOf(_rig.Root)) continue;
                string tn = r.GetType().Name;
                if (tn == "ParticleSystemRenderer" || tn == "TrailRenderer" || tn == "LineRenderer") continue;
                stillOn++;
                if (shown < 25)
                {
                    shown++;
                    sb.AppendLine("     - " + SteveDump.PathOf(r.transform) + " <" + tn + ">  visivel=" + r.isVisible);
                }
            }
            Plugin.Log.LogInfo("[diag] " + _player.name + " raiz=" + (_meshRoot != null ? SteveDump.PathOf(_meshRoot) : "(destruida)") + ": escondidos=" + _body.Count + " ferramentas=" + _toolR.Count
                + " forceRenderingOff=" + (ForceOff != null ? "sim" : "nao") + " camadaOculta=" + _hideLayer
                + " | renderers do chef AINDA ligados (fora do Steve): " + stillOn + "\n" + sb);
        }

        void OnDestroy()
        {
            All.Remove(this);
            foreach (KeyValuePair<GameObject, int> kv in _origLayer)
                if (kv.Key != null) kv.Key.layer = kv.Value;
            if (_rig != null && _rig.Root != null) Destroy(_rig.Root.gameObject);
        }
    }

    // =====================================================================
    //  DUMP (hierarquia + campos de reflexao) -> BepInEx/chef_dump2.txt
    // =====================================================================
    static class SteveDump
    {
        static readonly string[] INTERESTING = {
            "ChefMeshReplacer", "PlayerAttachmentCarrier", "ClientPlayerAttachmentCarrier", "PlayerControls"
        };

        public static void Write(Transform player)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== " + PathOf(player) + " ===");
            sb.AppendLine("pos=" + player.position + " euler=" + player.eulerAngles + " lossyScale=" + player.lossyScale);

            sb.AppendLine("\n--- Hierarquia ---");
            Tree(player, 0, sb);

            sb.AppendLine("\n--- Campos dos componentes ---");
            foreach (string name in INTERESTING)
            {
                Component c = player.GetComponent(name);
                if (c == null) { sb.AppendLine("[" + name + "] nao encontrado"); continue; }
                sb.AppendLine("[" + name + "]  tipo=" + c.GetType().FullName);
                Fields(c, sb);
            }

            string file = System.IO.Path.Combine(Paths.BepInExRootPath, "chef_dump2.txt");
            File.WriteAllText(file, sb.ToString());
            Plugin.Log.LogInfo("Dump gravado em " + file);
        }

        static void Tree(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 14) return;
            var comps = new StringBuilder();
            foreach (Component c in t.GetComponents<Component>())
                if (c != null && !(c is Transform)) comps.Append(c.GetType().Name).Append(' ');
            sb.AppendLine(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " [off]")
                + "  lp=" + t.localPosition + " le=" + t.localEulerAngles + " ls=" + t.localScale
                + "  { " + comps + "}");
            foreach (Transform c in t) Tree(c, depth + 1, sb);
        }

        static void Fields(object o, StringBuilder sb)
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type t = o.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(F))
                {
                    object v = null;
                    try { v = f.GetValue(o); } catch { }
                    sb.AppendLine("    " + t.Name + "." + f.Name + " : " + f.FieldType.Name + " = " + Describe(v));
                }
            }
        }

        static string Describe(object v)
        {
            if (v == null) return "null";
            Transform tr = v as Transform;
            if (tr != null) return "Transform " + PathOf(tr);
            GameObject go = v as GameObject;
            if (go != null) return "GameObject " + PathOf(go.transform);
            Component comp = v as Component;
            if (comp != null) return comp.GetType().Name + " @ " + PathOf(comp.transform);
            IList list = v as IList;
            if (list != null)
            {
                string first = list.Count > 0 ? Describe(list[0]) : "";
                return "[" + list.Count + " itens] " + first;
            }
            string s = v.ToString();
            return s.Length > 120 ? s.Substring(0, 120) + "..." : s;
        }

        public static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}