using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    public int isListener = 0;

    private void OnEnable()
    {
        RayTracingMaster.RegisterObject(this);
        if (gameObject.tag == "Listener") 
            isListener = 1;
    }

    private void OnDisable()
    {
        RayTracingMaster.UnregisterObject(this);
    }
}