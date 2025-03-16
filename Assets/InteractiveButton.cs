using UnityEngine;

public class VRInteractiveButton : MonoBehaviour
{
    [Header("VR Controller Settings")]
    [SerializeField] private OVRInput.Controller rightHand = OVRInput.Controller.RTouch;
    [SerializeField] private OVRInput.Controller leftHand = OVRInput.Controller.LTouch;

    private Renderer buttonRenderer;
    private Color defaultColor;
    private WhiteboardCapture whiteboardCapture;

    private bool isPressed = false; // Track button press state

    void Start()
    {
        buttonRenderer = GetComponent<Renderer>();
        defaultColor = buttonRenderer.material.color;

        // Find the WhiteboardCapture component in the scene
        whiteboardCapture = FindObjectOfType<WhiteboardCapture>();

        if (whiteboardCapture == null)
        {
            Debug.LogError("Could not find a WhiteboardCapture component in the scene!");
        }
    }

    void Update()
    {
        // Perform a raycast from the right-hand controller
        if (GetControllerRaycast(rightHand, out RaycastHit hit))
        {
            // If the raycast hits this button and trigger is pressed
            if (hit.collider.gameObject == gameObject && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, rightHand))
            {
                OnButtonPressed();
            }
        }
    }

    /// <summary>
    /// Called when the button is pressed via VR controller
    /// </summary>
    void OnButtonPressed()
    {
        if (isPressed) return; // Prevent duplicate calls while the button is being held

        isPressed = true;
        Debug.Log("Solve Button Pressed!");

        // Change button color to indicate it's pressed
        buttonRenderer.material.color = Color.red;

        if (whiteboardCapture == null) return;

        // 1. Capture the whiteboard content as a byte array
        byte[] boardImage = whiteboardCapture.CaptureBoardToByteArray();

        if (boardImage != null)
        {
            Debug.Log("Chalkboard captured successfully! Byte array length: " + boardImage.Length);

            // 2. Save the captured image as a file
            whiteboardCapture.SaveBoardToFile("WhiteboardCapture.png");

            // 3. TODO: Send the byte[] to an AI solver API
            // Example:
            // StartCoroutine(MyApiHelper.SendToMathpix(boardImage));
        }
        else
        {
            Debug.LogError("Board image capture failed. Bytes are null.");
        }

        // Reset the button color after a delay
        Invoke(nameof(ResetColor), 2f);
    }

    void ResetColor()
    {
        buttonRenderer.material.color = defaultColor;
        isPressed = false; // Reset button state
    }

    /// <summary>
    /// Performs a raycast from the VR controller to detect objects.
    /// </summary>
    private bool GetControllerRaycast(OVRInput.Controller controller, out RaycastHit hit)
    {
        Vector3 position = OVRInput.GetLocalControllerPosition(controller);
        Quaternion rotation = OVRInput.GetLocalControllerRotation(controller);
        Vector3 direction = rotation * Vector3.forward;

        return Physics.Raycast(position, direction, out hit, 5f); // 5m range for interaction
    }
}
