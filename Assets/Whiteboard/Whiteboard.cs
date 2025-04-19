using System.Collections.Generic;
using UnityEngine;
using Oculus.Platform;              // If you’re using Oculus Platform
using Oculus.Platform.Models;      // If you’re using Oculus Platform
using UnityEngine.XR;
using UnityEngine.InputSystem;      // Optional if you’re using the new InputSystem

public class Whiteboard : MonoBehaviour
{
    [Header("XR Controller Settings")]
    [SerializeField] private OVRInput.Controller rightHand = OVRInput.Controller.RTouch;
    [SerializeField] private OVRInput.Controller leftHand = OVRInput.Controller.LTouch;

    [Header("Textures")]
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Texture2D brushTexture;
    [SerializeField] private Texture2D eraserTexture;

    [Header("Brush Settings")]
    [SerializeField] private Color brushColor = Color.black;
    [SerializeField] private float brushSize = 15.0f;
    [SerializeField] private float smoothSteps = 800f;

    [Header("Eraser Settings")]
    [SerializeField] private GameObject eraserMesh;

    [Header("Marker Settings")]
    [SerializeField] private Material baseMarkerMaterial;
    [SerializeField] private List<Color> colors;            // 0..n color markers
    [SerializeField] private List<GameObject> displayMarkers; // Physical markers on the pen rack
    [SerializeField] private GameObject heroMarker;          // The pen tip that follows your controller

    [Header("Other Settings")]
    [SerializeField] private GameObject rag;               // The "rag" or "eraser" object on the board
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private float hoverOffset = 0.02f;
    [SerializeField] private float markerSmoothingSpeed = 20f;

    // Internal references
    private Material drawMaterial;
    private MeshRenderer heroMarkerRenderer;
    private Vector2? previousUV = null;

    private int selectedMarker = 0; // Which marker index is selected (0..colors.Count-1, 5 = eraser, etc.)

    private void Start()
    {
        heroMarkerRenderer = heroMarker.GetComponent<MeshRenderer>();
        InitializeRenderTexture();
        InitializeDisplayMarkers();
        ChangeMarkers(0); // Start with index 0 by default
    }

