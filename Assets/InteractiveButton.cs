using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class MathpixResponse
{
    public string text;
    public string latex_styled;
    public float confidence;
}

public class VRInteractiveButton : MonoBehaviour
{
    [Header("VR Controller Settings")]
    [SerializeField] private OVRInput.Controller rightHand = OVRInput.Controller.RTouch;
    [SerializeField] private OVRInput.Controller leftHand = OVRInput.Controller.LTouch;

    private Renderer buttonRenderer;
    private Color defaultColor;
    private WhiteboardCapture whiteboardCapture;

    [Header("Mathpix API Settings")]
    [SerializeField] private string appId = "mathvr_cb7ea5_cb157f";   
    [SerializeField] private string appKey = "4d52d29ed99f7cd04f4382adcf5ff0be75df806dda0eeb4c9cfe0cc045827b72"; 

    [Header("Gemini API Settings")]
    [SerializeField] private string geminiApiKey = "AIzaSyDeYukkUW8P1HvUxy4M1pQ3toV2l5NxPlU";


    private bool isPressed = false;

    void Start()
    {
        buttonRenderer = GetComponent<Renderer>();
        defaultColor = buttonRenderer.material.color;

        whiteboardCapture = FindObjectOfType<WhiteboardCapture>();
        if (whiteboardCapture == null)
        {
            Debug.LogError("Could not find a WhiteboardCapture component in the scene!");
        }
    }

    void Update()
    {
        if (GetControllerRaycast(rightHand, out RaycastHit hit))
        {
            if (hit.collider.gameObject == gameObject && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, rightHand))
            {
                OnButtonPressed();
            }
        }
    }

    void OnButtonPressed()
    {
        if (isPressed) return;
        isPressed = true;

        Debug.Log("Solve Button Pressed!");
        buttonRenderer.material.color = Color.red;

        if (whiteboardCapture == null) return;

        byte[] boardImage = whiteboardCapture.CaptureBoardToByteArray();

        if (boardImage != null)
        {
            Debug.Log("Chalkboard captured. Byte array length: " + boardImage.Length);
            whiteboardCapture.SaveBoardToFile("WhiteboardCapture.png");

            StartCoroutine(SendImageToMathpix(boardImage));
        }
        else
        {
            Debug.LogError("Board image capture failed. Bytes are null.");
        }

        Invoke(nameof(ResetColor), 2f);
    }

    void ResetColor()
    {
        buttonRenderer.material.color = defaultColor;
        isPressed = false;
    }

    private bool GetControllerRaycast(OVRInput.Controller controller, out RaycastHit hit)
    {
        Vector3 position = OVRInput.GetLocalControllerPosition(controller);
        Quaternion rotation = OVRInput.GetLocalControllerRotation(controller);
        Vector3 direction = rotation * Vector3.forward;

        return Physics.Raycast(position, direction, out hit, 5f);
    }

    private IEnumerator SendImageToMathpix(byte[] imageBytes)
{
    string mathpixUrl = "https://api.mathpix.com/v3/text";

    WWWForm form = new WWWForm();
    form.AddBinaryData("file", imageBytes, "whiteboard.png", "image/png");

    string optionsJson = "{\"math_inline_delimiters\": [\"$\", \"$\"], \"rm_spaces\": true, \"formats\": [\"text\"]}";
    form.AddField("options_json", optionsJson);

    UnityWebRequest request = UnityWebRequest.Post(mathpixUrl, form);
    request.SetRequestHeader("app_id", appId);    
    request.SetRequestHeader("app_key", appKey);   

    Debug.Log("Sending request to Mathpix...");

    yield return request.SendWebRequest();

    if (request.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError("Mathpix API Error: " + request.error);
    }
    else
    {
        string response = request.downloadHandler.text;
        Debug.Log("Mathpix Response:\n" + response);
        MathpixResponse parsed = JsonUtility.FromJson<MathpixResponse>(response);

        if (parsed != null)
        {
            string latex = parsed.latex_styled?.Replace("\\\\", "\\") ?? "";
            float confidence = parsed.confidence;

            Debug.Log("Extracted LaTeX: " + latex);
            Debug.Log($"Mathpix Confidence: {confidence * 100f:0.00}%");

            //Uncomment for gemini, but need to test mathpix first
            //StartCoroutine(SendLatexToGemini(latex));
        }
        else
        {
            Debug.LogError("Failed to parse Mathpix response.");
        }
    }
}

private IEnumerator SendLatexToGemini(string latex)
{
    string geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={geminiApiKey}";

    string prompt = $"Solve this problem step by step and make sure the step by step is not too long:\n\n{latex}";

    string jsonBody = "{\"contents\": [{\"parts\": [{\"text\": \"" + EscapeJson(prompt) + "\"}]}]}";

    UnityWebRequest request = new UnityWebRequest(geminiUrl, "POST");
    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
    request.downloadHandler = new DownloadHandlerBuffer();

    request.SetRequestHeader("Content-Type", "application/json");

    Debug.Log("Sending LaTeX to Gemini...");
    yield return request.SendWebRequest();

    if (request.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError("Gemini API Error: " + request.error);
    }
    else
    {
        string result = request.downloadHandler.text;
        Debug.Log("Gemini Response:\n" + result);
    }
}
 private string EscapeJson(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
