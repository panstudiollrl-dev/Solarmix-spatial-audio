using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [Header("旋轉")]
    public float mouseSensitivity = 3f;
    public float touchSensitivity = 0.5f;

    [Header("縮放（僅滑鼠滾輪）")]
    public float minDistance = 80f;
    public float maxDistance = 1600f;
    public float scrollSpeed = 15f;

    private float currentDistance = 950f;
    public float ZoomDistance
    {
        get => currentDistance;
        set => currentDistance = Mathf.Clamp(value, minDistance, maxDistance);
    }
    private float rotX = 55f;
    private float rotY = 0f;
    private Vector3 target = Vector3.zero;

    // Fingers that started on UI — blocked for their entire gesture
    private readonly HashSet<int> uiFingers = new HashSet<int>();

    /// <summary>
    /// Set true by SolarSystemUI whenever any interactive panel is visible.
    /// Completely suppresses camera input — more reliable than per-frame raycasting
    /// against transparent UI backgrounds that have raycastTarget = false.
    /// </summary>
    public static bool Blocked = false;

    void Start()
    {
        // Force correct range regardless of Inspector values
        minDistance    = 40f;
        maxDistance    = 1600f;
        currentDistance = 950f;
        GetComponent<Camera>().farClipPlane = 3000f;
        UpdateCamera();
    }

    public void ResetView()
    {
        rotX = 55f;
        rotY = 0f;
        target = Vector3.zero;
        currentDistance = 950f;
        UpdateCamera();
    }

    void Update()
    {
        // When any UI panel is visible, ignore all camera input entirely.
        if (Blocked) { UpdateCamera(); return; }

        // ── Track fingers that started on UI ──────────────────────────────────
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began)
            {
                if (IsTouchOnUI(touch.position))
                    uiFingers.Add(touch.fingerId);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                uiFingers.Remove(touch.fingerId);
        }

        // 滑鼠拖曳旋轉（UI 上不觸發）
        if ((Input.GetMouseButton(0) || Input.GetMouseButton(1)) &&
            !EventSystem.current.IsPointerOverGameObject())
        {
            rotY += Input.GetAxis("Mouse X") * mouseSensitivity;
            rotX -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            rotX = Mathf.Clamp(rotX, 10f, 89f);
        }

        // 滾輪縮放（僅桌機，UI 上不觸發）
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f &&
            !EventSystem.current.IsPointerOverGameObject())
        {
            currentDistance -= scroll * scrollSpeed * 20f;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }

        // 單指旋轉 — 雙重封鎖：
        //   1. uiFingers：手指從 UI 元件上開始的整個手勢
        //   2. IsTouchOnUI：即時檢查（catch panels that appeared after touch began,
        //      or sliders/scrollviews whose raycast isn't caught at Began phase）
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved)
            {
                bool blocked = uiFingers.Contains(t.fingerId) || IsTouchOnUI(t.position);
                if (!blocked)
                {
                    rotY += t.deltaPosition.x * touchSensitivity;
                    rotX -= t.deltaPosition.y * touchSensitivity;
                    rotX = Mathf.Clamp(rotX, 10f, 89f);
                }
            }
        }

        UpdateCamera();
    }

    // Returns true if screenPos is currently over any UI element.
    // Called both on Began (to register uiFingers) and on every Moved frame
    // (to catch sliders / panels that may not have been hit at Began).
    bool IsTouchOnUI(Vector2 screenPos)
    {
        var pe   = new PointerEventData(EventSystem.current) { position = screenPos };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pe, hits);
        return hits.Count > 0;
    }

    void UpdateCamera()
    {
        Quaternion rot = Quaternion.Euler(rotX, rotY, 0f);
        transform.position = target + rot * new Vector3(0f, 0f, -currentDistance);
        transform.LookAt(target);
    }
}
