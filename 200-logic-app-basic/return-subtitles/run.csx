/*
This function returns subtitles from an asset.

Input:
{
    "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Mandatory, Id of the source asset
 }

Output:
{
    "vttUrl" : "",      // the full path to vtt file if asset is publised
    "ttmlUrl" : "",     // the full path to vtt file if asset is publised
    "pathUrl" : "",     // the path to the asset if asset is publised
    "vttDocument" : "", // the full vtt document
    "ttmlDocument : ""  // the full ttml document
 }
*/

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

    // Init variables
    string vttUrl = "";
    string pathUrl = "";
    string ttmlUrl = "";
    string vttContent = "";
    string ttmlContent = "";

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.assetId == null)
    {
        // for test
        // data.assetId = "nb:cid:UUID:d9496372-32f5-430d-a4c6-d21ec3e01525";

        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (AssetId)"
        });
    }

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
        string assetid = data.assetId;
        var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

        if (outputAsset == null)
        {
            log.Info($"Asset not found {assetid}");
            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not found"
            });
        }

        var vttSubtitle = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".VTT")).FirstOrDefault();
        var ttmlSubtitle = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".TTML")).FirstOrDefault();

        Uri publishurl = GetValidOnDemandPath(outputAsset);
        if (publishurl != null)
        {
            pathUrl = publishurl.ToString();
        }
        else
        {
            log.Info($"Asset not published");
        }

        if (vttSubtitle != null)
        {
            if (publishurl != null)
            {
                vttUrl = pathUrl + vttSubtitle.Name;
                log.Info($"vtt url : {vttUrl}");
            }
            vttContent = ReturnContent(vttSubtitle);
        }

        if (ttmlSubtitle != null)
        {
            if (publishurl != null)
            {
                ttmlUrl = pathUrl + vttSubtitle.Name;
                log.Info($"ttml url : {ttmlUrl}");
            }
            ttmlContent = ReturnContent(ttmlSubtitle);
        }
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info($"");
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        vttUrl = vttUrl,
        ttmlUrl = ttmlUrl,
        pathUrl = pathUrl,
        ttmlDocument = ttmlContent,
        vttDocument = vttContent
    });
}

public static string ReturnContent(IAssetFile assetFile)
{
    string datastring = null;

    try
    {
        string tempPath = System.IO.Path.GetTempPath();
        string filePath = Path.Combine(tempPath, assetFile.Name);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        assetFile.Download(filePath);

        StreamReader streamReader = new StreamReader(filePath);
        Encoding fileEncoding = streamReader.CurrentEncoding;
        datastring = streamReader.ReadToEnd();
        streamReader.Close();

        File.Delete(filePath);
    }
    catch
    {

    }

    return datastring;
}