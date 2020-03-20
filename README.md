# ej2-azure-aspcore-file-provider

This repository contains the ASP.NET Core Azure storage file system providers for the Essential JS 2 File Manager component.

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

To run this application, clone the [`ej2-azure-aspcore-file-provider`](https://github.com/ej2-azure-aspcore-file-provider) repository and then navigate to its appropriate path where it has been located in your system.

To do so, open the command prompt and run the below commands one after the other.

```

git clone https://github.com/ej2-azure-aspcore-file-provider  ej2-azure-aspcore-file-provider

cd ej2-azure-aspcore-file-provider

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
