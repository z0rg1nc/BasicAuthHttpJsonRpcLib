using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using BtmI2p.JsonRpcHelpers.Client;
using BtmI2p.MiscUtils;
using BtmI2p.Newtonsoft.Json;
using BtmI2p.Newtonsoft.Json.Linq;
using LinFu.DynamicProxy;
using NLog;

namespace BtmI2p.BasicAuthHttpJsonRpc.Client
{
    public class VoidResult
    {
    }

    public static class ResultOrError
    {
        public static async Task<ResultOrError<T2>> GetFromFunc<T2>(
            Func<Task<T2>> func
        )
        {
            try
            {
                var result = await func().ConfigureAwait(false);
                return new ResultOrError<T2>
                {
                    Res = result
                };
            }
            catch (RpcRethrowableException rpcExc)
            {
                return new ResultOrError<T2>
                {
                    Err = rpcExc.ErrorData,
                    Res = default(T2)
                };
            }
            catch (Exception exc)
            {
                return new ResultOrError<T2>
                {
                    Err = new RpcRethrowableExceptionData
                    {
                        ErrorCode = (int)EJsonRpcClientProcessorServiceErrCodes.UnknownErrorRpcExcCode,
                        ErrorMessage = exc.Message
                    },
                    Res = default(T2)
                };
            }
        }
    }

    public class ResultOrError<T1>
    {
        public ResultOrError() : base()
        {
            Err = null;
        }

        // null if no error
        public RpcRethrowableExceptionData Err { get; set; }
        public T1 Res { get; set; }
        /**/

        public T1 GetResult()
        {
            if(Err != null)
                throw new RpcRethrowableException(
                    Err
                );
            return Res;
        }
        
    }

    public class BasicAuthHttpJsonClientService<T1> : IInvokeWrapper
    {
        private BasicAuthHttpJsonClientService()
        {
        }

        public T1 Proxy
        {
            get
            {
                var factory = new ProxyFactory();
                return factory.CreateProxy<T1>(this);
            }
        }

        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private string _username;
        private string _password;
        private Uri _baseAddress;
        private AuthenticationSchemes _auth;
	    private bool _useLowCaseMthdNames = false;
        public static BasicAuthHttpJsonClientService<T1> CreateInstance(
            Uri baseAddress,
            string username,
            string password,
            AuthenticationSchemes auth = AuthenticationSchemes.Basic,
			bool useLowCaseMthdNames = false
        )
        {
            if (
                auth != AuthenticationSchemes.Anonymous
                && auth != AuthenticationSchemes.Basic
            )
            {
                throw new ArgumentOutOfRangeException(
                    MyNameof.GetLocalVarName(() => auth)
                );
            }
            var result = new BasicAuthHttpJsonClientService<T1>();
            result._baseAddress = baseAddress;
            result._auth = auth;
            result._username = username;
            result._password = password;
	        result._useLowCaseMthdNames = useLowCaseMthdNames;
            return result;
        }

        public void Dispose()
        {
        }

        public void BeforeInvoke(InvocationInfo info)
        {
        }

        private async Task<object> DoInvokeImpl(InvocationInfo info)
        {
            var joe = JsonRpcClientProcessor.GetJsonRpcRequest(
				info,
				useLowCaseMthdName: _useLowCaseMthdNames
			);
            string jsonRequest = JsonConvert.SerializeObject(joe, Formatting.Indented);

            using (
                var httpClient = new HttpClient(
                    new HttpClientHandler()
                    {
                        UseProxy = false
                    },
                    true
                )
            )
            {
                if (_auth == AuthenticationSchemes.Basic)
                {
                    var credentials = Encoding.ASCII.GetBytes(
                        $"{_username}:{_password}"
                    );
                    httpClient.DefaultRequestHeaders.Authorization
                        = new AuthenticationHeaderValue(
                            "Basic", Convert.ToBase64String(credentials)
                        );
                }
                var response = await httpClient.PostAsync(
                    _baseAddress,
                    new StringContent(
                        jsonRequest,
                        Encoding.UTF8,
                        "application/json"
                    )
                ).ConfigureAwait(false);
                var responseString
                    = await response.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);
                //_log.Trace("responce string {0}", responseString);
                var jsonResult = JsonConvert.DeserializeObject<JObject>(
                    responseString
                );
                return await JsonRpcClientProcessor.GetJsonRpcResult(jsonResult, info)
                    .ConfigureAwait(false);
            }
        }

        public object DoInvoke(InvocationInfo info)
        {
            return JsonRpcClientProcessor.DoInvokeHelper(
                info,
                DoInvokeImpl
            );
        }

        public void AfterInvoke(InvocationInfo info, object returnValue)
        {
        }
    }
}
