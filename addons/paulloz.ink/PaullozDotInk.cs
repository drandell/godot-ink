#if TOOLS
using Godot;
using Godot.Collections;

[Tool]
public class PaullozDotInk : EditorPlugin
{
    private readonly Dictionary settings = new Dictionary() {
        {"inklecate_path", new Dictionary() {
            { "type", Variant.Type.String },
            { "hint", PropertyHint.GlobalFile },
            { "hint_string", "*.exe" },
            { "default", "" }
        }}
    };
    private const string addonBasePath = "res://addons/paulloz.ink";

    private NodePath customTypeScriptPath = $"{addonBasePath}/InkStory.cs";
    private NodePath customTypeIconPath = $"{addonBasePath}/icon.svg";

    private NodePath dockScene = $"{addonBasePath}/InkDock.tscn";
    private Control dock;

    private NodePath importPluginScriptPath = $"{addonBasePath}/import_ink.gd";
    private EditorImportPlugin importPlugin;

    public override void _EnterTree()
    {
        // Settings
        foreach (string key in settings.Keys)
        {
            string property_name = $"ink/{key}";
            if (!ProjectSettings.HasSetting(property_name))
            {
                Dictionary setting = settings[key] as Dictionary;
                ProjectSettings.SetSetting(property_name, setting["default"]);
                ProjectSettings.AddPropertyInfo(new Dictionary() {
                    { "name", property_name },
                    { "type", setting["type"] },
                    { "hint", setting["hint"] },
                    { "hint_string", setting["hint_string"] }
                });
            }
        }
        ProjectSettings.Save();

        // Resources
        importPlugin = GD.Load<GDScript>(importPluginScriptPath).New() as EditorImportPlugin;
        AddImportPlugin(importPlugin);

        // Custom types
        AddCustomType("Ink Story", "Node", GD.Load<Script>(customTypeScriptPath), GD.Load<Texture>(customTypeIconPath));

        // Editor
        dock = GD.Load<PackedScene>(dockScene).Instance() as Control;
        AddControlToBottomPanel(dock, "Ink");
    }

    public override void _ExitTree()
    {
        // Editor
        RemoveControlFromBottomPanel(dock);
        dock.Free();

        // Custom types
        RemoveCustomType("Ink Story");

        // Resources
        RemoveImportPlugin(importPlugin);

        // Settings
        foreach (string key in settings.Keys)
        {
            string property_name = $"ink/{key}";
            if (ProjectSettings.HasSetting(property_name))
                ProjectSettings.SetSetting(property_name, null);
        }
    }
}
#endif
