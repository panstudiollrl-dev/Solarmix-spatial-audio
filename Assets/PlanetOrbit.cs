using UnityEngine;

public class SphereOrbit : MonoBehaviour
{
    [Header("繞行目標")]
    public Transform centerTarget; // 拖一個物件進來當圓心，留空則用世界原點

    [Header("軌道設定")]
    public float radiusX = 5f;   // X 軸半徑
    public float radiusZ = 5f;   // Z 軸半徑（與 radiusX 不同就是橢圓）
    public float speed = 1f;     // 每秒繞幾弧度（約 1 = 6 秒一圈）
    public float inclination = 0f; // 軌道傾角（度）

    [Header("初始角度")]
    public float startAngle = 0f; // 起始角度（度）

    private float currentAngle;

    void Start()
    {
        currentAngle = startAngle * Mathf.Deg2Rad;
    }

    void Update()
    {
        currentAngle += speed * Time.deltaTime;

        // 計算軌道上的位置
        float x = radiusX * Mathf.Cos(currentAngle);
        float z = radiusZ * Mathf.Sin(currentAngle);
        float y = 0f;

        // 套用傾角（繞 X 軸旋轉）
        Vector3 orbitPos = Quaternion.Euler(inclination, 0f, 0f) * new Vector3(x, y, z);

        // 加上圓心位置
        Vector3 center = centerTarget != null ? centerTarget.position : Vector3.zero;
        transform.position = center + orbitPos;
    }
}