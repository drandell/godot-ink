using Godot;
using System;
using System.Collections.Generic;

#if TOOLS
[Tool]
#endif
public class InkStory : Node
{
    // All the signals we'll need
    [Signal] public delegate void InkContinued(string text, string[] tags);
    [Signal] public delegate void InkEnded();
    [Signal] public delegate void InkChoices(string[] choices);
    public delegate void InkVariableChanged(string variableName, object variableValue);

    private string ObservedVariableSignalName(string name)
    {
        return $"{nameof(InkVariableChanged)}-{name}";
    }

    // All the exported variables
    [Export] public bool AutoLoadStory = false;
    [Export] public Resource InkFile = null;

    // All the public variables
    public string CurrentText { get { return story?.currentText ?? ""; } }
    public string[] CurrentTags { get { return story?.currentTags.ToArray() ?? new string[0]; } }
    public string[] CurrentChoices => story?.currentChoices.ConvertAll<string>(choice => choice.text).ToArray() ?? new string[0];

    // All the properties
    public bool CanContinue { get { return story?.canContinue ?? false; } }
    public bool HasChoices { get { return story?.currentChoices.Count > 0; } }
    public string[] GlobalTags { get { return story?.globalTags.ToArray() ?? new string[0]; } }

    private Ink.Runtime.Story story = null;
    private List<string> observedVariables = new List<string>();
    private Ink.Runtime.Story.VariableObserver observer;

    private void Reset()
    {
        if (story == null)
            return;

        foreach (string varName in observedVariables)
            RemoveVariableObserver(varName, false);
        observedVariables.Clear();

        story =  null;
    }

    public override void _Ready()
    {
        observer = (string varName, object varValue) => {
            if (observedVariables.Contains(varName))
                EmitSignal(ObservedVariableSignalName(varName), varName, MarshallVariableValue(varValue));
        };

        if (AutoLoadStory)
            LoadStory();
    }

    public bool LoadStory()
    {
        Reset();

        if (!IsJSONFileValid())
        {
            GD.PrintErr("The story you're trying to load is not valid.");
            return false;
        }

        story = new Ink.Runtime.Story(InkFile.GetMeta("content") as string);
        return true;
    }

    public bool LoadStoryFromString(string story)
    {
        InkFile = new Resource();
        InkFile.SetMeta("content", story);
        return LoadStory();
    }

    public bool LoadStoryAndSetState(string state)
    {
        if (!LoadStory())
            return false;
        SetState(state);
        return true;
    }

    public string Continue()
    {
        string text = null;

        // Continue if we can
        if (CanContinue)
        {
            story.Continue();
            text = CurrentText;

            EmitSignal(nameof(InkContinued), new object[] { CurrentText, CurrentTags });
            if (HasChoices) // Check if we have choices after continuing
                EmitSignal(nameof(InkChoices), new object[] { CurrentChoices });
        }
        else if (!HasChoices) // If we can't continue and don't have any choice, we're at the end
        {
            EmitSignal(nameof(InkEnded));
        }

        return text;
    }

    public void ChooseChoiceIndex(int index)
    {
        if (index >= 0 && index < story?.currentChoices.Count)
        {
            story.ChooseChoiceIndex(index);
            Continue();
        }
    }

    public bool ChoosePathString(string pathString)
    {
        try
        {
            if (story != null)
                story.ChoosePathString(pathString);
            else
                return false;
        }
        catch (Ink.Runtime.StoryException e)
        {
            GD.PrintErr(e.ToString());
            return false;
        }

        return true;
    }

    public int VisitCountAtPathString(string pathString)
    {
        return story?.state.VisitCountAtPathString(pathString) ?? 0;
    }

    public string[] TagsForContentAtPath(string pathString)
    {
        return story?.TagsForContentAtPath(pathString).ToArray() ?? new string[0];
    }

    public object GetVariable(string name)
    {
        return MarshallVariableValue(story?.variablesState[name]);
    }

