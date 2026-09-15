using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

// The Documentation folder ships inside the release zip, so someone reading it offline cannot fall
// back to searching GitHub when a link is dead. Nothing else in the build checks these links.
// External URLs are deliberately not checked: they need the network and produce false failures from
// rate limits and bot blocking, which must never stand between a change and a merge.
public class DocumentationLinkTests
{
    // [text](target) or [text](target "title"), capturing the target.
    private static readonly Regex k_Link = new(@"\[[^\]]*\]\(\s*([^)\s]+?)(?:\s+""[^""]*"")?\s*\)", RegexOptions.Compiled);
    private static readonly Regex k_Fence = new(@"^\s*(```|~~~)", RegexOptions.Compiled);
    private static readonly Regex k_HeadingLink = new(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);

    [Test]
    public void AllInternalLinksResolve()
    {
        var repoRoot = FindRepositoryRoot();
        if (repoRoot == null)
            Assert.Inconclusive("Could not locate the repository root, so the markdown files are not available.");

        var markdown = EnumerateMarkdown(repoRoot).ToList();
        Assert.That(markdown, Has.Count.GreaterThan(20),
            "Found suspiciously few markdown files; the file discovery is probably broken.");

        var anchorsByFile = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();

        foreach (var file in markdown)
        {
            var directory = Path.GetDirectoryName(file);
            var lines = File.ReadAllLines(file);

            foreach (var (line, lineNumber) in NonFencedLines(lines))
            {
                foreach (Match match in k_Link.Matches(BlankInlineCode(line)))
                {
                    var target = match.Groups[1].Value;
                    if (IsExternal(target))
                        continue;

                    var (path, anchor) = SplitAnchor(target);
                    var targetFile = file;

                    if (path.Length > 0)
                    {
                        targetFile = Path.GetFullPath(Path.Combine(directory, path));
                        if (!ExistsWithExactCase(repoRoot, targetFile))
                        {
                            problems.Add($"{Relative(repoRoot, file)}:{lineNumber}  missing target  {target}");
                            continue;
                        }
                    }

                    if (anchor.Length == 0 || !targetFile.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!anchorsByFile.TryGetValue(targetFile, out var anchors))
                    {
                        anchors = CollectAnchors(targetFile);
                        anchorsByFile[targetFile] = anchors;
                    }

                    if (!anchors.Contains(anchor))
                        problems.Add($"{Relative(repoRoot, file)}:{lineNumber}  missing anchor  {target}");
                }
            }
        }

        Assert.That(problems, Is.Empty,
            $"Unresolved documentation links ({problems.Count}):{Environment.NewLine}"
            + string.Join(Environment.NewLine, problems));
    }

    // The slug rules are the fiddly part of anchor checking, so they are pinned directly.
    [TestCase("## objects", ExpectedResult = "objects")]
    [TestCase("# AssetBundle Format", ExpectedResult = "assetbundle-format")]
    [TestCase("## property_names and property_types", ExpectedResult = "property_names-and-property_types")]
    [TestCase("## mesh_view (MeshProcessor)", ExpectedResult = "mesh_view-meshprocessor")]
    [TestCase("## How `analyze` represents this", ExpectedResult = "how-analyze-represents-this")]
    [TestCase("#### `addressables_builds`", ExpectedResult = "addressables_builds")]
    [TestCase("### m_SceneHashes: mapping a scene", ExpectedResult = "m_scenehashes-mapping-a-scene")]
    [TestCase("## refs / refs_view", ExpectedResult = "refs--refs_view")]
    [TestCase("## dangling_refs / dangling_refs_view", ExpectedResult = "dangling_refs--dangling_refs_view")]
    [TestCase("## **Bold** heading", ExpectedResult = "bold-heading")]
    [TestCase("## [Linked](somewhere.md) heading", ExpectedResult = "linked-heading")]
    public string SlugMatchesGitHubRules(string heading) => Slug(heading);

