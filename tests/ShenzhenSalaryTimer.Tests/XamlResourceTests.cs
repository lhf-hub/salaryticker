// ============================================================================
// 文件名：XamlResourceTests.cs
// 功能描述：静态检查 XAML 里的资源引用是否都有定义。
//           dotnet build 只编译 BAML，dotnet test 不会实例化窗口；
//           缺 key / 重命名漏改只会在运行时炸。这个测试补上静态闸门。
// ============================================================================

using System.Text.RegularExpressions;
using Xunit;

namespace ShenzhenSalaryTimer.Tests;

public class XamlResourceTests
{
    private static readonly Regex KeyDefinition = new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex StyleTag = new(@"<Style\b[^>]*>", RegexOptions.Compiled);
    private static readonly Regex TargetType = new(@"TargetType=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex ResourceReference =
        new(@"\{(StaticResource|DynamicResource)\s+([^}]+)\}", RegexOptions.Compiled);

    [Fact]
    public void AllXamlResourceReferencesResolve()
    {
        var root = FindRepoRoot();
        var files = Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin")
                        && !HasPathSegment(path, "obj")
                        && !HasPathSegment(path, ".git")
                        && !HasPathSegment(path, ".claude"))
            .ToArray();

        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            foreach (Match match in KeyDefinition.Matches(text))
                defined.Add(match.Groups[1].Value);

            // 隐式样式（<Style TargetType="TextBox"> 没有 x:Key）以类型名作为 key，
            // 对应 {StaticResource {x:Type TextBox}} 的引用方式。
            foreach (Match match in StyleTag.Matches(text))
            {
                if (match.Value.Contains("x:Key=", StringComparison.Ordinal))
                    continue;

                var targetType = TargetType.Match(match.Value);
                if (targetType.Success)
                    defined.Add(targetType.Groups[1].Value.Split(':').Last());
            }
        }

        var missing = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match match in ResourceReference.Matches(text))
            {
                var key = match.Groups[2].Value.Trim();

                if (key.StartsWith("{x:Type ", StringComparison.Ordinal))
                {
                    key = key["{x:Type ".Length..].Split('}')[0].Trim().Split(':').Last();
                }
                else if (key.IndexOfAny(new[] { '{', '}', '.', ':', ' ' }) >= 0)
                {
                    // 系统资源、转换器等不在本仓库字典里的引用，交给运行时 smoke。
                    continue;
                }

                if (defined.Contains(key))
                    continue;

                var line = text[..match.Index].Count(c => c == '\n') + 1;
                missing.Add($"{Path.GetRelativePath(root, file)}:{line} -> {key}");
            }
        }

        Assert.True(
            missing.Count == 0,
            "发现未定义的 XAML 资源引用：" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "SalaryTicker.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
