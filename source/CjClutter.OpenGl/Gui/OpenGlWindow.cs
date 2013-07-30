using System;
using System.Diagnostics;
using CjClutter.OpenGl.Camera;
using CjClutter.OpenGl.CoordinateSystems;
using CjClutter.OpenGl.Input;
using CjClutter.OpenGl.Input.Keboard;
using CjClutter.OpenGl.Input.Mouse;
using CjClutter.OpenGl.Noise;
using CjClutter.OpenGl.SceneGraph;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using FrameEventArgs = OpenTK.FrameEventArgs;

namespace CjClutter.OpenGl.Gui
{
    public class OpenGlWindow : GameWindow
    {
        private readonly FrameTimeCounter _frameTimeCounter = new FrameTimeCounter();
        private Stopwatch _stopwatch;
        private readonly MouseInputProcessor _mouseInputProcessor;
        private readonly MouseInputObservable _mouseInputObservable;
        private readonly KeyboardInputProcessor _keyboardInputProcessor = new KeyboardInputProcessor();
        private readonly KeyboardInputObservable _keyboardInputObservable;
        private readonly OpenTkCamera _openTkCamera;
        private readonly Scene _scene;
        private readonly Hud _hud;
        private readonly Renderer _renderer;
        private readonly TrackballCamera _trackballCamera;
        private readonly Menu _menu;

        public OpenGlWindow(int width, int height, string title, OpenGlVersion openGlVersion)
            : base(
            width,
            height,
            GraphicsMode.Default,
            title,
            GameWindowFlags.Default,
            DisplayDevice.Default,
            openGlVersion.Major,
            openGlVersion.Minor,
            GraphicsContextFlags.Default)
        {
            //VSync = VSyncMode.Off;

            _mouseInputProcessor = new MouseInputProcessor(this, new GuiToRelativeCoordinateTransformer());

            var buttonUpEventEvaluator = new ButtonUpActionEvaluator(_mouseInputProcessor);
            _mouseInputObservable = new MouseInputObservable(buttonUpEventEvaluator);

            _keyboardInputObservable = new KeyboardInputObservable(_keyboardInputProcessor);

            var trackballCameraRotationCalculator = new TrackballCameraRotationCalculator();
            _trackballCamera = new TrackballCamera(new LookAtCamera(), trackballCameraRotationCalculator);
            _openTkCamera = new OpenTkCamera(_mouseInputProcessor, _trackballCamera);

            _renderer = new Renderer();
            _scene = new Scene();
            _hud = new Hud(this);
            _menu = new Menu(this, _scene);
            _menu.GenerationSettingsControl.SetSettings(new FractalBrownianMotionSettings(6, 0.5, 0.6));
        }

        protected override void OnLoad(EventArgs e)
        {
            _stopwatch = new Stopwatch();
            _stopwatch.Start();

            //update mouse and keyboard processors always, then run update depending on wether the menu or renderer is active

            //Input for both
            _keyboardInputObservable.SubscribeKey(KeyCombination.LeftAlt && KeyCombination.Enter, CombinationDirection.Down, ToggleFullScren);

            //Inputs for "game"
            _keyboardInputObservable.SubscribeKey(KeyCombination.Esc, CombinationDirection.Down, Exit);
            _keyboardInputObservable.SubscribeKey(KeyCombination.P, CombinationDirection.Down, () => _renderer.SetProjectionMode(ProjectionMode.Perspective));
            _keyboardInputObservable.SubscribeKey(KeyCombination.O, CombinationDirection.Down, () => _renderer.SetProjectionMode(ProjectionMode.Orthographic));
            _keyboardInputObservable.SubscribeKey(KeyCombination.Tilde, CombinationDirection.Down, () => _menu.IsEnabled = !_menu.IsEnabled);

            //Inputs for "menu"
            _scene.Reload(_menu.GenerationSettingsControl.GetSettings());
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _scene.Unload();
            _hud.Close();
            _menu.Close();
        }

        private void ToggleFullScren()
        {
            if (WindowState == WindowState.Fullscreen)
            {
                WindowState = WindowState.Normal;
            }
            else if(WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Fullscreen;
            }
        }

        protected override void OnUnload(EventArgs e)
        {
            _scene.Unload();
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
            _renderer.Resize(Width, Height);
            _hud.Resize(Width, Height);
            _menu.Resize(Width, Height);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            ProcessMouseInput();
            ProcessKeyboardInput();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            _frameTimeCounter.UpdateFrameTime(e.Time);

            _scene.Update(ElapsedTime.TotalSeconds);
            _renderer.Render(_scene, _trackballCamera);

            GL.Clear(ClearBufferMask.DepthBufferBit);
            //_hud.Update(ElapsedTime.TotalSeconds, _frameTimeCounter.FrameTime);
            //_hud.Draw();
            _menu.Update();
            _menu.Draw();

            SwapBuffers();
        }

        private void ProcessKeyboardInput()
        {
            if (!Focused)
            {
                return;
            }

            var keyboardState = OpenTK.Input.Keyboard.GetState();

            _keyboardInputProcessor.Update(keyboardState);
            _keyboardInputObservable.ProcessKeys();
        }

        private void ProcessMouseInput()
        {
            if (!Focused)
            {
                return;
            }

            var mouseState = OpenTK.Input.Mouse.GetState();

            _mouseInputProcessor.Update(mouseState);
            _mouseInputObservable.ProcessMouseButtons();

            _openTkCamera.Update();
        }

        public TimeSpan ElapsedTime
        {
            get { return _stopwatch.Elapsed; }
        }
    }
}

public abstract class ProjectionMode
{
    public static readonly ProjectionMode Orthographic = new OrthographicProjection();
    public static readonly ProjectionMode Perspective = new PerspectiveProjection();

    public const int NearPlane = 1;
    public const int FarPlane = 100;

    public abstract Matrix4d ComputeProjectionMatrix(double width, double height);
}

public class PerspectiveProjection : ProjectionMode
{
    public const double FieldOfView = Math.PI/4;

    public override Matrix4d ComputeProjectionMatrix(double width, double height)
    {
        var aspectRatio = width/height;

        return Matrix4d.CreatePerspectiveFieldOfView(FieldOfView, aspectRatio, NearPlane, FarPlane);
    }
}

public class OrthographicProjection : ProjectionMode
{
    public const double CameraWidth = 2;
    public const double CameraHeight = 2;

    public override Matrix4d ComputeProjectionMatrix(double width, double height)
    {
        return Matrix4d.CreateOrthographic(CameraWidth, CameraHeight, NearPlane, FarPlane);
    }
}