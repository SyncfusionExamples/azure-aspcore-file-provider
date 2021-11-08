using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Syncfusion.EJ2.FileManager.Base;

namespace Syncfusion.EJ2.FileManager.AzureFileProvider
{
    public class AzureFileProvider : AzureFileProviderBase
    {
        List<FileManagerDirectoryContent> directoryContentItems = new List<FileManagerDirectoryContent>();
        BlobContainerClient container;
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
            container = new BlobServiceClient(new Uri(blobPath.Substring(0, blobPath.Length - blobName.Length - 1)), new StorageSharedKeyCredential(accountName, accountKey), null).GetBlobContainerClient(blobName);
        }

        // Sets blob and file path
        public void SetBlobContainer(string blob_Path, string file_Path)
        {
            blobPath = blob_Path;
            filesPath = file_Path;
            rootPath = filesPath.Replace(blobPath, "");
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
                string[] extensions = ((filter.Replace(" ", "")) ?? "*").Split(",|;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                cwd.Name = selectedItems.Length > 0 ? selectedItems[0].Name : rootPath;
                var sampleDirectory = container.GetBlobClient(path);
                cwd.Type = "File Folder";
                cwd.FilterPath = selectedItems.Length > 0 ? selectedItems[0].FilterPath : "";
                cwd.Size = 0;
                await foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchyAsync(prefix: path, delimiter: "/").AsPages())
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
                            lastUpdated = prevUpdated = DateTime.MinValue;
                            details.Add(entry);
                        }
                    }
                    prefixes = page.Values.Where(item => item.IsPrefix).Select(item => item.Prefix).ToList();
                }
                cwd.HasChild = prefixes?.Count != 0;
                readResponse.CWD = cwd;
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
            await foreach (Azure.Page<BlobItem> page in container.GetBlobsAsync(prefix: path).AsPages())
            {
                DateTime checkFileModified = (page.Values.ToList().OrderByDescending(m => m.Properties.LastModified).ToList().First()).Properties.LastModified.Value.LocalDateTime;
                lastUpdated = prevUpdated = prevUpdated < checkFileModified ? checkFileModified : prevUpdated;
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
                error.Message = "Folder Already Exists";
                createResponse.Error = error;
            }
            return createResponse;
        }

        // Creates a new folder
        protected async Task CreateFolderAsync(string path, string name, IEnumerable<object> selectedItems = null)
        {
            string checkName = name.Contains(" ") ? name.Replace(" ", "%20") : name;
            await foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchyAsync(prefix: path, delimiter: "/").AsPages())
            {
                List<BlobItem> items = page.Values.Where(item => item.IsBlob).Select(item => item.Blob).ToList();
                if (await IsFolderExists(path + name) || (items.Where(x => x.Name.Split("/").Last().Replace("/", "").ToLower() == checkName.ToLower()).Select(i => i).ToArray().Length > 0))
                {
                    this.isFolderAvailable = true;
                }
                else
                {
                    BlobClient blob = container.GetBlobClient(path + name + "/About.txt");
                    await blob.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes("This is a auto generated file")), new BlobHttpHeaders() { ContentType = "text/plain" });
                }
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
                isAlreadyAvailable = isFile ? await IsFileExists(path + newName) : await IsFolderExists(path + newName);
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
                            string name = container.GetBlobClient(item.Name).Uri.AbsolutePath.Replace(container.GetBlobClient(path + oldName).Uri.AbsolutePath + "/", "").Replace("%20", " ");
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
                    await foreach (Azure.Page<BlobItem> items in container.GetBlobsAsync(prefix: path + fileItem.Name + "/").AsPages())
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
            removeResponse.Files = details;
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
                        BlobClient blockBlob = container.GetBlobClient(path.Replace(blobPath, "") + file.FileName);
                        string fileName = file.FileName;
                        string absoluteFilePath = Path.Combine(path, fileName);
                        if (action == "save")
                        {
                            if (!await IsFileExists(absoluteFilePath))
                            {
                                await blockBlob.UploadAsync(file.OpenReadStream());
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
                            await blockBlob.UploadAsync(file.OpenReadStream());
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
                            await newBlob.UploadAsync(file.OpenReadStream());
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
            catch (Exception)
            {
                throw;
            }
            return uploadResponse;
        }

        protected async Task CopyFileToTemp(string path, BlobClient blockBlob)
        {
            using FileStream fileStream = File.Create(path);
            await blockBlob.DownloadToAsync(fileStream);
            fileStream.Close();
        }

        // Download file(s) from the storage
        public virtual FileStreamResult Download(string path, string[] names = null, params FileManagerDirectoryContent[] selectedItems)
        {
            return DownloadAsync(filesPath + path + "", names, selectedItems).GetAwaiter().GetResult();
        }

        // Download file(s) from the storage
        protected async Task<FileStreamResult> DownloadAsync(string path, string[] names = null, params FileManagerDirectoryContent[] selectedItems)
        {
            foreach (FileManagerDirectoryContent file in selectedItems)
            {
                if (file.IsFile && selectedItems.Length == 1)
                {
                    FileStreamResult fileStreamResult = new FileStreamResult(new MemoryStream(new WebClient().DownloadData(filesPath + (names[0].Contains('/') ? '/' + names[0] : selectedItems[0].FilterPath + names[0]))), "APPLICATION/octet-stream");
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
            return null;
        }

        // Download folder(s) from the storage
        private async Task DownloadFolder(string path, string folderName, ZipArchiveEntry zipEntry, ZipArchive archive)
        {
            zipEntry = archive.CreateEntry(currentFolderName + "/");
            await foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchyAsync(prefix: pathValue, delimiter: "/").AsPages())
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
            await foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchyAsync(prefix: path, delimiter: "/").AsPages())
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
            entry.FilterPath = targetPath;
            return entry;
        }

        // To check if folder exists
        private async Task<bool> IsFolderExists(string path)
        {
            List<string> x = new List<string>() { };
            await foreach (Azure.Page<BlobHierarchyItem> page in container.GetBlobsByHierarchyAsync(prefix: path, delimiter: "/").AsPages())
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
            try
            {
                renamedFiles ??= Array.Empty<string>();
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
                error.Code = "404";
                error.Message = e.Message.ToString();
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
                renamedFiles ??= Array.Empty<string>();
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
    }
}