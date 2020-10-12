using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using FluentFTP;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace SFTPTransfer
{
    public class Processor
    {
        private IConfigurationRoot _config;
        private string _env;
        private string[] _uploadFiles;
        private IEnumerable<SftpFile> _downloadFiles;

        public Processor(IConfigurationRoot config, string env)
        {
            _config = config;
            _env = env;
        }
        public int ConnectSFTPSite(string[] args)
        {
            try
            {
                string host = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("Host").Value;
                var port = Int32.Parse(_config.GetSection("FTPCredentials").GetSection(_env).GetSection("Port").Value);
                string username = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("UserName").Value;
                string password = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("Password").Value;
                string remoteDirectoryUpload = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("RemoteDirectoryUpload").Value;
                string remoteDirectoryDownload = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("RemoteDirectoryDownload").Value;
                string localDirectoryUpload = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("LocalDirectoryUpload").Value;
                string localDirectoryDownload = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("LocalDirectoryDownload").Value;
                string fileName1 = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("FileName1").Value.ToUpper();
                string fileName2 = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("FileName2").Value.ToUpper();

                using (var sftp = new SftpClient(host, port, username, password))
                {
                    sftp.Connect();
                    
                    // upload file(s) to remote SFTP site
                    if (_env == "Waystar")  // Waystar upload/download only
                    {
                        if (args.Length == 0)
                        {
                            return 0;
                        }
                        string action = args[0];
                        if (action.ToUpper() == "UPLOAD" || action.ToUpper() == "UPLOADI")
                        {
                            sftp.ChangeDirectory(remoteDirectoryUpload);
                            if (action.ToUpper() == "UPLOADI")
                            {
                                localDirectoryUpload = _config.GetSection("FTPCredentials").GetSection(_env).GetSection($"LocalDirectoryUpload2").Value;
                            }
                            _uploadFiles = Directory.GetFiles(localDirectoryUpload);
                            
                            if (_uploadFiles.Any())
                            {
                                foreach (var file in _uploadFiles)
                                {
                                    var fileName = Path.GetFileName(file);
                                    if (fileName.ToUpper().Equals(fileName1) || fileName.ToUpper().Equals(fileName2))
                                    {
                                        using (FileStream fileStream = new FileStream(file, FileMode.Open))
                                        {
                                            sftp.BufferSize = 4 * 1024;
                                            sftp.UploadFile(fileStream, fileName);
                                        }
                                    }
                                }
                            }
                        }
                        else if (action.ToUpper() == "DOWNLOAD" || action.ToUpper() == "DOWNLOADI")
                        {
                            // download any files in the folder
                            _downloadFiles = sftp.ListDirectory(remoteDirectoryDownload);

                            if (_downloadFiles.Any())
                            {
                                foreach (var file in _downloadFiles)
                                {
                                    string remoteFileName = file.Name;
                                    if (remoteFileName == "Archive" || remoteFileName == "Download")
                                    {
                                        continue;
                                    }
                                    string localFileName = localDirectoryDownload + remoteFileName;

                                    using (Stream stream = File.OpenWrite(localFileName))
                                    {
                                        sftp.DownloadFile(remoteDirectoryDownload + remoteFileName, stream);
                                        stream.Flush();
                                        stream.Close();
                                        // remove file from SFTP site
                                        sftp.Delete(remoteDirectoryDownload + file.Name);
                                    }
                                }
                            }
                        }
                    }
                    else if (_env == "HHAExchange")          // upload/download/archive/email responseFile
                    {
                        string localDirectoryUploadArchive = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("LocalDirectoryUploadArchive").Value;
                        string localDirectoryDownloadArchive = _config.GetSection("FTPCredentials").GetSection(_env).GetSection("LocalDirectoryDownloadArchive").Value;
                        // upload files
                        _uploadFiles = Directory.GetFiles(localDirectoryUpload);

                        if (_uploadFiles.Any())
                        {
                            Console.WriteLine($"found {_uploadFiles.Count()} file(s) to upload");
                            sftp.ChangeDirectory(remoteDirectoryUpload);
                            foreach (var file in _uploadFiles)
                            {
                                var fileName = Path.GetFileName(file);
                                if ((fileName.ToUpper().StartsWith(fileName1) || fileName.ToUpper().StartsWith(fileName2)) && fileName.ToUpper().EndsWith(".CSV"))
                                {
                                    using (FileStream fs = new FileStream(file, FileMode.Open))
                                    {
                                        sftp.BufferSize = 4 * 1024;
                                        sftp.UploadFile(fs, fileName);
                                    }

                                    // archive upload files
                                    string archiveFileName = Path.Combine(localDirectoryUploadArchive, fileName.Substring(0, fileName.Length - 4))
                                                             + DateTime.Now.ToString("_yyyyMMdd_HHmmss") + @".csv";
                                    File.Move(file, archiveFileName);
                                }
                            }
                        }

                        // download file(s) from remote SFTP site
                        _downloadFiles = sftp.ListDirectory(remoteDirectoryDownload)
                            .Where(file => (file.Name.ToUpper().StartsWith(fileName1) || file.Name.ToUpper().StartsWith(fileName2)) && file.Name.ToUpper().EndsWith(".CSV"));
                        if (_downloadFiles.Any())
                        {
                            Console.WriteLine($"found {_downloadFiles.Count()} file(s) to download");
                            foreach (var file in _downloadFiles)
                            {
                                string remoteFileName = file.Name;
                                string localFileName = localDirectoryDownload + remoteFileName;

                                using (Stream stream = File.OpenWrite(localFileName))
                                {
                                    sftp.DownloadFile(remoteDirectoryDownload + remoteFileName, stream);
                                    stream.Flush();
                                    stream.Close();

                                    //archive download file and email
                                    string archiveFileName = Path.Combine(localDirectoryDownloadArchive, remoteFileName.Substring(0, remoteFileName.Length - 4))
                                                            + DateTime.Now.ToString("_yyyyMMdd_HHmmss") + @".csv";

                                    Email.SendEmail(_config,
                                        "HHA Exhange Response File",
                                        "Please see attached response file from HHA Exchange.",
                                        localFileName);
                                    Console.WriteLine("Email sent");
                                    File.Move(localFileName, archiveFileName);

                                    // remove file from SFTP site
                                    sftp.Delete(remoteDirectoryDownload + file.Name);
                                }
                            }
                        }   // end of download files
                        sftp.Disconnect();
                    }
                   
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return -1;
            }

        }

    }
}