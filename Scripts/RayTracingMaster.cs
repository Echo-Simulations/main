using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;

    [Header("Customization")]
    public int Bounces = 7;
    public int Diffractions = 0;
    public int w = Screen.width;
    public int h = Screen.height;

    public System.Guid id
    {
        get { return _id; }
    }

    private System.Guid _id = System.Guid.Empty;
    private Camera _camera;
    private float _lastFieldOfView;
    private Transform _transform;
    private RenderTexture _target;
    private static List<Transform> _transformsToWatch = new List<Transform>();
    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();
    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;
    private NativeArray<byte> _buffer;

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
        public int isSoundSource;
    }

    private void Start()
    {
        _buffer = new NativeArray<byte>(w * h * (Diffractions + 1) * sizeof(byte), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _transform = GetComponent<Transform>();

        _transformsToWatch.Add(transform);
    }

    private void OnEnable()
    {
        if (_id == System.Guid.Empty)
	{
            _id = System.Guid.NewGuid();
	}
    }

    private void OnDisable()
    {
        _id = System.Guid.Empty;

        _meshObjectBuffer?.Release();
        _vertexBuffer?.Release();
        _indexBuffer?.Release();
        _buffer.Dispose();
    }

    private void Update()
    {
        foreach (Transform t in _transformsToWatch)
        {
            if (t.hasChanged)
            {
                t.hasChanged = false;
                _meshObjectsNeedRebuilding = true;
            }
        }
    }

    public static void RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _transformsToWatch.Add(obj.transform);
        _meshObjectsNeedRebuilding = true;
    }

    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }

    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }

        _meshObjectsNeedRebuilding = false;
        //_currentSample = 0;

        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();

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

            // Add the object itself
            _meshObjects.Add(new MeshObject()
            {
                isSoundSource = obj.isSoundSource,
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length
            });
        }

        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 76);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
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
            // If the buffer has been released or wasn't there to
            // begin with, create it
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
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != w || _target.height != h)
        {
            // Release render texture if we already have one
            if (_target != null)
            {
                _target.Release();
            }

            // Get a render target for Ray Tracing
            _target = new RenderTexture(w, h, 0,
                RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            _target.dimension = TextureDimension.Tex2DArray;
            _target.volumeDepth = Diffractions+1;
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(w / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(h / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, Diffractions+1);

        // Blit the result texture to the screen
        //Graphics.Blit(_target, destination);

        //Use an asynchronous readback request to get the data out of the render texture
        AsyncGPUReadback.RequestIntoNativeArray(ref _buffer, _target, 0, OnCompleteReadback);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //Only re-render if something has changed.
        //Causes Unity to throw a warning. Just ignore it, this is intentional.
        if (_meshObjectsNeedRebuilding)
        {
            RebuildMeshObjectBuffers();
            SetShaderParameters();
            Render(destination);
        }
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.Log("GPU readback error detected.");
            return;
        }
        else
        {
            Debug.Log("Native Array acquired.");

            int count = 0;
            for(int i = 0; i < _buffer.Length; i++)
            {
                if(_buffer[i] != 0)
                {
                    count++;
                }
            }
            Debug.Log(count);
            Debug.Log(_buffer.Length);
        }
    }
}
