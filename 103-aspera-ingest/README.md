# Use Aspera On Demand for High Speed Ingest
<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2F103-aspera-ingest%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

## Transfer files to and from Azure BLOB up to 100x faster than TCP or FTP
This template creates a Media Services Account with its Storage account on Azure. In addition it adds the Aspera On Demand service from the Azure Marketplace (seperately licensed) and connects it directly to the storage account for the Media Services Account. You will need a promo code from Aspera to deploy this template. 

After deployment, ingesting of files through the high-speed Aspera ingest client will drop the files into the deployed storage account in the 'input' container. The Azure Function will be triggered on new files arriving from Aspera in the 'input' container and trigger the ingest of a new Media Services Asset. Finally, the function will submit a standard encoding job for 'Adaptive Streaming' and create another new Media Services Asset. 

The original asset will be removed from the 'input' folder upon completion. 

## About Aspera
Aspera software simplifies and accelerates the movement of data to and from the cloud at rates up to 100’s of times faster than standard TCP-based transfers. Aspera Server On Demand is software running on Azure that enables high-speed upload and download of large files and ldata sets directly into Azure Blob object storage. Using the patented Aspera FASP® high-speed transfer protocol, Azure customers can quickly move data of any size over any distance to Blob at line speed. The unique Direct-to-Cloud capability integrates with the underlying Azure Blob multi-part HTTP interfaces to enable the fastest file uploads and downloads while adding key transfer management features such as pause, resume, and encryption over the wire and at rest. Server On Demand is available as a subscription service, based on the amount of data transferred.


## Troubleshooting

1. If you run into an error on deployment related to Source Control or GitHub, it is likely that you have never configured Github in your subscription before. The easiest way to solve the issue is to create a new empty Azure Functions app, and configure the source control settings for the Function. In this process you will authorize the Azure Portal to work with your Github credentials.

## Additional Information
- For details on Aspera On Demand, usage see [Aspera On Demand For Microsoft Azure FAQ](http://cloud.asperasoft.com/ja/aspera-on-demand/aspera-on-demand-for-microsoft-azure-faq/)
- For pricing details on Aspera on Demand see [Aspera On Demand in the Azure Marketplace](https://azuremarketplace.microsoft.com/en-us/marketplace/apps/aspera.sod?tab=PlansAndPrice)
- For more information about Azure Media Services, see [Media Services Documentation](https://docs.microsoft.com/en-us/azure/media-services/).