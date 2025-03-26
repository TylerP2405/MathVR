using Oculus.Interaction;
using UnityEngine;

public class FilteredTransformer : MonoBehaviour, ITransformer
{
    [SerializeField]
    private GrabFreeTransformer _underlyingTransformer;

    [SerializeField]
    private Transform _targetTransform;

    [SerializeField]
    private Transform _underlyingTargetTransform;

    [SerializeField, Range(0f, 1f)]
    private float _filterStrength = 0.05f;

    public void BeginTransform()
    {
        _underlyingTransformer.BeginTransform();
    }

    public void EndTransform()
    {
        _underlyingTransformer.EndTransform();
    }

    public void Initialize(IGrabbable grabbable)
    {
        _underlyingTransformer.Initialize(grabbable);
    }

    public void UpdateTransform()
    {
        _underlyingTransformer.UpdateTransform();

        _targetTransform.position = Vector3.Lerp(_targetTransform.position, _underlyingTargetTransform.position, _filterStrength);
        _targetTransform.rotation = Quaternion.Slerp(_targetTransform.rotation, _underlyingTargetTransform.rotation, _filterStrength);
    }
}