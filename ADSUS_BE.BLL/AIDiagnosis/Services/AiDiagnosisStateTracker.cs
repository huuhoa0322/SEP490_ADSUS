using System.Threading;

namespace ADSUS_BE.BLL.AIDiagnosis.Services;

public interface IAiDiagnosisStateTracker
{
    void BeginDiagnosis();
    void EndDiagnosis();
    bool IsAnyDiagnosisInProgress();
}

public class AiDiagnosisStateTracker : IAiDiagnosisStateTracker
{
    private int _activeDiagnoses = 0;

    public void BeginDiagnosis()
    {
        Interlocked.Increment(ref _activeDiagnoses);
    }

    public void EndDiagnosis()
    {
        Interlocked.Decrement(ref _activeDiagnoses);
    }

    public bool IsAnyDiagnosisInProgress()
    {
        return Interlocked.CompareExchange(ref _activeDiagnoses, 0, 0) > 0;
    }
}
