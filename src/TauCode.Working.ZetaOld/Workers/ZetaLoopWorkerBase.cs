﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TauCode.Working.ZetaOld.Workers
{
    public abstract class ZetaLoopWorkerBase : ZetaWorkerBase
    {
        #region Constants

        protected const int ControlSignalIndex = 0;
        private const int PauseTimeoutMilliseconds = 10;

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

        private Task _routineTask;

        private AutoResetEvent _controlSignal;
        private AutoResetEvent _routineSignal;

        private WaitHandle[] _controlSignalWithExtraSignals;

        #endregion

        #region Abstract

        protected abstract Task<WorkFinishReason> DoWorkAsyncImpl(); // todo: cancellation token.

        protected abstract Task<VacationFinishReason> TakeVacationAsyncImpl();

        protected abstract IList<AutoResetEvent> CreateExtraSignals();

        #endregion

        #region Private

        private async Task LoopRoutine()
        {
            string message;

            message = $"'{nameof(LoopRoutine)}' started.";
            //this.LogDebug(message, 3);
            this.GetLogger().Debug(message, nameof(LoopRoutine));

            this.CheckState(message, ZetaWorkerState.Starting);

            message = $"Acknowledging control thread that {nameof(LoopRoutine)} is ready to go.";
            //this.LogDebug(message, 3);
            this.GetLogger().Debug(message, nameof(LoopRoutine));

            WaitHandle.SignalAndWait(_routineSignal, _controlSignal);
            this.CheckState(message, ZetaWorkerState.Running);

            var goOn = true;

            message = $"Entering '{nameof(LoopRoutine)}' loop.";
            //this.LogDebug(message, 3);
            this.GetLogger().Debug(message, nameof(LoopRoutine));

            
            
            while (goOn)
            {
                var workFinishReason = await this.DoWorkAsync();
                message = $"{nameof(DoWorkAsync)} result: {workFinishReason}.";
                //this.LogDebug(, 3);
                this.GetLogger().Debug(message, nameof(LoopRoutine));

                if (workFinishReason == WorkFinishReason.GotControlSignal)
                {
                    goOn = this.ContinueAfterControlSignal(ZetaWorkerState.Pausing, ZetaWorkerState.Stopping, ZetaWorkerState.Disposing);
                }
                else if (workFinishReason == WorkFinishReason.WorkIsDone)
                {
                    var vacationFinishedReason = await this.TakeVacationAsync();
                    message = $"{nameof(TakeVacationAsync)} result: {vacationFinishedReason}.";
                    //this.LogDebug(, 3);
                    this.GetLogger().Debug(message, nameof(LoopRoutine));

                    switch (vacationFinishedReason)
                    {
                        case VacationFinishReason.GotControlSignal:
                            goOn = this.ContinueAfterControlSignal(ZetaWorkerState.Pausing, ZetaWorkerState.Stopping, ZetaWorkerState.Disposing);
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
            var message = "Entered.";
            //this.LogDebug($"Entered.");
            this.GetLogger().Debug(message, nameof(DoWorkAsync));

            return this.DoWorkAsyncImpl();
        }

        private Task<VacationFinishReason> TakeVacationAsync()
        {
            var message = "Entered.";
            //this.LogDebug($"Entered.");
            this.GetLogger().Debug(message, nameof(TakeVacationAsync));

            return this.TakeVacationAsyncImpl();
        }

        private void PauseRoutine()
        {
            var message = "Entered.";
            //this.LogDebug($"Entered.");
            this.GetLogger().Debug(message, nameof(PauseRoutine));

            while (true)
            {
                var gotControlSignal = _controlSignal.WaitOne(PauseTimeoutMilliseconds);
                if (gotControlSignal)
                {
                    message = "Got control signal.";
                    //this.LogDebug("Got control signal.");
                    this.GetLogger().Debug(message, nameof(PauseRoutine));

                    return;
                }
            }
        }

        private bool ContinueAfterControlSignal(params ZetaWorkerState[] expectedStates)
        {
            var message = "Continuing after control signal.";
            //this.LogDebug(message);
            this.GetLogger().Debug(message, nameof(ContinueAfterControlSignal));


            this.CheckState(message, expectedStates);

            message = "Sending signal to control thread and awaiting response signal.";
            //this.LogDebug("Sending signal to control thread and awaiting response signal.");
            this.GetLogger().Debug(message, nameof(ContinueAfterControlSignal));


            WaitHandle.SignalAndWait(_routineSignal, _controlSignal);

            message = "Got response signal from control thread.";
            //this.LogDebug(message);
            this.GetLogger().Debug(message, nameof(ContinueAfterControlSignal));

            var stableStates = expectedStates
                .Select(ZetaWorkingExtensions.GetStableWorkerState)
                .ToArray();

            this.CheckState(message, stableStates);

            var state = this.State;

            bool result;

            switch (state)
            {
                case ZetaWorkerState.Disposed:
                case ZetaWorkerState.Stopped:
                    result = false;
                    break;

                case ZetaWorkerState.Paused:
                    this.PauseRoutine();

                    // After exit from 'PauseRoutine()', state cannot be 'Pausing', therefore recursion is never endless.
                    result = ContinueAfterControlSignal(ZetaWorkerState.Stopping, ZetaWorkerState.Resuming, ZetaWorkerState.Disposing);
                    break;

                case ZetaWorkerState.Running:
                    result = true;
                    break;

                default:
                    throw this.CreateInternalErrorException(); // should never happen
            }

            return result;
        }

        #endregion

        #region Protected

        protected int WaitForControlSignalWithExtraSignals(int millisecondsTimeout) =>
            this.WaitForControlSignalWithExtraSignals(TimeSpan.FromMilliseconds(millisecondsTimeout));

        protected int WaitForControlSignalWithExtraSignals(TimeSpan timeout)
        {
            var index = WaitHandle.WaitAny(_controlSignalWithExtraSignals, timeout);
            return index;
        }

        protected virtual void Shutdown(ZetaWorkerState shutdownState)
        {
            var message = $"Sending signal to {nameof(LoopRoutine)}.";
            //this.LogDebug($"Sending signal to {nameof(LoopRoutine)}.");
            this.GetLogger().Debug(message, nameof(Shutdown));

            // todo0: here hangs unit tests.
            WaitHandle.SignalAndWait(_controlSignal, _routineSignal);

            this.ChangeState(shutdownState);
            _controlSignal.Set();

            message = $"Waiting {nameof(LoopRoutine)} to terminate.";
            //this.LogDebug($"Waiting {nameof(LoopRoutine)} to terminate.");
            this.GetLogger().Debug(message, nameof(Shutdown));

            this._routineTask.Wait();

            message = $"{nameof(LoopRoutine)} terminated.";
            //this.LogDebug($"{nameof(LoopRoutine)} terminated.");
            this.GetLogger().Debug(message, nameof(Shutdown));

            this._routineTask.Dispose();
            this._routineTask = null;

            foreach (var signal in _controlSignalWithExtraSignals)
            {
                signal.Dispose();
            }

            _controlSignalWithExtraSignals = null;

            _controlSignal = null;

            _routineSignal.Dispose();
            _routineSignal = null;

            message = "OS Resources disposed.";
            //this.LogDebug("OS Resources disposed.");
            this.GetLogger().Debug(message, nameof(Shutdown));
        }

        #endregion

        #region Overridden

        protected override void StartImpl()
        {
            this.ChangeState(ZetaWorkerState.Starting);

            _controlSignal = new AutoResetEvent(false);
            _routineSignal = new AutoResetEvent(false);

            var controlSignalWithExtraSignalsList = new List<AutoResetEvent>
            {
                _controlSignal, // always has index #0
            };

            var extraSignals = this.CreateExtraSignals();

            var extraSignalsOk =
                extraSignals != null &&
                extraSignals.Distinct().Count() == extraSignals.Count &&
                extraSignals.All(x => x != null);

            if (!extraSignalsOk)
            {
                throw new InvalidOperationException($"'{nameof(CreateExtraSignals)}' must return non-null list with unique non-null elements.");
            }
            
            controlSignalWithExtraSignalsList.AddRange(extraSignals);
            _controlSignalWithExtraSignals = controlSignalWithExtraSignalsList
                .Cast<WaitHandle>()
                .ToArray();


            this._routineTask = Task.Factory.StartNew(this.LoopRoutine);

            // wait signal from routine that routine has started
            _routineSignal.WaitOne();

            this.ChangeState(ZetaWorkerState.Running);

            // inform routine that state has been changed to 'Running' and routine can start actual work
            _controlSignal.Set();
        }

        protected override void PauseImpl()
        {
            this.ChangeState(ZetaWorkerState.Pausing);

            WaitHandle.SignalAndWait(_controlSignal, _routineSignal);

            this.ChangeState(ZetaWorkerState.Paused);
            _controlSignal.Set();
        }

        protected override void ResumeImpl()
        {
            this.ChangeState(ZetaWorkerState.Resuming);

            WaitHandle.SignalAndWait(_controlSignal, _routineSignal);

            this.ChangeState(ZetaWorkerState.Running);
            _controlSignal.Set();
        }

        protected override void StopImpl()
        {
            this.ChangeState(ZetaWorkerState.Stopping);

            this.Shutdown(ZetaWorkerState.Stopped);
        }

        protected override void DisposeImpl()
        {
            var previousState = this.State;
            this.ChangeState(ZetaWorkerState.Disposing);

            if (previousState == ZetaWorkerState.Stopped)
            {
                var message = "Worker was stopped, nothing to dispose.";
                //this.LogDebug("Worker was stopped, nothing to dispose.");
                this.GetLogger().Debug(message, nameof(DisposeImpl));


                this.ChangeState(ZetaWorkerState.Disposed);
                return;
            }

            this.Shutdown(ZetaWorkerState.Disposed);
        }

        #endregion
    }
}
