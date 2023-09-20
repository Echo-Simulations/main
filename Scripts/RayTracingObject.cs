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

    private void OnEnable()
    {
        RayTracingMaster.RegisterObject(this);
        //if (gameObject.tag == "Listener") 
        //    isSoundSource = 1;
    }

    private void OnDisable()
    {
        RayTracingMaster.UnregisterObject(this);
    }
}
