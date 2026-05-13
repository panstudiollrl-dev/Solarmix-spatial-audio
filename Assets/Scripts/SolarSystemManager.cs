using UnityEngine;
using System.Collections.Generic;

public class SolarSystemManager : MonoBehaviour
{
    public static SolarSystemManager Instance;
    const int OrbitSegments = 384;

    [Header("材質")]
    public Material defaultPlanetMaterial;

    // 不用 [Serializable]，直接在 code 裡定死，Inspector 無法覆蓋
    struct PlanetData
    {
        public string name;
        public Color color;
        public float speed;
        public float dist;
        public float incl;
        public float size;
    }

    PlanetData[] planets;

    public List<PlanetOrbit> Planets { get; private set; } = new List<PlanetOrbit>();
    public List<FMSynthesizer> Synths { get; private set; } = new List<FMSynthesizer>();

    void Awake()
    {
        Instance = this;

        // 直接在 code 裡定義，完全不受 Inspector 影響
        planets = new PlanetData[]
        {
            new PlanetData { name="Mercury", color=new Color(0.78f,0.78f,1.0f),  speed=8.0f,  dist=48f,  incl=7.0f,  size=5f  },
            new PlanetData { name="Venus",   color=new Color(1.0f, 0.84f,0.25f), speed=6.0f,  dist=72f,  incl=3.4f,  size=6f  },
            new PlanetData { name="Earth",   color=new Color(0.16f,0.71f,0.96f), speed=4.5f,  dist=96f,  incl=0.0f,  size=7f  },
            new PlanetData { name="Mars",    color=new Color(1.0f, 0.34f,0.13f), speed=3.5f,  dist=120f, incl=1.8f,  size=6f  },
            new PlanetData { name="Jupiter", color=new Color(1.0f, 0.65f,0.15f), speed=2.2f,  dist=150f, incl=1.3f,  size=11f },
            new PlanetData { name="Saturn",  color=new Color(1.0f, 0.95f,0.46f), speed=1.8f,  dist=182f, incl=2.5f,  size=9f  },
            new PlanetData { name="Uranus",  color=new Color(0.15f,0.78f,0.85f), speed=1.3f,  dist=214f, incl=0.8f,  size=8f  },
            new PlanetData { name="Neptune", color=new Color(0.24f,0.35f,1.0f),  speed=1.0f,  dist=248f, incl=1.8f,  size=8f  },
            new PlanetData { name="Pluto",   color=new Color(0.81f,0.58f,0.85f), speed=0.7f,  dist=284f, incl=17.0f, size=6f  },
        };
    }

    void Start()
    {
        SpawnSun();
        // SpawnStars();
        for (int i = 0; i < planets.Length; i++)
            SpawnPlanet(i);
    }

