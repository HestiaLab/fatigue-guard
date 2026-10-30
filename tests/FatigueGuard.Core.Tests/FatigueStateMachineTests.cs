using FatigueGuard.Core;
using FatigueGuard.Core.Analysis;
using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Tests;

public sealed class FatigueStateMachineTests
{
    private static FatigueStateMachine CreateMachine(double criticalRepeatSeconds = 15)
    {
        var config = new FatigueThresholds { CriticalRepeatSeconds = criticalRepeatSeconds };
        var machine = new FatigueStateMachine(config);
        machine.SetCalibrated();
        return machine;
    }

    private static FatigueMetrics Metrics(
        double? perclos = 0.05,
        double? blinkRate = 10,
        int longWindow = 0,
        int longLookback = 0,
        int microTrigger = 0,
        int microLong = 0,
        double validSeconds = 60,
        bool sufficient = true)
    {
        return new FatigueMetrics(
            perclos,
            blinkRate,
            validSeconds,
            sufficient,
            0,
            longWindow,
            longLookback,
            microTrigger,
            microLong,
            1.0);
    }

    private static TimeSpan T(double seconds) => TimeSpan.FromSeconds(seconds);

    private static List<FatigueEvent> EvaluateRange(
        FatigueStateMachine machine,
        FatigueMetrics metrics,
        double fromSeconds,
        double toSeconds,
        Func<double, bool>? qualityLow = null,
        Func<double, bool>? noFace = null)
    {
        var events = new List<FatigueEvent>();
        for (double t = fromSeconds; t <= toSeconds + 1e-9; t += 0.5)
        {
            FatigueEvent? e = machine.Evaluate(
                T(t),
                metrics,
                qualityLow?.Invoke(t) ?? false,
                noFace?.Invoke(t) ?? false);
            if (e is not null)
            {
                events.Add(e);
            }
        }

        return events;
    }

    [Fact]
    public void OkToWarning_PerclosDwell()
    {
        FatigueStateMachine machine = CreateMachine();

        List<FatigueEvent> events = EvaluateRange(machine, Metrics(perclos: 0.17), 0, 5);

        Assert.Single(events);
        Assert.Equal(FatigueEventKind.WarningEntered, events[0].Kind);
        Assert.Equal(FatigueTrigger.Perclos, events[0].Trigger);
        Assert.Equal(FatigueDisplayState.Warning, machine.State);
    }

    [Fact]
    public void OkToWarning_BlinkRateDwell()
    {
        FatigueStateMachine machine = CreateMachine();

        List<FatigueEvent> events = EvaluateRange(
            machine,
            Metrics(perclos: 0.05, blinkRate: 32),
            0,
            5);

        Assert.Single(events);
        Assert.Equal(FatigueTrigger.BlinkRate, events[0].Trigger);
    }

    [Fact]
    public void OkToWarning_LongClosuresDwell()
    {
        FatigueStateMachine machine = CreateMachine();

        List<FatigueEvent> events = EvaluateRange(
            machine,
            Metrics(perclos: 0.05, blinkRate: 10, longWindow: 3, longLookback: 3),
            0,
            5);

        Assert.Single(events);
        Assert.Equal(FatigueTrigger.LongClosures, events[0].Trigger);
        Assert.Equal(3, events[0].LongClosures);
    }

    [Fact]
    public void ConditionMustBeSustainedForFullDwell()
    {
        FatigueStateMachine machine = CreateMachine();
        EvaluateRange(machine, Metrics(perclos: 0.17), 0, 4.5);

        Assert.Equal(FatigueDisplayState.Ok, machine.State);

        List<FatigueEvent> events = EvaluateRange(machine, Metrics(perclos: 0.17), 5, 5);

        Assert.Single(events);
    }

    [Fact]
    public void ResetOfDwell_WhenConditionClears()
    {
        FatigueStateMachine machine = CreateMachine();
        EvaluateRange(machine, Metrics(perclos: 0.17), 0, 4.5);
        EvaluateRange(machine, Metrics(perclos: 0.05), 5, 5.5);

        List<FatigueEvent> events = EvaluateRange(machine, Metrics(perclos: 0.17), 6, 10.5);

        Assert.Empty(events);
        Assert.Equal(FatigueDisplayState.Ok, machine.State);
    }

