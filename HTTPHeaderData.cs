using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Bismuth
{
    public enum EHTTPVersion
    {
        Unknown,
        HTTP09,
        HTTP10,
        HTTP11,
        HTTP20,
    }

    public enum EHTTPMethod
    {
        Unknown,
        GET,
        POST,
        PUT,
        DELETE,
        HEAD,
        OPTIONS,
        CONNECT,
    }

    public enum EHTTPResponse : int
    {
        R100_Continue               = 100,
        R101_SwitchingProtocols     = 101,
        R102_Processing             = 102,

        R200_OK                             = 200,
        R201_Created                        = 201,
        R202_Accepted                       = 202,
        R203_Non__AuthoritativeInformation  = 203,
        R204_NoContent                      = 204,
        R205_ResetContent                   = 205,
        R206_PartialContent                 = 206,
        R207_Multi__Status                  = 207,
        R208_Alreadyreported                = 208,
        R226_IMUsed                         = 226,

        R300_MultipleChoices        = 300,
        R301_MovedPermanently       = 301,
        R302_Found                  = 302,
        R303_SeeOther               = 303,
        R304_NotModified            = 304,
        R305_UseProxy               = 305,
        R306_SwitchProxy            = 306,
        R307_TemporaryRedirect      = 307,
        R308_PermanentRedirect      = 308,
        R308_ResumeIncomplete       = 308,

        R400_BadRequest                         = 400,
        R401_Unauthorized                       = 401,
        R402_PaymentRequired                    = 402,
        R403_Forbidden                          = 403,
        R404_NotFound                           = 404,
        R405_MethodNotAllowed                   = 405,
        R406_NotAcceptable                      = 406,
        R407_ProxyAuthenticationRequired        = 407,
        R408_RequestTimeout                     = 408,
        R409_Conflict                           = 409,
        R410_Gone                               = 410,
        R411_LengthRequired                     = 411,
        R412_PreconditionFailed                 = 412,
        R413_PayloadTooLarge                    = 413,
        R414_Request__URI_TooLong               = 414,
        R415_UnsupportedMediaType               = 415,
        R416_RequestedRangeNotSatisfiable       = 416,
        R417_ExpectationFailed                  = 417,
        R419_AuthenticationTimeout              = 419,
        R421_MisdirectedRequest                 = 421,
        R422_UnprocessableEntity                = 422,
        R423_Locked                             = 423,
        R424_FailedDependency                   = 424,
        R426_UpgradeRequired                    = 426,
        R428_PreconditionRequired               = 428,
        R429_TooManyRequests                    = 429,
        R431_RequestHeaderFieldsTooLarge        = 431,
        R451_UnavailableForLegalReasons         = 451,

        R500_InternalServerError                = 500,
        R501_NotImplemented                     = 501,
        R502_BadGateway                         = 502,
        R503_ServiceUnavailable                 = 503,
        R504_GatewayTimeout                     = 504,
        R505_HTTPVersionNotSupported            = 505,
        R506_VariantAlsoNegotiates              = 506,
        R507_InsufficientStorage                = 507,
        R508_LoopDetected                       = 508,
        R509_BandwidthLimitExceeded             = 509,
        R510_NotExtended                        = 510,
        R511_NetworkAuthenticationRequired      = 511,
        R520_UnknownError                       = 520,
        R522_OriginConnectionTimeOut            = 522,
    }

    public class HTTPHeaderData
    {
        public bool InvalidHeader { get; private set; }

        public EHTTPVersion HTTPVersion = EHTTPVersion.Unknown;
        public EHTTPMethod HTTPMethod = EHTTPMethod.Unknown;
        public EHTTPResponse HTTPResponseCode = EHTTPResponse.R400_BadRequest;

        Dictionary<string, string> headerData = new Dictionary<string, string>();
        string requestedResource = "/";

        public HTTPHeaderData(EHTTPVersion httpVersion, EHTTPResponse httpResponseCode)
        {
            HTTPVersion = httpVersion;
            HTTPResponseCode = httpResponseCode;
            InvalidHeader = false;
        }

        public HTTPHeaderData(string headerString)
        {
            InvalidHeader = true;

            int doubleCRLFIndex = headerString.IndexOf("\r\n\r\n");
            if (doubleCRLFIndex >= 0)
                headerString = headerString.Substring(0, doubleCRLFIndex);

            string[] headerRows = headerString.Split(new string[] { "\r\n" }, StringSplitOptions.None);

            if (headerRows[0].EndsWith("HTTP/1.1"))
                HTTPVersion = EHTTPVersion.HTTP11;
            else if (headerRows[0].EndsWith("HTTP/1.0"))
                HTTPVersion = EHTTPVersion.HTTP11;
            else
                HTTPVersion = EHTTPVersion.HTTP09;

            int spaceIndex = headerRows[0].IndexOf(' ');

            if (spaceIndex < 3 || spaceIndex > 7)
                return;

            string method = headerRows[0].Substring(0, spaceIndex);
            switch(method)
            {
                case "GET": HTTPMethod = EHTTPMethod.GET; break;
                case "POST": HTTPMethod = EHTTPMethod.POST; break;
                case "PUT": HTTPMethod = EHTTPMethod.PUT; break;
                case "DELETE": HTTPMethod = EHTTPMethod.DELETE; break;

                case "HEAD": HTTPMethod = EHTTPMethod.HEAD; break;
                case "OPTIONS": HTTPMethod = EHTTPMethod.OPTIONS; break;
                case "CONNECT": HTTPMethod = EHTTPMethod.CONNECT; break;
                default: return; //Invalid
            }

            requestedResource = headerRows[0].Substring(headerRows[0].IndexOf(' ') + 1);
            requestedResource = requestedResource.Substring(0, requestedResource.IndexOfAny(new char[] { ' ', '\r' }));

            for (int i = 1; i < headerRows.Length; i++)
            {
                if (headerRows[i] == "")
                    break;

                string field = headerRows[i].Substring(0, headerRows[i].IndexOf(':'));
                string value = headerRows[i].Substring(headerRows[i].IndexOf(':') + 1).TrimStart(null);

                //Check for a malformed field

                headerData.Add(field, value);
            }

            InvalidHeader = false;
        }

        public string GetRequestedResource()
        {
            return requestedResource;
        }

        public bool HasHeaderField(string header)
        {
            return headerData.ContainsKey(header);
        }

        public HTTPHeaderData AddHeaderField(string field, string value)
        {
            if (headerData.ContainsKey(field))
                headerData[field] = value;
            else
                headerData.Add(field, value);

            return this;
        }

        public HTTPHeaderData AddHeaderFieldIfNotPresent(string field, string value)
        {
            if (!headerData.ContainsKey(field))
                headerData.Add(field, value);

            return this;
        }

        public string GetHeaderField(string header)
        {
            return headerData.ContainsKey(header) ? headerData[header] : null;
        }

        public override string ToString()
        {
            StringWriter sw = new StringWriter();

            switch (HTTPVersion)
            {
                default: case EHTTPVersion.HTTP11: sw.Write("HTTP/1.1 "); break;
                case EHTTPVersion.HTTP10: sw.Write("HTTP/1.0 "); break;
                case EHTTPVersion.HTTP09: sw.Write("HTTP/0.9 "); break;
            }
            sw.Write(GetResponseCodeString(HTTPResponseCode) + "\r\n");

            foreach (KeyValuePair<string, string> kvp in headerData)
            {
                sw.Write(kvp.Key + ": " + kvp.Value + "\r\n");
            }

            sw.Write("\r\n");
            return sw.ToString();
        }

        public static string GetResponseCodeString(EHTTPResponse responseCode)
        {
            StringBuilder finalResponse = new StringBuilder();
            string code = responseCode.ToString();

            for(int i = 1; i < code.Length; ++i)
            {
                if (code[i] == '_')
                {
                    if (code[i + 1] == '_')
                    {
                        finalResponse.Append('-');
                        ++i;
                    }
                    else
                    {
                        finalResponse.Append(' ');
                    }
                    continue;
                }
                else if (char.IsUpper(code[i]) && code[i - 1] != '_' && !char.IsUpper(code[i - 1]))
                    finalResponse.Append(' ');

                finalResponse.Append(code[i]);
            }

            return finalResponse.ToString();
        }

        public static string GetResponseCodeString(int responseCode)
        {
            if(Enum.IsDefined(typeof(EHTTPResponse), responseCode))
                return GetResponseCodeString((EHTTPResponse)responseCode);
            return GetResponseCodeString(EHTTPResponse.R400_BadRequest);
        }

        public static string GetResponseCodeInfo(EHTTPResponse responseCode, HTTPHeaderData requestHeaders, VirtualHost host = null)
        {
            switch (responseCode)
            {
                default: return "";
                case EHTTPResponse.R404_NotFound: return "The requested URL " + requestHeaders.GetRequestedResource() + " was not found on this server.";
            }
        }
    }
}
