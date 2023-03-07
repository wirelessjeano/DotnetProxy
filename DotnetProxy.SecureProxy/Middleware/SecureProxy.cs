using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DotnetProxy.SecureProxy.Configs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

namespace DotnetProxy.SecureProxy.Middleware
{
    public class SecureProxy
    {
        private static HttpClient Client { get; set; }
        
        private static IOptions<ProxyConfigs> _configs;
            
        public SecureProxy(RequestDelegate next, IOptions<ProxyConfigs> configs)
        {
            // This is an HTTP Handler, so no need to store next
            Client = new HttpClient();
            Client.DefaultRequestHeaders.ConnectionClose = true;
            _configs = configs;
        }

        /// <summary>
        /// This middleware intercepts an initial HTTP request and forwards to a target URL
        /// </summary>
        /// <param name="context">The HttpContext to intercept</param>
        /// <returns>Response from the target URL</returns>
        public async Task Invoke(HttpContext context)
        {
            //initial request our HTTP proxy is routing to the target URL
            async Task SendRequestToTargetAsync(string targetUrl, bool sendResponse = true, bool forceJsonContentType = false)
            {
                var initialRequest = context.Request;

                //copy the path and the query params from the initial request to our target request URL
                var targetUrlWithParams = CopyUrlParams(initialRequest, targetUrl);

                //create our HttpRequest method to be sent off via the HttpClient
                var targetRequest = new HttpRequestMessage(new HttpMethod(initialRequest.Method), targetUrlWithParams);

                //copy the relevant headers from the initialRequest to the targetRequest
                foreach (var headerKey in initialRequest.Headers.Keys)
                {
                    try
                    {
                        switch (headerKey)
                        {
                            // Let Kestrel handle these. 
                            case "Connection":
                            case "Content-Length":
                            case "Date":
                            case "Expect":
                            case "Host":
                            case "If-Modified-Since":
                            case "Range":
                            case "Transfer-Encoding":
                            case "Proxy-Connection":
                            case "Accept-Encoding":
                                break;

                            // Restricted headers - we set these manually below
                            case "Accept":
                            case "Referer":
                            case "Content-Type":
                            case "User-Agent"
                                : //NB! do not change User-Agent header. This breaks the request when going through a browser.
                                break;

                            default:
                                if (initialRequest.Headers.ContainsKey(headerKey))
                                    targetRequest.Headers.TryAddWithoutValidation(headerKey,
                                        initialRequest.Headers[headerKey].FirstOrDefault());
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(@"Invalid header");
                    }

                }

                //manually set restricted headers
                targetRequest.Headers.Add("Accept", initialRequest.Headers["Accept"].FirstOrDefault());

                var referer = initialRequest.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(referer))
                    targetRequest.Headers.Referrer = new Uri(referer);


                //For POST and PUT requests we add the body content to the request 
                if (HttpMethods.IsPost(initialRequest.Method) || HttpMethods.IsPut(initialRequest.Method))
                {
                    // Enable seeking
                    initialRequest.EnableBuffering();
                    // Read the body of the request stream as text
                    var bodyAsText = await new System.IO.StreamReader(initialRequest.Body).ReadToEndAsync();
                    // Set the position of the stream to 0 to enable rereading
                    initialRequest.Body.Position = 0;

                    if (forceJsonContentType)
                    {
                        targetRequest.Content = new StringContent(bodyAsText, Encoding.UTF8, "application/json");
                    }
                    else
                    {
                        bool validBodyContentType =
                            MediaTypeHeaderValue.TryParse(initialRequest.ContentType, out var parsedBodyValue);

                        if (validBodyContentType)
                        {
                            //Add body content and copy the content-type header
                            targetRequest.Content = new StringContent(bodyAsText, Encoding.UTF8, parsedBodyValue.MediaType);
                        }
                        else
                        {
                            //Add body content and copy the content-type header
                            targetRequest.Content = new StringContent(bodyAsText, Encoding.UTF8);
                        }
                    }

                }

                //Send the request to to the target URL via the HttpClient
                var response = await Client.SendAsync(targetRequest);
                
                if (sendResponse)
                {
                    bool validResponseContentType =
                        MediaTypeHeaderValue.TryParse(response.Content.Headers.ContentType.ToString(),
                            out var parsedResponseValue);

                    if (validResponseContentType)
                    {
                        //Add body content and copy the content-type header
                        context.Response.ContentType = parsedResponseValue.MediaType;
                    }
                    else
                    {
                        //Add body content and copy the content-type header
                        context.Response.ContentType = "application/json";
                    }

                    context.Response.StatusCode = (int) response.StatusCode;
                    
                    await context.Response.WriteAsync(await response.Content.ReadAsStringAsync());
                }
            }

            await SendRequestToTargetAsync(_configs.Value.TargetUrl, true, true);
        }

        //this function builds the new request url from the initial request url
        //includes all query params and paths appended to the url
        private static string CopyUrlParams(HttpRequest initialRequest, string targetUrl)
        {
            var query = string.Empty;
            var firstElement = true;

            //Setup our requestUri
            var requestUri = new Uri(initialRequest.GetDisplayUrl());

            //get baseUri (i.e., the Authority)
            var baseUri = requestUri.GetLeftPart(UriPartial.Authority);

            //the pathUri includes the base and the path but no query params
            var pathUri = requestUri.GetLeftPart(UriPartial.Path);

            //now we remove the base to just have the path so we can add the query params after
            var pathOnlyUri = pathUri.Substring(baseUri.Length);

            //append the path to our request URL
            targetUrl += pathOnlyUri;

            //copy request params
            foreach (var (key, value) in initialRequest.Query)
            {
                query += (firstElement ? "?" : "&") + key + "=" + value;
                if (firstElement) firstElement = false;
            }

            //append the query params to our request URL
            targetUrl += query;
            return targetUrl;
        }
    }
}