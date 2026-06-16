using UnityEngine;

/// <summary>
/// Camera controller that focuses on each exhibit object in sequence.
/// Arrow keys / A-D cycle between objects; W-S zoom; mouse drag orbits.
/// </summary>
public class SceneNavigator : MonoBehaviour
{
    [Header("Targets")]
    public Transform[] exhibitTargets;
    public string[]    exhibitNames;
    public float       focusDistance  = 5f;
    public float       transitionTime = 0.8f;

    [Header("Orbit")]
    public float orbitSpeed  = 120f;
    public float zoomSpeed   = 4f;
    public float minDistance = 1.5f;
    public float maxDistance = 12f;

    // --- runtime ---
    int     _current  = 0;
    Vector3 _targetPos;
    Quaternion _targetRot;
    float   _t         = 1f;          // transition progress [0-1]
    Vector3 _fromPos;
    Quaternion _fromRot;
    float   _orbitYaw  = 0f;
    float   _orbitPitch= 20f;
    float   _currentDist;
    bool    _dragging  = false;
    Vector3 _lastMouse;

    // GUI skin-less label
    GUIStyle _labelStyle;

    void Start()
    {
        _currentDist = focusDistance;
        if (exhibitTargets != null && exhibitTargets.Length > 0)
            SnapToTarget(_current);
    }

    void Update()
    {
        HandleInput();
        UpdateTransition();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            NavigateTo((_current + 1) % exhibitTargets.Length);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            NavigateTo((_current - 1 + exhibitTargets.Length) % exhibitTargets.Length);

        // zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _currentDist = Mathf.Clamp(_currentDist - scroll * zoomSpeed * 2f, minDistance, maxDistance);

        if (Input.GetKeyDown(KeyCode.W)) _currentDist = Mathf.Max(minDistance, _currentDist - 0.5f);
        if (Input.GetKeyDown(KeyCode.S)) _currentDist = Mathf.Min(maxDistance,  _currentDist + 0.5f);

        // mouse orbit
        if (Input.GetMouseButtonDown(1)) { _dragging = true; _lastMouse = Input.mousePosition; }
        if (Input.GetMouseButtonUp(1))   _dragging = false;
        if (_dragging)
        {
            Vector3 delta = Input.mousePosition - _lastMouse;
            _orbitYaw   += delta.x * orbitSpeed * Time.deltaTime;
            _orbitPitch -= delta.y * orbitSpeed * Time.deltaTime;
            _orbitPitch  = Mathf.Clamp(_orbitPitch, -80f, 80f);
            _lastMouse   = Input.mousePosition;
        }

        // live orbit around current target
        if (_t >= 1f && exhibitTargets != null && exhibitTargets.Length > 0)
        {
            Transform tgt = exhibitTargets[_current];
            Vector3 dir = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f) * Vector3.forward;
            transform.position = tgt.position - dir * _currentDist;
            transform.LookAt(tgt.position + Vector3.up * 0.5f);
        }
    }

    void UpdateTransition()
    {
        if (_t >= 1f) return;
        _t = Mathf.Min(_t + Time.deltaTime / transitionTime, 1f);
        float e = Ease(_t);
        transform.position = Vector3.Lerp(_fromPos, _targetPos, e);
        transform.rotation = Quaternion.Slerp(_fromRot, _targetRot, e);
    }

    void NavigateTo(int index)
    {
        _current = index;
        _fromPos = transform.position;
        _fromRot = transform.rotation;
        _t       = 0f;

        Transform tgt = exhibitTargets[_current];
        Vector3 dir = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f) * Vector3.forward;
        _targetPos = tgt.position - dir * _currentDist;
        _targetRot = Quaternion.LookRotation(tgt.position + Vector3.up * 0.5f - _targetPos);
    }

    void SnapToTarget(int index)
    {
        Transform tgt = exhibitTargets[index];
        Vector3 dir   = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f) * Vector3.forward;
        transform.position = tgt.position - dir * _currentDist;
        transform.LookAt(tgt.position + Vector3.up * 0.5f);
    }

    float Ease(float t) => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

    void OnGUI()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = Color.white;
        }

        string name = (exhibitNames != null && _current < exhibitNames.Length)
            ? exhibitNames[_current] : $"Object {_current + 1}";

        int total = exhibitTargets != null ? exhibitTargets.Length : 0;

        GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 30),
            $"[ {_current + 1} / {total} ]  —  {name}", _labelStyle);
        GUI.Label(new Rect(0, Screen.height - 60, Screen.width, 25),
            "← → Navigate     Scroll / W·S Zoom     RMB Drag Orbit", _labelStyle);
    }
}
