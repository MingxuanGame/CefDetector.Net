using System.Runtime.InteropServices;
using CefDetector.Net;
using System.CommandLine;

class Program
{
    static Dictionary<string, CefType> cefList = new();

    static string findCmd = "";
    static string findArg = "";
    static string path = "";
    static string _disk = "";// for Windows
    static void SetPath(DirectoryInfo? dir)
    {
        if (dir is null || !dir.Exists)
        {
            return;
        }
        string name = dir.FullName;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            try
            {
                DriveInfo root = new DriveInfo(dir.Root.FullName);
                if (IsLocalDisk(root.DriveType))
                    _disk = name.TrimEnd('\\');
                else
                    return;
            }
            catch (System.ArgumentException)
            {
                // is not local disk, e.g. Network Path
                return;
            }
        }
        path = name;
    }

    static bool IsLocalDisk(DriveType type)
    {
        return type != DriveType.CDRom &&
            type != DriveType.NoRootDirectory &&
            type != DriveType.Network &&
            type != DriveType.Unknown;
    }

    public static void Main(string[] args)
    {
        // params
        var pathOption = new Option<DirectoryInfo?>(
                    name: "--dir",
                    description: "The path to search."
                    );
        var rootCommand = new RootCommand("Check how many CEFs are on your Windows/Linux/MacOS.");
        rootCommand.AddOption(pathOption);
        rootCommand.SetHandler((file) =>
            {
                SetPath(file);
            },
            pathOption);
        rootCommand.Invoke(args);

        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                findCmd = "cmd";
                findArg = "/c ";
                if (string.IsNullOrEmpty(path))
                {
                    var diskList = DriveInfo.GetDrives()
                                            .Where(i => IsLocalDisk(i.DriveType))
                                            .Select(i => i.RootDirectory.FullName.TrimEnd('\\'))
                                            .ToList();

                    List<string> argList = new();
                    foreach (var disk in diskList)
                    {
                        argList.Add($" {disk} && cd / && dir /B /S *_percent.pak");
                    }

                    findArg += string.Join('&', argList);
                }
                else
                {
                    findArg += $" {_disk} && cd {path} && dir /B /S *_percent.pak";
                }
                break;
            case PlatformID.Unix:
            case PlatformID.MacOSX:
            case PlatformID.Other:
                if (Environment.OSVersion.Platform == PlatformID.Other)
                {
                    Console.WriteLine("[初始化]\t操作系统类型未知，尝试使用Unix系命令进行搜索……");

                }
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = "/";
                }
                findCmd = "find";
                findArg = $" {path} -name *_percent.pak";
                break;
        }

        Console.WriteLine("[初始化]\t正在计算chrome内核个数，请等待，下面是列表：");
        using var executor = new ProcessExecutor(findCmd, findArg);
        executor.OnOutputDataReceived += (sender,
                                           str) =>
                                         {
                                             if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                                                  //Windows下不搜索回收站和OneDrive
                                                  (str.Contains("$RECYCLE.BIN") || (str.Contains("OneDrive"))) &&
                                                  !(str.Contains("找不到文件"))
                                                )
                                             {
                                                 return;
                                             }

                                             var result = CefClassifier.SearchDir(str);
                                             foreach (var (file, type) in result)
                                             {
                                                 if (!cefList.ContainsKey(file))
                                                 {
                                                     cefList.Add(file, type);
                                                     Console.WriteLine("[" + type + "]\t" + file);
                                                 }
                                             }
                                         };
        executor.OnErrorDataReceived += (sender,
                                          str) =>
                                        {
                                            //Console.WriteLine( "ERR：" + str );
                                        };
        executor.Execute();
        Console.WriteLine($"[喜报]\t您系统里总共有{cefList.Count}个chrome内核！（可能有重复计算）");

    }
}

