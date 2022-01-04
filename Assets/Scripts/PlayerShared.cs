using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerShared : NetworkBehaviour {

    [Header("References")]
    [SerializeField] private new Rigidbody rigidbody;

    public event Action<InputDataHistory> InputReceivedFromClient;
    public event Action<PositionData> PositionReceivedFromServer;

    public void ResetToPositionData(PositionData positionData) {
        rigidbody.position = positionData.Position;
        rigidbody.rotation = positionData.Rotation;
        rigidbody.velocity = positionData.Velocity;
        rigidbody.angularVelocity = positionData.AngularVelocity;
    }

    public void ApplyForcesForInput(InputData inputData, float fixedDeltaTime) {
        Vector3 force = inputData.Input * 50;
        force.z = force.y;
        force.y = 0;
        rigidbody.AddForce(force);
    }

    public PositionData GeneratePositionData(int tick) {
        return new PositionData() {
            Position = rigidbody.position,
            Rotation = rigidbody.rotation,
            Velocity = rigidbody.velocity,
            AngularVelocity = rigidbody.angularVelocity,
            Tick = tick
        };
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    public void SendInput_ServerRpc(InputDataHistory inputDataHistory) {
        InputReceivedFromClient?.Invoke(inputDataHistory);
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    public void SendPosition_ClientRpc(PositionData newPositionData) {
        PositionReceivedFromServer?.Invoke(newPositionData);
    }
}
