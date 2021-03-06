﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MandrillApi.cs" company="">
//   
// </copyright>
// <summary>
//   Core class for using the MandrillApp Api
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;

using Mandrill.Models;
using Mandrill.Requests;
using Mandrill.Utilities;

using System.Net.Http;
using System.Text;

using Newtonsoft.Json;

namespace Mandrill
{
    /// <summary>
    ///   Core class for using the MandrillApp Api
    /// </summary>
    public partial class MandrillApi
    {
        #region Fields

        private readonly string baseUrl;

        private HttpClient _httpClient;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref="MandrillApi" /> class.
        /// </summary>
        /// <param name="apiKey">
        ///   The API Key recieved from MandrillApp
        /// </param>
        /// <param name="useHttps">
        /// </param>
        /// <param name="useStatic">
        /// </param>
        public MandrillApi(string apiKey, bool useHttps = true, bool useStatic = false)
        {
            ApiKey = apiKey;

            if (useHttps && useStatic)
            {
                baseUrl = Configuration.BASE_STATIC_SECURE_URL;
            }
            else if (useHttps && !useStatic)
            {
                baseUrl = Configuration.BASE_SECURE_URL;
            }
            else if (!useHttps && useStatic)
            {
                baseUrl = Configuration.BASE_STATIC_URL;
            }
            else
            {
                baseUrl = Configuration.BASE_URL;
            }

            // Store URL value to be used in public BaseURL property
            BaseUrl = baseUrl;
        }

        #endregion

        #region Public Properties

        /// <summary>
        ///   The Api Key for the project received from the MandrillApp website
        /// </summary>
        public string ApiKey { get; private set; }

        ///<Summary>
        /// The base URL value being used for call, useful for client logging purposes
        ///</Summary>
        public string BaseUrl { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///   Allows overriding the HttpClient which is used in Post()
        /// </summary>
        /// <param name="httpClient">the httpClient to use</param>
        public void SetHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        /// <summary>
        ///   Execute post to path
        /// </summary>
        /// <param name="path">the path to post to</param>
        /// <param name="data">the payload to send in request body as json</param>
        /// <returns></returns>
        public async Task<T> Post<T>(string path, RequestBase data)
        {
            data.Key = ApiKey;
            try
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                    _httpClient.BaseAddress = new Uri(baseUrl);
                }

                string requestContent;
                try
                {
                    requestContent = JsonConvert.SerializeObject(data);
                }
                catch (JsonException ex)
                {
                    throw new MandrillSerializeRequestException("Failed to serialize request data.", ex);
                }

                var response =
                    await
                    _httpClient.PostAsync(
                        path,
                        new StringContent(requestContent, Encoding.UTF8, "application/json"))
                        .ConfigureAwait(false);

                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return ParseResponseContent<T>(path, data, responseContent, response);

            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(string.Format("Post timed out to {0}", path), ex);
            }
        }
        #endregion

        #region Private Methods and Operators
        private static T ParseResponseContent<T>(string path, RequestBase data, string content, HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    return JsonConvert.DeserializeObject<T>(content);
                }
                catch (JsonException ex)
                {
                    throw new MandrillDeserializeResponseException(string.Format("Failed to deserialize content received from path: {0}.", path), ex)
                    {
                        HttpResponseMessage = response,
                        MandrillRequest = data,
                        Content = content
                    };
                }
            }

            try
            {
                var error = JsonConvert.DeserializeObject<ErrorResponse>(content);

                throw new MandrillException(error, string.Format("Post failed: {0}", path))
                {
                    HttpResponseMessage = response,
                    MandrillRequest = data
                };
            }
            catch (JsonException ex)
            {
                throw new MandrillDeserializeResponseException(string.Format("Failed to deserialize error content received from path: {0}.", path), ex)
                {
                    HttpResponseMessage = response,
                    MandrillRequest = data,
                    Content = content
                };
            }
        }
        #endregion
    }
}
