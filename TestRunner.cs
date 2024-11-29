using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WindowsEventViewerAnalyzer
{
    public class TestRunner
    {
        private EventLogReader _eventLogReader;
        private GptSolutionProvider _gptSolutionProvider;

        public TestRunner(string apiKey)
        {
            _eventLogReader = new EventLogReader();
            _gptSolutionProvider = new GptSolutionProvider(apiKey);
        }

        public async Task RunTests()
        {
            await TestApplicationLogs();
            await TestSystemLogs();
            await TestSecurityLogs();
            await TestMixedLogs();
            await TestLargeDataset();
            TestErrorHandling();
        }

        private async Task TestApplicationLogs()
        {
            Console.WriteLine("Testing Application Logs...");
            await TestLogType("Application");
        }

        private async Task TestSystemLogs()
        {
            Console.WriteLine("Testing System Logs...");
            await TestLogType("System");
        }

        private async Task TestSecurityLogs()
        {
            Console.WriteLine("Testing Security Logs...");
            await TestLogType("Security");
        }

        private async Task TestMixedLogs()
        {
            Console.WriteLine("Testing Mixed Logs...");
            var logs = new List<EventLogReader.RankedEvent>();
            logs.AddRange(_eventLogReader.GetRankedEvents(_eventLogReader.FetchEventLogs("Application", 33), "Application"));
            logs.AddRange(_eventLogReader.GetRankedEvents(_eventLogReader.FetchEventLogs("System", 33), "System"));
            logs.AddRange(_eventLogReader.GetRankedEvents(_eventLogReader.FetchEventLogs("Security", 34), "Security"));

            await TestRankedEvents(logs);
        }

        private async Task TestLargeDataset()
        {
            Console.WriteLine("Testing Large Dataset...");
            await TestLogType("Application", 1000);
        }

        private void TestErrorHandling()
        {
            Console.WriteLine("Testing Error Handling...");
            try
            {
                _eventLogReader.FetchEventLogs("InvalidLogName");
                Console.WriteLine("Error: Invalid log name not caught");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Successfully caught error: {ex.Message}");
            }
        }

        private async Task TestLogType(string logName, int maxEvents = 100)
        {
            var stopwatch = Stopwatch.StartNew();
            var logs = _eventLogReader.FetchEventLogs(logName, maxEvents);
            stopwatch.Stop();
            Console.WriteLine($"Fetched {logs.Count} logs in {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Restart();
            var rankedEvents = _eventLogReader.GetRankedEvents(logs, logName);
            stopwatch.Stop();
            Console.WriteLine($"Ranked {rankedEvents.Count} events in {stopwatch.ElapsedMilliseconds}ms");

            await TestRankedEvents(rankedEvents);
        }

        private async Task TestRankedEvents(List<EventLogReader.RankedEvent> rankedEvents)
        {
            for (int i = 0; i < Math.Min(5, rankedEvents.Count); i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var (solution, rank) = await _gptSolutionProvider.GetRankedSolutionSuggestion(rankedEvents[i]);
                stopwatch.Stop();
                Console.WriteLine($"Generated solution for event {rankedEvents[i].EventId} (rank {rank}) in {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}
