#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
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


// Submit an encoding job
// with MES (default)
// with Premium Encoder if data.WorkflowAssetId is specified


public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.AssetId == null)
    {
        // for test
        // data.AssetId = "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc";

        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (AssetId)"
        });
    }

    // for test
    // data.WorkflowAssetId = "nb:cid:UUID:44fe8196-616c-4490-bf80-24d1e08754c5";
    // if data.WorkflowAssetId is passed, then it means a Premium Encoding task is asked

    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    IJob job = null;
    IAsset outputassetencoded = null;
    ITask taskEncoding = null;
    ITask taskIndex1 = null;
    IAsset outputassetindex1 = null;
    ITask taskIndex2 = null;
    IAsset outputassetindex2 = null;

    try
    {
        // Create and cache the Media Services credentials in a static class variable.
        _cachedCredentials = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

        // Used the chached credentials to create CloudMediaContext.
        _context = new CloudMediaContext(_cachedCredentials);

        // find the Asset
        string assetid = (string)data.AssetId;
        IAsset asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

        if (asset == null)
        {
            log.Info($"Asset not found {assetid}");
            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not found"
            });
        }

        if (data.WorkflowAssetId == null)  // MES Task
        {
            // Declare a new encoding job with the Standard encoder
            job = _context.Jobs.Create("Azure Function - MES Job");
            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");

            // Change or modify the custom preset JSON used here.
            // string preset = File.ReadAllText("D:\home\site\wwwroot\Presets\H264 Multiple Bitrate 720p.json");

            // Create a task with the encoding details, using a string preset.
            // In this case "H264 Multiple Bitrate 720p" system defined preset is used.
            taskEncoding = job.Tasks.AddNew("My encoding task",
               processor,
               "H264 Multiple Bitrate 720p",
               TaskOptions.None);

            // Specify the input asset to be encoded.
            taskEncoding.InputAssets.Add(asset);
        }
        else // Premium Encoder Task
        {

            //find the workflow asset
            string workflowassetid = (string)data.WorkflowAssetId;
            IAsset workflowAsset = _context.Assets.Where(a => a.Id == workflowassetid).FirstOrDefault();

            if (workflowAsset == null)
            {
                log.Info($"Workflow not found {workflowassetid}");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Workflow not found"
                });
            }

            // Declare a new job.
            job = _context.Jobs.Create("Premium Encoder Job");

            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Premium Workflow");

            string premiumConfiguration = "";
            // In some cases, a configuration can be loaded and passed it to the task to tuned the workflow
            // premiumConfiguration=File.ReadAllText(@"D:\home\site\wwwroot\Presets\SetRuntime.xml").Replace("VideoFileName", VideoFile.Name).Replace("AudioFileName", AudioFile.Name);

            // Create a task
            taskEncoding = job.Tasks.AddNew("Premium Workflow encoding task",
               processor,
               premiumConfiguration,
               TaskOptions.None);

            log.Info("task created");

            // Specify the input asset to be encoded.
            taskEncoding.InputAssets.Add(workflowAsset); // first add the Workflow
            taskEncoding.InputAssets.Add(asset); // Then add the video asset
        }

        // Add an output asset to contain the results of the job. 
        // This output is specified as AssetCreationOptions.None, which 
        // means the output asset is not encrypted. 
        taskEncoding.OutputAssets.AddNew(asset.Name + " encoded", AssetCreationOptions.None);

        if (data.IndexV1Language != null)  // Indexing v1 task
        {
            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processorIndex1 = GetLatestMediaProcessorByName("Azure Media Indexer");

            string indexer1Configuration = File.ReadAllText(@"D:\home\site\wwwroot\Presets\IndexerV1.xml").Replace("English", (string)data.IndexV1Language);

            // Create a task with the encoding details, using a string preset.
            taskIndex1 = job.Tasks.AddNew("My Indexing v1 Task",
               processorIndex1,
               indexer1Configuration,
               TaskOptions.None);

            // Specify the input asset to be indexed.
            taskIndex1.InputAssets.Add(asset);

            // Add an output asset to contain the results of the job.
            taskIndex1.OutputAssets.AddNew("My Indexing v1 Output Asset", AssetCreationOptions.None);
        }
        if (data.IndexV2Language != null)  // Indexing v1 task
        {
            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processorIndex2 = GetLatestMediaProcessorByName("Azure Media Indexer 2 Preview");

            string indexer2Configuration = File.ReadAllText(@"D:\home\site\wwwroot\Presets\IndexerV2.json").Replace("EnUs", (string)data.IndexV2Language);

            // Create a task with the encoding details, using a string preset.
            taskIndex2 = job.Tasks.AddNew("My Indexing v2 Task",
               processorIndex2,
               indexer2Configuration,
               TaskOptions.None);

            // Specify the input asset to be indexed.
            taskIndex2.InputAssets.Add(asset);

            // Add an output asset to contain the results of the job.
            taskIndex2.OutputAssets.AddNew("My Indexing v2 Output Asset", AssetCreationOptions.None);
        }

        job.Submit();
        log.Info("Job Submitted");

        outputassetencoded = job.OutputMediaAssets.FirstOrDefault();
        outputassetindex1 = taskIndex1 != null ? job.OutputMediaAssets[1] : null;
        outputassetindex2 = taskIndex2 != null ? job.OutputMediaAssets.LastOrDefault() : null;
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info("Job Id: " + job.Id);
    log.Info("Output asset Id: " + outputassetencoded.Id);

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        JobId = job.Id,
        OutputAssetId = outputassetencoded.Id,
        OutputAssetIndexV1Id = outputassetindex1 != null ? outputassetindex1.Id : "",
        OutputAssetIndexV2Id = outputassetindex2 != null ? outputassetindex2.Id : ""

    });
}