    [Fact]
    public void WarningToCritical_MicrosleepIsImmediate()
    {
        FatigueStateMachine machine = CreateMachine();
        EvaluateRange(machine, Metrics(perclos: 0.17), 0, 5);
        Assert.Equal(FatigueDisplayState.Warning, machine.State);

        FatigueEvent? e = machine.ForceMicrosleep(T(20), 1.3, qualityLow: false, noFace: false);

        Assert.NotNull(e);
        Assert.Equal(FatigueEventKind.CriticalEntered, e!.Kind);
        Assert.Equal(FatigueTrigger.Microsleep, e.Trigger);
        Assert.Equal(1.3, e.MicrosleepSeconds!.Value, 6);
        Assert.Equal(FatigueDisplayState.Critical, machine.State);
    }

    [Fact]
    public void OkToCritical_MicrosleepIsImmediate()
    {
        FatigueStateMachine machine = CreateMachine();

        FatigueEvent? e = machine.ForceMicrosleep(T(1), 1.25, qualityLow: false, noFace: false);

        Assert.NotNull(e);
        Assert.Equal(FatigueDisplayState.Critical, machine.State);
    }

    [Fact]
    public void WarningToCritical_PerclosDwell()
    {
        FatigueStateMachine machine = CreateMachine();
        EvaluateRange(machine, Metrics(perclos: 0.17), 0, 5);

        var events = new List<FatigueEvent>();
        for (double t = 5.5; t <= 10.5; t += 0.5)
        {
            FatigueEvent? e = machine.Evaluate(T(t), Metrics(perclos: 0.27), false, false);
            if (e is not null)
            {
                events.Add(e);
            }
        }

        Assert.Single(events);
        Assert.Equal(FatigueEventKind.CriticalEntered, events[0].Kind);
        Assert.Equal(FatigueTrigger.Perclos, events[0].Trigger);
    }

    [Fact]
    public void WarningToOk_RequiresAllExitConditionsFor15s()
    {
        FatigueStateMachine machine = CreateMachine();
        EvaluateRange(machine, Metrics(perclos: 0.17), 0, 5);

        List<FatigueEvent> early = EvaluateRange(
            machine,
            Metrics(perclos: 0.05, blinkRate: 10, longLookback: 0),
            6,
            20.5);
        Assert.Empty(early);
        Assert.Equal(FatigueDisplayState.Warning, machine.State);

        List<FatigueEvent> events = EvaluateRange(
            machine,
            Metrics(perclos: 0.05, blinkRate: 10, longLookback: 0),
            21,
            21);
        Assert.Single(events);
        Assert.Equal(FatigueEventKind.Recovery, events[0].Kind);
        Assert.Equal(FatigueDisplayState.Ok, machine.State);
    }

    [Fact]
    public void WarningToOk_BlockedByLongClosureInLookback()
    {
        FatigueStateMachine machine = CreateMachine();
        EvaluateRange(machine, Metrics(perclos: 0.17), 0, 5);

        List<FatigueEvent> events = EvaluateRange(
            machine,
            Metrics(perclos: 0.05, blinkRate: 10, longLookback: 1),
            6,
            30);

        Assert.Empty(events);
        Assert.Equal(FatigueDisplayState.Warning, machine.State);
    }

    [Fact]
    public void CriticalToWarning_Requires15sWithoutMicrosleep()
    {
        FatigueStateMachine machine = CreateMachine(criticalRepeatSeconds: 1000);
        machine.ForceMicrosleep(T(1), 1.3, qualityLow: false, noFace: false);

        List<FatigueEvent> early = EvaluateRange(
            machine,
            Metrics(perclos: 0.10, microTrigger: 0, microLong: 0),
            2,
            15.5);
        Assert.Empty(early);

        List<FatigueEvent> events = EvaluateRange(
            machine,
            Metrics(perclos: 0.10, microTrigger: 0, microLong: 0),
            17,
            17);
        Assert.Single(events);
        Assert.Equal(FatigueEventKind.WarningDowngraded, events[0].Kind);
        Assert.Equal(FatigueDisplayState.Warning, machine.State);
    }

