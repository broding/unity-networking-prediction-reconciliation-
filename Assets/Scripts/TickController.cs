using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TickController : MonoBehaviour {

    [SerializeField] private int targetBuffer = 4;
    [SerializeField] private int maxBuffer = 50;
    [SerializeField] private int inputLostIncrease = 10;
    [SerializeField] private float recoverSpeed = 0.5f;
    [SerializeField] private int hardResetTickDelta = 100;

    private float currentBuffer;

    public void ReportInputLoss() {
        currentBuffer += inputLostIncrease;
        currentBuffer = Mathf.Min(currentBuffer, maxBuffer);
    }

    private void Awake() {
        currentBuffer = targetBuffer;
    }

    private void Update() {
        NetworkManager.Singleton.NetworkTimeSystem.LocalBufferSec = currentBuffer / 60f;
        NetworkManager.Singleton.NetworkTimeSystem.HardResetThresholdSec = hardResetTickDelta / 60f;
        currentBuffer = Mathf.Lerp(currentBuffer, targetBuffer, Time.deltaTime * recoverSpeed);
    }

    public static TickController Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<TickController>();
            }

            return instance;
        }
    }
    private static TickController instance;
}
