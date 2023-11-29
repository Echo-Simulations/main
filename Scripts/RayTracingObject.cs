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

    private bool isSoundSource = false;
    private int soundSourceId = 0;
    //Make public if you want to interact with acoustic properties
    private acousticBehavior acoustics;

    private Mesh mesh;
    private acousticBehavior savedAcoustics;

    private bool isRegistered = false; //Used to ensure only valid meshes are used

    private void OnEnable()
    {
        if (this.gameObject.GetComponent<MeshFilter>().mesh != null)
        {
            if(this.gameObject.GetComponent<AudioProcessor>() != null)
            {
                isSoundSource = true;
            }
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
        isSoundSource = false;
    }

    private void OnDestroy()
    {
        if (isRegistered)
        {
            RayTracingMaster.UnregisterObject(this);
            isRegistered = false;
        }

        mesh = null;
        isSoundSource = false;
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

    public int Id
    {
        get { return soundSourceId; }
        set { soundSourceId = value; }
    }

    public bool IsSoundSource
    {
        get { return isSoundSource;  }
    }
}
