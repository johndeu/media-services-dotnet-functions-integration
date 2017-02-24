---
services: media-services,functions
platforms: dotnet
author: shigeyf
---

# Media Services: Integrating Azure Media Services with Azure Functions
This project contains examples of using Azure Functions with Azure Media Services. 
The project includes several folders of sample Azure Functions for use with Azure Media Services that show workflows related
to ingesting content directly from blob storage, encoding, and writing content back to blob storage.


# How to use 201-logic-app-workflow-1 sample media workflow


## Setup media workflow functions on your Azure Subscription
1. Fork https://github.com/Azure-Samples/media-services-dotnet-functions-integration to your own repo
2. Deploy Azure Functions  

  <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fazuredeploy.json" target="_blank"><img src="http://azuredeploy.net/deploybutton.png"/></a>  

  * This deployment script will create an Azure Media Services account and an Azure Storage account 
  * Please consider Consumption Plan or App Service Plan if you will deploy manually without the deployment script above
    * Consumption Plan – Timeout of function will be 5 mins
    * App Service Plan (Dedicated Plan) – There is no timeout (if AlwaysOn is enabled)
3. Check App Settings of Azure Functions @ Azure Portal
  * Plaese makes sure if the following environment Key/Value pairs in the "App Settings" of your Azure Functions are correctly configured

    | Key | Value Description |
    | --- | --- |
    | **Project** | Set the project name to "201-logic-app-workflow-1". This will bind the continous integration to this functions folder. |
    | **AMSAccount** | Your AMS Account Name |
    | **AMSKey** | Your AMS Account Key |
    | **MediaServicesStorageAccountName** | Your Media Services Storage Account Name |
    | **MediaServicesStorageAccountKey** | Your Media Services Storage Account Key |

4. Check if Azure Functions are deployed from your Github repo into your Azure Function App  @ Azure Portal
  * If not, please do "Sync" manually from "Configure continusous integration" in Function app settings
5. Deploy Logic App for sample media workflow  

  <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fshigeyf%2Fmedia-services-dotnet-functions-integration%2Fmaster%2F201-logic-app-workflow-1%2Fazuredeploy-logic-app-workflow.json" target="_blank"><img src="http://azuredeploy.net/deploybutton.png"/></a>  

  * This deployment script will create a Logic App which is using the deployed Azure Functions above
  * Please refer the next section if you will deploy manually
6. Update both API Connection's credentials for Outlook and OneDrve accounts


## Setup media workflow with Azure Logic Apps manually
This media workflow sample functions are implemented as a set of Azure Functions.
These functions are sequentially arranged and called in the Azure Logic Apps as a workflow.
Many pre-defined APIs for Logic Apps which are already provided from Microsoft and 3rd party partners can be combined for your workflow.

Here is an example of media workflow with Azure Logic Apps.

#### Step 1) A sample logic is triggered by OneDrive file
Use "OneDrive - When a file is created" action.
* Specify a watch folder to where IngestAssetConfig JSON file is uploaded
* Specify a frequecy to watch the specified watch folder 
* Note that when multiple JSON files will be uploaded then multiple workflows will be triggered 

![Screen capture](images/WorkflowSample-Step1.png?raw=true)

#### Step 2) Import Asset (Create empty asset and copy blobs)
Use Azure Function - **WF-1-CreateAsset-ImportAssetFromIngestAssetConfig**
* Specify "OneDrive – File content" as "FileContent" input
* Specify "OneDrive – File name" as "FileName" input

![Screen capture](images/WorkflowSample-Step2.png?raw=true)

#### Step 3) Wait for Copy Blobs
Use "Until" loop with **WF-1-CreateAsset-MonitorCopyBlob**
* Wait until when **WF-1-CreateAsset-MonitorCopyBlob** - "CopyStatus" *is equal to* "2"
* Specify "WF-1-CreateAsset-ImportAssetFromIngestAssetConfig – DestinationContainer" as "DestinationContainer" input

![Screen capture](images/WorkflowSample-Step3.png?raw=true)

#### Step 4) Finalize Creating Asset
Use Azure Function **WF-1-CreateAsset-UpdateFinal**
* Specify "WF-1-CreateAsset-ImportAssetFromIngestAssetConfig – AssetId" as "AssetId" input
* Specify "OneDrive – File content" as "IngestAssetConfigJson" input

![Screen capture](images/WorkflowSample-Step4.png?raw=true)

#### Step 5) Check if Media Processing is required
Use "Condition"
* Use "WF-1-CreateAsset-UpdateFinal – MediaProcessRequired" as "Condition" parameter
* IF "MediaProcessRequired" *is greater than* "0", goto Step 6
* IF NO, goto Step 7

