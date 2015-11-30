using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BtmI2p.JsonRpcHelpers.Server;
using BtmI2p.MiscUtils;
using BtmI2p.Newtonsoft.Json.Linq;
using NLog;
using ObjectStateLib;

namespace BtmI2p.BasicAuthHttpJsonRpc.Server
{
    public class BasicAuthHttpJsonServerService<TInterface> : IMyAsyncDisposable
    {
        private BasicAuthHttpJsonServerService()
        {
        }

        private HttpListener _listener;
        private JsonRpcServerProcessor<TInterface, object> _jsonRpcServer;
        private string _username;
        private string _password;
        private AuthenticationSchemes _auth;
        public static BasicAuthHttpJsonServerService<TInterface> CreateInstance(
            Uri baseAddress,
            TInterface implInstance,
            string username,
            string password,
            AuthenticationSchemes auth = AuthenticationSchemes.Basic
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
            var result = new BasicAuthHttpJsonServerService<TInterface>();
            result._auth = auth;
            result._username = username;
            result._password = password;
            result._listener = new HttpListener();
            result._listener.Prefixes.Add(baseAddress.ToString());
            //result._listener.IgnoreWriteExceptions = true;
            if (auth != AuthenticationSchemes.Anonymous)
            {
                result._listener.AuthenticationSchemeSelectorDelegate 
                    = request =>
                    {
                        return auth;
                    };
                result._listener.AuthenticationSchemes = auth;
            }
            result._listener.Start();
            try
            {
                var jsonServer = new JsonRpcServerProcessor<TInterface, object>(implInstance);
                result._jsonRpcServer = jsonServer;
                result._stateHelper.SetInitializedState();
                result.MainLoop();
                return result;
            }
            catch
            {
                result._listener.Stop();
                throw;
            }
        }

        private async void MainLoop()
        {
            var curMethodName = this.MyNameOfMethod(e => e.MainLoop());
            try
            {
                while (true)
                {
                    using (_stateHelper.GetFuncWrapper())
                    {
                        if(_cts.IsCancellationRequested)
                            return;
                        var ctx = await _listener.GetContextAsync()
                            .ThrowIfCancelled(_cts.Token).ConfigureAwait(false);
                        //_logger.Trace("{0} request {1}", curMethodName, ctx.Request.QueryString);
                        if (ctx.Request.HttpMethod != "POST")
                        {
                            ctx.Response.StatusCode = 400;
                            ctx.Response.Close();
                            continue;
                        }
                        if (_auth != AuthenticationSchemes.Anonymous)
                        {
                            if (
                                ctx.User == null
                                || !ctx.User.Identity.IsAuthenticated
                            )
                            {
                                ctx.Response.StatusCode = 401;
                                ctx.Response.Close();
                                continue;
                            }
                            if (_auth == AuthenticationSchemes.Basic)
                            {
                                var identity = (HttpListenerBasicIdentity) ctx.User.Identity;
                                if (
                                    !(
                                        identity.Name == _username
                                        && identity.Password == _password)
                                    )
                                {
                                    ctx.Response.StatusCode = 403;
                                    ctx.Response.Close();
                                    continue;
                                }
                            }
                            else
                            {
                                // NotImplemented
                                ctx.Response.StatusCode = 404;
                                ctx.Response.Close();
                                continue;
                            }
                        }
                        string post;
                        using(
                            var reader = new StreamReader(
                                ctx.Request.InputStream, 
                                ctx.Request.ContentEncoding
                            )
                        )
                        {
                            post = reader.ReadToEnd();
                        }
                        JObject requestJson;
                        try
                        {
                            requestJson = post.ParseJsonToType<JObject>();
                        }
                        catch(Exception exc)
                        {
                            _logger.Error(
                                "{0} Parse post data '{1}' error '{2}'", 
                                curMethodName, 
                                post,
                                exc.ToString()
                            );
                            ctx.Response.StatusCode = 400;
                            ctx.Response.Close();
                            continue;
                        }
                        var jResult = await _jsonRpcServer.ProcessRequest(
                            requestJson
                        ).ConfigureAwait(false);
                        var resultDataBytes = Encoding.UTF8.GetBytes(
                            jResult.WriteObjectToJson()
                        );
                        ctx.Response.ContentEncoding = Encoding.UTF8;
                        ctx.Response.ContentLength64 = resultDataBytes.Length;
                        await ctx.Response.OutputStream.WriteAsync(
                            resultDataBytes, 
                            0, 
                            resultDataBytes.Length
                        ).ConfigureAwait(false);
                        ctx.Response.Close();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WrongDisposableObjectStateException)
            {
            }
            catch (Exception exc)
            {
                _logger.Error("{0} '{1}'", curMethodName, exc.ToString());
            }
        }

        private readonly Logger _logger
            = LogManager.GetCurrentClassLogger();
        private readonly CancellationTokenSource _cts
            = new CancellationTokenSource();
        private readonly DisposableObjectStateHelper _stateHelper
            = new DisposableObjectStateHelper("BasicAuthHttpJsonServerService");
        public async Task MyDisposeAsync()
        {
            _cts.Cancel();
            await _stateHelper.MyDisposeAsync().ConfigureAwait(false);
            _listener.Close();
            _cts.Dispose();
        }
    }
}
