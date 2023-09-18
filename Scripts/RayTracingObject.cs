using UnityEngine;

//[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    public int isSoundSource = 0;

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