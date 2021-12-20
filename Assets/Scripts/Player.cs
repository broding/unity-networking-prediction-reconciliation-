using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour {

    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Settings")]
    [SerializeField] private bool enableReconsiliation = true;
    [SerializeField] private float maxReconsilitationDelta = 0.1f;

    private List<InputData> inputBuffer = new List<InputData>();
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
            OnServerAfterTick();
        }
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
            predictedPositions.Add(new PositionData() {
                Position = rb.position,
                Rotation = rb.rotation,
                Velocity = rb.velocity,
                AngularVelocity = rb.angularVelocity,
                Tick = NetworkManager.LocalTime.Tick
            });
        }
    }

    private void OnClientBeforeTick() {
        if(previousTick >= NetworkManager.LocalTime.Tick) {
            InvalidateCachesFromTick(NetworkManager.LocalTime.Tick);
        }

        // Check position/rotation threshold here. If too much, reconcile.
        if(!IsHost && pendingPositionData != null) {
            PositionData predictedPosition = predictedPositions.FirstOrDefault(p => p.Tick == pendingPositionData.Tick);
            if(predictedPosition != null) {
                float delta = predictedPosition.CalculateDelta(pendingPositionData);
                if (enableReconsiliation && delta > maxReconsilitationDelta) {
                    rb.position = pendingPositionData.Position;
                    rb.rotation = pendingPositionData.Rotation;
                    rb.velocity = pendingPositionData.Velocity;
                    rb.angularVelocity = pendingPositionData.AngularVelocity;

                    PhysicsController.Instance.MoveRigidbodyToScene(rb);
                    foreach (InputData data in inputBuffer.Where(i => i.Tick > pendingPositionData.Tick)) {
                        ProcessInput(data);
                        PhysicsController.Instance.SimulatePhysics(NetworkManager.NetworkTickSystem.LocalTime.FixedDeltaTime);
                    }
                    PhysicsController.Instance.ReturnRigidbody();

                    pendingPositionData = null;
                }
            }
        }

        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        InputData inputData = new InputData() { Input = input, Tick = NetworkManager.LocalTime.Tick };

        SendInput_ServerRpc(inputData);
        if (!IsHost) {
            ProcessInput(inputData);
        }

        inputBuffer.Add(inputData);
    }

    private void InvalidateCachesFromTick(int tick) {
        inputBuffer.RemoveAll(i => i.Tick >= tick);
        predictedPositions.RemoveAll(i => i.Tick >= tick);
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendInput_ServerRpc(InputData inputData) {
        inputBuffer.Add(inputData);
    }

    private void OnServerAfterTick() {
        int tick = NetworkManager.LocalTime.Tick;
        InputData inputData = inputBuffer.FirstOrDefault(i => i.Tick == tick);
        if(inputData.Tick == 0) {
            inputData = previousInput;
        } else {
            inputBuffer.Remove(inputData);
        }

        ProcessInput(inputData);
        previousInput = inputData;
    }

    private void ProcessInput(InputData inputData) {
        Vector3 force = inputData.Input * 50;
        force.z = force.y;
        force.y = 0;
        rb.AddForce(force);
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
