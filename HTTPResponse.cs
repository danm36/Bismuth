using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    public class HTTPResponse
    {
        public HTTPHeaderData Header;
        public byte[] Body = null;
        DateTime lastModified = DateTime.MinValue;
        public string ContentType = null;

        MD5 md5 = MD5.Create();
        public string ETag = "";

        public HTTPResponse(EHTTPVersion httpVersion, EHTTPResponse httpResponse)
        {
            Header = new HTTPHeaderData(httpVersion, httpResponse);
        }

        public void SetResponseBody(string pBody, string contentType = null)
        {
            SetResponseBody(pBody, contentType, DateTime.MinValue);
        }

        public void SetResponseBody(string pBody, string contentType, DateTime pLastModified)
        {
            SetResponseBody(Encoding.ASCII.GetBytes(pBody), contentType, pLastModified);
        }

        public void SetResponseBody(byte[] pBody, string contentType = null)
        {
            SetResponseBody(pBody, contentType, DateTime.MinValue);
        }

        public void SetResponseBody(byte[] pBody, string contentType, DateTime pLastModified)
        {
            Body = pBody;
            ContentType = contentType;
            lastModified = pLastModified;

            byte[] hash = md5.ComputeHash(Body);

            StringBuilder pendingETag = new StringBuilder();
            for (int i = 0; i < hash.Length; ++i)
                pendingETag.Append(hash[i].ToString("X2"));

            ETag = pendingETag.ToString();
        }


        public void WriteToStream(Stream stream, bool closingConnection = true)
        {
            Header.HTTPResponseCode = EHTTPResponse.R200_OK;

            Header.AddHeaderField("Connection", closingConnection ? "close" : "keep-alive")
                .AddHeaderField("Date", DateTime.Now.ToUniversalTime().ToString("r"))
                .AddHeaderField("Server", Program.GetVersionString())
                .AddHeaderField("ETag", ETag);

            if (!closingConnection) Header.AddHeaderField("Keep-Alive", "timeout=" + (NetworkManager.ConnectionTTL / 1000) + ", max=100");
            if (Body != null) Header.AddHeaderField("Content-Length", Body.Length.ToString());
            if (ContentType != null) Header.AddHeaderField("Content-Type", ContentType);
            if (lastModified != DateTime.MinValue) Header.AddHeaderField("Last-Modified", lastModified.ToUniversalTime().ToString("r"));

            byte[] headerData = Encoding.ASCII.GetBytes(Header.ToString());
            stream.Write(headerData, 0, headerData.Length);
            stream.Write(Body, 0, Body.Length);
            stream.Flush();

            md5.Dispose();
        }
    }
}
