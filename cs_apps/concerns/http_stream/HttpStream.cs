using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
// using Newtonsoft.Json;
using Polly;
using Polly.Extensions.Http;

namespace concerns.http_stream
{
    /// <summary>
    /// HTTPレスポンス クラス
    /// </summary>
    public class HttpStreamResponse
    {
        public int m_code;
        public byte[] m_body;
        public string m_url;
        public HttpStreamResponse(int code, byte[] body, string url)
        {
            this.m_code = code;
            this.m_body = body;
            this.m_url = url;
        }
        public string GetBodyString()
        {
            if (this.m_body != null)
            {
                return Encoding.UTF8.GetString(this.m_body);
            }
            return "null";
        }
    }

    /// <summary>
    /// HTTPストリームオブザーバ I/F クラス
    /// </summary>
    public interface IHttpStreamObserver
    {
        /// <summary>
        /// HTTPストリーム受信コールバック関数
        /// </summary>
        /// <param name="byteArray">受信バッファー</param>
        void HttpStreamResponse(List<HttpStreamResponse> responses);
    }

    public class HttpStream: IDisposable
    {
        private ServiceCollection m_service;
        private ServiceProvider m_provider;
        private IBlobService m_blobService;
        private object m_mutex; //!< ミューテックス
        private List<IHttpStreamObserver> m_observers; //!< 通知先リスト

        /// <summary>
        /// HTTPストリームクラス
        /// </summary>
        public HttpStream()
        {
            /*
             * SetHandlerLifetime(TimeSpan.FromMinutes(5))は、HttpClientFactoryが生成するHttpMessageHandlerの寿命を設定。
             * この設定により、HttpClientFactoryは5分ごとに新しいHttpMessageHandlerを生成し、それをHttpClientに関連付ける。
             * この設定の目的は、長時間実行されるプロセスでHttpClientの共有インスタンスを使用する際の問題を解決する。
             * 具体的には、HttpClientがシングルトンまたは静的オブジェクトとしてインスタンス化される状況では、DNSの変更を処理できない問題がある。
             * しかし、HttpMessageHandlerの寿命を管理することで、この問題を回避できる。
             * 現状、5分という時間は、HttpClientがDNSの変更を認識するための最大遅延時間と考えることができる。
             * この値はアプリケーションの要件に応じて調整することが可能。
             * 例えば、DNSの変更をより迅速に反映させる必要がある場合や、ネットワーク接続が頻繁に変わる環境では、
             * より短い時間を設定することも考えられる。
             * 逆に、DNSの変更がほとんどない安定した環境では、より長い時間を設定することも可能。
             * ※設定できる最小値は TimeSpan.Zero（つまり0秒）で、最大値は TimeSpan.MaxValue（約10,000年）。ただし設計／運用次第
             *  (1) IHttpClientFactory を使用して回復力の高い HTTP 要求を実装する
             *      https://learn.microsoft.com/ja-jp/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests.
             *  (2) Polly で指数バックオフを含む HTTP 呼び出しの再試行を実装する
             *      https://learn.microsoft.com/ja-jp/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly.
             *  (3) IHttpClientFactory を使って今はこれ一択と思った話 #C# - Qiita
             *      https://qiita.com/TsuyoshiUshio@github/items/7092fbc510772ce5db63.
             *  (4) Is a Singleton HttpClient receiving a new HttpMessageHandler
             *      https://stackoverflow.com/questions/68820007/is-a-singleton-httpclient-receiving-a-new-httpmessagehandler-after-x-minutes.
             */
            this.m_service = new ServiceCollection();
            this.m_service.AddHttpClient<IBlobService, BlobService>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddPolicyHandler(GetRetryPolicy());
            this.m_provider = m_service.BuildServiceProvider();
            this.m_blobService = m_provider.GetRequiredService<IBlobService>();

            this.m_mutex = new object();
            this.m_observers = new List<IHttpStreamObserver>();
        }

