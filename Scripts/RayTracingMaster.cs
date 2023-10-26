//#define SHOW_READBACK_INFO // Define this to show readback debug information

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;

    [Header("Customization")]
    //Range values are mostly tentative/arbitrary EXCEPT FOR DIFFRACTIONS. DO NOT CHANGE DIFFRACTIONS.
    [Range(0, 15)]
    public int Bounces = 0; //The maximum number of reflections allowed
    [Range(0,7)]
    public int Diffractions = 0; //The maximum number of diffractions allowed
    [Range(1, 1024)]
    public int w = Screen.width; //The width of the ray tracing texture
    [Range(1, 1024)]
    public int h = Screen.height; //The height of the ray tracing texture

    private Transform _transform; //The transform of the listener (the object this script is attached to)
    private RenderTexture _target; //The texture the GPU writes to; does not exist in CPU memory

    private static List<Transform> _transformsToWatch = new List<Transform>(); //An array of transforms of relevant objects
    private static bool _meshObjectsNeedRebuilding = false; //A flag for if the scene has changed
    private static bool _transformsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>(); //An array of objects that rays collide with
    private static List<RayTracingObject> _soundSources = new List<RayTracingObject>(); //An array of objects that are also sound sources
    private static List<AudioProcessor> _sourceProcessors = new List<AudioProcessor>(); //An array of audio processors on sound sources

    private static List<MeshObject> _meshObjects = new List<MeshObject>(); //An array of all mesh data in the scene in the CPU
    private static List<Vector3> _vertices = new List<Vector3>(); //An array of all vertexes in the scene in the CPU
    private static List<int> _indices = new List<int>(); //An array of all polygon data in the scene in the CPU
    private ComputeBuffer _meshObjectBuffer; //An array of all mesh data in the scene in the GPU
    private ComputeBuffer _vertexBuffer; //An array of all vertexes in the scene in the GPU
    private ComputeBuffer _indexBuffer; //An array of all polygon data in the scene in the GPU

    private static List<Vector3> _rayPos = new List<Vector3>();
    private static List<Vector3> _rayDir = new List<Vector3>();
    private static List<int> _rayEnabled = new List<int>();
    private ComputeBuffer _rayPosBuffer;
    private ComputeBuffer _rayDirBuffer;
    private ComputeBuffer _rayEnabledBuffer;

    private NativeArray<float> _buffer; //The return value of the ray tracing, expressed as a float array
    private bool _isBusy = false; //Used to avoid timing issues

    private const int _parameterCount = 2; //Represents the number of channels necessary per ray
    private const int _computeBufferCount = 6; //Represents the number of compute buffers we are using

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
        public int isSoundSource;
        public Vector3 center;
        public Vector3 extents;
    }

    //Plays immediately upon application startup
    private void Awake()
    {
#if UNITY_EDITOR
        //Test if there is hardware support for all of the features the program needs
        if (!(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) &&
            SystemInfo.supports2DArrayTextures &&
            SystemInfo.supportsAsyncGPUReadback &&
            SystemInfo.supportsComputeShaders &&
            SystemInfo.maxComputeBufferInputsCompute >= _computeBufferCount))
        {
            Debug.LogError("[" + GetType().ToString() + "] ERROR: Hardware compatibility");
        }
