using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

//[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    [System.Serializable]
    public struct acousticBehavior
    {
        public float hardness;
        public float smoothness;
        public float permeability;
        public float absorbance;
        public float density;
        // ...
    };

    public bool isSoundSource = false;
    public acousticBehavior acoustics;

    private Mesh mesh;
    private acousticBehavior savedAcoustics;

    private bool isRegistered = false; //Used to ensure only valid meshes are used

    private void OnEnable()
    {
        if (this.gameObject.GetComponent<MeshFilter>().mesh != null)
        {
            RayTracingMaster.RegisterObject(this);
            isRegistered = true;
        }

        mesh = transform.GetComponent<MeshFilter>().mesh;
        savedAcoustics = acoustics;
    }

    private void OnDisable()
    {
        if (isRegistered)
        {
            RayTracingMaster.UnregisterObject(this);
            isRegistered = false;
        }

        mesh = null;
    }

    private void FixedUpdate()
    {
#if UNITY_EDITOR
        //Throw an exception if the mesh changes while the object is enabled
        if(transform.GetComponent<MeshFilter>().mesh != mesh)
        {
            Debug.LogError("ERROR: Cannot change ray tracing mesh while it is enabled");
        }
#endif
        //Re-register when acoustic properties change
        if(!(acoustics.Equals(savedAcoustics)) && isRegistered)
        {
            savedAcoustics = acoustics;
            RayTracingMaster.UnregisterObject(this);
            RayTracingMaster.RegisterObject(this);
        }
    }
}
