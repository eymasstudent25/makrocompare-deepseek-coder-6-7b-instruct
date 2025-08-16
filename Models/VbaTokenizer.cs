using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MakroCompare1408.Models;

public static class VbaTokenizer
{
	private static readonly HashSet<string> VbaKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"Option","Explicit","Public","Private","Dim","ReDim","Static","Const","As","ByRef","ByVal",
		"Sub","Function","End","If","Then","Else","ElseIf","Select","Case","For","To","Step",
		"Next","Do","While","Loop","Until","Each","In","With","EndWith","Exit","GoTo","On","Error",
		"Resume","Call","Set","New","Not","And","Or","Xor","Mod","Is","Nothing","True","False",
		"Len","Mid","Left","Right","Instr","Replace","Split","Join","UCase","LCase","Trim","CInt","CLng","CDbl","CStr"
	};

	private static readonly Regex CommentLineRegex = new(@"^\s*'(?<c>.*)$", RegexOptions.Multiline | RegexOptions.Compiled);
	private static readonly Regex RemCommentRegex = new(@"(?i)\bRem\b.*$", RegexOptions.Multiline | RegexOptions.Compiled);
	private static readonly Regex LineContinuationRegex = new(@" _\r?\n", RegexOptions.Compiled);
	private static readonly Regex StringLiteralRegex = new(@"""(?:\\.|[^""])*""", RegexOptions.Compiled);
	private static readonly Regex NumberLiteralRegex = new(@"\b\d+(?:\.\d+)?\b", RegexOptions.Compiled);
	private static readonly Regex IdentifierRegex = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
	private static readonly Regex PunctuationRegex = new(@"[(),.:=<>+\-*/]", RegexOptions.Compiled);

	public static string NormalizeVbaCode(string code)
	{
		if (string.IsNullOrWhiteSpace(code)) return string.Empty;

		var text = code.Replace("\r\n", "\n").Replace("\r", "\n");
		text = LineContinuationRegex.Replace(text, " ");
		text = CommentLineRegex.Replace(text, string.Empty);
		text = RemCommentRegex.Replace(text, string.Empty);
		// Collapse strings and numbers to placeholders
		text = StringLiteralRegex.Replace(text, " __STR__ ");
		text = NumberLiteralRegex.Replace(text, " __NUM__ ");
		// Remove extra whitespace
		text = Regex.Replace(text, @"\s+", " ");
		return text.Trim();
	}

	public static List<string> GetTokens(string code)
	{
		var tokens = new List<string>();
		if (string.IsNullOrWhiteSpace(code)) return tokens;

		int index = 0;
		while (index < code.Length)
		{
			char c = code[index];
			if (char.IsWhiteSpace(c)) { index++; continue; }

			// punctuation
			var mP = PunctuationRegex.Match(code, index);
			if (mP.Success && mP.Index == index)
			{
				tokens.Add(mP.Value);
				index += mP.Length;
				continue;
			}

			// string/number placeholders
			if (code.AsSpan(index).StartsWith("__STR__")) { tokens.Add("__STR__"); index += 7; continue; }
			if (code.AsSpan(index).StartsWith("__NUM__")) { tokens.Add("__NUM__"); index += 7; continue; }

			// identifier/keyword
			var mId = IdentifierRegex.Match(code, index);
			if (mId.Success && mId.Index == index)
			{
				var id = mId.Value;
				if (VbaKeywords.Contains(id))
				{
					tokens.Add(id.ToUpperInvariant());
				}
				else
				{
					// normalize identifiers
					tokens.Add("__ID__");
				}
				index += mId.Length;
				continue;
			}

			// fallback single char
			tokens.Add(c.ToString());
			index++;
		}

		return tokens;
	}

	public static HashSet<string> GetNGrams(IReadOnlyList<string> tokens, int n)
	{
		var set = new HashSet<string>(StringComparer.Ordinal);
		if (tokens.Count < n) return set;
		for (int i = 0; i <= tokens.Count - n; i++)
		{
			var gram = string.Join(' ', tokens.Skip(i).Take(n));
			set.Add(gram);
		}
		return set;
	}
}
