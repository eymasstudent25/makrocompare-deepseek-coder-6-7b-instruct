using System.Collections.Generic;

namespace MakroCompare1408.Models;

public class CodeComparisonResult
{
    public double SyntaxSimilarity { get; set; }
    public double LogicalSimilarity { get; set; }
    public double OverallSimilarity { get; set; }
    public string DetailedAnalysis { get; set; } = string.Empty;
    public List<string> CommonElements { get; set; } = new();
    public List<string> Differences { get; set; } = new();
}

public class OllamaRequest
{
    public string Model { get; set; } = "deepseek-coder:6.7b";
    public string Prompt { get; set; } = string.Empty;
    public bool Stream { get; set; } = false;
    public double Temperature { get; set; } = 0;
    public double TopP { get; set; } = 1.0;
    public Dictionary<string, object>? Options { get; set; }
}

public class OllamaResponse
{
    public string Model { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public bool Done { get; set; }
    public long TotalDuration { get; set; }
    public long LoadDuration { get; set; }
    public long PromptEvalCount { get; set; }
    public long PromptEvalDuration { get; set; }
    public long EvalCount { get; set; }
    public long EvalDuration { get; set; }
}
