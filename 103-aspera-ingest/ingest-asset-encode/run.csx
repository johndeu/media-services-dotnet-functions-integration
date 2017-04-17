/*
This function monitors a storage account container location folder named "input" for new MP4 files.
These files may be uploaded by the Aspera On Demand service available through the Azure Marketplace.
The azuredeploy.json template is configured to create all of the resources, including the Aspera On Demand service
which is seperately licensed.  

Once a file is uploaded through the Aspera Client or Aspera Drive, this Function will trigger the ingest and creation of
a new Media Services Asset. 

In addition this function will trigger a basic encoding job and create a new encoded Asset in Media Services.

*/

#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
#r "System.Web"
#load "../helpers/copyBlobHelpers.csx"
#load "../helpers/mediaServicesHelpers.csx"

using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;

// Read values from the App.config file.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");
static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");


private static CloudStorageAccount _destinationStorageAccount = null;

// Field for service context.
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;

public static void Run(CloudBlockBlob inputBlob, string fileName, string fileExtension, TraceWriter log)
{
    // NOTE that the variables {fileName} and {fileExtension} here come from the path setting in function.json
    // and are passed into the  Run method signature above. We can use this to make decisions on what type of file
    // was dropped into the input container for the function. 

    // No need to do any Retry strategy in this function, By default, the SDK calls a function up to 5 times for a 
    // given blob. If the fifth try fails, the SDK adds a message to a queue named webjobs-blobtrigger-poison.

    log.Info($"C# Blob trigger function processed: {fileName}.{fileExtension}");
    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");


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
        log.Info("Aspera Ingest: Asset created:" + newAsset.Id);

        log.Info("Deleting the source asset from the input container");
        inputBlob.DeleteIfExists();
        
        // Step 2: Create an Encoding Job and then exit the function. 

        // Declare a new encoding job with the Standard encoder
        IJob job = _context.Jobs.Create("Function - Encode blob single input");

        // Get a media processor reference, and pass to it the name of the 
        // processor to use for the specific task.
        IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");

        // Create a task with the encoding details, using a custom preset
        ITask task = job.Tasks.AddNew("Aspera Ingest Encode to Adaptive",
            processor,
            "Adaptive Streaming",
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
        //           or WebHook based notifications. See the Notification_Webhook_Function project.
        //           For any job that takes longer than 5 minutes, Functions will die, so it is better to monitor
        //           long running encode jobs in a seperate function, or use WebHook Notifications 
        //           from Media Services

        log.Info("Aspera Ingest Complete, Adaptive Bitrate Encode job is queued!");

    }
    catch (Exception ex)
    {
        log.Error("ERROR: failed.");
        log.Info($"StackTrace : {ex.StackTrace}");
        throw ex;
    }
}