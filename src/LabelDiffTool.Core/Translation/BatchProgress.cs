namespace LabelDiffTool.Core.Translation;

/// <summary>Progress payload reported while a batch translation runs.</summary>
public readonly record struct BatchProgress(int Completed, int Total)
{
    public double Fraction => Total == 0 ? 1d : (double)Completed / Total;
}
