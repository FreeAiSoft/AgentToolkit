using System;
using System.Collections.Generic;

namespace PackTool
{
    /// <summary>Computes the next feed package version.</summary>
    public static class VersionBumper
    {
        /// <summary>
        /// Returns the highest version among the given strings, or null when none parse.
        /// </summary>
        public static string FindMaxVersion(IEnumerable<string> versions)
        {
            Version max = null;
            string maxText = null;
            foreach (var text in versions)
            {
                if (Version.TryParse(text, out var parsed) &&
                    (max == null || parsed > max))
                {
                    max = parsed;
                    maxText = parsed.ToString();
                }
            }
            return maxText;
        }

        /// <summary>
        /// Computes the next version by bumping the trailing revision component.
        /// The counter starts at 1 because NuGet collapses a trailing ".0" revision
        /// (1.7.2.0 is stored as 1.7.2): 1.7.2 -&gt; 1.7.2.1, 1.4.5.6 -&gt; 1.4.5.7.
        /// </summary>
        public static string ComputeNext(string current)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return "1.0.0.0";
            }

            var parts = current.Split('.');
            var numbers = new int[4];
            for (var i = 0; i < numbers.Length; i++)
            {
                numbers[i] = i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
            }

            numbers[3] = parts.Length >= 4 ? numbers[3] + 1 : 1;

            return $"{numbers[0]}.{numbers[1]}.{numbers[2]}.{numbers[3]}";
        }

        /// <summary>
        /// Computes the next feed version from the versions already present in the feed
        /// plus the version declared in Directory.Build.props. The declared version acts
        /// as a floor: a fresh/empty feed or a stale feed can never produce a package
        /// below the version the repository currently declares.
        /// </summary>
        public static string ComputeNextFromFeed(IEnumerable<string> feedVersions, string declaredVersion)
        {
            var candidates = new List<string>(feedVersions ?? Array.Empty<string>());
            if (!string.IsNullOrWhiteSpace(declaredVersion))
            {
                candidates.Add(declaredVersion);
            }

            return ComputeNext(FindMaxVersion(candidates));
        }
    }
}
