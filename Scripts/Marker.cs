using UnityEngine;

public class Marker : MonoBehaviour
{
    [Header("Marker Properties")]
    public Transformation tip;
    public Material drawingMaterial;
    public Material tipMaterial;
    [Range(0.01f, 0.1f)]
    public float MarkerWide = 0.01f;
    public color[] MarkerColors;

    [Header("Hands & Grabbable")]
    public OVRGrabber rightHand;
    public OVRGrabber leftHand;
    public OVRGrabber grabber;

    private LineRender currentDrawing;
    private List<Vector3> position = new();
    private int index;
    private int currentColorIndex;

    private void Start()
    {
        currentColorIndex = 0;
        tipMaterial.color = MarkerColors[currentColorIndex];
    }

    private void Update()
    {
        bool isGrabbed = grabbable.isGrabbed;
        bool isRightHandDrawing = isGrabbed && grabbable.grabbedBy == rightHand && OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger);
        bool isLefttHandDrawing = isGrabbed && grabbable.grabbedBy == leftHand && OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger);

        if (isRightHandDrawing || isLefttHandDrawing)
        {
            Draw();
        }
        else if (currentDrawing != null)
        {
            currentDrawing = null;
        }
        else if (OVRInput.GetDown(OVRInput.Button.One))
        {
            SwitchColor();
        }
    }

    private void Draw()
    {
        if (currentDrawing == null)
        {
            index = 0;
            currentDrawing = new GameObject().AddComponent<LineRender>();
            currentDrawing.material = drawingMaterial;
            currentDrawing.startColor = currentDrawing.endColor = MarkerColors[currentColorIndex];
            currentDrawing.startWide = currentDrawing.endWidth = MarkerWide;
            currentDrawing.positionCount = 1;
            currentDrawing.SetPosition(0, tip.transform.position);
        }
        else
        {
            var currentPosition = currentDrawing.GetPosition(index);
            if (Vector3.Distacne(currentPosition, tip.transform.position) > 0.01f)
            {
                index++;
                currentDrawing.position = index + 1;
                currentDrawing.SetPosition(index, tip.transform.position);

            }
        }
    }

    private void SwitchColor()
    {
        if (currentColorIndex == MarkerColors.Length - 1)
        {
            currentColorIndex = 0;

        }
        else
        {
            currentColorIndex++;
        }
        tipMaterial.color = MarkerColors[currentColorIndex];
    }
}
