using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MyCamera
{
    public class ImportImages
    {

        public int FilesImported = 0;
        public int FilesSaved = 0;
        private bool lastFileFound = false;

        public void ImportFromCamera()
        {
            using (var client = new WebClient())
            {
                string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(Properties.Settings.Default.CameraUserName + ":" + Properties.Settings.Default.CameraPassword));
                client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;
                
                ProcessHtml(client, Properties.Settings.Default.CameraURL + "/sd/");
            }
        }

        private void ProcessHtml(WebClient client, string dir)
        {
            string html = client.DownloadString(dir);

            int trend = 0;
            int trstart = html.IndexOf(@"<tr>", trend);
            while (trstart >= 0)
            {
                trstart += 4;
                trend = html.IndexOf(@"</tr>", trstart);
                if (trend >= 0)
                {
                    string rw = html.Substring(trstart, trend - trstart);
                    if (rw.Contains("[DIRECTORY]"))
                    {
                        int dirStart = rw.IndexOf(@"""");
                        if (dirStart >= 0)
                        {
                            dirStart++;
                            int dirEnd = rw.IndexOf(@"""", dirStart);
                            if (dirEnd >= 0)
                                ProcessHtml(client, Properties.Settings.Default.CameraURL + rw.Substring(dirStart, dirEnd - dirStart));
                        }
                    }
                    else if (rw.Contains(@".264"))
                    {
                        int fnStart = rw.IndexOf(@"""");
                        if (fnStart >= 0)
                        {
                            fnStart += 1;
                            int fnEnd = rw.IndexOf(@"""", fnStart);
                            if (fnEnd >= 0)
                            {
                                string fileName = rw.Substring(fnStart, fnEnd - fnStart);
                                //int dtStart = rw.IndexOf(@"<td>", fnEnd) + 4;
                                //int dtEnd = rw.IndexOf(@"</td>", dtStart);
                                //string dt = rw.Substring(dtStart, dtEnd - dtStart);
                                //dt = dt.Replace(@"&nbsp;", "");
                                //DateTime fdt = DateTime.Parse(dt);
                                string localFileName = fileName.Substring(fileName.LastIndexOf("/") + 1).Replace(@"/", @"\");
                                // Filename format: Axxxxxx_yyyyyy_zzzzzz.264
                                //   xxxxxx = Date (YYMMDD)
                                //   yyyyyy = Start Time (HHMMSS)
                                //   zzzzzz = End Time (HHMMSS)
                                if (localFileName.StartsWith("A"))
                                {
                                    // Strip off "A" and End Time
                                    localFileName = localFileName.Substring(1, 13) + ".264";
                                    if (String.IsNullOrEmpty(LastFileDownloaded))
                                        lastFileFound = true;

                                    if (lastFileFound)
                                    {
                                        string destinationFile = Path.Combine(Properties.Settings.Default.LocalRawImagesFolder, localFileName);
                                        if (!File.Exists(destinationFile))
                                        {
                                            Console.WriteLine(String.Format("Downloading {0}...", fileName));
                                            client.DownloadFile(Properties.Settings.Default.CameraURL + "/" + fileName, destinationFile);
                                            this.FilesImported++;
                                            LastFileDownloaded = localFileName;
                                        }
                                    }
                                    else
                                    {
                                        if (LastFileDownloaded == localFileName)
                                            lastFileFound = true;
                                    }
                                }
                            }
                        }
                    }
                }
                trstart = html.IndexOf(@"<tr>", trend);
            }
        }

        private string _LastFileDownloaded;
        private string LastFileDownloaded
        {
            get
            {
                if (String.IsNullOrEmpty(_LastFileDownloaded))
                {
                    if (File.Exists(GetLastFileDownloadedFileName()))
                        _LastFileDownloaded = File.ReadAllText(GetLastFileDownloadedFileName());
                    else
                        _LastFileDownloaded = "";
                }
                return _LastFileDownloaded; ;
            }
            set
            {
                File.WriteAllText(GetLastFileDownloadedFileName(), value);
                _LastFileDownloaded = value;
            }
        }

        private string GetLastFileDownloadedFileName()
        {
            string fileName =  Path.Combine(GetDropboxFolder(), Properties.Settings.Default.LastFileDownLoadedDropboxFile);
            return fileName;
        }


        public void SaveConvertedImages()
        {
            UpdateAllFileDateTimes(Properties.Settings.Default.LocalRawImagesFolder);
            string aviDestFile;
            string rawFullFileName;
            foreach(string aviSrcFullFileName in Directory.GetFiles(Properties.Settings.Default.LocalRawImagesFolder, "*.avi"))
            {
                aviDestFile = aviSrcFullFileName.Substring(aviSrcFullFileName.LastIndexOf(@"\") + 1);
                Console.WriteLine(String.Format("Moving {0}...", aviSrcFullFileName));
                File.Move(aviSrcFullFileName, Path.Combine(Properties.Settings.Default.LocalFinalImagesFolder, aviDestFile));
                FilesSaved++;
                rawFullFileName = aviSrcFullFileName.Substring(0, aviSrcFullFileName.Length - 4) + ".264";
                if (File.Exists(rawFullFileName))
                    File.Delete(rawFullFileName);
            }
        }

        private string GetDropboxFolder()
        {
            var infoPath = @"Dropbox\info.json";
            var jsonPath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), infoPath);
            if (!File.Exists(jsonPath))
                jsonPath = Path.Combine(Environment.GetEnvironmentVariable("AppData"), infoPath);
            if (!File.Exists(jsonPath))
                throw new Exception("Dropbox Info.json could not be found!");
            //string test = File.ReadAllText(jsonPath);
            //string[] t2 = test.Split('\"');
            //return File.ReadAllText(jsonPath).Split('\"')[9].Replace(@"\\", @"\");
            string folder = "";
            foreach (string test in File.ReadAllText(jsonPath).Split('\"'))
            {
                if (test.ToLower().StartsWith("c:"))
                {
                    folder = test;
                    break;
                }
            }
            if(String.IsNullOrEmpty(folder))
                throw new Exception("Dropbox folder could not be found!");
            return folder;
        }

        private void UpdateAllFileDateTimes(string dir)
        {
            string fileTouchExe = Path.Combine(Properties.Settings.Default.LocalRawImagesFolder, @"FileTouch.exe");
            if (!File.Exists(fileTouchExe))
                throw new Exception(@"FileTouch.exe could not be found!");

            CultureInfo enUS = new CultureInfo("en-US");
            string arg;
            string fileName;
            string dt;
            string tm;
            DateTime testDate;
            foreach (string aviSrcFullFileName in Directory.GetFiles(dir, "*.avi"))
            {
                Console.WriteLine(String.Format("Touching {0}...", aviSrcFullFileName));
                fileName = aviSrcFullFileName.Substring(aviSrcFullFileName.LastIndexOf(@"\") + 1);
                if (fileName.Length == 17)
                {
                    // "180610_213518"
                    // mm-dd-yyyy
                    dt = string.Format("{0}-{1}-20{2}", fileName.Substring(2,2), fileName.Substring(4, 2), fileName.Substring(0, 2));
                    // hh:mm:ss
                    tm = string.Format("{0}:{1}:{2}", fileName.Substring(7, 2), fileName.Substring(9, 2), fileName.Substring(11, 2));
                    if (DateTime.TryParseExact(dt + " " + tm, @"MM-dd-yyyy HH:mm:ss", enUS, DateTimeStyles.None, out testDate))
                    {
                        if (TimeZoneInfo.Local.IsDaylightSavingTime(testDate))
                        {
                            testDate = testDate.AddHours(-1);
                            dt = string.Format("{0:MM-dd-yyyy}", testDate);
                            tm = string.Format("{0:HH:mm:ss}", testDate);
                        }
                        arg = String.Format("/W /C /D {0} /T {1} \"{2}\"", dt, tm, aviSrcFullFileName);
                        Process ft = new Process();
                        ft.StartInfo.UseShellExecute = false;
                        ft.StartInfo.FileName = fileTouchExe;
                        ft.StartInfo.Arguments = arg;
                        ft.StartInfo.CreateNoWindow = true;
                        ft.Start();
                    }
                    
                }
            }
        }
    }
}
