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
        // data.AssetId = "nb:cid:UUID:c0d770b4-1a69-43c4-a4e6-bc60d20ab0b2";
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (AssetId)"
        });
    }

    string playerUrl = "";
    string smoothUrl = "";

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

        // publish with a streaming locator
        IAccessPolicy readPolicy2 = _context.AccessPolicies.Create("readPolicy", TimeSpan.FromHours(4), AccessPermissions.Read);
        ILocator outputLocator2 = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, outputAsset, readPolicy2);
        Uri publishurl = GetValidOnDemandURI(outputAsset);
        if (outputLocator2 != null && publishurl != null)
        {
            smoothUrl = publishurl.ToString();
        }

        log.Info($"Smooth url : {smoothUrl}");

        playerUrl = "http://ampdemo.azureedge.net/?url=" + System.Web.HttpUtility.UrlEncode(smoothUrl);
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info($"");
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        PlayerUrl = playerUrl,
        SmoothUrl = smoothUrl
    });
}




