using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Agent.Tests
{
    /// <summary>
    /// Regression guard for the theme coverage fix (Task 2). Fails when a control XAML file
    /// re-introduces a hardcoded color instead of a Theme.* palette brush, and verifies that every
    /// Theme.* key referenced anywhere in the toolkit is actually defined in both palettes.
    /// </summary>
    [TestFixture]
    public class ThemeHardcodeGuardTests
    {
        private static readonly Regex HexColor = new Regex(@"#[0-9A-Fa-f]{6,8}", RegexOptions.Compiled);
        private static readonly Regex BlackBackground = new Regex(@"Background\s*=\s*""Black""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DynamicResourceKey = new Regex(@"\{DynamicResource\s+(Theme\.[A-Za-z0-9_]+)\}", RegexOptions.Compiled);
        private static readonly Regex CodeStringKey = new Regex(@"""Theme\.([A-Za-z0-9_]+)""", RegexOptions.Compiled);
        private static readonly Regex PaletteKey = new Regex(@"x:Key=""(Theme\.[A-Za-z0-9_]+)""", RegexOptions.Compiled);

        // Palettes and the legacy Dark.* shim are the only XAML files allowed to carry hex values.
        private static readonly HashSet<string> HardcodeExemptFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Theme.Dark.xaml",
            "Theme.Light.xaml",
            "AgentTheme.xaml",
        };

        private static string RepoRoot => FindRepoRoot();

        [Test]
        public void ControlXaml_HasNoHardcodedColors()
        {
            var failures = new List<string>();
            foreach (var file in EnumerateFiles("*.xaml").Where(f => !HardcodeExemptFiles.Contains(Path.GetFileName(f))))
            {
                var content = File.ReadAllText(file);
                var relative = Relative(file);
                foreach (Match m in HexColor.Matches(content))
                    failures.Add($"{relative}: hardcoded color '{m.Value}'");
                foreach (Match m in BlackBackground.Matches(content))
                    failures.Add($"{relative}: '{m.Value.Trim()}'");
            }

            Assert.That(failures, Is.Empty,
                "Control XAML must reference Theme.* palette brushes, not hardcoded colors:\n" + string.Join("\n", failures));
        }

        [Test]
        public void ReferencedThemeKeys_ExistInBothPalettes()
        {
            var dark = PaletteKeysOf("Agent.Toolkit.Wpf/Styles/Theme.Dark.xaml");
            var light = PaletteKeysOf("Agent.Toolkit.Wpf/Styles/Theme.Light.xaml");

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in EnumerateFiles("*.xaml").Concat(EnumerateFiles("*.cs")))
            {
                foreach (Match m in DynamicResourceKey.Matches(File.ReadAllText(file)))
                    referenced.Add(m.Groups[1].Value);
            }
            foreach (var file in EnumerateFiles("*.cs"))
            {
                foreach (Match m in CodeStringKey.Matches(File.ReadAllText(file)))
                    referenced.Add("Theme." + m.Groups[1].Value);
            }

            var missing = referenced
                .Where(k => !dark.Contains(k) || !light.Contains(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            Assert.That(missing, Is.Empty,
                "Theme.* keys referenced in the toolkit must be defined in both palettes:\n" + string.Join("\n", missing));
        }

        private static HashSet<string> PaletteKeysOf(string palettePath)
        {
            var content = File.ReadAllText(Path.Combine(RepoRoot, palettePath));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in PaletteKey.Matches(content))
                keys.Add(m.Groups[1].Value);
            return keys;
        }

        private static IEnumerable<string> EnumerateFiles(string pattern)
        {
            var root = Path.Combine(RepoRoot, "Agent.Toolkit.Wpf");
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .Where(f => !IsBuildOutput(f));
        }

        private static bool IsBuildOutput(string path)
        {
            var normalized = path.Replace('\\', '/');
            return normalized.Contains("/bin/") || normalized.Contains("/obj/");
        }

        private static string Relative(string path)
            => path.Substring(RepoRoot.Length).TrimStart('\\', '/');

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Agent.Toolkit.Wpf")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Agent.Tests")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the AgentShared repository root.");
        }
    }
}
