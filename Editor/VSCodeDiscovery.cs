﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.CodeEditor;

public interface IDiscovery
{
  CodeEditor.Installation[] PathCallback();
}

public class VSCodeDiscovery : IDiscovery
{
  List<CodeEditor.Installation> m_Installations;

  public CodeEditor.Installation[] PathCallback()
  {
    if (m_Installations == null)
    {
      m_Installations = new List<CodeEditor.Installation>();
      FindInstallationPaths();
    }

    return m_Installations.ToArray();
  }

  void FindInstallationPaths()
  {
    string[] possiblePaths =
#if UNITY_EDITOR_OSX
            {
				"/opt/homebrew/bin/nvim",
                // "/Applications/Visual Studio Code.app",
                // "/Applications/Visual Studio Code - Insiders.app"
            };
#elif UNITY_EDITOR_WIN
            {
                GetProgramFiles() + @"/Microsoft VS Code/bin/code.cmd",
                GetProgramFiles() + @"/Microsoft VS Code/Code.exe",
                GetProgramFiles() + @"/Microsoft VS Code Insiders/bin/code-insiders.cmd",
                GetProgramFiles() + @"/Microsoft VS Code Insiders/Code.exe",
                GetLocalAppData() + @"/Programs/Microsoft VS Code/bin/code.cmd",
                GetLocalAppData() + @"/Programs/Microsoft VS Code/Code.exe",
                GetLocalAppData() + @"/Programs/Microsoft VS Code Insiders/bin/code-insiders.cmd",
                GetLocalAppData() + @"/Programs/Microsoft VS Code Insiders/Code.exe",
            };
#else
            {
              "/usr/bin/nvim",
                // "/usr/bin/code",
                // "/bin/code",
                // "/usr/local/bin/code",
                // "/var/lib/flatpak/exports/bin/com.visualstudio.code",
                // "/snap/current/bin/code",
                // "/snap/bin/code"
            };
#endif
    var existingPaths = possiblePaths.Where(VSCodeExists).ToList();
    if (!existingPaths.Any())
    {
      return;
    }

    var lcp = GetLongestCommonPrefix(existingPaths);
    m_Installations = existingPaths.Select(path => new CodeEditor.Installation
    {
      Name = $"Neovim ({path.Substring(lcp.Length)})",
      Path = path
    }).ToList();
  }

#if UNITY_EDITOR_WIN
        static string GetProgramFiles()
        {
            return Environment.GetEnvironmentVariable("ProgramFiles")?.Replace("\\", "/");
        }

        static string GetLocalAppData()
        {
            return Environment.GetEnvironmentVariable("LOCALAPPDATA")?.Replace("\\", "/");
        }
#endif

  static string GetLongestCommonPrefix(List<string> paths)
  {
    var baseLength = paths.First().Length;
    for (var pathIndex = 1; pathIndex < paths.Count; pathIndex++)
    {
      baseLength = Math.Min(baseLength, paths[pathIndex].Length);
      for (var i = 0; i < baseLength; i++)
      {
        if (paths[pathIndex][i] == paths[0][i]) continue;

        baseLength = i;
        break;
      }
    }

    return paths[0].Substring(0, baseLength);
  }

  static bool VSCodeExists(string path)
  {
    return new FileInfo(path).Exists;
  }
}
