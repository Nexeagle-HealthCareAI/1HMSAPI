using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "gsk_dummy");
        
        var requestMap = new Dictionary<string, string> { { "chiefComplaint", "headache" } };
        var jsonInput = JsonSerializer.Serialize(requestMap);
        var prompt = $"Translate the values of the following JSON object to Hindi. Keep the keys exactly the same. Only output valid JSON and nothing else. Do not wrap in markdown.\n\n{jsonInput}";

        var requestBody = new
        {
            model = "llama-3.3-70b-versatile",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.1,
            response_format = new { type = "json_object" }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("chat/completions", content);
        Console.WriteLine(await response.Content.ReadAsStringAsync());
    }
}
