using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhysicsController : MonoBehaviour {

    public event Action OnBeforePhysicsTick;
    public event Action OnAfterPhysicsTick;

    private Scene temporaryScene;
    private Scene defaultScene;
    private PhysicsScene temporaryPhysicsScene;

    private GameObject target;

    public void Initialize() { 
        NetworkManager.Singleton.NetworkTickSystem.Tick += OnTick;

        if (NetworkManager.Singleton.IsClient) {
            temporaryScene = SceneManager.CreateScene("Physics");
            temporaryPhysicsScene = temporaryScene.GetPhysicsScene();
        }
    }

    private void OnDestroy() {
        NetworkManager.Singleton.NetworkTickSystem.Tick -= OnTick;
    }

    private void OnTick() {
        OnBeforePhysicsTick?.Invoke();
        Physics.Simulate(NetworkManager.Singleton.NetworkTickSystem.LocalTime.FixedDeltaTime);
        OnAfterPhysicsTick?.Invoke();
    }

    public void MoveGameObjectToTemporaryScene(GameObject gameObject) {
        this.target = gameObject;
        defaultScene = target.scene;
        SceneManager.MoveGameObjectToScene(target, temporaryScene);
    }

    public void ReturnRigidbody() {
        SceneManager.MoveGameObjectToScene(target, defaultScene);
    }

    public void SimulatePhysics(float step) {
        temporaryPhysicsScene.Simulate(step);
    }

    public static PhysicsController Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<PhysicsController>();
            }

            return instance;
        }
    }

    private static PhysicsController instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSingleton() {
        instance = null;
    }
}
