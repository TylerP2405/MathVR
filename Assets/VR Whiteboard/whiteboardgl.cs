using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Haptics;
using Fusion;
using static WhiteBoardGL;
using System.Linq;
using Oculus.Interaction.Input;


//using BNG; // only needed if using VR Interaction Framework
public class WhiteBoardGL : NetworkBehaviour
{
    // Custom Haptic
    public HapticClip hapticClip;
    private HapticClipPlayer clipPlayer;
    private float frequency = 0.5f;
    private float amplitude = 0.2f;
    private float duration = 0.5f;

    //
    [Tooltip("The Render Texture to draw on")]
    public RenderTexture renderTexture;

    // Networked properties
    public struct DrawPoint : INetworkStruct
    {
        public Vector2 position;
        public Color color;
        public float sizeX;
        public float sizeY;
        public float rotationAngle;
    }
    // Buffer to reiceve drawing from other players
    private List<DrawPoint> drawBuffer = new List<DrawPoint>();

    // while rendering late draw, put new drawPoints to cache
    private Queue<DrawPoint> drawCache = new Queue<DrawPoint>();

    // store every drawing point that has been drawn
    private List<DrawPoint> drawHistory = new List<DrawPoint>();
    [Networked] 
    public int currentDrawCount { get; set; }      // Sync latest number of drawing 
    [Networked]
    public bool sentAllDrawHist { get; set; }      // if Host had sent all drawing points from Hist or not
    public bool processLateDraw = false;

    // Maximum budget for drawing per frame
    private int MAX_BUFFER_PER_UPDATE = 4000;

    // Check NetworkState of Brush object
    [Networked]
    public NetworkObject BrushGlobalNetObj { get; set; }


    // Menu and local properties
    [Header("BrushSettings")]
    [Tooltip("Max distance for brush detection")]
    public float maxDistance = 0.2f;
    [Tooltip("Minimum distance between brush positions")]
    public float minBrushDistance = 2f;

    private Material brushMaterial; // Material used for GL drawing

    public Color backGroundColor;
    [Range(0, 1)]
    public float markerAlpha = 0.7f;

    [Header("Collider Type")]
    [Tooltip("Set false to use a MeshCollider for 3d objects")]
    public bool useBoxCollider = true;

    // Define a Brush class to hold properties for each brush
    [System.Serializable]
    public class BrushSettings
    {
        public Grabbable brushGrabbable; //  Bruch Grabbable        
        public GrabInteractable grabInteractable;
        public Transform brushTransform;// Transform of the brush
        public NetworkObject brushNetObj;
        public Color color = Color.black;// Brush color
        public int sizeY = 20;// Brush size in pixels
        public int sizeX = 20;// Brush size in pixels
                              //public int segmentAmount = 60;// Segment amount for circular brushes

        public bool isEraser = false;

        [HideInInspector] public Vector2 lastPosition; // Last drawn position
        [HideInInspector] public bool isFirstDraw = true; // Flag for first draw
        [HideInInspector] public bool isDrawing = false;  // Whether the brush is in contact
    }

    [Header("Add Brushes")]
    public List<BrushSettings> brushes = new List<BrushSettings>(); // List to hold multiple brushes

    public override void Spawned()
    {
        Debug.Log("Starting whiteboard networked variables");
        // Start with assigned networked variables
        base.Spawned();

        // Set networked drawCount from Host
        if (HasStateAuthority)
        {
            currentDrawCount = 0;
        }

        // If this is a late joiner
        // process lateDraw
        Debug.LogWarning("local Hist count: " + drawHistory.Count + " | current count: " + currentDrawCount);
        if ((drawHistory.Count == 0) && (drawHistory.Count < currentDrawCount) && !HasStateAuthority)
        {
            Debug.LogWarning("Late joiner detected");
            // Request history from the authority
            processLateDraw = true;
            RPC_RequestDrawHistory(Runner.LocalPlayer);
        }
    }

