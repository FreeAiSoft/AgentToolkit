using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace PackTool
{
    /// <summary>
    /// Command-line tool invoked by the Agent.Core Release post-build event.
    /// Computes the next package version from the local feed (feed\) and rebuilds +
    /// packs the combined AgentToolkit / AgentToolkit.Wpf packages into the feed.
    /// </summary>
    internal static class Program
    {
        private static readonly Regex VersionSuffix =
            new Regex(@"\.(?<version>\d+(?:\.\d+)+)$", RegexOptions.Compiled);

        public static int Main(string[] args)
        {
            try
            {
                var root = ResolveRoot(args);
                var feed = Path.Combine(root, "feed");
                Directory.CreateDirectory(feed);

                var max = VersionBumper.FindMaxVersion(ReadFeedVersions(feed));
                var next = VersionBumper.ComputeNext(max);

                Console.WriteLine($"Feed max version: {max ?? "(none)"}");
                Console.WriteLine($"Packing version:  {next}");

                string[] projects =
                {
                    Path.Combine(root, "Agent.Mvvm", "Agent.Mvvm.csproj"),
                    Path.Combine(root, "Agent.Core", "Agent.Core.csproj"),
                    Path.Combine(root, "Agent.Models", "Agent.Models.csproj"),
                    Path.Combine(root, "Agent.Toolkit.Wpf", "Agent.Toolkit.Wpf.csproj"),
                };

                foreach (var csproj in projects)
                {
                    Console.WriteLine($"Building {Path.GetFileName(Path.GetDirectoryName(csproj))} ...");
                    // AgentSkipReleasePack guards against recursion: Agent.Core's
                    // post-build event must not fire again from here.
                    Run(root, "dotnet", "build", csproj, "-c", "Release",
                        "-p:AgentSkipReleasePack=true", "--nologo");
                }

                Pack(root, feed, "AgentToolkit", next);
                Pack(root, feed, "AgentToolkit.Wpf", next);

                Console.WriteLine($"Packages published to {feed}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"PackTool failed: {ex.Message}");
                return 1;
            }
        }

        private static void Pack(string root, string feed, string package, string version)
        {
            var csproj = Path.Combine(root, package, package + ".csproj");
            Console.WriteLine($"Packing {package} ...");
            Run(root, "dotnet", "pack", csproj, "-c", "Release",
                "-p:PackageVersion=" + version, "-o", feed, "--nologo");
        }

        private static IEnumerable<string> ReadFeedVersions(string feed)
        {
            foreach (var file in Directory.GetFiles(feed, "*.nupkg"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var match = VersionSuffix.Match(name);
                if (match.Success)
                {
                    yield return match.Groups["version"].Value;
                }
            }
        }

        private static string ResolveRoot(string[] args)
        {
            return args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? Path.GetFullPath(args[0])
                : Directory.GetCurrentDirectory();
        }

        private static void Run(string workingDirectory, string fileName, params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            Console.WriteLine($"> {fileName} {string.Join(" ", args)}");

            using (var process = Process.Start(psi))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Console.Write(stdout);
                if (!string.IsNullOrEmpty(stderr))
                {
                    Console.Error.Write(stderr);
                }
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"{fileName} {args[0]} failed with exit code {process.ExitCode}.");
                }
            }
        }
    }
}
