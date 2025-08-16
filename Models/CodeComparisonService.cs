using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MakroCompare1408.Models;

public class CodeComparisonService
{
	private readonly HttpClient _httpClient;
	private readonly string _ollamaUrl = "http://localhost:11434/api/generate";
	private readonly string _ollamaPullUrl = "http://localhost:11434/api/pull";
	private bool _modelWarmedUp = false;
	private readonly Dictionary<string, (double logical, string analysis)> _cache = new();

	public CodeComparisonService(HttpClient httpClient)
	{
		_httpClient = httpClient;
		_ = WarmupModelAsync(); // Arka planda model ısınması
	}

	private async Task WarmupModelAsync()
	{
		try
		{
			// Modeli ısıtmak için basit bir çağrı yap
			var warmupRequest = new OllamaRequest
			{
				Model = "deepseek-coder:6.7b",
				Prompt = "test",
				Stream = false,
				Temperature = 0,
				TopP = 1.0,
				Options = new Dictionary<string, object>
				{
					["num_predict"] = 1,
					["num_thread"] = Environment.ProcessorCount,
					["num_ctx"] = 512,
					["keep_alive"] = "5m" // 5 dakika ısınmış tut
				}
			};

			var json = JsonSerializer.Serialize(warmupRequest);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			await _httpClient.PostAsync(_ollamaUrl, content);
			_modelWarmedUp = true;
		}
		catch (Exception)
		{
			// Model ısınması başarısız olsa bile devam et
			_modelWarmedUp = false;
		}
	}

	public async Task<CodeComparisonResult> CompareCodesAsync(string code1, string code2)
	{
		var result = new CodeComparisonResult();

		// Syntax benzerliği
		result.SyntaxSimilarity = CalculateVbaSyntaxSimilarity(code1, code2);

		// LLM + Cache
		var cacheKey = GenerateCacheKey(code1, code2);
		double llmLogical;
		string details;
		if (_cache.TryGetValue(cacheKey, out var cached))
		{
			llmLogical = cached.logical;
			details = cached.analysis;
		}
		else
		{
			llmLogical = await CalculateLogicalSimilarityAsync(code1, code2);
			details = await GetDetailedAnalysisAsync(code1, code2);
			_cache[cacheKey] = (llmLogical, details);
			if (_cache.Count > 1000)
			{
				var oldestKey = _cache.Keys.First();
				_cache.Remove(oldestKey);
			}
		}

		// Heuristik amaç benzerliği skoru
		double heuristicLogical = CalculateHeuristicLogicalSimilarity(code1, code2);

		// Birleştir: LLM %70 + Heuristik %30
		result.LogicalSimilarity = Math.Max(0, Math.Min(100, llmLogical * 0.7 + heuristicLogical * 0.3));
		result.DetailedAnalysis = details;

		// Genel skor (mantık %65, syntax %35)
		result.OverallSimilarity = Math.Max(0, Math.Min(100, result.SyntaxSimilarity * 0.35 + result.LogicalSimilarity * 0.65));
		return result;
	}

	private double CalculateHeuristicLogicalSimilarity(string raw1, string raw2)
	{
		var h1 = VbaPurposeHeuristics.ExtractSummary(raw1);
		var h2 = VbaPurposeHeuristics.ExtractSummary(raw2);

		// Amaç eşleşmesi
		double purposeScore = string.Equals(h1.purpose, h2.purpose, StringComparison.OrdinalIgnoreCase) && h1.purpose != "unknown" ? 92 : 30;

		// IO benzerliği (Jaccard)
		var ioSet1 = new HashSet<string>(h1.io, StringComparer.OrdinalIgnoreCase);
		var ioSet2 = new HashSet<string>(h2.io, StringComparer.OrdinalIgnoreCase);
		double ioScore = ioSet1.Count == 0 && ioSet2.Count == 0 ? 70 : (ioSet1.Intersect(ioSet2, StringComparer.OrdinalIgnoreCase).Count() / (double)ioSet1.Union(ioSet2, StringComparer.OrdinalIgnoreCase).Count()) * 100.0;

		// Operatör/döngü kalıbı benzerliği (kosin benzeri)
		var allKeys = h1.ops.Keys.Union(h2.ops.Keys, StringComparer.OrdinalIgnoreCase).ToList();
		double dot = 0, n1 = 0, n2 = 0;
		foreach (var k in allKeys)
		{
			int v1 = h1.ops.TryGetValue(k, out var a) ? a : 0;
			int v2 = h2.ops.TryGetValue(k, out var b) ? b : 0;
			dot += v1 * v2;
			n1 += v1 * v1;
			n2 += v2 * v2;
		}
		double denom = Math.Sqrt(n1) * Math.Sqrt(n2);
		double opScore = denom == 0 ? 50 : (dot / denom) * 100.0;

		// Heuristik toplam: amaç %60, IO %20, op kalıbı %20
		double score = purposeScore * 0.6 + ioScore * 0.2 + opScore * 0.2;
		return Math.Max(0, Math.Min(100, score));
	}

	private string GenerateCacheKey(string code1, string code2)
	{
		var normalized1 = VbaTokenizer.NormalizeVbaCode(code1);
		var normalized2 = VbaTokenizer.NormalizeVbaCode(code2);
		var combined = string.Compare(normalized1, normalized2) <= 0 ? normalized1 + "|||" + normalized2 : normalized2 + "|||" + normalized1;
		using var sha256 = SHA256.Create();
		var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
		return Convert.ToBase64String(hashBytes);
	}

