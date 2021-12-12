using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Game : MonoBehaviour {

    [SerializeField] private bool isServer;
    [SerializeField] private CustomNetworkManager networkManager;
    [SerializeField] private Player playerPrefab;

    private void Start() {
#if UNITY_EDITOR
        if (ParrelSync.ClonesManager.IsClone()) {
            StartServer();
        } else {
            StartClient();
        }
#endif

        networkManager.NetworkTickSystem.Tick += OnTick;
    }

    private void OnTick() {
        networkManager.Tick();
    }

    private void OnDestroy() {
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.NetworkTickSystem.Tick -= OnTick;
    }

    private void StartServer() {
        networkManager.StartServer();
        
        networkManager.OnClientConnectedCallback += OnClientConnected;
        
    }

    private void OnClientConnected(ulong clientId) {
        Player player = Instantiate(playerPrefab);
        player.NetworkObject.Spawn();
        player.NetworkObject.ChangeOwnership(clientId);
    }

    private void StartClient() {
        networkManager.StartClient();
    }
}
