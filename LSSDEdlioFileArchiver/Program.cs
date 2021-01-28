using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using HtmlAgilityPack;
using System.Threading.Tasks;

namespace LSSDEdlioFileArchiver
{
    class Program
    {
        static string _edlioFilesDomain = "files.edl.io";
        static string _downloadDirectory = "download";
        static string _filename_table_filename = "FilenameMappings.txt";

        // Don't put trailing slashes
        static List<string> _rootURls = new List<string>() {
//            "https://www.lskysd.ca"
        };

        const int maxDepth = 10;

        static async Task<string> getHTMLBody(string url)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("LSSD-Site-Archiver", "0.1"));
                    HttpResponseMessage response = await httpClient.SendAsync(request);
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        static List<string> parseHrefs(string htmlBody)
        {
            List<string> foundLinks = new List<string>();

            try {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlBody);

                foreach(HtmlNode link in doc.DocumentNode.SelectNodes("//a[@href]"))
                {
                    string hrefValue = link.GetAttributeValue("href", string.Empty);

                    if (!foundLinks.Contains(hrefValue))
                    {
                        foundLinks.Add(hrefValue);
                    }
                }
            }
            catch {}
            return foundLinks;
        }

        static List<string> filterUrls(List<string> input_urls, string root_url) {

            List<string> ok_urls = new List<string>();

            foreach(string url in input_urls)
            {
                string massaged_url = url;
                // Ignore tel:
                if (massaged_url.StartsWith("tel", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                // Ignore anchors
                if (massaged_url.StartsWith("#", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                // Ignore mailto
                if (massaged_url.StartsWith("mailto:", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                // Ignore mailto
                if (massaged_url.StartsWith("javascript:", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                // Ignore "/"
                if (massaged_url.Equals("/"))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(massaged_url))
                {
                    continue;
                }

                // Try to parse the url into a uri object. If it fails, we don't want it
                try {
                    Uri test_uri = new Uri(massaged_url);
                }
                catch { continue; }

                // Convert relative URLs into absolute urls
                if (massaged_url.StartsWith("/"))
                {
                    massaged_url = $"{root_url}{massaged_url}";                    
                }

                // Ignore from other domains
                if (!((massaged_url.Contains(root_url)) || (massaged_url.Contains(_edlioFilesDomain))))
                {
                    continue;
                }

                ok_urls.Add(massaged_url);
            }

            return ok_urls;
        }

        static async Task<List<string>> getAllValidLinksFromURLAsync(string uri, string root_url)
        {
            if (!string.IsNullOrEmpty(uri))
            {
                return filterUrls(parseHrefs(await getHTMLBody(uri)), root_url);
            } else {
                return new List<string>();
            }
        }

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
            List<string> _visitedURLs = new List<string>();

            foreach (string rooturl in _rootURls)
            {
                Queue<string> urls_to_visit = new Queue<string>();
                urls_to_visit.Enqueue(rooturl);

                while(urls_to_visit.Count > 0)
                {
                    string this_url = urls_to_visit.Dequeue();

                    Console.WriteLine($"Crawling {this_url}...");

                    // Visit the url
                    List<string> foundUrls = await getAllValidLinksFromURLAsync(this_url, rooturl);

                    Console.WriteLine($"> Found {foundUrls.Count} urls");

                    // Mark the url as visited
                    _visitedURLs.Add(this_url);

                    foreach(string url in foundUrls)
                    {
                        // Add files links to the discovered files collection
                        if (url.Contains(_edlioFilesDomain)) {

                            if (!_discoveredFiles.Contains(url))
                            {
                                Console.WriteLine($"Found file: {url}");
                                _discoveredFiles.Add(url);
                            }

                        } else {

                            // Add discovered links to the queue, as long as we've never been to them before
                            if (!_visitedURLs.Contains(url))
                            {
                                if (!urls_to_visit.Contains(url))
                                {
                                    Console.WriteLine($"Adding site to queue: {url}");
                                    urls_to_visit.Enqueue(url);
                                }
                            }
                        }
                    }

                    // Report our status
                    Console.WriteLine($"Visited: {_visitedURLs.Count}, Left to visit: {urls_to_visit.Count}");

                    // Wait a few second so we don't hammer the server
                    Thread.Sleep(500);
                }

                Console.WriteLine("Crawl Complete.");
                Console.WriteLine("Visited urls:");
                foreach(string url in _visitedURLs)
                {
                    Console.WriteLine(url);
                }
                
                Console.WriteLine("Found files:");
                foreach(string url in _discoveredFiles)
                {
                    Console.WriteLine(url);
                }
                
            }

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
