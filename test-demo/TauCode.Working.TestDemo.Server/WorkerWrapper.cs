﻿using EasyNetQ;
using System;
using System.Reflection;
using System.Text;
using TauCode.Working.TestDemo.Common;

// todo clean up
namespace TauCode.Working.TestDemo.Server
{
    public class WorkerWrapper
    {
        private readonly IWorker _worker;
        private readonly string _connectionString;

        public WorkerWrapper(IWorker worker, string connectionString)
        {
            _worker = worker;
            _connectionString = connectionString;
        }

        public void Run()
        {
            var bus = RabbitHutch.CreateBus(_connectionString);

            var rpcHandle1 = bus.Respond<WorkerCommandRequest, WorkerCommandResponse>(
                this.ProcessMethodInvocation,
                configuration => configuration.WithQueueName(_worker.Name));

            _worker.WaitForStateChange(System.Threading.Timeout.Infinite, WorkerState.Disposed);

            rpcHandle1.Dispose();

            bus.Dispose();
        }

        private WorkerCommandResponse ProcessMethodInvocation(WorkerCommandRequest request)
        {
            try
            {
                var result = this.ExecuteCommand(request.Command);
                var response = new WorkerCommandResponse
                {
                    Result = result,
                };

                return response;

                //var method = _worker.GetType().GetMethod(request.MethodName);
                //if (method == null)
                //{
                //    throw new Exception($"Method '{request.MethodName}' not found.");
                //}

                //var parameters = BuildParameters(method, request.Arguments);
                //var result = method.Invoke(_worker, parameters);
                //var resultString = GetResultString(method, result);

                //var response = new InvokeMethodResponse
                //{
                //    Result = resultString,
                //};

                //return response;
            }
            //catch (TargetInvocationException ex)
            //{
            //    var errorResponse = new InvokeMethodResponse
            //    {
            //        Exception = ExceptionInfo.FromException(ex.InnerException),
            //    };

            //    return errorResponse;
            //}
            catch (Exception ex)
            {
                var errorResponse = new WorkerCommandResponse
                {
                    Exception = ExceptionInfo.FromException(ex),
                };

                return errorResponse;
            }
        }

        private string ExecuteCommand(WorkerCommand command)
        {
            string result;

            switch (command)
            {
                case WorkerCommand.GetInfo:
                    result = this.GetInfo();
                    break;

                case WorkerCommand.Start:
                    _worker.Start();
                    result = _worker.State.ToString();
                    break;

                case WorkerCommand.Pause:
                    _worker.Pause();
                    result = _worker.State.ToString();
                    break;

                case WorkerCommand.Resume:
                    _worker.Resume();
                    result = _worker.State.ToString();
                    break;

                case WorkerCommand.Stop:
                    _worker.Stop();
                    result = _worker.State.ToString();
                    break;

                case WorkerCommand.Dispose:
                    _worker.Dispose();
                    result = _worker.State.ToString();
                    break;

                default:
                    throw new NotImplementedException(); // todo
            }

            return result;
        }

        private string GetInfo()
        {
            var sb = new StringBuilder();
            sb.Append($"Type: {_worker.GetType().FullName}; ");
            sb.Append($"Name: {_worker.Name}; ");
            sb.Append($"State: {_worker.State}");

            return sb.ToString();
        }

        private static string GetResultString(MethodInfo method, object result)
        {
            var returnType = method.ReturnType;
            if (returnType == typeof(void))
            {
                return null;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static object[] BuildParameters(MethodInfo method, string[] arguments)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                return new object[] { };
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
