#r "Newtonsoft.Json"
//#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
#load "../../Shared/mediaServicesHelpers.csx"
#load "../../Shared/copyBlobHelpers.csx"

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

    // for test
    data.VideoAssetId = "nb:cid:UUID:04354615-9833-489c-8750-916e8e5fe234";
    if (data.VideoAssetId == null)
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (VideoAssetId)"
        });
    }

    // for test
    data.WorkflowAssetId = "nb:cid:UUID:44fe8196-616c-4490-bf80-24d1e08754c5";
    if (data.WorkflowAssetId == null)
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass workflow asset ID in the input object (WorkflowAssetId)"
        });
    }

    

    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    IJob job = null;
    IAsset outputasset = null;
    
    try
    {
        // Create and cache the Media Services credentials in a static class variable.
        _cachedCredentials = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

        // Used the chached credentials to create CloudMediaContext.
        _context = new CloudMediaContext(_cachedCredentials);

        // find the Asset
        string videoAssetId = (string)data.VideoAssetId;
        IAsset videoAsset = _context.Assets.Where(a => a.Id == videoAssetId).FirstOrDefault();

        //find the workflow asset
         string workflowassetid = (string)data.WorkflowAssetId;
        IAsset workflowAsset = _context.Assets.Where(a => a.Id == workflowassetid).FirstOrDefault();

        if (videoAsset == null)
        {
            log.Info($"Asset not found {videoAssetId}");
            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not found"
            });
        }

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

            log.Info("Job created");
            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.

            IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Premium Workflow");
       
            string configurationFile=File.ReadAllText(@"D:\home\site\wwwroot\MergeMultilanguageAudio\SetRuntime.xml");

            var AudioFile = videoAsset.AssetFiles.Where(a=>a.Name.EndsWith(".mp3")).FirstOrDefault();
            if (AudioFile == null)
            {
                    log.Info($"Audio file not found");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Audio file not found"
                });
            }

            var VideoFile = videoAsset.AssetFiles.Where(a=>a.Name != AudioFile.Name).FirstOrDefault();
            if (VideoFile == null)
            {
                    log.Info($"Video file not found");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Video file not found"
                });
            }

            configurationFile.Replace("VideFileName", VideoFile.Name).Replace("AudioFileName", AudioFile.Name);

            // Create a task
            ITask task = job.Tasks.AddNew("Premium Workflow encoding task",
                processor,
                configurationFile,
                TaskOptions.None);

            log.Info("task created");

            // Specify the input asset to be encoded.
            task.InputAssets.Add(workflowAsset); // first add the Workflow
            task.InputAssets.Add(videoAsset); // Then add the video asset
            // Add an output asset to contain the results of the job. 
            // This output is specified as AssetCreationOptions.None, which 
            // means the output asset is not encrypted. 
            task.OutputAssets.AddNew("Output asset",
                AssetCreationOptions.None);
   
           // XDocument configurationXml = XDocument.Load(SetRuntime.xml);
   
          
           //  job = _context.Jobs.CreateWithSingleTask(
           //                                   "Media Encoder Premium Workflow",
           //                                   configurationFile,
           //                                   workflowAsset,
           //                                   videoAsset,
           //                                   "Output asset",
           //                                   AssetCreationOptions.None);
            // Launch the job.
            job.Submit();
            log.Info("Job Submitted");

            outputasset = job.OutputMediaAssets.FirstOrDefault();
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
         log.Info(ex.StackTrace);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info("Job Id: " + job.Id);
    log.Info("Output asset Id: " + outputasset.Id);

    return req.CreateResponse(HttpStatusCode.OK, new {
        JobId = job.Id,
        OutputAssetId = outputasset.Id
    });
}
