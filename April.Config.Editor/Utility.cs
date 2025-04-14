using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace April.Config.Editor;

public static partial class Utility
{
    public const int IMGUI_DEFAULT_MAX_LENGTH = 4096;

    public static Vector3 IntToRGB(int color)
    {
        float red = ((color >> 16) & 0xFF) / 255.0f;
        float green = ((color >> 8) & 0xFF) / 255.0f;
        float blue = (color & 0xFF) / 255.0f;

        return new Vector3(red, green, blue);
    }

    public static int RGBToInt(float red, float green, float blue)
    {
        int r = (int)(red * 255);
        int g = (int)(green * 255);
        int b = (int)(blue * 255);

        return (r << 16) | (g << 8) | b;
    }

    public static bool TryParseEmote(string text, [NotNullWhen(true)] out Emote? result)
    {
        result = null;

        if (text.Length >= 4 && text[0] == '<' && (text[1] == ':' || (text[1] == 'a' && text[2] == ':')) && text[text.Length - 1] == '>')
        {
            bool animated = text[1] == 'a';
            int startIndex = animated ? 3 : 2;

            int splitIndex = text.IndexOf(':', startIndex);
            if (splitIndex == -1)
                return false;

            if (!ulong.TryParse(text.Substring(splitIndex + 1, text.Length - splitIndex - 2), NumberStyles.None, CultureInfo.InvariantCulture, out ulong id))
                return false;

            string name = text.Substring(startIndex, splitIndex - startIndex);
            result = new Emote(id, name, animated);
            return true;
        }
        return false;

    }

    public class Emote(ulong id, string name, bool animated)
    {
        public string name = name;
        public ulong id = id;
        public bool animated = animated;
    }

    enum Direction
    {
        None,
        Up,
        Down
    }

    public static void ArrayDisplay<T>(
        List<T> list,
        Action<T, Action<string>> itemRenderFunc,
        Action<T>? onItemRemove = null) where T : class
    {
        T? itemToRemove = null;
        T? itemToMove = null;
        var direction = Direction.None;

        int i = 0;
        foreach (var item in list)
        {
            var i1 = i;
            itemRenderFunc.Invoke(item, (removeButtonText) =>
            {
                if(removeButtonText.StartsWith("##")) ImGui.PushID(removeButtonText[2..]);
                
                if (ImGui.Button(removeButtonText == "" || removeButtonText.StartsWith("##") ? Codicons.Trashcan : $"{Codicons.Trashcan} {removeButtonText}"))
                {
                    itemToRemove = item;
                }

                ImGui.SameLine();

                bool firstItemInList = i1 == 0;
                bool lastInList = i1 == list.Count - 1;

                ImGui.BeginDisabled(firstItemInList);
                if (ImGui.ArrowButton("moveUp", ImGuiDir.Up))
                {
                    direction = Direction.Up;
                    itemToMove = item;
                }
                ImGui.EndDisabled();

                ImGui.SameLine();

                ImGui.BeginDisabled(lastInList);
                if (ImGui.ArrowButton("moveDown", ImGuiDir.Down))
                {
                    direction = Direction.Down;
                    itemToMove = item;
                }
                ImGui.EndDisabled();
                
                if(removeButtonText.StartsWith("##")) ImGui.PopID();
            });

            i++;
        }

        if (itemToRemove != null)
        {
            onItemRemove?.Invoke(itemToRemove);
            list.Remove(itemToRemove);
        }
        else if (itemToMove != null && direction != Direction.None)
        {
            var moveDelta = direction == Direction.Up ? -1 : 1;

            list.Move(itemToMove, list.IndexOf(itemToMove) + moveDelta);
        }
    }

    public static void Move<T>(this List<T> list, int oldIndex, int newIndex)
    {
        var item = list[oldIndex];

        list.RemoveAt(oldIndex);

        if (newIndex > oldIndex) newIndex--;
        // the actual index could have shifted due to the removal

        list.Insert(newIndex, item);
    }

    public static void Move<T>(this List<T> list, T item, int newIndex)
    {
        if (item != null)
        {
            var oldIndex = list.IndexOf(item);
            if (oldIndex > -1)
            {
                list.RemoveAt(oldIndex);

                // if (newIndex > oldIndex) newIndex--;
                // the actual index could have shifted due to the removal

                list.Insert(newIndex, item);
            }
        }

    }

    [GeneratedRegex(@"https?://(?:[a-zA-Z0-9-]+\.)*(?:discord\.com|discordapp\.com)(?:/.*)?")]
    public static partial Regex DiscordRegex();
}