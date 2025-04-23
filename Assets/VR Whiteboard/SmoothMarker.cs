using UnityEngine;

/// <summary>
/// Attach this to your RayCastTransform (child of Marker).
/// It will smoothly follow its parent using configurable smoothing.
/// </summary>
public class SmoothRayCastTransform : MonoBehaviour
{
    [Header("Smoothing (0 = no smoothing, 0.9 = very smooth/laggy)")]
    [Range(0f, 0.95f)] public float positionSmoothing = 0.7f;
    [Range(0f, 0.95f)] public float rotationSmoothing = 0.7f;

    private Vector3 _smoothedPosition;
    private Quaternion _smoothedRotation;
    private bool _initialized = false;

    void LateUpdate()
    {
        Transform target = transform.parent;
        if (target == null) return;

        if (!_initialized)
        {
            _smoothedPosition = target.position;
            _smoothedRotation = target.rotation;
            _initialized = true;
        }
        else
        {
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, target.position, 1f - positionSmoothing);
            _smoothedRotation = Quaternion.Slerp(_smoothedRotation, target.rotation, 1f - rotationSmoothing);
        }

        transform.position = _smoothedPosition;
        transform.rotation = _smoothedRotation;
    }
}
