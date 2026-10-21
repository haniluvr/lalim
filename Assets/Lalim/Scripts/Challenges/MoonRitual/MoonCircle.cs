using UnityEngine;

namespace Lalim.Challenges.MoonRitual
{
    /// <summary>
    /// One stone circle of Bakunawa's ritual, lying on the abyss floor with a moon phase carved
    /// into it. The diver "stands" in it by swimming into the column above it and holding still a
    /// moment (the rim brightens as they hold); then the ritual decides whether it was the right
    /// phase. Looks:
    ///  - Hidden: not there yet (before the diver comes down).
    ///  - Ready: a faint moon and rim, waiting.
    ///  - Lit: correct so far: bright moon, a column of light and a soft light on the floor.
    ///  - Wrong: a red flash, then back to Ready with the rest.
    ///  - Hint: the next one to step on pulses.
    /// Built at runtime from two materials (stone, and an unlit see-through glow).
    /// </summary>
    public class MoonCircle : MonoBehaviour
    {
        public enum Look { Hidden, Ready, Lit, Wrong }

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private static readonly Color Moonlight = new Color(0.78f, 0.9f, 1f);
        private static readonly Color Sour = new Color(1f, 0.25f, 0.2f);

        public int Phase { get; private set; }
        public float Radius { get; private set; } = 2.4f;
        public Look Current { get; private set; } = Look.Hidden;
        public bool Hinted { get; set; }
        /// <summary>0…1 while the diver holds still in the column (the rim brightens with it).</summary>
        public float Hold { get; set; }

        private Renderer _stone, _moon, _rim, _pool, _columnA, _columnB;
        private Light _light;
        private MaterialPropertyBlock _mpb;
        private float _shown;            // 0 hidden … 1 there
        private float _lit;              // 0 ready … 1 lit
        private float _wrong;            // red flash, fading
        private float _flare;            // the finale's burst
        private float _seed;

        public static MoonCircle Create(Transform parent, Vector3 floorPoint, float radius, Material stone, Material glow)
        {
            var go = new GameObject("MoonCircle");
            go.transform.SetParent(parent, false);
            go.transform.position = floorPoint;
            var c = go.AddComponent<MoonCircle>();
            c.Build(radius, stone, glow);
            return c;
        }

        private void Build(float radius, Material stone, Material glow)
        {
            Radius = radius;
            _mpb = new MaterialPropertyBlock();
            _seed = Random.value * 10f;

            // The stone: a low drum, half sunk in the sand.
            GameObject drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drum.name = "Stone";
            Destroy(drum.GetComponent<Collider>());
            drum.transform.SetParent(transform, false);
            drum.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            drum.transform.localScale = new Vector3(radius * 2.1f, 0.18f, radius * 2.1f);
            _stone = drum.GetComponent<Renderer>();
            if (stone != null) _stone.sharedMaterial = stone;
            _stone.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _pool = Flat("Pool", radius * 2.6f, 0.2f, glow, MoonPhases.Disc);
            _rim = Flat("Rim", radius * 2.2f, 0.24f, glow, MoonPhases.Ring);
            _moon = Flat("Moon", radius * 1.5f, 0.26f, glow, MoonPhases.Glyph(0));

            // Two crossed quads: the column of light that rises when it's lit.
            _columnA = Upright("ColumnA", radius * 2f, 11f, 0f, glow);
            _columnB = Upright("ColumnB", radius * 2f, 11f, 90f, glow);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = radius * 5f;
            _light.color = Moonlight;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;

            Apply();
        }

        private Renderer Flat(string name, float size, float height, Material glow, Texture2D tex)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.localPosition = new Vector3(0f, height, 0f);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // facing up
            q.transform.localScale = new Vector3(size, size, 1f);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetTex(r, tex);
            return r;
        }

