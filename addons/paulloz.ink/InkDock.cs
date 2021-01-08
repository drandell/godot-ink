// #if TOOLS
using Godot;
using System;

[Tool]
public class InkDock : Control
{
    private OptionButton fileSelect;
    private FileDialog fileDialog;

    private String currentFilePath;

    private Node storyNode;
    private VBoxContainer storyText;
    private VBoxContainer storyChoices;
    
    private ScrollBar scrollbar;

    public override void _Ready()
    {
        fileSelect = GetNode<OptionButton>("Container/Top/OptionButton");
        fileDialog = GetNode<FileDialog>("FileDialog");
        fileSelect.Connect("item_selected", this, nameof(OnFileSelectItemSelected));
        fileDialog.Connect("file_selected", this, nameof(OnFileDialogFileSelected));
        fileDialog.Connect("popup_hide", this, nameof(OnFileDialogHide));

        storyNode = GetNode("Story");
        storyNode.SetScript(ResourceLoader.Load("res://addons/paulloz.ink/InkStory.cs") as Script);
        storyNode.Connect(nameof(InkStory.InkChoices), this, nameof(OnStoryChoices));
        storyText = GetNode<VBoxContainer>("Container/Bottom/Scroll/Margin/StoryText");
        storyChoices = GetNode<VBoxContainer>("Container/Bottom/StoryChoices");

        scrollbar = GetNode<ScrollContainer>("Container/Bottom/Scroll").GetVScrollbar();
    }

    private void ResetFileSelectItems()
    {
        while (fileSelect.GetItemCount() > 2)
            fileSelect.RemoveItem(fileSelect.GetItemCount() - 1);
    }

    private void ResetStoryContent()
    {
        RemoveAllStoryContent();
        RemoveAllChoices();
    }

    private void RemoveAllStoryContent()
    {
        foreach (Node n in storyText.GetChildren())
            storyText.RemoveChild(n);
    }

    private void RemoveAllChoices()
    {
        foreach (Node n in storyChoices.GetChildren())
            storyChoices.RemoveChild(n);
    }

    private void OnFileSelectItemSelected(int id)
    {
        if (id == 0)
        {
            ResetFileSelectItems();
            ResetStoryContent();
            currentFilePath = "";
        }
        else if (id == 1)
        {
            fileSelect.Select(0);
            fileDialog.PopupCentered();
        }
    }

    private void OnFileDialogFileSelected(string path)
    {
        if (path.EndsWith(".json") || path.EndsWith(".ink"))
        {
            ResetFileSelectItems();
            fileSelect.AddItem(path.Substring(path.FindLast("/") + 1));
            currentFilePath = path;
        }
    }

    private void OnFileDialogHide()
    {
        if (string.IsNullOrEmpty(currentFilePath))
        {
            fileSelect.Select(0);
        }
        else
        {
            fileSelect.Select(2);
            storyNode.Set("InkFile", ResourceLoader.Load(currentFilePath));
            storyNode.Call("LoadStory");
            ResetStoryContent();
            ContinueStoryMaximally();
        }
    }

    private async void ContinueStoryMaximally()
    {
        bool canContinue = (bool)storyNode.Get("CanContinue");

        while (canContinue)
        {
            try
            {
                storyNode.Call("Continue");
                OnStoryContinued(storyNode.Get("CurrentText") as string, new string[] { });
            }
            catch (Ink.Runtime.StoryException e)
            {
                OnStoryContinued(e.ToString(), new string[] { });
            }
        }
        await ToSignal(GetTree(), "idle_frame");
        scrollbar.Value = scrollbar.MaxValue;
    }

    private void OnStoryContinued(string text, string[] tags)
    {
        Label newLine = new Label
        {
            Autowrap = true,
            Text = text.Trim(new char[] { ' ', '\n' })
        };
        storyText.AddChild(newLine);
    }

    private void OnStoryChoices(string[] choices)
    {
        int i = 0;
        foreach (string choice in choices)
        {
            Button button = new Button
            {
                Text = choice
            };
            button.Connect("pressed", this, nameof(ClickChoice), new Godot.Collections.Array() { i });
            storyChoices.AddChild(button);
            ++i;
        }
    }

    private void ClickChoice(int idx)
    {
        storyNode.Callv("ChooseChoiceIndex", new Godot.Collections.Array() { idx });
        RemoveAllChoices();
        storyText.AddChild(new HSeparator());
        ContinueStoryMaximally();
    }
}
// #endif