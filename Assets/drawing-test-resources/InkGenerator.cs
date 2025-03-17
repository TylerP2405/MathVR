using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InkGenerator : MonoBehaviour
{
    // variable to store ink prefab
    [SerializeField] private GameObject inkPrefab;
    // variable to store pencil transform
    [SerializeField] private Transform pencilTransform;
    // variable to store transform of the paper
    [SerializeField] private Transform paperTransform;
    // variable to store pencil offset
    [SerializeField] private Vector3 pencilOffset;

    // variable to store ink component
    private InkTracer ink;
    // variable to capture the touch
    private bool isTouching = false;
    // variableto store the newly created ink
    private GameObject newInk = null;

    private void OnTriggerEnter(Collider otherObject)
    {
        if (otherObject.CompareTag("Paper"))
            isTouching = true;
    }

    private void OnTriggerExit(Collider otherObject)
    {
        if (otherObject.CompareTag("Paper"))
            isTouching = false;
    }

    private void Update()
    {
        // if the pencil is touching and ink is not there, instantiate ink and get its comnponent
        if (isTouching && newInk == null)
        {
            newInk = Instantiate(inkPrefab);
            ink = newInk.GetComponent<InkTracer>();
        }
        // if the pencil is touching and there is ink, the update the ink as per the pencil and paper position
        if (isTouching && newInk != null)
        {
            Vector3 pos = new Vector3(pencilTransform.position.x, paperTransform.position.y, pencilTransform.position.z);
            ink.UpdateLineRenderer(pos + pencilOffset);
        }
        // if the pencil is not touching the paper,set the variable to null
        if (!isTouching)
            newInk = null;
    }
}