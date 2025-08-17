using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroCompare1408.Models;
using System;
using System.Threading.Tasks;

namespace MakroCompare1408.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CodeComparisonService _comparisonService;

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

    public MainWindowViewModel(CodeComparisonService comparisonService)
    {
        _comparisonService = comparisonService;
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

            ResultText = $"Yazım Benzerliği: %{SyntaxSimilarity:F1}\n" +
                        $"Mantıksal Benzerlik: %{LogicalSimilarity:F1}\n" +
                        $"Genel Benzerlik: %{OverallSimilarity:F1}";
        }
        catch (Exception)
        {
            ResultText = "Karşılaştırma sırasında bir hata oluştu.";
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