    [Test]
    public void RepeatedHeadingsGetNumberedAnchors()
    {
        var file = Path.Combine(TestContext.CurrentContext.TestDirectory, "slug_dedup_test.md");
        File.WriteAllText(file, "# Example\n# Example\n# Example\n");
        try
        {
            Assert.That(CollectAnchors(file), Is.EquivalentTo(new[] { "example", "example-1", "example-2" }));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static bool IsExternal(string target) =>
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private static (string path, string anchor) SplitAnchor(string target)
    {
        var hash = target.IndexOf('#');
        var path = hash < 0 ? target : target.Substring(0, hash);
        var anchor = hash < 0 ? "" : target.Substring(hash + 1);
        return (Uri.UnescapeDataString(path), Uri.UnescapeDataString(anchor).ToLowerInvariant());
    }

    // Walks up from the test assembly looking for the solution file. Returns null when the tests run
    // from somewhere the repository is not available, so the caller can skip rather than fail.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UnityDataTools.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateMarkdown(string repoRoot)
    {
        // Library holds Unity's package cache: third-party markdown we do not maintain.
        var skipped = new[] { "bin", "obj", "Library", ".git" };

        return Directory.EnumerateFiles(repoRoot, "*.md", SearchOption.AllDirectories)
            .Where(f => !Relative(repoRoot, f)
                .Split('/')
                .SkipLast(1)
                .Any(segment => skipped.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    // Skips fenced code blocks so that example commands and SQL are not mistaken for links or headings.
    private static IEnumerable<(string line, int lineNumber)> NonFencedLines(IReadOnlyList<string> lines)
    {
        var inFence = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (k_Fence.IsMatch(lines[i]))
            {
                inFence = !inFence;
                continue;
            }
            if (!inFence)
                yield return (lines[i], i + 1);
        }
    }

    // Blanks the content of inline code spans, so that a documented example of link syntax is not
    // mistaken for a real link. Not used for headings: GitHub keeps the code text when building an
    // anchor, so `analyze` in a heading still contributes "analyze" to the slug.
    private static string BlankInlineCode(string line)
    {
        var builder = new StringBuilder(line.Length);
        var inCode = false;
        foreach (var c in line)
        {
            if (c == '`')
            {
                inCode = !inCode;
                builder.Append(' ');
            }
            else
            {
                builder.Append(inCode ? ' ' : c);
            }
        }
        return builder.ToString();
    }

    private static HashSet<string> CollectAnchors(string file)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (line, _) in NonFencedLines(File.ReadAllLines(file)))
        {
            if (!line.StartsWith("#"))
                continue;

            var slug = Slug(line);
            if (slug.Length == 0)
                continue;

            // GitHub disambiguates repeated headings by appending -1, -2, and so on.
            if (counts.TryGetValue(slug, out var seen))
            {
                counts[slug] = seen + 1;
                anchors.Add($"{slug}-{seen}");
            }
            else
            {
                counts[slug] = 1;
                anchors.Add(slug);
            }
        }
        return anchors;
    }

    // Reproduces GitHub's heading-to-anchor rule: drop the markers and inline formatting, lower-case,
    // discard punctuation, then turn each remaining space into a hyphen. Underscores and hyphens are
    // kept. Spaces are converted one for one rather than collapsed, which is why the heading
    // "dangling_refs / dangling_refs_view" anchors as "dangling_refs--dangling_refs_view".
    private static string Slug(string heading)
    {
        var text = heading.TrimStart('#').Trim();
        text = k_HeadingLink.Replace(text, "$1");
        text = text.Replace("`", "").Replace("*", "");
        text = text.ToLowerInvariant();

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                builder.Append(c);
            else if (char.IsWhiteSpace(c))
                builder.Append('-');
        }

        return builder.ToString();
    }

    // File.Exists is case-insensitive on Windows and macOS, which lets a wrong-case link reach Linux
    // users broken. Comparing against the real directory entries makes the check behave the same
    // everywhere.
    private static bool ExistsWithExactCase(string repoRoot, string fullPath)
    {
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            return false;

        var relative = Relative(repoRoot, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
            return true; // outside the repository; nothing to compare against

        var current = repoRoot;
        foreach (var segment in relative.Split('/'))
        {
            var match = Directory.EnumerateFileSystemEntries(current)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => string.Equals(name, segment, StringComparison.Ordinal));
            if (match == null)
                return false;
            current = Path.Combine(current, match);
        }
        return true;
    }

    private static string Relative(string repoRoot, string fullPath) =>
        Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');
}
