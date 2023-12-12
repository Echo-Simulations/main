# Echo Simulations - Audio Ray Tracer

## Information

This repository contains an implementation of an audio ray tracing engine for Unity 2022.1.23f1 in C# using Unity's API. Rays are simulated using the GPU using a compute shader to achieve superior performance. The officially supported platforms are desktop Windows and Linux with a dedicated GPU.

## Setup 

1. Install Vulkan from https://www.vulkan.org/
1. Download Unity 2022.1.23f1
1. Open a Unity 2022.1.23f1 project
1. Make a scene
1. Drag the Scripts folder into the project's Assets folder
1. Drag and drop the RayTracingMaster.cs script onto the listener object. Drag and drop TraceAudioRays.compute into the RayTracingMaster.cs 'Ray Tracing Shader' field
1. Drag and drop the AudioProcessor.cs script onto all audio sources. Give each audio source an audio clip
1. Drag and drop the RayTracingObject.cs script onto all other meshes that you want to include in the process
1. Press play

## Set Graphics API (Optional)

1. File -> Build Settings -> Player settings... -> Player -> Settings for Windows, Mac, Linux -> Other settings
2. Uncheck 'Auto Graphics API for Windows', remove DX11, and add Vulkan
3. Restart Unity editor if prompted
