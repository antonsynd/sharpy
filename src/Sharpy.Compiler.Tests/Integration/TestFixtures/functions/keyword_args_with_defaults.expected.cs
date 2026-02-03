#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.KeywordArgsWithDefaults
{
    public static class Program
    {
        public static int CalculateScore(int @base, int bonus = 10, int multiplier = 1, int penalty = 0)
        {
#line 47 "keyword_args_with_defaults.spy"
            int result = @base + bonus;
#line 48 "keyword_args_with_defaults.spy"
            result = result * multiplier;
#line 49 "keyword_args_with_defaults.spy"
            result = result - penalty;
#line 50 "keyword_args_with_defaults.spy"
            return result;
        }

        public static int ProcessData(string name, int value = 100, bool enabled = true)
        {
#line 53 "keyword_args_with_defaults.spy"
            if (enabled)
            {
#line 54 "keyword_args_with_defaults.spy"
                global::Sharpy.Core.Exports.Print(name);
#line 55 "keyword_args_with_defaults.spy"
                return value;
            }

#line 56 "keyword_args_with_defaults.spy"
            return 0;
        }

        public static void Main()
        {
#line 59 "keyword_args_with_defaults.spy"
            ConfigBuilder config1 = new ConfigBuilder();
#line 60 "keyword_args_with_defaults.spy"
            config1.DisplayConfig();
#line 62 "keyword_args_with_defaults.spy"
            ConfigBuilder config2 = new ConfigBuilder(host: "example.com", port: 9000);
#line 63 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(config2.Host);
#line 64 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(config2.Port);
#line 66 "keyword_args_with_defaults.spy"
            HttpClient client = new HttpClient(url: "https://api.example.com", retries: 10);
#line 67 "keyword_args_with_defaults.spy"
            int result = client.SendRequest(endpoint: "/users", method: "POST", retries: 5);
#line 68 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(result);
#line 70 "keyword_args_with_defaults.spy"
            int score1 = CalculateScore(@base: 50);
#line 71 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(score1);
#line 73 "keyword_args_with_defaults.spy"
            int score2 = CalculateScore(@base: 50, bonus: 20, multiplier: 2);
#line 74 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(score2);
#line 76 "keyword_args_with_defaults.spy"
            int score3 = CalculateScore(@base: 100, penalty: 15, multiplier: 3);
#line 77 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(score3);
#line 79 "keyword_args_with_defaults.spy"
            int val = ProcessData(name: "DataPoint", value: 250);
#line 80 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(val);
        }
    }

    public class ConfigBuilder
    {
        public string Host;
        public int Port;
        public int Timeout;
        public int RetryCount;
        public void DisplayConfig()
        {
#line 17 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(this.Host);
#line 18 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(this.Port);
#line 19 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(this.Timeout);
#line 20 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(this.RetryCount);
        }

        public ConfigBuilder(string host = "localhost", int port = 8080, int timeout = 30, int retryCount = 3)
        {
#line 11 "keyword_args_with_defaults.spy"
            this.Host = host;
#line 12 "keyword_args_with_defaults.spy"
            this.Port = port;
#line 13 "keyword_args_with_defaults.spy"
            this.Timeout = timeout;
#line 14 "keyword_args_with_defaults.spy"
            this.RetryCount = retryCount;
        }
    }

    public abstract class RequestHandler
    {
        public string BaseUrl;
        public abstract int SendRequest(string endpoint, string method, int retries);
        public RequestHandler(string url)
        {
#line 27 "keyword_args_with_defaults.spy"
            this.BaseUrl = url;
        }
    }

    public class HttpClient : RequestHandler
    {
        public int MaxRetries;
        public override int SendRequest(string endpoint = "/api", string method = "GET", int retries = 3)
        {
#line 42 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(endpoint);
#line 43 "keyword_args_with_defaults.spy"
            global::Sharpy.Core.Exports.Print(method);
#line 44 "keyword_args_with_defaults.spy"
            return retries;
        }

        public HttpClient(string url, int retries = 5) : base(url)
        {
#line 38 "keyword_args_with_defaults.spy"
            this.MaxRetries = retries;
        }
    }
}
