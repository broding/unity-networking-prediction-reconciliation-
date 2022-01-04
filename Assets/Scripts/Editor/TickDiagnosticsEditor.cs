using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class TickDiagnosticsEditor : EditorWindow {

    [MenuItem("Window/Netcode For GameObjects/Tick Diagnostics")]
    public static void ShowWindow() {
        EditorWindow.GetWindow(typeof(TickDiagnosticsEditor));
    }

    void OnGUI() {
        if(NetworkManager.Singleton == null) {
            return;
        }

        int localTick = NetworkManager.Singleton.LocalTime.Tick;
        int tickDelta = localTick - NetworkManager.Singleton.ServerTime.Tick;
        uint tickRate = NetworkManager.Singleton.NetworkTickSystem.TickRate;
        double localBuffer = NetworkManager.Singleton.NetworkTimeSystem.LocalBufferSec;

        EditorGUILayout.LabelField("Tick rate", tickRate.ToString());
        EditorGUILayout.LabelField("Local tick buffer", (localBuffer * tickRate).ToString());
        EditorGUILayout.LabelField("Tick difference", (tickDelta).ToString());
        EditorGUILayout.LabelField("Real RTT", $"{tickDelta * (1f / tickRate)} sec.");
        NetworkManager.Singleton.NetworkTimeSystem.LocalBufferSec = EditorGUILayout.Slider((float)localBuffer, 0.001f, 1);
    }

    public void OnInspectorUpdate() {
        Repaint();
    }
}
