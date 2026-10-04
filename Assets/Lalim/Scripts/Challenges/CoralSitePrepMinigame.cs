using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lalim.DDA;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;
using Lalim.UI;

namespace Lalim.Challenges
{
    /// <summary>
    /// Zone 2, minigame 2 — coral replanting at one area, seen from above (top-down camera).
    ///
    /// Site prep, on a rock at the area:
    ///  - SWIPE across the toxic algae to brush it away (a painted mask; clear the target %);
    ///  - TAP the bleached coral skeletons repeatedly to chisel them off;
    ///  - DON'T tap the living coral (decoys at higher difficulty) — that's an error.
    /// Then TAP the clean rock to plant the Coral Seed: coral blooms across the rock and stays there.
    ///
    /// The work happens on a flat stone slab set into a rock (the "coral bed") that rises out of the
    /// sand at the area: its top sits just over the seabed. Areas are placed on flat ground
    /// (Level1SceneBuilder.PlaceCoralSitesOnFlatGround), so nothing sinks into a slope.
    ///
    /// Tasks: one per skeleton + one for the algae + one for planting.
    /// A small glass card at the top shows a progress bar and one hint: what's left to do next.
    /// </summary>
    public class CoralSitePrepMinigame : MonoBehaviour, IAreaMinigame
    {
        [SerializeField] private MinigameDifficultyProfile _profile;
        [Tooltip("Coral shapes for the dead skeletons (painted bleached white).")]
        [SerializeField] private GameObject[] _coralModels;
        [Tooltip("Living coral prefabs, kept in their own colours: the decoys, the planted coral and the bloom.")]
        [SerializeField] private GameObject[] _livingModels;
        [SerializeField] private GameObject _seedModel;
        [Tooltip("URP Unlit with alpha clipping: the brushed-away parts of the algae simply vanish.")]
        [SerializeField] private Material _algaeMaterial;

        [Header("Camera")]
        [Tooltip("Height of the top-down camera above the rock.")]
        [SerializeField, Min(1f)] private float _viewHeight = 3.3f;
        [Tooltip("90 = straight down; a little less keeps some depth on the coral.")]
        [SerializeField, Range(45f, 90f)] private float _viewPitch = 80f;

        [Header("Glass card")]
        [SerializeField] private TMP_FontAsset _hintFont;

        [Header("Site")]
        [SerializeField, Min(0.5f)] private float _rockRadius = 1.3f;
        [SerializeField, Min(16)] private int _maskResolution = 192;
        [SerializeField, Min(0.02f)] private float _brushRadius = 0.09f; // in UV units
        [SerializeField] private Color _algaeColor = new Color(0.33f, 0.4f, 0.16f, 1f);
        [SerializeField] private Color _rockColor = new Color(0.55f, 0.5f, 0.45f);
        [Tooltip("The rock the work slab sits on (the slab borrows its material).")]
        [SerializeField] private GameObject _platformRock;
        [Tooltip("How many corals bloom across the rock when the seed is planted.")]
        [SerializeField, Range(0, 16)] private int _bloomCount = 8;
        [SerializeField] private Color[] _bloomColors =
        {
            new Color(1f, 0.45f, 0.55f), new Color(0.55f, 0.8f, 1f), new Color(1f, 0.7f, 0.3f),
            new Color(0.75f, 0.5f, 1f), new Color(1f, 0.9f, 0.4f), new Color(0.4f, 1f, 0.8f)
        };
        [SerializeField] private Color _bleachedColor = new Color(0.92f, 0.9f, 0.86f);
        [SerializeField] private Color[] _livingColors =
        {
            new Color(1f, 0.45f, 0.55f), new Color(0.55f, 0.8f, 1f), new Color(1f, 0.7f, 0.3f)
        };

        public int TaskCount => Skeletons + 2;
        public float ExpectedSeconds => _profile != null ? _profile.expectedSeconds : 45f;
        public float ExpectedFirstActionSeconds => _profile != null ? _profile.expectedFirstActionSeconds : 5f;
        public int HintBudget => 0;

