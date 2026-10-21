using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Apify.Client.Tests.Examples;

/// <summary>
/// Compiles every <c>csharp</c> code block of the top-level README and of <c>docs/*.md</c> against the
/// library, so a snippet that no longer compiles (a renamed method, a missing argument, a bare fragment)
/// fails CI. The runnable scenarios in <c>docs/examples.md</c> are additionally executed by
/// <see cref="ExamplesTests"/>.
/// </summary>
/// <remarks>
/// A block that starts with <c>using</c> directives is a complete program (top-level statements). Any
/// other block is a fragment that continues the most recent complete block of the same file, so it is
/// compiled after that block's directives and declarations.
/// </remarks>
[Trait("Category", "Examples")]
public sealed class DocumentationSnippetTests
{
    private static readonly Regex CodeBlock = new(@"```csharp\r?\n(?<code>.*?)```", RegexOptions.Singleline | RegexOptions.Compiled);

    public static IEnumerable<object[]> Snippets()
    {
        var root = FindRepositoryRoot();
        var files = new[] { Path.Combine(root, "README.md") }
            .Concat(Directory.GetFiles(Path.Combine(root, "docs"), "*.md").OrderBy(static f => f, StringComparer.Ordinal));
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var lastComplete = string.Empty;
            var index = 0;
            foreach (Match match in CodeBlock.Matches(File.ReadAllText(file)))
            {
                index++;
                var code = match.Groups["code"].Value;
                var isComplete = code.TrimStart().StartsWith("using ", StringComparison.Ordinal);
                var program = isComplete ? code : lastComplete + "\n" + code;
                if (isComplete)
                {
                    lastComplete = code;
                }

                yield return new object[] { relative + " snippet #" + index, program };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Snippets))]
    public void SnippetCompiles(string name, string program)
    {
        var tree = CSharpSyntaxTree.ParseText(program, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            "DocSnippet",
            new[] { tree },
            References(),
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Select(static d => d.ToString())
            .ToList();

        Assert.True(errors.Count == 0, name + " does not compile:\n" + string.Join("\n", errors));
    }

    private static IEnumerable<MetadataReference> References()
    {
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (var path in trusted.Split(Path.PathSeparator))
        {
            yield return MetadataReference.CreateFromFile(path);
        }

        yield return MetadataReference.CreateFromFile(typeof(ApifyClient).Assembly.Location);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Apify.Client.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root (Apify.Client.sln).");
    }
}
