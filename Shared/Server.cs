﻿using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

namespace PerpetualEngine
{
    public class Server
    {
        public string Get(string url)
        {
            return Request("GET", url);
        }

        public async Task<string> GetAsync(string url)
        {
            return await Task.Run(() => {
                return Get(url);
            });
        }

        public void Get(string url, string filePath)
        {
            RequestDownload(url, filePath);
        }

        public async void GetAsync(string url, string filePath)
        {
            await Task.Run(() => {
                Get(url, filePath);
            });
        }

        public string Post(string url, string filePath)
        {
            return RequestUpload(url, filePath);
        }

        public async Task<string> PostAsync(string url, string filePath)
        {
            return await Task.Run(() => {
                return Post(url, filePath);
            });
        }

        public string Delete(string url)
        {
            return Request("DELETE", url);
        }

        public async Task<string> DeleteAsync(string url)
        {
            return await Task.Run(() => {
                return Delete(url);
            });
        }

        private string Request(string method, string url)
        {
            var request = HttpWebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = method;
            try {
                using (var response = request.GetResponse() as HttpWebResponse) {
                    if (response.StatusCode != HttpStatusCode.OK)
                        Console.WriteLine("Error fetching data. Server returned status code: {0}", response.StatusCode);
                    // TODO "else"? But not in example at http://docs.xamarin.com/guides/cross-platform/application_fundamentals/web_services/#Using_HTTPWebRequest
                    using (var reader = new StreamReader(response.GetResponseStream())) {
                        var content = reader.ReadToEnd();
                        if (string.IsNullOrWhiteSpace(content)) {
                            Console.WriteLine("Response contained empty body...");
                        } else {
                            Console.WriteLine("Response body: \r\n {0}", content);
                            return content;
                        }
                    }
                }
            } catch (Exception e) {
                Console.WriteLine(e.Message);
            }
            return null;
        }

        private void RequestDownload(string url, string filePath)
        {
            var request = HttpWebRequest.Create(url);
            using (var response = request.GetResponse()) {
                using (var input = response.GetResponseStream()) {
                    using (var output = new FileStream(filePath, FileMode.Create)) {
                        int bytesRead;
                        byte[] buffer = new byte[32768];
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0) {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }
        }
        //
        //
        // The following code implements multipart form posts and is based on a post at
        // http://www.briangrinstead.com/blog/multipart-form-post-in-c
        private string RequestUpload(string url, string filePath)
        {
            Console.WriteLine("uploading to " + url);
            FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Close();

            Dictionary<string, object> postParameters = new Dictionary<string, object>();
            postParameters.Add("filename", filePath);
            postParameters.Add("fileformat", "png"); // TODO more general
            postParameters.Add("file", new FileParameter(data, filePath, "application/octet-stream")); // TODO more general

            var fullResponse = "";
            // Create request and receive response
            string userAgent = "Someone"; // TODO necessary?
            using (var webResponse = MultipartFormDataPost(url, userAgent, postParameters)) {

                // Process response
                using (var stream = webResponse.GetResponseStream()) {
                    using (var responseReader = new StreamReader(stream)) {
                        fullResponse = responseReader.ReadToEnd();
//                        var c = ' ';
//                        while (c != '}') {
//                            c = (char)responseReader.Read();
//                            fullResponse += c;
//                            if (c == '"')
//                                c = c;
//                        }
                    }
                }
            }
            return fullResponse;
        }

        private static readonly Encoding encoding = Encoding.UTF8;

        private static HttpWebResponse MultipartFormDataPost(string postUrl, string userAgent, Dictionary<string, object> postParameters)
        {
            string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
            string contentType = "multipart/form-data; boundary=" + formDataBoundary;

            byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

            return PostForm(postUrl, userAgent, contentType, formData);
        }

        private static HttpWebResponse PostForm(string postUrl, string userAgent, string contentType, byte[] formData)
        {
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

            if (request == null) {
                throw new NullReferenceException("request is not a http request");
            }

            // Set up the request properties.
            request.Method = "POST";
            request.ContentType = contentType;
            request.UserAgent = userAgent;
            request.CookieContainer = new CookieContainer();
            request.ContentLength = formData.Length;

            // You could add authentication here as well if needed:
            // request.PreAuthenticate = true;
            // request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
            // request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.Default.GetBytes("username" + ":" + "password")));

            // Send the form data to the request.
            using (Stream requestStream = request.GetRequestStream()) {
                requestStream.Write(formData, 0, formData.Length);
                //                requestStream.Close();
            }

            return request.GetResponse() as HttpWebResponse;
        }

        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters) {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                if (param.Value is FileParameter) {
                    FileParameter fileToUpload = (FileParameter)param.Value;

                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n",
                                        boundary,
                                        param.Key,
                                        fileToUpload.FileName ?? param.Key,
                                        fileToUpload.ContentType ?? "application/octet-stream");

                    formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                } else {
                    string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                                          boundary,
                                          param.Key,
                                          param.Value);
                    formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }

        public class FileParameter
        {
            public byte[] File { get; set; }

            public string FileName { get; set; }

            public string ContentType { get; set; }

            public FileParameter(byte[] file) : this(file, null)
            {
            }

            public FileParameter(byte[] file, string filename) : this(file, filename, null)
            {
            }

            public FileParameter(byte[] file, string filename, string contenttype)
            {
                File = file;
                FileName = filename;
                ContentType = contenttype;
            }
        }
    }
}

