using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroCompare1408.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MakroCompare1408.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CodeComparisonService _comparisonService;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private string _code1 = string.Empty;

    [ObservableProperty]
    private string _code2 = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private string _detailedAnalysis = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool _isProcessing = false;

    public bool CanInteract => !IsProcessing;

    [ObservableProperty]
    private double _syntaxSimilarity = 0;

    [ObservableProperty]
    private double _logicalSimilarity = 0;

    [ObservableProperty]
    private double _overallSimilarity = 0;

    public MainWindowViewModel(CodeComparisonService comparisonService, ILogger<MainWindowViewModel> logger)
    {
        _comparisonService = comparisonService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task CompareCodesAsync()
    {
        if (string.IsNullOrWhiteSpace(Code1) || string.IsNullOrWhiteSpace(Code2))
        {
            ResultText = "Lütfen her iki kod alanını da doldurun.";
            return;
        }

        try
        {
            IsProcessing = true;
            ResultText = "Kodlar karşılaştırılıyor...";

            var result = await _comparisonService.CompareCodesAsync(Code1, Code2);

            SyntaxSimilarity = result.SyntaxSimilarity;
            LogicalSimilarity = result.LogicalSimilarity;
            OverallSimilarity = result.OverallSimilarity;
            DetailedAnalysis = result.DetailedAnalysis;

            ResultText = $"Yazım (Syntax) Benzerliği: %{SyntaxSimilarity:F1}\n" +
                        $"Mantıksal Benzerlik: %{LogicalSimilarity:F1}\n" +
                        $"Genel Benzerlik: %{OverallSimilarity:F1}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kod karşılaştırma sırasında hata oluştu");
            ResultText = "Karşılaştırma sırasında bir hata oluştu. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task ExplainCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(Code1) && string.IsNullOrWhiteSpace(Code2))
        {
            ResultText = "Lütfen en az bir kod alanını doldurun.";
            return;
        }

        try
        {
            IsProcessing = true;
            ResultText = "Kod analiz ediliyor...";

            var codeToAnalyze = !string.IsNullOrWhiteSpace(Code1) ? Code1 : Code2;
            var result = await _comparisonService.CompareCodesAsync(codeToAnalyze, codeToAnalyze);

            DetailedAnalysis = result.DetailedAnalysis;
            ResultText = "Kod analizi tamamlandı. Detaylı açıklama aşağıda görüntüleniyor.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kod analizi sırasında hata oluştu");
            ResultText = "Analiz sırasında bir hata oluştu. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Code1 = string.Empty;
        Code2 = string.Empty;
        ResultText = string.Empty;
        DetailedAnalysis = string.Empty;
        SyntaxSimilarity = 0;
        LogicalSimilarity = 0;
        OverallSimilarity = 0;
    }
}
