using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Discord.WebSocket;
using NLog;
using Roki.Extensions;
using Roki.Modules.Searches.Models;
using Roki.Services;

namespace Roki.Modules.Searches.Services
{
    public class SearchService : IRokiService
    {
        private readonly IRokiConfig _config;
        private readonly IHttpClientFactory _httpFactory;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public SearchService(IHttpClientFactory httpFactory, IRokiConfig config)
        {
            _httpFactory = httpFactory;
            _config = config;
        }

        public async Task<string> GetWeatherDataAsync(float lat, float lng)
        {
            try
            {
                using var http = _httpFactory.CreateClient();
                var result = await http.GetStringAsync($"https://wttr.in/{lat},{lng}?0ATQ").ConfigureAwait(false);
                return result;
            }
            catch (Exception e)
            {
                Logger.Warn(e);
                return null;
            }
        }

        public async Task<TimeData> GetTimeDataAsync(string arg)
        {
            try
            {
                using var http = _httpFactory.CreateClient();
                var obj = await GetLocationDataAsync(arg);
                var culture = GetLocalCulture(obj.Results[0].AddressComponents);

                var currentSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var timeResult = await http
                    .GetStringAsync(
                        $"https://maps.googleapis.com/maps/api/timezone/json?location={obj.Results[0].Geometry.Location.Lat},{obj.Results[0].Geometry.Location.Lng}&timestamp={currentSeconds}&key={_config.GoogleApi}")
                    .ConfigureAwait(false);
                var timeObj = timeResult.Deserialize<TimeZoneResult>();

                var timeData = new TimeData
                {
                    Address = obj.Results[0].FormattedAddress,
                    Time = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, timeObj.TimeZoneId),
                    TimeZoneName = timeObj.TimeZoneName,
                    Culture = culture
                };
                
                return timeData;
            }
            catch (Exception e)
            {
                Logger.Warn(e);
                return null;
            }
        }

        private CultureInfo GetLocalCulture(IEnumerable<AddressComponent> components)
        {
            var country = components.FirstOrDefault(c => c.Types.Contains("country", StringComparer.Ordinal));
            if (country == null)
            {
                return null;
            }

            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText("./data/countries.json"));
                var lang = json.RootElement.GetProperty(country.ShortName).GetProperty("languages")[0].GetString();
                return new CultureInfo(lang);
            }
            catch (Exception)
            {
                Logger.Warn("Could not find culture for {culture}", country.LongName);
                return null;
            }
        }

        public async Task<TimeZoneResult> GetLocalDateTime(float lat, float lng)
        {
            using var http = _httpFactory.CreateClient();
            var currentSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeResult = await http
                .GetStringAsync(
                    $"https://maps.googleapis.com/maps/api/timezone/json?location={lat},{lng}&timestamp={currentSeconds}&key={_config.GoogleApi}")
                .ConfigureAwait(false);
            return timeResult.Deserialize<TimeZoneResult>();
        }

        public async Task<GeolocationResult> GetLocationDataAsync(string location)
        {
            try
            {
                using var http = _httpFactory.CreateClient();
                var result = await http.GetStringAsync($"https://maps.googleapis.com/maps/api/geocode/json?address={location}&key={_config.GoogleApi}")
                    .ConfigureAwait(false);
                var geo = result.Deserialize<GeolocationResult>();
                if (geo?.Results == null || geo.Results.Length == 0)
                {
                    Logger.Warn("Geocode lookup failed for {location}", location);
                    return null;
                }

                return geo;
            }
            catch (Exception e)
            {
                Logger.Warn(e);
                return null;
            }
        }

        public async Task<OmdbMovie> GetMovieDataAsync(string name)
        {
            name = HttpUtility.UrlEncode(name.ToLowerInvariant());
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync($"http://www.omdbapi.com/?t={name}&apikey={_config.OmdbApi}&y=&plot=full&r=json").ConfigureAwait(false);
            var movie = result.Deserialize<OmdbMovie>();
            return movie?.Title == null ? null : movie;
        }

        public async Task<string> GetRandomCatAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://aws.random.cat/meow").ConfigureAwait(false);
            using var cat = JsonDocument.Parse(result);
            using var client = new WebClient();
            var uri = new Uri(cat.RootElement.GetProperty("file").GetString());
            var path = "./temp/" + Path.GetFileName(uri.LocalPath);
            await client.DownloadFileTaskAsync(uri, path).ConfigureAwait(false);
            return path;
        }
        
        public async Task<string> GetRandomDogAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://random.dog/woof.json").ConfigureAwait(false);
            using var dog = JsonDocument.Parse(result);
            using var client = new WebClient();
            var uri = new Uri(dog.RootElement.GetProperty("url").GetString());
            var path = "./temp/" + Path.GetFileName(uri.LocalPath);
            await client.DownloadFileTaskAsync(uri, path).ConfigureAwait(false);
            return path;
        }

        public async Task<string> GetCatFactAsync()
        {
            using var http = _httpFactory.CreateClient();
            var result = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
            using var fact = JsonDocument.Parse(result);

            return fact.RootElement.GetProperty("fact").GetString();
        }
    }
}