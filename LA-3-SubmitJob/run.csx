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

private static int _taskindex = 0;


// Submit an encoding job
// Required : data.AssetId (Example : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc")
// with MES if MESPreset is specified (Example : "H264 Multiple Bitrate 720p"). If MESPreset contains an extension "H264 Multiple Bitrate 720p with thumbnail.json" then it loads this file from D:\home\site\wwwroot\Presets 
// with Premium Encoder if WorkflowAssetId is specified (Example : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc")
// with Indexer v1 if IndexV1Language is specified (Example : "English")
// with Indexer v2 if IndexV2Language is specified (Example : "EnUs")
// with Video OCR if OCRLanguage is specified (Example: "AutoDetect" or "English")
// with Face Detection if FaceDetectionMode is specified (Example : "PerFaceEmotion")
// with Motion Detection if MotionDetectionLevel is specified (Example : "medium")
// with Video Summarization if SummarizationDuration is specified (Example : "0.0" for automatic)
// with Hyperlapse if HyperlapseSpeed is specified (Example : "8" for speed x8)


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
    ITask taskEncoding = null;

    int OutputMES = -1;
    int OutputPremium = -1;
    int OutputIndex1 = -1;
    int OutputIndex2 = -1;
    int OutputOCR = -1;
    int OutputFace = -1;
    int OutputMotion = -1;
    int OutputSummarization = -1;
    int OutputHyperlapse = -1;

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

        if (data.MESPreset != null)  // MES Task
        {
            // Declare a new encoding job with the Standard encoder
            job = _context.Jobs.Create("Azure Function - MES Job");
            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processorMES = GetLatestMediaProcessorByName("Media Encoder Standard");

            string preset = data.MESPreset;

            if (preset.ToUpper().EndsWith(".JSON"))
            {
                // Change or modify the custom preset JSON used here.
                preset = File.ReadAllText(@"D:\home\site\wwwroot\Presets\" + preset);
            }

            // Create a task with the encoding details, using a string preset.
            // In this case "H264 Multiple Bitrate 720p" system defined preset is used.
            taskEncoding = job.Tasks.AddNew("MES encoding task",
               processorMES,
               preset,
               TaskOptions.None);

            // Specify the input asset to be encoded.
            taskEncoding.InputAssets.Add(asset);
            OutputMES = _taskindex++;

            // Add an output asset to contain the results of the job. 
            // This output is specified as AssetCreationOptions.None, which 
            // means the output asset is not encrypted. 
            taskEncoding.OutputAssets.AddNew(asset.Name + " MES encoded", AssetCreationOptions.None);
        }

        if (data.WorkflowAssetId != null)// Premium Encoder Task
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

            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processorMEPW = GetLatestMediaProcessorByName("Media Encoder Premium Workflow");

            string premiumConfiguration = "";
            // In some cases, a configuration can be loaded and passed it to the task to tuned the workflow
            // premiumConfiguration=File.ReadAllText(@"D:\home\site\wwwroot\Presets\SetRuntime.xml").Replace("VideoFileName", VideoFile.Name).Replace("AudioFileName", AudioFile.Name);

            // Create a task
            taskEncoding = job.Tasks.AddNew("Premium Workflow encoding task",
               processorMEPW,
               premiumConfiguration,
               TaskOptions.None);

            log.Info("task created");

            // Specify the input asset to be encoded.
            taskEncoding.InputAssets.Add(workflowAsset); // first add the Workflow
            taskEncoding.InputAssets.Add(asset); // Then add the video asset
            OutputPremium = _taskindex++;

            // Add an output asset to contain the results of the job. 
            // This output is specified as AssetCreationOptions.None, which 
            // means the output asset is not encrypted. 
            taskEncoding.OutputAssets.AddNew(asset.Name + " Premium encoded", AssetCreationOptions.None);
        }


        // Media Analytics
        OutputIndex1 = AddTask(job, asset, (string)data.IndexV1Language, "Azure Media Indexer", "IndexerV1.xml", "English");
        OutputIndex2 = AddTask(job, asset, (string)data.IndexV2Language, "Azure Media Indexer 2 Preview", "IndexerV2.json", "EnUs");
        OutputOCR = AddTask(job, asset, (string)data.OCRLanguage, "Azure Media OCR", "OCR.json", "AutoDetect");
        OutputFace = AddTask(job, asset, (string)data.FaceDetectionMode, "Azure Media Face Detector", "FaceDetection.json", "PerFaceEmotion");
        OutputMotion = AddTask(job, asset, (string)data.MotionDetectionLevel, "Azure Media Motion Detector", "MotionDetection.json", "medium");
        OutputSummarization = AddTask(job, asset, (string)data.SummarizationDuration, "Azure Media Video Thumbnails", "Summarization.json", "0.0");
        OutputHyperlapse = AddTask(job, asset, (string)data.HyperlapseSpeed, "Azure Media Hyperlapse", "Hyperlapse.json", "8");

        job.Submit();
        log.Info("Job Submitted");
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info("Job Id: " + job.Id);
    log.Info("OutputAssetMESId: ", ReturnId(job, OutputMES));
    log.Info("OutputAssetMEPWId: ", ReturnId(job, OutputPremium));
    log.Info("OutputAssetIndexV1Id: ", ReturnId(job, OutputIndex1));
    log.Info("OutputAssetIndexV2Id: ", ReturnId(job, OutputIndex2));
    log.Info("OutputAssetOCRId: ", ReturnId(job, OutputOCR));
    log.Info("OutputAssetFaceDetectionId: ", ReturnId(job, OutputFace));
    log.Info("OutputAssetMotionDetectionId: ", ReturnId(job, OutputMotion));
    log.Info("OutputAssetSummarizationId: ", ReturnId(job, OutputSummarization));
    log.Info("OutputAssetHyperlapseId: ", ReturnId(job, OutputHyperlapse));

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        JobId = job.Id,
        OutputAssetMESId = ReturnId(job, OutputMES),
        OutputAssetMEPWId = ReturnId(job, OutputPremium),
        OutputAssetIndexV1Id = ReturnId(job, OutputIndex1),
        OutputAssetIndexV2Id = ReturnId(job, OutputIndex2),
        OutputAssetOCRId = ReturnId(job, OutputOCR),
        OutputAssetFaceDetectionId = ReturnId(job, OutputFace),
        OutputAssetMotionDetectionId = ReturnId(job, OutputMotion),
        OutputAssetSummarizationId = ReturnId(job, OutputSummarization),
        OutputAssetHyperlapseId = ReturnId(job, OutputHyperlapse),
    });
}

public static string ReturnId(IJob job, int index)
{
    return index > -1 ? job.OutputMediaAssets[index].Id : "";
}

public static int AddTask(IJob job, IAsset sourceAsset, string value, string processor, string presetfilename, string stringtoreplace)
{
    if (value != null)
    {
        // Get a media processor reference, and pass to it the name of the 
        // processor to use for the specific task.
        IMediaProcessor mediaProcessor = GetLatestMediaProcessorByName(processor);

        string Configuration = File.ReadAllText(@"D:\home\site\wwwroot\Presets\" + presetfilename).Replace(stringtoreplace, value);

        // Create a task with the encoding details, using a string preset.
        var task = job.Tasks.AddNew(processor + " task",
           mediaProcessor,
           Configuration,
           TaskOptions.None);

        // Specify the input asset to be indexed.
        task.InputAssets.Add(sourceAsset);

        // Add an output asset to contain the results of the job.
        task.OutputAssets.AddNew(processor + " Output Asset", AssetCreationOptions.None);

        return _taskindex++;
    }
    else
    {
        return -1;
    }
}




