using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class InputDataHistory : INetworkSerializable {

    public InputData[] InputDatas;
    public int Tick;

    public InputDataHistory(InputData[] inputDatas, int tick) {
        this.InputDatas = inputDatas;
        this.Tick = tick;
    }

    public InputDataHistory() { }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref Tick);

        int length = 0;
        if (serializer.IsWriter) {
            length = InputDatas.Length;
        }

        serializer.SerializeValue(ref length);

        if (serializer.IsReader) {
            InputDatas = new InputData[length];
        }

        for(int i = 0; i < length; i++) {
            serializer.SerializeValue(ref InputDatas[i]);
        }
    }
}