    /// <summary>
    /// Merges the VR logic and the first-script behavior:
    /// 1) Raycast from your right-hand controller.
    /// 2) If it hits something, check if it's a marker, rag, or board.
    /// 3) Act accordingly (select marker/eraser/rag or draw).
    /// 4) Show/hide the hero marker or eraser mesh.
    /// </summary>
    private void Update()
    {
        // 1) Raycast from your VR controller
        if (GetControllerPosition(rightHand, out Vector3 controllerPos, out Vector3 controllerDir))
        {
            Ray ray = new Ray(controllerPos, controllerDir);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {

                GameObject hitObject = hit.collider.gameObject;

                // 2) Check if we’re hitting any of the marker objects or the rag
                //    and if the user pulls the trigger *down* this frame
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, rightHand))
                {
                    // Check if we hit a marker in displayMarkers
                    for (int i = 0; i < displayMarkers.Count; i++)
                    {
                        if (hitObject == displayMarkers[i])
                        {
                            ChangeMarkers(i);
                            return; // We can return here because we've selected a new marker
                        }
                    }

                    // Check if we hit the rag
                    if (hitObject == rag)
                    {
                        ClearRenderTexture();
                        return; // We can return here because we just cleared the board
                    }
                }

                // 3) If we’re hitting the whiteboard itself
                if (hitObject == this.gameObject)
                {
                    // Move or show the marker/eraser (even when hovering)
                    UpdateMarkerOrEraserPosition(hit);

                    // If user is holding the trigger for drawing/erasing
                    if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, rightHand))
                    {
                        // Attempt to get UV coords for the board
                        if (TryGetUVCoordinates(hit, out Vector2 uv))
                        {
                            if (previousUV.HasValue)
                            {
                                DrawBetween(previousUV.Value, uv);
                            }
                            else
                            {
                                Draw(uv);
                            }
                            previousUV = uv;
                        }
                    }
                    else
                    {
                        // Not drawing, reset previousUV so lines don’t connect
                        previousUV = null;
                    }
                }
                else
                {
                    // We’re hitting something else that’s not the board
                    // Hide the hero marker or move it to the marker if it’s a marker object
                    // but we only handle marker selection on GetDown
                    HideMarkerAndEraser();
                }
            }
            else
            {
                // Raycast didn’t hit anything, hide everything
                HideMarkerAndEraser();
                previousUV = null;
            }
        }
    }

    /// <summary>
    /// Same logic from first script: changes marker color, toggles brush or eraser,
    /// shows/hides hero marker or eraser mesh, etc.
    /// </summary>
    void ChangeMarkers(int index = 0)
    {
        selectedMarker = index;
        for (int i = 0; i < displayMarkers.Count; i++)
        {
            if (i == selectedMarker)
                displayMarkers[i].SetActive(false); // Hide the selected marker
            else
                displayMarkers[i].SetActive(true);  // Show the unselected markers
        }

        // If your design has the eraser as the last index (like i=5 in the original),
        // you can do: if (index == 5) { ... } else { ... }
        // Just ensure your `displayMarkers.Count` includes the eraser in the right slot.
        if (index == 5) // Eraser
        {
            brushColor = Color.white;
            brushSize = 150f;
            drawMaterial.SetTexture("_MainTex", eraserTexture);

            heroMarker.SetActive(false);
            eraserMesh.SetActive(true);
        }
        else
        {
            brushColor = colors[index];
            brushSize = 15f;
            drawMaterial.SetTexture("_MainTex", brushTexture);

            Material markerMaterialInstance = new Material(baseMarkerMaterial);
            markerMaterialInstance.color = colors[index];
            heroMarkerRenderer.material = markerMaterialInstance;

            heroMarker.SetActive(true);
            eraserMesh.SetActive(false);
        }
    }

    /// <summary>
    /// Called whenever we’re hovering or drawing on the whiteboard.
    /// Moves the hero marker or eraser to the correct position.
    /// </summary>
    private void UpdateMarkerOrEraserPosition(RaycastHit hit)
    {
        // The exact position on the board surface
        Vector3 targetPos = hit.point + hit.normal * hoverOffset;

        if (selectedMarker == 5)
        {
            // Eraser
            eraserMesh.SetActive(true);
            // Smoothly move the eraser
            eraserMesh.transform.position = Vector3.Lerp(eraserMesh.transform.position,
                                                         targetPos,
                                                         Time.deltaTime * markerSmoothingSpeed);
            heroMarker.SetActive(false);
        }
        else
        {
            // Marker
            heroMarker.SetActive(true);
            heroMarker.transform.position = Vector3.Lerp(heroMarker.transform.position,
                                                         targetPos,
                                                         Time.deltaTime * markerSmoothingSpeed);
            eraserMesh.SetActive(false);
        }
    }
    /// <summary>
    /// Hide both the pen tip and the eraser meshes completely.
    /// </summary>
    private void HideMarkerAndEraser()
    {
        heroMarker.SetActive(false);
        eraserMesh.SetActive(false);
    }

    #region VR Utility Methods

    private bool GetControllerPosition(OVRInput.Controller controller, out Vector3 position, out Vector3 direction)
    {
        // Replace these with your actual tracking transforms if needed.
        // By default, OVRInput.GetLocalControllerPosition/Rotation returns local-to-centerEye coords.
        // You might want a real-world position from the OVRCameraRig’s anchor transforms.
        position = OVRInput.GetLocalControllerPosition(controller);
        Quaternion rotation = OVRInput.GetLocalControllerRotation(controller);
        direction = rotation * Vector3.forward;
        return true; // If you want error checking, do it here
    }

    #endregion

    #region Drawing Logic (same as first script)

    void InitializeRenderTexture()
    {
        if (renderTexture == null)
        {
            Debug.LogError("RenderTexture was not assigned.");
            return;
        }

        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.white);
        RenderTexture.active = null;

        Shader drawShader = Shader.Find("Custom/DrawShader");
        if (drawShader == null)
        {
            Debug.LogError("Custom DrawShader not found.");
            return;
        }

        drawMaterial = new Material(drawShader);
        drawMaterial.SetTexture("_MainTex", brushTexture);
    }

    void InitializeDisplayMarkers()
    {
        // For every marker except the eraser index, set the colored material
        for (int i = 0; i < displayMarkers.Count; i++)
        {
            // If index 5 is your eraser, skip coloring
            if (i == 5) continue;

            MeshRenderer renderer = displayMarkers[i].GetComponent<MeshRenderer>();
            Material markerMaterialInstance = new Material(baseMarkerMaterial);
            markerMaterialInstance.color = colors[i];
            renderer.material = markerMaterialInstance;
        }
    }

    void ClearRenderTexture()
    {
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.white);
        RenderTexture.active = null;
    }

    bool TryGetUVCoordinates(RaycastHit hit, out Vector2 uv)
    {
        uv = Vector2.zero;
        MeshCollider meshCollider = hit.collider as MeshCollider;
        if (meshCollider == null || meshCollider.sharedMesh == null)
            return false;

        Mesh mesh = meshCollider.sharedMesh;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;

        int triangleIndex = hit.triangleIndex * 3;
        Vector3 p0 = vertices[triangles[triangleIndex + 0]];
        Vector3 p1 = vertices[triangles[triangleIndex + 1]];
        Vector3 p2 = vertices[triangles[triangleIndex + 2]];

        Vector2 uv0 = uvs[triangles[triangleIndex + 0]];
        Vector2 uv1 = uvs[triangles[triangleIndex + 1]];
        Vector2 uv2 = uvs[triangles[triangleIndex + 2]];

        Vector3 barycentric = hit.barycentricCoordinate;
        uv = uv0 * barycentric.x + uv1 * barycentric.y + uv2 * barycentric.z;
        return true;
    }

    void Draw(Vector2 textureCoord)
    {
        RenderTexture.active = renderTexture;
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, renderTexture.width, renderTexture.height, 0);

        Vector2 brushPos = new Vector2(textureCoord.x * renderTexture.width,
                                       (1 - textureCoord.y) * renderTexture.height);

        // If selectedMarker == 5 => eraser
        if (selectedMarker == 5)
            drawMaterial.SetTexture("_MainTex", eraserTexture);
        else
            drawMaterial.SetTexture("_MainTex", brushTexture);

        drawMaterial.SetColor("_Color", brushColor);
        drawMaterial.SetPass(0);

        Rect rect = new Rect(brushPos.x - brushSize / 2,
                             brushPos.y - brushSize / 2,
                             brushSize, brushSize);

        Graphics.DrawTexture(rect, drawMaterial.GetTexture("_MainTex"), drawMaterial);
        GL.PopMatrix();
        RenderTexture.active = null;
    }

    void DrawBetween(Vector2 startUV, Vector2 endUV)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(startUV, endUV) * smoothSteps);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 interpolatedUV = Vector2.Lerp(startUV, endUV, (float)i / steps);
            Draw(interpolatedUV);
        }
    }

    #endregion
}
