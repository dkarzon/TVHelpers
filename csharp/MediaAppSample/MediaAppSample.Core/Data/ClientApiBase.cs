﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace MediaAppSample.Core.Data
{
    /// <summary>
    /// Base class for any SDK client API implementation containing reusable logic for common call types, error handling, request retry attempts.
    /// </summary>
    public abstract class ClientApiBase : IDisposable
    {
        #region Variables

        protected HttpClient Client { get; private set; }

        protected Uri BaseUri { get; private set; }
        
        private const int E_WINHTTP_TIMEOUT = unchecked((int)0x80072ee2);
        private const int E_WINHTTP_NAME_NOT_RESOLVED = unchecked((int)0x80072ee7);
        private const int E_WINHTTP_CANNOT_CONNECT = unchecked((int)0x80072efd);
        private const int E_WINHTTP_CONNECTION_ERROR = unchecked((int)0x80072efe);

        #endregion

        #region Constructors

        public ClientApiBase(string baseURL = null)
        {
            this.BaseUri = new Uri(baseURL);
            this.Client = new HttpClient();
        }

        public void Dispose()
        {
            this.Client.Dispose();
            this.Client = null;
        }

        #endregion

        #region Methods

        #region Get

        /// <summary>
        /// Gets data from the specified URL.
        /// </summary>
        /// <typeparam name="T">Type for the strongly typed class representing data returned from the URL.</typeparam>
        /// <param name="url">URL to retrieve data from.</param>
        /// <param name="serializerType">Specifies how the data should be deserialized.</param>
        /// <param name="retryCount">Number of retry attempts if a call fails. Default is zero.</param>
        /// <returns>Instance of the type specified representing the data returned from the URL.</returns>
        /// <summary>
        protected async Task<T> GetAsync<T>(string url, CancellationToken ct, SerializerTypes serializerType = SerializerTypes.Default)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            var response = await this.Client.GetAsync(new Uri(this.BaseUri, url)).AsTask(ct);
            this.Log(response);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsStringAsync();
            return Serializer.Deserialize<T>(data, serializerType);
        }

        #endregion

        #region Post

        /// <summary>
        /// Posts data to the specified URL.
        /// </summary>
        /// <typeparam name="T">Type for the strongly typed class representing data returned from the URL.</typeparam>
        /// <param name="url">URL to retrieve data from.</param>
        /// <param name="contents">Any content that should be passed into the post.</param>
        /// <param name="serializerType">Specifies how the data should be deserialized.</param>
        /// <param name="retryCount">Number of retry attempts if a call fails. Default is zero.</param>
        /// <returns>Instance of the type specified representing the data returned from the URL.</returns>
        /// <summary>
        protected async Task<T> PostAsync<T, R>(string url, R contents, CancellationToken ct, SerializerTypes serializerType = SerializerTypes.Default) where R : IHttpContent
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));
            
            try
            {
                var response = await this.Client.PostAsync(new Uri(this.BaseUri, url), contents).AsTask(ct);
                this.Log(response);
                response.EnsureSuccessStatusCode();
                var data = await response.Content?.ReadAsStringAsync();
                return Serializer.Deserialize<T>(data, serializerType);
            }
            catch (Exception ex)
            {
                switch (ex.HResult)
                {
                    case E_WINHTTP_TIMEOUT:
                    // The connection to the server timed out.
                    case E_WINHTTP_NAME_NOT_RESOLVED:
                    case E_WINHTTP_CANNOT_CONNECT:
                    case E_WINHTTP_CONNECTION_ERROR:
                    // Unable to connect to the server. Check that you have Internet access.
                    default:
                        // "Unexpected error connecting to server: ex.Message
                        return default(T);
                }
            }
        }

        #endregion

        #region Logging

        /// <summary>
        /// Logs HttpRequest information to the application logger.
        /// </summary>
        /// <param name="request">Request to log.</param>
        private void Log(HttpRequestMessage request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (Platform.Current.Logger.CurrentLevel > LogLevels.Debug)
                return;

            Platform.Current.Logger.Log(LogLevels.Debug,
                Environment.NewLine + "---------------------------------" + Environment.NewLine +
                "WEB REQUEST to {0}" + Environment.NewLine +
                "-Method: {1}" + Environment.NewLine +
                "-Headers: {2}" + Environment.NewLine +
                "-Contents: " + Environment.NewLine + "{3}" + Environment.NewLine +
                "---------------------------------",
                request.RequestUri.OriginalString,
                request.Method.Method,
                request.Headers?.ToString(),
                request.Content?.ReadAsStringAsync().AsTask().Result
                );
        }

        /// <summary>
        /// Logs the HttpResponse object to the application logger.
        /// </summary>
        /// <param name="response">Response to log.</param>
        private void Log(HttpResponseMessage response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            if (Platform.Current.Logger.CurrentLevel > LogLevels.Debug)
                return;

            this.Log(response.RequestMessage);
            Platform.Current.Logger.Log(LogLevels.Debug,
                Environment.NewLine + "---------------------------------" + Environment.NewLine +
                "WEB RESPONSE to {0}" + Environment.NewLine +
                "-HttpStatus: {1}" + Environment.NewLine +
                "-Reason Phrase: {2}" + Environment.NewLine +
                "-ContentLength: {3:0.00 KB}" + Environment.NewLine +
                "-Contents: " + Environment.NewLine + "{4}" + Environment.NewLine +
                "---------------------------------",
                response.RequestMessage.RequestUri.OriginalString,
                string.Format("{0} {1}", (int)response.StatusCode, response.StatusCode.ToString()),
                response.ReasonPhrase,
                Convert.ToDecimal(Convert.ToDouble(response.Content.Headers.ContentLength) / 1024),
                response.Content?.ReadAsStringAsync().AsTask().Result
                );
        }

        #endregion

        #endregion
    }
}