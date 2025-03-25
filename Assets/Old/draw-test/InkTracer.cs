using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(LineRenderer))]
public class InkTracer : MonoBehaviour
{
    private LineRenderer lineRenderer = null; // Variable to store the LineRenderer component
    private List<Vector3> points = new List<Vector3>(); // List to store the points of the ink stroke
    [SerializeField] private float drawingThreshold = 0.001f; // Threshold for determining if a new point should be added to the line renderer

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>(); // Get the LineRenderer component attached to this object
    }

    public void UpdateLineRenderer(Vector3 newPosition)
    {
        if (IsUpdateRequired(newPosition)) // Check if adding a new point is required based on the drawing threshold
        {
            points.Add(newPosition); // Add the new position to the list of points
            lineRenderer.positionCount = points.Count; // Set the position count of the LineRenderer to match the number of points
            lineRenderer.SetPosition(points.Count - 1, newPosition); // Set the position of the last added point in the LineRenderer
        }
    }

    private bool IsUpdateRequired(Vector3 position)
    {
        if (points.Count == 0)
            return true; // Check if there are no points yet
        return Vector3.Distance(points[points.Count - 1], position) > drawingThreshold; // Check if the distance between the last point and the new position is greater than the drawing threshold
    }
}
