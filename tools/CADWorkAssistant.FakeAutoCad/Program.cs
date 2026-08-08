using System;
using System.Threading;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.FakeAutoCad.Handlers;
using CADWorkAssistant.FakeAutoCad.Scenarios;
using CADWorkAssistant.Infrastructure.Ipc;
using Serilog;

namespace CADWorkAssistant.FakeAutoCad;

/// <summary>
/// Headless AutoCAD Simulation Host. 실제 AutoCAD Plugin과 동일한 Named Pipe Protocol로 응답한다.
/// `dotnet CADWorkAssistant.FakeAutoCad.dll --scenario NormalSelection` 형태로 실행한다.
/// stdin에 아무 줄이나 들어오거나(Integration Test가 Process를 종료시킬 때) Ctrl+C를 누르면 종료한다.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        var scenarioName = ParseScenarioArg(args) ?? ScenarioCatalog.DefaultScenarioName;
        if (!ScenarioCatalog.TryGet(scenarioName, out var scenario))
        {
            Console.Error.WriteLine(
                $"Unknown scenario '{scenarioName}'. Available: {string.Join(", ", ScenarioCatalog.Names)}");
            return 1;
        }

        var processId = Environment.ProcessId;
        var handlers = new IIpcRequestHandler[]
        {
            new FakePingHandler(),
            new FakeGetApplicationInfoHandler(processId, scenario),
            new FakeGetDrawingContextHandler(scenario),
            new FakeSelectLengthObjectsHandler(scenario)
        };

        var server = new AutoCadPipeServer(new IpcRequestDispatcher(handlers), processId);
        server.Start();

        // Integration Test 및 개발자가 "준비됐다"를 파싱할 수 있는 고정 형식.
        Console.WriteLine($"READY pid={processId} scenario={scenario.Name}");
        Console.Out.Flush();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        _ = Task.Run(() =>
        {
            var line = Console.In.ReadLine();
            // 일부 프로세스 실행 방식(특히 PowerShell의 StandardInput)은 첫 줄 앞에 UTF-8 BOM을
            // 끼워 보낸다 - 실제로 겪었다. 방어적으로 벗겨내고 비교한다.
            const char byteOrderMark = (char)0xFEFF;
            var normalized = line?.TrimStart(byteOrderMark).Trim();
            if (normalized is null or "quit")
            {
                cts.Cancel();
            }
        });

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await server.StopAsync().ConfigureAwait(false);
        return 0;
    }

    private static string? ParseScenarioArg(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--scenario" or "-s")
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
