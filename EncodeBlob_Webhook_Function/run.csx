#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#load "../Shared/copyBlobHelpers.csx"
#load "../Shared/mediaServicesHelpers.csx"

using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;

// Read values from the App.config file.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");
private static CloudStorageAccount _destinationStorageAccount = null;

// Set the output container name here.
private static string _outputContainerName = "output";

// Field for service context.
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    // data.fileName  "movie.mp4"
  

    if (data.filename == null)
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass fileName property in the input object"
        });
    }
    else
    {
        string fileName = data.fileName;

        try
        {
            // Create and cache the Media Services credentials in a static class variable.
            _cachedCredentials = new MediaServicesCredentials(
                            _mediaServicesAccountName,
                            _mediaServicesAccountKey);

            // Used the chached credentials to create CloudMediaContext.
            _context = new CloudMediaContext(_cachedCredentials);

            // Step 1:  Copy the Blob into a new Input Asset for the Job
            // ***NOTE: Ideally we would have a method to ingest a Blob directly here somehow. 
            // using code from this sample - https://azure.microsoft.com/en-us/documentation/articles/media-services-copying-existing-blob/

            IAsset newAsset = CreateAssetFromBlob(inputBlob, fileName, log).GetAwaiter().GetResult();


            // Step 2: Create an Encoding Job


            // Declare a new encoding job with the Standard encoder
            IJob job = _context.Jobs.Create("Azure Function - MES Job");
            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");

            // Change or modify the custom preset JSON used here.
            // string preset = File.ReadAllText("D:\home\site\wwwroot\Presets\H264 Multiple Bitrate 720p.json");

            // Create a task with the encoding details, using a string preset.
            // In this case "H264 Multiple Bitrate 720p" system defined preset is used.
            ITask task = job.Tasks.AddNew("My encoding task",
                processor,
                "H264 Multiple Bitrate 720p",
                TaskOptions.None);

            // Specify the input asset to be encoded.
            task.InputAssets.Add(newAsset);

            // Add an output asset to contain the results of the job. 
            // This output is specified as AssetCreationOptions.None, which 
            // means the output asset is not encrypted. 
            task.OutputAssets.AddNew(fileName, AssetCreationOptions.None);

            job.Submit();
            log.Info("Job Submitted");

            // Step 3: Monitor the Job
            // ** NOTE:  We could just monitor in this function, or create another function that monitors the Queue
            //           or WebHook based notifications. We should create both samples in this project. 
            while (true)
            {
                job.Refresh();
                // Refresh every 5 seconds
                Thread.Sleep(5000);
                log.Info($"Job ID:{job.Id} State: {job.State.ToString()}");

                if (job.State == JobState.Error || job.State == JobState.Finished || job.State == JobState.Canceled)
                    break;
            }

            if (job.State == JobState.Finished)
                log.Info($"Job {job.Id} is complete.");
            else if (job.State == JobState.Error)
            {
                log.Error("Job Failed with Error. ");
                throw new Exception("Job failed encoding .");
            }

            // Step 4: Output the resulting asset to another location - the output Container - so that 
            //         another function could pick up the results of the job. 

            IAsset outputAsset = job.OutputMediaAssets[0];
            log.Info($"Output Asset  Id:{outputAsset.Id}");

            //Get a reference to the storage account that is associated with the Media Services account. 
            StorageCredentials mediaServicesStorageCredentials =
                new StorageCredentials(_storageAccountName, _storageAccountKey);
            _destinationStorageAccount = new CloudStorageAccount(mediaServicesStorageCredentials, false);

            IAccessPolicy readPolicy = _context.AccessPolicies.Create("readPolicy",
            TimeSpan.FromHours(4), AccessPermissions.Read);
            ILocator outputLocator = _context.Locators.CreateLocator(LocatorType.Sas, outputAsset, readPolicy);
            CloudBlobClient destBlobStorage = _destinationStorageAccount.CreateCloudBlobClient();

            // Get the asset container reference
            string outContainerName = (new Uri(outputLocator.Path)).Segments[1];
            CloudBlobContainer outContainer = destBlobStorage.GetContainerReference(outContainerName);
            CloudBlobContainer targetContainer = inputBlob.Container.ServiceClient.GetContainerReference(_outputContainerName);

            log.Info($"TargetContainer = {targetContainer.Name}");
            CopyBlobsToTargetContainer(outContainer, targetContainer, log).Wait();

        }
        catch (Exception ex)
        {
            log.Error("ERROR: failed.");
            log.Info($"StackTrace : {ex.StackTrace}");
            throw ex;
        }
    }

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        greeting = $"Ok {data.Source}, {data.Mode} mode"
    });
}