#endif

        _transform = GetComponent<Transform>();

        _transformsToWatch.Add(transform);
    }

    //Plays upon application quit
    private void OnApplicationQuit()
    {
        _meshObjectBuffer?.Release();
        _vertexBuffer?.Release();
        _indexBuffer?.Release();
        _rayPosBuffer?.Release();
        _rayDirBuffer?.Release();
        _rayEnabledBuffer?.Release();
    }

    //Plays once every frame
    private void Update()
    {
        foreach (Transform t in _transformsToWatch)
        {
            if (t.hasChanged)
            {
                t.hasChanged = false;
                //_meshObjectsNeedRebuilding = true;
                _transformsNeedRebuilding = true;
            }
        }
    }

    public static void RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _transformsToWatch.Add(obj.transform);
        if (obj.isSoundSource)
        {
            if (_soundSources.Count < 255)
            {
                _soundSources.Add(obj);
                _sourceProcessors.Add(obj.GetComponent<AudioProcessor>());
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogError("ERROR: Too many active sound sources");
            }
#endif
        }
        _meshObjectsNeedRebuilding = true;
    }

    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        if (obj.isSoundSource){
            _soundSources.Remove(obj);
            _sourceProcessors.Remove(obj.GetComponent<AudioProcessor>());
        }
        _meshObjectsNeedRebuilding = true;
    }

    //Remakes internal buffers with current scene geometry data
    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }

        _meshObjectsNeedRebuilding = false;

        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();

        // Add the default cube to the list
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        _vertices.AddRange(cube.vertices); // 8 vertices
        var faces = cube.GetIndices(0);
        _indices.AddRange(faces.Select(index => index)); // 12 polys (36 entries)

        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;

            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);

            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));

            // Calculate sound source identifier
            int id = 0;
            if (obj.isSoundSource)
            {
                id = _soundSources.FindIndex(x => x.gameObject == obj.gameObject)+1;
            }

            // Add the object itself
            _meshObjects.Add(new MeshObject()
            {
                isSoundSource = id,
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length,
                center = mesh.bounds.center,
                extents = mesh.bounds.extents
            });
        }

        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 100);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }

    private void RebuildTransformationMatrices()
    {
        _transformsNeedRebuilding = false;

        List<MeshObject> updatedMeshObjects = new List<MeshObject>(_meshObjects);
        for (int i = 0; i < _rayTracingObjects.Count; i++)
        {
            updatedMeshObjects[i] = new MeshObject()
            {
                isSoundSource = updatedMeshObjects[i].isSoundSource,
                localToWorldMatrix = _rayTracingObjects[i].transform.localToWorldMatrix,
                indices_offset = updatedMeshObjects[i].indices_offset,
                indices_count = updatedMeshObjects[i].indices_count,
                center = updatedMeshObjects[i].center,
                extents = updatedMeshObjects[i].extents
            };
        }

        _meshObjects = updatedMeshObjects;
        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 100);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
        where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }

        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }

            // Set data on the buffer
            buffer.SetData(data);
        }
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }

    //Primes the compute shader
    private void SetShaderParameters()
    {
        //Pass in the source location
        Matrix4x4 matrix = _transform.localToWorldMatrix;
        matrix[0, 2] = -1.0f * matrix[0, 2];
        matrix[1, 2] = -1.0f * matrix[1, 2];
        matrix[2, 2] = -1.0f * matrix[2, 2];
        matrix[3, 2] = -1.0f * matrix[3, 2];
        RayTracingShader.SetMatrix("_Source", matrix);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        RayTracingShader.SetFloat("_Seed", Random.value);

        RayTracingShader.SetInt("_Bounces", Bounces+1);
        RayTracingShader.SetInt("_Diffractions", Diffractions);

        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);

        _rayPos.Clear();
        _rayDir.Clear();
        _rayEnabled.Clear();
        for (int i = 0; i < w * h * (Diffractions + 1); i++)
        {
            _rayPos.Add(new Vector3(0.0f, 0.0f, 0.0f));
            _rayDir.Add(new Vector3(0.0f, 0.0f, 0.0f));
            if(i < w * h)
            {
                _rayEnabled.Add(0);
            }
        }
        CreateComputeBuffer(ref _rayPosBuffer, _rayPos, 12);
        CreateComputeBuffer(ref _rayDirBuffer, _rayDir, 12);
        CreateComputeBuffer(ref _rayEnabledBuffer, _rayEnabled, 4);
        SetComputeBuffer("_RayPos", _rayPosBuffer);
        SetComputeBuffer("_RayDir", _rayDirBuffer);
        SetComputeBuffer("_RayEnabled", _rayEnabledBuffer);
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != w || _target.height != h || _target.volumeDepth != (Diffractions + 1) * _parameterCount)
        {
            // Release render texture if we already have one
            if (_target != null)
            {
                _target.Release();
            }

            // Get a render target for Ray Tracing
            _target = new RenderTexture(w, h, 0,
                RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            _target.dimension = TextureDimension.Tex2DArray;
            _target.volumeDepth = (Diffractions+1) * _parameterCount;
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }

    //Dispatches the compute shader and returns its result asynchronously
    private void Render()
    {
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(w / 4.0f);
        int threadGroupsY = Mathf.CeilToInt(h / 4.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, (Diffractions+1));

        //Use an asynchronous readback request to get the data out of the render texture
        if (_isBusy == false)
        {
            _isBusy = true;
            _buffer = new NativeArray<float>(w * h * (Diffractions + 1) * _parameterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            AsyncGPUReadback.RequestIntoNativeArray(ref _buffer, _target, 0, OnCompleteReadback);
        }
    }

    //Plays every fixed timestep independently of actual framerate
    //The exact rate is controlled by the project settings
    private void FixedUpdate()
    {
        //Only re-render if something has changed.
        //Has a slim chance of causing unexpected behavior
        if (_meshObjectsNeedRebuilding || _transformsNeedRebuilding)
        {
            if (_meshObjectsNeedRebuilding)
            {
                RebuildMeshObjectBuffers();
            }
            else
            {
                RebuildTransformationMatrices();
            }
            SetShaderParameters();
            Render();
        }
    }

    //Plays whenever the compute shader finishes execution
    //Will skip some frames when there are multiple updates back-to-back
    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            //Debug.Log("GPU readback error detected.");
            _isBusy = false;
            return;
        }
        else
        {
            //Debug.Log("Native Array acquired.");

            //This is where you would make the program use the buffer data
            //Right now, make it print some basic data
#if SHOW_READBACK_INFO && UNITY_EDITOR
            int count = 0;
            for(int i = 0; i < _buffer.Length / _parameterCount; i++)
            {
                if(_buffer[i] > 0.0f)
                {
                    count++;
                }
            }
            Debug.Log(count + "/" + _buffer.Length / _parameterCount);
#endif
            // Send the texture to the audio processor if this object has the
            // component.
            if (_sourceProcessors.Count != 0 && _sourceProcessors[0] != null)
            {
                _sourceProcessors[0].SendTexture(_buffer.ToArray(),
                    w*h, _parameterCount, Diffractions);
            }
        }
        _buffer.Dispose();
        _isBusy = false;
    }
}
