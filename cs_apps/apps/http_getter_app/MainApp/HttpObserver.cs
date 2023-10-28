using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using concerns.http_stream;

namespace apps.http_getter_app.MainApp
{
    public class HttpObserver : IHttpStreamObserver, IDisposable
    {
        public HttpStream m_httpStream;
        public HttpObserver()
        {
            this.m_httpStream = new HttpStream();
            this.m_httpStream.AddObserver(this);
        }

        public void Dispose()
        {
            if (this.m_httpStream != null)
            {
                this.m_httpStream.Dispose();
            }
        }

        public void RequestOneTime()
        {
            this.m_httpStream.Request("http://localhost:8080/api2/test");
        }

        public void RequestMultipleTime()
        {
            var urls = new List<string>() {
                "http://localhost:8080/api/test",
                "http://localhost:8080/api2/test",
                "http://localhost:8080/image",
            };
            this.m_httpStream.Requests(urls);
        }

        public void HttpStreamResponse(List<HttpStreamResponse> responses)
        {
            foreach (var response in responses)
            {
                Console.WriteLine("HttpStreamResponse received!!!");
                Console.WriteLine($"code:{response.m_code}\nbody:{response.GetBodyString()}\nurl:{response.m_url}");
            }
        }
    }
}
