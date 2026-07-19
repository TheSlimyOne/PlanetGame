using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

static class ShaderError
{
    // Matches: "ERROR: 0:226:"  -> group "line" = 226
    private static readonly Regex GlslErrorLineRegex = new Regex(@"ERROR:\s*\d+:(?<line>\d+):", RegexOptions.Compiled);

    public static string FormatError(string sourceCompute, string compileError)
    {
        string[] srcLines = sourceCompute.Replace("\r\n", "\n").Split('\n');

        HashSet<int> errorLines = [];
        foreach (Match m in GlslErrorLineRegex.Matches(compileError))
        {
            if (int.TryParse(m.Groups["line"].Value, out int line) && line > 0)
                errorLines.Add(line);
        }

        var sb = new StringBuilder(sourceCompute.Length * 2);

        for (int i = 0; i < srcLines.Length; i++)
        {
            int lineNo = i + 1;
            bool isError = errorLines.Contains(lineNo);

            if (isError)
                sb.Append("[color=#ff4040]");

            sb.Append(lineNo.ToString().PadLeft(4));
            sb.Append("  ");
            sb.Append(EscapeBbcode(srcLines[i]));

            if (isError)
                sb.Append("[/color]");

            sb.Append('\n');
        }
        sb.AppendLine();
        
        return sb.ToString();
    }

    private static string EscapeBbcode(string s)
    {
        return s
            .Replace("[", "\u0001")
            .Replace("]", "\u0002")
            .Replace("\u0001", "[lb]")
            .Replace("\u0002", "[rb]");
    }
}