        private Renderer Upright(string name, float width, float height, float yaw, Material glow)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.localPosition = new Vector3(0f, height * 0.5f + 0.2f, 0f);
            q.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            q.transform.localScale = new Vector3(width, height, 1f);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetTex(r, MoonPhases.Column);
            return r;
        }

        private void SetTex(Renderer r, Texture2D tex)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetTexture(BaseMap, tex);
            r.SetPropertyBlock(_mpb);
        }

        private void Tint(Renderer r, Color c)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, c);
            r.SetPropertyBlock(_mpb);
            r.enabled = c.a > 0.003f;
        }

        /// <summary>Carves a (new) moon phase into the stone.</summary>
        public void SetPhase(int phase)
        {
            Phase = phase;
            SetTex(_moon, MoonPhases.Glyph(phase));
        }

        public void Show(Look look)
        {
            if (look == Look.Wrong) _wrong = 1f;   // flashes red, then settles back to Ready
            Current = look;
        }

        /// <summary>The finale: every circle bursts bright, then stays lit.</summary>
        public void Flare() => _flare = 1f;

        /// <summary>The diver is in the column above the stone (close enough to the floor to count).</summary>
        public bool Contains(Vector3 p, float height) =>
            new Vector2(p.x - transform.position.x, p.z - transform.position.z).sqrMagnitude <= Radius * Radius
            && p.y - transform.position.y <= height && p.y - transform.position.y >= -1f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _shown = Mathf.MoveTowards(_shown, Current == Look.Hidden ? 0f : 1f, dt * 0.7f);
            _lit = Mathf.MoveTowards(_lit, Current == Look.Lit ? 1f : 0f, dt * (Current == Look.Lit ? 3f : 1.5f));
            _wrong = Mathf.MoveTowards(_wrong, 0f, dt * 1.1f);
            _flare = Mathf.MoveTowards(_flare, 0f, dt * 0.5f);
            if (Current == Look.Wrong && _wrong <= 0.4f) Current = Look.Ready;
            Apply();
        }

        private void Apply()
        {
            float t = Time.time;
            float breathe = 0.85f + 0.15f * Mathf.Sin(t * 1.3f + _seed);
            float hint = Hinted && Current == Look.Ready ? 0.5f + 0.5f * Mathf.Sin(t * 6f) : 0f;

            // The phase's colour (the same as the places that sing it): faint while waiting, full
            // when lit, and past white then (blooms where bloom is on).
            Color phaseCol = MoonPhases.ColorOf(Phase);
            Color moonCol = Color.Lerp(Color.Lerp(Moonlight, phaseCol, 0.35f), phaseCol, Mathf.Max(_lit, hint)) * (1f + 0.9f * Mathf.Max(_lit, hint) + _flare);
            moonCol = Color.Lerp(moonCol, Sour * 1.6f, _wrong);
            float moonA = Mathf.Lerp(0.65f * breathe, 1f, Mathf.Max(_lit, hint)) + _wrong * 0.4f;
            Tint(_moon, WithA(moonCol, moonA * _shown));

            float rimA = Mathf.Lerp(0.4f * breathe, 1f, Mathf.Max(Mathf.Max(_lit, Hold), hint)) + _flare * 0.5f;
            Tint(_rim, WithA(Color.Lerp(Color.Lerp(Color.Lerp(Moonlight, phaseCol, 0.35f), phaseCol, Mathf.Max(Hold, _lit)), Sour, _wrong), Mathf.Clamp01(rimA) * _shown));

            float poolA = (0.15f + 0.7f * _lit + 0.35f * Hold + 0.5f * hint + 0.5f * _wrong + 0.5f * _flare) * _shown;
            Tint(_pool, WithA(Color.Lerp(moonCol, Sour, _wrong), poolA));

            float colA = (0.75f * _lit * (0.85f + 0.15f * Mathf.Sin(t * 2.1f + _seed)) + 0.6f * hint + 0.6f * _flare) * _shown;
            Color colC = WithA(Color.Lerp(Moonlight, phaseCol, 0.7f) * 1.4f, colA);
            Tint(_columnA, colC);
            Tint(_columnB, colC);

            float li = (6f * _lit + 4f * _wrong + 6f * _flare + 3.5f * hint) * _shown;
            _light.enabled = li > 0.02f;
            _light.intensity = li;
            _light.color = Color.Lerp(Color.Lerp(Moonlight, phaseCol, _lit), Sour, _wrong);
        }

        private static Color WithA(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }
    }
}
