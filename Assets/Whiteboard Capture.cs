using UnityEngine;
using System.IO;

public class WhiteboardCapture : MonoBehaviour
{
    [SerializeField] private RenderTexture chalkboardRT; // Assign your chalkboard's RenderTexture in the Inspector

    /// <summary>
    /// Captures the chalkboard RenderTexture as a PNG byte array.
    /// </summary>
    public byte[] CaptureBoardToByteArray()
    {
        if (chalkboardRT == null)
        {
            Debug.LogError("RenderTexture is not assigned on WhiteboardCapture!");
            return null;
        }

        // 1. Create a new Texture2D with the same dimensions as the RenderTexture.
        Texture2D texture2D = new Texture2D(
            chalkboardRT.width,
            chalkboardRT.height,
            TextureFormat.RGB24,
            false
        );

        // 2. Remember the current active RenderTexture
        RenderTexture currentActiveRT = RenderTexture.active;

        byte[] pngBytes = null;
        try
        {
            // 3. Make our chalkboard RenderTexture active so we can read pixels.
            RenderTexture.active = chalkboardRT;

            // 4. Read the pixels from the entire RenderTexture area and store them in the Texture2D.
            texture2D.ReadPixels(
                new Rect(0, 0, chalkboardRT.width, chalkboardRT.height),
                0,
                0
            );

            // 5. Apply the changes to the Texture2D to finalize.
            texture2D.Apply();

            // 6. Encode the Texture2D to PNG. This returns a byte[].
            pngBytes = texture2D.EncodeToPNG();
        }
        finally
        {
            // 7. Restore the previously active RenderTexture.
            RenderTexture.active = currentActiveRT;

            // 8. Clean up the temporary Texture2D.
            Destroy(texture2D);
        }

        // 9. Return the PNG byte array (ready to be sent to an API or saved to disk).
        return pngBytes;
    }

    /// <summary>
    /// Saves the chalkboard RenderTexture as a PNG file to disk.
    /// </summary>
    public void SaveBoardToFile(string filename)
    {
        byte[] pngData = CaptureBoardToByteArray();
        if (pngData == null)
        {
            Debug.LogError("Failed to capture board! pngData is null.");
            return;
        }

        // Create a path somewhere in the project for testing:
        // You can replace Application.dataPath with another path if needed.
        string savePath = Path.Combine(Application.dataPath, filename);

        File.WriteAllBytes(savePath, pngData);
        Debug.Log($"Chalkboard image saved to: {savePath}");
    }
}
