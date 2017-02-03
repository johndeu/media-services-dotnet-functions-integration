#r "Newtonsoft.Json"
#r "System.Web"

#load "../Shared/mediaServicesHelpers.csx"

using System;
using System.Net;
using System.Web;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;


// Read values from the App.config file.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;


public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");
    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);
    log.Info("Request : " + jsonContent);

    // Validate input objects
    int delay = 15000;
    if (data.JobId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass JobId in the input object" });
    if (data.Delay != null)
        delay = data.Delay;
    log.Info("Input - Job Id : " + data.JobId);
    //log.Info("delay : " + delay);

    log.Info($"Wait " + delay + "(ms)");
    System.Threading.Thread.Sleep(delay);

    IJob job = null;
    try
    {
        // Load AMS account context
        log.Info("Using Azure Media Services account : " + _mediaServicesAccountName);
        _context = new CloudMediaContext(new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey));

        // Get the job
        string jobid = (string)data.JobId;
        job = _context.Jobs.Where(j => j.Id == jobid).FirstOrDefault();
        if (job == null)
        {
            log.Info("Job not found : " + jobid);
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Job not found" });
        }
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    // IJob.State
    // - Queued = 0
    // - Scheduled = 1
    // - Processing = 2
    // - Finished = 3
    // - Error = 4
    // - Canceled = 5
    // - Canceling = 6
    log.Info($"Job {job.Id} status is {job.State}");

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        JobId = job.Id,
        JobState = job.State
    });
}
