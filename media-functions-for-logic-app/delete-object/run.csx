/*
This function delete an object (job and/or asset)

Input:
{
    "jobID": "",      
    "assetId" : "",
    
}

Output:
{
        
}
*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
#r "System.XML"
#r "System.XML.Linq"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"
#load "../Shared/jobHelpers.csx"

using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;
using System.Xml;
using System.Xml.Linq;

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
    int taskindex = 0;

    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.jobId == null && data.assetId == null)
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass job Id and/or asset Id of the objects to delete"
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
    }
    catch
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }


    if (data.jobId != null)
    {
        string jobId = data.jobId;
        log.Info($"Using job Id : {jobId}");
        var job = _context.Jobs.Where(j => j.Id == jobId).FirstOrDefault();
        if (job != null)
        {
            job.Delete();
        }
        else
        {
            log.Info("Job not found!");
        }
    }

    if (data.assetId != null)
    {
        string assetId = data.assetId;
        log.Info($"Using asset Id : assetId}");
        var asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
        if (asset != null)
        {
            asset.Delete();
        }
        else
        {
            log.Info("Asset not found!");
        }
    }

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        
    });
}

