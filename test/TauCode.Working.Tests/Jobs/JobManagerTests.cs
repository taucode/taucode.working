﻿using NUnit.Framework;
using System;
using TauCode.Infrastructure.Time;
using TauCode.Working.Exceptions;
using TauCode.Working.Jobs;

// todo clean up
namespace TauCode.Working.Tests.Jobs
{
    [TestFixture]
    public class JobManagerTests
    {
        //[Test]
        //public async Task Constructor_NoArguments_RunsSimpleHappyPath()
        //{
        //    // Arrange
        //    IJobManager scheduleManager = new JobManager();
        //    scheduleManager.Start();

        //    var now = TimeProvider.GetCurrent();

        //    var schedule = new SimpleSchedule(
        //        SimpleScheduleKind.Hour,
        //        1,
        //        now,
        //        new List<TimeSpan>()
        //        {
        //            TimeSpan.FromSeconds(2),
        //        });



        //    // Act
        //    scheduleManager.Register(
        //        "my-job",
        //        (parameter, writer, token) => Task.Run(async () =>
        //            {
        //                for (int i = 0; i < 10; i++)
        //                {
        //                    await writer.WriteLineAsync(TimeProvider.GetCurrent().ToString("O", CultureInfo.InvariantCulture));

        //                    var got = token.WaitHandle.WaitOne(100);
        //                    if (got)
        //                    {
        //                        await writer.WriteLineAsync("Got cancel!");
        //                        throw new TaskCanceledException();
        //                    }
        //                }

        //                return true;
        //            },
        //            token),
        //        schedule,
        //        10);

        //    await Task.Delay(2001);
        //    scheduleManager.Cancel("my-job");

        //    // Assert
        //    scheduleManager.Dispose();
        //}

        [SetUp]
        public void SetUp()
        {
            TimeProvider.Reset();
        }

        #region JobManager.ctor

        /// <summary>
        /// ===========
        /// Arrange:
        /// 
        /// ===========
        /// Act: 
        /// 1. Instance of <see cref="JobManager"/> is created.
        ///
        /// ===========
        /// Assert:
        /// 1. Instance is not started.
        /// 2. Instance is not disposed.
        /// 3. GetNames() returns 0 elements.
        /// </summary>
        [Test]
        public void Constructor_NoArguments_CreatesInstance()
        {
            // Arrange

            // Act
            IJobManager jobManager = new JobManager();

            // Assert
            Assert.That(jobManager.IsRunning, Is.False);
            Assert.That(jobManager.IsDisposed, Is.False);

            jobManager.Dispose();
        }


        #endregion

        #region IJobManager.Start

        [Test]
        public void Start_NotStarted_Starts()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();

            // Act
            jobManager.Start();

            // Assert
            Assert.That(jobManager.IsRunning, Is.True);
            Assert.That(jobManager.IsDisposed, Is.False);
            Assert.That(jobManager.GetNames(), Has.Count.Zero);
        }

        [Test]
        public void Start_AlreadyStarted_ThrowsInvalidJobOperationException()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Start();

            // Act
            var ex = Assert.Throws<InvalidJobOperationException>(() => jobManager.Start());

