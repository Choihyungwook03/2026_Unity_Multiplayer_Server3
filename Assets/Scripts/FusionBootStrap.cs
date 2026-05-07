using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UIElements;

public class FusionBootStrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Session")]
    [SerializeField] private string sessionName = "Room_01";

    [Header("Player")]
    //[SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private NetworkPrefabRef[] playerPrefab;                               //네트워크에 등록된 프리팹
    [SerializeField] private Transform[] spawnPoints;                                       //스폰 위치 설정 

    [Header("Pickable Box")]
    [SerializeField] private NetworkPrefabRef pickableBoxPrefab;
    [SerializeField] private Transform[] boxSpawnPoints;

    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private int maxPlayerPerTeam = 2;

    [Header("Lobby")]
    [SerializeField] private NetworkPrefabRef lobbyDataPrefab;

    private Dictionary<PlayerRef, NetworkObject> lobbyObjects = new();

    private bool boxesSpawned = false;

    private Dictionary<PlayerRef, NetworkObject> playerObjects = new();

    private NetworkRunner runner;

    public struct NetworkInputData : INetworkInput
    {
        public Vector2 move;
        public float cameraYaw;
        public NetworkButtons buttons;
    }

    public enum InputButton
    {
        Fire = 0,
        Jump = 1,
        Pickup = 2,
    }


    public void StartHost() => _ = StartGame(GameMode.Host);
    public void StartClinet() => _ = StartGame(GameMode.Client);

    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = player.RawEncoded % spawnPoints.Length;
            return spawnPoints[index].position;
        }

        return new Vector3(player.RawEncoded * 2, 1, 0);                //RawEncoded (바이트(byte)) 형태로 변환 (직렬화) 중간단계
    }

    private async Task StartGame(GameMode mode)
    {
        if (runner != null) return;

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;

        runner.AddCallbacks(this);

        var SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            SceneManager = SceneManager
        });

        if (result.Ok)
        {
            Debug.Log($"[Fusion] StartGame OK - {mode} / {sessionName}");

            if (runner.IsServer)
            {
                SpawnBoxes();
            }
        }
        else
        {
            Debug.LogError($"[Fusion] StartGame FAILED - {result.ShutdownReason}");
        }
    }

    public void SpawnBoxes()
    {
        if (!runner.IsServer) return;

        if (boxesSpawned) return;

        boxesSpawned = true;

        if (boxSpawnPoints == null || boxSpawnPoints.Length == 0) return;

        foreach (var point in boxSpawnPoints)
        {
            if (point == null) continue;

            runner.Spawn(pickableBoxPrefab, point.position, point.rotation, null);
        }

        Debug.Log($"상자 {boxSpawnPoints.Length} 개 생성 완료");
    }



    // --------------------- 콜백 (필수/미사용은 빈 구현) -------------------

    public bool CanJoinTeam(int teamIndex)
    {
        int count = 0;

        foreach (var pair in lobbyObjects)
        {
            PlayerLobbyData data = pair.Value.GetComponent<PlayerLobbyData>();

            if (data != null && data.TeamIndex == teamIndex)
                count++;
        }

        return count < maxPlayerPerTeam;

    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"플레이어 입장 : {player}");

        if (!runner.IsServer)
            return;

        if (lobbyObjects.Count >= maxPlayers)
        {
            Debug.LogWarning($"최대 인원 초과 : {player}");
            return;
        }

        NetworkObject lobbyObj = runner.Spawn(
                lobbyDataPrefab,
                Vector3.zero,
                Quaternion.identity,
                player
            );

        lobbyObjects[player] = lobbyObj;

        Debug.Log($"로비 데이터 생성 완료 : {player}");
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        if (playerObjects.TryGetValue(player, out var obj))
        {
            runner.Despawn(obj);
            playerObjects.Remove(player);
        }

        Debug.Log($"플레이어 제거됨 : {player}");

    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new NetworkInputData();

        data.move = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        data.cameraYaw = SimplePlayer.LocalCameraYaw;

        var buttons = new NetworkButtons();                                         // 네트워크 버튼 생성
        buttons.Set((int)InputButton.Fire, Input.GetMouseButton(0));                // 마우스 버튼
        buttons.Set((int)InputButton.Jump, Input.GetKey(KeyCode.Space));            // 점프 버튼
        buttons.Set((int)InputButton.Pickup, Input.GetKey(KeyCode.E));              // 물건 상호작용 버튼

        data.buttons = buttons;

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[Fusion] Disconnected : {reason}");
    }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        Debug.Log($"[Fusion] Shutdown : {reason}");
        this.runner = null;
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    //------------------------------------------------------------------------------------------------------------

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}