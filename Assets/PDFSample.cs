using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class Example : MonoBehaviour
{
    private void Start()
    {
        ConvertPDF converter = new ConvertPDF();

        string jpegOutput = Path.Combine(Application.temporaryCachePath, "converted-pdf.jpg");
        string path = Path.Combine(Application.temporaryCachePath, "GeneratedFromGemini.pdf");

        converter.Convert(path, jpegOutput, 1, 2, "jpeg", 500, 600);
        Debug.Log("Converted Images from " + path + " in" + jpegOutput);
    }
}