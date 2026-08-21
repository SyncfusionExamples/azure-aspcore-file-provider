# azure-aspcore-file-provider

This repository contains the ASP.NET Core Azure storage file system providers for the File Manager component.

## Key Features

Azure file system provider serves the file system support for the FileManager component with the Microsoft Azure blob storage.

The following actions can be performed with Azure file system Provider.

| **Actions** | **Description** |
| --- | --- |
| Read     | Reads the files from Azure blob container. |
| Details  | Provides details about files Type, Size, Location and Modified date. |
| Download | Downloads the selected file or folder from the Azure blob. |
| Upload   | Uploads a files to Azure blob. t accepts uploaded media with the following characteristics: <ul><li>Maximum file size:  30MB</li><li>Accepted Media MIME types: `*/*` </li></ul> |
| Create   | Creates a new folder. |
| Delete   | Removes a file from Azure blob. |
| Copy     | Copys the selected Files from target. |
| Move     | Pastes the copied files to the desired location. |
| Rename   | Renames a folder or file. |
| Search   | Searches a file or folder in Azure blob. |

## Prerequisites

In order to run the service, we need to create the [Azure blob storage account](https://docs.microsoft.com/en-us/azure/storage/common/storage-quickstart-create-account?tabs=azure-portal) and register the Azure storage details like  account name, password and blob name details with in the RegisterAzure method.

```

  RegisterAzure(string accountName, string accountKey, string blobName)

```

## How to run this application?

To run this application, clone the [`azure-aspcore-file-provider`](https://github.com/azure-aspcore-file-provider) repository and then navigate to its appropriate path where it has been located in your system.

To do so, open the command prompt and run the below commands one after the other.

```

git clone https://github.com/azure-aspcore-file-provider  azure-aspcore-file-provider

cd azure-aspcore-file-provider

```

## Running application

Once cloned, open solution file in visual studio.Then build the project after restoring the nuget packages and run it.


## File Manager AjaxSettings

To access the basic actions such as Read, Delete, Copy, Move, Rename, Search, and Get Details of File Manager using Azure service, just map the following code snippet in the Ajaxsettings property of File Manager.

Here, the `hostUrl` will be your locally hosted port number.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/AzureProvider/AzureFileOperations'
  }
```

## File download AjaxSettings

To perform download operation, initialize the `downloadUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/AzureProvider/AzureFileOperations',
        downloadUrl: hostUrl +'api/AzureProvider/AzureDownload'
  }
```

## File upload AjaxSettings

To perform upload operation, initialize the `uploadUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/AzureProvider/AzureFileOperations',
        uploadUrl: hostUrl +'api/AzureProvider/AzureUpload'
  }
```

## File image preview AjaxSettings

To perform image preview support in the File Manager component, initialize the `getImageUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/AzureProvider/AzureFileOperations',
         getImageUrl: hostUrl +'api/AzureProvider/AzureGetImage'
  }
```

The FileManager will be rendered as the following.

![File Manager](https://ej2.syncfusion.com/products/images/file-manager/readme.gif)

## Docker Support

The File Manager Azure file provider is also available as a pre-built Docker image for quick deployment to your infrastructure. The image exposes the same Web API endpoints as the ASP.NET Core project in this repository; configure it through the environment variables listed below.

You can deploy it quickly to your infrastructure. If you want to add new functionality or customize any existing functionalities, create your own Docker file by referencing the existing [File Manager Azure Docker project](https://github.com/SyncfusionExamples/azure-aspcore-file-provider).

The File Manager is supported on multiple platforms including JavaScript, Angular, React, Vue, ASP.NET Core, ASP.NET MVC, TypeScript, and Blazor.

### Prerequisites

Have [Docker](https://www.docker.com/products/container-runtime#/download) installed in your environment:

* On Windows, install [Docker for Windows](https://docs.docker.com/docker-for-windows/install/).
* On macOS, install [Docker for Mac](https://docs.docker.com/docker-for-mac/install/).

### How to deploy the File Manager Azure Service Docker Image

**Step 1:** Pull the Azure file provider image from Docker Hub.

```console
docker pull syncfusion/filemanager-azure-aspnetcore-provider
```

**Step 2:** Create the `docker-compose.yml` file with the following content.

**docker-compose.yml**

```yaml
version: '3.8'

