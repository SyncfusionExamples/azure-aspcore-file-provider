using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Syncfusion.EJ2.FileManager.Base;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared;
using Syncfusion.EJ2.FileManager.Base;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Net;
using System.Threading;
#if EJ2_DNX
using System.Web;
using System.Web.Mvc;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif


namespace Syncfusion.EJ2.FileManager.AzureFileProvider
{
    public class AzureFileProvider : AzureFileProviderBase
    {
        List<FileManagerDirectoryContent> Items = new List<FileManagerDirectoryContent>();
        public CloudBlobContainer container;
        public string AccountName;
        public string ContainerName;
        public string AccountKey;
        private CloudBlobDirectory item;
        private string PathValue;
        private string BlobPath;
        private string FilesPath;
        private string DownloadLocation;
        private long size;
        private string rootPath;
        private List<string> existFiles = new List<string>();
        private List<string> missingFiles = new List<string>();
        private bool isFolderAvailable = false;
        private List<FileManagerDirectoryContent> copiedFiles = new List<FileManagerDirectoryContent>();

        public AzureFileProvider()
        {

        }
        // Registering the azure storage 
        public void RegisterAzure(string accountName, string accountKey, string blobName)
        {
            this.AccountKey = accountKey;
            this.AccountName = accountName;
            this.ContainerName = blobName;
            StorageCredentials creds = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);
            CloudBlobClient client = account.CreateCloudBlobClient();
            container = client.GetContainerReference(blobName);
        }
        // Sets blob and file path
        public void setBlobContainer(string blobPath, string filePath)
        {
            this.BlobPath = blobPath;
            this.FilesPath = filePath;
            this.rootPath = this.FilesPath.Replace(this.BlobPath, "");
        }
        // Sets download location
        public void setDownloadPath(string downloadLocation)
        {
            this.DownloadLocation = downloadLocation;
        }

        // Performs files operations
        public async Task<BlobResultSegment> AsyncReadCall(string path, string oper)
        {
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
            BlobRequestOptions options = new BlobRequestOptions();
            OperationContext context = new OperationContext();
            dynamic Asyncitem = null;
            if (oper == "Read") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(false, BlobListingDetails.Metadata, null, null, options, context);
            if (oper == "Paste") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(false, BlobListingDetails.None, null, null, options, context);
            if (oper == "Rename") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(true, BlobListingDetails.Metadata, null, null, options, context);
            if (oper == "Remove") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(true, BlobListingDetails.None, null, null, options, context);
            if (oper == "HasChild") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(false, BlobListingDetails.None, null, null, options, context);
            //return Asyncitem;
            return await Task.Run(() =>
            {
                return Asyncitem;
            });
        }
        // Reads the storage 
        public object Read(string path, FileManagerDirectoryContent[] selectedItems)
        {
            return ReadAsync(path, "*.*", selectedItems).GetAwaiter().GetResult();
        }

