using System;
using System.Windows.Forms;

namespace WindowsEventViewerAnalyzer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--test")
            {
                RunTests();
            }
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }

        static async void RunTests()
        {
            Console.WriteLine("Running tests...");
            try
            {
                var json = System.IO.File.ReadAllText("appsettings.json");
                var config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                var apiKey = config.GetProperty("OpenAI").GetProperty("ApiKey").GetString();
                
                var testRunner = new TestRunner(apiKey);
                await testRunner.RunTests();
                Console.WriteLine("Tests completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running tests: {ex.Message}");
            }
        }
    }
}