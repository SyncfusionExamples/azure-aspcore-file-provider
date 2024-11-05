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
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Syncfusion.EJ2.FileManager.Base;
using System.Text;
using Azure.Storage.Blobs.Specialized;
using System.Text.Json;

namespace Syncfusion.EJ2.FileManager.AzureFileProvider
{
    public class AzureFileProvider
    {
        List<FileManagerDirectoryContent> directoryContentItems = new List<FileManagerDirectoryContent>();
        BlobContainerClient container;
        AccessDetails AccessDetails = new AccessDetails();
        private string rootName = string.Empty;
        private string accessMessage = string.Empty;
        string pathValue;
        string blobPath;
        string filesPath;
        long size;
        static string rootPath;
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
            container = new BlobServiceClient(new Uri(blobPath.Substring(0, blobPath.Length - blobName.Length - 1)), new StorageSharedKeyCredential(accountName, accountKey), null).GetBlobContainerClient(blobName);            
        }

        // Sets blob and file path
        public void SetBlobContainer(string blob_Path, string file_Path)
        {
            blobPath = blob_Path;
            filesPath = file_Path;
            rootPath = filesPath.Replace(blobPath, "");
        }

        public void SetRules(AccessDetails details)
        {
            this.AccessDetails = details;
            DirectoryInfo root = new DirectoryInfo(rootPath);
            this.rootName = root.Name;
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
            List<string> prefixes = new List<string>() { };
            List<FileManagerDirectoryContent> details = new List<FileManagerDirectoryContent>();
            FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
            try
            {
                var blobPages = container.GetBlobsAsync(prefix: path).AsPages().GetAsyncEnumerator();
                await blobPages.MoveNextAsync();
                bool directoryExists = blobPages.Current.Values.Count() > 0;
                string[] extensions = ((filter.Replace(" ", "")) ?? "*").Split(",|;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                cwd.Name = selectedItems.Length > 0 ? selectedItems[0].Name : path.TrimEnd('/');
                var sampleDirectory = container.GetBlobClient(path);
                cwd.Type = "File Folder";
                cwd.FilterPath = selectedItems.Length > 0 ? selectedItems[0].FilterPath : "";
                cwd.Size = 0;
                cwd.Permission = GetPathPermission(path, false);
                if (directoryExists)
                {
                    foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchy(prefix: path, delimiter: "/").AsPages())
                    {
                        foreach (BlobItem item in page.Values.Where(item => item.IsBlob).Select(item => item.Blob))
                        {
                            bool includeItem = true;
                            if (!(extensions[0].Equals("*.*") || extensions[0].Equals("*")))
                            {
                                if (!(Array.IndexOf(extensions, "*." + (item.Name.ToString().Trim().Split('.'))[item.Name.ToString().Trim().Split('.').Length - 1]) >= 0))
                                    includeItem = false;
                            }
                            if (includeItem)
                            {
                                FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
                                entry.Name = item.Name.Replace(path, "");
                                entry.Type = System.IO.Path.GetExtension(item.Name.Replace(path, ""));
                                entry.IsFile = true;
                                entry.Size = item.Properties.ContentLength.Value;
                                entry.DateModified = item.Properties.LastModified.Value.LocalDateTime;
                                entry.HasChild = false;
                                entry.FilterPath = selectedItems.Length > 0 ? path.Replace(rootPath, "") : "/";
                                entry.Permission = GetPermission(item.Name.Replace(entry.Name, ""), entry.Name, true);
                                details.Add(entry);
                            }
                        }
                        foreach (string item in page.Values.Where(item => item.IsPrefix).Select(item => item.Prefix))
                        {
                            bool includeItem = true;

                            if (includeItem)
                            {
                                FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
                                string directory = item;
                                entry.Name = directory.Replace(path, "").Replace("/", "");
                                entry.Type = "Directory";
                                entry.IsFile = false;
                                entry.Size = 0;
                                entry.HasChild = await HasChildDirectory(directory);
                                entry.FilterPath = selectedItems.Length > 0 ? path.Replace(rootPath, "") : "/";
                                entry.DateModified = await DirectoryLastModified(directory);
                                entry.Permission = GetPathPermission(directory, false);
                                lastUpdated = prevUpdated = DateTime.MinValue;
                                details.Add(entry);
                            }
                        }
                        prefixes = page.Values.Where(item => item.IsPrefix).Select(item => item.Prefix).ToList();
                    }
                }
                cwd.HasChild = prefixes?.Count != 0;
                readResponse.CWD = cwd;
            }
            catch (Exception)
            {
                return readResponse;
            }
            try
            {
                if ((cwd.Permission != null && !cwd.Permission.Read))
                {
                    readResponse.Files = null;
                    accessMessage = cwd.Permission.Message;
                    throw new UnauthorizedAccessException("'" + cwd.Name + "' is not accessible. You need permission to perform the read action.");
                }
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                readResponse.Error = er;
                return readResponse;
            }
            readResponse.Files = details;
            return readResponse;
        }

