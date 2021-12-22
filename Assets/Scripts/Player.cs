using RingBuffer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour {

    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Settings")]
    [SerializeField] private bool enableReconsiliation = true;
    [SerializeField] private float maxReconsilitationDelta = 0.1f;

    private RingBuffer<InputData> inputBuffer = new RingBuffer<InputData>(100);
    private Dictionary<int, InputData> serverInputBuffer = new Dictionary<int, InputData>();
    private List<PositionData> predictedPositions = new List<PositionData>();
    private PositionData pendingPositionData;
    private InputData previousInput;
    private int previousTick;

    private PositionData cachedPositionData;

    public new CustomNetworkManager NetworkManager => (CustomNetworkManager)base.NetworkManager;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        NetworkManager.BeforeTick += OnBeforeTick;
        NetworkManager.AfterTick += OnAfterTick;

        if (IsClient) {
            NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(CustomMessageNames.INPUT_LOST, (senderClientId, messagePayload) => {
                TickController.Instance.ReportInputLoss();
            });
        }
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        NetworkManager.BeforeTick -= OnBeforeTick;
        NetworkManager.AfterTick -= OnAfterTick;
    }

    private void OnBeforeTick() {
        if (IsClient) {
            OnClientBeforeTick();
        }

        if (IsServer) {
            OnServerBeforeTick();
        }
    }

    private void OnClientBeforeTick() {
        int tickDelta = NetworkManager.LocalTime.Tick - NetworkManager.ServerTime.Tick;

        if (previousTick >= NetworkManager.LocalTime.Tick) {
            Debug.LogError($"Reset thing from tick {previousTick} --> {NetworkManager.LocalTime.Tick} (changed = {previousTick - NetworkManager.LocalTime.Tick} (Tickdelta: {tickDelta}) Last pendiing position tick: {pendingPositionData?.Tick} - tick time {NetworkManager.LocalTime.FixedDeltaTime}"); 
        }

        if(!IsHost && pendingPositionData != null) {
            PositionData predictedPosition = predictedPositions.FirstOrDefault(p => p.Tick == pendingPositionData.Tick);
            if(predictedPosition != null) {
                float delta = predictedPosition.CalculateDelta(pendingPositionData);
                if (enableReconsiliation && delta > maxReconsilitationDelta) {
                    Debug.Log($"Reconsiliated!");

                    // Remove previous predicted positions from the current authorative position on. 
                    predictedPositions.RemoveAll(i => i.Tick > predictedPosition.Tick);

                    rb.position = pendingPositionData.Position;
                    rb.rotation = pendingPositionData.Rotation;
                    rb.velocity = pendingPositionData.Velocity;
                    rb.angularVelocity = pendingPositionData.AngularVelocity;

                    // Replay input and simulate physics for each input
                    PhysicsController.Instance.MoveRigidbodyToScene(rb);
                    foreach (InputData data in inputBuffer.Where(i => i.Tick > pendingPositionData.Tick)) {
                        ProcessInput(data);
                        PhysicsController.Instance.SimulatePhysics(NetworkManager.NetworkTickSystem.LocalTime.FixedDeltaTime);
                        RecordPredictedPosition(data.Tick);
                    }
                    PhysicsController.Instance.ReturnRigidbody();

                    pendingPositionData = null;
                }
            }
        }

        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        InputData inputData = new InputData() { Input = input, Tick = NetworkManager.LocalTime.Tick };
        inputBuffer.Put(inputData);

        const int minHistorySize = 15;
        const int maxHistorySize = 100;
        int historySize = Mathf.Clamp(tickDelta, Math.Min(inputBuffer.Count, minHistorySize), maxHistorySize);
        historySize = Math.Min(inputBuffer.Count, historySize);
        InputDataHistory inputDataHistory = new InputDataHistory(inputBuffer.TakeLast(historySize).ToArray(), NetworkManager.LocalTime.Tick);

        SendInput_ServerRpc(inputDataHistory);
        if (!IsHost) {
            ProcessInput(inputData);
        }
    }

    private void RecordPredictedPosition(int tick) {
        predictedPositions.Add(new PositionData() {
            Position = rb.position,
            Rotation = rb.rotation,
            Velocity = rb.velocity,
            AngularVelocity = rb.angularVelocity,
            Tick = tick
        });
    }

    private void OnServerBeforeTick() {
        int tick = NetworkManager.LocalTime.Tick;
        InputData inputData;

        if(serverInputBuffer.TryGetValue(tick, out inputData)) {
            serverInputBuffer.Remove(tick);
        } else {
            inputData = previousInput;
            Debug.LogError($"Input not found for tick {tick}. Highest tick buffer count was {serverInputBuffer.OrderByDescending(i => i.Key).First().Key}");
            NetworkManager.CustomMessagingManager.SendNamedMessage(CustomMessageNames.INPUT_LOST, OwnerClientId, new FastBufferWriter(0, Allocator.Temp), NetworkDelivery.Reliable);
        }

        ProcessInput(inputData);
        previousInput = inputData;
    }

    private void OnAfterTick() {
        previousTick = NetworkManager.LocalTime.Tick;

        if (IsServer) {
            SendPosition_ClientRpc(new PositionData() {
                Position = rb.position,
                Rotation = rb.rotation,
                Velocity = rb.velocity,
                AngularVelocity = rb.angularVelocity,
                Tick = NetworkManager.LocalTime.Tick
            });
        } 
        if (IsClient) {
            RecordPredictedPosition(NetworkManager.LocalTime.Tick);
        }
    }

    private void ProcessInput(InputData inputData) {
        Vector3 force = inputData.Input * 50;
        force.z = force.y;
        force.y = 0;
        rb.AddForce(force);
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendInput_ServerRpc(InputDataHistory inputDataHistory) {
        int historySize = inputDataHistory.InputDatas.Length;
        for (int i = 0; i < historySize; i++) {
            int inputTick = inputDataHistory.Tick - historySize + i + 1;
            if (serverInputBuffer.ContainsKey(inputTick)) {
                serverInputBuffer[inputTick] = inputDataHistory.InputDatas[i];
            } else {
                serverInputBuffer.Add(inputTick, inputDataHistory.InputDatas[i]);
            }
        }
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendPosition_ClientRpc(PositionData newPositionData) {
        if(pendingPositionData == null || pendingPositionData.Tick < newPositionData.Tick) {
            pendingPositionData = cachedPositionData = newPositionData;
        }
    }

    private void OnDrawGizmos() {
        if (cachedPositionData != null) {
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(cachedPositionData.Position, cachedPositionData.Rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
