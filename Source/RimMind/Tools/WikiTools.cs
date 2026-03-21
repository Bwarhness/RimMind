using System;
using System.IO;
using System.Net;
using System.Text;
using RimMind.API;
using Verse;

namespace RimMind.Tools
{
    public static class WikiTools
    {
        private const string WIKI_BASE = "https://rimworldwiki.com/api.php";
        private const int MAX_RESULT_CHARS = 800;
        private const int TIMEOUT_MS = 10000;

        public static string WikiLookup(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return ToolExecutor.JsonError("Query parameter required.");

            try
            {
                // Step 1: Search for the query
                string searchUrl = WIKI_BASE + "?action=query&format=json&list=search&srsearch=" + 
                    Uri.EscapeDataString(query) + "&srlimit=3";
                
                string searchJson = FetchUrl(searchUrl);
                if (searchJson == null)
                    return ToolExecutor.JsonError("Failed to search wiki - network error.");

                var searchResult = JSONNode.Parse(searchJson);
                var searchList = searchResult?["query"]?["search"]?.AsArray;

                if (searchList == null || searchList.Count == 0)
                {
                    var noResult = new JSONObject();
                    noResult["query"] = query;
                    noResult["found"] = false;
                    noResult["message"] = "No wiki pages found for '" + query + "'.";
                    return noResult.ToString();
                }

                // Get the best match (first result)
                string pageTitle = searchList[0]["title"]?.Value;
                if (string.IsNullOrEmpty(pageTitle))
                    return ToolExecutor.JsonError("Wiki search returned invalid result.");

                // Step 2: Get the page intro wikitext via parse API
                string parseUrl = WIKI_BASE + "?action=parse&format=json&page=" +
                    Uri.EscapeDataString(pageTitle) + "&prop=wikitext&section=0";

                string parseJson = FetchUrl(parseUrl);
                if (parseJson == null)
                    return ToolExecutor.JsonError("Failed to fetch wiki page - network error.");

                var parseResult = JSONNode.Parse(parseJson);
                var parseNode = parseResult?["parse"];

                if (parseNode == null)
                    return ToolExecutor.JsonError("Wiki returned no page data.");

                string actualTitle = parseNode["title"]?.Value;
                string rawWikitext = parseNode["wikitext"]?["*"]?.Value;
                string extract = StripWikiMarkup(rawWikitext);

                if (string.IsNullOrEmpty(extract))
                {
                    var noExtract = new JSONObject();
                    noExtract["query"] = query;
                    noExtract["title"] = actualTitle ?? pageTitle;
                    noExtract["found"] = true;
                    noExtract["message"] = "Page found but no intro text available.";
                    noExtract["url"] = "https://rimworldwiki.com/wiki/" + Uri.EscapeDataString(pageTitle.Replace(" ", "_"));
                    return noExtract.ToString();
                }

                // Truncate if needed
                string truncatedExtract = extract;
                bool wasTruncated = false;
                if (extract.Length > MAX_RESULT_CHARS)
                {
                    // Try to truncate at a sentence boundary
                    int cutoff = extract.LastIndexOf('.', MAX_RESULT_CHARS);
                    if (cutoff > MAX_RESULT_CHARS / 2)
                        truncatedExtract = extract.Substring(0, cutoff + 1);
                    else
                        truncatedExtract = extract.Substring(0, MAX_RESULT_CHARS) + "...";
                    wasTruncated = true;
                }

                // Build result
                var result = new JSONObject();
                result["query"] = query;
                result["title"] = actualTitle ?? pageTitle;
                result["extract"] = truncatedExtract.Trim();
                result["truncated"] = wasTruncated;
                result["url"] = "https://rimworldwiki.com/wiki/" + Uri.EscapeDataString(pageTitle.Replace(" ", "_"));

                // Include other search results as related pages
                if (searchList.Count > 1)
                {
                    var related = new JSONArray();
                    for (int i = 1; i < searchList.Count && i < 3; i++)
                    {
                        string relatedTitle = searchList[i]["title"]?.Value;
                        if (!string.IsNullOrEmpty(relatedTitle))
                            related.Add(relatedTitle);
                    }
                    if (related.Count > 0)
                        result["related_pages"] = related;
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] WikiTools error: " + ex.Message);
                return ToolExecutor.JsonError("Wiki lookup failed: " + ex.Message);
            }
        }

        private static string StripWikiMarkup(string wikitext)
        {
            if (string.IsNullOrEmpty(wikitext)) return null;

            var sb = new StringBuilder(wikitext.Length);
            int i = 0;
            while (i < wikitext.Length)
            {
                // Skip templates {{ ... }} (handle nesting)
                if (i < wikitext.Length - 1 && wikitext[i] == '{' && wikitext[i + 1] == '{')
                {
                    int depth = 1;
                    i += 2;
                    while (i < wikitext.Length - 1 && depth > 0)
                    {
                        if (wikitext[i] == '{' && wikitext[i + 1] == '{') { depth++; i += 2; }
                        else if (wikitext[i] == '}' && wikitext[i + 1] == '}') { depth--; i += 2; }
                        else i++;
                    }
                    continue;
                }
                // Skip HTML comments <!-- ... -->
                if (i < wikitext.Length - 3 && wikitext[i] == '<' && wikitext[i + 1] == '!' && wikitext[i + 2] == '-' && wikitext[i + 3] == '-')
                {
                    int end = wikitext.IndexOf("-->", i + 4);
                    i = end >= 0 ? end + 3 : wikitext.Length;
                    continue;
                }
                // Skip wiki tables {| ... |}
                if (i < wikitext.Length - 1 && wikitext[i] == '{' && wikitext[i + 1] == '|')
                {
                    int end = wikitext.IndexOf("|}", i + 2);
                    i = end >= 0 ? end + 2 : wikitext.Length;
                    continue;
                }
                // Strip [[link|display]] -> display, [[link]] -> link
                if (i < wikitext.Length - 1 && wikitext[i] == '[' && wikitext[i + 1] == '[')
                {
                    int end = wikitext.IndexOf("]]", i + 2);
                    if (end >= 0)
                    {
                        string inner = wikitext.Substring(i + 2, end - i - 2);
                        int pipe = inner.IndexOf('|');
                        sb.Append(pipe >= 0 ? inner.Substring(pipe + 1) : inner);
                        i = end + 2;
                        continue;
                    }
                }
                // Strip bold/italic markers
                if (wikitext[i] == '\'') { i++; continue; }
                // Skip ---- horizontal rules
                if (i < wikitext.Length - 3 && wikitext[i] == '-' && wikitext[i + 1] == '-' && wikitext[i + 2] == '-' && wikitext[i + 3] == '-')
                {
                    while (i < wikitext.Length && wikitext[i] == '-') i++;
                    continue;
                }
                sb.Append(wikitext[i]);
                i++;
            }

            // Clean up extra whitespace
            string result = sb.ToString().Trim();
            while (result.Contains("\n\n\n"))
                result = result.Replace("\n\n\n", "\n\n");
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        private static string FetchUrl(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.UserAgent = "RimMind/1.0 (RimWorld Mod; +https://github.com/rimmind)";
                request.Timeout = TIMEOUT_MS;
                request.ReadWriteTimeout = TIMEOUT_MS;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException webEx)
            {
                Log.Warning("[RimMind] Wiki fetch error: " + webEx.Message);
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Wiki fetch exception: " + ex.Message);
                return null;
            }
        }
    }
}