        // Returns the last modified date for directories
        protected async Task<DateTime> DirectoryLastModified(string path)
        {
            foreach (Azure.Page<BlobItem> page in container.GetBlobs(prefix: path).AsPages())
            {
                BlobItem item = page.Values.ToList().OrderByDescending(m => m.Properties.LastModified).FirstOrDefault();
                if (item != null && item.Properties != null && item.Properties.LastModified != null)
                {
                    DateTime checkFileModified = item.Properties.LastModified.Value.LocalDateTime;
                    lastUpdated = prevUpdated = prevUpdated < checkFileModified ? checkFileModified : prevUpdated;
                }
            }
            return lastUpdated;
        }

        // Converts the byte size value to appropriate value
        protected string ByteConversion(long fileSize)
        {
            try
            {
                string[] index = {"B","KB","MB","GB","TB","PB","EB"};
                // Longs run out around EB
                if (fileSize == 0)
                {
                    return "0 " + index[0];
                }
                int value = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(fileSize), 1024)));
                return (Math.Sign(fileSize) * Math.Round(Math.Abs(fileSize) / Math.Pow(1024, value), 1)).ToString() + " " + index[value];
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Gets the size value of the directory
        protected async Task<long> GetSizeValue(string path)
        {
            foreach (Azure.Page<BlobItem> page in container.GetBlobs(prefix: path + "/").AsPages())
            {
                foreach (BlobItem item in page.Values)
                {
                    BlobClient blob = container.GetBlobClient(item.Name);
                    BlobProperties properties = await blob.GetPropertiesAsync();
                    size += properties.ContentLength;
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
                bool namesAvailable = names.Length > 0;
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
                                BlobClient blob = container.GetBlobClient(rootPath + fileItem.FilterPath + fileItem.Name);
                                BlobProperties properties = await blob.GetPropertiesAsync();
                                isFile = fileItem.IsFile;
                                fileDetails.IsFile = isFile;
                                fileDetails.Name = fileItem.Name;
                                fileDetails.Location = (namesAvailable ? (rootPath + fileItem.FilterPath + fileItem.Name) : path.TrimEnd('/')).Replace("/", @"\");
                                fileDetails.Size = ByteConversion(properties.ContentLength); fileDetails.Modified = properties.LastModified.LocalDateTime; detailsResponse.Details = fileDetails;
                            }
                            else
                            {
                                long sizeValue = GetSizeValue((namesAvailable ? rootPath + fileItem.FilterPath + fileItem.Name : path.TrimEnd('/'))).Result;
                                isFile = false;
                                fileDetails.Name = fileItem.Name;
                                fileDetails.Location = (namesAvailable ? rootPath + fileItem.FilterPath + fileItem.Name : path.Substring(0, path.Length - 1)).Replace("/", @"\");
                                fileDetails.Size = ByteConversion(sizeValue); fileDetails.Modified = await DirectoryLastModified(path); detailsResponse.Details = fileDetails;
                            }
                        }
                        else
                        {
                            multipleSize += (fileItem.IsFile ? fileItem.Size : GetSizeValue(namesAvailable ? rootPath + fileItem.FilterPath + fileItem.Name : path).Result);
                            size = 0;
                            fileDetails.Name = previousName == "" ? previousName = fileItem.Name : previousName + ", " + fileItem.Name;
                            previousPath = previousPath == "" ? rootPath + fileItem.FilterPath : previousPath;
                            if (previousPath == rootPath + fileItem.FilterPath && !isVariousFolders)
                            {
                                previousPath = rootPath + fileItem.FilterPath;
                                fileDetails.Location = ((rootPath + fileItem.FilterPath).Replace("/", @"\")).Substring(0, (rootPath + fileItem.FilterPath).Replace(" / ", @"\").Length - 1);
                            }
                            else
                            {
                                isVariousFolders = true;
                                fileDetails.Location = "Various Folders";
                            }
                            fileDetails.Size = ByteConversion(multipleSize); fileDetails.MultipleFiles = true; detailsResponse.Details = fileDetails;
                        }
                    }
                }
                return await Task.Run(() => {
                    size = 0;
                    return detailsResponse;
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Creates a new folder
        public FileManagerResponse Create(string path, string name, params FileManagerDirectoryContent[] selectedItems)
        {
            this.isFolderAvailable = false;
            FileManagerResponse createResponse = new FileManagerResponse();
            AccessPermission PathPermission = GetPathPermission(path, false);
            try
            {
                if (PathPermission != null && (!PathPermission.Read || !PathPermission.WriteContents))
                {
                    accessMessage = PathPermission.Message;
                    throw new UnauthorizedAccessException("'" + this.getFileNameFromPath(path) + "' is not accessible. You need permission to perform the writeContents action.");
                }
                CreateFolderAsync(path, name, selectedItems).GetAwaiter().GetResult();
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
                    error.Message = "Folder Already Exists";
                    createResponse.Error = error;
                }
                return createResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                createResponse.Error = er;
                return createResponse;
            }
        }

        // Creates a new folder
        protected async Task CreateFolderAsync(string path, string name, IEnumerable<object> selectedItems = null)
        {
            string checkName = name.Contains(" ") ? name.Replace(" ", "%20") : name;
            foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchy(prefix: path, delimiter: "/").AsPages())
            {
                List<BlobItem> items = page.Values.Where(item => item.IsBlob).Select(item => item.Blob).ToList();
                if (await IsFolderExists(path + name) || (items.Where(x => x.Name.Split("/").Last().Replace("/", "").ToLower() == checkName.ToLower()).Select(i => i).ToArray().Length > 0))
                {
                    this.isFolderAvailable = true;
                }
                else
                {
                    BlobClient blob = container.GetBlobClient(path + name + "/About.txt");
                    await blob.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes("This is an auto generated file")), new BlobHttpHeaders() { ContentType = "text/plain" });
                }
            }
        }

        // Renames file(s) or folder(s)
        public FileManagerResponse Rename(string path, string oldName, string newName, bool replace = false, bool showFileExtension = true, params FileManagerDirectoryContent[] data)
        {
            return RenameAsync(path, oldName, newName, showFileExtension, data).GetAwaiter().GetResult();
        }

        // Renames file(s) or folder(s)
        protected async Task<FileManagerResponse> RenameAsync(string path, string oldName, string newName, bool showFileExtension, params FileManagerDirectoryContent[] selectedItems)
        {
            FileManagerResponse renameResponse = new FileManagerResponse();
            List<FileManagerDirectoryContent> details = new List<FileManagerDirectoryContent>();
            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
            try
            {
                AccessPermission permission = GetPermission(GetPath(path), oldName, selectedItems[0].IsFile);
                if (permission != null && (!permission.Read || !permission.Write))
                {
                    accessMessage = permission.Message;
                    throw new UnauthorizedAccessException();
                }
                bool isAlreadyAvailable = false;
                bool isFile = false;
                foreach (FileManagerDirectoryContent fileItem in selectedItems)
                {
                    FileManagerDirectoryContent directoryContent = fileItem;
                    isFile = directoryContent.IsFile;
                    isAlreadyAvailable = await IsFileExists(path + newName);
                    entry.Name = newName;
                    entry.Type = directoryContent.Type;
                    entry.IsFile = isFile;
                    entry.Size = directoryContent.Size;
                    entry.HasChild = directoryContent.HasChild;
                    entry.FilterPath = directoryContent.FilterPath;
                    details.Add(entry);
                    break;
                }
                if (!isAlreadyAvailable)
                {
                    if (isFile)
                    {
                        if (!showFileExtension)
                        {
                            oldName = oldName + selectedItems[0].Type;
                            newName = newName + selectedItems[0].Type;
                        }
                        BlobClient existBlob = container.GetBlobClient(path + oldName);
                        await (container.GetBlobClient(path + newName)).StartCopyFromUriAsync(existBlob.Uri);
                        await existBlob.DeleteAsync();
                    }
                    else
                    {
                        foreach (Azure.Page<BlobItem> page in container.GetBlobs(prefix: path + oldName + "/").AsPages())
                        {
                            foreach (BlobItem item in page.Values)
                            {
                                string name = Uri.UnescapeDataString(container.GetBlobClient(item.Name).Uri.AbsolutePath.Replace(container.GetBlobClient(path + oldName).Uri.AbsolutePath + "/", "").Replace("%20", " "));
                                await (container.GetBlobClient(path + newName + "/" + name)).StartCopyFromUriAsync(container.GetBlobClient(item.Name).Uri);
                                await container.GetBlobClient(path + oldName + "/" + name).DeleteAsync();
                            }
                        }
                    }
                    renameResponse.Files = details;
                }
                else
                {
                    ErrorDetails error = new ErrorDetails();
                    error.FileExists = existFiles;
                    error.Code = "400";
                    error.Message = "File or Folder Already Exists";
                    renameResponse.Error = error;
                }
                return renameResponse;
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Message = (e.GetType().Name == "UnauthorizedAccessException") ? "'" + this.getFileNameFromPath(path + oldName) + "' is not accessible. You need permission to perform the write action." : e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                renameResponse.Error = er;
                return renameResponse;
            }
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
            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
            try
            {
                foreach (FileManagerDirectoryContent item in selectedItems)
                {
                    AccessPermission permission = GetPermission(path, item.Name, item.IsFile);
                    if (permission != null && (!permission.Read || !permission.Write))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + (path + item.Name) + "' is not accessible.  You need permission to perform the write action.");
                    }
                    AccessPermission PathPermission = GetPathPermission(path, item.IsFile);
                    if (PathPermission != null && (!PathPermission.Read || !PathPermission.Write))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + this.getFileNameFromPath(path) + "' is not accessible.  You need permission to perform the write action.");
                    }
                }
                foreach (FileManagerDirectoryContent fileItem in selectedItems)
                {
                    if (fileItem.IsFile)
                    {
                        path = filesPath.Replace(blobPath, "") + fileItem.FilterPath;
                        BlobClient currentFile = container.GetBlobClient(path + fileItem.Name);
                        currentFile.DeleteIfExists();
                        string absoluteFilePath = Path.Combine(Path.GetTempPath(), fileItem.Name);
                        DirectoryInfo tempDirectory = new DirectoryInfo(Path.GetTempPath());
                        foreach (string file in Directory.GetFiles(tempDirectory.ToString()))
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
                        path = filesPath.Replace(blobPath, "") + fileItem.FilterPath;
                        foreach (Azure.Page<BlobItem> items in container.GetBlobs(prefix: path + fileItem.Name + "/").AsPages())
                        {
                            foreach (BlobItem item in items.Values)
                            {
                                BlobClient currentFile = container.GetBlobClient(item.Name);
                                await currentFile.DeleteAsync();
                            }
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
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains(" is not accessible.  You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                removeResponse.Error = er;
                return removeResponse;
            }
            removeResponse.Files = details;
            return removeResponse;
        }

        // Upload file(s) to the storage
        public FileManagerResponse Upload(string path, IList<IFormFile> files, string action, int chunkIndex = 0, int totalChunk = 0, params FileManagerDirectoryContent[] data)
        {
            return UploadAsync(files, action, path, chunkIndex, totalChunk, data).GetAwaiter().GetResult();
        }

        // Upload file(s) to the storage
        protected async Task<FileManagerResponse> UploadAsync(IEnumerable<IFormFile> files, string action, string path, int chunkIndex, int totalChunk, IEnumerable<object> selectedItems = null)
        {
            FileManagerResponse uploadResponse = new FileManagerResponse();
            try
            {
                AccessPermission PathPermission = GetPathPermission(path, false);
                if (PathPermission != null && (!PathPermission.Read || !PathPermission.Upload))
                {
                    accessMessage = PathPermission.Message;
                    throw new UnauthorizedAccessException("'" + this.getFileNameFromPath(path) + "' is not accessible. You need permission to perform the upload action.");
                }
                foreach (IFormFile file in files)
                {
                    if (files != null)
                    {
                        BlockBlobClient blockBlobClient = container.GetBlockBlobClient(path.Replace(blobPath, "") + file.FileName);
                        BlobClient blockBlob = container.GetBlobClient(path.Replace(blobPath, "") + file.FileName);
                        string fileName = file.FileName;
                        string absoluteFilePath = Path.Combine(path, fileName);
                        if (action == "save")
                        {
                            if (!await IsFileExists(absoluteFilePath))
                            {
                                await PerformUpload(file, blockBlobClient, blockBlob, chunkIndex, totalChunk);
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
                            await PerformUpload(file, blockBlobClient, blockBlob, chunkIndex, totalChunk);
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
                            BlobClient newBlob = container.GetBlobClient(path.Replace(blobPath, "") + newAbsoluteFilePath);
                            BlockBlobClient newBlockBlobClient = container.GetBlockBlobClient(path.Replace(blobPath, "") + newAbsoluteFilePath);
                            await PerformUpload(file, newBlockBlobClient, newBlob, chunkIndex, totalChunk);
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
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();

                er.Message = (e.GetType().Name == "UnauthorizedAccessException") ? "'" + this.getFileNameFromPath(path) + "' is not accessible. You need permission to perform the upload action." : e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                uploadResponse.Error = er;
                return uploadResponse;
            }
            return uploadResponse;
        }

        private async Task PerformUpload(IFormFile file, BlockBlobClient blockBlobClient, BlobClient blockBlob, int chunkIndex, int totalChunk)
        {
            if (file.ContentType == "application/octet-stream")
            {
                using (var fileStream = file.OpenReadStream())
                {
                    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(chunkIndex.ToString("d6")));

                    await blockBlobClient.StageBlockAsync(blockId, fileStream);

                    if (chunkIndex == totalChunk - 1)
                    {
                        var blockList = Enumerable.Range(0, totalChunk)
                            .Select(i => Convert.ToBase64String(Encoding.UTF8.GetBytes(i.ToString("d6")))).ToList();

                        await blockBlobClient.CommitBlockListAsync(blockList);
                    }
                }
            }
            else
            {
                await blockBlob.UploadAsync(file.OpenReadStream());
            }
        }

        protected async Task CopyFileToTemp(string path, BlobClient blockBlob)
        {
            using (FileStream fileStream = File.Create(path))
            {
                await blockBlob.DownloadToAsync(fileStream);
                fileStream.Close();
            }
        }

        // Download file(s) from the storage
        public virtual FileStreamResult Download(string path, string[] names = null, params FileManagerDirectoryContent[] selectedItems)
        {
            return DownloadAsync(filesPath + path + "", names, selectedItems).GetAwaiter().GetResult();
        }

        // Download file(s) from the storage
        protected async Task<FileStreamResult> DownloadAsync(string path, string[] names = null, params FileManagerDirectoryContent[] selectedItems)
        {
            try
            {
                foreach (FileManagerDirectoryContent file in selectedItems)
                {
                    AccessPermission FilePermission = GetPermission(path.Replace(this.blobPath, ""), file.Name, file.IsFile);
                    if (FilePermission != null && (!FilePermission.Read || !FilePermission.Download))
                    {
                        throw new UnauthorizedAccessException("'" + this.rootName + path + file.Name + "' is not accessible. Access is denied.");
                    }
                    AccessPermission FolderPermission = GetPathPermission(path.Replace(this.blobPath, ""), file.IsFile);
                    if (FolderPermission != null && (!FolderPermission.Read || !FolderPermission.Download))
                    {
                        throw new UnauthorizedAccessException("'" + this.rootName + path + file.Name + "' is not accessible. Access is denied.");
                    }
                    if (file.IsFile && selectedItems.Length == 1)
                    {
                        string relativeFilePath = filesPath + selectedItems[0].FilterPath;
                        relativeFilePath = relativeFilePath.Replace(blobPath, "");
                        // Initialize BlobClient object with the container, relative file path, and the name of the selected item in Azure Blob Storage.
                        BlobClient blockBlob = container.GetBlobClient(relativeFilePath + selectedItems[0].Name);
                        string absoluteFilePath = Path.GetTempPath() + selectedItems[0].Name;
                        // Copy file from Azure Blob Storage to a temporary location using the CopyFileToTemp method.
                        await CopyFileToTemp(absoluteFilePath, blockBlob);
                        FileStream fileStreamInput = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                        FileStreamResult fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                        fileStreamResult.FileDownloadName = selectedItems[0].Name;
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
                                }
                                string relativeFilePath = filesPath + files.FilterPath;
                                relativeFilePath = relativeFilePath.Replace(blobPath, "");
                                currentFolderName = files.Name;
                                if (files.IsFile)
                                {
                                    BlobClient blockBlob = container.GetBlobClient(relativeFilePath + files.Name);
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
                                    relativeFilePath = relativeFilePath.Replace(blobPath, "");
                                    pathValue = relativeFilePath == files.Name + files.FilterPath ? relativeFilePath : relativeFilePath + files.Name + "/";
                                    previousFolderName = relativeFilePath == files.Name + files.FilterPath ? "" : relativeFilePath;
                                    await DownloadFolder(relativeFilePath, files.Name, zipEntry, archive);
                                }
                            }
                        }
                        archive.Dispose();
                        FileStream fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                        FileStreamResult fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                        fileStreamResult.FileDownloadName = selectedItems.Length == 1 && selectedItems[0].Name != "" ? selectedItems[0].Name + ".zip" : "Files.zip";
                        if (File.Exists(Path.Combine(Path.GetTempPath(), "temp.zip")))
                        {
                            File.Delete(Path.Combine(Path.GetTempPath(), "temp.zip"));
                        }
                        return fileStreamResult;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }

        // Download folder(s) from the storage
        private async Task DownloadFolder(string path, string folderName, ZipArchiveEntry zipEntry, ZipArchive archive)
        {
            zipEntry = archive.CreateEntry(currentFolderName + "/");
            foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchy(prefix: pathValue, delimiter: "/").AsPages())
            {
                foreach (BlobItem item in page.Values.Where(item => item.IsBlob).Select(item => item.Blob))
                {
                    BlobClient blob = container.GetBlobClient(item.Name);
                    int index = blob.Name.LastIndexOf("/");
                    string fileName = blob.Name.Substring(index + 1);
                    string absoluteFilePath = Path.GetTempPath() + blob.Name.Split("/").Last();
                    if (File.Exists(absoluteFilePath))
                    {
                        File.Delete(absoluteFilePath);
                    }
                    await CopyFileToTemp(absoluteFilePath, blob);
                    zipEntry = archive.CreateEntryFromFile(absoluteFilePath, currentFolderName + "\\" + fileName, CompressionLevel.Fastest);
                    if (File.Exists(absoluteFilePath))
                    {
                        File.Delete(absoluteFilePath);
                    }
                }
                foreach (string item in page.Values.Where(item => item.IsPrefix).Select(item => item.Prefix))
                {
                    string absoluteFilePath = item.Replace(blobPath, ""); // <-- Change your download target path here
                    pathValue = absoluteFilePath;
                    string targetPath = item.Replace(filesPath + "/", "");
                    string folderPath = new DirectoryInfo(targetPath).Name;
                    currentFolderName = previousFolderName.Length > 1 ? item.Replace(previousFolderName, "").Trim('/') : item.Trim('/');
                    await DownloadFolder(absoluteFilePath, folderPath, zipEntry, archive);
                }
            }
        }

        // Check whether the directory has child
        private async Task<bool> HasChildDirectory(string path)
        {
            List<string> prefixes = new List<string>() { };
            foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchy(prefix: path, delimiter: "/").AsPages())
            {
                prefixes = page.Values.Where(item => item.IsPrefix).Select(item => item.Prefix).ToList();
            }
            return prefixes.Count != 0;
        }

        // To get the file details
        private static FileManagerDirectoryContent GetFileDetails(string targetPath, FileManagerDirectoryContent fileDetails)
        {
            FileManagerDirectoryContent entry = new FileManagerDirectoryContent();
            entry.Name = fileDetails.Name;
            entry.Type = fileDetails.Type;
            entry.IsFile = fileDetails.IsFile;
            entry.Size = fileDetails.Size;
            entry.HasChild = fileDetails.HasChild;
            entry.FilterPath = targetPath.Replace(rootPath, "");
            return entry;
        }

        // To check if folder exists
        private async Task<bool> IsFolderExists(string path)
        {
            List<string> x = new List<string>() { };
            foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchy(prefix: path, delimiter: "/").AsPages())
            {
                x = page.Values.Where(item => item.IsPrefix).Select(item => item.Prefix).ToList();
            }
            return x.Count > 0;
        }

        // To check if file exists
        private async Task<bool> IsFileExists(string path)
        {
            BlobClient newBlob = container.GetBlobClient(path);
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
            HashSet<string> processedItems = new HashSet<string>();
            try
            {
                renamedFiles = renamedFiles ?? Array.Empty<string>();
                foreach (FileManagerDirectoryContent item in data)
                {
                    if (processedItems.Contains(item.Name))
                    {
                        continue;
                    }
                    processedItems.Add(item.Name);
                    AccessPermission permission = GetPathPermission(path, item.IsFile);
                    if (permission != null && (!permission.Read || !permission.Copy))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + this.getFileNameFromPath(path) + "' is not accessible. You need permission to perform the copy action.");
                    }
                    AccessPermission pathPermission = GetPermission(path, item.Name, item.IsFile);
                    if (pathPermission != null && (!pathPermission.Read || !pathPermission.Copy))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + path + item.Name + "' is not accessible. You need permission to perform the copy action.");
                    }
                    if (item.IsFile)
                    {
                        if (await IsFileExists(targetPath + item.Name))
                        {
                            int index = -1;
                            if (renamedFiles.Length > 0)
                            {
                                index = Array.FindIndex(renamedFiles, Items => Items.Contains(item.Name));
                            }
                            if ((index != -1))
                            {
                                string newName = await FileRename(targetPath, item.Name);
                                CopyItems(rootPath + item.FilterPath, targetPath, item.Name, newName);
                                copiedFiles.Add(GetFileDetails(targetPath, item));
                            }
                            else
                            {
                                this.existFiles.Add(item.Name);
                            }
                        }
                        else
                        {
                            CopyItems(rootPath + item.FilterPath, targetPath, item.Name, null);
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
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "404";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                error.FileExists = copyResponse.Error?.FileExists;
                copyResponse.Error = error;
                return copyResponse;
            }
        }

        // To iterate and copy subfolder
        private void CopySubFolder(FileManagerDirectoryContent subFolder, string targetPath)
        {
            targetPath = targetPath + subFolder.Name + "/";
            foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchy(prefix: subFolder.Path + "/", delimiter: "/").AsPages())
            {
                foreach (BlobItem item in page.Values.Where(item => item.IsBlob).Select(item => item.Blob))
                {
                    string name = item.Name.Replace(subFolder.Path + "/", "");
                    string sourcePath = item.Name.Replace(name, "");
                    CopyItems(sourcePath, targetPath, name, null);
                }
                foreach (string item in page.Values.Where(item => item.IsPrefix).Select(item => item.Prefix))
                {
                    FileManagerDirectoryContent itemDetail = new FileManagerDirectoryContent();
                    itemDetail.Name = item.Replace(subFolder.Path, "").Replace("/", "");
                    itemDetail.Path = subFolder.Path + "/" + itemDetail.Name;
                    CopySubFolder(itemDetail, targetPath);
                }
            }
        }

        // To iterate and copy files
        private void CopyItems(string sourcePath, string targetPath, string name, string newName)
        {
            if (newName == null)
            {
                newName = name;
            }
            BlobClient existBlob = container.GetBlobClient(sourcePath + name);
            BlobClient newBlob = container.GetBlobClient(targetPath + newName);
            newBlob.StartCopyFromUri(existBlob.Uri);
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
            return await Task.Run(() => {
                return fileName;
            });
        }

        // Returns the image 
        public FileStreamResult GetImage(string path, string id, bool allowCompress, ImageSize size, params FileManagerDirectoryContent[] data)
        {
            AccessPermission PathPermission = GetFilePermission("Files" + path);
            if (PathPermission != null && !PathPermission.Read)
            {
                return null;
            }
            return new FileStreamResult((new MemoryStream(new WebClient().DownloadData(filesPath + path))), "APPLICATION/octet-stream");
        }

        private async Task MoveItems(string sourcePath, string targetPath, string name, string newName)
        {
            BlobClient existBlob = container.GetBlobClient(sourcePath + name);
            CopyItems(sourcePath, targetPath, name, newName);
            await existBlob.DeleteAsync();
        }

        private async void MoveSubFolder(FileManagerDirectoryContent subFolder, string targetPath)
        {
            targetPath = targetPath + subFolder.Name + "/";
            foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchy(prefix: subFolder.Path + "/", delimiter: "/").AsPages())
            {
                foreach (BlobItem item in page.Values.Where(item => item.IsBlob).Select(item => item.Blob))
                {
                    string name = item.Name.Replace(subFolder.Path + "/", "");
                    string sourcePath = item.Name.Replace(name, "");
                    await MoveItems(sourcePath, targetPath, name, null);
                }
                foreach (string item in page.Values.Where(item => item.IsPrefix).Select(item => item.Prefix))
                {
                    FileManagerDirectoryContent itemDetail = new FileManagerDirectoryContent();
                    itemDetail.Name = item.Replace(subFolder.Path, "").Replace("/", "");
                    itemDetail.Path = subFolder.Path + "/" + itemDetail.Name;
                    MoveSubFolder(itemDetail, targetPath);
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
                renamedFiles = renamedFiles ?? Array.Empty<string>();
                foreach (FileManagerDirectoryContent item in data)
                {
                    AccessPermission permission = GetPermission(path, item.Name, item.IsFile);
                    if (permission != null && (!permission.Read || !permission.Write))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + (path + item.Name) + "' is not accessible. You need permission to perform the write action.");
                    }
                    AccessPermission PathPermission = GetPathPermission(path, item.IsFile);
                    if (PathPermission != null && (!PathPermission.Read || !PathPermission.WriteContents))
                    {
                        accessMessage = PathPermission.Message;
                        throw new UnauthorizedAccessException("'" + this.getFileNameFromPath(path) + "' is not accessible. You need permission to perform the write action.");
                    }
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
                error.Message = e.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "404";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                error.FileExists = moveResponse.Error?.FileExists;
                moveResponse.Error = error;
                return moveResponse;
            }
        }

        // Search for file(s) or folders
        public FileManagerResponse Search(string path, string searchString, bool showHiddenItems, bool caseSensitive, params FileManagerDirectoryContent[] data)
        {
            directoryContentItems.Clear();
            FileManagerResponse searchResponse = GetFiles(path, true, data);
            directoryContentItems.AddRange(searchResponse.Files);
            GetAllFiles(path, searchResponse);
            searchResponse.Files = directoryContentItems.Where(item => new Regex(WildcardToRegex(searchString), (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)).IsMatch(item.Name));
            return searchResponse;
        }

        // Gets all files
        protected virtual void GetAllFiles(string path, FileManagerResponse data)
        {
            FileManagerResponse directoryList = new FileManagerResponse();
            directoryList.Files = data.Files.Where(item => item.IsFile == false);
            for (int i = 0; i < directoryList.Files.Count(); i++)
            {
                FileManagerResponse innerData = GetFiles(path + directoryList.Files.ElementAt(i).Name + "/", true, (new[] { directoryList.Files.ElementAt(i) }));
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
                GetAllFiles(path + directoryList.Files.ElementAt(i).Name + "/", innerData);
            }
        }

        protected virtual string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        }

        protected virtual string[] GetFolderDetails(string path)
        {
            string[] str_array = path.Split('/'), fileDetails = new string[2];
            string parentPath = "";
            for (int i = 0; i < str_array.Length - 2; i++)
            {
                parentPath += str_array[i] + "/";
            }
            fileDetails[0] = parentPath;
            fileDetails[1] = str_array[str_array.Length - 2];
            return fileDetails;
        }
        protected virtual string GetPath(string path)
        {
            String fullPath = (this.blobPath + path);
            return fullPath;
        }
        protected virtual bool HasPermission(Permission rule)
        {
            return rule == Permission.Allow ? true : false;
        }
        protected virtual AccessPermission UpdateFileRules(AccessPermission filePermission, AccessRule fileRule)
        {
            filePermission.Copy = HasPermission(fileRule.Copy);
            filePermission.Download = HasPermission(fileRule.Download);
            filePermission.Write = HasPermission(fileRule.Write);
            filePermission.Read = HasPermission(fileRule.Read);
            filePermission.Message = string.IsNullOrEmpty(fileRule.Message) ? string.Empty : fileRule.Message;
            return filePermission;
        }

        protected virtual AccessPermission GetPermission(string location, string name, bool isFile)
        {
            AccessPermission FilePermission = new AccessPermission();
            if (isFile)
            {
                if (this.AccessDetails.AccessRules == null) return null;
                string nameExtension = Path.GetExtension(name).ToLower();
                string fileName = Path.GetFileNameWithoutExtension(name);
                string currentPath = GetPath(location);
                //string currentPath = (location + name+"/");
                foreach (AccessRule fileRule in AccessDetails.AccessRules)
                {
                    if (!string.IsNullOrEmpty(fileRule.Path) && fileRule.IsFile && (fileRule.Role == null || fileRule.Role == AccessDetails.Role))
                    {
                        if (fileRule.Path.IndexOf("*.*") > -1)
                        {
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*.*"));
                            if (currentPath.IndexOf(GetPath(parentPath)) == 0 || parentPath == "")
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf("*.") > -1)
                        {
                            string pathExtension = Path.GetExtension(fileRule.Path).ToLower();
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*."));
                            if ((GetPath(parentPath) == currentPath || parentPath == "") && nameExtension == pathExtension)
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf(".*") > -1)
                        {
                            string pathName = Path.GetFileNameWithoutExtension(fileRule.Path);
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf(pathName + ".*"));
                            if ((GetPath(parentPath) == currentPath || parentPath == "") && fileName == pathName)
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (GetPath(fileRule.Path) == GetPath(location + name) || (GetPath(fileRule.Path) == GetPath(location + fileName)))
                        {
                            FilePermission = UpdateFileRules(FilePermission, fileRule);
                        }
                    }
                }
                return FilePermission;
            }
            else
            {
                if (this.AccessDetails.AccessRules == null) { return null; }
                foreach (AccessRule folderRule in AccessDetails.AccessRules)
                {
                    if (folderRule.Path != null && folderRule.IsFile == false && (folderRule.Role == null || folderRule.Role == AccessDetails.Role))
                    {
                        if (folderRule.Path.IndexOf("*") > -1)
                        {
                            string parentPath = folderRule.Path.Substring(0, folderRule.Path.IndexOf("*"));
                            if ((location + name).IndexOf(GetPath(parentPath)) == 0 || parentPath == "")
                            {
                                FilePermission = UpdateFolderRules(FilePermission, folderRule);
                            }
                        }
                        else if (GetPath(folderRule.Path) == (location + name) || GetPath(folderRule.Path) == (location + name + Path.DirectorySeparatorChar) || GetPath(folderRule.Path) == (location + name + "/"))
                        {
                            FilePermission = UpdateFolderRules(FilePermission, folderRule);
                        }
                        else if ((location + name).IndexOf(GetPath(folderRule.Path)) == 0)
                        {
                            FilePermission = UpdateFolderRules(FilePermission, folderRule);
                        }
                    }
                }
                return FilePermission;
            }
        }
        protected virtual AccessPermission UpdateFolderRules(AccessPermission folderPermission, AccessRule folderRule)
        {
            folderPermission.Copy = HasPermission(folderRule.Copy);
            folderPermission.Download = HasPermission(folderRule.Download);
            folderPermission.Write = HasPermission(folderRule.Write);
            folderPermission.WriteContents = HasPermission(folderRule.WriteContents);
            folderPermission.Read = HasPermission(folderRule.Read);
            folderPermission.Upload = HasPermission(folderRule.Upload);
            folderPermission.Message = string.IsNullOrEmpty(folderRule.Message) ? string.Empty : folderRule.Message;
            return folderPermission;
        }

        protected virtual AccessPermission GetPathPermission(string path, bool isFile)
        {
            string[] fileDetails = GetFolderDetails(path);
            if (isFile)
            {
                return GetPermission(GetPath(fileDetails[0]), fileDetails[1], true);
            }
            return GetPermission(GetPath(fileDetails[0]), fileDetails[1], false);
        }
        private string getFileNameFromPath(string path)
        {
            int index = path.LastIndexOf("/");
            return path.Remove(index);
        }
        protected virtual AccessPermission GetFilePermission(string path)
        {
            string parentPath = path.Substring(0, path.LastIndexOf("/") + 1);
            string fileName = Path.GetFileName(path);
            return GetPermission(parentPath, fileName, true);
        }

        public string ToCamelCase(object userData)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            return JsonSerializer.Serialize(userData, options);
        }
    }
}
