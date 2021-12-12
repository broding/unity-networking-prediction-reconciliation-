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

    private List<InputData> inputBuffer = new List<InputData>();
    private PositionData positionData;
    private InputData previousInput;
    private int previousTick;
    private Rigidbody[] cachedRigidbodies;

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
        if (IsServer) {
            OnServerTick();
        } else {
            OnClientTick();
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
        } else {

        }
    }

    private void OnClientTick() {
        if(previousTick >= NetworkManager.LocalTime.Tick) {
            Debug.Log("Tick has reset");
            InvalidateCachesFromTick(NetworkManager.LocalTime.Tick);
        }

        // Check position/rotation threshold here. If too much, reconcile.

        if(enableReconsiliation && positionData != null) {
            rb.position = positionData.Position;
            rb.rotation = positionData.Rotation;
            rb.velocity = positionData.Velocity;
            rb.angularVelocity = positionData.AngularVelocity;

            PhysicsController.Instance.MoveRigidbodyToScene(rb);
            foreach (InputData data in inputBuffer.Where(i => i.Tick > positionData.Tick)) {
                ProcessInput(data);
                PhysicsController.Instance.SimulatePhysics(NetworkManager.NetworkTickSystem.LocalTime.FixedDeltaTime);
            }
            PhysicsController.Instance.ReturnRigidbody();

            positionData = null;
        }

        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        InputData inputData = new InputData() { Input = input, Tick = NetworkManager.LocalTime.Tick };

        SendInput_ServerRpc(inputData);
        ProcessInput(inputData);
        inputBuffer.Add(inputData);
    }

    private void InvalidateCachesFromTick(int tick) {
        inputBuffer.RemoveAll(i => i.Tick >= tick);
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendInput_ServerRpc(InputData inputData) {
        inputBuffer.Add(inputData);
    }

    private void OnServerTick() {
        int tick = NetworkManager.LocalTime.Tick;
        InputData inputData = inputBuffer.FirstOrDefault(i => i.Tick == tick);
        if(inputData.Tick == 0) {
            Debug.LogError("No input for tick: " + tick);
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
        if(positionData == null || positionData.Tick < newPositionData.Tick) {
            positionData = cachedPositionData = newPositionData;
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
