using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace BossMod.Autorotation;

public sealed class UIRotationModule
{
    public static void DescribeModule(Type type, RotationModuleDefinition definition)
    {
        ImGui.TextUnformatted(Loc.Tr(definition.DisplayName));
        ImGui.TextUnformatted(Loc.Tr(definition.Description));
        ImGui.TextUnformatted($"L{definition.MinLevel}-{definition.MaxLevel} {string.Join(" ", GetClasses(definition))}");
        ImGui.TextUnformatted(Loc.Format("Author/contributors: {0}", definition.Author));
        ImGui.TextUnformatted(Loc.Format("Quality: {0}/{1} {2}", (int)definition.Quality, (int)RotationModuleQuality.Count - 1, Loc.Tr(GeneratedEnumMetadata.For(definition.Quality).Attribute<PropertyDisplayAttribute>()?.Label ?? "")));
        using (ImRaii.Disabled())
        {
            ImGui.TextUnformatted(Loc.Format("Class: {0}", type.FullName));
            ImGui.TextUnformatted(Loc.Format("Order group: {0}", definition.Order));
        }
    }

    private static Class[] GetClasses(RotationModuleDefinition definition)
    {
        var classes = definition.Classes.SetBits();
        var len = classes.Length;
        var strings = new Class[len];
        for (var i = 0; i < len; ++i)
        {
            strings[i] = (Class)classes[i];
        }
        return strings;
    }
}
