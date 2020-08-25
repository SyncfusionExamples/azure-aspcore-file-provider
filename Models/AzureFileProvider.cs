using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Syncfusion.EJ2.FileManager.Base;


namespace Syncfusion.EJ2.FileManager.AzureFileProvider
{
    public class AzureFileProvider : AzureFileProviderBase
    {
        List<FileManagerDirectoryContent> directoryContentItems = new List<FileManagerDirectoryContent>();
        CloudBlobContainer container;
        CloudBlobDirectory item;
        string pathValue;
        string blobPath;
        string filesPath;
        long size;
        string rootPath;
        string currentFolderName = "";
        string previousFolderName = "";
        string initialFolderName = "";
        List<string> existFiles = new List<string>();
        List<string> missingFiles = new List<string>();
        bool isFolderAvailable = false;
        List<FileManagerDirectoryContent> copiedFiles = new List<FileManagerDirectoryContent>();
        DateTime lastUpdated = DateTime.MinValue;
        DateTime prevUpdated = DateTime.MinValue;

        // Registering the azure storage 
        public void RegisterAzure(string accountName, string accountKey, string blobName)
        {
            container = new CloudStorageAccount(new StorageCredentials(accountName, accountKey), useHttps: true).CreateCloudBlobClient().GetContainerReference(blobName);
        }
        // Sets blob and file path
        public void SetBlobContainer(string blobPath, string filePath)
        {
            this.blobPath = blobPath;
            this.filesPath = filePath;
            this.rootPath = this.filesPath.Replace(this.blobPath, "");
        }

        // Performs files operations
        protected async Task<BlobResultSegment> AsyncReadCall(string path, string action)
        {
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
            BlobRequestOptions options = new BlobRequestOptions();
            OperationContext context = new OperationContext();
            dynamic Asyncitem = null;
            if (action == "Read") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(false, BlobListingDetails.Metadata, null, null, options, context);
            if (action == "Paste") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(false, BlobListingDetails.None, null, null, options, context);
            if (action == "Rename") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(true, BlobListingDetails.Metadata, null, null, options, context);
            if (action == "Remove") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(true, BlobListingDetails.None, null, null, options, context);
            if (action == "HasChild") Asyncitem = await sampleDirectory.ListBlobsSegmentedAsync(false, BlobListingDetails.None, null, null, options, context);
            // Return Asyncitem;
            return await Task.Run(() =>
            {
                return Asyncitem;
            });
        }
        // Reads the storage 
        public FileManagerResponse GetFiles(string path, bool showHiddenItems, FileManagerDirectoryContent[] selectedItems)
        {
            return GetFilesAsync(path, "*.*", selectedItems).GetAwaiter().GetResult();
        }