            // Assert
            Assert.That(ex.Message, Is.EqualTo($"'{typeof(IJobManager).FullName}' is already running"));
        }

        [Test]
        public void Start_AlreadyDisposed_ThrowsException()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Dispose();

            // Act
            var ex = Assert.Throws<JobObjectDisposedException>(() => jobManager.Start());

            // Assert
            Assert.That(ex.Message, Is.EqualTo($"'{typeof(IJobManager).FullName}' is disposed."));
            Assert.That(ex.ObjectName, Is.EqualTo(typeof(IJobManager).FullName));
        }

        #endregion

        #region IJobManager.IsRunning

        [Test]
        public void IsRunning_NotStarted_ReturnsFalse()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();

            // Act
            var isRunning = jobManager.IsRunning;

            // Assert
            Assert.That(isRunning, Is.False);
        }

        [Test]
        public void IsRunning_Started_ReturnsTrue()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Start();

            // Act
            var isRunning = jobManager.IsRunning;

            // Assert
            Assert.That(isRunning, Is.True);
        }

        [Test]
        public void IsRunning_NotStartedThenDisposed_ReturnsFalse()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Dispose();

            // Act
            var isRunning = jobManager.IsRunning;

            // Assert
            Assert.That(isRunning, Is.False);
        }

        [Test]
        public void IsRunning_StartedThenDisposed_ReturnsFalse()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Start();
            jobManager.Dispose();

            // Act
            var isRunning = jobManager.IsRunning;

            // Assert
            Assert.That(isRunning, Is.False);
        }

        #endregion

        #region IJobManager.IsDisposed

        [Test]
        public void IsDisposed_NotStarted_ReturnsFalse()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();

            // Act
            var isDisposed = jobManager.IsDisposed;

            // Assert
            Assert.That(isDisposed, Is.False);
        }

        [Test]
        public void IsDisposed_Started_ReturnsFalse()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Start();

            // Act
            var isDisposed = jobManager.IsDisposed;

            // Assert
            Assert.That(isDisposed, Is.False);
        }

        [Test]
        public void IsDisposed_NotStartedThenDisposed_ReturnsTrue()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Dispose();

            // Act
            var isDisposed = jobManager.IsDisposed;

            // Assert
            Assert.That(isDisposed, Is.True);
        }

        [Test]
        public void IsDisposed_StartedThenDisposed_ReturnsTrue()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Start();
            jobManager.Dispose();

            // Act
            var isDisposed = jobManager.IsDisposed;

            // Assert
            Assert.That(isDisposed, Is.True);
        }

        #endregion

        #region IJobManager.Create

        [Test]
        public void Create_NotStarted_ThrowsInvalidJobOperationException()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();

            // Act
            var ex = Assert.Throws<InvalidJobOperationException>(() => jobManager.Create("job1"));

            // Assert
            Assert.That(ex.Message, Is.EqualTo($"'{typeof(IJobManager).FullName}' not started."));
        }

        [Test]
        public void Create_Started_ReturnsJob()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Start();

            // Act
            var job = jobManager.Create("job1");

            // Assert
            Assert.That(job.Name, Is.EqualTo("job1"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Create_BadJobName_ThrowsArgumentException(string badJobName)
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Start();

            // Act
            var ex = Assert.Throws<ArgumentException>(() => jobManager.Create(badJobName));

            // Assert
            Assert.That(ex.Message, Does.StartWith("Job name cannot be null or empty."));
            Assert.That(ex.ParamName, Is.EqualTo("jobName"));
        }

        [Test]
        public void Create_NameAlreadyExists_ThrowsInvalidJobOperationException()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Start();
            var name = "job1";
            jobManager.Create(name);

            // Act
            var ex = Assert.Throws<InvalidJobOperationException>(() => jobManager.Create(name));

            // Assert
            Assert.That(ex.Message, Is.EqualTo($"Job '{name}' already exists."));
        }

        [Test]
        public void Create_Disposed_ThrowsJobObjectIsDisposedException()
        {
            // Arrange
            using IJobManager jobManager = new JobManager();
            jobManager.Dispose();

            // Act
            var ex = Assert.Throws<JobObjectDisposedException>(() => jobManager.Create("job1"));

            // Assert
            Assert.That(ex.Message, Is.EqualTo($"'{typeof(IJobManager).FullName}' is disposed."));
            Assert.That(ex.ObjectName, Is.EqualTo(typeof(IJobManager).FullName));

        }

        #endregion

        [Test]
        public void Create_ValidJobName_CreatesJob()
        {
            // Arrange
            IJobManager jobManager = new JobManager();
            jobManager.Start(); // todo: ut cannot be started twice.

            // Act
            var job = jobManager.Create("my-job");

            // Assert
            var now = TimeProvider.GetCurrent();
            Assert.That(job.Schedule.GetDueTimeAfter(now), Is.EqualTo(JobExtensions.Never));
            Assert.That(job.Routine, Is.Not.Null);
            Assert.That(job.Parameter, Is.Null);
            Assert.That(job.ProgressTracker, Is.Null);
            Assert.That(job.Output, Is.Null);
        }

        [Test]
        public void GetJobNames_NoArguments_ReturnsJobNames()
        {
            // Arrange
            IJobManager jobManager = new JobManager();
            jobManager.Start(); // todo: ut cannot be started twice.
            jobManager.Create("job1");
            jobManager.Create("job2");

            // Act
            var names = jobManager.GetNames();

            // Assert
            CollectionAssert.AreEquivalent(new[] { "job1", "job2" }, names);
        }

        [Test]
        public void Get_ValidName_ReturnsJob()
        {
            // Arrange
            IJobManager jobManager = new JobManager();
            jobManager.Start(); // todo: ut cannot be started twice.
            var job = jobManager.Create("job1");

            // Act
            var gotJob = jobManager.Get("job1");

            // Assert
            Assert.That(job, Is.SameAs(gotJob));
        }



        // todo: IJobManager.GetJobNames
        // - happy path
        // - exception if not started
        // - exception if disposed

        // todo: IJobManager.Get
        // - happy path
        // - exception if not started
        // - exception on bad job name
        // - exception if disposed

        // todo: IJobManager.Dispose
        // - happy path on started (serilog)
        // - happy path on not started (serilog)
        // - exception if called twice

    }
}
