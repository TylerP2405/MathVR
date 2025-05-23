/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/*
using UnityEngine;
using Oculus.Interaction.Input;
using UnityEngine.Assertions;
using Oculus.Interaction;

public class FilteredGrabbingInput: MonoBehaviour, ITransformer
{

    [SerializeField]
    private GrabFreeTransformer _underlyingTransformer;

    [SerializeField]
    private Transform _targetTransform;

    [SerializeField]
    private Transform _underlyingTargetTransform;

    [SerializeField]
    private Transform _sourceTransform;

    [SerializeField]
    private bool _filterPosition;

    [SerializeField]
    private OneEuroFilterPropertyBlock _positionFilterProperties =
        new OneEuroFilterPropertyBlock(2f, 3f);

    [SerializeField]
    private bool _filterRotation;

    [SerializeField]
    private OneEuroFilterPropertyBlock _rotationFilterProperties =
        new OneEuroFilterPropertyBlock(2f, 3f);

    private IOneEuroFilter<Vector3> _positionFilter;
    private IOneEuroFilter<Quaternion> _rotationFilter;

    public void Initialize(IGrabbable grabbable)
    {
        _underlyingTransformer.Initialize(grabbable);
        _positionFilter = OneEuroFilter.CreateVector3();
        _rotationFilter = OneEuroFilter.CreateQuaternion();
    }

    public void BeginTransform() {
        _underlyingTransformer.BeginTransform();
    }

    public void EndTransform()
    {
        _underlyingTransformer.EndTransform();
    }

    public void UpdateTransform()
    {
        _underlyingTransformer.UpdateTransform();

        _targetTransform.position = Vector3.Lerp(_targetTransform.position, _underlyingTargetTransform.position, _filterStrength);
        _targetTransform.rotation = Quaternion.Slerp(_targetTransform.rotation, _underlyingTargetTransform.rotation, _filterStrength);
    }
    
    protected virtual void Update()
    {
        if (_filterPosition)
        {
            Vector3 position = _sourceTransform.position;
            _positionFilter.SetProperties(_positionFilterProperties);
            transform.position =
                _positionFilter.Step(_sourceTransform.position, Time.deltaTime);
        }
        else
        {
            transform.position = _sourceTransform.position;
        }

        if (_filterRotation)
        {
            _rotationFilter.SetProperties(_rotationFilterProperties);
            transform.rotation =
                _rotationFilter.Step(_sourceTransform.rotation, Time.deltaTime);
        }
        else
        {
            transform.rotation = _sourceTransform.rotation;
        }
    }
}
*/