# Create a Media Services Account connected to Aspera On Demand Services for Ingest
<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2F103-aspera-ingest%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

This template creates a Media Services Account with its Storage account on Azure. In addition it adds the Aspera On Demand service from the Azure Marketplace (seperately licensed) and connects it directly to the storage account for the Media Services Account. You will need a promo code from Aspera to deploy this template. 

After deployment, ingesting of files through the high-speed Aspera ingest client will drop the files into the deployed storage account in the 'input' container. The Azure Function will be triggered on new files arriving from Aspera in the 'input' container and trigger the ingest of a new Media Services Asset. Finally, the function will submit a standard encoding job for 'Adaptive Streaming' and create another new Media Services Asset. 

The original asset will be removed from the 'input' folder upon completion. 

## Troubleshooting

1. If you run into an error on deployment related to Source Control or GitHub, it is likely that you have never configured Github in your subscription before. The easiest way to solve the issue is to create a new empty Azure Functions app, and configure the source control settings for the Function. In this process you will authorize the Azure Portal to work with your Github credentials.

For more information about Azure Media Services, see [Media Services Documentation](https://docs.microsoft.com/en-us/azure/media-services/).