        // リトライポリシー設定
        /*
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()  // todo: static にする必要性があるかわからん。。。
        {
            var jitterier = new Random();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                                                      + TimeSpan.FromMilliseconds(jitterier.Next(0, 100)),
                    onRetry: (response, delay, retryCount, context) =>
                    {
                        // this.NotifyError((int)response.Result.StatusCode, response.Result.ReasonPhrase, response.Result.RequestMessage.RequestUri);
                        Console.WriteLine($"Retrying: StatusCode: {response.Result.StatusCode} Message: {response.Result.ReasonPhrase} RequestUri: {response.Result.RequestMessage.RequestUri}");
                    });
        }
        */
        private IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            var jitterier = new Random();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                                                      + TimeSpan.FromMilliseconds(jitterier.Next(0, 100)),
                    onRetry: (response, delay, retryCount, context) =>
                    {
                        if (response.Exception != null) // 接続できない問題なので、レスポンス情報はない時
                        {
                            // ここに来た場合、リトライ回数分すべてNGとなった場合、呼び出し元の await m_blobService.Execute(urls) で
                            // HttpRequestException 例外が発生するので注意
                            Console.WriteLine($"Retrying due to Exception: {response.Exception.Message}");
                        }
                        else if (response.Result != null) // 接続できているが別の問題（権限等）なので、レスポンス情報がある時(404とか)
                        {
                            // 失敗の理由をデバッグ出力しておく
                            Console.WriteLine($"Retrying: StatusCode: {response.Result.StatusCode} Message: {response.Result.ReasonPhrase} RequestUri: {response.Result.RequestMessage.RequestUri}");
                        }
                    });
        }

        public async void Requests(List<string> urls)
        {
            Console.WriteLine("Requests() start");
            try
            {
                var responses = await m_blobService.Execute(urls);
                Console.WriteLine("Requests() execute end");
                this.NotifyResponse(responses); // 通知
            }
            catch (HttpRequestException e)
            {
                // サーバが応答を返さない場合
                Console.WriteLine($"Requests() failed: {e.Message}");
                var erroResponses = new List<HttpStreamResponse>();
                erroResponses.Add(new HttpStreamResponse(-1, null, urls[0]));
                this.NotifyResponse(erroResponses);
                return;
            }
            Console.WriteLine("Requests() end");
        }

        public void Request(string url)
        {
            Console.WriteLine("Request() start");
            var urls = new List<string>();
            urls.Add(url);
            this.Requests(urls);
            Console.WriteLine("Request() end");
            /*
            try
            {
                var responses = await m_blobService.Execute(urls);
                Console.WriteLine("Request() execute end");
                this.NotifyResponse(responses); // 通知
            }
            catch (HttpRequestException e)
            {
                // サーバが応答を返さない場合
                Console.WriteLine($"Request() failed: {e.Message}");
                var erroResponses = new List<HttpStreamResponse>();
                erroResponses.Add(new HttpStreamResponse(-1, null, url));
                this.NotifyResponse(erroResponses);
                return;
            }
            Console.WriteLine("Request() end");
            */
        }

        /// <summary>
        /// リスナーを登録する
        /// </summary>
        /// <remarks>
        ///  受信通知、状態変化通知を受けるリスナーを登録する
        /// </remarks>
        /// <param name="observer">オブザーバ</param>
        public bool AddObserver(IHttpStreamObserver observer)
        {
            lock (this.m_mutex)
            {
                if (this.m_observers.Contains(observer))
                {
                    return false; // 重複
                }
                this.m_observers.Add(observer);
            }
            return true;
        }

        /// <summary>
        /// リスナーを破棄する
        /// </summary>
        /// <remarks>
        ///  受信通知を受けるリスナーを破棄する
        /// </remarks>
        /// <param name="observer">オブザーバ</param>
        public bool RemoveObserver(IHttpStreamObserver observer)
        {
            lock (this.m_mutex)
            {
                if (!this.m_observers.Contains(observer))
                {
                    return false; //未登録
                }
                this.m_observers.Remove(observer);
            }
            return true;
        }

        /// <summary>
        /// HTTPストリーム受信通知
        /// </summary>
        /// <param name="responses">レスポンス</param>
        public void NotifyResponse(List<HttpStreamResponse> responses)
        {
            lock (this.m_mutex)
            {
                foreach (var sb in this.m_observers)
                {
                    sb.HttpStreamResponse(responses);
                }
            }
        }

        /// <summary>
        /// 後始末
        /// </summary>
        public void Dispose()
        {
            this.m_observers.Clear();
            this.m_provider.Dispose();
            this.m_service.Clear();
        }
    }

    public interface IBlobService
    {
        Task<List<HttpStreamResponse>> Execute(IEnumerable<string> urls);
    }

    public class BlobService : IBlobService
    {
        private readonly HttpClient _client;
        public BlobService(HttpClient client)
        {
            this._client = client;
        }

        public async Task<List<HttpStreamResponse>> Execute(IEnumerable<string> urls)
        {
            Console.WriteLine("Execute() start");
            var responses = new List<HttpStreamResponse>();
            foreach (var url in urls)
            {
                /*
                var response = await _client.GetAsync(url);
                Console.WriteLine($"StatusCode: {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                    responses.Add(new HttpStreamResponse(200, bytes));
                    // Console.WriteLine(Encoding.UTF8.GetString(bytes));
                }
                */
                var response = await _client.GetAsync(url);
                // byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                // 一部のエラーステータスコード（例えば、404 Not Foundや204 No Contentなど）では、レスポンスの本文が存在しない場合がある
                byte[] bytes = response.Content != null ? await response.Content.ReadAsByteArrayAsync() : null;
                responses.Add(new HttpStreamResponse((int)response.StatusCode, bytes, url));
            }
            Console.WriteLine("Execute() end");
            return responses;
        }
    }
}
