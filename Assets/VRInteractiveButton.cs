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
using SkiaSharp;
using UnityEngine.UI;



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

    [Header("Loading Image")]
    [SerializeField] public GameObject loadingSpinner;

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
    private bool requestInFlight = false;
    private float lastPressTime = 0f;
    private const float debounceSeconds = 2f;



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

    public void TriggerButtonFromTouch()
    {
        OnButtonPressed();
    }

    void OnButtonPressed()
    {
        //Show loading spinner
        loadingSpinner.SetActive(true);
        Debug.Log("Loading Script");

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
        //whiteboardCapture.SaveBoardToFile("WhiteboardCapture.png");
        StartCoroutine(SendImageToMathpix(boardImage));
    }

    // helper
    private void ResetButtonState()
    {
        //Hide loading spinner after finished
        loadingSpinner.SetActive(false);
        Debug.Log("Loading stopped, reseted button");
        requestInFlight = false;
        buttonRenderer.material.color = defaultColor;
    }

    private IEnumerator SendImageToMathpix(byte[] imageBytes)
    {
        Debug.Log("Starting API MatchPix");
        string mathpixUrl = "https://api.mathpix.com/v3/text";

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageBytes, "whiteboard.png", "image/png");

        string optionsJson = JsonConvert.SerializeObject(new
        {
            formats = new[] { "latex_styled" },
            rm_spaces = true,
            math_inline_delimiters = new[] { "$", "$" },
            word_data = true
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
                ResetButtonState();
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
            if (gemini?.candidates?.Count == 0 || gemini.candidates[0].content?.parts?.Count == 0)
            {
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

            StartCoroutine(LocalConvertPDF(pdfBytes));     // Changed to local ConvertPDF
        }
        else
        {
            Debug.LogError("LaTeX-on-HTTP Error: " + request.error);
            Debug.LogError(request.downloadHandler.text);
            ResetButtonState();
            yield break;
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


    private IEnumerator LocalConvertPDF(byte[] pdfBytes)
    {
        var bitmap = PDFtoImage.Conversion.ToImage(pdfBytes);
        DisplayImageOnUI(bitmap);
        yield return null;
        ResetButtonState();
    }

    private void DisplayImageOnUI(SKBitmap bitmap)
    {
        var tex = new Texture2D(bitmap.Width, bitmap.Height);
        using var ms = new MemoryStream();
        bitmap.Encode(ms, SKEncodedImageFormat.Png, 100);
        ms.Position = 0;
        tex.LoadImage(ms.ToArray());
        tex.anisoLevel = 0;        // Change to increase clarity at distance
        GameObject uiImage = GameObject.Find("SolutionImage");

        if (uiImage == null)
        {
            Debug.LogError("SolutionImage UI object not found.");
            return;
        }

        UnityEngine.UI.RawImage rawImage = uiImage.GetComponent<UnityEngine.UI.RawImage>();
        Debug.Log(rawImage.rectTransform.localScale);
        rawImage.rectTransform.sizeDelta = new Vector2(bitmap.Width / 5, bitmap.Height / 5);
        rawImage.texture = tex;

        Debug.Log("Image displayed on UI.");
    }

}