        // Reads the storage files
        public async Task<FileManagerResponse> ReadAsync(string path, string filter, FileManagerDirectoryContent[] selectedItems)
        {
            OperationContext context = new OperationContext();
            BlobRequestOptions options = new BlobRequestOptions();
            FileManagerResponse ReadResponse = new FileManagerResponse();
            List<FileManagerDirectoryContent> details = new List<FileManagerDirectoryContent>();

            FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
            try
            {
                filter = filter.Replace(" ", "");
                var extensions = (filter ?? "*").Split(",|;".ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
                CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);

                cwd.Name = sampleDirectory.Prefix.Split(sampleDirectory.Parent.Prefix)[sampleDirectory.Prefix.Split(sampleDirectory.Parent.Prefix).Length - 1].Replace("/", "");
                cwd.Type = "File Folder";
                cwd.FilterPath = selectedItems.Length > 0 ? selectedItems[0].FilterPath : "";
                cwd.Size = 0;
                cwd.HasChild = await HasChildDirectory(path);
                ReadResponse.CWD = cwd;
                string Oper = "Read";
                var items = await AsyncReadCall(path, Oper);
                foreach (var item in items.Results)
                {
                    bool canAdd = false;
                    if (extensions[0].Equals("*.*") || extensions[0].Equals("*"))
                        canAdd = true;
                    else if (item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob file = (CloudBlockBlob)item;
                        var names = file.Name.ToString().Trim().Split('.');
                        if (Array.IndexOf(extensions, "*." + names[names.Count() - 1]) >= 0)
                            canAdd = true;
                        else canAdd = false;
                    }
                    else
                        canAdd = true;
                    if (canAdd)
                    {
                        if (item.GetType() == typeof(CloudBlockBlob))
                        {
                            CloudBlockBlob file = (CloudBlockBlob)item;
                            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
                            entry.Name = file.Name.Replace(path, "");
                            entry.Type = System.IO.Path.GetExtension(file.Name.Replace(path, ""));
                            entry.IsFile = true;
                            entry.Size = file.Properties.Length;
                            entry.DateModified = file.Properties.LastModified.Value.LocalDateTime;
                            entry.HasChild = false;
                            entry.FilterPath = selectedItems.Length > 0 ? path.Replace(this.rootPath, "") : "/";
                            details.Add(entry);
                        }
                        else if (item.GetType() == typeof(CloudBlobDirectory))
                        {
                            CloudBlobDirectory directory = (CloudBlobDirectory)item;
                            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
                            entry.Name = directory.Prefix.Replace(path, "").Replace("/", "");
                            entry.Type = "Directory";
                            entry.IsFile = false;
                            entry.Size = 0;
                            entry.HasChild = await HasChildDirectory(directory.Prefix);
                            entry.FilterPath = selectedItems.Length > 0 ? path.Replace(this.rootPath,"") : "/";
                            details.Add(entry);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return ReadResponse;
            }
            ReadResponse.Files = (IEnumerable<FileManagerDirectoryContent>)details;

            return ReadResponse;
        }


        public static string GetRelativePath(string rootPath, string fullPath)
        {
            if (!String.IsNullOrEmpty(rootPath) && !String.IsNullOrEmpty(fullPath))
            {
                var rootDirectory = new DirectoryInfo(rootPath);
                if (rootDirectory.FullName.Substring(rootDirectory.FullName.Length - 1) == "\\")
                {
                    if (fullPath.Contains(rootDirectory.FullName))
                    {
                        return fullPath.Substring(rootPath.Length - 1);
                    }
                }
                else if (fullPath.Contains(rootDirectory.FullName + "\\"))
                {
                    return "\\" + fullPath.Substring(rootPath.Length + 1);
                }
            }
            return String.Empty;
        }

        // Converts the byte size value to appropriate value
        public String byteConversion(long fileSize)
        {
            try
            {
                string[] index = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
                if (fileSize == 0)
                {
                    return "0 " + index[0];
                }

                long bytes = Math.Abs(fileSize);
                int loc = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
                double num = Math.Round(bytes / Math.Pow(1024, loc), 1);
                return (Math.Sign(fileSize) * num).ToString() + " " + index[loc];
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        // Gets the SizeValue of the Directory
        public async Task<long> getSizeValue(string path)
        {
            var items = await AsyncReadCall(path, "Read");
            foreach (var item in items.Results)
            {
                if (item is CloudBlockBlob blockBlob)
                {
                    CloudBlockBlob blob = container.GetBlockBlobReference(new CloudBlockBlob(item.Uri).Name);
                    await blob.FetchAttributesAsync();
                    size = size + blob.Properties.Length;
                }
                else if (item is CloudBlobDirectory blobDirectory)
                {
                    var localPath = item.Uri.ToString().Replace(this.BlobPath, ""); // <-- Change your download target path here
                    await getSizeValue(localPath);
                }
            }
            return size;

        }
        // Gets Details of the files
        public virtual FileManagerResponse Details(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            return GetDetailsAsync(path, names, data).GetAwaiter().GetResult();

        }
        // Gets the details
        public async Task<FileManagerResponse> GetDetailsAsync(string path, string[] names, IEnumerable<object> selectedItems = null)
        {

            FileManagerResponse DetailsResponse = new FileManagerResponse();
            try
            {
                bool isFile = false;
                bool namesAvailable = names.Length > 0 ? true : false;
                if (names.Length == 0 && selectedItems != null)
                {
                    List<string> values = new List<string>();
                    foreach (FileManagerDirectoryContent item in selectedItems)
                    {
                        values.Add(item.Name);
                    }
                    names = values.ToArray();
                }

                FileDetails[] fDetails = new FileDetails[names.Length];
                FileDetails fileDetails = new FileDetails();
                long multipleSize = 0;
                if (selectedItems != null)
                {
                    foreach (FileManagerDirectoryContent Fileitem in selectedItems)

                    {
                        FileManagerDirectoryContent s_item = Fileitem;
                        if (names.Length == 1)
                        {
                            if (s_item.IsFile)
                            {
                                var blob = container.GetBlockBlobReference(path + s_item.Name);
                                isFile = s_item.IsFile;
                                fileDetails.IsFile = isFile;
                                await blob.FetchAttributesAsync();
                                fileDetails.Name = names[0];
                                fileDetails.Location = (path + (namesAvailable ? s_item.Name : "")).Replace("/", @"\");
                                fileDetails.Size = byteConversion(blob.Properties.Length);
                                fileDetails.Modified = blob.Properties.LastModified.Value.LocalDateTime;
                                DetailsResponse.Details = fileDetails;
                            }

                            else
                            {
                                CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
                                long sizeValue = getSizeValue(path + (namesAvailable ? s_item.Name : "")).Result;
                                isFile = false;
                                fileDetails.Name = s_item.Name;
                                fileDetails.Location = (path + (namesAvailable ? s_item.Name : "")).Replace("/", @"\");
                                fileDetails.Size = byteConversion(sizeValue);
                                fileDetails.Modified = s_item.DateModified;
                                DetailsResponse.Details = fileDetails;
                            }
                        }
                        else
                        {
                                multipleSize = multipleSize + (s_item.IsFile ? s_item.Size : getSizeValue(path + (namesAvailable ? s_item.Name : "")).Result);
                                size = 0;
                                isFile = s_item.IsFile;
                                fileDetails.IsFile = isFile;
                                fileDetails.Name = string.Join(", ", names);
                                fileDetails.Location = path.Replace("/", @"\");
                            fileDetails.Size = byteConversion(multipleSize);
                                fileDetails.MultipleFiles = true;
                                DetailsResponse.Details = fileDetails;
                        }

                    }
                }

                return await Task.Run(() =>
                {
                    size = 0;
                    return DetailsResponse;
                });
            }
            catch (Exception ex) { throw ex; }
        }
        // Creates a NewFolder
        public FileManagerResponse Create(string path, string name, params FileManagerDirectoryContent[] selectedItems)
        {
            this.isFolderAvailable = false;
            CreateFolderAsync(path, name, selectedItems).GetAwaiter().GetResult();
            FileManagerResponse CreateResponse = new FileManagerResponse();
            if (!this.isFolderAvailable)
            {
                FileManagerDirectoryContent content = new FileManagerDirectoryContent();
                content.Name = name;
                var directories = new[] { content };
                CreateResponse.Files = (IEnumerable<FileManagerDirectoryContent>)directories;
            }
            else
            {
                ErrorDetails er = new ErrorDetails();
                er.FileExists = existFiles;
                er.Code = "400";
                er.Message = "Folder Already Already Exists";
                CreateResponse.Error = er;
            }
            return CreateResponse;
        }
        // Creates a NewFolder
        public async Task CreateFolderAsync(string path, string name, IEnumerable<object> selectedItems = null)
        {
            if (await IsFolderExists(path + name))
            {
                this.isFolderAvailable = true;
            }
            else
            {
                CloudBlockBlob blob = container.GetBlockBlobReference(path + name + "/About.txt");
                blob.Properties.ContentType = "text/plain";
                await blob.UploadTextAsync("This is a auto generated file");
            }
            
        }
        // Renames file(s) or folder(s)
        public FileManagerResponse Rename(string path, string oldName, string newName, bool replace, params FileManagerDirectoryContent[] data)
        {
            return RenameAsync(path, oldName, newName, data).GetAwaiter().GetResult();
        }
        // Renames file(s) or folder(s)
        public async Task<FileManagerResponse> RenameAsync(string path, string oldName, string newName, params FileManagerDirectoryContent[] selectedItems)
        {
            FileManagerResponse renameResponse = new FileManagerResponse();
            List<FileManagerDirectoryContent> details = new List<FileManagerDirectoryContent>();
            CloudBlobDirectory directory = (CloudBlobDirectory)item;
            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
            bool isAlreadyAvailable = false;
            bool isFile = false;
            foreach (var Fileitem in selectedItems)
            {
                FileManagerDirectoryContent s_item = Fileitem;
                isFile = s_item.IsFile;
                if (isFile)
                {
                    isAlreadyAvailable = await IsFileExists(path + newName);
                }
                else
                {
                    isAlreadyAvailable = await IsFolderExists(path + newName);
                }
                entry.Name = newName;
                entry.Type = s_item.Type;
                entry.IsFile = isFile;
                entry.Size = s_item.Size;
                entry.HasChild = s_item.HasChild;
                entry.FilterPath = path;
                details.Add(entry);
                break;
            }
            if (!isAlreadyAvailable)
            {
                if (isFile)
                {
                    CloudBlob existBlob = container.GetBlobReference(path + oldName);
                    CloudBlob newBlob = container.GetBlobReference(path + newName);
                    await newBlob.StartCopyAsync(existBlob.Uri);
                    await existBlob.DeleteIfExistsAsync();
                }
                else
                {
                    CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path + oldName);
                    var items = await AsyncReadCall(path + oldName, "Rename");
                    foreach (var item in items.Results)
                    {
                        string name = item.Uri.AbsolutePath.Replace(sampleDirectory.Uri.AbsolutePath, "").Replace("%20", " ");
                        CloudBlob newBlob = container.GetBlobReference(path + newName + "/" + name);
                        await newBlob.StartCopyAsync(item.Uri);
                        await container.GetBlobReference(path + oldName + "/" + name).DeleteAsync();
                    }

                }
                renameResponse.Files = (IEnumerable<FileManagerDirectoryContent>)details;
            }
            else
            {
                ErrorDetails er = new ErrorDetails();
                er.FileExists = existFiles;
                er.Code = "400";
                er.Message = "File or Folder Already Already Exists";
                renameResponse.Error = er;
            }
            return renameResponse;
        }
        // Deletes file(s) or folder(s)
        public FileManagerResponse Delete(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            return RemoveAsync(names, path, data).GetAwaiter().GetResult();
        }
        // Deletes file(s) or folder(s)
        public async Task<FileManagerResponse> RemoveAsync(string[] names, string path, params FileManagerDirectoryContent[] selectedItems)
        {
            FileManagerResponse removeResponse = new FileManagerResponse();

            List<FileManagerDirectoryContent> details = new List<FileManagerDirectoryContent>();
            CloudBlobDirectory directory = (CloudBlobDirectory)item;
            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
            foreach (var Fileitem in selectedItems)
            {
                FileManagerDirectoryContent s_item = Fileitem;

                if (s_item.IsFile)
                {
                    CloudBlockBlob blockBlob = container.GetBlockBlobReference(path + s_item.Name);
                    await blockBlob.DeleteAsync();
                    entry.Name = s_item.Name;
                    entry.Type = s_item.Type;
                    entry.IsFile = s_item.IsFile;
                    entry.Size = s_item.Size;
                    entry.HasChild = s_item.HasChild;
                    entry.FilterPath = path;
                    details.Add(entry);
                }
                else
                {
                    CloudBlobDirectory subDirectory = container.GetDirectoryReference(path + s_item.Name);
                    var items = await AsyncReadCall(path + s_item.Name, "Remove");
                    foreach (var item in items.Results)
                    {
                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(path + s_item.Name + "/" + item.Uri.ToString().Replace(subDirectory.Uri.ToString(), ""));
                        await blockBlob.DeleteAsync();
                        entry.Name = s_item.Name;
                        entry.Type = s_item.Type;
                        entry.IsFile = s_item.IsFile;
                        entry.Size = s_item.Size;
                        entry.HasChild = s_item.HasChild;
                        entry.FilterPath = path;
                        details.Add(entry);
                    }
                }
            }
            removeResponse.Files = (IEnumerable<FileManagerDirectoryContent>)details;
            return removeResponse;

        }
        // Upload file(s) to the storage
        public FileManagerResponse Upload(string path, IList<IFormFile> files, string action, params FileManagerDirectoryContent[] data)
        {
            return UploadAsync(files, path, data).GetAwaiter().GetResult();
        }
        // Upload file(s) to the storage
        public async Task<FileManagerResponse> UploadAsync(IEnumerable<IFormFile> files, string path, IEnumerable<object> selectedItems = null)
        {
            try
            {
                string MyPath = path.Replace(this.BlobPath, "");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    CloudBlockBlob blockBlob = container.GetBlockBlobReference(MyPath + file.FileName);
                    blockBlob.Properties.ContentType = file.ContentType;
                    await blockBlob.UploadFromStreamAsync(file.OpenReadStream());
                }
            }
            catch (Exception ex) { throw ex; }
            return null;
        }

        public async Task CopyFileToTemp(string path, CloudBlockBlob blockBlob)
        {
            using (var fileStream = System.IO.File.Create(path))
            {
                await blockBlob.DownloadToStreamAsync(fileStream);
                fileStream.Close();
            }
        }
        // Download file(s) from the storage
        public virtual FileStreamResult Download(string path, string[] names = null, params FileManagerDirectoryContent[] selectedItems)
        {
            return DownloadAsync(this.FilesPath + path + "", names, selectedItems).GetAwaiter().GetResult();
        }
        // Download file(s) from the storage
        public async Task<FileStreamResult> DownloadAsync(string path, string[] names = null, params FileManagerDirectoryContent[] selectedItems)
        {
            string MyPath = path.Replace(this.BlobPath, "");
            foreach (var file in selectedItems)
            {
                if (file.IsFile && selectedItems.Count() == 1)
                {
                    CloudBlockBlob blockBlob = container.GetBlockBlobReference(MyPath + file.Name);
                    if (File.Exists(Path.Combine(Path.GetTempPath(), file.Name)))
                    {
                        File.Delete(Path.Combine(Path.GetTempPath(), file.Name));
                    }
                    var localPath = Path.GetTempPath() + file.Name;  // <-- Change your download target path here
                    await CopyFileToTemp(localPath, blockBlob);
                    FileStream fileStreamInput = new FileStream(Path.Combine(Path.GetTempPath(), file.Name), FileMode.Open, FileAccess.Read);
                    FileStreamResult fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = file.Name;
                    return fileStreamResult;
                }
                else
                {
                    ZipArchiveEntry zipEntry = null;
                    ZipArchive archive;
                    var tempPath = Path.Combine(Path.GetTempPath(), "temp.zip");
                    using (archive = ZipFile.Open(tempPath, ZipArchiveMode.Update))
                    {
                        foreach (var files in selectedItems)
                        {
                            if (files.IsFile)
                            {
                                CloudBlockBlob blockBlob = container.GetBlockBlobReference(MyPath + files.Name);
                                if (File.Exists(Path.Combine(Path.GetTempPath(), files.Name)))
                                {
                                    File.Delete(Path.Combine(Path.GetTempPath(), files.Name));
                                }
                                var localPath = Path.GetTempPath() + files.Name;
                                await CopyFileToTemp(localPath, blockBlob);
                                zipEntry = archive.CreateEntryFromFile(Path.GetTempPath() + files.Name, files.Name, CompressionLevel.Fastest);
                                if (File.Exists(Path.Combine(Path.GetTempPath(), files.Name)))
                                {
                                    File.Delete(Path.Combine(Path.GetTempPath(), files.Name));
                                }
                            }
                            else
                            {
                                string subFolder = path.Replace(this.FilesPath + "/", "");
                                path = path.Replace(this.BlobPath, "");
                                PathValue = path + files.Name;
                                await DownloadFolder(path, subFolder + files.Name, zipEntry, archive);
                            }
                        }                        
                    }                
                    archive.Dispose();
                    FileStream fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                    FileStreamResult fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = "Files.zip";
                    if (File.Exists(Path.Combine(Path.GetTempPath(), "temp.zip"))) ;
                    {
                        File.Delete(Path.Combine(Path.GetTempPath(), "temp.zip"));
                    }
                    return fileStreamResult;
                }
            }
            return null;
        }

        // Download folder(s) from the storage
        private async Task DownloadFolder(string path, string Name, ZipArchiveEntry zipEntry, ZipArchive archive)
        {
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(PathValue);
            var items = await AsyncReadCall(PathValue, "Read");
            foreach (var item in items.Results)
            {
                if (item is CloudBlockBlob blockBlob)
                {
                    CloudBlockBlob blob = container.GetBlockBlobReference(new CloudBlockBlob(item.Uri).Name);
                    await blob.FetchAttributesAsync();
                    var localPath = Path.GetTempPath() + blob.Name.Split("/").Last();
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }
                    await CopyFileToTemp(localPath, blob);
                    zipEntry = archive.CreateEntryFromFile(localPath , Name.Replace("/","\\") + "\\" + blob.Name.Split("/").Last(), CompressionLevel.Fastest);
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }
                }
                else if (item is CloudBlobDirectory blobDirectory)
                {
                    var localPath = item.Uri.ToString().Replace(this.BlobPath, ""); // <-- Change your download target path here
                    PathValue = localPath;
                    string toPath = item.Uri.ToString().Replace(this.FilesPath + "/", "");
                    await DownloadFolder(localPath, toPath.Substring(0, toPath.Length -1), zipEntry, archive);
                }
            }
        }
        // Check whether the directory has child
        private async Task<bool> HasChildDirectory(string path)
        {
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
            var items = await AsyncReadCall(path, "HasChild");
            foreach (var item in items.Results)
            {
                if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    return true;
                }
            }
            return false;
        }

        // To get the file details
        private FileManagerDirectoryContent GetFileDetails(string targetPath, FileManagerDirectoryContent fileDetails)
        {
            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
            FileManagerDirectoryContent s_item = fileDetails;
            entry.Name = fileDetails.Name;
            entry.Type = s_item.Type;
            entry.IsFile = s_item.IsFile;
            entry.Size = s_item.Size;
            entry.HasChild = s_item.HasChild;
            entry.FilterPath = targetPath;
            return entry;
        }

        // To check if folder exists
        private async Task<bool> IsFolderExists(string path)
        {
            var items = await AsyncReadCall(path, "Paste");
            return await Task.Run(() =>
            {
                return items.Results.Count<IListBlobItem>() > 0;
            });

        }

        // To check if file exists
        private async Task<bool> IsFileExists(string path)
        {
            CloudBlob newBlob = container.GetBlobReference(path);
            return await newBlob.ExistsAsync();
        }


        //To copy files
        public FileManagerResponse Copy(string path, string targetPath, string[] fileNames, string[] renameFiles, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] selectedItems)
        {
            return CopyToAsync(path, targetPath, fileNames, renameFiles, selectedItems).GetAwaiter().GetResult();
        }

        private async Task<FileManagerResponse> CopyToAsync(string path, string targetPath, string[] fileNames, string[] renamedFiles = null, params FileManagerDirectoryContent[] selectedItems)
        {
            FileManagerResponse copyResponse = new FileManagerResponse();
            try
            {
                renamedFiles = renamedFiles ?? new string[0];
                foreach (var item in selectedItems)
                {
                    if (item.IsFile)
                    {
                        if (await IsFileExists(targetPath + item.Name))
                        {
                            int index = -1;
                            if (renamedFiles.Length > 0)
                            {
                                index = Array.FindIndex(renamedFiles, Items => Items.Contains(item.Name));
                            }
                            if ((path == targetPath) || (index != -1))
                            {
                                var newName = await FileRename(targetPath, item.Name);
                                await CopyItems(path, targetPath, item.Name, newName);
                                copiedFiles.Add(GetFileDetails(targetPath, item));
                            }
                            else
                            {
                                this.existFiles.Add(item.Name);
                            }
                        }
                        else
                        {
                            await CopyItems(path, targetPath, item.Name, null);
                            copiedFiles.Add(GetFileDetails(targetPath, item));
                        }
                    }
                    else
                    {
                        if (!await IsFolderExists(path + item.Name))
                        {
                            missingFiles.Add(item.Name);
                        }
                        else if (await IsFolderExists(targetPath + item.Name))
                        {
                            int index = -1;
                            if (renamedFiles.Length > 0)
                            {

                                index = Array.FindIndex(renamedFiles, Items => Items.Contains(item.Name));
                            }
                            if ((path == targetPath) || (index != -1))
                            {
                                item.Path = path + item.Name;
                                item.Name = await FileRename(targetPath, item.Name);
                                CopySubFolder(item, targetPath);
                                copiedFiles.Add(GetFileDetails(targetPath, item));
                            }
                            else
                            {
                                existFiles.Add(item.Name);
                            }
                        }
                        else
                        {
                            item.Path = path + item.Name;
                            CopySubFolder(item, targetPath);
                            copiedFiles.Add(GetFileDetails(targetPath, item));
                        }
                    }

                }

                CloudBlobDirectory directory = (CloudBlobDirectory)item;
                copyResponse.Files = copiedFiles;
                CloudBlobDirectory directory1 = container.GetDirectoryReference(targetPath);
                if (existFiles.Count > 0)
                {
                    ErrorDetails er = new ErrorDetails();
                    er.FileExists = existFiles;
                    er.Code = "400";
                    er.Message = "File Already Exists";
                    copyResponse.Error = er;
                }
                if (missingFiles.Count > 0)
                {
                    var nameList = missingFiles[0];
                    for (int k = 1; k < missingFiles.Count; k++)
                    {
                        nameList = nameList + ", " + missingFiles[k];
                    }
                    throw new FileNotFoundException(nameList + " not found in given location.");
                }

                return copyResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "404";
                er.Message = e.Message.ToString();
                er.FileExists = copyResponse.Error?.FileExists;
                copyResponse.Error = er;
                return copyResponse;
            }
        }

        // To iterate and copy subfolder
        private async void CopySubFolder(FileManagerDirectoryContent subfolder, string targetPath)
        {
            CloudBlobDirectory blobDirectory = container.GetDirectoryReference(targetPath);
            var items = await AsyncReadCall(subfolder.Path, "Paste");
            await CreateFolderAsync(targetPath, subfolder.Name);
            targetPath = targetPath + subfolder.Name + "/";
            foreach (var item in items.Results)
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    var BlobItem = (CloudBlockBlob)item;
                    var name = BlobItem.Name.Replace(subfolder.Path + "/", "");
                    var sourcePath = BlobItem.Name.Replace(name, "");
                    await CopyItems(sourcePath, targetPath, name, null);
                }
                if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    var BlobItem = (CloudBlobDirectory)item;
                    FileManagerDirectoryContent itemDetail = new FileManagerDirectoryContent();
                    itemDetail.Name = BlobItem.Prefix.Replace(subfolder.Path, "").Replace("/", "");
                    itemDetail.Path = subfolder.Path + "/" + itemDetail.Name;
                    CopySubFolder(itemDetail, targetPath);
                }
            }
        }

        // To iterate and copy files
        private async Task CopyItems(string sourcePath, string targetPath, string name, string newName)
        {
            if (newName == null) { newName = name; }
            CloudBlob existBlob = container.GetBlobReference(sourcePath + name);
            CloudBlob newBlob = container.GetBlobReference(targetPath + newName);
            await newBlob.StartCopyAsync(existBlob.Uri);
        }

        // To rename files incase of duplicates
        private async Task<string> FileRename(string newPath, string fileName)
        {
            int index = fileName.LastIndexOf(".");
            var NameNoExt = string.Empty;
            NameNoExt = index >= 0 ? fileName.Substring(0, index) : fileName;
            int fileCount = 0;
            while (index > -1 ? await IsFileExists(newPath + NameNoExt + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(fileName) : Path.GetExtension(fileName))) : await IsFolderExists(newPath + NameNoExt + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(fileName) : Path.GetExtension(fileName))))
            {
                fileCount++;
            }
            fileName = NameNoExt + (fileCount > 0 ? "(" + fileCount.ToString() + ")" : "") + Path.GetExtension(fileName);
            return await Task.Run(() =>
            {
                return fileName;
            });
        }

        public FileManagerResponse GetFiles(string path, bool showHiddenItems, params FileManagerDirectoryContent[] data)
        {
            throw new NotImplementedException();
        }
        // Returns the image 
        public FileStreamResult GetImage(string path, string id, bool allowCompress, ImageSize size, params FileManagerDirectoryContent[] data)
        {
            var webClient = new WebClient();
            byte[] imageBytes = webClient.DownloadData(Path.Join(this.FilesPath, path));
            Stream stream = new MemoryStream(imageBytes);
            FileStreamResult fileStreamResult = new FileStreamResult(stream, "APPLICATION/octet-stream");
            return fileStreamResult;
        }

        private async Task MoveItems(string sourcePath, string targetPath, string name, string newName)
        {
            CloudBlob existBlob = container.GetBlobReference(sourcePath + name);
            await CopyItems(sourcePath, targetPath, name, newName);
            await existBlob.DeleteIfExistsAsync();
        }


        private async void MoveSubFolder(FileManagerDirectoryContent subfolder, string targetPath)
        {
            CloudBlobDirectory blobDirectory = container.GetDirectoryReference(targetPath);
            var items = await AsyncReadCall(subfolder.Path, "Paste");
            await CreateFolderAsync(targetPath, subfolder.Name);
            targetPath = targetPath + subfolder.Name + "/";
            foreach (var item in items.Results)
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    var BlobItem = (CloudBlockBlob)item;
                    var name = BlobItem.Name.Replace(subfolder.Path + "/", "");
                    var sourcePath = BlobItem.Name.Replace(name, "");
                    await MoveItems(sourcePath, targetPath, name, null);
                }
                if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    var BlobItem = (CloudBlobDirectory)item;
                    FileManagerDirectoryContent itemDetail = new FileManagerDirectoryContent();
                    itemDetail.Name = BlobItem.Prefix.Replace(subfolder.Path, "").Replace("/", "");
                    itemDetail.Path = subfolder.Path + "/" + itemDetail.Name;
                    CopySubFolder(itemDetail, targetPath);
                }
            }
        }


        // To move folders or files
        public FileManagerResponse Move(string path, string targetPath, string[] fileNames, string[] renameFiles, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] selectedItems)
        {
            return MoveToAsync(path, targetPath, fileNames, renameFiles, selectedItems).GetAwaiter().GetResult();
        }


        private async Task<FileManagerResponse> MoveToAsync(string path, string targetPath, string[] fileNames, string[] renamedFiles = null, params FileManagerDirectoryContent[] selectedItems)
        {
            FileManagerResponse moveResponse = new FileManagerResponse();
            try
            {
                renamedFiles = renamedFiles ?? new string[0];
                foreach (var item in selectedItems)
                {
                    if (item.IsFile)
                    {
                        if (await IsFileExists(targetPath + item.Name))
                        {
                            int index = -1;
                            if (renamedFiles.Length > 0)
                            {
                                index = Array.FindIndex(renamedFiles, Items => Items.Contains(item.Name));
                            }
                            if ((path == targetPath) || (index != -1))
                            {
                                var newName = await FileRename(targetPath, item.Name);
                                await MoveItems(path, targetPath, item.Name, newName);
                                copiedFiles.Add(GetFileDetails(targetPath, item));
                            }
                            else
                            {
                                this.existFiles.Add(item.Name);
                            }
                        }
                        else
                        {
                            await MoveItems(path, targetPath, item.Name, null);
                            copiedFiles.Add(GetFileDetails(targetPath, item));
                        }
                    }
                    else
                    {
                        if (!await IsFolderExists(path + item.Name))
                        {
                            missingFiles.Add(item.Name);
                        }
                        else if (await IsFolderExists(targetPath + item.Name))
                        {
                            int index = -1;
                            if (renamedFiles.Length > 0)
                            {

                                index = Array.FindIndex(renamedFiles, Items => Items.Contains(item.Name));
                            }
                            if ((path == targetPath) || (index != -1))
                            {
                                item.Path = path + item.Name;
                                item.Name = await FileRename(targetPath, item.Name);
                                MoveSubFolder(item, targetPath);
                                copiedFiles.Add(GetFileDetails(targetPath, item));
                            }
                            else
                            {
                                existFiles.Add(item.Name);
                            }
                        }
                        else
                        {
                            item.Path = path + item.Name;
                            MoveSubFolder(item, targetPath);
                            copiedFiles.Add(GetFileDetails(targetPath, item));
                        }
                    }

                }

                CloudBlobDirectory directory = (CloudBlobDirectory)item;
                moveResponse.Files = copiedFiles;
                CloudBlobDirectory directory1 = container.GetDirectoryReference(targetPath);
                if (existFiles.Count > 0)
                {
                    ErrorDetails er = new ErrorDetails();
                    er.FileExists = existFiles;
                    er.Code = "400";
                    er.Message = "File Already Exists";
                    moveResponse.Error = er;
                }
                if (missingFiles.Count > 0)
                {
                    var nameList = missingFiles[0];
                    for (int k = 1; k < missingFiles.Count; k++)
                    {
                        nameList = nameList + ", " + missingFiles[k];
                    }
                    throw new FileNotFoundException(nameList + " not found in given location.");
                }

                return moveResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "404";
                er.Message = e.Message.ToString();
                er.FileExists = moveResponse.Error?.FileExists;
                moveResponse.Error = er;
                return moveResponse;
            }
        }

        // Search for file(s) or folders
        public FileManagerResponse Search(string path, string searchString, bool showHiddenItems, bool caseSensitive, params FileManagerDirectoryContent[] data)
        {


            Items.Clear();
            FileManagerResponse res = (FileManagerResponse)Read(path, data);
            Items.AddRange(res.Files);
            getAllFiles(path, res);
            res.Files = Items.Where(item => new Regex(WildcardToRegex(searchString), (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)).IsMatch(item.Name));
            return res;
        }
        // Gets all files
        public virtual void getAllFiles(string path, FileManagerResponse data)
        {
            FileManagerResponse directoryList = new FileManagerResponse();
            directoryList.Files = (IEnumerable<FileManagerDirectoryContent>)data.Files.Where(item => item.IsFile == false);
            for (int i = 0; i < directoryList.Files.Count(); i++)
            {

                FileManagerDirectoryContent[] selectedItem = new[] { directoryList.Files.ElementAt(i) };
                FileManagerResponse innerData = (FileManagerResponse)Read(path + directoryList.Files.ElementAt(i).Name + "/", selectedItem);
                innerData.Files = innerData.Files.Select(file => new FileManagerDirectoryContent
                {
                    Name = file.Name,
                    Type = file.Type,
                    IsFile = file.IsFile,
                    Size = file.Size,
                    HasChild = file.HasChild,
                    FilterPath = (file.FilterPath)
                });
                Items.AddRange(innerData.Files);
                getAllFiles(path + directoryList.Files.ElementAt(i).Name + "/", innerData);
            }
        }


        public virtual string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                              .Replace(@"\*", ".*")
                              .Replace(@"\?", ".")
                       + "$";
        }
    }
}