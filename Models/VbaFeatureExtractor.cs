using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MakroCompare1408.Models;

public static class VbaFeatureExtractor
{
	private static readonly Regex SubOrFunctionRegex = new(@"(?i)\b(Sub|Function)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
	private static readonly Regex ForLoopRegex = new(@"(?i)\bFor\b[\s\S]*?\bNext\b", RegexOptions.Compiled);
	private static readonly Regex DoLoopRegex = new(@"(?i)\bDo\b[\s\S]*?\bLoop\b", RegexOptions.Compiled);
	private static readonly Regex IfRegex = new(@"(?i)\bIf\b[\s\S]*?\bEnd\s*If\b", RegexOptions.Compiled);
	private static readonly Regex SelectCaseRegex = new(@"(?i)\bSelect\s+Case\b[\s\S]*?\bEnd\s*Select\b", RegexOptions.Compiled);
	private static readonly Regex ArrayRegex = new(@"(?i)\bReDim\b|\((?:\s*__NUM__\s*(?:,\s*__NUM__\s*)*)\)", RegexOptions.Compiled);
	private static readonly Regex WorksheetRegex = new(@"(?i)Worksheets?\(|Range\(|Cells\(|Columns?\(|Rows\(", RegexOptions.Compiled);

	public static Dictionary<string, double> ExtractFeatures(string rawCode)
	{
		var code = VbaTokenizer.NormalizeVbaCode(rawCode);
		var tokens = VbaTokenizer.GetTokens(code);
		var unigrams = new HashSet<string>(tokens);
		var bigrams = VbaTokenizer.GetNGrams(tokens, 2);
		var trigrams = VbaTokenizer.GetNGrams(tokens, 3);

		double length = tokens.Count;
		return new Dictionary<string, double>
		{
			["length_tokens"] = length,
			["uniq_unigrams"] = unigrams.Count,
			["uniq_bigrams"] = bigrams.Count,
			["uniq_trigrams"] = trigrams.Count,
			["has_sub_or_function"] = SubOrFunctionRegex.IsMatch(code) ? 1 : 0,
			["loops_for_count"] = ForLoopRegex.Matches(code).Count,
			["loops_do_count"] = DoLoopRegex.Matches(code).Count,
			["condition_if_count"] = IfRegex.Matches(code).Count,
			["select_case_count"] = SelectCaseRegex.Matches(code).Count,
			["array_usage"] = ArrayRegex.IsMatch(code) ? 1 : 0,
			["worksheet_ops"] = WorksheetRegex.Matches(code).Count
		};
	}

	public static double Jaccard(HashSet<string> a, HashSet<string> b)
	{
		if (a.Count == 0 && b.Count == 0) return 1.0;
		var inter = a.Intersect(b).Count();
		var union = a.Union(b).Count();
		return union == 0 ? 0.0 : (double)inter / union;
	}

	public static (double uni, double bi, double tri) NgramSimilarities(string raw1, string raw2)
	{
		var c1 = VbaTokenizer.NormalizeVbaCode(raw1);
		var c2 = VbaTokenizer.NormalizeVbaCode(raw2);
		var t1 = VbaTokenizer.GetTokens(c1);
		var t2 = VbaTokenizer.GetTokens(c2);
		var u1 = new HashSet<string>(t1);
		var u2 = new HashSet<string>(t2);
		var b1 = VbaTokenizer.GetNGrams(t1, 2);
		var b2 = VbaTokenizer.GetNGrams(t2, 2);
		var g1 = VbaTokenizer.GetNGrams(t1, 3);
		var g2 = VbaTokenizer.GetNGrams(t2, 3);
		return (Jaccard(u1, u2), Jaccard(b1, b2), Jaccard(g1, g2));
	}
}
