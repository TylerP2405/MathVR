using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Compression;
using System.Text;



[System.Serializable]
public class MathpixWord
{
    public string type;
    public string text;
    public string latex;
}

[System.Serializable]
public class MathpixResponse
{
    public string text;
    public string latex_styled;
    public float confidence;
    public List<MathpixWord> word_data;
}
[System.Serializable] public class GeminiPart { public string text; }
[System.Serializable] public class GeminiContent { public List<GeminiPart> parts; }
[System.Serializable] public class GeminiCandidate { public GeminiContent content; }
[System.Serializable] public class GeminiResponse { public List<GeminiCandidate> candidates; }


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

    [SerializeField] private string CovertApiSecretKey = "secret_key_157438fb48fedadaa5cb37ebbde714d5";
    [SerializeField] private string CovertApiPublicKey = "public_key_53161b4d6d374ee353aeca553ec4a566";

    private bool isPressed = false;
    private bool   requestInFlight = false;
    private float  lastPressTime   = 0f;
    private const  float debounceSeconds = 2f;

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

    /*void Update()
    {
        if (GetControllerRaycast(rightHand, out RaycastHit hit))
        {
            if (hit.collider.gameObject == gameObject && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, rightHand))
            {
                OnButtonPressed();
            }
        }
    }*/
    public void TriggerButtonFromTouch()
    {
        OnButtonPressed();
    }

    void OnButtonPressed()
{
    // Debounce rapid taps 
    if (Time.time - lastPressTime < debounceSeconds) return;
    lastPressTime = Time.time;

    // One active request at a time 
    if (requestInFlight) return;
    requestInFlight = true;

    Debug.Log("Solve Button Pressed!");
    buttonRenderer.material.color = Color.red;

    if (whiteboardCapture == null)
    {
        ResetButtonState();
        Debug.LogError("WhiteboardCapture not found.");
        return;
    }

    byte[] boardImage = whiteboardCapture.CaptureBoardToByteArray();
    if (boardImage == null)
    {
        ResetButtonState();
        Debug.LogError("Board image capture failed (bytes null).");
        return;
    }

    Debug.Log($"Chalkboard captured. Bytes: {boardImage.Length}");
    whiteboardCapture.SaveBoardToFile("WhiteboardCapture.png");
    StartCoroutine(SendImageToMathpix(boardImage));
}

// helper
private void ResetButtonState()
{
    requestInFlight = false;
    buttonRenderer.material.color = defaultColor;
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

        string optionsJson = JsonConvert.SerializeObject(new
            {
                formats      = new[] { "latex_styled" },
                rm_spaces    = true,
                math_inline_delimiters = new[] { "$", "$" },
                word_data    = true
            });
        form.AddField("options_json", optionsJson);

        UnityWebRequest request = UnityWebRequest.Post(mathpixUrl, form);
        request.SetRequestHeader("app_id", appId);    
        request.SetRequestHeader("app_key", appKey);   

        Debug.Log("Sending request to Mathpix...");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Mathpix API Error: " + request.error);
            ResetButtonState(); 
            yield break;

        }
        else
        {
            string response = request.downloadHandler.text;
            Debug.Log("Mathpix Response:\n" + response);

            MathpixResponse parsed = JsonConvert.DeserializeObject<MathpixResponse>(response);

            string latex = "";

        // Try extracting math from word_data
        if (parsed.word_data != null && parsed.word_data.Count > 0)
        {
            latex = BuildFullPromptFromWordData(parsed.word_data);
            if (!string.IsNullOrWhiteSpace(latex))
            {
                Debug.Log("Extracted math LaTeX from word_data: " + latex);
            }
        }

        // If no math from word_data, fallback to latex_styled
        if (string.IsNullOrWhiteSpace(latex) && !string.IsNullOrWhiteSpace(parsed.latex_styled))
        {
            latex = parsed.latex_styled.Replace("\\\\", "\\");
            Debug.Log("Fallback to latex_styled: " + latex);
        }

        // Final validation: only send if it's actual math
        if (!string.IsNullOrWhiteSpace(latex) && Regex.IsMatch(latex, @"(\\[a-zA-Z]+|[a-zA-Z]|\d).{2,}"))
        {
            Debug.Log("Valid math equation found: " + latex);

            // Uncommnet when Mathpix is good.
            StartCoroutine(SendLatexToGemini(latex));
        }
        else
        {
            Debug.LogWarning("No valid math equation found — skipping request.");
        }

                }
    }

    private static string BuildFullPromptFromWordData(IEnumerable<MathpixWord> words)
{
    var sb = new System.Text.StringBuilder();

    foreach (var w in words)
    {
        if (string.IsNullOrWhiteSpace(w?.latex)) continue;

        if (w.type == "math")
            sb.Append("\\[").Append(w.latex).Append("\\] ");
        else // "text" or anything else
            sb.Append(w.latex).Append(' ');
    }

    return sb.ToString().TrimEnd();   // remove last space
}


