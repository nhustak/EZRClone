namespace EZRClone.Models;

public class RCloneBackendType
{
    public string TypeName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public static List<RCloneBackendType> GetKnownTypes() =>
    [
        new() { TypeName = "s3", DisplayName = "Amazon S3", Description = "Amazon S3 and compatible services" },
        new() { TypeName = "azureblob", DisplayName = "Azure Blob", Description = "Microsoft Azure Blob Storage" },
        new() { TypeName = "b2", DisplayName = "Backblaze B2", Description = "Backblaze B2 Cloud Storage" },
        new() { TypeName = "box", DisplayName = "Box", Description = "Box cloud storage" },
        new() { TypeName = "drive", DisplayName = "Google Drive", Description = "Google Drive" },
        new() { TypeName = "dropbox", DisplayName = "Dropbox", Description = "Dropbox cloud storage" },
        new() { TypeName = "ftp", DisplayName = "FTP", Description = "FTP file transfer" },
        new() { TypeName = "gcs", DisplayName = "Google Cloud Storage", Description = "Google Cloud Storage" },
        new() { TypeName = "local", DisplayName = "Local", Description = "Local filesystem" },
        new() { TypeName = "mega", DisplayName = "Mega", Description = "Mega cloud storage" },
        new() { TypeName = "onedrive", DisplayName = "OneDrive", Description = "Microsoft OneDrive" },
        new() { TypeName = "sftp", DisplayName = "SFTP", Description = "SSH/SFTP file transfer" },
        new() { TypeName = "swift", DisplayName = "OpenStack Swift", Description = "OpenStack Swift object storage" },
        new() { TypeName = "webdav", DisplayName = "WebDAV", Description = "WebDAV file access" },
        new() { TypeName = "crypt", DisplayName = "Crypt", Description = "Encrypt/decrypt a remote" },
    ];
}
