/*
This function check a task status.

Input:
{
    "jobId" : "nb:jid:UUID:1ceaa82f-2607-4df9-b034-cd730dad7097", // Mandatory, Id of the source asset
    "taskId" : "nb:tid:UUID:cdc25b10-3ed7-4005-bcf9-6222b35b5be3", // Mandatory
    "extendedInfo" : true // optional. Returns ams account unit size, nb units, nb of jobs in queue, scheduled and running states. Only if job is complete or error
 }

Output:
{
    "taskState" : 2, // The state of the task (int)
    "errorText" : "" // error(s) text if task state is error
    "startTime" :""
    "endTime" : "",
    "runningDuration" : ""
    "extendedInfo" :  // if extendedInfo is true and job is finished or in error
    {
        mediaUnitNumber = 2,
        mediaUnitSize = "S2",
        otherJobsProcessing = 2;
        otherJobsScheduled = 1;
        otherJobsQueue = 1;
    }
 }
*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"

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
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;


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

    if (data.jobId == null || data.taskId == null)
    {
        // used to test the function
        //data.jobId = "nb:jid:UUID:acf38b8a-aef9-4789-9f0f-f69bf6ccb8e5";
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass the job and task ID in the input object (jobId, taskId)"
        });
    }

    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    IJob job = null;
    ITask task = null;
    string startTime = "";
    string endTime = "";
    StringBuilder sberror = new StringBuilder();
    string runningDuration = "";


    bool extendedInfo = false;
    if (data.extendedInfo != null && ((bool)data.extendedInfo) == true)
    {
        extendedInfo = true;
    }

    try
    {
        // Create and cache the Media Services credentials in a static class variable.
        _cachedCredentials = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

        // Used the chached credentials to create CloudMediaContext.
        _context = new CloudMediaContext(_cachedCredentials);

        // Get the job
        string jobid = (string)data.jobId;
        job = _context.Jobs.Where(j => j.Id == jobid).FirstOrDefault();

        if (job == null)
        {
            log.Info($"Job not found {jobid}");

            return req.CreateResponse(HttpStatusCode.InternalServerError, new
            {
                error = "Job not found"
            });
        }

        // Get the task
        string taskid = (string)data.taskId;
        task = job.Tasks.Where(j => j.Id == taskid).FirstOrDefault();

        if (task == null)
        {
            log.Info($"Task not found {taskid}");

            return req.CreateResponse(HttpStatusCode.InternalServerError, new
            {
                error = "Task not found"
            });
        }


        for (int i = 1; i <= 3; i++) // let's wait 3 times 5 seconds (15 seconds)
        {
            log.Info($"Task {task.Id} status is {task.State}");

            if (task.State == JobState.Finished || task.State == JobState.Canceled || task.State == JobState.Error)
            {
                break;
            }

            log.Info("Waiting 5 s...");
            System.Threading.Thread.Sleep(5 * 1000);
            task = job.Tasks.Where(j => j.Id == taskid).FirstOrDefault();
        }

        if (task.State == JobState.Error)
        {
            foreach (var details in task.ErrorDetails)
            {
                sberror.AppendLine(details.Message);
            }
        }

        if (task.StartTime != null) startTime = ((DateTime)task.StartTime).ToString("o");

        if (task.EndTime != null) endTime = ((DateTime)task.EndTime).ToString("o");

        if (task.RunningDuration != null) runningDuration = task.RunningDuration.ToString();

    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.InternalServerError, new
        {
            Error = ex.ToString()
        });
    }

    if (extendedInfo && (task.State == JobState.Finished || task.State == JobState.Canceled || task.State == JobState.Error))
    {
        dynamic stats = new JObject();
        stats.mediaUnitNumber = _context.EncodingReservedUnits.FirstOrDefault().CurrentReservedUnits;
        stats.mediaUnitSize = ReturnMediaReservedUnitName(_context.EncodingReservedUnits.FirstOrDefault().ReservedUnitType); ;
        stats.otherJobsProcessing = _context.Jobs.Where(j => j.State == JobState.Processing).Count();
        stats.otherJobsScheduled = _context.Jobs.Where(j => j.State == JobState.Scheduled).Count();
        stats.otherJobsQueue = _context.Jobs.Where(j => j.State == JobState.Queued).Count();

        return req.CreateResponse(HttpStatusCode.OK, new
        {
            taskState = task.State,
            errorText = sberror.ToString(),
            startTime = startTime,
            endTime = endTime,
            runningDuration = runningDuration,
            extendedInfo = stats.ToString()
        });
    }
    else
    {
        return req.CreateResponse(HttpStatusCode.OK, new
        {
            taskState = task.State,
            errorText = sberror.ToString(),
            startTime = startTime,
            endTime = endTime,
            runningDuration = runningDuration
        });
    }
}

// Return the new name of Media Reserved Unit
public static string ReturnMediaReservedUnitName(ReservedUnitType unitType)
{
    switch (unitType)
    {
        case ReservedUnitType.Basic:
        default:
            return "S1";

        case ReservedUnitType.Standard:
            return "S2";

        case ReservedUnitType.Premium:
            return "S3";

    }
}




