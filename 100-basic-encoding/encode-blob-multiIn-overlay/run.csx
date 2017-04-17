/*
This function monitors a storage account container location folder named "input" for new MP4 files and a JSON
file that indicates the mulitple files to ingest into an Asset.  The files should be uploaded first, followed
by the JSON file last to trigger the funciton processing. Once the JSON file  is dropped into the storage 
container, the blob trigger will execute the function.

This sample shows how to ingest multiple assets into Media Services,  submit a job running Media Encoder Standard with 
multiple inputs. This is very useful for encoding jobs that require an overlay image on a video. 

In this sample, the expected JSON must contain a single MP4 file, and an image to overlay called "Logo.png".

Sample JSON File (extension must be .json (in lowercase))
[{
    "fileName": "BigBuckBunny.mp4",
    "isPrimary": true
  },
  {
    "fileName": "Logo.png"
  }
]

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
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json;


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


public static void Run(CloudBlockBlob inputBlob, TraceWriter log, string fileName)
{
    // NOTE that the variable {fileName} here come from the path setting in function.json
    // and is passed into the  Run method signature above. We can use this to make decisions on what type of file
    // was dropped into the input container for the function. 

    // No need to do any Retry strategy in this function, By default, the SDK calls a function up to 5 times for a 
    // given blob. If the fifth try fails, the SDK adds a message to a queue named webjobs-blobtrigger-poison.


    log.Info($"C# Blob trigger  function processed: {fileName}.json");
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

        // get files from the json file

        List<assetfileinJson> items = new List<assetfileinJson>();
        try
        {
            using (var stream = inputBlob.OpenRead())
            {

                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        string json = reader.ReadToEnd();
                        items = JsonConvert.DeserializeObject<List<assetfileinJson>>(json);
                    }
                }
            }
        }

        catch (Exception ex)
        {
            log.Error(ex.Message);
            log.Info(ex.StackTrace);
            log.Info("Reading JSON failed.");
            throw;
        }

        IAsset newAsset = CreateAssetFromBlobMultipleFiles(inputBlob, fileName, log, items).GetAwaiter().GetResult();

        // Step 2: Create an Encoding Job

        // Declare a new encoding job with the Standard encoder
        IJob job = _context.Jobs.Create("Function - Encode blob multi-in");
        // Get a media processor reference, and pass to it the name of the 
        // processor to use for the specific task.
        IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");


        // Read in custom preset string
        string homePath = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
        log.Info("Home= " + homePath);
        string presetPath;
        
        if (homePath == String.Empty){
            presetPath = @"../presets/MesWithOverlay.json"; 
        }else{
            presetPath =  Path.Combine(homePath, @"site\repository\100-basic-encoding\presets\MesWithOverlay.json");
        }
        log.Info($"Preset path : {presetPath}");
        string preset = File.ReadAllText(presetPath);

   
        // Create a task with the encoding details, using a string preset.
        // In this case a local preset is loaded. It includes an overlay with Logo.png which should be present in the asset
        ITask task = job.Tasks.AddNew("My encoding task",
            processor,
            preset,
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


private static string GetScriptPath()
    => Path.Combine(GetEnvironmentVariable("HOME"), @"site\wwwroot");

private static string GetEnvironmentVariable(string name)
    => System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);