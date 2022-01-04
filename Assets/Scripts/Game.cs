using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Game : MonoBehaviour {

    [SerializeField] private PlayerShared playerPrefab;

    private void Start() {
#if UNITY_EDITOR
        if (ParrelSync.ClonesManager.IsClone()) {
            NetworkManager.Singleton.StartServer();
        } else {
            NetworkManager.Singleton.StartClient();
        }
#endif

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        if (NetworkManager.Singleton.IsHost) {
            SpawnPlayer(NetworkManager.Singleton.LocalClientId);
        }

        PhysicsController.Instance.Initialize();
    }

    private void OnDestroy() {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId) {
        if (!NetworkManager.Singleton.IsServer) {
            return;
        }

        SpawnPlayer(clientId);
    }

    private void SpawnPlayer(ulong clientId) {
        PlayerShared player = Instantiate(playerPrefab);
        player.NetworkObject.Spawn();
        player.NetworkObject.ChangeOwnership(clientId);
    }
}
