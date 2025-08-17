using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq; // Added for Intersect and Union
using System.Threading; // Added for CancellationTokenSource

namespace MakroCompare1408.Models
{
    public class CodeComparisonService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaUrl = "http://localhost:11434/api/generate";

        public CodeComparisonService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CodeComparisonResult> CompareCodesAsync(string code1, string code2)
        {
            var result = new CodeComparisonResult
            {
                SyntaxSimilarity = CalculateSyntaxSimilarity(code1, code2),
                LogicalSimilarity = await CalculateLogicalSimilarityAsync(code1, code2)
            };

            // Genel skor (Mantıksal %80, Syntax %20) - Mantıksal daha önemli
            result.OverallSimilarity = result.SyntaxSimilarity * 0.2 + result.LogicalSimilarity * 0.8;

            // Kısa açıklama
            result.DetailedAnalysis = await GetSimpleAnalysisAsync(code1, code2);

            return result;
        }

        private double CalculateSyntaxSimilarity(string code1, string code2)
        {
            if (string.IsNullOrWhiteSpace(code1) && string.IsNullOrWhiteSpace(code2)) return 100.0;
            if (string.IsNullOrWhiteSpace(code1) || string.IsNullOrWhiteSpace(code2)) return 0.0;

            var norm1 = NormalizeCode(code1);
            var norm2 = NormalizeCode(code2);

            // Tamamen aynıysa %100
            if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
                return 100.0;

            // Levenshtein distance
            var distance = CalculateLevenshteinDistance(norm1, norm2);
            var maxLength = Math.Max(norm1.Length, norm2.Length);

            // Daha hassas hesaplama
            var similarity = Math.Max(0, 100 - ((double)distance / maxLength * 100));
            
            // Çok kısa kodlar için daha düşük skor
            if (maxLength < 50) similarity *= 0.8;
            
            return similarity;
        }

        private string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;

            // Yorumları kaldır
            code = Regex.Replace(code, @"'.*$", "", RegexOptions.Multiline);
            code = Regex.Replace(code, @"(?i)\bRem\b.*$", "", RegexOptions.Multiline);

            // Satır devamlarını kaldır
            code = Regex.Replace(code, @" _\r?\n", " ");

            // Fazla boşlukları temizle
            code = Regex.Replace(code, @"\s+", " ");

