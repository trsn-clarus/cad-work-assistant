using CADWorkAssistant.Core.Cad;
using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Core.Tests.Cad;

public class CadConnectionStateEvaluatorTests
{
    [Theory]
    [InlineData(CadConnectionState.NoAutoCadProcess, ConnectionSignal.ConnectAttemptStarted, CadConnectionState.Connecting)]
    [InlineData(CadConnectionState.Connecting, ConnectionSignal.ConnectSucceeded, CadConnectionState.Connected)]
    [InlineData(CadConnectionState.Connected, ConnectionSignal.HeartbeatSucceeded, CadConnectionState.Connected)]
    [InlineData(CadConnectionState.Connected, ConnectionSignal.ManualDisconnect, CadConnectionState.Disconnected)]
    [InlineData(CadConnectionState.Disconnected, ConnectionSignal.NoAutoCadProcessFound, CadConnectionState.NoAutoCadProcess)]
    [InlineData(CadConnectionState.ProcessDetected, ConnectionSignal.ProcessFoundPluginUnreachable, CadConnectionState.PluginUnavailable)]
    [InlineData(CadConnectionState.NoAutoCadProcess, ConnectionSignal.MultipleInstancesAwaitingSelection, CadConnectionState.ProcessDetected)]
    [InlineData(CadConnectionState.Connected, ConnectionSignal.Faulted, CadConnectionState.Faulted)]
    public void Evaluate_ReturnsExpectedNextState(CadConnectionState current, ConnectionSignal signal, CadConnectionState expected)
    {
        var next = CadConnectionStateEvaluator.Evaluate(current, signal);

        Assert.Equal(expected, next);
    }

    [Fact]
    public void Evaluate_FirstHeartbeatFailureWhileConnected_MovesToReconnectingNotDisconnected()
    {
        var next = CadConnectionStateEvaluator.Evaluate(CadConnectionState.Connected, ConnectionSignal.HeartbeatFailed);

        Assert.Equal(CadConnectionState.Reconnecting, next);
    }

    [Fact]
    public void Evaluate_SecondConsecutiveHeartbeatFailure_MovesToDisconnected()
    {
        var afterFirstFailure = CadConnectionStateEvaluator.Evaluate(CadConnectionState.Connected, ConnectionSignal.HeartbeatFailed);
        var afterSecondFailure = CadConnectionStateEvaluator.Evaluate(afterFirstFailure, ConnectionSignal.HeartbeatFailed);

        Assert.Equal(CadConnectionState.Reconnecting, afterFirstFailure);
        Assert.Equal(CadConnectionState.Disconnected, afterSecondFailure);
    }

    [Fact]
    public void Evaluate_ReconnectSucceeds_ReturnsToConnected()
    {
        var reconnecting = CadConnectionStateEvaluator.Evaluate(CadConnectionState.Connected, ConnectionSignal.HeartbeatFailed);
        var recovered = CadConnectionStateEvaluator.Evaluate(reconnecting, ConnectionSignal.HeartbeatSucceeded);

        Assert.Equal(CadConnectionState.Connected, recovered);
    }
}
