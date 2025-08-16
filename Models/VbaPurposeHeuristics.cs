using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MakroCompare1408.Models;

public static class VbaPurposeHeuristics
{
	private static readonly Regex SumPattern = new(@"(?i)\b(sum|total|toplam)\b|WorksheetFunction\.Sum|Application\.WorksheetFunction\.Sum|\b(\w+)\s*=\s*\1\s*\+", RegexOptions.Compiled);
	private static readonly Regex AvgPattern = new(@"(?i)Average|WorksheetFunction\.Average|Application\.WorksheetFunction\.Average", RegexOptions.Compiled);
	private static readonly Regex CopyPattern = new(@"(?i)\.Copy\b|\.Paste(Special)?\b", RegexOptions.Compiled);
	private static readonly Regex FilterPattern = new(@"(?i)AutoFilter\b|AdvancedFilter\b", RegexOptions.Compiled);
	private static readonly Regex SortPattern = new(@"(?i)Sort\b|SortFields\b", RegexOptions.Compiled);
	private static readonly Regex FindReplacePattern = new(@"(?i)\.Find\(|Replace\(", RegexOptions.Compiled);
	private static readonly Regex RangePattern = new(@"(?i)Range\(""[^""]+""\)|Cells\([^)]*\)|Worksheets\?\([^)]*\)", RegexOptions.Compiled);
	private static readonly Regex ForLoopPattern = new(@"(?i)\bFor\b[\s\S]*?\bNext\b", RegexOptions.Compiled);
	private static readonly Regex WhileLoopPattern = new(@"(?i)\bDo\s+While\b[\s\S]*?\bLoop\b|\bDo\b[\s\S]*?\bLoop\s+While\b", RegexOptions.Compiled);
	private static readonly Regex IfPattern = new(@"(?i)\bIf\b[\s\S]*?\bEnd\s*If\b", RegexOptions.Compiled);
	private static readonly Regex AddOpPattern = new(@"\+", RegexOptions.Compiled);
	private static readonly Regex MulOpPattern = new(@"\*", RegexOptions.Compiled);
	private static readonly Regex AssignPattern = new(@"(?i)\b[A-Za-z_][A-Za-z0-9_]*\s*=\s*", RegexOptions.Compiled);

	public static (string purpose, List<string> io, Dictionary<string,int> ops) ExtractSummary(string raw)
	{
		var code = VbaTokenizer.NormalizeVbaCode(raw);

		string purpose = "unknown";
		if (SumPattern.IsMatch(code)) purpose = "sum values";
		else if (AvgPattern.IsMatch(code)) purpose = "average values";
		else if (CopyPattern.IsMatch(code)) purpose = "copy/paste range";
		else if (FilterPattern.IsMatch(code)) purpose = "filter data";
		else if (SortPattern.IsMatch(code)) purpose = "sort data";
		else if (FindReplacePattern.IsMatch(code)) purpose = "find/replace";

		var io = RangePattern.Matches(raw).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();

		var ops = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
		{
			["loop_for"] = ForLoopPattern.Matches(code).Count,
			["loop_while"] = WhileLoopPattern.Matches(code).Count,
			["if_count"] = IfPattern.Matches(code).Count,
			["assign_count"] = AssignPattern.Matches(code).Count,
			["add_ops"] = AddOpPattern.Matches(code).Count,
			["mul_ops"] = MulOpPattern.Matches(code).Count
		};

		return (purpose, io, ops);
	}

	public static string ToCompactString((string purpose, List<string> io, Dictionary<string,int> ops) s)
	{
		string ioStr = s.io.Count == 0 ? "-" : string.Join(";", s.io);
		string opsStr = string.Join(",", s.ops.Select(kv => kv.Key+"="+kv.Value));
		return $"purpose={s.purpose} | io={ioStr} | ops={opsStr}";
	}
}
