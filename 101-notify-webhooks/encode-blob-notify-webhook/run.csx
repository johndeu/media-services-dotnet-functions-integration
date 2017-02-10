#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
#load "../helpers/copyBlobHelpers.csx"
#load "../helpers/mediaServicesHelpers.csx"

using System;
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

// NOTE: You have to update the WebHookEndpoint and Signing Key that you wish to use in the AppSettings to match
//       your deployed Notification_Webhook_Function. After deployment, you will have a unique endpoint. 
static string _webHookEndpoint = Environment.GetEnvironmentVariable("WebHookEndpoint");
static string _signingKey = Environment.GetEnvironmentVariable("SigningKey");

private static CloudStorageAccount _destinationStorageAccount = null;

// Field for service context.
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;

public static void Run(CloudBlockBlob inputBlob, string fileName, string fileExtension, CloudBlockBlob outputBlob, TraceWriter log)
{
    // NOTE that the variables {fileName} and {fileExtension} here come from the path setting in function.json
    // and are passed into the  Run method signature above. We can use this to make decisions on what type of file
    // was dropped into the input container for the function. 

    // No need to do any Retry strategy in this function, By default, the SDK calls a function up to 5 times for a 
    // given blob. If the fifth try fails, the SDK adds a message to a queue named webjobs-blobtrigger-poison.

    log.Info($"C# Blob  trigger  function processed: {fileName}.{fileExtension}");
    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    // Use this key to sign WebHook requests with
    // Example Key value: wOlDEUJ4/VN1No8HxVxpsRvej0DZrO5DXvImGLjFhfctPGFiMkUA0Cj8HSfJW7lePX9XsfHAMhw30p0yYqG+1A== 

    byte[] keyBytes = Convert.FromBase64String(_signingKey);

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
        
        StorageCredentials mediaServicesStorageCredentials =
            new StorageCredentials(_storageAccountName, _storageAccountKey);

        IAsset newAsset = CreateAssetFromBlob(inputBlob, fileName, log).GetAwaiter().GetResult();
        
        // Step 2: Create an Encoding Job

        // Check for existing Notification Endpoint with the name "FunctionWebHook"
        var existingEndpoint = _context.NotificationEndPoints.Where(e=>e.Name == "FunctionWebHook").FirstOrDefault();
        INotificationEndPoint endpoint = null;

        if (existingEndpoint != null){
            log.Info ("webhook endpoint already exists");
            endpoint = (INotificationEndPoint)existingEndpoint;
        }
        else{
            endpoint = _context.NotificationEndPoints.Create("FunctionWebHook", 
                    NotificationEndPointType.WebHook, _webHookEndpoint, keyBytes); 
            log.Info($"Notification Endpoint Created with Key : {keyBytes.ToString()}");
        }

        // Declare a new encoding job with the Standard encoder
        IJob job = _context.Jobs.Create("Azure Function - MES Job");

        // Get a media processor reference, and pass to it the name of the 
        // processor to use for the specific task.
        IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");

        // Read in custom preset string
        string preset = File.ReadAllText(@"D:\home\site\wwwroot\Presets\singleMP4.json");

        // Create a task with the encoding details, using a custom preset
        ITask task = job.Tasks.AddNew("Encode with Custom Preset",
            processor,
            preset,
            TaskOptions.None);

        // Specify the input asset to be encoded.
        task.InputAssets.Add(newAsset);

        // Add an output asset to contain the results of the job. 
        // This output is specified as AssetCreationOptions.None, which 
        // means the output asset is not encrypted. 
        task.OutputAssets.AddNew(fileName, AssetCreationOptions.None);

        // Add the WebHook notification to this Task and request all notification state changes
        if (endpoint != null){
            task.TaskNotificationSubscriptions.AddNew(NotificationJobState.All, endpoint, true);
            log.Info($"Created Notification Subscription for endpoint: {_webHookEndpoint}");
        }else{
            log.Error("No Notification Endpoint is being used");
        }
        
        job.Submit();
        log.Info("Job Submitted");

        // Step 3: Monitor the Job
        // ** NOTE:  We could just monitor in this function, or create another function that monitors the Queue
        //           or WebHook based notifications. See the Notification_Webhook_Function project.

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
        log.Info($"Output Asset Id:{outputAsset.Id}");

        //Get a reference to the storage account that is associated with the Media Services account. 
        _destinationStorageAccount = new CloudStorageAccount(mediaServicesStorageCredentials, false);

        IAccessPolicy readPolicy = _context.AccessPolicies.Create("readPolicy",
        TimeSpan.FromHours(4), AccessPermissions.Read);
        ILocator outputLocator = _context.Locators.CreateLocator(LocatorType.Sas, outputAsset, readPolicy);
        CloudBlobClient destBlobStorage = _destinationStorageAccount.CreateCloudBlobClient();

        // Get the asset container reference
        string outContainerName = (new Uri(outputLocator.Path)).Segments[1];
        CloudBlobContainer outContainer = destBlobStorage.GetContainerReference(outContainerName);

        log.Info($"Getting Output Blob from : {outContainer.Name}");

        CloudBlockBlob jobOutput = null;

            // Get only the single MP4 output file. 
            var blobs = outContainer.ListBlobs().OfType<CloudBlob>()
                        .Where(b=>b.Name.ToLower().EndsWith(".mp4"));


        foreach (var blob in blobs)
        {
            log.Info($"Blob URI:  {blob.Uri}");
            if (blob is CloudBlockBlob)
            {
                jobOutput = (CloudBlockBlob)blob;
                break;
            }
        }

        CopyBlob(jobOutput, outputBlob).Wait();
        log.Info("Copy Blob succeeded.");

        // Change some settings on the output blob.
        outputBlob.Metadata["Custom1"] = "Some Custom Metadata";
        outputBlob.Properties.ContentType = "video/mp4";
        outputBlob.SetProperties();

        log.Info("Done!");

    }
    catch (Exception ex)
    {
        log.Error("ERROR: failed.");
        log.Info($"StackTrace : {ex.StackTrace}");
        throw ex;
    }
}