![Screen capture](images/WorkflowSample-Step5.png?raw=true)  
![Screen capture](images/WorkflowSample-Step6-C1YES.png?raw=true)  
![Screen capture](images/WorkflowSample-Step6-C2NO.png?raw=true)  

#### Step 6-1) [Condition=YES] Encode Asset
Use **WF-2-ProcessAsset-SubmitEncodingJob**
* Specify "WF-1-CreateAsset-UpdateFinal – AssetId" as "AssetId" input
* Specify "OneDrive – File content" as "IngestAssetConfigJson" input

#### Step 6-2) [Condition=YES] Wait for Encoding Job
Use "Until" loop with **WF-2-ProcessAsset-MonitorJob**
* Wait until when **WF-2-ProcessAsset-SubmitEncodingJob** - "JobState" *is greater than* "2"
* Specify "WF-2-ProcessAsset-SubmitEncodingJob – JobId" as "JobId"

![Screen capture](images/WorkflowSample-Step6-C1YES-P1.png?raw=true)

#### Step 6-3) [Condition=YES] Check if Encoding Job is successfully done 
Use "Condition"
* Use "WF-2-ProcessAsset-MonitorJob – JobState" as "Condition" parameter
* IF "JobState" *is equal to* "3", goto Step 6-4

![Screen capture](images/WorkflowSample-Step6-C1YES-P2.png?raw=true)

#### Step 6-4) [Condition=YES] Send an email
Use **WF-5-PublishAsset**
* Specify "WF-2-ProcessAsset-SubmitEncodingJob – OutputAssetId" as "OutputAssetId" input
* Specify "OneDrive – File content" as "IngestAssetConfigJson" input

#### Step 6-5) [Condition=YES] Send an email
Use "Outlook.com - Send an email" to send an email

#### Step 7-1) [Condition=NO] Publish Asset
Use **WF-5-PublishAsset**
* Specify "WF-1-CreateAsset-ImportAssetFromIngestAssetConfig – AssetId" as "AssetId" input
* Specify "OneDrive – File content" as "IngestAssetConfigJson" input

![Screen capture](images/WorkflowSample-Step6-C2NO_Details.png?raw=true)

#### Step 7-2) [Condition=NO] Send an email
Use "Outlook.com - Send an email" to send an email


## Run workflow
1. Upload a source asset to a source container of your Azure Blob Storage account (specified with **SourceStorageAccountName**)
2. Create IngestAssetConfig JSON file
3. Run workflow
  * Upload IngestAssetConfig JSON file to /AMSImports folder of OneDrive
  * Workflow will be automatically triggered in configured minutes (default - 3 minutes)

## Sample **IngestAssetConfig** JSON
* Example #1 - Single bitrate media file with encoding to a multi-bitrate asset
```
{
    "IngestSource": {
        "SourceContainerName": "bigbuckbunny-1080p24p"
    },
    "IngestAsset": {
        "AssetName": "BigBuckBunny_1080p24p",
        "AssetFiles": [
            {
                "FileName": "big_buck_bunny_1080p_h264.mov",
                "IsPrimary": true
            }
        ],
        "AssetCreationOption": "None"
    },
    "IngestAssetEncoding": {
        "Encoding": true,
        "Encoder": "MES",
        "EncodingConfiguration": "H264 Multiple Bitrate 1080p"
    }
}
```

* Example #2 – Smooth Asset
```
{
    "IngestSource": {
        "SourceContainerName": "bigbuckbunny-720p-smooth"
    },
    "IngestAsset": {
        "AssetName": "BigBuckBunny_720_Smooth",
        "AssetFiles": [
            {
                "FileName": "BigBuckBunny.ism",
                "IsPrimary": true
            },
            {   "FileName": "BigBuckBunny.ismc“      },
            {   "FileName": "BigBuckBunny_230.ismv”  },
            {   "FileName": "BigBuckBunny_331.ismv“  },
            {   "FileName": "BigBuckBunny_477.ismv“  },
            {   "FileName": "BigBuckBunny_688.ismv“  },
            {   "FileName": "BigBuckBunny_991.ismv“  },
            {   "FileName": "BigBuckBunny_1427.ismv“ },
            {   "FileName": "BigBuckBunny_2056.ismv“ },
            {   "FileName": "BigBuckBunny_2962.ismv“ },
            {   "FileName": "BigBuckBunny_Thumb.jpg“ }
        ],
        "AssetCreationOption": "None"
    },
    "IngestAssetEncoding": {
        "Encoding": "false"
    }
}
```

