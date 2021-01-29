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
        static readonly string _outputFile_Filenames = $"downloader-filenames-{DateTime.Now.ToString("yyyy-MM-dd-HHmm")}.txt";

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
                    Console.WriteLine($"{url} => {downloaded_path_with_filename}");
                }

                IEnumerable<string> headerContentDispositions = new List<string>();
                if (response.Content.Headers.TryGetValues("content-disposition", out headerContentDispositions))
                {
                    foreach(string cd in headerContentDispositions) 
                    {
                        using(StreamWriter writer = File.AppendText(_outputFile_Filenames))
                        {
                            writer.WriteLine($"{url}\t{Path.Combine(filepath,filename)}\t{cd}");
                        }  
                    }
                } 
            }

            // In the content-disposition header, we can get the original filename
            // example: content-disposition:	inline; filename*=UTF-8''I%20Can%20ELA-Assessment%20language.docx

            return true;  
        }

        static async Task Main(string[] args)
        {

            string url_list_file = "scraper-downloadables.txt";

            // Get command line arguments
            if (args.Length > 0) 
            {
                // Until we find a better way, just accept the filename for a text file of urls
                url_list_file = args[0];
            }

            // Try to open the file
            Console.WriteLine($"Opening url file \"{url_list_file}\"...");

            List<string> urls_to_download = new List<string>();
            foreach(string line in File.ReadAllLines(url_list_file))
            {
                if (!string.IsNullOrEmpty(line)) 
                {
                    if (!urls_to_download.Contains(line)) 
                    {
                        urls_to_download.Add(line);
                    }
                }
            }        

            Console.WriteLine($"Found {urls_to_download.Count} urls to download.");
                
            // Now, download all of those files         
            if (urls_to_download.Count > 0)  
            {
                Console.WriteLine("Downloading all found files...");   

                foreach(string url in urls_to_download)
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