private string lastLatexTex; 

private IEnumerator SendLatexToGemini(string latex)
{
    string geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={geminiApiKey}";
    
    latex = Regex.Replace(latex, @"\r?\n", "\n"); 

    string prompt =
    "You are a LaTeX math tutor.\n\n" +

    "TASK:\n" +
    "Solve the problem below and return **only** the explanation body—" +
    "no \\documentclass, no \\begin{document}, no metadata.\n\n" +

    "FORMAT RULES\n" +
    "• Restate the problem in one sentence that begins with: "
        + "\"The question asks to compute/solve:\"\n" +
    "• Put every derivation line in display math mode (\\[ ... \\]).\n" +
    "• End with the final answer boxed: \\boxed{...}\n" +
    "• Do not add any text outside the LaTeX body.\n\n" +

    "PROBLEM (LaTeX):\n" +
    latex;   


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
        ResetButtonState(); 
        yield break;
    }
    else
    {
        string result = request.downloadHandler.text;
        Debug.Log("Gemini Response JSON:\n" + result);

        // Deserialize Gemini response and extract the LaTeX .tex file text
        GeminiResponse gemini = JsonConvert.DeserializeObject<GeminiResponse>(result);
        if (gemini?.candidates?.Count == 0 || gemini.candidates[0].content?.parts?.Count == 0){
            Debug.LogError("Gemini returned no usable content.");
            ResetButtonState();          
            yield break;                 
        }

        string rawContent = gemini.candidates[0].content.parts[0].text;
        rawContent = Regex.Replace(rawContent, @"\\(documentclass|begin|end)\{document\}", "", RegexOptions.IgnoreCase);
        lastLatexTex = WrapInLatexDocument(rawContent);

        Debug.Log("Extracted LaTeX .tex content:\n" + lastLatexTex);

        StartCoroutine(SendLatexToLatexOnHTTP(lastLatexTex));
    }
}
private IEnumerator SendLatexToLatexOnHTTP(string texContent)
{
    // Send to LaTeX-on-HTTP
    var requestData = new
    {
        compiler = "pdflatex",
        resources = new[] {
            new {
                main = true,
                content = texContent
            }
        },
        output_format = "pdf"
    };

    string json = JsonConvert.SerializeObject(requestData);
    UnityWebRequest request = new UnityWebRequest("https://latex.ytotech.com/builds/sync", "POST");
    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
    request.downloadHandler = new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type", "application/json");

    Debug.Log("Sending full LaTeX document to LaTeX-on-HTTP...");
    yield return request.SendWebRequest();

    if (request.result == UnityWebRequest.Result.Success)
    {
        byte[] pdfBytes = request.downloadHandler.data;
        string path = Path.Combine(Application.temporaryCachePath, "GeneratedFromGemini.pdf");
        System.IO.File.WriteAllBytes(path, pdfBytes);
        Debug.Log($"PDF saved to: {path}");
        StartCoroutine(ConvertPdfToPng(path));
    }
    else
    {
        Debug.LogError("LaTeX-on-HTTP Error: " + request.error);
        Debug.LogError(request.downloadHandler.text);
        ResetButtonState();
        yield break;
    } 
}
private IEnumerator ConvertPdfToPng(string pdfFilePath)
{
    // 1. Get access token
    var tokenPayload = new { publicKey = CovertApiPublicKey, secretKey = CovertApiSecretKey };
    UnityWebRequest tokenRequest = new UnityWebRequest("https://api-server.compdf.com/server/v1/oauth/token", "POST");
    byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tokenPayload));
    tokenRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
    tokenRequest.downloadHandler = new DownloadHandlerBuffer();
    tokenRequest.SetRequestHeader("Content-Type", "application/json");
    yield return tokenRequest.SendWebRequest();

    if (tokenRequest.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError("Token error: " + tokenRequest.error);
        ResetButtonState();
        yield break;
    }
    Debug.Log($"OAuth raw JSON: {tokenRequest.downloadHandler.text}");

    var tokenJson = JObject.Parse(tokenRequest.downloadHandler.text);
    string accessToken = tokenJson["data"]?["access_token"]?.ToString();

    // 2. Create PDF-to-PNG task
    UnityWebRequest taskRequest = UnityWebRequest.Get("https://api-server.compdf.com/server/v1/task/pdf/png");
    taskRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
    yield return taskRequest.SendWebRequest();

    if (taskRequest.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError("Task creation error: " + taskRequest.error);
        ResetButtonState();
        yield break;
    }

    var taskJson = JObject.Parse(taskRequest.downloadHandler.text);
    string taskId = taskJson["data"]?["taskId"]?.ToString();

    // 3. Upload the PDF file
    List<IMultipartFormSection> form = new List<IMultipartFormSection>
    {
        new MultipartFormFileSection("file", File.ReadAllBytes(pdfFilePath), "input.pdf", "application/pdf"),
        new MultipartFormDataSection("taskId", taskId),
        new MultipartFormDataSection("password", ""),
        new MultipartFormDataSection("parameter", "{\"imgDpi\":\"300\"}"),
        new MultipartFormDataSection("language", "")
    };

    UnityWebRequest uploadRequest = UnityWebRequest.Post("https://api-server.compdf.com/server/v1/file/upload", form);
    uploadRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
    yield return uploadRequest.SendWebRequest();

    if (uploadRequest.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError("Upload error: " + uploadRequest.error);
        ResetButtonState();
        yield break;
    }

    // 4. Execute conversion
    UnityWebRequest execRequest = UnityWebRequest.Get($"https://api-server.compdf.com/server/v1/execute/start?taskId={taskId}");
    execRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
    yield return execRequest.SendWebRequest();

    if (execRequest.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError("Execution error: " + execRequest.error);
        ResetButtonState();
        yield break;
    }

    // 5. Fetch result info
    UnityWebRequest resultRequest = UnityWebRequest.Get($"https://api-server.compdf.com/server/v1/task/taskInfo?taskId={taskId}");
    resultRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
    yield return resultRequest.SendWebRequest();

    if (resultRequest.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError("Result fetch error: " + resultRequest.error);
        ResetButtonState();
        yield break;
    }

    var resultJson = JObject.Parse(resultRequest.downloadHandler.text);
    JArray files = resultJson["data"]?["files"] as JArray;

    if (files == null || files.Count == 0)
    {
        Debug.LogError("No files returned in result.");
        ResetButtonState();
        yield break;
    }

    string zipUrl = files[0]?["url"]?.ToString();

    // 6. Download the ZIP
    if (!string.IsNullOrEmpty(zipUrl))
    {
        UnityWebRequest zipRequest = UnityWebRequest.Get(zipUrl);
        string localZipPath = Path.Combine(Application.temporaryCachePath, "converted_images.zip");
        zipRequest.downloadHandler = new DownloadHandlerFile(localZipPath);
        yield return zipRequest.SendWebRequest();

        if (zipRequest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("ZIP downloaded: " + localZipPath);
            string extractedFolder = Path.Combine(Application.temporaryCachePath, "unzipped_images");
            ExtractZipFile(localZipPath, extractedFolder);
        }
        else
        {
            Debug.LogError("ZIP download failed: " + zipRequest.error);
        }
    }

    ResetButtonState();
}