* Example #3 – PlayReady Protected Smooth Asset
```
{
    "IngestSource": {
        "SourceContainerName": "superspeedway-720p-smooth"
    },
    "IngestAsset": {
        "AssetName": "SuperSpeedway_720_Smooth",
        "AssetFiles": [
            {
                "FileName": "SuperSpeedway_720.ism",
                "IsPrimary": true
            },
            {   "FileName": "SuperSpeedway_720.ismc“      },
            {   "FileName": "SuperSpeedway_720_230.ismv”  },
            {   "FileName": "SuperSpeedway_720_331.ismv“  },
            {   "FileName": "SuperSpeedway_720_477.ismv“  },
            {   "FileName": "SuperSpeedway_720_688.ismv“  },
            {   "FileName": "SuperSpeedway_720_991.ismv“  },
            {   "FileName": "SuperSpeedway_720_1427.ismv“ },
            {   "FileName": "SuperSpeedway_720_2056.ismv“ },
            {   "FileName": "SuperSpeedway_720_2962.ismv“ },
            {   "FileName": "SuperSpeedway_720_Thumb.jpg“ }
        ],
        "AssetCreationOption": "CommonEncryptionProtected"
    },
    "IngestAssetEncoding": {
        "Encoding": "false"
    } ,
    "IngestAssetPublish": {
        "StartDate": "2018-01-01",
        "EndDate": "2018-12-31"
    }
}
```


# Documentation - Media Workflow WF-x sample
This workflow implementation provides a simple workflow for media file(s) ingesting, encoding, and publishing as VOD assets.
Current implemented functions (@2017/01/31) is as follows:
* **WF-1** – Ingest Asset to AMS accounts with IngestAssetConfig JSON configuration
  * *WF-1-CreateAsset-ImportAssetFromIngestAssetConfig* – Create AMS asset and copy source assets from a source blob container to a destination blob container asynchrounously
  * *WF-1-CreateAsset-MonitorCopyBlob* – Monitor async blob copy until finishing
  * *WF-1-CreateAsset-UpdateFinal* – Add asset files to AMS asset
* **WF-2** – Encode Asset
  * *WF-2-ProcessAsset-SubmitEncodingJob* – Submit AMS encoding job
  * *WF-2-ProcessAsset-MonitorJob* – Monitor encoding job until finishing
* **WF-5** – Publish Asset
  * *WF-5-PublishAsset* – Publish AMS asset

## Ingest Media Asset Configuration
This workflow requires an input JSON data (IngestAssetConfig) for ingesting media asset as a workflow configuration information:  

IngestAssetConfig (v1) containes:
* **IngestSource**
 - Asset source location
* **IngestAsset**
 - Asset & Asset files info
* **IngestAssetEncoding** - Asset Encoding options
* **IngestAssetPublish**
 - Asset Publishing options

```
{
    "IngestSource": {
        "SourceContainerName": "<source_container_name>"
    },
    "IngestAsset": {
        "AssetName": "<asset_name>",
        "AssetFiles": [
            {
                "FileName": "<asset_file_1>",
                "IsPrimary": true
            },
            {
                "FileName": "<asset_file_2>"
            }
        ],
        "AssetCreationOption": "<option>"
    },
    "IngestAssetEncoding": {
        "Encoding": true,
        "Encoder": "<encoder_name>",
        "EncodingConfiguration": "<encoding_configuration>"
    },
    "IngestAssetPublish": {
        "StartDate": "<YYYY-MM-DD>",
        "EndDate": "<YYYY-MM-DD>"
    }
}

```

### Speficication of IngestAssetConfig (v1) JSON

#### IngestSource – Asset source location
* **SourceContainerName** [REQUIRED] – Azure Blob Storage container name for source asset

#### IngestAsset – Asset & Asset files info
* **AssetName** [REQUIRED] – Asset friendly name to be created
* **AssetFiles** [REQUIRED] – Files registered to Asset as AssetFiles
* **Filename** [REQUIRED] – File name of Asset Files
* **IsPrimary** [OPTIONAL] – Primary flag of Asset Files in Asset (default = false)
* **AssetCreationOption** [OPTIONAL] – Asset Option for Asset
  * Current supported options in this sample workflow implementation
    * *"None"* (default)
    * *"CommonEncryptionProtected"*
    * *"EnvelopeEncryptionProtected"*

#### IngestAssetEncoding – Asset Encoding options
* **Encoding** [OPTIONAL] – Process encoding job for Asset (default = false)
* **Encoder** [OPTIONAL] – Encoder name to be used for encoding job; the following encoder processor can be specified:
  * *"MES"* : Media Encoder Standard
  * *"MEPW"* : Media Encoder Premium workflow
* **EncodingConfiguration** [OPTIONAL] – Encoder parameter string (or pre-defined preset name)

#### IngestAssetPublish – Asset Publishing options
* **StartDate** [OPTIONAL] – "YYYY-MM-DD" format date starting to publish (a future date will be required)
* **EndDate** [REQUIRED] – "YYYY-MM-DD" format date finishing to publish (a future date will be required)

## WF-x media workflow function speficication documentation

