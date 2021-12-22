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

        int tickDelta = NetworkManager.Singleton.LocalTime.Tick - NetworkManager.Singleton.ServerTime.Tick;

        EditorGUILayout.LabelField("Tick rate", NetworkManager.Singleton.NetworkTickSystem.TickRate.ToString());
        EditorGUILayout.LabelField("Local tick buffer", (NetworkManager.Singleton.NetworkTimeSystem.LocalBufferSec * NetworkManager.Singleton.NetworkTickSystem.TickRate).ToString());
        EditorGUILayout.LabelField("Tick difference", (NetworkManager.Singleton.LocalTime.Tick - NetworkManager.Singleton.ServerTime.Tick).ToString());
        EditorGUILayout.LabelField("Real RTT", $"{tickDelta * (1f / NetworkManager.Singleton.NetworkTickSystem.TickRate)} sec.");
    }

    public void OnInspectorUpdate() {
        Repaint();
    }
}
