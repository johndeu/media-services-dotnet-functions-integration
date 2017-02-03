#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"

using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;


// Read values from the App.config file.
static string _sourceStorageAccountName = Environment.GetEnvironmentVariable("SourceStorageAccountName");
static string _sourceStorageAccountKey = Environment.GetEnvironmentVariable("SourceStorageAccountKey");

private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

// Field for service context.
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;
private static CloudStorageAccount _destinationStorageAccount = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.AssetId == null)
    {
        // for test
        data.AssetId = "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b";
        /*
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (AssetId)"
        });
        */
    }

    string vttUrl = "";
    string pathUrl = "";

    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    try
    {
        // Create and cache the Media Services credentials in a static class variable.
        _cachedCredentials = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

        // Used the chached credentials to create CloudMediaContext.
        _context = new CloudMediaContext(_cachedCredentials);

        // Get the asset
        string assetid = data.AssetId;
        var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

        if (outputAsset == null)
        {
            log.Info($"Asset not found {assetid}");
            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not found"
            });
        }

        Uri publishurl = GetValidOnDemandURI(outputAsset);
        if (publishurl != null)
        {
            UriBuilder u2 = new UriBuilder();
            u2.Host = publishurl.Host;
            u2.Path = publishurl.Segments[0] + publishurl.Segments[1];
            u2.Scheme = publishurl.Scheme;
            pathUrl = u2.ToString();

            var subtitle = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWidth(".VTT").FirstOrDefault());
            if (subtitle == null)
            {
                log.Info($"VTT Subtitle file not found {assetid}");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "VTT subtitle not found"
                });
            }
            else
            {
                vttUrl = pathUrl + subtitle.Name;
            }
        }
        else
        {
            log.Info($"Asset not published");

            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not published"
            });

        }

        log.Info($"Vtt url : {vttUrl}");

    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info($"");
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        VttUrl = vttUrl,
        PathUrl = pathUrl
    });
}




