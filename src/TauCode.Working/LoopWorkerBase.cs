﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// todo clean up
namespace TauCode.Working
{
    public abstract class LoopWorkerBase : WorkerBase
    {
        #region Constants

        protected const int ControlSignalIndex = 0;

        #endregion

        #region Nested

        protected enum WorkFinishReason
        {
            GotControlSignal = 1,
            WorkIsDone,
        }

        protected enum VacationFinishReason
        {
            GotControlSignal = 1,
            VacationTimeElapsed,
            NewWorkArrived,
        }

        #endregion

        #region Fields

        private AutoResetEvent _controlSignal;
        private AutoResetEvent _routineSignal;

        private WaitHandle[] _controlSignalWithExtraSignals;

        #endregion

        #region Abstract

        protected abstract Task<WorkFinishReason> DoWorkAsyncImpl();

        protected abstract Task<VacationFinishReason> TakeVacationAsyncImpl();

        protected abstract AutoResetEvent[] GetExtraSignals();

        #endregion

        #region Private

        private async Task Routine()
        {
            string message;

            message = $"'{nameof(Routine)}' started.";
            this.LogDebug(message, 3);
            this.CheckState2(message, WorkerState.Starting);

            message = $"Acknowledging control thread that {nameof(Routine)} is ready to go.";
            this.LogDebug(message, 3);
            WaitHandle.SignalAndWait(_routineSignal, _controlSignal);
            this.CheckState2(message, WorkerState.Running);

            var goOn = true;

            message = $"Entering '{nameof(Routine)}' loop.";
            this.LogDebug(message, 3);


            while (goOn)
            {
                var workFinishReason = await this.DoWorkAsync();
                this.LogDebug($"{nameof(DoWorkAsync)} result: {workFinishReason}.", 3);

                if (workFinishReason == WorkFinishReason.GotControlSignal)
                {
                    goOn = this.ContinueAfterControlSignal(WorkerState.Pausing, WorkerState.Stopping, WorkerState.Disposing);
                }
                else if (workFinishReason == WorkFinishReason.WorkIsDone)
                {
                    var vacationFinishedReason = await this.TakeVacationAsync();
                    this.LogDebug($"{nameof(TakeVacationAsync)} result: {vacationFinishedReason}.", 3);

                    switch (vacationFinishedReason)
                    {
                        case VacationFinishReason.GotControlSignal:
                            goOn = this.ContinueAfterControlSignal(WorkerState.Pausing, WorkerState.Stopping, WorkerState.Disposing);
                            break;

                        case VacationFinishReason.VacationTimeElapsed:
                        case VacationFinishReason.NewWorkArrived:
                            // let's get back to work.
                            break;

                        default:
                            throw this.CreateInternalErrorException(); // should never happen
                    }
                }
                else
                {
                    throw this.CreateInternalErrorException(); // should never happen
                }
            }
        }

        private Task<WorkFinishReason> DoWorkAsync()
        {
            this.LogDebug($"Entered.");
            return this.DoWorkAsyncImpl();
        }

        private Task<VacationFinishReason> TakeVacationAsync()
        {
            this.LogDebug($"Entered.");
            return this.TakeVacationAsyncImpl();
        }

        private void PauseRoutine()
        {
            this.LogDebug($"Entered.");

            while (true)
            {
                var gotControlSignal = _controlSignal.WaitOne(11); // todo
                if (gotControlSignal)
                {
                    this.LogDebug("Got control signal.");
                    return;
                }
            }
        }

        private bool ContinueAfterControlSignal(params WorkerState[] expectedStates)
        {
            var message = "Continuing after control signal.";
            this.LogDebug(message);
            this.CheckState2(message, expectedStates);



            //_routineSignal.S-et();
            //_controlSignal.WaitOne();
            this.LogDebug("Sending signal to control thread and awaiting response signal.");
            WaitHandle.SignalAndWait(_routineSignal, _controlSignal);

            message = "Got response signal from control thread.";
            this.LogDebug(message);
            var stableStates = expectedStates
                .Select(WorkingExtensions.GetStableWorkerState)
                .ToArray();

            this.CheckState2(message, stableStates);

            var state = this.State;

            bool result;

            switch (state)
            {
                case WorkerState.Disposed:
                case WorkerState.Stopped:
                    result = false;
                    break;

                case WorkerState.Paused:
                    this.PauseRoutine();

                    // After exit from 'PauseRoutine()', state cannot be 'Pausing', therefore recursion is never endless.
                    result = ContinueAfterControlSignal(WorkerState.Stopping, WorkerState.Resuming, WorkerState.Disposing);
                    break;

                case WorkerState.Running:
                    result = true;
                    break;

                default:
                    throw this.CreateInternalErrorException(); // should never happen
            }

            return result;
        }

