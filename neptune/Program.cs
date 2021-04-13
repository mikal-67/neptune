using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;

namespace neptune
{
    class Program
    {
        static void Main(string[] args)
        {
            var doc = new XmlDocument();
            doc.LoadXml(File.ReadAllText("config.xml"));

            if (args.Length > 2)
            {
                if (args[0] == "set")
                {
                    doc[args[1]].InnerText = args[2];
                }
            }
            else if (args.Length > 1)
            {
                if (args[0] == "get")
                {
                    try
                    {
                        var client = new HttpClient();
                        
                        Console.WriteLine("Getting the package index...");
                        var index = client.GetStringAsync("http://10.0.0.57/packindex.txt").Result.Split('\n');

                        Console.WriteLine("Done.\nLooking for your package...");
                        string url = "";
                        string insttype = "";
                        foreach (var item in index)
                        {
                            var line = item.Split(';');
                            if (line[0] == args[1])
                            {
                                url = line[1];
                                insttype = line[3];
                                
                            }
                        }
                        
                        if (url == "")
                        {
                            Console.WriteLine($"Package {args[1]} was not found.");
                        }
                        else
                        {
                            Console.WriteLine("Downloading package " + args[1] + " from " + url);
                        }
                        
                        var installer = client.GetAsync(url).Result.Content.ReadAsByteArrayAsync().Result;

                        File.WriteAllBytes(Directory.GetCurrentDirectory() + "\\installer.exe", installer);
                        string @params = "";
                        string il = doc["instlocation"].InnerText;
                        switch (insttype)
                        {
                            case "inno":
                                @params = $"/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=737 /LOG=logs\\installer.log /DIR=\"{il}\"";
                                break;
                        }
                        Console.WriteLine("Running it...");
                        var p = Process.Start("installer.exe", @params);
                        p.WaitForExit();
                        Console.WriteLine("Cleaning up...");
                        if (p.ExitCode == 737) Console.WriteLine("A restart is required.");
                        //File.Delete("installer.exe");
                    }
                    catch (HttpRequestException)
                    {

                    }
                    catch (IOException)
                    {

                    }
                    catch (InvalidDataException)
                    {
                        Console.WriteLine("This pack could not be downloaded because of an issue with the server's MIME map. Contact this website's webmaster and ask them to fix this.");
                    }
                    /*catch (InvalidOperationException)
                    {
                        Console.WriteLine("URL " + args[1] + " is not valid.");
                    }*/
                }
                else if (args[0] == "init")
                {
                    try
                    {
                        if (Directory.Exists(args[1])) Directory.Delete(args[1]);
                        Directory.CreateDirectory(args[1]);
                        Console.Write("Command OK");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        ChangeColors(ConsoleColor.Red);
                        Console.WriteLine("Command's syntax was OK but an IO error occurred: You do not have admin access in a directory that requires it.");
                        return;
                    }
                    catch (IOException)
                    {
                        return;
                    }

                    Directory.CreateDirectory(args[1] + @"\src");
                    var file = File.Create(args[1] + @"\README.md");
                    file.Write(Encoding.UTF8.GetBytes("# " + args[1]), 0, 2 + args[1].Length);
                    file.Flush();
                    file.Close();
                }
                else if (args[0] == "compress")
                {
                    try
                    {
                        string file = args[1] + "\\";
                        if (!Validate(file))
                        {
                            Console.WriteLine($"Command syntax OK, but directory to pack is invalid (type \"neptune init {args[1]}\" for an automatically valid one)");
                            return;
                        }
                        ZipFile.CreateFromDirectory(args[1], args[1] + ".pack");
                        Console.WriteLine("Command OK, directory " + args[1] + " packed");
                    }
                    catch (DirectoryNotFoundException)
                    {
                        Console.WriteLine($"Command syntax OK, but directory to pack does not exist (type \"neptune init {args[1]}\" to create an automatically valid one)");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("A generic exception occurred. Details were written to err.log.");
                        File.AppendAllText("err.log", e.ToString() + "\n\n");
                    }
                }
                else if (args[0] == "info")
                {
                    try
                    {
                        string file = args[1] + "\\";
                        var files = GetAllFiles(new DirectoryInfo(file + "src"));
                        Console.WriteLine("Info about " + args[1] + ":");
                        Console.Write("Valid: ");
                        if (!Validate(file))
                        {
                            Console.WriteLine("False");
                            Environment.Exit(0);
                        }
                        else Console.WriteLine("True");
                        Console.WriteLine("Number of Source Files: " + files.Length);

                        //var cffile = new CfFile(file + ".packconfig");
                        Console.ReadKey();
                        //if(cffile.KeyExists("writtenin"))
                        //  Console.WriteLine("Written in:" + cffile.GetValue("writtenin"));
                    }
                    catch (DirectoryNotFoundException)
                    {
                        Console.WriteLine($"Command syntax OK, but directory does not exist (type \"pack new {args[1]}\" to create an automatically valid one)");
                    }/*
                    catch (Exception e)
                    {
                        Console.WriteLine("A generic exception occurred. Details were written to err.log.");
                        File.AppendAllText("err.log", e.ToString() + "\n\n");
                    }*/
                }
                else if (args[0] == "validate")
                {
                    Console.WriteLine("This pack is " + (Validate(args[1]) ? "valid" : "invalid"));
                }
            }
            else if (args.Length > 0 && args[0] == "help" || args.Length > 0 && args[0] == "-h" || args.Length > 0 && args[0] == "--help" || args.Length > 0 && args[0] == "-?")
            {
                DisplayHelp();
            }
            else
            {
                DisplayHelp();
            }
        }
        public static void ChangeColors(ConsoleColor foreground = ConsoleColor.Gray, ConsoleColor background = ConsoleColor.Black)
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
        }
        public static void DisplayHelp()
        {
            Console.WriteLine("Usage: neptune [command] (parameters)");
            Console.WriteLine("Command is the command to execute.");
            Console.WriteLine("List Of Commands:");
            Console.WriteLine("    dl:");
            Console.WriteLine("       Downloads a pack. Parameters should be the URL of the server.");
            Console.WriteLine("    init:");
            Console.WriteLine("       Creates an uncompressed pack. Parameters should be the name of the pack.");
            Console.WriteLine("    help:");
            Console.WriteLine("       Shows this help page. Parameters should be blank.");
            Console.WriteLine("    template:");
            Console.WriteLine("       Creates an uncompressed pack with a template. Parameters should be the name of the pack, followed by the name of the template.");
            Console.WriteLine("    info:");
            Console.WriteLine("       Shows info about an uncompressed pack. Parameters should be the name of the pack.");
            Console.WriteLine("    get:");
            Console.WriteLine("       Yet to be fully implemented. This will get the pack from the servers at my website. Parameters should be the name of the pack.");
            Console.WriteLine("    compress:");
            Console.WriteLine("       Compresses a folder into a pack. Parameters should be the name of the folder.");
        }

        public static FileInfo[] GetAllFiles(DirectoryInfo d)
        {
            var files = new List<FileInfo>();
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();

            foreach (FileInfo fi in fis)
            {
                files.Add(fi);
            }
            foreach (DirectoryInfo di in d.GetDirectories())
            {
                files.AddRange(GetAllFiles(di));
            }
            return files.ToArray();
        }
        public static bool Validate(string folder)
        {
            return (File.Exists(folder + ".packconfig") && File.Exists(folder + "readme.md"));
        }
        // Checks if the current user has admin privileges. Also works on Linux/Mono.
        public static bool AdminPriv()
        {
            bool isElevated;
            using (System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                isElevated = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            return isElevated;
        }
    }
}
