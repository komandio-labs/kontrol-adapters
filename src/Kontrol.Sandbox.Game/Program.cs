using Kontrol.Sdk.IPC;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Kontrol.Sandbox.Game;

public static class Program
{
    private static GL? _gl;
    private static IWindow? _window;
    private static uint _program, _vao, _vbo;

    public static void Main(string[] args)
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1000, 620),
            Title = "Kontrol Sandbox Game — no adapter loaded",
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3))
        };
        _window = Window.Create(options);
        _window.Load += InitializeRenderer;
        _window.Render += _ => Render();
        _window.Update += _ => UpdateTitle();
        _window.Run();
        _gl?.Dispose();
    }


    private static void Render()
    {
        if (_gl is null) return;
        var c = SandboxControlState.Snapshot();
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        // Header/state strip: green while Kontrol input is active, amber while paused.
        DrawRect(-.95f, .82f, 1.9f, .10f, c.IsInputEnabled != 0 ? (.10f, .85f, .40f) : (.90f, .55f, .10f));

        // Six generic live input bars: three translation inputs on the left, three rotation inputs on the right.
        DrawAxis(-.92f, .52f, GetAnalog(c, 0), (.20f, .75f, 1f)); DrawAxis(-.92f, .27f, GetAnalog(c, 1), (.20f, .75f, 1f)); DrawAxis(-.92f, .02f, GetAnalog(c, 2), (.20f, .75f, 1f));
        DrawAxis(.08f, .52f, GetAnalog(c, 3), (1f, .35f, .55f)); DrawAxis(.08f, .27f, GetAnalog(c, 4), (1f, .35f, .55f)); DrawAxis(.08f, .02f, GetAnalog(c, 5), (1f, .35f, .55f));
        DrawAction(-.70f, -.55f, (c.DiscreteStates & (1UL << 6)) != 0); DrawAction(-.18f, -.55f, (c.DiscreteStates & (1UL << 7)) != 0); DrawAction(.34f, -.55f, (c.TriggeredActions & (1UL << 8)) != 0);

    }

    private static unsafe void InitializeRenderer()
    {
        _gl = GL.GetApi(_window!);
        const string vertex = "#version 330 core\nlayout(location=0) in vec2 p; layout(location=1) in vec3 c; out vec3 v; void main(){gl_Position=vec4(p,0,1);v=c;}";
        const string fragment = "#version 330 core\nin vec3 v; out vec4 o; void main(){o=vec4(v,1);}";
        uint vs = _gl.CreateShader(ShaderType.VertexShader); _gl.ShaderSource(vs, vertex); _gl.CompileShader(vs);
        uint fs = _gl.CreateShader(ShaderType.FragmentShader); _gl.ShaderSource(fs, fragment); _gl.CompileShader(fs);
        _program = _gl.CreateProgram(); _gl.AttachShader(_program, vs); _gl.AttachShader(_program, fs); _gl.LinkProgram(_program); _gl.DeleteShader(vs); _gl.DeleteShader(fs);
        _vao = _gl.GenVertexArray(); _vbo = _gl.GenBuffer(); _gl.BindVertexArray(_vao); _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), (void*)0); _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), (void*)(2 * sizeof(float))); _gl.EnableVertexAttribArray(1);
    }

    private static unsafe void DrawAxis(float x, float y, float value, (float r, float g, float b) color)
    {
        DrawRect(x, y, .84f, .11f, (.12f, .12f, .15f));
        DrawRect(x + .42f, y - .015f, .006f, .14f, (.35f, .35f, .4f));
        float width = Math.Abs(value) * .42f;
        DrawRect(value >= 0 ? x + .42f : x + .42f - width, y, width, .11f, color);
    }
    private static void DrawAction(float x, float y, bool pressed) => DrawRect(x, y, .36f, .16f, pressed ? (.15f, .9f, .4f) : (.18f, .18f, .22f));

    private static unsafe void DrawRect(float x, float y, float width, float height, (float r, float g, float b) color)
    {
        if (_gl is null || width <= 0) return;
        float x2=x+width, y2=y+height; float[] v = { x,y,color.r,color.g,color.b, x2,y,color.r,color.g,color.b, x2,y2,color.r,color.g,color.b, x,y,color.r,color.g,color.b, x2,y2,color.r,color.g,color.b, x,y2,color.r,color.g,color.b };
        fixed (float* p = v) { _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo); _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(v.Length*sizeof(float)), p, BufferUsageARB.DynamicDraw); }
        _gl.UseProgram(_program); _gl.BindVertexArray(_vao); _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    private static void UpdateTitle()
    {
        if (_window is null) return;
        var c = SandboxControlState.Snapshot();
        _window.Title = $"Kontrol Sandbox | {(c.IsInputEnabled != 0 ? "ACTIVE" : "PAUSED")} | Action 1 / Action 2 / Action 3";
    }

    private static unsafe float GetAnalog(InputFrame frame, int index) => index is >= 0 and < InputFrame.MaxAnalogInputs ? frame.AnalogValues[index] : 0f;
}

public static class SandboxControlState
{
    private static readonly object Sync = new();
    private static InputFrame _control;
    private static ulong _latchedTriggers;
    private static DateTime _triggerLatchExpiresUtc;

    public static void SetInputFrame(InputFrame control)
    {
        lock (Sync)
        {
            _control = control;
            if (control.TriggeredActions != 0)
            {
                _latchedTriggers |= control.TriggeredActions;
                _triggerLatchExpiresUtc = DateTime.UtcNow.AddMilliseconds(350);
            }
        }
    }

    public static InputFrame Snapshot()
    {
        lock (Sync)
        {
            var snapshot = _control;
            if (DateTime.UtcNow < _triggerLatchExpiresUtc) snapshot.TriggeredActions |= _latchedTriggers;
            else _latchedTriggers = 0;
            return snapshot;
        }
    }
}