        #endregion

        #region Protected

        protected Task LoopTask { get; private set; } // todo: private?

        protected bool WaitControlSignal(int millisecondsTimeout)
        {
            return _controlSignal.WaitOne(millisecondsTimeout);
        }

        protected int WaitForControlSignalWithExtraSignals(int millisecondsTimeout) =>
            this.WaitForControlSignalWithExtraSignals(TimeSpan.FromMilliseconds(millisecondsTimeout));
        
        protected int WaitForControlSignalWithExtraSignals(TimeSpan timeout) // todo rename
        {
            if (_controlSignalWithExtraSignals == null)
            {
                throw new InvalidOperationException(); // todo
            }

            var index = WaitHandle.WaitAny(_controlSignalWithExtraSignals, timeout);
            return index;
        }

        #endregion

        #region Overridden

        protected override void StartImpl()
        {
            this.ChangeState(WorkerState.Starting);

            _controlSignal = new AutoResetEvent(false);
            _routineSignal = new AutoResetEvent(false);

            var extraSignals = this.GetExtraSignals();
            if (extraSignals == null)
            {
                this.CheckInternalIntegrity(_controlSignalWithExtraSignals == null);
            }
            else
            {
                if (extraSignals.Length == 0)
                {
                    throw new NotImplementedException(); // todo. if you don't need extra signals, return null instead of empty array.
                }

                var distinctExtraSignals = extraSignals.Distinct().ToArray();
                if (extraSignals.Length != distinctExtraSignals.Length)
                {
                    throw new NotImplementedException(); // must be different.
                }

                var list = new List<WaitHandle>();
                list.Add(_controlSignal); // always has index #0
                list.AddRange(distinctExtraSignals);

                _controlSignalWithExtraSignals = list.ToArray();
            }

            this.LoopTask = Task.Factory.StartNew(this.Routine);

            // wait signal from routine that routine has started
            _routineSignal.WaitOne();

            this.ChangeState(WorkerState.Running);

            // inform routine that state has been changed to 'Running' and routine can start actual work
            _controlSignal.Set();
        }

        protected override void PauseImpl()
        {
            this.ChangeState(WorkerState.Pausing);

            WaitHandle.SignalAndWait(_controlSignal, _routineSignal);

            this.ChangeState(WorkerState.Paused);
            _controlSignal.Set();
        }

        protected override void ResumeImpl()
        {   
            this.ChangeState(WorkerState.Resuming);

            WaitHandle.SignalAndWait(_controlSignal, _routineSignal);

            this.ChangeState(WorkerState.Running);
            _controlSignal.Set();
        }

        protected override void StopImpl()
        {
            this.ChangeState(WorkerState.Stopping);

            WaitHandle.SignalAndWait(_controlSignal, _routineSignal);

            this.ChangeState(WorkerState.Stopped);
            _controlSignal.Set();

            this.LogDebug("Waiting task to terminate.");
            this.LoopTask.Wait();
            this.LogDebug("Task terminated.");

            this.LoopTask.Dispose();
            this.LoopTask = null;

            _controlSignal.Dispose();
            _controlSignal = null;

            _routineSignal.Dispose();
            _routineSignal = null;

            this.LogDebug("OS Resources disposed.");
        }

        protected override void DisposeImpl()
        {   
            var previousState = this.State;
            this.ChangeState(WorkerState.Disposing);

            if (previousState == WorkerState.Stopped)
            {
                this.LogDebug("Worker was stopped, nothing to dispose.");
                this.ChangeState(WorkerState.Disposed);
                return;
            }

            this.LogDebug($"Sending signal to {nameof(Routine)}.");
            WaitHandle.SignalAndWait(_controlSignal, _routineSignal);

            this.ChangeState(WorkerState.Disposed);
            _controlSignal.Set();

            this.LogDebug($"Waiting {nameof(Routine)} to terminate.");
            this.LoopTask.Wait();
            this.LogDebug($"{nameof(Routine)} terminated.");

            this.LoopTask.Dispose();
            this.LoopTask = null;

            _controlSignal.Dispose();
            _controlSignal = null;

            _routineSignal.Dispose();
            _routineSignal = null;

            this.LogDebug("OS Resources disposed.");
        }

        #endregion
    }
}