        private int Skeletons => _profile != null && _profile.Has(Level1DifficultyKnobs.Skeletons) ? Mathf.Max(1, _profile.GetInt(Level1DifficultyKnobs.Skeletons)) : 3;

        private MinigameStage _stage;
        private Transform _site;   // prep pieces: algae, skeletons, decoys, socket (removed after)
        private Transform _bed;    // the rock + slab + bloom (stays)
        private Texture2D _mask;
        private Collider _algaeCollider;
        private int _clearedPixels, _totalPixels;
        private float _algaeTarget;
        private bool _algaeDone, _planted, _ready;
        private readonly Dictionary<Collider, int> _skeletonHp = new Dictionary<Collider, int>();
        private readonly HashSet<Collider> _decoys = new HashSet<Collider>();
        private Collider _socket;
        private GameObject _grownCoral;
        private TMP_Text _hint;
        private RectTransform _barFill;
        private GameObject _statusCanvas;
        private Transform _topView;
        private int _tapsPerSkeleton = 4;
        private float[] _pixelNoise;
        private Color32[] _pixels;   // the algae mask, edited in place and uploaded once per frame
        private float _nextDecoyWarning;
        private Vector3 _lastBrushUv = new Vector3(-1f, -1f, 0f);

        // ───────────────────────── IAreaMinigame ─────────────────────────

        public void Play(AreaStation station)
        {
            _stage ??= new MinigameStage(this);
            BuildSite();
            _stage.Enter(TopView(station));
            BuildStatus();
            UpdateStatus();
            StartCoroutine(EnableAfter(0.6f));
        }

        public void Stop()
        {
            StopAllCoroutines();
            _ready = false;
            if (_statusCanvas != null) Destroy(_statusCanvas);
            // The bed (rock, slab, bloom) stays; the prep pieces go.
            if (_site != null) Destroy(_site.gameObject);
            if (_mask != null) Destroy(_mask);
            if (_topView != null) Destroy(_topView.gameObject);
            _stage?.Exit();
        }

        private Vector3 _bedHome;
        private Vector3 BedHome => _bedHome;

        /// <summary>A camera above the rock looking (nearly) straight down, turned the way the diver faces.</summary>
        private Transform TopView(AreaStation station)
        {
            if (_topView == null) _topView = new GameObject("[CoralTopView]").transform;
            Vector3 centre = BedHome + Vector3.up * 0.3f;
            Vector3 heading = station.ViewPoint.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
            heading.Normalize();
            // Pull back a little along the heading for the slight tilt, so the rock stays centred.
            float back = _viewHeight / Mathf.Tan(_viewPitch * Mathf.Deg2Rad);
            _topView.position = centre + Vector3.up * _viewHeight - heading * back;
            _topView.rotation = Quaternion.LookRotation(centre - _topView.position, heading);
            return _topView;
        }

        /// <summary>Restoring a finished area: the rock is already covered in coral.</summary>
        public void ShowCompletedState()
        {
            if (_grownCoral != null) return;
            EnsureBed(false);
            var rng = new System.Random(name.GetHashCode() ^ transform.position.GetHashCode());
            _grownCoral = SpawnGrownCoral(_bed.position + Vector3.up * 0.3f, 1f, _bed);
            foreach (var (pos, size, color, model) in BloomLayout(rng)) Bloom(pos, size, color, model);
        }

        private IEnumerator EnableAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _ready = true;
        }

        // ───────────────────────── Site ─────────────────────────

