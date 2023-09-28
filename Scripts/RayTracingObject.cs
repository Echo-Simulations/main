using UnityEngine;

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

    public int isSoundSource = 0;
    public acousticBehavior acoustics;

    private bool isRegistered = false; //Used to ensure only valid meshes are used

    private void OnEnable()
    {
        if (this.gameObject.GetComponent<MeshFilter>().mesh != null)
        {
            RayTracingMaster.RegisterObject(this);
            isRegistered = true;
        }
    }

    private void OnDisable()
    {
        if (isRegistered)
        {
            RayTracingMaster.UnregisterObject(this);
            isRegistered = false;
        }
    }
}
