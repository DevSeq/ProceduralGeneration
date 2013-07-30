using System;
using System.Collections.Generic;
using CjClutter.OpenGl.Camera;
using CjClutter.OpenGl.OpenGl;
using CjClutter.OpenGl.OpenGl.Shaders;
using CjClutter.OpenGl.OpenTk;
using CjClutter.OpenGl.SceneGraph;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace CjClutter.OpenGl.Gui
{
    public class Renderer
    {
        private readonly Dictionary<SceneObject, MeshResources> _resources = new Dictionary<SceneObject, MeshResources>();
        private readonly ResourceAllocator _resourceAllocator;
        private ProjectionMode _projectionMode = ProjectionMode.Perspective;
        private Matrix4d _projectionMatrix;
        private Vector2 _windowScale;

        public Renderer()
        {
            _resourceAllocator = new ResourceAllocator(new OpenGlResourceFactory());
        }

        public void Render(Scene scene, ICamera camera)
        {
            var cameraMatrix = camera.GetCameraMatrix();
            scene.ViewMatrix = cameraMatrix;
            scene.ProjectionMatrix = _projectionMatrix;

            GL.ClearColor(Color4.White);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            foreach (var sceneObject in scene.SceneObjects)
            {
                var resources = GetOrCreateResources(sceneObject);
                var sceneObjectLocalCopy = sceneObject;
                RunWithResourcesBound(
                    () => DrawMesh(scene, sceneObjectLocalCopy, resources),
                    resources.RenderableMesh.VertexArrayObject,
                    resources.RenderProgram);

                RunWithResourcesBound(
                    () => DrawNormals(scene, sceneObjectLocalCopy, resources),
                    resources.RenderableMesh.VertexArrayObject,
                    resources.NormalDebugProgram);
            }
        }

        private void DrawMesh(Scene scene, SceneObject sceneObject, MeshResources meshResources)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.CullFace);
            GL.FrontFace(FrontFaceDirection.Cw);

            var projectionMatrix = scene.ProjectionMatrix.ToMatrix4();
            meshResources.RenderProgram.ProjectionMatrix.Set(projectionMatrix);

            var viewMatrix = scene.ViewMatrix.ToMatrix4();
            meshResources.RenderProgram.ViewMatrix.Set(viewMatrix);

            meshResources.RenderProgram.Color.Set(sceneObject.Color);

            meshResources.RenderProgram.WindowScale.Set(_windowScale);
            meshResources.RenderProgram.ModelMatrix.Set(sceneObject.ModelMatrix);

            GL.DrawElements(BeginMode.Triangles, sceneObject.Mesh.Faces.Length * 3, DrawElementsType.UnsignedInt, 0);
        }

        private void DrawNormals(Scene scene, SceneObject sceneObject, MeshResources meshResources)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.CullFace);
            GL.FrontFace(FrontFaceDirection.Cw);

            var projectionMatrix = scene.ProjectionMatrix.ToMatrix4();
            meshResources.NormalDebugProgram.ProjectionMatrix.Set(projectionMatrix);

            var viewMatrix = scene.ViewMatrix.ToMatrix4();
            meshResources.NormalDebugProgram.ViewMatrix.Set(viewMatrix);

            meshResources.NormalDebugProgram.ModelMatrix.Set(sceneObject.ModelMatrix);

            GL.DrawElements(BeginMode.Triangles, sceneObject.Mesh.Faces.Length * 3, DrawElementsType.UnsignedInt, 0);
        }

        private void RunWithResourcesBound(Action action, params IBindable[] bindables)
        {
            foreach (var bindable in bindables)
            {
                bindable.Bind();
            }

            action();

            foreach (var bindable in bindables)
            {
                bindable.Unbind();
            }
        }

        private MeshResources GetOrCreateResources(SceneObject sceneObject)
        {
            if (_resources.ContainsKey(sceneObject))
            {
                return _resources[sceneObject];
            }

            var renderableMesh = _resourceAllocator.AllocateResourceFor(sceneObject.Mesh);

            var simpleRenderProgram = new SimpleRenderProgram();
            simpleRenderProgram.Create();

            var normalDebugProgram = new NormalDebugProgram();
            normalDebugProgram.Create();

            var resources = new MeshResources
                {
                    RenderProgram = simpleRenderProgram,
                    RenderableMesh = renderableMesh,
                    NormalDebugProgram = normalDebugProgram,
                };

            _resources.Add(sceneObject, resources);
            return resources;
        }

        private void ReleaseResources(SceneObject sceneObject)
        {
            var resources = _resources[sceneObject];
            resources.RenderProgram.Delete();
            resources.RenderableMesh.Delete();
        }

        public void Resize(int width, int height)
        {
            _projectionMatrix = CreateProjectionMatrix(width, height);
            _windowScale = new Vector2(width, height);
        }

        private Matrix4d CreateProjectionMatrix(float width, float height)
        {
            return _projectionMode.ComputeProjectionMatrix(width, height);
        }

        public void SetProjectionMode(ProjectionMode projectionMode)
        {
            _projectionMode = projectionMode;
            _projectionMatrix = CreateProjectionMatrix(_windowScale.X, _windowScale.Y);
        }
    }
}