        // Reads the storage files
        protected async Task<FileManagerResponse> GetFilesAsync(string path, string filter, FileManagerDirectoryContent[] selectedItems)
        {
            FileManagerResponse readResponse = new FileManagerResponse();
            List<FileManagerDirectoryContent> details = new List<FileManagerDirectoryContent>();
            FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
            try
            {
                string[] extensions = ((filter.Replace(" ", "")) ?? "*").Split(",|;".ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
                CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
                cwd.Name = sampleDirectory.Prefix.Split(sampleDirectory.Parent.Prefix)[sampleDirectory.Prefix.Split(sampleDirectory.Parent.Prefix).Length - 1].Replace("/", "");
                cwd.Type = "File Folder";
                cwd.FilterPath = selectedItems.Length > 0 ? selectedItems[0].FilterPath : "";
                cwd.Size = 0;
                cwd.HasChild = await HasChildDirectory(path);
                readResponse.CWD = cwd;
                BlobResultSegment items = await AsyncReadCall(path, "Read");
                foreach (IListBlobItem item in items.Results)
                {
                    bool includeItem = true;
                    if (!(extensions[0].Equals("*.*") || extensions[0].Equals("*")) && item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob file = (CloudBlockBlob)item;
                        if (!(Array.IndexOf(extensions, "*." + (file.Name.ToString().Trim().Split('.'))[(file.Name.ToString().Trim().Split('.')).Count() - 1]) >= 0))
                            includeItem = false;
                    }
                    if (includeItem)
                    {
                        FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
                        if (item.GetType() == typeof(CloudBlockBlob))
                        {
                            CloudBlockBlob file = (CloudBlockBlob)item;
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
                            entry.Name = directory.Prefix.Replace(path, "").Replace("/", "");
                            entry.Type = "Directory";
                            entry.IsFile = false;
                            entry.Size = 0;
                            entry.HasChild = await HasChildDirectory(directory.Prefix);
                            entry.FilterPath = selectedItems.Length > 0 ? path.Replace(this.rootPath, "") : "/";
                            entry.DateModified = await DirectoryLastModified(directory.Prefix);
                            lastUpdated = prevUpdated = DateTime.MinValue;
                            details.Add(entry);
                        }
                    }
                }
            }
            catch (Exception)
            {
                return readResponse;
            }
            readResponse.Files = details;
            return readResponse;
        }
        // Returns the last modified date for directories
        protected async Task<DateTime> DirectoryLastModified(string path)
        {
            BlobResultSegment items = await AsyncReadCall(path, "Read");
            // Checks the corresponding folder's last modified date of recent updated file from any of its sub folders. 
            if (items.Results.Where(x => x.GetType() == typeof(CloudBlobDirectory)).Select(x => x).ToList().Count > 0)
            {
                List<IListBlobItem> folderItems = items.Results.Where(x => x.GetType() == typeof(CloudBlobDirectory)).Select(x => x).ToList();
                foreach (IListBlobItem item in folderItems)
                {
                    DateTime checkFolderModified = DirectoryLastModified(((CloudBlobDirectory)item).Prefix).Result;
                    lastUpdated = prevUpdated = (prevUpdated < checkFolderModified) ? checkFolderModified : prevUpdated;
                }
            }
            // Checks the corresponding folder's last modified date of recent updated file
            if (items.Results.Where(x => x.GetType() == typeof(CloudBlockBlob)).Select(x => x).ToList().Count > 0)
            {
                DateTime checkFileModified = ((CloudBlockBlob)items.Results.Where(x => x.GetType() == typeof(CloudBlockBlob)).Select(x => x).ToList().OrderByDescending(m => ((CloudBlockBlob)m).Properties.LastModified).ToList().First()).Properties.LastModified.Value.LocalDateTime;
                lastUpdated = prevUpdated = prevUpdated < checkFileModified ? checkFileModified : prevUpdated;
            }
            return lastUpdated;
        }
        // Converts the byte size value to appropriate value
        protected string ByteConversion(long fileSize)
        {
            try
            {
                string[] index = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
                if (fileSize == 0)
                {
                    return "0 " + index[0];
                }
                int value = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(fileSize), 1024)));
                return (Math.Sign(fileSize) * Math.Round(Math.Abs(fileSize) / Math.Pow(1024, value), 1)).ToString() + " " + index[value];
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        // Gets the size value of the directory
        protected async Task<long> GetSizeValue(string path)
        {
            BlobResultSegment items = await AsyncReadCall(path, "Read");
            foreach (IListBlobItem item in items.Results)
            {
                if (item is CloudBlockBlob blockBlob)
                {
                    CloudBlockBlob blob = container.GetBlockBlobReference(new CloudBlockBlob(item.Uri).Name);
                    await blob.FetchAttributesAsync();
                    size = size + blob.Properties.Length;
                }
                else if (item is CloudBlobDirectory blobDirectory)
                {
                    // Set your download target path as below methods parameter
                    await GetSizeValue(item.Uri.ToString().Replace(blobPath, ""));
                }
            }
            return size;
        }
        // Gets details of the files
        public FileManagerResponse Details(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            return GetDetailsAsync(path, names, data).GetAwaiter().GetResult();

        }
        // Gets the details
        protected async Task<FileManagerResponse> GetDetailsAsync(string path, string[] names, IEnumerable<object> selectedItems = null)
        {
            bool isVariousFolders = false;
            string previousPath = "";
            string previousName = "";
            FileManagerResponse detailsResponse = new FileManagerResponse();
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
                FileDetails fileDetails = new FileDetails();
                long multipleSize = 0;
                if (selectedItems != null)
                {
                    foreach (FileManagerDirectoryContent fileItem in selectedItems)
                    {

                        if (names.Length == 1)
                        {
                            if (fileItem.IsFile)
                            {
                                var blob = container.GetBlockBlobReference(rootPath + fileItem.FilterPath + fileItem.Name);
                                isFile = fileItem.IsFile;
                                fileDetails.IsFile = isFile;
                                await blob.FetchAttributesAsync();
                                fileDetails.Name = fileItem.Name;
                                fileDetails.Location = ((namesAvailable ? (rootPath + fileItem.FilterPath + fileItem.Name) : path)).Replace("/", @"\");
                                fileDetails.Size = ByteConversion(blob.Properties.Length);
                                fileDetails.Modified = blob.Properties.LastModified.Value.LocalDateTime;
                                detailsResponse.Details = fileDetails;
                            }

                            else
                            {
                                CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(rootPath + fileItem.FilterPath + fileItem.Name);
                                long sizeValue = GetSizeValue((namesAvailable ? rootPath + fileItem.FilterPath + fileItem.Name : "")).Result;
                                isFile = false;
                                fileDetails.Name = fileItem.Name;
                                fileDetails.Location = ((namesAvailable ? rootPath + fileItem.FilterPath + fileItem.Name : path.Substring(0, path.Length - 1))).Replace("/", @"\");
                                fileDetails.Size = ByteConversion(sizeValue);
                                fileDetails.Modified = await DirectoryLastModified(path);
                                detailsResponse.Details = fileDetails;
                            }
                        }
                        else
                        {
                            multipleSize = multipleSize + (fileItem.IsFile ? fileItem.Size : GetSizeValue((namesAvailable ? rootPath + fileItem.FilterPath + fileItem.Name : path)).Result);
                            size = 0;
                            fileDetails.Name = previousName == "" ? previousName = fileItem.Name : previousName + ", " + fileItem.Name;
                            previousPath = previousPath == "" ? rootPath + fileItem.FilterPath : previousPath;
                            if (previousPath == rootPath + fileItem.FilterPath && !isVariousFolders)
                            {
                                previousPath = rootPath + fileItem.FilterPath;
                                fileDetails.Location = ((rootPath + fileItem.FilterPath).Replace("/", @"\")).Substring(0, ((rootPath + fileItem.FilterPath).Replace("/", @"\")).Length - 1);
                            }
                            else
                            {
                                isVariousFolders = true;
                                fileDetails.Location = "Various Folders";
                            }
                            fileDetails.Size = ByteConversion(multipleSize);
                            fileDetails.MultipleFiles = true;
                            detailsResponse.Details = fileDetails;
                        }

                    }
                }
                return await Task.Run(() =>
                {
                    size = 0;
                    return detailsResponse;
                });
            }
            catch (Exception ex) { throw ex; }
        }
        // Creates a new folder
        public FileManagerResponse Create(string path, string name, params FileManagerDirectoryContent[] selectedItems)
        {
            this.isFolderAvailable = false;
            CreateFolderAsync(path, name, selectedItems).GetAwaiter().GetResult();
            FileManagerResponse createResponse = new FileManagerResponse();
            if (!this.isFolderAvailable)
            {
                FileManagerDirectoryContent content = new FileManagerDirectoryContent();
                content.Name = name;
                FileManagerDirectoryContent[] directories = new[] { content };
                createResponse.Files = (IEnumerable<FileManagerDirectoryContent>)directories;
            }
            else
            {
                ErrorDetails error = new ErrorDetails();
                error.FileExists = existFiles;
                error.Code = "400";
                error.Message = "Folder Already Already Exists";
                createResponse.Error = error;
            }
            return createResponse;
        }
        // Creates a new folder
        protected async Task CreateFolderAsync(string path, string name, IEnumerable<object> selectedItems = null)
        {
            BlobResultSegment items = await AsyncReadCall(path, "Read");
            string checkName = name.Contains(" ") ? name.Replace(" ", "%20") : name;
            if (await IsFolderExists(path + name) || (items.Results.Where(x => x.Uri.Segments.Last().Replace("/", "").ToLower() == checkName.ToLower()).Select(i => i).ToArray().Length > 0))
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
        public FileManagerResponse Rename(string path, string oldName, string newName, bool replace = false, params FileManagerDirectoryContent[] data)
        {
            return RenameAsync(path, oldName, newName, data).GetAwaiter().GetResult();
        }
        // Renames file(s) or folder(s)
        protected async Task<FileManagerResponse> RenameAsync(string path, string oldName, string newName, params FileManagerDirectoryContent[] selectedItems)
        {
            FileManagerResponse renameResponse = new FileManagerResponse();
            List<FileManagerDirectoryContent> details = new List<FileManagerDirectoryContent>();
            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
            bool isAlreadyAvailable = false;
            bool isFile = false;
            foreach (FileManagerDirectoryContent fileItem in selectedItems)
            {
                FileManagerDirectoryContent directoryContent = fileItem;
                isFile = directoryContent.IsFile;
                if (isFile)
                {
                    isAlreadyAvailable = await IsFileExists(path + newName);
                }
                else
                {
                    isAlreadyAvailable = await IsFolderExists(path + newName);
                }
                entry.Name = newName;
                entry.Type = directoryContent.Type;
                entry.IsFile = isFile;
                entry.Size = directoryContent.Size;
                entry.HasChild = directoryContent.HasChild;
                entry.FilterPath = path;
                details.Add(entry);
                break;
            }
            if (!isAlreadyAvailable)
            {
                if (isFile)
                {
                    CloudBlob existBlob = container.GetBlobReference(path + oldName);
                    await (container.GetBlobReference(path + newName)).StartCopyAsync(existBlob.Uri);
                    await existBlob.DeleteIfExistsAsync();
                }
                else
                {
                    CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path + oldName);
                    BlobResultSegment items = await AsyncReadCall(path + oldName, "Rename");
                    foreach (IListBlobItem item in items.Results)
                    {
                        string name = item.Uri.AbsolutePath.Replace(sampleDirectory.Uri.AbsolutePath, "").Replace("%20", " ");
                        await (container.GetBlobReference(path + newName + "/" + name)).StartCopyAsync(item.Uri);
                        await container.GetBlobReference(path + oldName + "/" + name).DeleteAsync();
                    }

                }
                renameResponse.Files = (IEnumerable<FileManagerDirectoryContent>)details;
            }
            else
            {
                ErrorDetails error = new ErrorDetails();
                error.FileExists = existFiles;
                error.Code = "400";
                error.Message = "File or Folder Already Already Exists";
                renameResponse.Error = error;
            }
            return renameResponse;
        }
        // Deletes file(s) or folder(s)
        public FileManagerResponse Delete(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            return RemoveAsync(names, path, data).GetAwaiter().GetResult();
        }
        // Deletes file(s) or folder(s)
        protected async Task<FileManagerResponse> RemoveAsync(string[] names, string path, params FileManagerDirectoryContent[] selectedItems)
        {
            FileManagerResponse removeResponse = new FileManagerResponse();
            List<FileManagerDirectoryContent> details = new List<FileManagerDirectoryContent>();
            CloudBlobDirectory directory = (CloudBlobDirectory)item;
            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
            foreach (FileManagerDirectoryContent fileItem in selectedItems)
            {
                if (fileItem.IsFile)
                {
                    path = this.filesPath.Replace(this.blobPath, "") + fileItem.FilterPath;
                    CloudBlockBlob blockBlob = container.GetBlockBlobReference(path + fileItem.Name);
                    await blockBlob.DeleteAsync();
                    string absoluteFilePath = Path.Combine(Path.GetTempPath(), fileItem.Name);
                    var tempDirectory = new DirectoryInfo(Path.GetTempPath());
                    foreach (var file in Directory.GetFiles(tempDirectory.ToString()))
                    {
                        if (file.ToString() == absoluteFilePath)
                        {
                            File.Delete(file);
                        }
                    }
                    entry.Name = fileItem.Name;
                    entry.Type = fileItem.Type;
                    entry.IsFile = fileItem.IsFile;
                    entry.Size = fileItem.Size;
                    entry.HasChild = fileItem.HasChild;
                    entry.FilterPath = path;
                    details.Add(entry);
                }
                else
                {
                    path = this.filesPath.Replace(this.blobPath, "") + fileItem.FilterPath;
                    CloudBlobDirectory subDirectory = container.GetDirectoryReference(path + fileItem.Name);
                    BlobResultSegment items = await AsyncReadCall(path + fileItem.Name, "Remove");
                    foreach (IListBlobItem item in items.Results)
                    {
                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(path + fileItem.Name + "/" + item.Uri.ToString().Replace(subDirectory.Uri.ToString(), ""));
                        await blockBlob.DeleteAsync();
                        entry.Name = fileItem.Name;
                        entry.Type = fileItem.Type;
                        entry.IsFile = fileItem.IsFile;
                        entry.Size = fileItem.Size;
                        entry.HasChild = fileItem.HasChild;
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
            return UploadAsync(files, action, path, data).GetAwaiter().GetResult();
        }
        // Upload file(s) to the storage
        protected async Task<FileManagerResponse> UploadAsync(IEnumerable<IFormFile> files, string action, string path, IEnumerable<object> selectedItems = null)
        {
            FileManagerResponse uploadResponse = new FileManagerResponse();
            try
            {             
                foreach (IFormFile file in files)
                {
                    if (files != null)
                    {
                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(path.Replace(this.blobPath, "") + file.FileName);
                        blockBlob.Properties.ContentType = file.ContentType;                     
                        string fileName = file.FileName;
                        string absoluteFilePath = Path.Combine(path, fileName);
                        if (action == "save")
                        {
                            if (!await IsFileExists(absoluteFilePath))
                            {
                                await blockBlob.UploadFromStreamAsync(file.OpenReadStream());
                            }
                            else
                            {
                                existFiles.Add(fileName);
                            }
                        }
                        else if (action == "replace")
                        {
                            if (await IsFileExists(absoluteFilePath))
                            {
                                await blockBlob.DeleteAsync();
                            }
                             
                            await blockBlob.UploadFromStreamAsync(file.OpenReadStream());
                        }
                        else if (action == "keepboth")
                        {
                            string newAbsoluteFilePath = absoluteFilePath;
                            string newFileName = file.FileName;
                            int index = absoluteFilePath.LastIndexOf(".");
                            int indexValue = newFileName.LastIndexOf(".");
                            if (index >= 0)
                            {
                                newAbsoluteFilePath = absoluteFilePath.Substring(0, index);
                                newFileName = newFileName.Substring(0, indexValue);
                            }
                            int fileCount = 0;
                            while (await IsFileExists(newAbsoluteFilePath + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(fileName) : Path.GetExtension(fileName))))
                            {
                                fileCount++;
                            }

                            newAbsoluteFilePath = newFileName + (fileCount > 0 ? "(" + fileCount.ToString() + ")" : "") + Path.GetExtension(fileName);
 
                            CloudBlockBlob newBlob = container.GetBlockBlobReference(path.Replace(this.blobPath, "") + newAbsoluteFilePath);
                            newBlob.Properties.ContentType = file.ContentType;
                            await newBlob.UploadFromStreamAsync(file.OpenReadStream());
                        }
                    }
                }
                if (existFiles.Count != 0)
                {
                    ErrorDetails error = new ErrorDetails();
                    error.FileExists = existFiles;
                    error.Code = "400";
                    error.Message = "File Already Exists";
                    uploadResponse.Error = error;
                }
            }
            catch (Exception ex) 
            { 
                throw ex; 
            }
            return uploadResponse;
        }
        protected async Task CopyFileToTemp(string path, CloudBlockBlob blockBlob)
        {
            using (FileStream fileStream = File.Create(path))
            {
                await blockBlob.DownloadToStreamAsync(fileStream);
                fileStream.Close();
            }
        }
        // Download file(s) from the storage
        public virtual FileStreamResult Download(string path, string[] names = null, params FileManagerDirectoryContent[] selectedItems)
        {
            return DownloadAsync(this.filesPath + path + "", names, selectedItems).GetAwaiter().GetResult();
        }
        // Download file(s) from the storage
        protected async Task<FileStreamResult> DownloadAsync(string path, string[] names = null, params FileManagerDirectoryContent[] selectedItems)
        {

            foreach (FileManagerDirectoryContent file in selectedItems)
            {
                if (file.IsFile && selectedItems.Count() == 1)
                {
                    FileStreamResult fileStreamResult = new FileStreamResult(new MemoryStream(new WebClient().DownloadData(this.filesPath + selectedItems[0].FilterPath + names[0])), "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = file.Name;
                    return fileStreamResult;
                }
                else
                {
                    ZipArchiveEntry zipEntry = null;
                    ZipArchive archive;
                    string tempPath = Path.Combine(Path.GetTempPath(), "temp.zip");
                    using (archive = ZipFile.Open(tempPath, ZipArchiveMode.Update))
                    {
                        foreach (FileManagerDirectoryContent files in selectedItems)
                        {
                            if (String.IsNullOrEmpty(files.FilterPath))
                            {
                                files.FilterPath = "/";
                                files.Name = "";
                            }
                            string relativeFilePath = this.filesPath + files.FilterPath;
                            relativeFilePath = relativeFilePath.Replace(this.blobPath, "");
                            this.currentFolderName = files.Name;
                            this.initialFolderName = files.Name;
                            if (files.IsFile)
                            {
                                CloudBlockBlob blockBlob = container.GetBlockBlobReference(relativeFilePath + files.Name);
                                if (File.Exists(Path.Combine(Path.GetTempPath(), files.Name)))
                                {
                                    File.Delete(Path.Combine(Path.GetTempPath(), files.Name));
                                }
                                string absoluteFilePath = Path.GetTempPath() + files.Name;
                                await CopyFileToTemp(absoluteFilePath, blockBlob);
                                zipEntry = archive.CreateEntryFromFile(absoluteFilePath, files.Name, CompressionLevel.Fastest);
                                if (File.Exists(Path.Combine(Path.GetTempPath(), files.Name)))
                                {
                                    File.Delete(Path.Combine(Path.GetTempPath(), files.Name));
                                }
                            }
                            else
                            {
                                relativeFilePath = relativeFilePath.Replace(this.blobPath, "");
                                pathValue = relativeFilePath + files.Name;
                                await DownloadFolder(relativeFilePath, files.Name, zipEntry, archive);
                            }
                        }
                    }
                    archive.Dispose();
                    FileStream fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                    FileStreamResult fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = selectedItems.Count() == 1 ? selectedItems[0].Name + ".zip" : "Files.zip";
                    if (File.Exists(Path.Combine(Path.GetTempPath(), "temp.zip")))
                    {
                        File.Delete(Path.Combine(Path.GetTempPath(), "temp.zip"));
                    }
                    return fileStreamResult;
                }
            }
            return null;
        }

        // Download folder(s) from the storage
        private async Task DownloadFolder(string path, string folderName, ZipArchiveEntry zipEntry, ZipArchive archive)
        {
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(pathValue);
            BlobResultSegment items = await AsyncReadCall(pathValue, "Read");
            zipEntry = archive.CreateEntry(this.currentFolderName + "/");
            foreach (IListBlobItem item in items.Results)
            {
                if (item is CloudBlockBlob blockBlob)
                {
                    CloudBlockBlob blob = container.GetBlockBlobReference(new CloudBlockBlob(item.Uri).Name);
                    await blob.FetchAttributesAsync();
                    int index = blob.Name.LastIndexOf("/");
                    string fileName = blob.Name.Substring(index + 1);
                    string absoluteFilePath = Path.GetTempPath() + blob.Name.Split("/").Last();
                    if (File.Exists(absoluteFilePath))
                    {
                        File.Delete(absoluteFilePath);
                    }
                    await CopyFileToTemp(absoluteFilePath, blob);
                    zipEntry = archive.CreateEntryFromFile(absoluteFilePath, this.currentFolderName + "\\" + fileName, CompressionLevel.Fastest);
                    if (File.Exists(absoluteFilePath))
                    {
                        File.Delete(absoluteFilePath);
                    }
                }
                else if (item is CloudBlobDirectory blobDirectory)
                {
                    string absoluteFilePath = item.Uri.ToString().Replace(this.blobPath, ""); // <-- Change your download target path here
                    pathValue = absoluteFilePath;
                    string targetPath = item.Uri.ToString().Replace(this.filesPath + "/", "");
                    string folderPath = new DirectoryInfo(targetPath).Name;
                    this.previousFolderName = this.currentFolderName = (this.initialFolderName == folderName ? folderName : this.previousFolderName) + "/" + folderPath;
                    await DownloadFolder(absoluteFilePath, folderPath, zipEntry, archive);
                }
            }
        }
        // Check whether the directory has child
        private async Task<bool> HasChildDirectory(string path)
        {
            BlobResultSegment items = await AsyncReadCall(path, "HasChild");
            foreach (IListBlobItem item in items.Results)
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
            entry.Name = fileDetails.Name;
            entry.Type = fileDetails.Type;
            entry.IsFile = fileDetails.IsFile;
            entry.Size = fileDetails.Size;
            entry.HasChild = fileDetails.HasChild;
            entry.FilterPath = targetPath;
            return entry;
        }

        // To check if folder exists
        private async Task<bool> IsFolderExists(string path)
        {
            BlobResultSegment items = await AsyncReadCall(path, "Paste");
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

        // Copies file(s) or folders
        public FileManagerResponse Copy(string path, string targetPath, string[] names, string[] renameFiles, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] data)
        {
            return CopyToAsync(path, targetPath, names, renameFiles, data).GetAwaiter().GetResult();
        }

        private async Task<FileManagerResponse> CopyToAsync(string path, string targetPath, string[] names, string[] renamedFiles = null, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse copyResponse = new FileManagerResponse();
            try
            {
                renamedFiles = renamedFiles ?? new string[0];
                foreach (FileManagerDirectoryContent item in data)
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
                                string newName = await FileRename(targetPath, item.Name);
                                await CopyItems(rootPath + item.FilterPath, targetPath, item.Name, newName);
                                copiedFiles.Add(GetFileDetails(targetPath, item));
                            }
                            else
                            {
                                this.existFiles.Add(item.Name);
                            }
                        }
                        else
                        {
                            await CopyItems(rootPath + item.FilterPath, targetPath, item.Name, null);
                            copiedFiles.Add(GetFileDetails(targetPath, item));
                        }
                    }
                    else
                    {
                        if (!await IsFolderExists((rootPath + item.FilterPath + item.Name)))
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
                                item.Path = rootPath + item.FilterPath + item.Name;
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
                            item.Path = rootPath + item.FilterPath + item.Name;
                            CopySubFolder(item, targetPath);
                            copiedFiles.Add(GetFileDetails(targetPath, item));
                        }
                    }

                }
                copyResponse.Files = copiedFiles;
                if (existFiles.Count > 0)
                {
                    ErrorDetails error = new ErrorDetails();
                    error.FileExists = existFiles;
                    error.Code = "400";
                    error.Message = "File Already Exists";
                    copyResponse.Error = error;
                }
                if (missingFiles.Count > 0)
                {
                    string missingFilesList = missingFiles[0];
                    for (int k = 1; k < missingFiles.Count; k++)
                    {
                        missingFilesList = missingFilesList + ", " + missingFiles[k];
                    }
                    throw new FileNotFoundException(missingFilesList + " not found in given location.");
                }

                return copyResponse;
            }
            catch (Exception e)
            {
                ErrorDetails error = new ErrorDetails();
                error.Code = "404";
                error.Message = e.Message.ToString();
                error.FileExists = copyResponse.Error?.FileExists;
                copyResponse.Error = error;
                return copyResponse;
            }
        }

        // To iterate and copy subfolder
        private async void CopySubFolder(FileManagerDirectoryContent subFolder, string targetPath)
        {
            BlobResultSegment items = await AsyncReadCall(subFolder.Path, "Paste");
            targetPath = targetPath + subFolder.Name + "/";
            foreach (IListBlobItem item in items.Results)
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    CloudBlockBlob BlobItem = (CloudBlockBlob)item;
                    string name = BlobItem.Name.Replace(subFolder.Path + "/", "");
                    string sourcePath = BlobItem.Name.Replace(name, "");
                    await CopyItems(sourcePath, targetPath, name, null);
                }
                if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    CloudBlobDirectory BlobItem = (CloudBlobDirectory)item;
                    FileManagerDirectoryContent itemDetail = new FileManagerDirectoryContent();
                    itemDetail.Name = BlobItem.Prefix.Replace(subFolder.Path, "").Replace("/", "");
                    itemDetail.Path = subFolder.Path + "/" + itemDetail.Name;
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
            string nameNotExist = string.Empty;
            nameNotExist = index >= 0 ? fileName.Substring(0, index) : fileName;
            int fileCount = 0;
            while (index > -1 ? await IsFileExists(newPath + nameNotExist + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(fileName) : Path.GetExtension(fileName))) : await IsFolderExists(newPath + nameNotExist + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(fileName) : Path.GetExtension(fileName))))
            {
                fileCount++;
            }
            fileName = nameNotExist + (fileCount > 0 ? "(" + fileCount.ToString() + ")" : "") + Path.GetExtension(fileName);
            return await Task.Run(() =>
            {
                return fileName;
            });
        }

        // Returns the image 
        public FileStreamResult GetImage(string path, string id, bool allowCompress, ImageSize size, params FileManagerDirectoryContent[] data)
        {
            return new FileStreamResult((new MemoryStream(new WebClient().DownloadData(this.filesPath + path))), "APPLICATION/octet-stream");
        }

        private async Task MoveItems(string sourcePath, string targetPath, string name, string newName)
        {
            CloudBlob existBlob = container.GetBlobReference(sourcePath + name);
            await CopyItems(sourcePath, targetPath, name, newName);
            await existBlob.DeleteIfExistsAsync();
        }

        private async void MoveSubFolder(FileManagerDirectoryContent subFolder, string targetPath)
        {
            BlobResultSegment items = await AsyncReadCall(subFolder.Path, "Paste");
            targetPath = targetPath + subFolder.Name + "/";
            foreach (IListBlobItem item in items.Results)
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    CloudBlockBlob BlobItem = (CloudBlockBlob)item;
                    string name = BlobItem.Name.Replace(subFolder.Path + "/", "");
                    string sourcePath = BlobItem.Name.Replace(name, "");
                    await MoveItems(sourcePath, targetPath, name, null);
                }
                if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    CloudBlobDirectory BlobItem = (CloudBlobDirectory)item;
                    FileManagerDirectoryContent itemDetail = new FileManagerDirectoryContent();
                    itemDetail.Name = BlobItem.Prefix.Replace(subFolder.Path, "").Replace("/", "");
                    itemDetail.Path = subFolder.Path + "/" + itemDetail.Name;
                    CopySubFolder(itemDetail, targetPath);
                }
            }
        }

        // Moves file(s) or folders
        public FileManagerResponse Move(string path, string targetPath, string[] names, string[] renameFiles, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] data)
        {
            return MoveToAsync(path, targetPath, names, renameFiles, data).GetAwaiter().GetResult();
        }

        private async Task<FileManagerResponse> MoveToAsync(string path, string targetPath, string[] names, string[] renamedFiles = null, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse moveResponse = new FileManagerResponse();
            try
            {
                renamedFiles = renamedFiles ?? new string[0];
                foreach (FileManagerDirectoryContent item in data)
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
                                string newName = await FileRename(targetPath, item.Name);
                                await MoveItems(rootPath + item.FilterPath, targetPath, item.Name, newName);
                                copiedFiles.Add(GetFileDetails(targetPath, item));
                            }
                            else
                            {
                                this.existFiles.Add(item.Name);
                            }
                        }
                        else
                        {
                            await MoveItems(rootPath + item.FilterPath, targetPath, item.Name, null);
                            copiedFiles.Add(GetFileDetails(targetPath, item));
                        }
                    }
                    else
                    {
                        if (!await IsFolderExists(rootPath + item.FilterPath + item.Name))
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
                                item.Path = rootPath + item.FilterPath + item.Name;
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
                            item.Path = rootPath + item.FilterPath + item.Name;
                            MoveSubFolder(item, targetPath);
                            copiedFiles.Add(GetFileDetails(targetPath, item));
                        }
                    }

                }
                moveResponse.Files = copiedFiles;
                if (existFiles.Count > 0)
                {
                    ErrorDetails error = new ErrorDetails();
                    error.FileExists = existFiles;
                    error.Code = "400";
                    error.Message = "File Already Exists";
                    moveResponse.Error = error;
                }
                if (missingFiles.Count > 0)
                {
                    string nameList = missingFiles[0];
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
                ErrorDetails error = new ErrorDetails();
                error.Code = "404";
                error.Message = e.Message.ToString();
                error.FileExists = moveResponse.Error?.FileExists;
                moveResponse.Error = error;
                return moveResponse;
            }
        }

        // Search for file(s) or folders
        public FileManagerResponse Search(string path, string searchString, bool showHiddenItems, bool caseSensitive, params FileManagerDirectoryContent[] data)
        {
            directoryContentItems.Clear();
            FileManagerResponse searchResponse = GetFiles(path, showHiddenItems, data);
            directoryContentItems.AddRange(searchResponse.Files);
            GetAllFiles(path, showHiddenItems, searchResponse);
            searchResponse.Files = directoryContentItems.Where(item => new Regex(WildcardToRegex(searchString), (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)).IsMatch(item.Name));
            return searchResponse;
        }
        // Gets all files
        protected virtual void GetAllFiles(string path, bool showHiddenItems, FileManagerResponse data)
        {
            FileManagerResponse directoryList = new FileManagerResponse();
            directoryList.Files = (IEnumerable<FileManagerDirectoryContent>)data.Files.Where(item => item.IsFile == false);
            for (int i = 0; i < directoryList.Files.Count(); i++)
            {
                FileManagerResponse innerData = GetFiles(path + directoryList.Files.ElementAt(i).Name + "/", showHiddenItems, (new[] { directoryList.Files.ElementAt(i) }));
                innerData.Files = innerData.Files.Select(file => new FileManagerDirectoryContent
                {
                    Name = file.Name,
                    Type = file.Type,
                    IsFile = file.IsFile,
                    Size = file.Size,
                    HasChild = file.HasChild,
                    FilterPath = (file.FilterPath)
                });
                directoryContentItems.AddRange(innerData.Files);
                GetAllFiles(path + directoryList.Files.ElementAt(i).Name + "/", showHiddenItems, innerData);
            }
        }

        protected virtual string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        }
    }
}
