using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct InputData : INetworkSerializable {

    public Vector2 Input;
    public int Tick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref Input);
        serializer.SerializeValue(ref Tick);
    }
}
