﻿module Terrain

open Rendering
open OpenTK
open CjClutter.OpenGl
open CjClutter.OpenGl.OpenGl
open CjClutter.OpenGl.Gui
open CjClutter.OpenGl.EntityComponent
open OpenTK.Graphics.OpenGL4

type node = ChunkedLodTreeFactory.ChunkedLodTreeNode

let resourceFactory = new ResourceAllocator(new OpenGlResourceFactory())

let allocate (node:node) =
    let factory = new TerrainChunkFactory()
    let mesh = factory.Create(node.Bounds)
    let allocatedMesh = resourceFactory.AllocateResourceFor(mesh)
    let bounds = node.Bounds
    let translation = Matrix4.CreateTranslation(float32 bounds.Center.X, 0.0f, float32 bounds.Center.Y)
    let delta = bounds.Max - bounds.Min
    let scale = Matrix4.CreateScale(float32 delta.X, 1.0f, float32 delta.Y)
    let bind () =
        allocatedMesh.CreateVAO()
        allocatedMesh.VertexArrayObject.Bind()
    { 
        bind = bind
        faces = mesh.Faces.Length
        renderContext = {
                            ModelMatrix = scale * translation
                            NormalMatrix = Matrix3.Identity
                        }
    }

type NoiseShaderProgram = 
    {
        programId : int
    }

let allocateOnGpu (programId:int) (node:node) =
    let buffer = GL.GenBuffer()
    GL.BindBuffer(BufferTarget.ShaderStorageBuffer, buffer)
    GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, buffer)
    GL.UseProgram(programId)
    GL.DispatchCompute(1, 1, 1)
    let bounds = node.Bounds
    let translation = Matrix4.CreateTranslation(float32 bounds.Center.X, 0.0f, float32 bounds.Center.Y)
    let delta = bounds.Max - bounds.Min
    let scale = Matrix4.CreateScale(float32 delta.X, 1.0f, float32 delta.Y)
    {
        bind = fun () -> ()
        faces = 1
        renderContext = {
                            ModelMatrix = scale * translation
                            NormalMatrix = Matrix3.Identity
                        }
    }

let makeTerrainLodTree =
    CjClutter.OpenGl.Terrain.CreateTree()