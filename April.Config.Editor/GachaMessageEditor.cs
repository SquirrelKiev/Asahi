using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Newtonsoft.Json;

namespace April.Config.Editor;

internal static class GachaMessageEditor
{
    private static readonly Dictionary<Guid, string> jsons = [];
    private static Exception? lastException = null;

    public static bool MessageEditor(Guid guid, ref GachaMessage gachaMessage)
    {
        bool returnValue = false;

#pragma warning disable CA1854
        if (!jsons.ContainsKey(guid))
        {
            jsons.Add(guid, JsonConvert.SerializeObject(gachaMessage, Formatting.Indented));
        }
#pragma warning restore CA1854

        ImGui.PushID($"message-{guid}");

        ImGui.TextDisabled($"{Codicons.Info} Hover over me!");
        if (ImGui.BeginItemTooltip())
        {
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35);
            ImGui.TextUnformatted("Starting to get a little tired of working on this config thing so doing the lazy solution:\n" +
                                  "Pressing the button below will to take you to Discohook.org. Make the message you want there, and when you're done, " +
                                  "click on the \"JSON Data Editor\" button and paste the result below. Then, press the \"Save message to config\" button." +
                                  "\nNoting that wont save it to the file, but instead the config we're working with here. (Which can then be saved to file). Hope that makes sense.\n" +
                                  "Also also noting that profile, thread, flags and files currently don't change anything.");
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        if (ImGui.Button("Open Discohook"))
        {
            Process.Start(new ProcessStartInfo(
                "https://discohook.org/?data=eyJtZXNzYWdlcyI6W3siZGF0YSI6eyJjb250ZW50IjpudWxsLCJlbWJlZHMiOm51bGwsImF0dGFjaG1lbnRzIjpbXX19XX0") 
                { UseShellExecute = true });
        }


        var json = jsons[guid];
        ImGui.InputTextMultiline("JSON", ref json, Utility.IMGUI_DEFAULT_MAX_LENGTH * 4, new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 4));
        jsons[guid] = json;

        bool save = ImGui.Button("Save message to config");
        ImGui.SetItemTooltip("Press this once you've pasted the JSON in, it saves the deserialized version of it to the copy of the config we're working with. ");

        const string errorPopupId = "JSON Error!";

        try
        {
            if (save)
            {
                var msg = JsonConvert.DeserializeObject<GachaMessage>(json);

                gachaMessage = msg ??
                               throw new NullReferenceException("The JSON failed to deserialize for whatever reason.");

                returnValue = true;

                jsons.Remove(guid);
            }
        }
        catch (Exception exception)
        {
            lastException = exception;
            ImGui.OpenPopup(errorPopupId);
        }

        ImGui.SetNextWindowSize(new Vector2(800,0));
        bool stupidHack = true;
        if (ImGui.BeginPopupModal(errorPopupId, ref stupidHack, ImGuiWindowFlags.NoResize))
        {
            ImGui.TextWrapped($"JSON failed to deserialize! exception below (you'll probably only care about the first line):\n{lastException}");

            if(ImGui.Button("Close")) ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }

        ImGui.PopID();

        return returnValue;
    }

    public static void ClearJsons()
    {
        jsons.Clear();
    }
}