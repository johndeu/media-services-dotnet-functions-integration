/*
This function returns media analytics from an asset.

Input:
{
    "assetFaceRedactionId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Mandatory, Id of the source asset that contains media analytics (face redaction)
    "timeOffset" :"00:01:00", // optional, offset to add to subtitles (used for live analytics)
    "deleteAsset" : true // Optional, delete the asset once data has been read from it
 }

Output:
{
    "jsonFaceRedaction" : "",      // the json of the face redaction
     "jsonFaceRedactionOffset" : "",      // the json of the face redaction with offset
     "jpgFaces":[
                {
                    "id" :24,
                    "fileId": "nb:cid:UUID:a93464ae-cbd5-4e63-9459-a3e2cf869f0e",
                    "fileName": "ArchiveTopBitrate_video_800000_thumb000024.jpg",
                    "url" : "http://xpouyatdemo.streaming.mediaservices.windows.net/903f9261-d745-48aa-8dfe-ebcd6e6128d6/ArchiveTopBitrate_video_800000_thumb000024.jpg"
                }
                ]
    "pathUrl" : "",     // the path to the asset if asset is published
 }
*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Xml.Linq"
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
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

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
    string pathUrl = "";
    string jsonFaceRedaction = "";
    string jsonFaceRedactionOffset = "";
    dynamic jpgFaces = new JArray() as dynamic;

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.assetFaceRedactionId == null)
    {
        // for test
        // data.assetId = "nb:cid:UUID:d9496372-32f5-430d-a4c6-d21ec3e01525";

        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (assetFaceRedactionId)"
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
        string assetid = data.assetFaceRedactionId;
        var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

        if (outputAsset == null)
        {
            log.Info($"Asset not found {assetid}");
            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not found"
            });
        }

        var jsonFile = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".JSON")).FirstOrDefault();
        var jpgFiles = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".JPG"));

        Uri publishurl = GetValidOnDemandPath(outputAsset);
        if (publishurl != null)
        {
            pathUrl = publishurl.ToString();
        }
        else
        {
            log.Info($"Asset not published");
        }

        foreach (IAssetFile file in jpgFiles)
        {
            string index = file.Name.Substring(file.Name.Length - 10, 6);
            int index_i = 0;
            if (int.TryParse(index, out index_i))
            {
                dynamic entry = new JObject();
                entry.id = index_i;
                entry.fileId = file.Id;
                entry.fileName = file.Name;
                if (!string.IsNullOrEmpty(pathUrl))
                {
                    entry.url = pathUrl + file.Name;
                }
                jpgFaces.Add(entry);
            }
        }

        if (jsonFile != null)
        {
            jsonFaceRedaction = ReturnContent(jsonFile);

            if (data.timeOffset != null) // let's update the json with new timecode
            {
                var tsoffset = TimeSpan.Parse((string)data.timeOffset);
                dynamic obj = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonFaceRedaction);
                foreach (var frag in obj.fragments)
                {
                    frag.start = (long)frag.start + tsoffset.Ticks;
                }
                jsonFaceRedactionOffset = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
            }
        }

        if (jsonFaceRedaction != "" && data.deleteAsset != null && ((bool)data.deleteAsset))
        // If asset deletion was asked
        {
            outputAsset.Delete();
        }
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.InternalServerError, new
        {
            Error = ex.ToString()
        });
    }

    log.Info($"");
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        jsonFaceRedaction = jsonFaceRedaction,
        jsonFaceRedactionOffset = jsonFaceRedactionOffset,
        jpgFaces = jpgFaces
        
    });
}