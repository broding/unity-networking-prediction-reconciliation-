using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PositionData : INetworkSerializable {
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 AngularVelocity;
    public int Tick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref Velocity);
        serializer.SerializeValue(ref AngularVelocity);
        serializer.SerializeValue(ref Tick);
    }

    public float CalculateDelta(PositionData other) {
        float positionDelta = (Position - other.Position).sqrMagnitude;
        float rotationDelta = Quaternion.Angle(Rotation, other.Rotation);
        float velocityDelta = (Velocity - other.Velocity).sqrMagnitude;
        float angularVelocityDelta = (AngularVelocity - other.AngularVelocity).sqrMagnitude;

        return positionDelta + rotationDelta + velocityDelta + angularVelocityDelta;
    }
}
