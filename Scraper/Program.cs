using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using HtmlAgilityPack;
using System.Threading.Tasks;

namespace LSSDEdlioFileArchiver.Scraper
{
    class Program
    {
        static readonly string _edlioFilesDomain = "files.edl.io";
        static readonly string _outputFile_CrawledURls = $"scraper-crawledurls-{DateTime.Now.ToString("yyyy-MM-dd-HHmm")}.txt";
        static readonly string _outputFile_Downloadables = $"scraper-downloadables-{DateTime.Now.ToString("yyyy-MM-dd-HHmm")}.txt";
        static readonly string _outputFile_LogFile = $"scraper-log-{DateTime.Now.ToString("yyyy-MM-dd-HHmm")}.txt";
        static readonly List<string> blacklisted_phrases = new List<string>() {
            "/events/",
            "subscribe/"
        };
        
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

        static List<string> filterUrls(List<string> input_urls, string root_url) 
        {
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

                // Ignore urls with blacklisted phrases
                foreach(string blphrase in blacklisted_phrases) 
                {
                    if (massaged_url.Contains(blphrase, StringComparison.InvariantCultureIgnoreCase)) 
                    {
                        continue;
                    }
                }

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

        static string sanitizeRootURL(string inputString) 
        {
            string working = inputString;

            if (string.IsNullOrEmpty(working)) {
                return string.Empty;
            }

            if (!working.StartsWith("http://") || !working.StartsWith("https://")) 
            {
                working = $"https://{working}";
            }

            if (working.EndsWith("/"))
            {
                working.Remove(working.Length, 1);
            }

            return working;
        }

        static void Log(string msg) 
        {
            using(StreamWriter writer = File.AppendText(_outputFile_LogFile))
            {
                writer.WriteLine($"{DateTime.Now.ToString()}: {msg}");
            }            
        }

        static void ConsoleWrite(string msg)
        {
            Log(msg);
            Console.WriteLine($"{DateTime.Now.ToString("t")}: {msg}");
        }

        static void RecordVisitedURL(string filename, string url) 
        {
            using(StreamWriter writer = File.AppendText(filename))
            {
                writer.WriteLine(url);
            }  

        }

        static void RecordDownloadable(string filename, string url) 
        {
            using(StreamWriter writer = File.AppendText(filename))
            {
                writer.WriteLine(url);
            }  
            
        }

        static async Task Main(string[] args)
        {
            Random random = new Random();

            List<string> _discoveredFiles = new List<string>();
            List<string> _visitedURLs = new List<string>();
            
            List<string> urlsToScan = new List<string>();
            string filename_urls = _outputFile_CrawledURls;
            string filename_downloadables = _outputFile_Downloadables;

            // Get command line arguments
            if (args.Length > 0) 
            {
                // Until we find a better way, just accept the root url to scrape, or a list of root urls
                foreach(string arg in args) 
                {
                    if (!string.IsNullOrEmpty(arg)) 
                    {
                        string sanitizedURL = sanitizeRootURL(arg);
                        if (!string.IsNullOrEmpty(sanitizedURL))
                        {
                            if (!urlsToScan.Contains(sanitizedURL))
                            {
                                urlsToScan.Add(sanitizedURL);
                            }
                        }
                    }
                }
            }

            // Housekeeping
            // If the output files already exist, remove them
            if (File.Exists(filename_urls))
            {
                File.Delete(filename_urls);
            }

            if (File.Exists(filename_downloadables))
            {
                File.Delete(filename_downloadables);
            }

            if (urlsToScan.Count > 0)
            {
                // Start scrapin'
                int rootCount = 0;
                foreach (string rooturl in urlsToScan)
                {
                    rootCount++;
                    Queue<string> urls_to_visit = new Queue<string>();
                    urls_to_visit.Enqueue(rooturl);

                    while(urls_to_visit.Count > 0)
                    {
                        string this_url = urls_to_visit.Dequeue();
                        RecordVisitedURL(filename_urls, this_url);
                        
                        ConsoleWrite($"Crawling \"{this_url}\" ({rootCount}/{urls_to_visit.Count+1})...");

                        // Visit the url
                        List<string> foundUrls = await getAllValidLinksFromURLAsync(this_url, rooturl);

                        // Mark the url as visited
                        _visitedURLs.Add(this_url);

                        foreach(string url in foundUrls)
                        {
                            // Add files links to the discovered files collection
                            if (url.Contains(_edlioFilesDomain)) {

                                if (!_discoveredFiles.Contains(url))
                                {
                                    Log($"> Found file: {url}");
                                    _discoveredFiles.Add(url);
                                    RecordDownloadable(filename_downloadables, url);
                                }

                            } else {

                                // Add discovered links to the queue, as long as we've never been to them before
                                if (!_visitedURLs.Contains(url))
                                {
                                    if (!urls_to_visit.Contains(url))
                                    {
                                        Log($"> Adding site to queue: {url}");
                                        urls_to_visit.Enqueue(url);                                        
                                    }
                                }
                            }
                        }

                        // Report our status
                        Log($"Finished crawling {this_url}");

                        // Wait a few second so we don't hammer the server
                        Thread.Sleep(random.Next(100,500));
                    }

                    ConsoleWrite("Crawl Complete.");
                    ConsoleWrite($"Visited {_visitedURLs.Count} urls.");
                    ConsoleWrite($"Found {_discoveredFiles.Count} files.");

                }  
            } else {
                ConsoleWrite("No urls to scrape!");
            }     
        }
    }
}
