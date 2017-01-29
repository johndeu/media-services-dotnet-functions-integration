#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"

#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/ingestAssetConfigHelpers.csx"

using System;
using System.Net;
using System.Web;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;


// Read values from the App.config file.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;


public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");
    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);
    log.Info("Request : " + jsonContent);

    // Validate input objects
    if (data.AssetId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass AssetId in the input object" });
    if (data.IngestAssetConfigJson == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass IngestAssetConfigJson in the input object" });
    log.Info("Input - Asset Id : " + data.AssetId);
    log.Info("Input - IngestAssetConfigJson : " + data.IngestAssetConfigJson);

    string assetid = data.AssetId;
    string ingestAssetConfigJson = data.IngestAssetConfigJson;
    IngestAssetConfig config = ParseIngestAssetConfig(ingestAssetConfigJson);
    if (!ValidateIngestAssetConfig(config))
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid IngestAssetConfig as FileContent" });
    log.Info("Input - Valid IngestAssetConfig was loaded.");


    string streamingUrl = "";
    try
    {
        // Load AMS account context
        log.Info("Using Azure Media Services account : " + _mediaServicesAccountName);
        _context = new CloudMediaContext(new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey));

        // Get the Asset
        var asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();
        if (asset == null)
        {
            log.Info("Asset not found - " + assetid);
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
        }
        log.Info("Asset found, Asset ID : " + asset.Id);

        // Publish with a streaming locator
        IAccessPolicy streamingAccessPolicy = _context.AccessPolicies.Create("streamingAccessPolicy", config.IngestAssetPublish.PublishSpan, AccessPermissions.Read);
        ILocator outputLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset, streamingAccessPolicy, config.IngestAssetPublish.PublishStartDateTime);

        var manifestFile = asset.AssetFiles.Where(f => f.Name.ToLower().EndsWith(".ism")).FirstOrDefault();
        if (manifestFile != null && outputLocator != null)
        {
            streamingUrl = outputLocator.Path + manifestFile.Name + "/manifest";
        }

        log.Info("Streaming URL : " + streamingUrl);
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        StreamingUrl = streamingUrl
    });
}
