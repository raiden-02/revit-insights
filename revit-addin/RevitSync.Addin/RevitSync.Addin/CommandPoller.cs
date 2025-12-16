using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Timers;

namespace RevitSync.Addin
{
    public class CommandPoller : IDisposable
    {
        private readonly Timer _timer;
        private readonly HttpClient _http;
        private readonly Action<GeometryCommandDto> _onCommand;

        public CommandPoller(Action<GeometryCommandDto> onCommand)
        {
            _onCommand = onCommand;

            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5245/"),
                Timeout = TimeSpan.FromSeconds(2)
            };

            _timer = new Timer(1500);
            _timer.AutoReset = true;
            _timer.Elapsed += (sender, e) => Tick();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private void Tick()
        {
            try
            {
                // Prefer project-specific dequeue if we know it, else "any"
                var project = AppState.ActiveProjectName;
                string url = string.IsNullOrWhiteSpace(project)
                    ? "api/commands/next"
                    : $"api/commands/next?projectName={Uri.EscapeDataString(project)}";

                var resp = _http.GetAsync(url).Result;

                // 204 NoContent means nothing queued
                if ((int)resp.StatusCode == 204) return;
                if (!resp.IsSuccessStatusCode) return;

                var json = resp.Content.ReadAsStringAsync().Result;
                if (string.IsNullOrWhiteSpace(json)) return;

                var cmd = JsonConvert.DeserializeObject<GeometryCommandDto>(json);
                if (cmd == null) return;

                _onCommand(cmd);
            }
            catch
            {
                // swallow transient errors
            }
        }

        public void Dispose()
        {
            try { _timer?.Stop(); } catch { }
            try { _timer?.Dispose(); } catch { }
            try { _http?.Dispose(); } catch { }
        }
    }
}