    [Fact]
    public void CriticalToWarning_BlockedByMicrosleepInLongLookback()
    {
        FatigueStateMachine machine = CreateMachine(criticalRepeatSeconds: 1000);
        machine.ForceMicrosleep(T(1), 1.3, qualityLow: false, noFace: false);

        List<FatigueEvent> events = EvaluateRange(
            machine,
            Metrics(perclos: 0.10, microTrigger: 0, microLong: 1),
            2,
            30);

        Assert.Empty(events);
        Assert.Equal(FatigueDisplayState.Critical, machine.State);
    }

    [Fact]
    public void Critical_RepeatsReminderEvery15Seconds()
    {
        FatigueStateMachine machine = CreateMachine();
        machine.ForceMicrosleep(T(0), 1.3, qualityLow: false, noFace: false);

        var reminders = new List<FatigueEvent>();
        for (double t = 0.5; t <= 45.5; t += 0.5)
        {
            FatigueEvent? e = machine.Evaluate(T(t), Metrics(perclos: 0.30, microLong: 1), false, false);
            if (e is not null)
            {
                reminders.Add(e);
            }
        }

        Assert.Equal(3, reminders.Count);
        Assert.All(reminders, r => Assert.Equal(FatigueEventKind.CriticalReminder, r.Kind));
        Assert.Equal(15.0, reminders[0].Time.TotalSeconds, 3);
        Assert.Equal(30.0, reminders[1].Time.TotalSeconds, 3);
        Assert.Equal(45.0, reminders[2].Time.TotalSeconds, 3);
    }

    [Fact]
    public void QualityLow_PausesUpgradeButAllowsDowngrade()
    {
        FatigueStateMachine machine = CreateMachine();

        List<FatigueEvent> paused = EvaluateRange(
            machine,
            Metrics(perclos: 0.30),
            0,
            10,
            qualityLow: _ => true);
        Assert.Empty(paused);
        Assert.Equal(FatigueDisplayState.Ok, machine.State);

        List<FatigueEvent> events = EvaluateRange(machine, Metrics(perclos: 0.30), 10.5, 15.5);
        Assert.Single(events);
        Assert.Equal(FatigueEventKind.WarningEntered, events[0].Kind);
    }

    [Fact]
    public void NoFace_PausesTransitions()
    {
        FatigueStateMachine machine = CreateMachine();

        List<FatigueEvent> events = EvaluateRange(
            machine,
            Metrics(perclos: 0.30),
            0,
            20,
            noFace: _ => true);

        Assert.Empty(events);
        Assert.Equal(FatigueDisplayState.Ok, machine.State);
    }

    [Fact]
    public void InsufficientData_BlocksAlarms()
    {
        FatigueStateMachine machine = CreateMachine();

        List<FatigueEvent> events = EvaluateRange(
            machine,
            Metrics(perclos: 0.30, sufficient: false),
            0,
            20);

        Assert.Empty(events);
        Assert.Equal(FatigueDisplayState.Ok, machine.State);
    }

    [Fact]
    public void MicrosleepLookback_UpgradesAfterQualityRecovers()
    {
        FatigueStateMachine machine = CreateMachine();
        EvaluateRange(machine, Metrics(perclos: 0.17), 0, 5);
        Assert.Equal(FatigueDisplayState.Warning, machine.State);

        FatigueEvent? paused = machine.Evaluate(T(6), Metrics(perclos: 0.17, microTrigger: 1), true, false);
        Assert.Null(paused);

        FatigueEvent? resumed = machine.Evaluate(T(7), Metrics(perclos: 0.17, microTrigger: 1), false, false);
        Assert.NotNull(resumed);
        Assert.Equal(FatigueEventKind.CriticalEntered, resumed!.Kind);
        Assert.Equal(FatigueTrigger.Microsleep, resumed.Trigger);
    }
}
