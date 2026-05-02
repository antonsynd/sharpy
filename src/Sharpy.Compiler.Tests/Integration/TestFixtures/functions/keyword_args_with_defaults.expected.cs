// Snapshot: Keyword arguments with default values
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class KeywordArgsWithDefaults
{
    public class ConfigBuilder
    {
        public string Host;
        public int Port;
        public int Timeout;
        public int RetryCount;
        public void DisplayConfig()
#line 16 "keyword_args_with_defaults.spy"
        {
#line (17, 9) - (17, 25) 1 "keyword_args_with_defaults.spy"
            global::Sharpy.Builtins.Print(this.Host);
#line (18, 9) - (18, 25) 1 "keyword_args_with_defaults.spy"
            global::Sharpy.Builtins.Print(this.Port);
#line (19, 9) - (19, 28) 1 "keyword_args_with_defaults.spy"
            global::Sharpy.Builtins.Print(this.Timeout);
#line (20, 9) - (20, 32) 1 "keyword_args_with_defaults.spy"
            global::Sharpy.Builtins.Print(this.RetryCount);
        }

        public ConfigBuilder(string host = "localhost", int port = 8080, int timeout = 30, int retryCount = 3)
#line 10 "keyword_args_with_defaults.spy"
        {
#line (11, 9) - (11, 25) 1 "keyword_args_with_defaults.spy"
            this.Host = host;
#line (12, 9) - (12, 25) 1 "keyword_args_with_defaults.spy"
            this.Port = port;
#line (13, 9) - (13, 31) 1 "keyword_args_with_defaults.spy"
            this.Timeout = timeout;
#line (14, 9) - (14, 39) 1 "keyword_args_with_defaults.spy"
            this.RetryCount = retryCount;
        }
    }

    public abstract class RequestHandler
    {
        public string BaseUrl;
        public abstract int SendRequest(string endpoint, string method, int retries);
        public RequestHandler(string url)
#line 26 "keyword_args_with_defaults.spy"
        {
#line (27, 9) - (27, 28) 1 "keyword_args_with_defaults.spy"
            this.BaseUrl = url;
        }
    }

    public class HttpClient : RequestHandler
    {
        public int MaxRetries;
        public override int SendRequest(string endpoint = "/api", string method = "GET", int retries = 3)
#line 41 "keyword_args_with_defaults.spy"
        {
#line (42, 9) - (42, 24) 1 "keyword_args_with_defaults.spy"
            global::Sharpy.Builtins.Print(endpoint);
#line (43, 9) - (43, 22) 1 "keyword_args_with_defaults.spy"
            global::Sharpy.Builtins.Print(method);
#line (44, 9) - (44, 24) 1 "keyword_args_with_defaults.spy"
            return retries;
        }

        public HttpClient(string url, int retries = 5) : base(url)
#line 36 "keyword_args_with_defaults.spy"
        {
#line (38, 9) - (38, 35) 1 "keyword_args_with_defaults.spy"
            this.MaxRetries = retries;
        }
    }

    public static int CalculateScore(int @base, int bonus = 10, int multiplier = 1, int penalty = 0)
    {
#line (47, 5) - (47, 32) 1 "keyword_args_with_defaults.spy"
        int result = @base + bonus;
#line (48, 5) - (48, 33) 1 "keyword_args_with_defaults.spy"
        result = result * multiplier;
#line (49, 5) - (49, 30) 1 "keyword_args_with_defaults.spy"
        result = result - penalty;
#line (50, 5) - (50, 19) 1 "keyword_args_with_defaults.spy"
        return result;
    }

    public static int ProcessData(string name, int value = 100, bool enabled = true)
    {
#line (53, 5) - (56, 1) 1 "keyword_args_with_defaults.spy"
        if (enabled)
        {
#line (54, 9) - (54, 20) 1 "keyword_args_with_defaults.spy"
            global::Sharpy.Builtins.Print(name);
#line (55, 9) - (55, 22) 1 "keyword_args_with_defaults.spy"
            return value;
        }

#line (56, 5) - (56, 14) 1 "keyword_args_with_defaults.spy"
        return 0;
    }

    public static void Main()
    {
#line (59, 5) - (59, 46) 1 "keyword_args_with_defaults.spy"
        ConfigBuilder config1 = new ConfigBuilder();
#line (60, 5) - (60, 29) 1 "keyword_args_with_defaults.spy"
        config1.DisplayConfig();
#line (62, 5) - (62, 75) 1 "keyword_args_with_defaults.spy"
        ConfigBuilder config2 = new ConfigBuilder(host: "example.com", port: 9000);
#line (63, 5) - (63, 24) 1 "keyword_args_with_defaults.spy"
        global::Sharpy.Builtins.Print(config2.Host);
#line (64, 5) - (64, 24) 1 "keyword_args_with_defaults.spy"
        global::Sharpy.Builtins.Print(config2.Port);
#line (66, 5) - (66, 80) 1 "keyword_args_with_defaults.spy"
        HttpClient client = new HttpClient(url: "https://api.example.com", retries: 10);
#line (67, 5) - (67, 84) 1 "keyword_args_with_defaults.spy"
        int result = client.SendRequest(endpoint: "/users", method: "POST", retries: 5);
#line (68, 5) - (68, 18) 1 "keyword_args_with_defaults.spy"
        global::Sharpy.Builtins.Print(result);
#line (70, 5) - (70, 44) 1 "keyword_args_with_defaults.spy"
        int score1 = CalculateScore(@base: 50);
#line (71, 5) - (71, 18) 1 "keyword_args_with_defaults.spy"
        global::Sharpy.Builtins.Print(score1);
#line (73, 5) - (73, 68) 1 "keyword_args_with_defaults.spy"
        int score2 = CalculateScore(@base: 50, bonus: 20, multiplier: 2);
#line (74, 5) - (74, 18) 1 "keyword_args_with_defaults.spy"
        global::Sharpy.Builtins.Print(score2);
#line (76, 5) - (76, 71) 1 "keyword_args_with_defaults.spy"
        int score3 = CalculateScore(@base: 100, penalty: 15, multiplier: 3);
#line (77, 5) - (77, 18) 1 "keyword_args_with_defaults.spy"
        global::Sharpy.Builtins.Print(score3);
#line (79, 5) - (79, 58) 1 "keyword_args_with_defaults.spy"
        int val = ProcessData(name: "DataPoint", value: 250);
#line (80, 5) - (80, 15) 1 "keyword_args_with_defaults.spy"
        global::Sharpy.Builtins.Print(val);
    }
}
