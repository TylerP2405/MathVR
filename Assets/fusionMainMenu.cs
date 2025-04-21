using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using System.Threading.Tasks;

public class FusionMainMenu : MonoBehaviour
{
    public NetworkRunner runnerPrefab;
    public string sessionName = "VRClassroom"; 

    public async void CreateRoom()
    {
        Debug.Log("Create Room Working");
        await StartFusionSession(GameMode.Host);
    }

    public async void JoinRoom()
    {
        Debug.Log("Join Room Working");
        await StartFusionSession(GameMode.Client);
    }

    private async Task StartFusionSession(GameMode mode)
    {
        //        var runner = Instantiate(runnerPrefab);
        //        runner.ProvideInput = true;

        //        var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();

        //        //Attach the spawner
        //        var spawner = FindObjectOfType<FusionSpawner>();
        //        runner.AddCallbacks(spawner);

        //        var result = await runner.StartGame(new StartGameArgs
        //        {
        //            GameMode = mode,
        //            SessionName = sessionName,
        //            Scene = SceneManager.GetActiveScene().buildIndex,
        //            SceneManager = sceneManager
        //        });

        //        if (!result.Ok)
        //        {
        //            Debug.LogError($"Failed to start session: {result.ShutdownReason}");
        //        }
    }
}

