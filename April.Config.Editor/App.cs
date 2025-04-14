using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using Veldrid;
using Veldrid.StartupUtilities;

namespace April.Config.Editor
{
    public class App()
    {
        public void MainLoop(WindowCreateInfo windowInfo)
        {
            var configService = new ConfigService();

            // hacky
            configService.OnConfigLoaded += _ => { GachaMessageEditor.ClearJsons(); };

            VeldridStartup.CreateWindowAndGraphicsDevice(
                windowInfo,
                out var window,
                out var gd);

            var cl = gd.ResourceFactory.CreateCommandList();

            var imguiRenderer = new ImGuiController(
                gd,
                gd.MainSwapchain.Framebuffer.OutputDescription,
                window.Width,
                window.Height);

            window.Resized += () =>
            {
                imguiRenderer.WindowResized(window.Width, window.Height);
                gd.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
            };

            gd.SyncToVerticalBlank = true;

            var collection = new ServiceCollection()
                    .AddSingleton(this)
                    .AddSingleton(gd)
                    .AddSingleton(imguiRenderer)
                    .AddSingleton(configService)
                    .AddSingleton<ImageLoader>()
                    .AddSingleton<MainWindow>()
                ;

            var services = collection.BuildServiceProvider();
            
            var mainWindow = services.GetRequiredService<MainWindow>();

            var stopwatch = Stopwatch.StartNew();
            var previousFrameTicks = 0L;
            var accumulatedDeltaTime = 0.0;
            var framesInInterval = 0;
            var fps = 0.0;

            //const int targetFPS = 300;
            //var targetFrameTicks = Stopwatch.Frequency / targetFPS;

            SetupImGuiStyle();

            var bgColor = new Vector3(0.081f, 0.086f, 0.106f);
            bool showDemoWindow = false;

            while (window.Exists)
            {
                var snapshot = window.PumpEvents();

                var currentTicks = stopwatch.ElapsedTicks;
                var deltaTimeSeconds = (currentTicks - previousFrameTicks) / (double)Stopwatch.Frequency;
                previousFrameTicks = currentTicks;

                // fps tracking for funsies
                accumulatedDeltaTime += deltaTimeSeconds;
                framesInInterval++;

                if (accumulatedDeltaTime >= .05f)
                {
                    fps = framesInInterval / accumulatedDeltaTime;
                    accumulatedDeltaTime = 0.0;
                    framesInInterval = 0;
                }

                imguiRenderer.Update((float)deltaTimeSeconds, snapshot);

                ImGui.PushFont(imguiRenderer.FontPtr);

#if DEBUG
                if (showDemoWindow)
                    ImGui.ShowDemoWindow(ref showDemoWindow);

                ImGui.SetNextWindowPos(new Vector2(ImGui.GetMainViewport().Size.X, 0), ImGuiCond.Always, Vector2.UnitX);
                if (ImGui.Begin($"{Codicons.Coffee} Debug", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
                {
                    ImGui.Text($"FPS: {fps:F1}");
                    ImGui.Text($"DT: {deltaTimeSeconds:F7}");

                    if (ImGui.CollapsingHeader("BG Color"))
                        ImGui.ColorPicker3("##ColorPicker", ref bgColor, ImGuiColorEditFlags.Float);

                    ImGui.Checkbox("Show Demo Window", ref showDemoWindow);

                    ImGui.SeparatorText("Colors");
                    if (ImGui.Button("Cherry-Style"))
                    {
                        SetupImGuiStyle();
                    }
                    if (ImGui.Button("Default Light"))
                    {
                        ImGui.StyleColorsLight();
                    }
                    if (ImGui.Button("Default Dark"))
                    {
                        ImGui.StyleColorsDark();
                    }
                    if (ImGui.Button("Default Classic"))
                    {
                        ImGui.StyleColorsClassic();
                    }

                    ImGui.End();
                }
#endif

                mainWindow.Show();

                ImGui.PopFont();

                cl.Begin();
                cl.SetFramebuffer(gd.MainSwapchain.Framebuffer);
                cl.ClearColorTarget(0, new RgbaFloat(bgColor.X, bgColor.Y, bgColor.Z, 1f));
                imguiRenderer.Render(gd, cl);
                cl.End();
                gd.SubmitCommands(cl);
                gd.SwapBuffers(gd.MainSwapchain);

                //var remainingTicks = targetFrameTicks - (stopwatch.ElapsedTicks - currentTicks);
                //if (remainingTicks > 0)
                //{
                //    var remainingMilliseconds = (int)(remainingTicks * 1000 / Stopwatch.Frequency);
                //    Thread.Sleep(remainingMilliseconds);
                //}
            }

            imguiRenderer.Dispose();
        }

        private IntPtr fontArrayPtr;
        private ImFontPtr fontPtr;

        public void SetupImGuiStyle()
        {
            // Soft Cherry style Patitotective from ImThemes
            var style = ImGui.GetStyle();

            style.Alpha = 1.0f;
            style.DisabledAlpha = 0.4000000059604645f;
            style.WindowPadding = new Vector2(10.0f, 10.0f);
            style.WindowRounding = 4.0f;
            style.WindowBorderSize = 1.0f;
            style.WindowMinSize = new Vector2(50.0f, 50.0f);
            style.WindowTitleAlign = new Vector2(0.5f, 0.5f);
            style.WindowMenuButtonPosition = ImGuiDir.Left;
            style.ChildRounding = 0.0f;
            style.ChildBorderSize = 1.0f;
            style.PopupRounding = 1.0f;
            style.PopupBorderSize = 1.0f;
            style.FramePadding = new Vector2(5.0f, 3.0f);
            style.FrameRounding = 3.0f;
            style.FrameBorderSize = 0.0f;
            style.ItemSpacing = new Vector2(6.0f, 6.0f);
            style.ItemInnerSpacing = new Vector2(3.0f, 2.0f);
            style.CellPadding = new Vector2(3.0f, 3.0f);
            style.IndentSpacing = 6.0f;
            style.ColumnsMinSpacing = 6.0f;
            style.ScrollbarSize = 13.0f;
            style.ScrollbarRounding = 16.0f;
            style.GrabMinSize = 20.0f;
            style.GrabRounding = 4.0f;
            style.TabRounding = 4.0f;
            style.TabBorderSize = 1.0f;
            style.TabMinWidthForCloseButton = 0.0f;
            style.ColorButtonPosition = ImGuiDir.Right;
            style.ButtonTextAlign = new Vector2(0.5f, 0.5f);
            style.SelectableTextAlign = new Vector2(0.0f, 0.0f);

            style.Colors[(int)ImGuiCol.Text] = new Vector4(0.8588235378265381f, 0.929411768913269f, 0.886274516582489f, 1.0f);
            style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.5215686559677124f, 0.5490196347236633f, 0.5333333611488342f, 1.0f);
            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.1294117718935013f, 0.1372549086809158f, 0.168627455830574f, 1.0f);
            style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.1490196138620377f, 0.1568627506494522f, 0.1882352977991104f, 1.0f);
            style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.2000000029802322f, 0.2196078449487686f, 0.2666666805744171f, 1.0f);
            style.Colors[(int)ImGuiCol.Border] = new Vector4(0.1372549086809158f, 0.1137254908680916f, 0.1333333402872086f, 1.0f);
            style.Colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.168627455830574f, 0.1843137294054031f, 0.2313725501298904f, 1.0f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.4549019634723663f, 0.196078434586525f, 0.2980392277240753f, 1.0f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.4549019634723663f, 0.196078434586525f, 0.2980392277240753f, 1.0f);
            style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.2313725501298904f, 0.2000000029802322f, 0.2705882489681244f, 1.0f);
            style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.501960813999176f, 0.07450980693101883f, 0.2549019753932953f, 1.0f);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.2000000029802322f, 0.2196078449487686f, 0.2666666805744171f, 1.0f);
            style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.2000000029802322f, 0.2196078449487686f, 0.2666666805744171f, 1.0f);
            style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.239215686917305f, 0.239215686917305f, 0.2196078449487686f, 1.0f);
            style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.3882353007793427f, 0.3882353007793427f, 0.3725490272045135f, 1.0f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.6941176652908325f, 0.6941176652908325f, 0.686274528503418f, 1.0f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.6941176652908325f, 0.6941176652908325f, 0.686274528503418f, 1.0f);
            style.Colors[(int)ImGuiCol.CheckMark] = new Vector4(0.658823549747467f, 0.1372549086809158f, 0.1764705926179886f, 1.0f);
            style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.6509804129600525f, 0.1490196138620377f, 0.3450980484485626f, 1.0f);
            style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.7098039388656616f, 0.2196078449487686f, 0.2666666805744171f, 1.0f);
            style.Colors[(int)ImGuiCol.Button] = new Vector4(0.6509804129600525f, 0.1490196138620377f, 0.3450980484485626f, 1.0f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.4549019634723663f, 0.196078434586525f, 0.2980392277240753f, 1.0f);
            style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.3764705882352941f, 0.11764705882352941f, 0.2196078431372549f, 1.0f);
            style.Colors[(int)ImGuiCol.Header] = new Vector4(0.4549019634723663f, 0.196078434586525f, 0.2980392277240753f, 1.0f);
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.6509804129600525f, 0.1490196138620377f, 0.3450980484485626f, 1.0f);
            style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.501960813999176f, 0.07450980693101883f, 0.2549019753932953f, 1.0f);
            style.Colors[(int)ImGuiCol.Separator] = new Vector4(0.4274509847164154f, 0.4274509847164154f, 0.4980392158031464f, 1.0f);
            style.Colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.09803921729326248f, 0.4000000059604645f, 0.7490196228027344f, 1.0f);
            style.Colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.09803921729326248f, 0.4000000059604645f, 0.7490196228027344f, 1.0f);
            style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.6509804129600525f, 0.1490196138620377f, 0.3450980484485626f, 1.0f);
            style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.4549019634723663f, 0.196078434586525f, 0.2980392277240753f, 1.0f);
            style.Colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.4549019634723663f, 0.196078434586525f, 0.2980392277240753f, 1.0f);
            style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.1764705926179886f, 0.3490196168422699f, 0.5764706134796143f, 1.0f);
            style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.2588235437870026f, 0.5882353186607361f, 0.9764705896377563f, 1.0f);
            style.Colors[(int)ImGuiCol.TabActive] = new Vector4(0.196078434586525f, 0.407843142747879f, 0.6784313917160034f, 1.0f);
            style.Colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.06666667014360428f, 0.1019607856869698f, 0.1450980454683304f, 1.0f);
            style.Colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.1333333402872086f, 0.2588235437870026f, 0.4235294163227081f, 1.0f);
            style.Colors[(int)ImGuiCol.PlotLines] = new Vector4(0.8588235378265381f, 0.929411768913269f, 0.886274516582489f, 1.0f);
            style.Colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.4549019634723663f, 0.196078434586525f, 0.2980392277240753f, 1.0f);
            style.Colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.3098039329051971f, 0.7764706015586853f, 0.196078434586525f, 1.0f);
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(0.4549019634723663f, 0.196078434586525f, 0.2980392277240753f, 1.0f);
            style.Colors[(int)ImGuiCol.TableHeaderBg] = new Vector4(0.1882352977991104f, 0.1882352977991104f, 0.2000000029802322f, 1.0f);
            style.Colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.3098039329051971f, 0.3098039329051971f, 0.3490196168422699f, 1.0f);
            style.Colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.2274509817361832f, 0.2274509817361832f, 0.2470588237047195f, 1.0f);
            style.Colors[(int)ImGuiCol.TableRowBg] = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
            style.Colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.3843137323856354f, 0.6274510025978088f, 0.9176470637321472f, 1.0f);
            style.Colors[(int)ImGuiCol.DragDropTarget] = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            style.Colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.2588235437870026f, 0.5882353186607361f, 0.9764705896377563f, 1.0f);
            style.Colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            style.Colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.800000011920929f, 0.800000011920929f, 0.800000011920929f, 1.0f);
            style.Colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.800000011920929f, 0.800000011920929f, 0.800000011920929f, 0.300000011920929f);
        }
    }
}
