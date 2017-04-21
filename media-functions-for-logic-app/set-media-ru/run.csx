/*

This function sets the number and speed of media reserved units in the account.

Input:
{
    "ruCount" : "+1", // can be a number like "1", or a number with + or - to increase or decrease the number. Example :  "+2" or "-3"
    "ruSpeed" : "S1"  // can be "S1", "S2" or "S3"
}

Output:
{
    "success" : "True", // return if operation is a success or not
    "maxRu" : 10,       // number of max units
    "newRuCount" : 3,   // new count of units
    "newRuSpeed" : "S2" // new speed of units
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

    if (data.ruCount == null && data.ruSpeed == null)
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass ruCount and ruSpeed in the input object"
        });
    }

    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    int targetNbRU = -1;
    int? nbunits = null;
    bool relative = false;
    string RUspeed = "";
    ReservedUnitType? type = null;

    if (data.ruSpeed != null)
    {
        RUspeed = ((string)data.ruSpeed).ToUpper();
        if (RUspeed == "S1")
        {
            type = ReservedUnitType.Basic;
        }
        else if (RUspeed == "S2")
        {
            type = ReservedUnitType.Standard;
        }
        else if (RUspeed == "S3")
        {
            type = ReservedUnitType.Premium;
        }
        else
        {
            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Error parsing ruSpeed"
            });
        }
    }

    if (data.ruCount != null)
    {
        string RUcount = (string)data.ruCount;
        if (RUcount[0] == '+' || RUcount[0] == '-')
        {
            relative = true;
            try
            {
                nbunits = int.Parse(RUcount);
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Error (1) parsing ruCount"
                });
            }
        }
        else
        {
            try
            {
                nbunits = int.Parse(RUcount);
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Error (2) parsing ruCount"
                });
            }
        }
    }

    try
    {
        // Create and cache the Media Services credentials in a static class variable.
        _cachedCredentials = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

        // Used the chached credentials to create CloudMediaContext.
        _context = new CloudMediaContext(_cachedCredentials);
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.InternalServerError, new
        {
            Error = ex.ToString()
        });
    }

    IEncodingReservedUnit EncResUnit = _context.EncodingReservedUnits.FirstOrDefault();
    targetNbRU = EncResUnit.CurrentReservedUnits;
    ReservedUnitType targetType = EncResUnit.ReservedUnitType;

    log.Info("Current type of media RU: " + ReturnNewRUName(EncResUnit.ReservedUnitType));
    log.Info("Current count of media RU: " + EncResUnit.CurrentReservedUnits);
    log.Info("Maximum reservable media RUs: " + EncResUnit.MaxReservableUnits);

    if (nbunits != null)
    {
        if (relative)
        {
            if (((int)nbunits) > 0)
            {
                log.Info($"Adding {nbunits} unit(s)");
            }
            else
            {
                log.Info($"Removing {nbunits} unit(s)");
            }
            targetNbRU = Math.Max(targetNbRU + (int)nbunits, 0);
        }
        else
        {
            log.Info($"Changing to {nbunits} unit(s)");
            targetNbRU = (int)nbunits;
        }
    }

    if (type != null)
    {
        string sru = ReturnNewRUName((ReservedUnitType)type);
        log.Info($"Changing to {sru} speed");
        targetType = (ReservedUnitType)type;
    }

    if (targetNbRU == 0 && targetType != ReservedUnitType.Basic)
    {
        targetType = ReservedUnitType.Basic; // 0 units so we switch to S1
    }

    bool Error = false;
    try
    {
        EncResUnit.CurrentReservedUnits = targetNbRU;
        EncResUnit.ReservedUnitType = targetType;
        EncResUnit.Update();
        EncResUnit = _context.EncodingReservedUnits.FirstOrDefault(); // Refresh
    }
    catch (Exception ex)
    {
        Error = true;
    }

    log.Info("Media RU unit(s) updated successfully.");
    log.Info("New current speed of media RU  : " + ReturnNewRUName(EncResUnit.ReservedUnitType));
    log.Info("New current count of media RU : " + EncResUnit.CurrentReservedUnits);

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        success = (!Error).ToString(),
        maxRu = EncResUnit.MaxReservableUnits,
        newRuCount = EncResUnit.CurrentReservedUnits,
        newRuSpeed = ReturnNewRUName(EncResUnit.ReservedUnitType)
    });
}

public static string ReturnNewRUName(ReservedUnitType reservedUnitType)
{
    return "S" + ((int)reservedUnitType + 1);
}




