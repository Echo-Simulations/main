# Echo Simulations - Audio Ray Tracer

## Information

This repository contains an implementation of an audio ray tracing engine for Unity 2022.3.7f1 in C# using Unity's API. Rays are simulated using the GPU using a compute shader to achieve superior performance. The officially supported platforms are desktop Windows and Linux with a dedicated GPU.

## Setup 

1. Download Unity 2022.1.23f1
1. Open a project
1. Make a simple scene
1. Drag the Scripts folder into the Unity Assets folder
1. Drag and drop the Ray Tracing Master.cs script onto the listener
2. Add a camera component to the listener
1. Drag and drop the Ray Tracing Object.cs script onto all meshes that you want to include in the demo
2. Increment the "sound source" property of sound sources to a non-zero value
1. Drag and drop TraceAudioRays.compute into the Ray Tracing Master.cs 'Ray Tracing Shader' field
1. Press play

## Set Graphics API

1. File -> Build Settings -> Player settings... -> Player -> Settings for Windows, Mac, Linux -> Other settings
2. Uncheck 'Auto Graphics API for Windows', remove DX11, and add Vulkan
3. Restart Unity editor if prompted
