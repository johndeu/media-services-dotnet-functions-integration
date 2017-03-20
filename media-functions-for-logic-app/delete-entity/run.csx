/*
This function delete AMS entities (job(s) and/or asset(s))

Input:
{
    "jobID": "nb:jid:UUID:7f566f5e-be9c-434f-bb7b-101b2e24f27e,nb:jid:UUID:58f9e85a-a889-4205-baa1-ecf729f9c753",     // job(s) id. Coma delimited if several job ids 
    "assetId" : "nb:cid:UUID:61926f1d-69ba-4386-a90e-e27803104853,nb:cid:UUID:b4668bc4-2899-4247-b339-429025153ab9"   // asset(s) id.
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
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            Error = ex.ToString()
        });
    }

    try
    {
        if (data.jobId != null)
        {
            var jobids = (string)data.jobId;
            foreach (string jobi in jobids.Split(','))
            {
                log.Info($"Using job Id : {jobi}");
                var job = _context.Jobs.Where(j => j.Id == jobi).FirstOrDefault();
                if (job != null)
                {
                    job.Delete();
                    log.Info("Job deleted.");
                }
                else
                {
                    log.Info("Job not found!");
                }
            }
        }

        if (data.assetId != null)
        {
            var assetids = (string)data.assetId;
            foreach (string asseti in assetids.Split(','))
            {
                log.Info($"Using asset Id : {asseti}");
                var asset = _context.Assets.Where(a => a.Id == asseti).FirstOrDefault();
                if (asset != null)
                {
                    asset.Delete();
                    log.Info("Asset deleted.");
                }
                else
                {
                    log.Info("Asset not found!");
                }
            }
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

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        
    });
}

