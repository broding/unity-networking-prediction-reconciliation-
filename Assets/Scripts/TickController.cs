using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TickController : MonoBehaviour {

    [SerializeField] private int targetBuffer = 4;
    [SerializeField] private int inputLostIncrease = 10;
    [SerializeField] private float recoverSpeed = 0.5f;

    private float currentBuffer;

    public void ReportInputLoss() {
        currentBuffer += inputLostIncrease;
    }

    private void Update() {
        NetworkManager.Singleton.NetworkTimeSystem.LocalBufferSec = currentBuffer / 60f;
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
