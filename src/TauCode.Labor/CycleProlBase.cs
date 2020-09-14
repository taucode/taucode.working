﻿using System;
using System.Threading;
using System.Threading.Tasks;
using TauCode.Extensions.Lab;

namespace TauCode.Labor
{
    public abstract class CycleProlBase : ProlBase
    {
        #region Constants

        protected readonly TimeSpan VeryLongVacation = TimeSpan.FromMilliseconds(int.MaxValue);
        protected readonly TimeSpan TimeQuantum = TimeSpan.FromMilliseconds(1);

        #endregion

        #region Fields

        private Thread _thread;
        private readonly object _threadLock;

        private long _workState; // increments each time new work arrived, or existing work is completed.

        #endregion

        #region Constructor

        protected CycleProlBase()
        {
            _threadLock = new object();
        }

        #endregion

        #region Abstract

        protected abstract Task<TimeSpan> DoWork(CancellationToken token);

        #endregion

        #region Overridden

        protected override void OnStarting()
        {
            // todo: check '_thread' is null
            _thread = new Thread(CycleRoutine);

            lock (_threadLock)
            {
                _thread.Start();
                Monitor.Wait(_threadLock);
            }
        }

        protected override void OnStopping()
        {
            lock (_threadLock)
            {
                Monitor.Pulse(_threadLock);
            }

            _thread.Join();
            _thread = null;
        }

        #endregion

        #region Protected

        protected void WorkArrived() => this.AdvanceWorkState();

        #endregion

        #region Private

        private void AdvanceWorkState()
        {
            lock (_threadLock)
            {
                _workState++;
                Monitor.Pulse(_threadLock);
            }
        }

        private long GetCurrentWorkState()
        {
            lock (_threadLock)
            {
                return _workState;
            }
        }

        private void CycleRoutine()
        {
            lock (_threadLock)
            {
                Monitor.Pulse(_threadLock);
            }

            var source = new CancellationTokenSource();
            //var taskEndedSignal = new ManualResetEventSlim(true);
            var endTask = Task.CompletedTask;

            while (true)
            {
                var vacation = VeryLongVacation;

                if (endTask.IsCompleted)
                {
                    // can try do some work.
                    var task = this.DoWork(source.Token); // todo: try/catch, not null etc.

                    if (task.IsCompleted)
                    {
                        // todo: log warning if task status is not 'RanToCompletion'
                        var wantedVacation = task.Result;
                        vacation = DateTimeExtensionsLab.MinMax(
                            TimeQuantum,
                            VeryLongVacation,
                            wantedVacation);
                    }
                    else
                    {
                        // task is not ended yet
                        //taskEndedSignal.Reset();
                        endTask = task.ContinueWith(this.EndWork, /*taskEndedSignal,*/ source.Token, source.Token);
                    }
                }

                //if (taskEndedSignal.IsSet)
                //{
                //    // can try do some work.
                //    var task = this.DoWork(source.Token); // todo: try/catch, not null etc.

                //    if (task.IsCompleted)
                //    {
                //        // todo: log warning if task status is not 'RanToCompletion'
                //        var wantedVacation = task.Result;
                //        vacation = DateTimeExtensionsLab.MinMax(
                //            TimeQuantum,
                //            VeryLongVacation,
                //            wantedVacation);
                //    }
                //    else
                //    {
                //        // task is not ended yet
                //        taskEndedSignal.Reset();
                //        task.ContinueWith(this.EndWork, taskEndedSignal, source.Token);
                //    }
                //}

                var workStateBeforeVacation = this.GetCurrentWorkState();

                lock (_threadLock)
                {
                    if (this.State != ProlState.Running)
                    {
                        break;
                    }

                    var workStateRightAfterVacationStarted = this.GetCurrentWorkState();
                    if (workStateBeforeVacation != workStateRightAfterVacationStarted)
                    {
                        // vacation is terminated, let's get back to work :(
                        continue;
                    }

                    Monitor.Wait(_threadLock, vacation);
                }

                if (this.State != ProlState.Running)
                {
                    break;
                }
            }

            source.Cancel();
            endTask.Wait();

            //taskEndedSignal.Wait();

            source.Dispose();
            //taskEndedSignal.Dispose();
        }

        private void EndWork(Task initialTask/*, object taskEndedSignalObject*/, object state)
        {
            var k = 3;

            this.AdvanceWorkState();

            //var taskEndedSignal = (ManualResetEventSlim)taskEndedSignalObject;

            //lock (_threadLock)
            //{
            //    Monitor.Pulse(_threadLock);
            //}

            //taskEndedSignal.Set();
        }

        #endregion
    }
}
