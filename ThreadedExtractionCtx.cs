namespace BrickVault;

public class ThreadedExtractionCtx
{
    public int Extracted = 0;
    public int Total = 0;
    public int TotalThreads = 0;
    public object LockObject = new object();
    public CancellationTokenSource Cancel;

    public delegate void ProgressUpdate();

    public event ProgressUpdate OnProgressChange;

    public void Increment()
    {
        Interlocked.Increment(ref Extracted);
        OnProgressChange?.Invoke();
    }

    public ThreadedExtractionCtx(int totalThreads, int totalFiles, CancellationTokenSource source)
    {
        TotalThreads = totalThreads;
        Total = totalFiles;
        Cancel = source;
    }
}