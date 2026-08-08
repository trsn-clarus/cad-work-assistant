using System.Diagnostics;

namespace CADWorkAssistant.Integration.Tests.Fixtures;

/// <summary>
/// Integration Test가 실제 CADWorkAssistant.FakeAutoCad.exe를 별도 프로세스로 띄우고, 끝나면
/// 확실히 정리한다 (Milestone 2 §9 - 사람이 터미널을 따로 열 필요가 없어야 한다, orphan 프로세스 금지).
/// </summary>
internal sealed class FakeAutoCadProcess : IAsyncDisposable
{
    private readonly Process _process;

    private FakeAutoCadProcess(Process process)
    {
        _process = process;
        ProcessId = process.Id;
    }

    public int ProcessId { get; }

    public static async Task<FakeAutoCadProcess> StartAsync(string scenario, CancellationToken cancellationToken)
    {
        var dllPath = typeof(FakeAutoCad.Program).Assembly.Location;

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { dllPath, "--scenario", scenario },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start CADWorkAssistant.FakeAutoCad.");

        try
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"FakeAutoCad (scenario '{scenario}') exited before reporting READY. stderr: {stderr}");
                }

                if (line.StartsWith("READY", StringComparison.Ordinal))
                {
                    break;
                }
            }
        }
        catch
        {
            KillSafely(process);
            throw;
        }

        return new FakeAutoCadProcess(process);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                await _process.StandardInput.WriteLineAsync("quit").ConfigureAwait(false);
                await _process.StandardInput.FlushAsync().ConfigureAwait(false);

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await _process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    KillSafely(_process);
                }
            }
        }
        catch
        {
            KillSafely(_process);
        }
        finally
        {
            _process.Dispose();
        }
    }

    private static void KillSafely(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 이미 죽었거나 접근할 수 없으면 그냥 넘어간다 - orphan만 안 남으면 된다.
        }
    }
}
