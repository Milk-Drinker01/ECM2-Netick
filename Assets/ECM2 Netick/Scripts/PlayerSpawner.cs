using Netick.Unity;
using UnityEngine;

public class PlayerSpawner : NetworkEventsListener
{
    public Transform SpawnPos;
    public GameObject PlayerPrefab;
    public bool StaggerSpawns = true;

    // This is called on the server when a client has connected.
    public override void OnPlayerConnected(NetworkSandbox sandbox, Netick.NetworkPlayer client)
    {
        var spawnPos = SpawnPos.position;
        if (StaggerSpawns)
            spawnPos += Vector3.left * (1 + sandbox.ConnectedPlayers.Count);
        var player = sandbox.NetworkInstantiate(PlayerPrefab, spawnPos, SpawnPos.rotation, client);
        client.PlayerObject = player.gameObject;
    }
}
