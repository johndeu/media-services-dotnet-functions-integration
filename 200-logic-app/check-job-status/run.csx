/*
This function chevck a job status.

Input:
{
    "jobId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "extendedInfo" : true // optional. Returns ams account unit size, nb units, nb of jobs in queue, scheduled and running states. Only if job is complete or error
 }

Output:
{
    "jobState" : 2, // The state of the job (int)
    "errorText" : "" // error(s) text if job state is error
    "startTime" :""
    "endTime" : "",
    "runningDuration" : ""
    "mediaUnitNumber" : 2,   // if extendedInfo is true and job is finished or in error
    "mediaUnitSize" : "S2", // if extendedInfo is true and job is finished or in error
    "jobQueue" : 3, // if extendedInfo is true and job is finished or in error
    "jobScheduled" : 1, // if extendedInfo is true and job is finished or in error
    "jobProcessing" : 2, // if extendedInfo is true and job is finished or in error
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

    if (data.jobId == null)
    {
        // used to test the function
        //data.jobId = "nb:jid:UUID:acf38b8a-aef9-4789-9f0f-f69bf6ccb8e5";
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass the job ID in the input object (JobId)"
        });
    }

    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    IJob job = null;

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

            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Job not found"
            });
        }
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    for (int i = 1; i <= 3; i++) // let's wait 3 times 5 seconds (15 seconds)
    {
        log.Info($"Job {job.Id} status is {job.State}");

        if (job.State == JobState.Finished || job.State == JobState.Canceled || job.State == JobState.Error)
        {
            break;
        }

        log.Info("Waiting 5 s...");
        System.Threading.Thread.Sleep(5 * 1000);
        job = _context.Jobs.Where(j => j.Id == job.Id).FirstOrDefault();
    }

    StringBuilder sberror = new StringBuilder();
    if (job.State == JobState.Error)
    {
        foreach (var task in job.Tasks)
        {
            foreach (var details in task.ErrorDetails)
            {
                sberror.AppendLine(details.Message);
            }
        }
    }

    string startTime = "";
    if (job.StartTime != null) startTime = job.StartTime.ToString();

    string endTime = "";
    if (job.EndTime != null) endTime = job.EndTime.ToString();

    string runningDuration = "";
    if (job.RunningDuration != null) runningDuration = job.RunningDuration.ToString();

    if (extendedInfo && (job.State == JobState.Finished || job.State == JobState.Canceled || job.State == JobState.Error))
    {
        int mediaUnitNumber = _context.EncodingReservedUnits.FirstOrDefault().CurrentReservedUnits;
        string mediaUnitSize = ReturnMediaReservedUnitName(_context.EncodingReservedUnits.FirstOrDefault().ReservedUnitType);
        var jobQueue = _context.Jobs.Where(j => j.State == JobState.Queued).Count();
        var jobScheduled = _context.Jobs.Where(j => j.State == JobState.Scheduled).Count();
        var jobProcessing = _context.Jobs.Where(j => j.State == JobState.Processing).Count();

        return req.CreateResponse(HttpStatusCode.OK, new
        {
            jobState = job.State,
            errorText = sberror.ToString(),
            startTime = startTime,
            endTime = endTime,
            runningDuration = runningDuration,
            mediaUnitNumber = mediaUnitNumber,
            mediaUnitSize = mediaUnitSize,
            jobQueue = jobQueue,
            jobScheduled= jobScheduled,
            jobProcessing = jobProcessing
        });
    }
    else
    {
        return req.CreateResponse(HttpStatusCode.OK, new
        {
            jobState = job.State,
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