    void SpawnSun()
    {
        var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glow.name = "太陽光暈";
        glow.transform.SetParent(transform);
        glow.transform.localPosition = Vector3.zero;
        glow.transform.localScale = Vector3.one * 30f;
        Destroy(glow.GetComponent<SphereCollider>());
        var glowMat = new Material(defaultPlanetMaterial);
        glowMat.color = new Color(1f, 0.9f, 0f, 0.08f);
        glowMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0f) * 0.5f);
        glowMat.EnableKeyword("_EMISSION");
        glow.GetComponent<MeshRenderer>().material = glowMat;

        var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sun.name = "太陽";
        sun.transform.SetParent(transform);
        sun.transform.localPosition = Vector3.zero;
        sun.transform.localScale = Vector3.one * 12f;
        Destroy(sun.GetComponent<SphereCollider>());
        var sunMat = new Material(defaultPlanetMaterial);
        sunMat.color = new Color(1f, 0.95f, 0.5f);
        sunMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.3f) * 2f);
        sunMat.EnableKeyword("_EMISSION");
        sun.GetComponent<MeshRenderer>().material = sunMat;
    }

    void SpawnSunAxisArrow(Transform parent, Vector3 dir, Color col)
    {
        var go = new GameObject("SunAxis_" + dir);
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, dir * 9f);
        lr.SetPosition(1, dir * 20f);
        lr.startWidth = 0.5f;
        lr.endWidth = 0.5f;
        var m = new Material(defaultPlanetMaterial);
        m.color = col;
        m.SetColor("_EmissionColor", col * 3f);
        m.EnableKeyword("_EMISSION");
        lr.material = m;

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Tip";
        tip.transform.SetParent(go.transform);
        tip.transform.localPosition = dir * 22f;
        tip.transform.localScale = Vector3.one * 2.2f;
        Destroy(tip.GetComponent<SphereCollider>());
        var tipMat = new Material(defaultPlanetMaterial);
        tipMat.color = col;
        tipMat.SetColor("_EmissionColor", col * 4f);
        tipMat.EnableKeyword("_EMISSION");
        tip.GetComponent<MeshRenderer>().material = tipMat;
    }

    void SpawnStars()
    {
        var stars = new GameObject("Stars");
        stars.transform.SetParent(transform);
        var ps = stars.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.maxParticles = 3000;
        main.startSize = 0.8f;
        main.startColor = new Color(0.9f, 0.9f, 1.0f, 0.7f);
        main.startLifetime = float.MaxValue;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3000) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 800f;
        var pr = stars.GetComponent<ParticleSystemRenderer>();
        if (pr != null && defaultPlanetMaterial != null)
            pr.material = new Material(defaultPlanetMaterial);
    }

    void SpawnPlanet(int i)
    {
        var data = planets[i];

        // Pivot
        var pivotGO = new GameObject(data.name + "_Pivot");
        pivotGO.transform.SetParent(transform);
        pivotGO.transform.localPosition = Vector3.zero;
        pivotGO.transform.localRotation = Quaternion.Euler(data.incl, 0f, 0f);

        // 軌道線
        var orbitGO = new GameObject(data.name + "_Orbit");
        orbitGO.transform.SetParent(pivotGO.transform);
        orbitGO.transform.localPosition = Vector3.zero;
        orbitGO.transform.localRotation = Quaternion.identity;
        orbitGO.transform.localScale = Vector3.one;
        var lr = orbitGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCornerVertices = 8;
        lr.numCapVertices = 8;
        lr.generateLightingData = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        float orbitWidth = Application.isMobilePlatform ? 1.25f : 0.85f;
        lr.widthCurve = AnimationCurve.EaseInOut(0f, orbitWidth, 1f, orbitWidth * 0.55f);
        lr.colorGradient = CreateOrbitGradient(data.color);
        lr.material = CreateOrbitMaterial(data.color);
        lr.positionCount = OrbitSegments + 1;
        for (int s = 0; s <= OrbitSegments; s++)
        {
            float a = (s / (float)OrbitSegments) * Mathf.PI * 2f;
            lr.SetPosition(s, new Vector3(Mathf.Cos(a) * data.dist, 0f, Mathf.Sin(a) * data.dist));
        }

        // 行星本體
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = data.name;
        go.transform.SetParent(pivotGO.transform);
        go.transform.localPosition = new Vector3(data.dist, 0f, 0f);
        go.transform.localScale = Vector3.one * data.size;
        Destroy(go.GetComponent<SphereCollider>());
        var mat = new Material(defaultPlanetMaterial);
        mat.color = data.color;
        mat.SetColor("_EmissionColor", data.color * 2f);
        mat.EnableKeyword("_EMISSION");
        go.GetComponent<MeshRenderer>().material = mat;

        // 光暈
        var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        halo.name = data.name + "_Halo";
        halo.transform.SetParent(go.transform);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localScale = Vector3.one * 2.0f;
        Destroy(halo.GetComponent<SphereCollider>());
        var haloMat = new Material(defaultPlanetMaterial);
        haloMat.color = new Color(data.color.r, data.color.g, data.color.b, 0.08f);
        halo.GetComponent<MeshRenderer>().material = haloMat;

        // 土星環
        if (data.name == "Saturn")
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "土星環";
            ring.transform.SetParent(go.transform);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localScale = new Vector3(2.5f, 0.04f, 2.5f);
            ring.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            Destroy(ring.GetComponent<CapsuleCollider>());
            var ringMat = new Material(defaultPlanetMaterial);
            ringMat.color = new Color(data.color.r, data.color.g, data.color.b, 0.5f);
            ring.GetComponent<MeshRenderer>().material = ringMat;
        }

        // PlanetOrbit — 直接設值，不讓 Inspector 覆蓋
        var orbit = go.AddComponent<PlanetOrbit>();
        orbit.planetName = data.name;
        orbit.dist = data.dist;
        orbit.baseSpeed = data.speed;
        orbit.inclination = data.incl;
        orbit.isActive = true;
        orbit.SetPivot(pivotGO.transform);
        orbit.SetStartAngle(i * (360f / planets.Length));
        orbit.SetOrbitLine(lr);

        Debug.Log($"生成 {data.name} | dist:{data.dist} | speed:{data.speed}");

        // 音效
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.spatialize = false;
        audioSource.dopplerLevel = 0f;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 1f;
        audioSource.minDistance = 9999f;
        audioSource.maxDistance = 10000f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        #if STEAMAUDIO_ENABLED
        var steamSrc = go.AddComponent<SteamAudio.SteamAudioSource>();
        steamSrc.distanceAttenuation = true;
        steamSrc.interpolation = SteamAudio.HRTFInterpolation.Bilinear;
        steamSrc.directivity = true;
        steamSrc.occlusion = true;
        #endif

        #if HIFI_HARP_SPATIALIZER
        var harpSpatializer = go.AddComponent<HiFiHarpSpatializer>();
        harpSpatializer.rirIndex = i;
        #endif

        var synth = go.AddComponent<FMSynthesizer>();
        synth.planetIndex = i + 1;
        synth.masterVolume = 1f;
        synth.Init(); // index 設好之後才初始化音色

        Planets.Add(orbit);
        Synths.Add(synth);
    }

    public Color GetPlanetColor(int idx)
    {
        if (idx < 0 || idx >= planets.Length) return Color.white;
        return planets[idx].color;
    }

    Material CreateOrbitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        var mat = new Material(shader);
        var lineColor = new Color(color.r, color.g, color.b, 0.62f);
        mat.color = lineColor;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", lineColor);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", color * 1.7f);
            mat.EnableKeyword("_EMISSION");
        }
        return mat;
    }

    Gradient CreateOrbitGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(color, Color.white, 0.2f), 0f),
                new GradientColorKey(color, 0.55f),
                new GradientColorKey(Color.Lerp(color, new Color(0.18f, 0.95f, 1f), 0.22f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.18f, 0f),
                new GradientAlphaKey(0.72f, 0.22f),
                new GradientAlphaKey(0.42f, 0.7f),
                new GradientAlphaKey(0.18f, 1f)
            });
        return gradient;
    }
}
