using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CustomNetworkManager : NetworkManager {

    public event Action BeforeTick;
    public event Action AfterTick;

    public void Tick() {
        BeforeTick?.Invoke();
        Physics.Simulate(NetworkTickSystem.LocalTime.FixedDeltaTime);
        AfterTick?.Invoke();
    }
}