### WF-1-CreateAsset

#### WF-1-CreateAsset-ImportAssetFromIngestAssetConfig
* Azure Function Template - C# Generic Webhook
* Input – JSON format data
  * *FileName* : **IngestAssetConfig** filename for target asset
  * *FileContent* : **IngestAssetConfig** JSON for target asset
  * *SourceStorageAccountName* : Azure Storage Account Name for source assets
  * *SourceStorageAccountKey* : Azure Storage Account Key for source assets
* Output – JSON format data
  * *AssetId* : Created AMS Asset Id (e.g. nb:cid:UUID:2f7e6884-2637-42f8-a336-97dc04942e69)
  * *SourceContainer* : Azure Blob Storage container name of target asset
  * *DestinationContainer* : Azure Blob Storage container name of created AMS asset
* Function Details
  * Create an AMS Asset for target asset (and blob container on Azure Storage account managed by AMS) – based on **IngetAsset** data of **IngestAssetConfig** JSON
  * Copy Azure Storage blobs from source container to target container – based on **IngetSource** data and **IngetAsset** data of **IngestAssetConfig** JSON

#### WF-1-CreateAsset-MonitorCopyBlob
* Azure Function Template - C# Generic Webhook
* Input – JSON format data
  * *DestinationContainer* : Azure Blob Storage container name of created AMS asset
  * *Delay* (Optional) : Delay to return the result (default 15,000 ms)
* Output – JSON format data
  * *CopyStatus* : Status of Azure Storage blob copy operations
* Function Details
  * Check blob copy operations of each blob in target container
  * Return CopyStatus
    * Return CopyStatus.Success if all blob copy operations has been successfully done
    * Return CopyStatus.Pending if one of blob copy operations is still doing
* Caution
  * Azure Functions with Consumption plan has 5 mins timeout. You must not pass 300,000 ms or more value as “Delay” input

#### WF-1-CreateAsset-UpdateFinal
* Azure Function Template - C# Generic Webhook
* Input – JSON format data
  * *AssetId* : Created AMS Asset Id
  * *IngestAssetConfigJson* : **IngestAssetConfig** JSON for target asset
* Output – JSON format data
  * *MediaProcessRequired* : Media Process must be done before publishing
* Function Details
  * Add AMS AssetFile(s) (associated blob(s)) to target AMS asset (indicated AssetId) in AMS account (after blob copy operations has been done) – based on **IngetAsset** data of **IngestAssetConfig** JSON
  * Return MediaProcessRequired flag
    * Return 0 if no media processing (including encoding) will not be required before publishing
    * Return 1 if media processing (including encoding) will be required before publishing

### WF-2-ProcessAsset

#### WF-2-ProcessAsset-SubmitEncodingJob
* Azure Function Template - C# Generic Webhook
* Input – JSON format data
  * *AssetId* : Created AMS Asset Id
  * *IngestAssetConfigJson* : **IngestAssetConfig** JSON for target asset
* Output – JSON format data
  * *JobId* : AMS Job Id for encoding
  * *OutputAssetId* : AMS Asset Id of encoding output asset 
* Function Details
  * Submit AMS encoding job for target AMS asset – based on **IngetAssetEncoding** data of **IngestAssetConfig** JSON
* Caution
  * Azure Functions with Consumption plan has 5 mins timeout. You must not pass 300,000 ms or more value as *Delay* input

#### WF-2-ProcessAsset-MonitorJob
* Azure Function Template - C# Generic Webhook
* Input – JSON format data
  * *JobId* : Created AMS Job Id
  * *Delay* (Optional) : Delay to return the result (default 15,000 ms)
* Output – JSON format data
  * *JobId* : Created AMS Job Id
  * *JobState* : Job State of AMS Job Id’s job
    * IJob.State is as follows:

      | Job State | State Value |
      | --- | ---: |
      | Queued | 0 |
      | Scheduled | 1 |
      | Processing | 2 |
      | Finished | 3 |
      | Error | 4 |
      | Canceled | 5 |
      | Canceling | 6 |

* Function Details
  * Check and Return IJob.State of AMS Job
* Caution
  * Azure Functions with Consumption plan has 5 mins timeout. You must not pass 300,000 ms or more value as *Delay* input

### WF-5-PublishAsset

#### WF-5-PublishAsset
* Azure Function Template - C# Generic Webhook
* Input – JSON format data
  * *AssetId* : AMS Asset Id for publishing (Imported or Encoded AMS asset)
  * *IngestAssetConfigJson* : **IngestAssetConfig** JSON for target asset
* Output – JSON format data
  * *StreamingUrl* : Published URL for streaming target asset
* Function Details
  * Create streaming locator for target asset with specified Start/End date of publishing – based on **IngetAssetPublish** data of **IngestAssetConfig** JSON