services:
  azure-aspnetcore-provider:
    image: syncfusion/filemanager-azure-aspnetcore-provider:latest
    environment:
      # Provide your Azure Blob Storage credentials
      AZURE_ACCOUNT_NAME: YOUR_AZURE_ACCOUNT_NAME
      AZURE_ACCOUNT_KEY: YOUR_AZURE_ACCOUNT_KEY
      AZURE_BLOB_NAME: YOUR_AZURE_BLOB_NAME
      # Full URL of the Azure Blob container where the file manager operates.
      # Example: "https://<account>.blob.core.windows.net/<container>/"
      AZURE_BLOB_PATH: "Blob_Path"
      # Full URL of the file path inside the blob container (the root folder shown by the file manager).
      # Example: "https://<account>.blob.core.windows.net/<container>/<file-path>"
      AZURE_FILE_PATH: "File_Path"
    ports:
      - "5000:80"
```

**Step 3:** Run the container.

In a terminal tab, navigate to the directory where you placed the `docker-compose.yml` file and execute the following:

```console
docker compose up
```

The File Manager Azure provider will be accessible at `http://localhost:5000`.

To stop the container, run:

```console
docker compose down
```

#### Azure Blob Storage credential details

| Environment Variable | Required | Description |
|----------------------|----------|-------------|
| `AZURE_ACCOUNT_NAME` | Yes | Name of your Azure Storage account. |
| `AZURE_ACCOUNT_KEY` | Yes | Access key for your Azure Storage account. |
| `AZURE_BLOB_NAME` | Yes | Name of the blob container that stores the files. |
| `AZURE_BLOB_PATH` | Yes | Full URL of the Azure Blob container. Example: `https://<account>.blob.core.windows.net/<container>/` |
| `AZURE_FILE_PATH` | Yes | Full URL of the file path inside the blob container shown by the File Manager. Example: `https://<account>.blob.core.windows.net/<container>/<file-path>` |

**Step 4:** Configure the client-side File Manager component.

To access the file operations through the Azure service running in Docker, just map the following code snippet in the `ajaxSettings` property of the File Manager component. Here, the `hostUrl` will be the URL of the running Docker instance.

```
  var hostUrl = http://localhost:5000/;
  ajaxSettings: {
        url: hostUrl + 'api/AzureProvider/AzureFileOperations',
        uploadUrl: hostUrl + 'api/AzureProvider/AzureUpload',
        downloadUrl: hostUrl + 'api/AzureProvider/AzureDownload',
        getImageUrl: hostUrl + 'api/AzureProvider/AzureGetImage'
  }
```

For more information about File Manager setup on other platforms, see the getting started pages for [JavaScript](https://help.syncfusion.com/file-manager-sdk/javascript/es5-getting-started), [Angular](https://help.syncfusion.com/file-manager-sdk/angular/getting-started), [React](https://help.syncfusion.com/file-manager-sdk/react/getting-started), [Vue](https://help.syncfusion.com/file-manager-sdk/vue/getting-started), [TypeScript](https://help.syncfusion.com/file-manager-sdk/typescript/getting-started), [ASP.NET Core](https://help.syncfusion.com/file-manager-sdk/asp-net-core/getting-started), [ASP.NET MVC](https://help.syncfusion.com/file-manager-sdk/asp-net-mvc/getting-started), and [Blazor](https://help.syncfusion.com/file-manager-sdk/blazor/getting-started-with-web-app).


## Support

Product support is available for through following mediums.

* Creating incident in Syncfusion [Direct-trac](https://www.syncfusion.com/support/directtrac/incidents?utm_source=npm&utm_campaign=filemanager) support system or [Community forum](https://www.syncfusion.com/forums/essential-js2?utm_source=npm&utm_campaign=filemanager).
* New [GitHub issue](https://github.com/syncfusion/ej2-javascript-ui-controls/issues/new).
* Ask your query in [Stack Overflow](https://stackoverflow.com/?utm_source=npm&utm_campaign=filemanager) with tag `syncfusion` and `ej2`.

## License

Check the license detail [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/license).

## Changelog

Check the changelog [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/controls/filemanager/CHANGELOG.md)

© Copyright 2019 Syncfusion, Inc. All Rights Reserved. The Syncfusion Essential Studio license and copyright applies to this distribution.
