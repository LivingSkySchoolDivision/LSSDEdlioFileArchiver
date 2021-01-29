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
        static string _edlioFilesDomain = "files.edl.io";
        static string _outputFile_CrawledURls = "scraper-crawledurls.txt";
        static string _outputFile_Downloadables = "scraper-downloadables.txt";
        
        // Don't put trailing slashes
        static List<string> _rootURls = new List<string>() {
            "https://www.lskysd.ca"
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

            Console.WriteLine($"Found {foundLinks.Count} links.");
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

            Console.WriteLine($"Filtered {input_urls.Count} down to approved {ok_urls.Count} urls.");
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
        
        }
    }
}