    private void Start()
    {

        // Init haptic
        clipPlayer = new HapticClipPlayer(hapticClip);

        // Initialize the brush material with a simple shader
        brushMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));

        // Set the Render Texture as the main texture of the object's material
        GetComponent<Renderer>().material.mainTexture = renderTexture;

        // Clear the Render Texture at the start
        RenderTexture.active = renderTexture;
        // set the background color
        GL.Clear(true, true, backGroundColor);
        // set the brush color to match the backgroundColor if it is an eraser
        foreach (BrushSettings brush in brushes)
        {
            // set the alpha level of the markers
            brush.color.a = markerAlpha;

            if (brush.isEraser)
            {
                brush.color = backGroundColor;
            }
        }
        RenderTexture.active = null;
    }

    // running in update gives a marker smear breakup effect.. in URP its recommended to run in RenderPipelineManager.endCameraRendering or
    // RenderPipelineManager.endFrameRendering, these are call backs, this will give sharp lines that don't look as much like a marker. For SRP its recommended to run
    // in OnPostRender(), the same result.. you can play with that and try adding noise etc but I was not able to find something that looked better than just 
    // running in Update()...

 
    public void Update()
    {
        // Ensure the Render Texture is active for drawing
        RenderTexture.active = renderTexture;

        // Draw each brush on the texture
        foreach (var brush in brushes)
        {
            // check if the brush is being held to only run functions for the brushs being used
            // change this to a holding check with what ever framework you use, this is a check to make sure that only 
            // the marker you are holding is processing any functions for performance
            //if ((brush.grabInteractable.State == Oculus.Interaction.InteractableState.Select) && (brush.brushNetObj.HasStateAuthority))

            if (brush.brushGrabbable && (brush.brushGrabbable.SelectingPointsCount > 0) && (brush.brushNetObj.HasStateAuthority))
            {
                BrushGlobalNetObj = brush.brushNetObj;
                DrawBrushOnTexture(brush);
            }
        }


        // Draw from local buffer (drawPoints recieved from others players) with throttling
        if (drawBuffer.Count > 0)
        {
            if (processLateDraw) {
                // If process lateDraw, quadrupled the draw render allowed for catching up to date.
                MAX_BUFFER_PER_UPDATE = MAX_BUFFER_PER_UPDATE * 4;
            }
            int pointsToProcess = Mathf.Min(MAX_BUFFER_PER_UPDATE, drawBuffer.Count);

            // Only process up to MAX_BUFFER_PER_UPDATE commands
            for (int i = 0; i < pointsToProcess; i++)
            {
                DrawAtPosition(drawBuffer[i]);
            }

            // Remove only the processed commands
            drawBuffer.RemoveRange(0, pointsToProcess);

        }
        // After process all lateDraw buffer, process from cache
        else if (processLateDraw && drawBuffer.Count == 0 && drawCache.Count > 0)
        {
            Debug.Log("Processing cache left: " + drawCache.Count);
            int pointsToProcess = Mathf.Min(MAX_BUFFER_PER_UPDATE, drawCache.Count);

            // Only process up to MAX_BUFFER_PER_UPDATE commands
            for (int i = 0; i < pointsToProcess; i++)
            {
                var cmd = drawCache.Dequeue();
                DrawAtPosition(cmd);
            }
            // After processed all lateDraw
            if (drawCache.Count == 0 && sentAllDrawHist)
            {
                Debug.LogWarning("Stopped lateDraw with bool sent: " + sentAllDrawHist);
                processLateDraw = false;
            }
        }

        // Deactivate the Render Texture after drawing
        RenderTexture.active = null;
    }


    // Send draw points into other players' localBuffer
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_AddDrawPoint(DrawPoint cmd, NetworkObject drawerBrush)
    {
        // If player is not drawing, then recieve drawPoint
        if (!drawerBrush.HasStateAuthority)
        {
            // Add new drawPoints to cache if joined late
            if (processLateDraw)
            {
                drawCache.Enqueue(cmd);
            }
            else
            {
                Debug.Log("recieve draw points for player with authority: " + BrushGlobalNetObj.HasStateAuthority);
                drawBuffer.Add(cmd);
                currentDrawCount++;
            }
        }
    }

    // Recieved drawPoints for late Joiners
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SendDrawHistory(PlayerRef target, DrawPoint[] batch)
    {
        Debug.LogWarning("Recieving drawHist for all");
        // Only the joining player processes this
        if (Runner.LocalPlayer == target)
        {
            Debug.LogWarning("Recieving drawHist");
            foreach (var cmd in batch)
            {
                drawBuffer.Add(cmd);
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDrawHistory(PlayerRef target)
    {
        // Send history in batches to avoid large messages
        Debug.LogWarning("Sending DrawHist from Host");
        const int batchSize = 10;
        for (int i = 0; i < drawHistory.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, drawHistory.Count - i);
            DrawPoint[] batch = drawHistory.GetRange(i, count).ToArray();
            RPC_SendDrawHistory(target, batch);
        }
        sentAllDrawHist = true;
    }

    private void processSyncDraw(DrawPoint cmd, NetworkObject drawerBrush)
        // Function to streamline drawing and syncing with other players
    {
        DrawAtPosition(cmd);
        RPC_AddDrawPoint(cmd, drawerBrush);
        drawHistory.Add(cmd);
        currentDrawCount++;
    }

    private void DrawBrushOnTexture(BrushSettings brush)
    {

        if (brush.brushTransform == null) return; // null check in case a transfrom isn't assigned

        // ray cast from the brush tip transform
        Ray ray = new Ray(brush.brushTransform.position, brush.brushTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            // if the raycast from the brush is hitting this game object whick is the board
            if (hit.collider.gameObject == gameObject)
            {
                Vector2 uv;

                if (useBoxCollider)
                {
                    // Using BoxCollider
                    BoxCollider boxCollider = GetComponent<BoxCollider>();
                    if (boxCollider == null)
                    {
                        Debug.LogError("No BoxCollider found on this GameObject!");
                        return;
                    }

                    // Calculate the local hit point and normalize to UV
                    Vector3 localHitPoint = transform.InverseTransformPoint(hit.point); // Convert hit point to local space
                    uv = new Vector2(
                        (localHitPoint.x / boxCollider.size.x) + 0.5f,       // Normalize X position
                        1.0f - ((localHitPoint.y / boxCollider.size.y) + 0.5f) // Normalize and flip Y position
                    );
                }
                else
                {
                    // Using MeshCollider
                    uv = hit.textureCoord;
                    uv.y = 1.0f - uv.y; // Flip Y-axis
                }

                // Convert UV coordinates to texture space
                int x = (int)(uv.x * renderTexture.width);
                int y = (int)(uv.y * renderTexture.height);
                Vector2 currentPosition = new Vector2(x, y);

                ////////////////////////////////////////////
                // Convert to networked propertie to be sent
                var cmd = new DrawPoint
                {
                    position = currentPosition,
                    color = brush.color,
                    sizeX = brush.sizeX,
                    sizeY = brush.sizeY,
                    rotationAngle = brush.brushTransform.rotation.eulerAngles.z
                };
                // only draw when we need to by comparing current position to the last
                if (!brush.isDrawing)
                {
                    // Reset when the brush starts drawing again
                    brush.isFirstDraw = true;
                    brush.isDrawing = true;
                }

                if (brush.isFirstDraw)
                {
                    processSyncDraw(cmd, BrushGlobalNetObj);
                    brush.lastPosition = currentPosition;
                    brush.isFirstDraw = false;
                    return;
                }

                // Check if the texture space coordinates wrap around the edges,
                // this is so if you are drawing on a 3d object if you move from the left to right and up and down across the textures mapped edge
                // without it, it will draw a line all the way across the texture
                float deltaX = Mathf.Abs(currentPosition.x - brush.lastPosition.x);
                float deltaY = Mathf.Abs(currentPosition.y - brush.lastPosition.y);

                bool crossesHorizontalEdge = deltaX > renderTexture.width / 16; // Crosses left-right edge
                bool crossesVerticalEdge = deltaY > renderTexture.height / 16; // Crosses top-bottom edge

                if (crossesHorizontalEdge || crossesVerticalEdge)
                {
                    // If crossing an edge, do not interpolate. Just draw at the current position
                    processSyncDraw(cmd, BrushGlobalNetObj);
                }
                else
                {
                    // Interpolate between the last position and the current position
                    float distance = Vector2.Distance(currentPosition, brush.lastPosition);
                    int steps = Mathf.CeilToInt(distance / minBrushDistance);
                    for (int i = 1; i <= steps; i++)
                    {
                        Vector2 interpolatedPosition = Vector2.Lerp(brush.lastPosition, currentPosition, i / (float)steps);
                        cmd.position = interpolatedPosition;
                        processSyncDraw(cmd, BrushGlobalNetObj);
                    }
                }

                brush.lastPosition = currentPosition; // Update the last drawn position

                // Play haptic when drawing
                playHaptic(brush);
            }
        }
        else
        {
            // Stop drawing when the brush is no longer in contact
            brush.isDrawing = false;
        }
    }

    private void DrawAtPosition(DrawPoint cmd)
    {

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, renderTexture.width, renderTexture.height, 0);

        brushMaterial.SetPass(0);

        GL.Begin(GL.QUADS);
        // GL.Color(brush.color);
        GL.Color(cmd.color);

        // Convert rotation angle to radians
        float radians = cmd.rotationAngle * Mathf.Deg2Rad;

        // Calculate the rotation matrix components 
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        // Define the local offset vertices of the rectangle relative to the center
        Vector2[] vertices = new Vector2[4];
        vertices[0] = new Vector2(-cmd.sizeX, -cmd.sizeY); // Bottom-left
        vertices[1] = new Vector2(cmd.sizeX, -cmd.sizeY);  // Bottom-right
        vertices[2] = new Vector2(cmd.sizeX, cmd.sizeY);   // Top-right
        vertices[3] = new Vector2(-cmd.sizeX, cmd.sizeY);  // Top-left

        // Rotate  each vertex to match the brush rotation,
        // this is so you can have a brush that is wide and thin and the paint will match the rotation
        for (int i = 0; i < vertices.Length; i++)
        {
            float rotatedX = vertices[i].x * cos + vertices[i].y * sin; // Positive Y-axis rotation
            float rotatedY = -vertices[i].x * sin + vertices[i].y * cos; // Inverted sine for clockwise rotation

            // Add the position offset to align with the center
            GL.Vertex3(cmd.position.x + rotatedX, cmd.position.y + rotatedY, 0);
        }

        // populate the pixles 
        GL.End();
        GL.PopMatrix();
    }

    #region Haptic
    private void playHaptic(BrushSettings brush)
    {
        ControllerRef controllerRef = brush.grabInteractable.GetComponent<ControllerRef>();

        // Debug play
        clipPlayer.Play(Oculus.Haptics.Controller.Right);
        /*
        if (controllerRef)
        {
            if (controllerRef.Handedness == Handedness.Right)
                TriggerHaptics(OVRInput.Controller.RTouch);
            else
                TriggerHaptics(OVRInput.Controller.LTouch);

        }
        else
        {
            Debug.LogError("No controllerRef found");
        }
        */
    }
    public void TriggerHaptics(OVRInput.Controller controller)
    {
        if (hapticClip)
            if (controller == OVRInput.Controller.RTouch)
            {
                clipPlayer.Play(Oculus.Haptics.Controller.Right);
            }
            else if (controller == OVRInput.Controller.LTouch)
            {
                clipPlayer.Play(Oculus.Haptics.Controller.Left);
            }
            else
                StartCoroutine(TriggerHapticsRoutine(controller));
    }
    public IEnumerator TriggerHapticsRoutine(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
        yield return new WaitForSeconds(duration);
        OVRInput.SetControllerVibration(0, 0, controller);
    }
    #endregion
}