            return code.Trim().ToLowerInvariant();
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
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[s1.Length, s2.Length];
        }

        private async Task<double> CalculateLogicalSimilarityAsync(string code1, string code2)
        {
            try
            {
                // Basit test: Ollama çalışmıyorsa sabit değer döndür
                if (string.IsNullOrWhiteSpace(code1) || string.IsNullOrWhiteSpace(code2))
                    return 0.0;

                // Eğer kodlar tamamen aynıysa %100
                if (string.Equals(code1.Trim(), code2.Trim(), StringComparison.OrdinalIgnoreCase))
                    return 100.0;

                var prompt = $@"Bu iki VBA makrosunun mantıksal benzerliğini değerlendir.

ÖNEMLİ: Sadece AMAÇ ve SONUÇ eşdeğerliğine bak. Değişken isimleri, döngü türleri, formatlama önemli değil.

ÖRNEKLER:
- İki makro da toplama yapıyorsa: 90-100%
- İki makro da sıralama yapıyorsa: 90-100%
- Biri toplama, diğeri sıralama yapıyorsa: 10-30%
- İki makro da aynı işi farklı yöntemle yapıyorsa: 80-95%

Sadece 0-100 arası bir sayı döndür.

Makro 1:
{TrimForPrompt(code1, 1000)}

Makro 2:
{TrimForPrompt(code2, 1000)}

Benzerlik yüzdesi:";

                var request = new OllamaRequest
                {
                    Model = "deepseek-coder:6.7b-instruct",
                    Prompt = prompt,
                    Stream = false,
                    Temperature = 0,
                    TopP = 1.0,
                    Options = new Dictionary<string, object>
                    {
                        ["num_predict"] = 8,
                        ["num_thread"] = Environment.ProcessorCount,
                        ["num_ctx"] = 2048,
                        ["keep_alive"] = "2m"
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Timeout ekle
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.PostAsync(_ollamaUrl, content, cts.Token);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
                    var result = ollamaResponse?.Response?.Trim();
                    var match = Regex.Match(result ?? "", @"(\d+(\.\d+)?)");
                    if (match.Success && double.TryParse(match.Value, out double percentage))
                    {
                        return Math.Clamp(percentage, 0, 100);
                    }
                }

                // Ollama çalışmıyorsa basit heuristik
                return CalculateSimpleLogicalSimilarity(code1, code2);
            }
            catch
            {
                // Hata durumunda basit heuristik
                return CalculateSimpleLogicalSimilarity(code1, code2);
            }
        }

        private double CalculateSimpleLogicalSimilarity(string code1, string code2)
        {
            // Basit heuristik: Anahtar kelimeleri karşılaştır
            var keywords1 = ExtractKeywords(code1);
            var keywords2 = ExtractKeywords(code2);

            if (keywords1.Count == 0 && keywords2.Count == 0) return 50.0;
            if (keywords1.Count == 0 || keywords2.Count == 0) return 25.0;

            var intersection = keywords1.Intersect(keywords2, StringComparer.OrdinalIgnoreCase).Count();
            var union = keywords1.Union(keywords2, StringComparer.OrdinalIgnoreCase).Count();

            var baseScore = union > 0 ? (double)intersection / union * 100.0 : 0.0;

            // Amaç bazlı ek puanlar
            var purposeScore = CalculatePurposeSimilarity(code1, code2);
            
            // Final skor: %70 amaç + %30 anahtar kelime
            return (baseScore * 0.3) + (purposeScore * 0.7);
        }

        private double CalculatePurposeSimilarity(string code1, string code2)
        {
            var purpose1 = DetectPurpose(code1);
            var purpose2 = DetectPurpose(code2);

            if (purpose1 == purpose2 && purpose1 != "unknown") return 95.0;
            if (purpose1 == "unknown" || purpose2 == "unknown") return 50.0;

            // Benzer amaçlar
            if ((purpose1 == "sum" && purpose2 == "sum") ||
                (purpose1 == "average" && purpose2 == "average") ||
                (purpose1 == "copy" && purpose2 == "copy") ||
                (purpose1 == "sort" && purpose2 == "sort") ||
                (purpose1 == "filter" && purpose2 == "filter"))
                return 90.0;

            // Farklı amaçlar
            return 20.0;
        }

        private string DetectPurpose(string code)
        {
            var normalized = NormalizeCode(code);
            
            if (normalized.Contains("sum") || normalized.Contains("toplam"))
                return "sum";
            if (normalized.Contains("average") || normalized.Contains("ortalama"))
                return "average";
            if (normalized.Contains("copy") || normalized.Contains("kopyala"))
                return "copy";
            if (normalized.Contains("sort") || normalized.Contains("sırala"))
                return "sort";
            if (normalized.Contains("filter") || normalized.Contains("filtrele"))
                return "filter";
            if (normalized.Contains("find") || normalized.Contains("bul"))
                return "find";
            if (normalized.Contains("replace") || normalized.Contains("değiştir"))
                return "replace";

            return "unknown";
        }

        private HashSet<string> ExtractKeywords(string code)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalized = NormalizeCode(code);

            // VBA anahtar kelimeleri
            var vbaKeywords = new[] { "sub", "function", "dim", "for", "next", "if", "then", "else", "do", "while", "loop", "select", "case", "end", "with", "worksheets", "range", "cells", "copy", "paste", "sum", "average", "count", "find", "replace", "sort", "filter" };

            foreach (var keyword in vbaKeywords)
            {
                if (normalized.Contains(keyword))
                    keywords.Add(keyword);
            }

            return keywords;
        }

        private async Task<string> GetSimpleAnalysisAsync(string code1, string code2)
        {
            try
            {
                var prompt = $@"Bu iki VBA makrosunu kısaca analiz et:

1. Her makro ne yapar? (1-2 cümle)
2. Benzerlikler neler? (1 cümle)
3. Farklar neler? (1 cümle)

Makro 1:
{TrimForPrompt(code1, 800)}

Makro 2:
{TrimForPrompt(code2, 800)}

Analiz:";

                var request = new OllamaRequest
                {
                    Model = "deepseek-coder:6.7b",
                    Prompt = prompt,
                    Stream = false,
                    Temperature = 0.2,
                    TopP = 1.0,
                    Options = new Dictionary<string, object>
                    {
                        ["num_predict"] = 64,
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
            catch
            {
                return "Analiz yapılamadı.";
            }
        }

        private static string TrimForPrompt(string s, int maxChars = 800)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var t = s.Replace("\r\n", "\n");
            return t.Length <= maxChars ? t : t.Substring(0, maxChars) + "...";
        }
    }
}
