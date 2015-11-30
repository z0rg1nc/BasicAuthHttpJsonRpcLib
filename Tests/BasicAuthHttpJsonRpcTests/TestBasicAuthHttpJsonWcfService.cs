using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BtmI2p.BasicAuthHttpJsonRpc.Client;
using BtmI2p.BasicAuthHttpJsonRpc.Server;
using BtmI2p.JsonRpcHelpers.Client;
using BtmI2p.MiscUtils;
using Xunit;
using Xunit.Abstractions;

namespace BtmI2p.BasicAuthHttpJsonRpc.Tests
{
    public class TestBasicAuthHttpJsonWcfServiceFixture : IDisposable
    {
        private bool _initialized = false;
        private BasicAuthHttpJsonClientService<ITestService1> _clientService;
        private BasicAuthHttpJsonServerService<ITestService1> _serverBasic = null;
        public void Init()
        {
            if (!_initialized)
            {
                var instance = Activator.CreateInstance<TestService1Impl>();
                _serverBasic = BasicAuthHttpJsonServerService<
                    ITestService1
                >.CreateInstance(
                    new Uri(TestService1Impl.BaseAddress),
                    instance,
                    TestService1Impl.Username,
                    TestService1Impl.Password
                );
                _clientService =
                    BasicAuthHttpJsonClientService<ITestService1>.CreateInstance(
                        new Uri(TestService1Impl.BaseAddress),
                        TestService1Impl.Username,
                        TestService1Impl.Password
                    );
                Proxy = _clientService.Proxy;
                _initialized = true;
            }
        }

        public ITestService1 Proxy;
        public void Dispose()
        {
            if (_initialized)
            {
                _clientService.Dispose();
                _serverBasic.MyDisposeAsync().Wait();
            }
        }
    }

    public class TestBasicAuthHttpJsonWcfService
    {		
        private readonly ITestOutputHelper _output;
        public TestBasicAuthHttpJsonWcfService(ITestOutputHelper output)
        {
	        _output = output;
        }

        [Fact]
        public async Task Test1()
        {
            using (var fixtureData = new TestBasicAuthHttpJsonWcfServiceFixture())
            {
                fixtureData.Init();

                _output.WriteLine((fixtureData.Proxy.TestMethod1()).WriteObjectToJson());
                _output.WriteLine((await fixtureData.Proxy.TestMethod2(
                    new List<TestServer1Data1>()
                    {
                        new TestServer1Data1(),
                        new TestServer1Data1()
                        {
                            Value1 = 150.5m
                        }
                    },
                    10
                    ).ConfigureAwait(false)
                    ).WriteObjectToJson());
                _output.WriteLine((await fixtureData.Proxy.TestMethod3(
                    new List<TestServer1Data1>()
                    {
                        new TestServer1Data1(),
                        new TestServer1Data1()
                        {
                            Value1 = 150.5m
                        }
                    },
                    10
                    ).ConfigureAwait(false)
                ).WriteObjectToJson());
                _output.WriteLine("{0}", DateTime.UtcNow);
                await fixtureData.Proxy.TestDelay().ConfigureAwait(false);
                _output.WriteLine("{0}", DateTime.UtcNow);
                await Assert.ThrowsAsync<RpcRethrowableException>(
                    async () => await fixtureData.Proxy.TestRpcExc().ConfigureAwait(false)
                ).ConfigureAwait(false);
                await Assert.ThrowsAsync<JsonRpcException>(
                    async () => await fixtureData.Proxy.TestJsonExc().ConfigureAwait(false)
                ).ConfigureAwait(false);
            }
        }
    }
}
