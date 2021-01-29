using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LSSDEdlioFileArchiver.Downloader
{
    class Program
    {
        static string _downloadDirectory = "download";
        static string _outputFile_FileNames = "scraper-filenames.txt";

        static async Task<bool> downloadFile(string url)
        {
            if (!Directory.Exists(_downloadDirectory))
            {
                Directory.CreateDirectory(_downloadDirectory);
            }
            
            // Parse the domain name, and extract the rest of the path            
            Uri uri = new Uri(url);
            string filename = Path.GetFileName(url);            
            string filepath = uri.PathAndQuery.Remove(uri.PathAndQuery.Length - filename.Length, filename.Length);
            string downloaded_path = $"{_downloadDirectory}{filepath}";
            string downloaded_path_with_filename = Path.Combine(downloaded_path, filename);

            Console.WriteLine("Download directory: " + _downloadDirectory);
            Console.WriteLine("Downloaded path: " + downloaded_path);
            Console.WriteLine("Downloaded path with filename: " + downloaded_path_with_filename);
            
            if (!Directory.Exists(downloaded_path))
            {
                Directory.CreateDirectory(downloaded_path);
            }

            // Download the file
            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage response = await httpClient.GetAsync(url);
                if (File.Exists(downloaded_path_with_filename)) 
                {
                    File.Delete(downloaded_path_with_filename);
                }

                using (FileStream newFile = File.Create(downloaded_path_with_filename)) 
                {
                    await response.Content.CopyToAsync(newFile);
                }

                IEnumerable<string> headerContentDispositions = new List<string>();
                if (response.Content.Headers.TryGetValues("content-disposition", out headerContentDispositions))
                {
                    // TODO: Save the filenames in a file here                    

                    foreach(string cd in headerContentDispositions) 
                    {
                        Console.WriteLine(" Found filename: " + cd);
                    }
                } 
            }

            // In the content-disposition header, we can get the original filename
            // example: content-disposition:	inline; filename*=UTF-8''I%20Can%20ELA-Assessment%20language.docx

            return true;  
        }

        static async Task Main(string[] args)
        {
            List<string> _discoveredFiles = new List<string>();
            
            /** ******************* DEBUG ***************** */
            _discoveredFiles.Add("https://22.files.edl.io/48ee/08/15/19/175547-71343254-78f6-45da-9021-4cb8f49d0e9e.pdf");
            _discoveredFiles.Add("https://22.files.edl.io/aa3d/08/15/19/175547-aa1a2464-41b5-4c79-bc43-59462da07403.pdf");
                
            // Now, download all of those files         
            if (_discoveredFiles.Count > 0)  
            {
                Console.WriteLine("Downloading all found files...");   

                foreach(string url in _discoveredFiles)
                {
                    if (!(await downloadFile(url)))
                    {
                        Console.WriteLine($"Error downloading file {url}");
                    }
                }
            } else {
                Console.WriteLine("No files to download!");
            }
        }
    }
}