	private double CalculateVbaSyntaxSimilarity(string raw1, string raw2)
	{
		if (string.IsNullOrWhiteSpace(raw1) && string.IsNullOrWhiteSpace(raw2)) return 100.0;
		if (string.IsNullOrWhiteSpace(raw1) || string.IsNullOrWhiteSpace(raw2)) return 0.0;
		var (uni, bi, tri) = VbaFeatureExtractor.NgramSimilarities(raw1, raw2);
		var c1 = VbaTokenizer.NormalizeVbaCode(raw1);
		var c2 = VbaTokenizer.NormalizeVbaCode(raw2);
		var charSim = CalculateCharacterSimilarity(c1, c2) / 100.0;
		double score = uni * 0.2 + bi * 0.4 + tri * 0.3 + charSim * 0.1;
		return Math.Max(0, Math.Min(100, score * 100.0));
	}

	private string NormalizeCode(string code)
	{
		var normalized = Regex.Replace(code, @"\s+", " ");
		normalized = Regex.Replace(normalized, @"//.*$", "", RegexOptions.Multiline);
		normalized = Regex.Replace(normalized, @"/\*.*?\*/", "", RegexOptions.Singleline);
		normalized = normalized.Replace("\r\n", "\n").Replace("\r", "\n");
		return normalized.Trim();
	}

	private double CalculateCharacterSimilarity(string code1, string code2)
	{
		if (code1.Length == 0 && code2.Length == 0) return 100.0;
		if (code1.Length == 0 || code2.Length == 0) return 0.0;
		var distance = CalculateLevenshteinDistance(code1, code2);
		var maxLength = Math.Max(code1.Length, code2.Length);
		return Math.Max(0, 100 - ((double)distance / maxLength * 100));
	}

	private int CalculateLevenshteinDistance(string s1, string s2)
	{
		var matrix = new int[s1.Length + 1, s2.Length + 1];
		for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
		for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;
		for (int i = 1; i <= s1.Length; i++)
		{
			for (int j = 1; j <= s2.Length; j++)
			{
				var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
				matrix[i, j] = Math.Min(
					Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
					matrix[i - 1, j - 1] + cost);
			}
		}
		return matrix[s1.Length, s2.Length];
	}

	private async Task<double> CalculateLogicalSimilarityAsync(string raw1, string raw2)
	{
		try
		{
			// Heuristik özetler
			var h1 = VbaPurposeHeuristics.ExtractSummary(raw1);
			var h2 = VbaPurposeHeuristics.ExtractSummary(raw2);
			string h1s = VbaPurposeHeuristics.ToCompactString(h1);
			string h2s = VbaPurposeHeuristics.ToCompactString(h2);

			// Few-shot ve amaç odaklı prompt
			string fewShot = @"Examples:
Input: MacroA purpose=sum values using For loop; MacroB purpose=sum values using While loop
Output: 92

Input: MacroA purpose=copy/paste range; MacroB purpose=sort data
Output: 25
";

			var prompt = $@"You are an expert evaluating VBA macro logical similarity.
Score based on PURPOSE and RESULT equivalence. Ignore syntax and naming.
Return only a number between 0 and 100.

{fewShot}

Heuristics:
A: {h1s}
B: {h2s}

Macro A:
{TrimForPrompt(raw1, 1000)}

Macro B:
{TrimForPrompt(raw2, 1000)}

Score:";

			var request = new OllamaRequest
			{
				Model = "deepseek-coder:6.7b",
				Prompt = prompt,
				Stream = false,
				Temperature = 0,
				TopP = 1.0,
				Options = new Dictionary<string, object>
				{
					["num_predict"] = 16,
					["num_thread"] = Environment.ProcessorCount,
					["num_ctx"] = 2048,
					["keep_alive"] = "3m"
				}
			};

			var json = JsonSerializer.Serialize(request);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await _httpClient.PostAsync(_ollamaUrl, content);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (response.IsSuccessStatusCode)
			{
				var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
				var result = ollamaResponse?.Response?.Trim();
				var match = Regex.Match(result ?? "", @"(\d+(?:\.\d+)?)");
				if (match.Success && double.TryParse(match.Value, out double percentage))
				{
					return Math.Max(0, Math.Min(100, percentage));
				}
			}
			return 0.0;
		}
		catch (Exception)
		{
			return 0.0;
		}
	}

	private async Task<string> GetDetailedAnalysisAsync(string raw1, string raw2)
	{
		try
		{
			var prompt = $@"Compare these two VBA macros and provide a detailed analysis:

1. Purpose: What does each macro do?
2. Similarities: What do they have in common?
3. Differences: How do they differ?
4. Efficiency: Which approach is better?

Macro 1:
{TrimForPrompt(raw1, 1000)}

Macro 2:
{TrimForPrompt(raw2, 1000)}

Analysis:";

			var request = new OllamaRequest
			{
				Model = "deepseek-coder:6.7b",
				Prompt = prompt,
				Stream = false,
				Temperature = 0.2,
				TopP = 1.0,
				Options = new Dictionary<string, object>
				{
					["num_predict"] = 128,
					["num_thread"] = Environment.ProcessorCount,
					["num_ctx"] = 2048,
					["keep_alive"] = "2m"
				}
			};

			var json = JsonSerializer.Serialize(request);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await _httpClient.PostAsync(_ollamaUrl, content);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (response.IsSuccessStatusCode)
			{
				var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
				return ollamaResponse?.Response?.Trim() ?? "Analiz yapılamadı.";
			}
			return "Analiz yapılamadı.";
		}
		catch (Exception)
		{
			return "Analiz yapılamadı.";
		}
	}

	private static string TrimForPrompt(string s, int maxChars = 1200)
	{
		if (string.IsNullOrEmpty(s)) return string.Empty;
		var t = s.Replace("\r\n", "\n");
		return t.Length <= maxChars ? t : t.Substring(0, maxChars) + "...";
	}
}
