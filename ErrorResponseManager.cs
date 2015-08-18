﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    public static class SimpleResponseManager
    {
        //TODO: Make configurable
        private const string SimpleResponsePageLayout = @"<!DOCTYPE html>
<html>
<head>
   <title>{BISMUTH_RESPONSE_NAME}</title>
</head>
<body>
   <h1>{BISMUTH_RESPONSE_NAME}</h1>
   <p>{BISMUTH_RESPONSE_DESC}</p>
   <hr />
   <div style=""text-align: center; font-size: 80%;"">{BISMUTH_RESPONSE_GENERATED_INFO}</div>
</body>
</html>";

        public static HTTPResponse PrepareSimpleResponse(EHTTPResponse responseCode, HTTPHeaderData requestHeaders, VirtualHost host = null)
        {
            HTTPResponse response = new HTTPResponse(requestHeaders == null ? EHTTPVersion.HTTP10 : requestHeaders.HTTPVersion, responseCode);

            string responseBody = null;
            if (host != null)
            {

            }

            if (responseBody == null)
                responseBody = SimpleResponsePageLayout;

            string generatedInfo = "Generated by " + Program.GetFullProgramVersionString() + " on " + DateTime.Now.ToUniversalTime().ToString("r");

            responseBody = responseBody
                .Replace("{BISMUTH_RESPONSE_NAME}", HTTPHeaderData.GetResponseCodeString(responseCode))
                .Replace("{BISMUTH_RESPONSE_DESC}", HTTPHeaderData.GetResponseCodeInfo(responseCode, requestHeaders, host))
                .Replace("{BISMUTH_RESPONSE_GENERATED_INFO}", generatedInfo);

            response.SetResponseBody(responseBody, "text/html");
            return response;
        }
    }
}