private void ExtractZipFile(string zipFilePath, string outputFolder)
{
    try
    {
        if (Directory.Exists(outputFolder))
            Directory.Delete(outputFolder, true);

        ZipFile.ExtractToDirectory(zipFilePath, outputFolder);
        Debug.Log($"ZIP extracted to: {outputFolder}");
        
        string[] files = Directory.GetFiles(outputFolder);
        foreach (var file in files)
        {
            Debug.Log("Extracted file: " + file);
            // TODO: Display first PNG on Mesh
            if (file.EndsWith(".png"))
                {
                    DisplayImageOnUI(file);
                    break;
                }
        }


    }
    catch (System.Exception ex)
    {
        Debug.LogError("Error extracting ZIP: " + ex.Message);
    }
}

private string EscapeJson(string input)
{
    return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
 private string WrapInLatexDocument(string bodyContent)
{
    return
@"\documentclass{article}
\usepackage{amsmath}
\usepackage{amsfonts}
\usepackage{amssymb}
\title{Solution}
\author{MathVR + Gemini}

\begin{document}
\maketitle
" + bodyContent + @"
\end{document}";
}

private void DisplayImageOnUI(string imagePath)
{
    if (!File.Exists(imagePath))
    {
        Debug.LogError("Image file not found: " + imagePath);
        return;
    }

    byte[] imageData = File.ReadAllBytes(imagePath);
    Texture2D tex = new Texture2D(2, 2);
    tex.LoadImage(imageData);

    GameObject uiImage = GameObject.Find("SolutionImage");

    if (uiImage == null)
    {
        Debug.LogError("SolutionImage UI object not found.");
        return;
    }

    UnityEngine.UI.RawImage rawImage = uiImage.GetComponent<UnityEngine.UI.RawImage>();
    rawImage.texture = tex;

    Debug.Log("Image displayed on UI.");
}

}
