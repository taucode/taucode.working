﻿using System;
using System.IO;
using TauCode.Working.Exceptions;


namespace TauCode.Working.Jobs.Instruments
{
    internal class JobPropertiesHolder : IDisposable
    {
        #region Fields

        private readonly string _jobName;

        private JobDelegate _routine;
        private object _parameter;
        private IProgressTracker _progressTracker;
        private TextWriter _output;

        private bool _isDisposed;

        private readonly object _lock;

        private readonly ObjectLogger _logger;

        #endregion

        #region Constructor

        internal JobPropertiesHolder(string jobName)
        {
            _jobName = jobName;
            _routine = JobExtensions.IdleJobRoutine;
            _lock = new object();

            _logger = new ObjectLogger(this, jobName);
        }

        #endregion

        #region Private

        private void CheckNotDisposed()
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    throw new JobObjectDisposedException(_jobName);
                }
            }
        }

        #endregion

        #region Internal

        internal JobDelegate Routine
        {
            get
            {
                lock (_lock)
                {
                    return _routine;
                }
            }
            set
            {
                lock (_lock)
                {
                    this.CheckNotDisposed();
                    _routine = value ?? throw new ArgumentNullException(nameof(IJob.Routine));
                }
            }
        }

        internal object Parameter
        {
            get
            {
                lock (_lock)
                {
                    return _parameter;
                }
            }
            set
            {
                lock (_lock)
                {
                    this.CheckNotDisposed();
                    _parameter = value;
                }
            }
        }

        internal IProgressTracker ProgressTracker
        {
            get
            {
                lock (_lock)
                {
                    return _progressTracker;
                }
            }
            set
            {
                lock (_lock)
                {
                    this.CheckNotDisposed();
                    _progressTracker = value;
                }
            }
        }

        internal TextWriter Output
        {
            get
            {
                lock (_lock)
                {
                    return _output;
                }
            }
            set
            {
                lock (_lock)
                {
                    this.CheckNotDisposed();
                    _output = value;
                }
            }
        }

        internal JobProperties ToJobProperties()
        {
            lock (_lock)
            {
                return new JobProperties(
                    _routine,
                    _parameter,
                    _progressTracker,
                    _output);
            }
        }

        internal void EnableLogging(bool enable)
        {
            _logger.IsEnabled = enable;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
            }
        }

        #endregion
    }
}
