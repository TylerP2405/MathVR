using Fusion;
using Fusion.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System; 

public class FusionMainMenu : MonoBehaviour
{
    public NetworkRunner runnerPrefab;
    public string sessionName = "ClassroomRoom"; // Could also be pulled from an input field
    private NetworkRunner runnerInstance;

    public async void CreateRoom()
    {
        await StartFusionSession(GameMode.Host);
    }

    public async void JoinRoom()
    {
        await StartFusionSession(GameMode.Client);
    }

    private async Task StartFusionSession(GameMode mode)
    {
        // Spawn the runner
        runnerInstance = Instantiate(runnerPrefab);
        runnerInstance.ProvideInput = true;

        // Attach scene manager
        var sceneManager = runnerInstance.GetComponent<NetworkSceneManagerDefault>();
        runnerInstance.AddCallbacks(FindAnyObjectByType<INetworkRunnerCallbacks>());

        var result = await runnerInstance.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = sessionName,
            Scene = SceneManager.GetActiveScene().buildIndex,
            SceneManager = sceneManager
        });

        if (!result.Ok)
        {
            Debug.LogError("Failed to start session: " + result.ShutdownReason);
        }
    }
}