    public void SetVariable(string name, object value_)
    {
        if (story != null)
            story.variablesState[name] = value_;
    }

    public string ObserveVariable(string name)
    {
        if (story != null)
        {
            string signalName = ObservedVariableSignalName(name);

            if (!observedVariables.Contains(name))
            {
                if (!HasUserSignal(signalName))
                    AddUserSignal(signalName);

                observedVariables.Add(name);
                story.ObserveVariable(name, observer);
            }

            return signalName;
        }

        return null;
    }

    public void RemoveVariableObserver(string name)
    {
        RemoveVariableObserver(name, true);
    }

    private void RemoveVariableObserver(string name, bool clear)
    {
        if (story != null)
        {
            if (observedVariables.Contains(name))
            {
                string signalName = ObservedVariableSignalName(name);
                if (HasUserSignal(signalName))
                {
                    Godot.Collections.Array connections = GetSignalConnectionList(signalName);
                    foreach (Godot.Collections.Dictionary connection in connections)
                        Disconnect(signalName, connection["target"] as Godot.Object, connection["method"] as string);
                    // Seems like there's no way to undo `AddUserSignal` so we're just going to unbind everything :/
                }

                story.RemoveVariableObserver(null, name);

                if (clear)
                    observedVariables.Remove(name);
            }
        }
    }

    public void BindExternalFunction(string inkFuncName, Func<object> func)
    {
        story?.BindExternalFunction(inkFuncName, func);
    }

    public void BindExternalFunction<T>(string inkFuncName, Func<T, object> func)
    {
        story?.BindExternalFunction(inkFuncName, func);
    }

    public void BindExternalFunction<T1, T2>(string inkFuncName, Func<T1, T2, object> func)
    {
        story?.BindExternalFunction(inkFuncName, func);
    }

    public void BindExternalFunction<T1, T2, T3>(string inkFuncName, Func<T1, T2, T3, object> func)
    {
        story?.BindExternalFunction(inkFuncName, func);
    }

    public void BindExternalFunction(string inkFuncName, Node node, string funcName)
    {
        story?.BindExternalFunctionGeneral(inkFuncName, (object[] foo) => node.Call(funcName, foo));
    }

    private object MarshallVariableValue(object value_)
    {
        if (value_ != null && value_.GetType() == typeof(Ink.Runtime.InkList))
            value_ = null;
        return value_;
    }

    public object EvaluateFunction(string functionName, bool returnTextOutput, params object [] arguments)
    {
        if (returnTextOutput)
        {
            string textOutput = null;
            object returnValue = story?.EvaluateFunction(functionName, out textOutput, arguments);
            return new object[] { returnValue, textOutput };
        }
        return story?.EvaluateFunction(functionName, arguments);
    }

    public string GetState()
    {
        return story.state.ToJson();
    }

    public void SaveStateOnDisk(string path)
    {
        if (!path.StartsWith("res://") && !path.StartsWith("user://"))
            path = $"user://{path}";
        File file = new File();
        file.Open(path, File.ModeFlags.Write);
        SaveStateOnDisk(file);
        file.Close();
    }

    public void SaveStateOnDisk(File file)
    {
        if (file.IsOpen())
            file.StoreString(GetState());
    }

    public void SetState(string state)
    {
        story.state.LoadJson(state);
    }

    public void LoadStateFromDisk(string path)
    {
        if (!path.StartsWith("res://") && !path.StartsWith("user://"))
            path = $"user://{path}";
        File file = new File();
        file.Open(path, File.ModeFlags.Read);
        LoadStateFromDisk(file);
        file.Close();
    }

    public void LoadStateFromDisk(File file)
    {
        if (file.IsOpen())
        {
            file.Seek(0);
            if (file.GetLen() > 0)
                story.state.LoadJson(file.GetAsText());
        }
    }

    private bool IsJSONFileValid()
    {
        return InkFile?.HasMeta("content") == true;
    }
}
