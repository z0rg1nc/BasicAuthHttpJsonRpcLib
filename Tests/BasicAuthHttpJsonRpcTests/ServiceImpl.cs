using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BtmI2p.JsonRpcHelpers.Client;
using NLog;

namespace BtmI2p.BasicAuthHttpJsonRpc.Tests
{
    public class TestServer1Data1
    {
        public DateTime Time = DateTime.UtcNow;
        public decimal Value1 = 140.3m;
    }
    public interface ITestService1
    {
        List<int> TestMethod1();

        Task<List<int>> TestMethod2(List<TestServer1Data1> data, int b);

        Task<TestServer1Data1> TestMethod3(List<TestServer1Data1> data, int b);

        Task TestDelay();

        Task TestRpcExc();

        Task TestJsonExc();
    }
    public class TestService1Impl : ITestService1
    {
        public const string BaseAddress = "http://127.0.0.1:14300/";
        public const string Username = "user1";
        public const string Password = "password1";
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        protected Guid _instanceGuid { get; set; } = Guid.NewGuid();

        public TestService1Impl()
        {
            _log.Trace("TestService1Impl ctor {0}", _instanceGuid);
        }

        public List<int> TestMethod1()
        {
            var curType = GetType();
            _log.Trace("TestMethod1 {0} {1}", _instanceGuid, curType);
            return Enumerable.Range(0, 10).ToList();
        }

        public async Task<List<int>> TestMethod2(List<TestServer1Data1> data, int b)
        {
            return data.Select(x => (int)(x.Value1 + b)).ToList();
        }
        
        public async Task<TestServer1Data1> TestMethod3(List<TestServer1Data1> data, int b)
        {
            return data.First();
        }

        public async Task TestDelay()
        {
            await Task.Delay(1000).ConfigureAwait(false);
        }

        public async Task TestRpcExc()
        {
            throw new RpcRethrowableException(
                new RpcRethrowableExceptionData()
                {
                    ErrorCode = 15
                }
            );
        }

        public async Task TestJsonExc()
        {
            throw new JsonRpcException()
            {
                JsonErrorCode = 150,
                JsonErrorMessage = "JsonException1Text"
            };
        }
    }
    
}
