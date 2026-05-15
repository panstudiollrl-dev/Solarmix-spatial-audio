using UnityEngine;

public class PlanetOrbit : MonoBehaviour
{
    public enum TrajectoryType { Elliptical, Circular, Figure8, Lissajous, Rose }
    const int OrbitSegments = 384;

    [Header("行星基本資料")]
    public string planetName = "行星";
    public float dist = 50f;
    public float baseSpeed = 1f;
    public float inclination = 0f;
    public float accelFactor = 2.0f;
    public bool isActive = true;

    [Header("軌道類型")]
    public TrajectoryType trajectoryType = TrajectoryType.Circular;

    private float currentT = 0f;
    private Transform pivotTransform;
    private LineRenderer orbitLine;

    public void SetPivot(Transform pivot)
    {
        pivotTransform = pivot;
    }

    public void SetStartAngle(float degrees)
    {
        currentT = degrees * Mathf.Deg2Rad;
    }

    public void SetOrbitLine(LineRenderer lr)
    {
        orbitLine = lr;
        UpdateOrbitLine();
    }

    public void SetTrajectory(TrajectoryType type)
    {
        trajectoryType = type;
        UpdateOrbitLine();
    }

    void UpdateOrbitLine()
    {
        if (orbitLine == null) return;
        orbitLine.positionCount = OrbitSegments + 1;
        for (int s = 0; s <= OrbitSegments; s++)
        {
            float a = (s / (float)OrbitSegments) * Mathf.PI * 2f;
            orbitLine.SetPosition(s, GetTrajectoryPos(trajectoryType, a, dist));
        }
    }

    public void SetInclination(float deg)
    {
        inclination = deg;
        if (pivotTransform != null)
            pivotTransform.localRotation = Quaternion.Euler(inclination, 0f, 0f);
    }

    void Update()
    {
        if (!isActive) return;

        // Kepler Second Law: angular momentum conservation → ω ∝ r^-2
        // dt = baseSpeed * (dist/r)^2 — faster at perihelion, slower at aphelion
        float currentRadius = Mathf.Max(2f, Vector3.Distance(transform.position, pivotTransform.position));
        float dt = baseSpeed * Mathf.Pow(dist / currentRadius, accelFactor) * Time.deltaTime;

        // 每幀最大旋轉量保護，防止 HRTF 撕裂；Kepler r^-2 靠近太陽時加速明顯，上限略放寬
        dt = Mathf.Clamp(dt, -0.12f, 0.12f);

        currentT += dt;
        transform.localPosition = GetTrajectoryPos(trajectoryType, currentT, dist);
    }

    public Vector3 GetWorldPosition() => transform.position;

    public static Vector3 GetTrajectoryPos(TrajectoryType type, float t, float r)
    {
        float x = 0, y = 0, z = 0;
        switch (type)
        {
            case TrajectoryType.Circular:
                x = r * Mathf.Cos(t);
                z = r * Mathf.Sin(t);
                break;
            case TrajectoryType.Elliptical:
            {
                // e = 0.5 → perihelion = 0.5r (fast), aphelion = 1.5r (slow)
                // Sun at focus: x shifted by ae so origin is at the focus, not ellipse centre
                float ecc = 0.5f;
                float bAxis = 0.866f; // sqrt(1 - e²)
                x = r * (Mathf.Cos(t) - ecc);
                z = r * bAxis * Mathf.Sin(t);
                break;
            }
            case TrajectoryType.Figure8:
                float s = r / (1f + Mathf.Pow(Mathf.Sin(t), 2f));
                x = s * Mathf.Cos(t);
                z = s * Mathf.Sin(t) * Mathf.Cos(t);
                // 【安全修正】加上 2f 的高度偏移，避開絕對的零距離奇異點
                y = (r * 0.2f * Mathf.Sin(t * 2f)) + 2f;
                break;
            case TrajectoryType.Lissajous:
                x = r * Mathf.Sin(t * 3f);
                z = r * Mathf.Sin(t * 2f);
                y = r * 0.4f * Mathf.Cos(t * 4f);
                break;
            case TrajectoryType.Rose:
                float rr = r * Mathf.Cos(t * 4f);
                x = rr * Mathf.Cos(t);
                z = rr * Mathf.Sin(t);
                // 【安全修正】玫瑰星雲也會穿越中心，同樣給予微小的高度偏移
                y = 2f; 
                break;
        }
        return new Vector3(x, y, z);
    }
}
