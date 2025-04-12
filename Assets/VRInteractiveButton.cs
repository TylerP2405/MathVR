using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.IO;



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

        string optionsJson = "{\"math_inline_delimiters\": [\"$\", \"$\"], \"rm_spaces\": true, \"formats\": [\"latex_styled\", \"text\"], \"word_data\": true}";
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

    private string BuildFullPromptFromWordData(List<MathpixWord> words)
{
    var parts = new List<string>();

    foreach (var word in words)
    {
        if (!string.IsNullOrEmpty(word.latex))
        {
            if (word.type == "math")
            {
                parts.Add($"\\[{word.latex}\\]");
            }
            else // type == "text"
            {
                parts.Add(word.latex); // Keep as LaTeX \text{...}
            }
        }
    }

    return string.Join(" ", parts);
}


private string lastLatexTex; 

private IEnumerator SendLatexToGemini(string latex)
{
    string geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={geminiApiKey}";
    
    latex = Regex.Replace(latex, @"\r?\n", "\n"); 

    string prompt = 
        "Solve the following math problem with a clear step-by-step explanation. " +
        "Use LaTeX formatting and display math mode (\\[ ... \\]) for all equations. " +
        "Do NOT include \\documentclass or \\begin{document} — just provide the explanation body.\n\n" +
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
    }
    else
    {
        string result = request.downloadHandler.text;
        Debug.Log("Gemini Response JSON:\n" + result);

        // Deserialize Gemini response and extract the LaTeX .tex file text
        GeminiResponse gemini = JsonConvert.DeserializeObject<GeminiResponse>(result);
        string rawContent = gemini.candidates[0].content.parts[0].text;
        rawContent = rawContent.Replace(@"\end{document}", ""); 
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
        }
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
    }
    else
    {
        Debug.LogError("LaTeX-on-HTTP Error: " + request.error);
        Debug.LogError(request.downloadHandler.text);
    }
}

    private string EscapeJson(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
private string WrapInLatexDocument(string bodyContent)
{
    return
@"\documentclass[12pt]{article}
\usepackage{amsmath}
\usepackage{amsfonts}
\usepackage{amssymb}
\usepackage{geometry}
\usepackage{graphicx}
\geometry{margin=1in}

\title{Equation Solution}
\author{MathVR + NavAR w/ Gemini}

\begin{document}

\maketitle

" + bodyContent + @"

\end{document}";
}

