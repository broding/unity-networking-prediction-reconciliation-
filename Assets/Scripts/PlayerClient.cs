using RingBuffer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerClient : NetworkBehaviour {

    [Header("References")]
    [SerializeField] private PlayerShared shared;

    [Header("Settings")]
    [SerializeField] private float maxReconsilitationDelta = 0.1f;

    private PositionData pendingPositionData;
    private List<PositionData> predictedPositions = new List<PositionData>();
    private RingBuffer<InputData> inputBuffer = new RingBuffer<InputData>(100);
    private int lastTick;
    private PositionData cachedPositionData;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if(IsServer && !IsHost) { return; }

        shared.PositionReceivedFromServer += OnPositionReceivedFromServer;

        PhysicsController.Instance.OnBeforePhysicsTick += OnBeforePhysicsTick;
        PhysicsController.Instance.OnAfterPhysicsTick += OnAfterPhysicsTick;

        NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(CustomMessageNames.INPUT_LOST, (senderClientId, messagePayload) => {
            TickController.Instance.ReportInputLoss();
        });
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        shared.PositionReceivedFromServer -= OnPositionReceivedFromServer;

        PhysicsController.Instance.OnBeforePhysicsTick -= OnBeforePhysicsTick;
        PhysicsController.Instance.OnAfterPhysicsTick -= OnAfterPhysicsTick;
    }

    private void OnPositionReceivedFromServer(PositionData newPositionData) {
        if (pendingPositionData == null || pendingPositionData.Tick < newPositionData.Tick) {
            pendingPositionData = cachedPositionData = newPositionData;
        }
    }

    private void OnBeforePhysicsTick() {
        int currentTick = NetworkManager.Singleton.LocalTime.Tick;

        if (!IsHost && pendingPositionData != null) {
            PositionData predictedPosition = predictedPositions.FirstOrDefault(p => p.Tick == pendingPositionData.Tick);
            if (predictedPosition != null) {
                float diff = predictedPosition.CalculateDelta(pendingPositionData);
                if (diff <= maxReconsilitationDelta) {
                    // Remove previous predicated positions because our latest authorative position is correct
                    predictedPositions.RemoveAll(i => i.Tick <= predictedPosition.Tick);
                } else {
                    Debug.LogError($"!!!!! Reconciliated! Tick {pendingPositionData.Tick} to {currentTick}");

                    //Remove previous predicted positions from the current authorative position on. 
                    predictedPositions.RemoveAll(i => i.Tick > predictedPosition.Tick);

                    shared.ResetToPositionData(pendingPositionData);

                    PhysicsController.Instance.MoveGameObjectToTemporaryScene(gameObject);
                    foreach (InputData cachedInputData in inputBuffer.Where(i => i.Tick > pendingPositionData.Tick).OrderBy(i => i.Tick)) {
                        shared.ApplyForcesForInput(cachedInputData, NetworkManager.Singleton.LocalTime.FixedDeltaTime);
                        PhysicsController.Instance.SimulatePhysics(NetworkManager.Singleton.LocalTime.FixedDeltaTime);

                        PositionData positionData = shared.GeneratePositionData(cachedInputData.Tick);
                        predictedPositions.Add(positionData);
                    }
                    PhysicsController.Instance.ReturnRigidbody();

                }

                pendingPositionData = null;
            }
        }

        if (lastTick >= currentTick) {
            Debug.Log($"Thrown back into time from {lastTick} to {currentTick}");
            return;
        }

        InputData inputData = RecordInputData();

        inputBuffer.Put(inputData);

        int tickDelta = NetworkManager.Singleton.LocalTime.Tick - NetworkManager.Singleton.ServerTime.Tick;
        const int maxHistorySize = 100;
        int historySize = Mathf.Clamp(tickDelta, Math.Min(inputBuffer.Count, tickDelta), maxHistorySize);
        historySize = Math.Min(inputBuffer.Count, historySize);
        InputDataHistory inputDataHistory = new InputDataHistory(inputBuffer.TakeLast(historySize).ToArray(), currentTick);

        shared.SendInput_ServerRpc(inputDataHistory);

        if (!IsHost) {
            shared.ApplyForcesForInput(inputData, NetworkManager.Singleton.LocalTime.FixedDeltaTime);
        }

        lastTick = currentTick;
    }

    private InputData RecordInputData() {
        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        return new InputData() { Input = input, Tick = NetworkManager.LocalTime.Tick };
    }

    private void OnAfterPhysicsTick() {
        PositionData positionData = shared.GeneratePositionData(NetworkManager.Singleton.LocalTime.Tick);
        predictedPositions.Add(positionData);
    }

    private void OnDrawGizmos() {
        if (cachedPositionData != null) {
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(cachedPositionData.Position, cachedPositionData.Rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
