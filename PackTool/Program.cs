using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private static readonly Regex DeclaredVersion =
            new Regex(@"<Version\s*>\s*(?<version>[^<\s]+)\s*</Version\s*>", RegexOptions.Compiled);

        public static int Main(string[] args)
        {
            try
            {
                var root = ResolveRoot(args);
                var feed = Path.Combine(root, "feed");
                Directory.CreateDirectory(feed);

                // The version declared in Directory.Build.props acts as the floor: a fresh
                // or stale feed must never produce a package below the declared version.
                var declared = ReadDeclaredVersion(root);
                var feedVersions = ReadFeedVersions(feed).ToList();
                var feedMax = VersionBumper.FindMaxVersion(feedVersions);
                var next = VersionBumper.ComputeNextFromFeed(feedVersions, declared);

                Console.WriteLine($"Declared version: {declared ?? "(none)"}");
                Console.WriteLine($"Feed max version: {feedMax ?? "(none)"}");
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

                PublishPackages(root, feed, next);

                Console.WriteLine($"Packages published to {feed}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"PackTool failed: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Reads the canonical package version from Directory.Build.props. Returns null
        /// when the file or the version element is missing.
        /// </summary>
        private static string ReadDeclaredVersion(string root)
        {
            var props = Path.Combine(root, "Directory.Build.props");
            if (!File.Exists(props))
            {
                return null;
            }

            var match = DeclaredVersion.Match(File.ReadAllText(props));
            return match.Success ? match.Groups["version"].Value : null;
        }

        /// <summary>
        /// Packs both toolkit packages into a staging folder and only moves them into the
        /// feed after both are produced with the requested version. This keeps the feed
        /// consistent: either both packages appear with the same version, or neither does.
        /// </summary>
        private static void PublishPackages(string root, string feed, string version)
        {
            var staging = Path.Combine(feed, ".staging");
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
            Directory.CreateDirectory(staging);

            try
            {
                Pack(root, staging, "AgentToolkit", version);
                Pack(root, staging, "AgentToolkit.Wpf", version);

                var expected = new[]
                {
                    Path.Combine(staging, $"AgentToolkit.{version}.nupkg"),
                    Path.Combine(staging, $"AgentToolkit.Wpf.{version}.nupkg"),
                };
                foreach (var file in expected)
                {
                    if (!File.Exists(file))
                    {
                        throw new InvalidOperationException(
                            $"Expected package was not produced: {Path.GetFileName(file)}");
                    }
                }

                foreach (var file in Directory.GetFiles(staging, "*.nupkg"))
                {
                    File.Move(file, Path.Combine(feed, Path.GetFileName(file)), overwrite: true);
                }
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, true);
                }
            }
        }

        private static void Pack(string root, string outputDirectory, string package, string version)
        {
            var csproj = Path.Combine(root, package, package + ".csproj");
            Console.WriteLine($"Packing {package} ...");
            Run(root, "dotnet", "pack", csproj, "-c", "Release",
                "-p:PackageVersion=" + version, "-o", outputDirectory, "--nologo");
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
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                var stdout = stdoutTask.GetAwaiter().GetResult();
                var stderr = stderrTask.GetAwaiter().GetResult();
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
