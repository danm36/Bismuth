using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    public class VirtualHost
    {
        public string LocalRootDirectory { get; private set; }
        public bool AllowExtensionlessFiles { get; private set; }
        public List<string> DirectoryIndexes = new List<string>() { "index.php", "index.html", "index.htm" };

        public VirtualHost(string localRoot)
        {
            LocalRootDirectory = localRoot.Replace('\\', '/');
        }

        public string GetFinalResourceLocation(HTTPHeaderData headers)
        {
            string resourceLoc = LocalRootDirectory + headers.GetRequestedResource();

            if (File.Exists(resourceLoc))
            {
                return resourceLoc;
            }
            else if (resourceLoc.EndsWith("/") || resourceLoc.EndsWith("\\"))
            {
                for (int i = 0; i < DirectoryIndexes.Count; i++)
                {
                    if (File.Exists(resourceLoc + DirectoryIndexes[i]))
                    {
                        return resourceLoc + DirectoryIndexes[i];
                    }
                }

                //Return generated directory info
            }

            return null;
        }

        public bool HasBeenModifiedSince(string resourceLocation, DateTime checkTime)
        {
            if (resourceLocation == null)
                return true;

            return File.GetLastWriteTime(resourceLocation) > checkTime;
        }

        public bool HasBeenModifiedSince(HTTPHeaderData headers, DateTime checkTime) { return HasBeenModifiedSince(GetFinalResourceLocation(headers), checkTime); }

        public HTTPResponse GetResource(HTTPHeaderData headers)
        {
            string resourceLoc = GetFinalResourceLocation(headers);
            HTTPResponse response = new HTTPResponse(headers.HTTPVersion, EHTTPResponse.R200_OK);

            if (File.Exists(resourceLoc))
            {
                response.SetResponseBody(File.ReadAllBytes(resourceLoc), MIMETypeManager.GetMIMEFromFilePath(resourceLoc), File.GetLastWriteTimeUtc(resourceLoc));
                response.SetETag(resourceLoc);
            }
            else
            {
                return SimpleResponseManager.PrepareSimpleResponse(EHTTPResponse.R404_NotFound, headers, this);
            }

            return response;
        }
    }
}
