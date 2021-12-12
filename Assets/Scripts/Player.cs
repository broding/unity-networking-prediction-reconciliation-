using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour {

    [SerializeField] private Rigidbody rb;

    private List<InputData> inputBuffer = new List<InputData>();
    private List<PositionData> positionBuffer = new List<PositionData>();
    private InputData previousInput;
    private int previousTick;
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
        }
    }

    private void OnClientTick() {
        if(previousTick >= NetworkManager.LocalTime.Tick) {
            Debug.Log("Tick has reset");
            InvalidateCachesFromTick(NetworkManager.LocalTime.Tick);
        }

        // Check position/rotation threshold here. If too much, reconcile.

        PositionData positionData = positionBuffer.FirstOrDefault();
        if(positionData != null) {
            rb.position = positionData.Position;
            rb.rotation = positionData.Rotation;
            rb.velocity = positionData.Velocity;
            rb.angularVelocity = positionData.AngularVelocity;
            positionBuffer.Remove(positionData);
            Debug.Log("input to reconsile: " + inputBuffer.Count(i => i.Tick > positionData.Tick));
            foreach (InputData data in inputBuffer.Where(i => i.Tick > positionData.Tick)) {
                ProcessInput(data);
                Physics.Simulate(NetworkManager.NetworkTickSystem.LocalTime.FixedDeltaTime);
            }
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
    private void SendPosition_ClientRpc(PositionData positionData) {
        positionBuffer.Add(positionData);
        positionBuffer = positionBuffer.OrderBy(p => p.Tick).ToList();
    }

    private void OnDrawGizmos() {
        PositionData latestPositionData = positionBuffer.Last();
        if (latestPositionData != null) {
            Gizmos.matrix = Matrix4x4.TRS(latestPositionData.Position, latestPositionData.Rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