        /// <summary>
        /// The bed's origin: above the area, with the slab top (origin + 0.3) just over the highest
        /// seabed under the slab, so a slope never swallows half of it. Cached once the bed exists.
        /// </summary>
        private Vector3 BedOrigin(out float lowestGround)
        {
            Vector3 ground = GroundPoint();
            float highest = ground.y;
            lowestGround = ground.y;
            float r = _rockRadius + 0.3f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                foreach (float k in new[] { 0.5f, 1f })
                {
                    Vector3 p = ground + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * k;
                    if (!Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.point.y > ground.y + 1.5f) continue;   // a coral or rock sticking up, not the seabed
                    highest = Mathf.Max(highest, hit.point.y);
                    lowestGround = Mathf.Min(lowestGround, hit.point.y);
                }
            }
            // Slab top 10 cm over the sand: a flat stone set into the seabed, not a table on top of it.
            return new Vector3(ground.x, highest + 0.1f - 0.3f, ground.z);
        }

        /// <summary>The rock with the flat stone slab on top. Rises out of the sand the first time.</summary>
        private void EnsureBed(bool rise)
        {
            if (_bed != null) return;
            Vector3 origin = BedOrigin(out float lowest);
            _bed = new GameObject("[CoralBed]").transform;
            _bed.SetParent(transform, true);
            _bed.position = origin;
            _bedHome = origin;

            Material stone = null;
            if (_platformRock != null)
            {
                GameObject rock = Instantiate(_platformRock, _bed);
                rock.name = "Rock";
                rock.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                foreach (Collider c in rock.GetComponentsInChildren<Collider>()) Destroy(c);
                // Wider than the slab, its top just under the slab top, its foot below the lowest seabed.
                Bounds b = RenderBounds(rock);
                float width = Mathf.Max(b.size.x, b.size.z);
                float s = _rockRadius * 2.6f / Mathf.Max(0.01f, width);
                rock.transform.localScale = Vector3.Scale(rock.transform.localScale, new Vector3(s, s, s));
                b = RenderBounds(rock);
                float top = origin.y + 0.27f, bottom = lowest - 0.4f;   // its rough edge just shows round the slab
                if (b.size.y < top - bottom)
                    rock.transform.localScale = Vector3.Scale(rock.transform.localScale, new Vector3(1f, (top - bottom) / b.size.y, 1f));
                b = RenderBounds(rock);
                rock.transform.position += Vector3.up * (top - b.max.y);
                var col = rock.AddComponent<BoxCollider>();
                FitCollider(col, rock);
                Renderer rr = rock.GetComponentInChildren<Renderer>();
                if (rr != null) stone = rr.sharedMaterial;
            }

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            slab.name = "Slab";
            slab.transform.SetParent(_bed, false);
            slab.transform.localScale = new Vector3(_rockRadius * 2f, 0.15f, _rockRadius * 2f);
            slab.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            if (stone != null) slab.GetComponent<MeshRenderer>().sharedMaterial = stone;
            else Paint(slab, _rockColor);
            Destroy(slab.GetComponent<Collider>());

            if (rise) StartCoroutine(Rise(_bed, origin));
        }

        private IEnumerator Rise(Transform bed, Vector3 home)
        {
            Vector3 from = home - Vector3.up * 2.2f;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                if (bed == null) yield break;
                bed.position = Vector3.Lerp(from, home, Mathf.SmoothStep(0f, 1f, t / 0.7f));
                yield return null;
            }
            if (bed != null) bed.position = home;
        }

        private static Bounds RenderBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private Vector3 GroundPoint()
        {
            Vector3 origin = transform.position + Vector3.up * 3f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            return transform.position - Vector3.up * 1.5f;
        }

        private void BuildSite()
        {
            _algaeTarget = _profile != null && _profile.Has(Level1DifficultyKnobs.AlgaeTarget) ? _profile.Get(Level1DifficultyKnobs.AlgaeTarget) : 0.75f;
            int taps = _profile != null && _profile.Has(Level1DifficultyKnobs.TapsPerSkeleton) ? Mathf.Max(1, _profile.GetInt(Level1DifficultyKnobs.TapsPerSkeleton)) : 4;
            _tapsPerSkeleton = taps;
            int decoys = _profile != null && _profile.Has(Level1DifficultyKnobs.Decoys) ? Mathf.Max(0, _profile.GetInt(Level1DifficultyKnobs.Decoys)) : 1;

            EnsureBed(true);
            _site = new GameObject("[CoralSite]").transform;
            _site.position = _bed.position;
            _site.SetParent(_bed, true);

            // Algae layer: a quad whose texture alpha is brushed away.
            var algae = GameObject.CreatePrimitive(PrimitiveType.Quad);
            algae.name = "Algae";
            algae.transform.SetParent(_site, false);
            algae.transform.localPosition = new Vector3(0f, 0.31f, 0f);
            algae.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            algae.transform.localScale = Vector3.one * (_rockRadius * 2f);
            Destroy(algae.GetComponent<Collider>());
            _algaeCollider = algae.AddComponent<MeshCollider>(); // MeshCollider gives texture coordinates
            _mask = BuildAlgaeTexture();
            algae.GetComponent<MeshRenderer>().sharedMaterial = AlgaeMaterial(_mask);

            // Bleached skeletons (tap to chisel) and living decoys (leave alone), on a ring.
            int total = Skeletons + decoys;
            var slots = new List<float>();
            for (int i = 0; i < total; i++) slots.Add((i + Random.value * 0.4f) / total * 360f);
            Shuffle(slots);
            for (int i = 0; i < total; i++)
            {
                bool decoy = i >= Skeletons;
                float angle = slots[i] * Mathf.Deg2Rad;
                Vector3 local = new Vector3(Mathf.Cos(angle), 0.3f, Mathf.Sin(angle)) * 1f;
                local.x *= _rockRadius * 0.62f;
                local.z *= _rockRadius * 0.62f;
                local.y = 0.3f;
                GameObject coral = decoy ? SpawnLiving(local, 0.6f) : SpawnCoral(local, _bleachedColor, 0.6f);
                var col = coral.AddComponent<BoxCollider>();
                FitCollider(col, coral);
                if (decoy) { _decoys.Add(col); coral.AddComponent<Sway>(); }
                else _skeletonHp[col] = taps;
            }

            // Socket for the seed (revealed when the site is clean).
            var socket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socket.name = "Socket";
            socket.transform.SetParent(_site, false);
            socket.transform.localPosition = new Vector3(0f, 0.33f, 0f);
            socket.transform.localScale = new Vector3(0.45f, 0.02f, 0.45f);
            Paint(socket, new Color(0.45f, 1f, 0.9f));
            _socket = socket.GetComponent<Collider>();
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(socket.transform, false);
            glow.type = LightType.Point; glow.range = 3f; glow.intensity = 2f; glow.color = new Color(0.45f, 1f, 0.9f);
            socket.SetActive(false);
        }

        private Material AlgaeMaterial(Texture2D mask)
        {
            Material mat;
            if (_algaeMaterial != null) mat = new Material(_algaeMaterial);
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            mat.SetTexture("_BaseMap", mask);
            mat.SetColor("_BaseColor", Color.white);
            return mat;
        }

        private Texture2D BuildAlgaeTexture()
        {
            int n = _maskResolution;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            _pixelNoise = new float[n * n];
            _totalPixels = 0;
            _clearedPixels = 0;
            float seed = Random.value * 50f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                    bool onRock = u * u + v * v <= 0.25f; // round, like the rock
                    float shade = 0.75f + 0.5f * Mathf.PerlinNoise(x * 0.12f + seed, y * 0.12f);
                    _pixelNoise[y * n + x] = Mathf.PerlinNoise(x * 0.09f + seed * 2f, y * 0.09f + 7f);
                    Color c = _algaeColor * shade;
                    c.a = onRock ? 1f : 0f;
                    pixels[y * n + x] = c;
                    if (onRock) _totalPixels++;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _pixels = pixels;
            return tex;
        }

        // ───────────────────────── Input ─────────────────────────

        private void Update()
        {
            if (!_ready || _planted || MinigameStage.PointerOverUI()) return;

            Ray ray = _stage.PointerRay();
            if (Input.GetMouseButtonDown(0) && Physics.Raycast(ray, out RaycastHit tap, 30f, ~0, QueryTriggerInteraction.Collide))
            {
                if (_skeletonHp.ContainsKey(tap.collider)) { Chisel(tap.collider); return; }
                if (_decoys.Contains(tap.collider)) { HitDecoy(tap.collider); return; }
                if (_socket != null && _socket.gameObject.activeSelf && tap.collider == _socket) { StartCoroutine(Plant()); return; }
            }

            // Swipe (drag) across the algae.
            if (Input.GetMouseButton(0) && !_algaeDone && _algaeCollider.Raycast(ray, out RaycastHit hit, 30f))
            {
                Vector2 uv = hit.textureCoord;
                if (_lastBrushUv.x >= 0f)
                {
                    // Fill the gap between frames so a fast swipe leaves a continuous stroke.
                    Vector2 from = _lastBrushUv;
                    int steps = Mathf.CeilToInt(Vector2.Distance(from, uv) / (_brushRadius * 0.5f));
                    for (int i = 1; i <= steps; i++) Brush(Vector2.Lerp(from, uv, i / (float)steps));
                }
                else Brush(uv);
                _lastBrushUv = uv;
                _mask.SetPixels32(_pixels);
                _mask.Apply();
                UpdateStatus();
                CheckAlgaeDone();
            }
            else _lastBrushUv = new Vector3(-1f, -1f, 0f);
        }

        private void Brush(Vector2 uv)
        {
            int n = _maskResolution;
            int cx = Mathf.RoundToInt(uv.x * n), cy = Mathf.RoundToInt(uv.y * n);
            int r = Mathf.CeilToInt(_brushRadius * n);
            bool clearedAny = false;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= n) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= n || (x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    int i = y * n + x;
                    if (_pixels[i].a < 128) continue;
                    _pixels[i].a = 0;
                    _clearedPixels++;
                    clearedAny = true;
                }
            }
            if (clearedAny)
            {
                ChallengeManager.Instance?.ReportCorrectAction();
            }
        }

        private void CheckAlgaeDone()
        {
            if (_algaeDone || _totalPixels == 0 || (float)_clearedPixels / _totalPixels < _algaeTarget) return;
            _algaeDone = true;
            StartCoroutine(DissolveAlgae());
            ChallengeManager.Instance?.ReportTaskCompleted();
            CheckSiteClean();
        }

        /// <summary>The last patches of algae break up and vanish (noise-driven, so it looks organic).</summary>
        private IEnumerator DissolveAlgae()
        {
            Color32[] px = _pixels;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float threshold = t / 0.7f;
                for (int i = 0; i < px.Length; i++) if (px[i].a > 0 && _pixelNoise[i] < threshold) px[i].a = 0;
                _mask.SetPixels32(px);
                _mask.Apply();
                yield return null;
            }
            _algaeCollider.gameObject.SetActive(false);
        }

        private void Chisel(Collider skeleton)
        {
            ChallengeManager.Instance?.ReportCorrectAction();
            int hp = --_skeletonHp[skeleton];
            Transform t = skeleton.transform;
            StartCoroutine(Shake(t, 0.12f));
            Burst(t.position, _bleachedColor, 8);
            if (hp > 0)
            {
                t.localScale *= 0.92f;
                UpdateStatus();
                return;
            }
            _skeletonHp.Remove(skeleton);
            Burst(t.position, _bleachedColor, 24);
            Destroy(skeleton.gameObject);
            ChallengeManager.Instance?.ReportTaskCompleted();
            UpdateStatus();
            CheckSiteClean();
        }

        private void HitDecoy(Collider decoy)
        {
            ChallengeManager.Instance?.ReportError();
            StartCoroutine(Shake(decoy.transform, 0.25f));
            if (Time.time >= _nextDecoyWarning)
            {
                _nextDecoyWarning = Time.time + 3f;
                ZoneBannerUI.Post("That coral is alive! Only chisel the white, dead skeletons.");
            }
        }

        private void CheckSiteClean()
        {
            if (!_algaeDone || _skeletonHp.Count > 0 || _socket == null) return;
            _socket.gameObject.SetActive(true);
            UpdateStatus();
        }

        private IEnumerator Plant()
        {
            _planted = true;
            _socket.gameObject.SetActive(false);
            UpdateStatus();

            if (_seedModel != null)
            {
                GameObject seed = Instantiate(_seedModel, _socket.transform.position + Vector3.up * 0.8f, Quaternion.identity, _site);
                NormaliseSize(seed, 0.25f);
                Vector3 start = seed.transform.position, end = _socket.transform.position + Vector3.up * 0.05f;
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    seed.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / 0.7f));
                    yield return null;
                }
                Destroy(seed);
            }

            _grownCoral = SpawnGrownCoral(_socket.transform.position, 0f, _bed);
            Burst(_socket.transform.position, new Color(0.45f, 1f, 0.9f), 30);
            StartCoroutine(Grow(_grownCoral.transform, 1.6f));
            yield return new WaitForSeconds(0.5f);

            // The seed takes: coral blooms outward across the rock, one after another.
            var rng = new System.Random(name.GetHashCode() ^ transform.position.GetHashCode());
            foreach (var (pos, size, color, model) in BloomLayout(rng))
            {
                GameObject c = Bloom(pos, size, color, model);
                Burst(c.transform.position, color, 10);
                StartCoroutine(Grow(c.transform, 0.8f));
                yield return new WaitForSeconds(0.15f);
            }
            yield return new WaitForSeconds(1.2f);

            ChallengeManager.Instance?.ReportTaskCompleted(); // last task → challenge completes → Stop()
        }

        // ───────────────────────── Bloom ─────────────────────────

        /// <summary>Where the bloom's corals go: a loose spiral out from the centre of the slab.</summary>
        private IEnumerable<(Vector3 pos, float size, Color color, int model)> BloomLayout(System.Random rng)
        {
            int models = HasLiving ? _livingModels.Length : (_coralModels != null ? _coralModels.Length : 0);
            for (int i = 0; i < _bloomCount; i++)
            {
                float a = i * 2.39996f + (float)rng.NextDouble() * 0.6f;              // golden-angle spiral
                float r = _rockRadius * Mathf.Lerp(0.3f, 0.85f, (i + 0.5f) / Mathf.Max(1, _bloomCount));
                Vector3 local = new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r);
                float size = Mathf.Lerp(0.5f, 0.85f, (float)rng.NextDouble());
                Color[] palette = _bloomColors != null && _bloomColors.Length > 0 ? _bloomColors : _livingColors;
                Color color = palette[rng.Next(palette.Length)];
                yield return (_bed.TransformPoint(local), size, color, models > 0 ? rng.Next(models) : -1);
            }
        }

        private GameObject Bloom(Vector3 worldPos, float size, Color color, int model)
        {
            GameObject src = model < 0 ? null : HasLiving ? _livingModels[model] : _coralModels[model];
            GameObject go = src != null ? Instantiate(src) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "[Bloom]";
            go.transform.SetParent(_bed, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            NormaliseSize(go, size);
            SitOn(go, go.transform.position.y);
            if (!HasLiving) Paint(go, color);   // the real prefabs keep their own colours
            go.AddComponent<Sway>();
            return go;
        }

        private static IEnumerator Grow(Transform t, float seconds)
        {
            if (t == null) yield break;
            Vector3 full = t.localScale;
            t.localScale = Vector3.zero;
            for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
            {
                float k = e / seconds;
                t.localScale = full * (Mathf.SmoothStep(0f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.12f);   // a little overshoot
                yield return null;
            }
            if (t != null) t.localScale = full;
        }

        // ───────────────────────── Visual helpers ─────────────────────────

        private GameObject SpawnCoral(Vector3 local, Color color, float size)
        {
            GameObject model = _coralModels != null && _coralModels.Length > 0 ? _coralModels[Random.Range(0, _coralModels.Length)] : null;
            GameObject go = model != null ? Instantiate(model, _site) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(_site, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            NormaliseSize(go, size);
            SitOn(go, go.transform.position.y);
            Paint(go, color);
            return go;
        }

        /// <summary>A living coral (decoy) in its own colours, swaying.</summary>
        private GameObject SpawnLiving(Vector3 local, float size)
        {
            GameObject model = HasLiving ? _livingModels[Random.Range(0, _livingModels.Length)] : null;
            if (model == null) return SpawnCoral(local, _livingColors[Random.Range(0, _livingColors.Length)], size);
            GameObject go = Instantiate(model, _site);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            NormaliseSize(go, size);
            SitOn(go, go.transform.position.y);
            return go;
        }

        private bool HasLiving => _livingModels != null && _livingModels.Length > 0;

        private GameObject SpawnGrownCoral(Vector3 worldPos, float unused, Transform parent)
        {
            GameObject model = HasLiving ? _livingModels[0] : (_coralModels != null && _coralModels.Length > 0 ? _coralModels[0] : null);
            GameObject go = model != null ? Instantiate(model) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "[PlantedCoral]";
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            NormaliseSize(go, 1.1f);
            SitOn(go, worldPos.y);
            if (!HasLiving) Paint(go, _livingColors[0]);
            return go;
        }

        /// <summary>Moves a model so the bottom of its mesh rests at height y (pivots differ between models).</summary>
        private static void SitOn(GameObject go, float y)
        {
            Bounds b = RenderBounds(go);
            go.transform.position += Vector3.up * (y - b.min.y - 0.02f);
        }

        private static void NormaliseSize(GameObject go, float size)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (largest > 0.0001f) go.transform.localScale *= size / largest;
        }

        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        // The coral models carry their colour in textures, so a tint can't bleach them: give each piece
        // a plain lit material in the colour we need (bleached white, living colours, rock).
        private void Paint(GameObject go, Color color)
        {
            if (!_materials.TryGetValue(color, out Material mat))
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader != null ? shader : Shader.Find("Standard"));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                mat.color = color;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
                // A soft glow keeps white skeletons and living colours readable in murky, dim water.
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 0.45f);
                }
                _materials[color] = mat;
            }
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void FitCollider(BoxCollider col, GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            col.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.lossyScale;
            col.size = new Vector3(b.size.x / Mathf.Max(0.0001f, s.x), b.size.y / Mathf.Max(0.0001f, s.y), b.size.z / Mathf.Max(0.0001f, s.z)) * 1.2f;
        }

        private IEnumerator Shake(Transform t, float seconds)
        {
            if (t == null) yield break;
            Vector3 home = t.localPosition;
            for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
            {
                t.localPosition = home + Random.insideUnitSphere * 0.03f;
                yield return null;
            }
            if (t != null) t.localPosition = home;
        }

        private void Burst(Vector3 at, Color color, int count)
        {
            var go = new GameObject("[Chips]");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f; main.loop = false; main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = color;
            main.gravityModifier = 0.15f;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.1f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            renderer.sharedMaterial = new Material(particles != null ? particles : Shader.Find("Sprites/Default"));
            ps.Emit(count);
            Destroy(go, 1.5f);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ───────────────────────── Glass card: progress + one hint ─────────────────────────

        private void BuildStatus()
        {
            _statusCanvas = new GameObject("[CoralSiteHud]", typeof(Canvas), typeof(CanvasScaler));
            _statusCanvas.transform.SetParent(transform, false);
            var canvas = _statusCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = _statusCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            RectTransform card = UiRect("Card", _statusCanvas.transform, new Vector2(0f, -36f), new Vector2(760f, 118f));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            UiSliced(card, "Fill", GlassSprites.Rounded, new Color(0.06f, 0.22f, 0.32f, 0.62f), 0.75f);
            UiSliced(card, "Sheen", GlassSprites.RoundedSheen, new Color(1f, 1f, 1f, 0.9f), 0.75f);
            UiSliced(card, "Rim", GlassSprites.RoundedOutline, new Color(0.78f, 0.96f, 1f, 0.55f), 0.75f);

            // Progress bar: a soft track with a mint fill that grows from the left.
            RectTransform track = UiRect("Track", card, new Vector2(0f, 18f), new Vector2(640f, 22f));
            UiSliced(track, "Back", GlassSprites.Rounded, new Color(1f, 1f, 1f, 0.16f), 0.22f);
            _barFill = UiRect("Fill", track, Vector2.zero, Vector2.zero);
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
            var fill = _barFill.gameObject.AddComponent<Image>();
            fill.sprite = GlassSprites.Rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f / 0.22f;
            fill.color = new Color(0.55f, 1f, 0.75f, 0.95f);
            fill.raycastTarget = false;

            RectTransform hintRt = UiRect("Hint", card, new Vector2(0f, -26f), new Vector2(720f, 40f));
            _hint = hintRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_hintFont != null) _hint.font = _hintFont;
            _hint.fontSize = 28f;
            _hint.alignment = TextAlignmentOptions.Center;
            _hint.color = new Color(1f, 1f, 1f, 0.88f);
            _hint.textWrappingMode = TextWrappingModes.NoWrap;
            _hint.raycastTarget = false;
        }

        private static RectTransform UiRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void UiSliced(RectTransform parent, string name, Sprite sprite, Color color, float cornerScale)
        {
            RectTransform rt = UiRect(name, parent, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / cornerScale;
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>
        /// Overall progress: the algae (up to its target) and the dead coral (each tap counts) weigh
        /// the same. The hint names what's still to do: algae first by default, the dead coral once
        /// the algae is gone, then planting.
        /// </summary>
        private void UpdateStatus()
        {
            if (_hint == null) return;
            float algae = _algaeDone || _totalPixels == 0 ? 1f : Mathf.Clamp01((float)_clearedPixels / _totalPixels / Mathf.Max(0.01f, _algaeTarget));
            float chiselled = 0f;
            foreach (int hp in _skeletonHp.Values) chiselled += _tapsPerSkeleton - hp;
            float coral = Skeletons > 0 ? (Skeletons * _tapsPerSkeleton - (_skeletonHp.Count * _tapsPerSkeleton - chiselled)) / (float)(Skeletons * _tapsPerSkeleton) : 1f;
            float progress = 0.5f * algae + 0.5f * Mathf.Clamp01(coral);
            _barFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

            if (_planted) _hint.text = "The young coral takes root…";
            else if (_socket != null && _socket.gameObject.activeSelf) _hint.text = "Tap the glowing spot to plant the Coral Seed";
            else if (!_algaeDone) _hint.text = "Swipe to brush away the algae";
            else _hint.text = "Tap the dead corals to chisel them off";
        }

#if UNITY_EDITOR
        public void EditorSetupCorals(GameObject[] skeletonShapes, GameObject[] livingModels)
        {
            _coralModels = skeletonShapes;
            _livingModels = livingModels;
        }

        public void EditorSetupLook(Material algaeMaterial, TMP_FontAsset hintFont, GameObject platformRock)
        {
            _algaeMaterial = algaeMaterial;
            _hintFont = hintFont;
            _platformRock = platformRock;
        }

        public void EditorSetup(MinigameDifficultyProfile profile, GameObject[] coralModels, GameObject seedModel)
        {
            _profile = profile;
            _coralModels = coralModels;
            _seedModel = seedModel;
        }
#endif

        /// <summary>Gentle sway so living coral reads as alive.</summary>
        private class Sway : MonoBehaviour
        {
            private float _phase;
            private Quaternion _home;
            private void Start() { _phase = Random.value * 10f; _home = transform.localRotation; }
            private void Update() => transform.localRotation = _home * Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + _phase) * 6f, 0f, Mathf.Cos(Time.time * 1.1f + _phase) * 4f);
        }
    }
}
