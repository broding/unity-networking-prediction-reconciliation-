using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerServer : NetworkBehaviour {

    [Header("References")]
    [SerializeField] private PlayerShared shared;

    private Dictionary<int, InputData> inputBuffer = new Dictionary<int, InputData>();
    private InputData lastInputData;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if (IsClient && !IsHost) { return; }

        shared.InputReceivedFromClient += OnInputReceived;

        PhysicsController.Instance.OnBeforePhysicsTick += OnBeforePhysicsTick;
        PhysicsController.Instance.OnAfterPhysicsTick += OnAfterPhysicsTick;
    }

    private void OnBeforePhysicsTick() {
        int currentTick = NetworkManager.Singleton.LocalTime.Tick;

        InputData currentInputData;
        if (inputBuffer.TryGetValue(currentTick, out currentInputData)) {
            inputBuffer.Remove(currentTick);
        } else {
            currentInputData = lastInputData;
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("INPUT_LOST", OwnerClientId, new FastBufferWriter(0, Allocator.Temp), NetworkDelivery.Reliable);
            Debug.Log($"Missed input for tick {currentTick}. Highest tick input found: {inputBuffer.OrderByDescending(i => i.Key).FirstOrDefault().Key}");
        }

        shared.ApplyForcesForInput(currentInputData, NetworkManager.Singleton.LocalTime.FixedDeltaTime);

        lastInputData = currentInputData;
    }

    private void OnAfterPhysicsTick() {
        shared.SendPosition_ClientRpc(shared.GeneratePositionData(NetworkManager.Singleton.LocalTime.Tick));
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        shared.InputReceivedFromClient -= OnInputReceived;
    }

    private void OnInputReceived(InputDataHistory inputDataHistory) {
        int historySize = inputDataHistory.InputDatas.Length;
        for (int i = 0; i < historySize; i++) {
            int inputTick = inputDataHistory.Tick - historySize + i + 1;
            if (inputBuffer.ContainsKey(inputTick)) {
                inputBuffer[inputTick] = inputDataHistory.InputDatas[i];
            } else {
                inputBuffer.Add(inputTick, inputDataHistory.InputDatas[i]);
            }
        }
    }
}
