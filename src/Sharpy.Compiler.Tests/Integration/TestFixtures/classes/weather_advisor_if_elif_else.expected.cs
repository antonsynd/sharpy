#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.WeatherAdvisorIfElifElse
{
    public static class Program
    {
        public static void Main()
        {
#line 39 "weather_advisor_if_elif_else.spy"
            WeatherAdvisor advisor1 = new WeatherAdvisor(5, true, 15);
#line 40 "weather_advisor_if_elif_else.spy"
            global::Sharpy.Core.Exports.Print(advisor1.GetActivityRecommendation());
#line 42 "weather_advisor_if_elif_else.spy"
            WeatherAdvisor advisor2 = new WeatherAdvisor(25, false, 5);
#line 43 "weather_advisor_if_elif_else.spy"
            global::Sharpy.Core.Exports.Print(advisor2.GetActivityRecommendation());
#line 45 "weather_advisor_if_elif_else.spy"
            WeatherAdvisor advisor3 = new WeatherAdvisor(35, false, 10);
#line 46 "weather_advisor_if_elif_else.spy"
            global::Sharpy.Core.Exports.Print(advisor3.GetActivityRecommendation());
#line 48 "weather_advisor_if_elif_else.spy"
            WeatherAdvisor advisor4 = new WeatherAdvisor(15, false, 8);
#line 49 "weather_advisor_if_elif_else.spy"
            global::Sharpy.Core.Exports.Print(advisor4.GetActivityRecommendation());
        }
    }

    public class WeatherAdvisor
    {
        public int Temperature;
        public bool IsRaining;
        public int WindSpeed;
        public string GetActivityRecommendation()
        {
#line 13 "weather_advisor_if_elif_else.spy"
            if (this.Temperature < 0)
            {
#line 14 "weather_advisor_if_elif_else.spy"
                return "Stay inside";
            }
            else if (this.Temperature < 10)
            {
#line 16 "weather_advisor_if_elif_else.spy"
                if (this.IsRaining)
                {
#line 17 "weather_advisor_if_elif_else.spy"
                    return "Warm clothing needed";
                }
                else
                {
#line 19 "weather_advisor_if_elif_else.spy"
                    return "Bundle up";
                }
            }
            else if (this.Temperature < 20)
            {
#line 21 "weather_advisor_if_elif_else.spy"
                return "Light jacket recommended";
            }
            else if (this.Temperature < 30)
            {
#line 23 "weather_advisor_if_elif_else.spy"
                if (this.WindSpeed > 20)
                {
#line 24 "weather_advisor_if_elif_else.spy"
                    return "Nice but windy";
                }
                else
                {
#line 26 "weather_advisor_if_elif_else.spy"
                    return "Perfect outdoor weather";
                }
            }
            else
            {
#line 28 "weather_advisor_if_elif_else.spy"
                return "Very hot";
            }
        }

        public bool ShouldBringUmbrella()
        {
#line 31 "weather_advisor_if_elif_else.spy"
            if (this.IsRaining)
            {
#line 32 "weather_advisor_if_elif_else.spy"
                return true;
            }
            else if (this.Temperature > 25 && this.WindSpeed < 10)
            {
#line 34 "weather_advisor_if_elif_else.spy"
                return false;
            }
            else
            {
#line 36 "weather_advisor_if_elif_else.spy"
                return false;
            }
        }

        public WeatherAdvisor(int temp, bool rain, int wind)
        {
#line 8 "weather_advisor_if_elif_else.spy"
            this.Temperature = temp;
#line 9 "weather_advisor_if_elif_else.spy"
            this.IsRaining = rain;
#line 10 "weather_advisor_if_elif_else.spy"
            this.WindSpeed = wind;
        }
    }
}
