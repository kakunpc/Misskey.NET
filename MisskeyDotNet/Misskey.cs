﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MisskeyDotNet
{
    public sealed class Misskey
    {
        public static HttpClient Http { get; } = new HttpClient();

        /// <summary>
        /// Get a host name of the connecting Misskey instance.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// IsNotSecureServer
        /// </summary>
        public bool IsNotSecureServer { get; } = false;

        /// <summary>
        /// Get a current API token.
        /// </summary>
        public string? Token { get; }

        /// <summary>
        /// Initialize a new instance of <see cref="Misskey"/> class.
        /// </summary>
        /// <param name="host"></param>
        public Misskey(string host)
        {
            Host = host;
        }

        /// <summary>
        /// Initialize a new instance of <see cref="Misskey"/> class.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="isNotSecureServer"></param>
        public Misskey(string host, bool isNotSecureServer)
        {
            Host = host;
            IsNotSecureServer = isNotSecureServer;
        }

        /// <summary>
        /// Initialize a new instance of <see cref="Misskey"/> class.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="token"></param>
        public Misskey(string host, string? token) : this(host)
        {
            Token = token;
        }

        /// <summary>
        /// Initialize a new instance of <see cref="Misskey"/> class.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="token"></param>
        public Misskey(string host, bool isNotSecureServer, string? token) : this(host)
        {
            Token = token;
            IsNotSecureServer = isNotSecureServer;
        }

        /// <summary>
        /// Call HTTP API.
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public ValueTask<Dictionary<string, object>> ApiAsync(string endPoint, object? parameters = null)
        {
            return ApiAsync<Dictionary<string, object>>(endPoint, parameters);
        }

        /// <summary>
        /// Call HTTP API.
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="parameters"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async ValueTask<T> ApiAsync<T>(string endPoint, object? parameters = null)
        {
            var dict = parameters.ConvertToDictionary();

            if (Token != null)
                dict["i"] = Token;

            var json = JsonConvert.SerializeObject(dict);
            var res = await Http.PostAsync(GetApiUrl(endPoint), new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
            var content = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode)
            {
                return Deserialize<T>(content);
            }
            switch (res.StatusCode)
            {
                case HttpStatusCode.InternalServerError:
                case HttpStatusCode.BadRequest:
                    throw new MisskeyApiException(
                        res.StatusCode == HttpStatusCode.InternalServerError
                        ? Deserialize<Error>(content)
                        : Deserialize<ErrorWrapper>(content).Error
                    );
                case HttpStatusCode.Unauthorized:
                    throw new MisskeyApiRequireTokenException();
                default:
                    throw new HttpException(res.StatusCode);
            }
        }

        /// <summary>
        /// Serialize this session.
        /// </summary>
        /// <returns></returns>
        public string Export()
        {
            var s = "Host=" + Host;
            if (Token is string)
            {
                s += "\nToken=" + Token;
            }
            return s;
        }

        /// <summary>
        /// Restore a serizlied session.
        /// </summary>
        /// <param name="serialized"></param>
        /// <returns></returns>
        public static Misskey Import(string serialized)
        {
            var o = serialized.Split('\n').Select(s => s.Split('=', 2)).Select(a => (key: a[0], value: a[1]));
            var host = "";
            string? token = null;

            foreach (var (key, value) in o)
            {
                switch (key)
                {
                    case "Host":
                        host = value;
                        break;
                    case "Token":
                        token = value;
                        break;
                }
            }

            return new Misskey(host, token);
        }

        public async ValueTask<Meta> MetaAsync(bool forceFetch = false)
        {
            if (forceFetch || cachedMeta is null)
            {
                cachedMeta = await ApiAsync<Meta>("meta");
            }
            return cachedMeta;
        }

        public async ValueTask<User> IAsync(bool forceFetch = false)
        {
            if (forceFetch || cachedI is null)
            {
                cachedI = await ApiAsync<User>("i");
            }
            return cachedI;
        }

        public static async ValueTask<JoinMisskeyApiResponse> JoinMisskeyInstancesApiAsync()
        {
            var res = await Http.GetAsync(joinMisskeyJsonUrl);
            var content = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode)
            {
                return Deserialize<JoinMisskeyApiResponse>(content);
            }
            throw new HttpException(res.StatusCode);
        }

        private string GetApiUrl(string endPoint)
        {
            var protocol = IsNotSecureServer ? "http://" : "https://";
            return protocol + Host + "/api/" + endPoint;
        }

        private static T Deserialize<T>(string s)
        {
            return JsonConvert.DeserializeObject<T>(s, new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
            })!;
        }

        private static readonly DefaultContractResolver contractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        };

        private Meta? cachedMeta;
        private User? cachedI;

        private static readonly string joinMisskeyJsonUrl = "https://instanceapp.misskey.page/instances.json";
    }
}
