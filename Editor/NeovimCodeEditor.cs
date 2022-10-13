using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public class NeovimCodeEditor : IExternalCodeEditor
{
    const string nvim_argument = "nvim_arguments";
    const string nvim_extension = "nvim_userExtensions";

    // private const string PackageName = "com.cloudedmountain.ide.neovim";
    // private const string NeovimLauncher = "com.cloudedmountain.ide.neovim.sh";
    static readonly string[] _supportedFileNames = { "nvim" };
    // static string DefaultArgument { get; } = "\"$(ProjectPath)\" -g \"$(File)\":$(Line):$(Column)";
    // static string DefaultArgument { get; } = "--server /tmp/nvimsocket --remote $(filePath)";
    static string DefaultApp => EditorPrefs.GetString("kScriptsDefaultApp");

    IDiscovery _discoverability;
    IGenerator _projectGeneration;
    string _arguments;

    static NeovimCodeEditor()
    {
        var editor = new NeovimCodeEditor(new VSCodeDiscovery(), new ProjectGeneration(Directory.GetParent(Application.dataPath).FullName));

        CodeEditor.Register(editor);
        editor.CreateIfDoesntExist();
    }

    static bool SupportsExtension(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
            return false;
        return HandledExtensions.Contains(extension.TrimStart('.'));
    }

    static string[] HandledExtensions
    {
        get
        {
            return HandledExtensionsString
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.TrimStart('.', '*'))
                .ToArray();
        }
    }

    static string HandledExtensionsString
    {
        get => EditorPrefs.GetString(nvim_extension, string.Join(";", defaultExtensions));
        set => EditorPrefs.SetString(nvim_extension, value);
    }

    static string[] defaultExtensions
    {
        get
        {
            var customExtensions = new[] { "json", "asmdef", "log" };
            return EditorSettings.projectGenerationBuiltinExtensions
                .Concat(EditorSettings.projectGenerationUserExtensions)
                .Concat(customExtensions)
                .Distinct().ToArray();
        }
    }

    // private static CodeEditor.Installation[] installations;
    // private static string launcherPath;

    // Provide the editor with a list of known and supported editors that this instance supports. This gets called multiple times through execution and is not expected to be persistent. For example, after calling TryGetInstallationForPath this class may realize that you have added a custom code editor.
    public CodeEditor.Installation[] Installations => _discoverability.PathCallback();


    public NeovimCodeEditor(IDiscovery discovery, IGenerator projectGeneration)
    {
        _discoverability = discovery;
        _projectGeneration = projectGeneration;
    }

    // Callback to the IExternalCodeEditor when it has been chosen from the PreferenceWindow.
    public void Initialize(string editorInstallationPath) { }

    // The external code editor needs to handle the request to open a file.
    public bool OpenProject(string filePath = "", int line = -1, int column = -1)
    {
        if (filePath != "" && (!SupportsExtension(filePath) || !File.Exists(filePath))) // Assets - Open C# Project passes empty path here
        {
            return false;
        }

        if (line == -1)
            line = 1;
        if (column == -1)
            column = 0;

        var arguments = $"--servername ~/.cache/nvimsocket -c 'call cursor({line}, {column})' {filePath}";
        Debug.Log(arguments);

        ExecuteBashCommand("nvr " + arguments);

        // Focus Kitty:
        var kittyProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"/Applications/kitty.app/",
                UseShellExecute = true,
            }
        };
        kittyProcess.Start();

        return true;
    }

    static string ExecuteBashCommand(string command)
    {
        // according to: https://stackoverflow.com/a/15262019/637142
        // thans to this we will pass everything as one command
        command = command.Replace("\"", "\"\"");
        Debug.Log(command);

        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"" + command + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        proc.Start();
        proc.WaitForExit();

        return proc.StandardOutput.ReadToEnd();
    }
    // Unity calls this function during initialization in order to sync the Project. This is different from SyncIfNeeded in that it does not get a list of changes.
    public void SyncAll()
    {
        (_projectGeneration.AssemblyNameProvider as IPackageInfoCache)?.ResetPackageInfoCache();
        AssetDatabase.Refresh();
        _projectGeneration.Sync();
    }

    // When you change Assets in Unity, this method for the current chosen instance of IExternalCodeEditor parses the new and changed Assets.
    public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
    {
        (_projectGeneration.AssemblyNameProvider as IPackageInfoCache)?.ResetPackageInfoCache();
        _projectGeneration.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToList(), importedFiles);
    }

    // Unity stores the path of the chosen editor. An instance of IExternalCodeEditor can take responsibility for this path, by returning true when this method is being called. The out variable installation need to be constructed with the path and the name that should be shown in the "External Tools" code editor list.
    public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
    {
        var lowerCasePath = editorPath.ToLower();
        var filename = Path.GetFileName(lowerCasePath).Replace(" ", "");
        var installations = Installations;

        if (!_supportedFileNames.Contains(filename))
        {
            installation = default;
            return false;
        }

        if (!installations.Any())
        {
            installation = new CodeEditor.Installation
            {
                Name = "Neovim",
                Path = editorPath
            };
        }
        else
        {
            try
            {
                installation = installations.First(inst => inst.Path == editorPath);
            }
            catch (InvalidOperationException)
            {
                installation = new CodeEditor.Installation
                {
                    Name = "Neovim",
                    Path = editorPath
                };
            }
        }

        return true;
    }

    public void CreateIfDoesntExist()
    {
        if (!_projectGeneration.SolutionExists())
        {
            _projectGeneration.Sync();
        }
    }

    public void OnGUI()
    {
    }
}
