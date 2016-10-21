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
using System.Web.Http;

// Read values from the App.config file.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

static string _sourceStorageAccountName = Environment.GetEnvironmentVariable("SourceStorageAccountName");
static string _sourceStorageAccountKey = Environment.GetEnvironmentVariable("SourceStorageAccountKey");
static string _sourceStorageContainer = Environment.GetEnvironmentVariable("SourceStorageContainer");

private static CloudStorageAccount _destinationStorageAccount = null;

// Set the output container name here.
private static string _outputContainerName = "output";

// default MES Preset
private static string _MESPresetName = "H264 Multiple Bitrate 720p";

// Delete source file
private static bool _DeleteSourceFileIfSuccess = false;


// Field for service context.
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;


public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    // INPUT:
    // required data.fileName (string) Example: "movie.mp4" 
    // optional data.MESPresetName (string) default "H264 Multiple Bitrate 720p"
    // optional data.DeleteSourceFileIfSuccess (bool) default false


    log.Info($"Webhook was triggered!");

    var MyController = new AsyncController();
    Guid id = Guid.NewGuid();  //Generate tracking Id
    var res = MyController.longrunningtask(req, log, id);

    log.Info($"response generated");

    //new Thread(() => doWork(id, req, log)).Start();   //Start the thread of work, but continue on before it completes
    // HttpResponseMessage responseMessage = req.CreateResponse(HttpStatusCode.Accepted);
    // responseMessage.Headers.Add("location", String.Format("{0}://{1}/api/status/{2}", req.RequestUri.Scheme, req.RequestUri.Host, id));  //Where the engine will poll to check status
    //responseMessage.Headers.Add("retry-after", "20");   //How many seconds it should wait (20 is default if not included)
    return res;

  

}

private static async void doWork(Guid id, HttpRequestMessage req, TraceWriter log)
{

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);


    // Optional MES Preset
    if (data.MESPresetName != null)
    {
        _MESPresetName = data.MESPresetName;
    }

    // Optional Delete Source File
    if (data.DeleteSourceFileIfSuccess != null)
    {
        _DeleteSourceFileIfSuccess = data.DeleteSourceFileIfSuccess;
    }

    /*
    if (data.fileName == null)
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass fileName property in the input object"
        });
    }
    */

    string fileName = data.fileName;

    try
    {
        // Create and cache the Media Services credentials in a static class variable.
        _cachedCredentials = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

        // Used the chached credentials to create CloudMediaContext.
        _context = new CloudMediaContext(_cachedCredentials);

        log.Info("fileName: {fileName}");

        // let get the inputBlob of the new file
        StorageCredentials SourceStorageCredentials = new StorageCredentials(_sourceStorageAccountName, _sourceStorageAccountKey);
        CloudStorageAccount sourceStorageAccount = new CloudStorageAccount(SourceStorageCredentials, false);
        CloudBlobClient sourceBlobStorageClient = sourceStorageAccount.CreateCloudBlobClient();
        CloudBlobContainer assetContainer = sourceBlobStorageClient.GetContainerReference(_sourceStorageContainer);
        CloudBlockBlob inputBlob = assetContainer.GetBlockBlobReference(fileName);


        // Step 1:  Copy the Blob into a new Input Asset for the Job
        // using code from this sample - https://azure.microsoft.com/en-us/documentation/articles/media-services-copying-existing-blob/

        IAsset newAsset = CreateAssetFromBlob(inputBlob, fileName, log).GetAwaiter().GetResult();

        // Step 2: Create an Encoding Job

        // Declare a new encoding job with the Standard encoder
        IJob job = _context.Jobs.Create("Azure Function - MES Job");
        // Get a media processor reference, and pass to it the name of the 
        // processor to use for the specific task.
        IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");

        // Create a task with the encoding details, using a string preset.
        // Default H264 Multiple Bitrate 720p" system defined preset is used if no preset was passed to the function.
        ITask task = job.Tasks.AddNew("My encoding task",
            processor,
            _MESPresetName,
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

        // Delete the source file if user want to
        if (_DeleteSourceFileIfSuccess)
        {
            inputBlob.DeleteIfExists();
        }
    }
    catch (Exception ex)
    {
        log.Error("ERROR: failed.");
        log.Info($"StackTrace : {ex.StackTrace}");

        /*
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = ex.StackTrace
        });
        */
    }


    /*

        return req.CreateResponse(HttpStatusCode.OK, new
        {
            greeting = $"Ok {data.Source}, {data.Mode} mode"
        });
        */

    runningTasks[id] = true;  //Set the flag to true - work done
}


public class AsyncController : ApiController
{
    //State dictionary for sample - stores the state of the working thread
    private static Dictionary<Guid, bool> runningTasks = new Dictionary<Guid, bool>();
    private static TraceWriter _log;


    /// <summary>
    /// This is the method that starts the task running.  It creates a new thread to complete the work on, and returns an ID which can be passed in to check the status of the job.  
    /// In a real world scenario your dictionary may contain the object you want to return when the work is done.
    /// </summary>
    /// <returns>HTTP Response with needed headers</returns>

    public async Task<HttpResponseMessage> longrunningtask(HttpRequestMessage req, TraceWriter log)
    {
        Guid id = Guid.NewGuid();  //Generate tracking Id
        runningTasks[id] = false;  //Job isn't done yet
        new Thread(() => doWork(id, log)).Start();   //Start the thread of work, but continue on before it completes

        HttpResponseMessage responseMessage = req.CreateResponse(HttpStatusCode.Accepted);
        responseMessage.Headers.Add("location", String.Format("{0}://{1}/api/status/{2}", req.RequestUri.Scheme, req.RequestUri.Host, id));  //Where the engine will poll to check status
        responseMessage.Headers.Add("retry-after", "20");   //How many seconds it should wait (20 is default if not included)
        return responseMessage;
    }


    /// <summary>
    /// This is where the actual long running work would occur.
    /// </summary>
    /// <param name="id"></param>
    private void doWork(Guid id, TraceWriter log)
    {
        _log = log;
        log.Error("Starting work");
        Task.Delay(120000).Wait(); //Do work will work for 120 seconds)
        log.Error("Work completed");
        runningTasks[id] = true;  //Set the flag to true - work done
    }

    /// <summary>
    /// Method to check the status of the job.  This is where the location header redirects to.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("api/status/{id}")]
    public HttpResponseMessage checkStatus([FromUri] Guid id)
    {
        _log.Error("Logic Apps check status");

        //If the job is complete
        if (runningTasks.ContainsKey(id) && runningTasks[id])
        {
            runningTasks.Remove(id);
            return Request.CreateResponse(HttpStatusCode.OK, "Some data could be returned here");
        }
        //If the job is still running
        else if (runningTasks.ContainsKey(id))
        {
            HttpResponseMessage responseMessage = Request.CreateResponse(HttpStatusCode.Accepted);
            responseMessage.Headers.Add("location", String.Format("{0}://{1}/api/status/{2}", Request.RequestUri.Scheme, Request.RequestUri.Host, id));  //Where the engine will poll to check status
            responseMessage.Headers.Add("retry-after", "20");
            return responseMessage;
        }
        else
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "No job exists with the specified ID");
        }
    }
}
