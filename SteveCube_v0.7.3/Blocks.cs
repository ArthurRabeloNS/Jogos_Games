using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SteveCube
{
    // marca os blocos criados pelo Steve
    public class SteveBlockTag : MonoBehaviour { }

    // obsidiana: a explosao da TNT nao quebra (o U quebra)
    public class SteveObsidianTag : MonoBehaviour { }

    // pedacinhos que voam quando algo quebra (sem colisao, so efeito)
    public class SteveDebris : MonoBehaviour
    {
        public Vector3 Vel;
        float _t;

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            Vel += Vector3.down * 14f * dt;
            transform.position += Vel * dt;
            transform.Rotate(Vel.x * 90f * dt, Vel.z * 90f * dt, 0f);
            transform.localScale *= Mathf.Max(0f, 1f - dt * 1.4f);
            if (_t > 0.9f) Destroy(gameObject);
        }
    }

    // =====================================================================
    //  B = coloca bloco de madeira na frente do Steve (com colisao)
    //  U = quebra o bloco / a bancada que ele esta encostado
    //  N = restaura as bancadas quebradas
    // =====================================================================
    [BepInPlugin("com.seunome.oc2.steveblocks", "Steve Blocks", "0.1.0")]
    public class SteveBlocks : BaseUnityPlugin
    {
        sealed class Broken
        {
            public GameObject Root;
            public readonly List<Collider> Cols = new List<Collider>();
            public readonly List<Renderer> Rends = new List<Renderer>();
        }

        ConfigEntry<KeyCode> _placeKey, _breakKey, _restoreKey;
        ConfigEntry<float> _size, _reach, _borderMargin;
        ConfigEntry<int> _maxBlocks;
        ConfigEntry<bool> _breakCounters, _breakStations, _protectBorders;

        Material _woodMat, _stoneMat, _obsMat;
        Bounds _mapBounds;
        bool _boundsValid;
        Vector3 _gridOrigin;
        bool _originValid;
        readonly List<GameObject> _blocks = new List<GameObject>();
        readonly List<Broken> _broken = new List<Broken>();
        static SteveBlocks _instance;
        float _boundsProbeAt;

        void Awake()
        {
            _instance = this;
            _placeKey = Config.Bind("Teclas", "ColocarBloco", KeyCode.B, "Coloca um bloco de madeira na frente do Steve");
            _breakKey = Config.Bind("Teclas", "Quebrar", KeyCode.U, "Quebra o bloco/bancada na frente do Steve");
            _restoreKey = Config.Bind("Teclas", "RestaurarBancadas", KeyCode.N, "Restaura todas as bancadas quebradas");

            _size = Config.Bind("Blocos", "TamanhoDoBloco", 1.0f, "Lado do bloco (e da grade) em unidades do jogo");
            _reach = Config.Bind("Blocos", "AlcanceParaQuebrar", 1.6f, "Distancia maxima para quebrar");
            _maxBlocks = Config.Bind("Blocos", "LimiteDeBlocos", 64, "Maximo de blocos ao mesmo tempo (o mais antigo some)");

            _breakCounters = Config.Bind("Quebrar", "QuebrarBancadas", true, "Permite quebrar as bancadas do mapa");
            _breakStations = Config.Bind("Quebrar", "QuebrarEstacoes", false, "Permite quebrar tabuas de cortar, caixas de ingredientes, etc. (pode travar a fase)");
            _protectBorders = Config.Bind("Quebrar", "ProtegerBordas", true, "Nao quebra as bancadas da borda do mapa");
            _borderMargin = Config.Bind("Quebrar", "MargemDaBorda", 0.7f, "Distancia da borda (unidades) que conta como borda protegida");

            SceneManager.sceneLoaded += delegate (Scene s, LoadSceneMode m)
            {
                _boundsValid = false;
                _originValid = false;
            };
            Logger.LogInfo("Steve Blocks pronto. B = bloco, U = quebrar, N = restaurar.");
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            try
            {
                if (!_boundsValid && Time.unscaledTime >= _boundsProbeAt)
                {
                    _boundsProbeAt = Time.unscaledTime + 0.5f;
                    Bounds warm;
                    MapBounds(out warm);
                }
                if (Input.GetKeyDown(_placeKey.Value)) Place();
                if (Input.GetKeyDown(_breakKey.Value)) BreakFront();
                if (Input.GetKeyDown(_restoreKey.Value)) Restore();
            }
            catch (System.Exception e)
            {
                Logger.LogError("Steve Blocks: " + e);
            }
        }

        // ------------------------------------------------------------------ helpers
        static Transform FindPlayer()
        {
            GameObject chefs = GameObject.Find("Chefs");
            if (chefs == null) return null;
            Transform best = null;
            foreach (Transform t in chefs.transform)
            {
                if (!t.gameObject.activeInHierarchy) continue;
                if (best == null || string.CompareOrdinal(t.name, best.name) < 0) best = t;   // Player 1
            }
            return best;
        }

        static Vector3 Cardinal(Vector3 f)
        {
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) return Vector3.forward;
            if (Mathf.Abs(f.x) > Mathf.Abs(f.z)) return new Vector3(Mathf.Sign(f.x), 0f, 0f);
            return new Vector3(0f, 0f, Mathf.Sign(f.z));
        }

        static Transform Group(string path)
        {
            GameObject g = GameObject.Find(path);
            return g != null ? g.transform : null;
        }

        static Collider SolidCollider(Transform t)
        {
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true))
                if (c != null && !c.isTrigger) return c;
            return null;
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        static bool IsUnder(Transform t, string name)
        {
            for (Transform c = t; c != null; c = c.parent)
                if (c.name == name) return true;
            return false;
        }

        // origem da grade = centro da primeira bancada (assim os blocos alinham com o mapa)
        Vector3 GridOrigin()
        {
            if (_originValid) return _gridOrigin;
            Transform g = Group("Design/Work Surfaces");
            if (g != null && g.childCount > 0)
            {
                Collider c = SolidCollider(g.GetChild(0));
                Vector3 p = c != null ? c.bounds.center : g.GetChild(0).position;
                _gridOrigin = new Vector3(p.x, 0f, p.z);
                _originValid = true;
            }
            else _gridOrigin = Vector3.zero;
            return _gridOrigin;
        }

        Vector3 SnapCell(Vector3 pos, float size)
        {
            Vector3 o = GridOrigin();
            pos.x = o.x + Mathf.Round((pos.x - o.x) / size) * size;
            pos.z = o.z + Mathf.Round((pos.z - o.z) / size) * size;
            return pos;
        }

        int BlockLayer()
        {
            // usa a mesma layer das bancadas, assim o chef colide com o bloco do mesmo jeito
            Transform g = Group("Design/Work Surfaces");
            if (g != null)
            {
                Collider c = SolidCollider(g);
                if (c != null) return c.gameObject.layer;
            }
            return 0;
        }

        // bounds do mapa (bancadas + estacoes), medido ANTES de quebrar qualquer coisa
        bool MapBounds(out Bounds b)
        {
            if (_boundsValid) { b = _mapBounds; return true; }
            b = new Bounds();
            bool has = false;
            string[] groups = { "Design/Work Surfaces", "Design/Gameplay Stations" };
            for (int i = 0; i < groups.Length; i++)
            {
                Transform g = Group(groups[i]);
                if (g == null) continue;
                foreach (Collider c in g.GetComponentsInChildren<Collider>(true))
                {
                    if (c == null || c.isTrigger) continue;
                    if (!has) { b = c.bounds; has = true; }
                    else b.Encapsulate(c.bounds);
                }
            }
            if (has) { _mapBounds = b; _boundsValid = true; }
            return has;
        }

        // usado pela Ender Pearl: limites do mapa (bancadas + estacoes), medidos antes de qualquer quebra
        public static bool TryGetMapBounds(out Bounds b)
        {
            b = new Bounds();
            if (_instance == null) return false;
            return _instance.MapBounds(out b);
        }

        bool IsBorder(Transform counter)
        {
            if (!_protectBorders.Value) return false;
            Bounds b;
            if (!MapBounds(out b)) return false;
            Collider col = SolidCollider(counter);
            Vector3 c = col != null ? col.bounds.center : counter.position;
            float m = _borderMargin.Value;
            return c.x - b.min.x < m || b.max.x - c.x < m || c.z - b.min.z < m || b.max.z - c.z < m;
        }

        // ------------------------------------------------------------------ materiais
        static Shader FindShader()
        {
            string[] names = { "Legacy Shaders/Diffuse", "Diffuse", "Mobile/Diffuse", "Unlit/Texture", "Sprites/Default" };
            for (int i = 0; i < names.Length; i++)
            {
                Shader s = Shader.Find(names[i]);
                if (s != null) return s;
            }
            return null;
        }

        static Material NewMaterial()
        {
            Shader sh = FindShader();
            if (sh != null) return new Material(sh);
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);   // plano C: copia o material padrao
            Material m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
            Destroy(tmp);
            return m;
        }

        static float Hash(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 255) / 255f;
        }

        // tabuas de carvalho 16x16 (pixel art). Se existir planks.png em BepInEx\plugins, usa ele.
        static Texture2D MakePlanks()
        {
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    int plank = y / 4, ly = y % 4;
                    float n = Hash(x + plank * 5, y) * 0.10f;
                    float shade = (plank % 2 == 0) ? 1.0f : 0.92f;
                    Color c = new Color(0.72f * shade + n * 0.3f, 0.57f * shade + n * 0.25f, 0.34f * shade + n * 0.2f, 1f);
                    if (ly == 3) c = new Color(0.43f, 0.32f, 0.17f, 1f);
                    int seam = (plank % 2 == 0) ? 5 : 12;
                    if (x == seam && ly != 3) c = new Color(0.50f, 0.38f, 0.20f, 1f);
                    t.SetPixel(x, 15 - y, c);
                }
            }
            t.Apply();
            return t;
        }

        Material WoodMaterial()
        {
            if (_woodMat != null) return _woodMat;
            Texture2D tex = null;
            try
            {
                string[] found = Directory.GetFiles(Paths.PluginPath, "planks.png", SearchOption.AllDirectories);
                if (found.Length > 0)
                {
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (t.LoadImage(File.ReadAllBytes(found[0]))) tex = t;
                }
            }
            catch { }
            if (tex == null) tex = MakePlanks();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            _woodMat = NewMaterial();
            _woodMat.mainTexture = tex;
            return _woodMat;
        }

        GameObject MakeBlock(string name, Vector3 center, float size, Material mat, bool obsidian)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);   // ja vem com BoxCollider
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.layer = BlockLayer();
            go.AddComponent<SteveBlockTag>();
            if (obsidian) go.AddComponent<SteveObsidianTag>();
            return go;
        }

        // obsidiana 16x16: quase preta com veios roxos
        static Texture2D MakeObsidian()
        {
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float n = Hash(x * 3 + 11, y * 5 + 7);
                    Color c = new Color(0.07f + n * 0.05f, 0.04f + n * 0.03f, 0.12f + n * 0.07f, 1f);
                    if (n > 0.86f) c = new Color(0.34f, 0.20f, 0.56f, 1f);
                    else if (n > 0.74f) c = new Color(0.17f, 0.10f, 0.29f, 1f);
                    t.SetPixel(x, 15 - y, c);
                }
            }
            t.Apply();
            return t;
        }

        Material ObsidianMaterial()
        {
            if (_obsMat != null) return _obsMat;
            Texture2D tex = MakeObsidian();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            _obsMat = NewMaterial();
            _obsMat.mainTexture = tex;
            return _obsMat;
        }

        Material StoneMaterial()
        {
            if (_stoneMat != null) return _stoneMat;
            _stoneMat = NewMaterial();
            _stoneMat.color = new Color(0.42f, 0.31f, 0.20f, 1f);
            return _stoneMat;
        }

        void SpawnDebris(Vector3 center, float size, Material mat)
        {
            for (int i = 0; i < 10; i++)
            {
                GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Collider col = d.GetComponent<Collider>();
                if (col != null) Destroy(col);
                d.transform.position = center + new Vector3(
                    UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.3f, 0.3f)) * size;
                d.transform.localScale = Vector3.one * size * 0.16f;
                d.GetComponent<Renderer>().sharedMaterial = mat;
                SteveDebris sd = d.AddComponent<SteveDebris>();
                sd.Vel = new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(2f, 4.5f), UnityEngine.Random.Range(-2f, 2f));
            }
        }

        // ------------------------------------------------------------------ B: colocar
        void Place()
        {
            Transform p = FindPlayer();
            if (p == null) return;

            float size = Mathf.Max(0.25f, _size.Value);
            Vector3 dir = Cardinal(p.forward);
            Vector3 center = SnapCell(p.position + dir * size, size);
            center.y = p.position.y + size * 0.5f;

            // celula ocupada? (ignora piso e gatilhos)
            Collider[] hits = Physics.OverlapBox(center, Vector3.one * size * 0.4f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider h = hits[i];
                if (h == null || h.isTrigger) continue;
                if (h.bounds.max.y <= p.position.y + size * 0.25f) continue;   // piso
                Logger.LogInfo("Sem espaco para o bloco: ocupado por " + PathOf(h.transform));
                return;
            }

            _blocks.RemoveAll(delegate (GameObject g) { return g == null; });
            while (_blocks.Count >= Mathf.Max(1, _maxBlocks.Value))
            {
                if (_blocks[0] != null) Destroy(_blocks[0]);
                _blocks.RemoveAt(0);
            }

            GameObject go;
            int selectedSlot = SteveHotbar.SelectedSlot;
            if (selectedSlot == 1)
            {
                // Slot 1: madeira
                go = MakeBlock("SteveBlock_Wood", center, size, WoodMaterial(), false);
                Logger.LogInfo("Bloco de madeira colocado em " + center + " (layer " + go.layer + ")");
            }
            else if (selectedSlot == 2)
            {
                // Slot 2: TNT no mesmo ponto de grade
                go = SteveHotbar.PlaceTnt(center, BlockLayer(), size);
                Logger.LogInfo("TNT colocada em " + center + "; pavio de 4 segundos.");
            }
            else if (selectedSlot == 5)
            {
                // Slot 5: obsidiana (a TNT nao quebra; o U quebra)
                go = MakeBlock("SteveBlock_Obsidian", center, size, ObsidianMaterial(), true);
                Logger.LogInfo("Obsidiana colocada em " + center);
            }
            else if (selectedSlot == 6)
            {
                // Slot 6: foguete no chao (decola e estoura em fogos coloridos; nao e solido)
                SteveHotbar.PlaceRocket(new Vector3(center.x, p.position.y, center.z));
                Logger.LogInfo("Foguete colocado em " + center);
                return;
            }
            else
            {
                Logger.LogInfo("Slot " + selectedSlot + " nao coloca nada com B. Use 1 (madeira), 2 (TNT), 5 (obsidiana) ou 6 (foguete).");
                return;
            }
            if (go != null) _blocks.Add(go);
        }

        // ------------------------------------------------------------------ U: quebrar
        bool Resolve(Collider c, out GameObject root, out bool isBlock)
        {
            root = null;
            isBlock = false;
            if (c == null || c.isTrigger) return false;

            SteveBlockTag tag = c.GetComponentInParent<SteveBlockTag>();
            if (tag != null) { root = tag.gameObject; isBlock = true; return true; }

            if (IsUnder(c.transform, "Chefs")) return false;

            for (Transform cur = c.transform; cur.parent != null; cur = cur.parent)
            {
                string pn = cur.parent.name;
                if (pn == "Work Surfaces" && _breakCounters.Value)
                {
                    if (IsBorder(cur)) return false;
                    root = cur.gameObject;
                    return true;
                }
                if (pn == "Gameplay Stations" && _breakStations.Value)
                {
                    if (IsBorder(cur)) return false;
                    root = cur.gameObject;
                    return true;
                }
            }
            return false;      // paredes, bordas, piso, etc. nunca quebram
        }

        void BreakFront()
        {
            Transform p = FindPlayer();
            if (p == null) return;

            float size = Mathf.Max(0.25f, _size.Value);
            Vector3 fwd = p.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 dir = Cardinal(p.forward);

            var cands = new List<Collider>();
            RaycastHit[] rh = Physics.RaycastAll(p.position + Vector3.up * 0.5f, fwd, _reach.Value);
            for (int i = 0; i < rh.Length; i++) cands.Add(rh[i].collider);
            Vector3 cell = SnapCell(p.position + dir * size, size);
            cell.y = p.position.y + size * 0.5f;
            Collider[] ov = Physics.OverlapBox(cell, Vector3.one * size * 0.45f);
            for (int i = 0; i < ov.Length; i++) cands.Add(ov[i]);

            GameObject bestRoot = null;
            bool bestIsBlock = false;
            float bestDist = float.MaxValue;
            for (int i = 0; i < cands.Count; i++)
            {
                GameObject r;
                bool blk;
                if (!Resolve(cands[i], out r, out blk)) continue;
                float d = Vector3.Distance(p.position, cands[i].bounds.center);
                if (d < bestDist) { bestDist = d; bestRoot = r; bestIsBlock = blk; }
            }

            if (bestRoot == null)
            {
                Logger.LogInfo("Nada para quebrar na frente do Steve (bordas, paredes e piso sao protegidos).");
                return;
            }

            Vector3 where = bestRoot.transform.position + Vector3.up * size * 0.5f;
            if (bestIsBlock)
            {
                SpawnDebris(where, size, bestRoot.GetComponent<SteveObsidianTag>() != null ? ObsidianMaterial() : WoodMaterial());
                _blocks.Remove(bestRoot);
                Destroy(bestRoot);
                Logger.LogInfo("Bloco quebrado.");
                return;
            }

            // bancada: NAO destroi (o jogo guarda referencias a ela). So desliga colisao e visual.
            var b = new Broken();
            b.Root = bestRoot;
            foreach (Collider c in bestRoot.GetComponentsInChildren<Collider>(true))
                if (c != null && c.enabled) { b.Cols.Add(c); c.enabled = false; }
            foreach (Renderer r in bestRoot.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.enabled) { b.Rends.Add(r); r.enabled = false; }
            _broken.Add(b);
            SpawnDebris(where, size, StoneMaterial());
            Logger.LogInfo("Bancada quebrada: " + PathOf(bestRoot.transform) + " (N restaura)");
        }

        // Chamado pelo TNT. Desativa visual e colisao das bancadas que o sistema permite quebrar.
        public static void DetonateWorkSurfaces(Vector3 center, float radius)
        {
            SteveBlocks inst = _instance;
            if (inst == null) return;
            Collider[] hits = Physics.OverlapSphere(center, radius);
            HashSet<GameObject> roots = new HashSet<GameObject>();
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null) continue;
                GameObject root;
                bool isBlock;
                if (!inst.Resolve(hit, out root, out isBlock) || root == null || isBlock || !roots.Add(root)) continue;
                bool alreadyBroken = false;
                for (int b = 0; b < inst._broken.Count; b++)
                    if (inst._broken[b].Root == root) { alreadyBroken = true; break; }
                if (alreadyBroken) continue;

                Broken broken = new Broken();
                broken.Root = root;
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                    if (colliders[c] != null && colliders[c].enabled) { broken.Cols.Add(colliders[c]); colliders[c].enabled = false; }
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                    if (renderers[r] != null && renderers[r].enabled) { broken.Rends.Add(renderers[r]); renderers[r].enabled = false; }
                if (broken.Cols.Count > 0 || broken.Rends.Count > 0) inst._broken.Add(broken);
            }
        }

        // ------------------------------------------------------------------ N: restaurar
        void Restore()
        {
            int n = 0;
            for (int i = 0; i < _broken.Count; i++)
            {
                Broken b = _broken[i];
                if (b.Root == null) continue;
                for (int c = 0; c < b.Cols.Count; c++) if (b.Cols[c] != null) b.Cols[c].enabled = true;
                for (int r = 0; r < b.Rends.Count; r++) if (b.Rends[r] != null) b.Rends[r].enabled = true;
                n++;
            }
            _broken.Clear();
            Logger.LogInfo("Bancadas restauradas: " + n);
        }
    }
}
