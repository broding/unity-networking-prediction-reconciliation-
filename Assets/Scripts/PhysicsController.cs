using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhysicsController {

    private Scene scene;
    private Scene defaultScene;
    private PhysicsScene physicsScene;
    private Rigidbody target;

    public PhysicsController() {
        scene = SceneManager.CreateScene("Physics");
        physicsScene = scene.GetPhysicsScene();
    }

    public void MoveRigidbodyToScene(Rigidbody target) {
        this.target = target;
        defaultScene = target.gameObject.scene;
        SceneManager.MoveGameObjectToScene(target.gameObject, scene);
    }

    public void ReturnRigidbody() {
        SceneManager.MoveGameObjectToScene(target.gameObject, defaultScene);
    }

    public void SimulatePhysics(float step) {
        physicsScene.Simulate(step);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSingleton() {
        instance = null;
    }

    public static PhysicsController Instance {
        get {
            if (instance == null) {
                instance = new PhysicsController();
            }

            return instance;
        }
    }
    private static PhysicsController instance;

}
