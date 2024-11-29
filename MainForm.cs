using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics.Eventing.Reader;
using System.Net.Http;
using System.Text.Json;
using System.IO;

namespace WindowsEventViewerAnalyzer
{
    public partial class MainForm : Form
    {
        private readonly ListView _eventListView;
        private readonly Button _refreshButton;
        private readonly ComboBox _logNameComboBox;
        private readonly TextBox _solutionTextBox;
        private readonly EventLogReader _eventLogReader;
        private readonly GptSolutionProvider _gptSolutionProvider;
        private List<(EventLogReader.RankedEvent Event, string Solution, int Rank)>? _rankedSolutions;

        public MainForm()
        {
            _eventLogReader = new EventLogReader();
            var apiKey = LoadApiKey();
            _gptSolutionProvider = new GptSolutionProvider(apiKey);
            _eventListView = new ListView();
            _refreshButton = new Button();
            _logNameComboBox = new ComboBox();
            _solutionTextBox = new TextBox();

            this.WindowState = FormWindowState.Maximized;
            InitializeComponent();

            _logNameComboBox.Items.AddRange(new object[] { "Application", "System", "Security" });
            _logNameComboBox.SelectedIndex = 0;
        }

        private void InitializeComponent()
        {
            this.Text = "Windows Event Viewer Analyzer";
            this.MinimumSize = new Size(800, 600);  // Add minimum size
            this.BackColor = Color.FromArgb(245, 246, 247);
            this.Font = new Font("Segoe UI", 9F);
            this.Padding = new Padding(15);
            this.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(15, 5, 15, 5)
            };

            _logNameComboBox.Location = new Point(15, 5);
            _logNameComboBox.Width = 200;
            _logNameComboBox.Height = 30;
            _logNameComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _logNameComboBox.Font = new Font("Segoe UI", 10F);
            _logNameComboBox.BackColor = Color.White;
            _logNameComboBox.FlatStyle = FlatStyle.Flat;

            _refreshButton.Text = "Refresh";
            _refreshButton.Location = new Point(225, 5);
            _refreshButton.Width = 100;
            _refreshButton.Height = 30;
            _refreshButton.FlatStyle = FlatStyle.Flat;
            _refreshButton.BackColor = Color.FromArgb(0, 120, 212);
            _refreshButton.ForeColor = Color.White;
            _refreshButton.Font = new Font("Segoe UI", 10F);
            _refreshButton.Cursor = Cursors.Hand;

            topPanel.Controls.Add(_logNameComboBox);
            topPanel.Controls.Add(_refreshButton);

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(245, 246, 247),
                SplitterWidth = 5,
                SplitterDistance = (int)(this.ClientSize.Height * 0.7)  // 70% for event list
            };

            _eventListView.Dock = DockStyle.Fill;
            _eventListView.View = View.Details;
            _eventListView.FullRowSelect = true;
            _eventListView.GridLines = false;
            _eventListView.Font = new Font("Segoe UI", 9F);
            _eventListView.BackColor = Color.White;
            _eventListView.BorderStyle = BorderStyle.None;

            _solutionTextBox.Dock = DockStyle.Fill;
            _solutionTextBox.Multiline = true;
            _solutionTextBox.ScrollBars = ScrollBars.Vertical;
            _solutionTextBox.ReadOnly = true;
            _solutionTextBox.Font = new Font("Segoe UI", 11F);
            _solutionTextBox.BackColor = Color.FromArgb(245, 247, 250);
            _solutionTextBox.ForeColor = Color.FromArgb(28, 41, 56);
            _solutionTextBox.BorderStyle = BorderStyle.Fixed3D;
            _solutionTextBox.Padding = new Padding(20);
            _solutionTextBox.WordWrap = true;

            splitContainer.Panel1.Controls.Add(_eventListView);
            splitContainer.Panel2.Controls.Add(_solutionTextBox);

            _eventListView.Columns.Add("Severity", 100);
            _eventListView.Columns.Add("Event ID", 80);
            _eventListView.Columns.Add("Occurrences", 90);
            _eventListView.Columns.Add("Last Occurrence", 150);
            _eventListView.Columns.Add("Description", -2);  // -2 means fill remaining space

            this.Controls.Add(topPanel);
            this.Controls.Add(splitContainer);

            _refreshButton.Click += RefreshButton_Click;
            _eventListView.SelectedIndexChanged += EventListView_SelectedIndexChanged;

            _refreshButton.MouseEnter += (s, e) => {
                _refreshButton.BackColor = Color.FromArgb(0, 100, 190);
            };
            _refreshButton.MouseLeave += (s, e) => {
                _refreshButton.BackColor = Color.FromArgb(0, 120, 212);
            };

            splitContainer.Panel1.Padding = new Padding(15, 15, 15, 5);
            splitContainer.Panel2.Padding = new Padding(15, 5, 15, 5);
        }

        private void EventListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_eventListView?.SelectedItems?.Count > 0)
            {
                var selectedItem = _eventListView.SelectedItems[0];
                if (selectedItem?.Tag is EventLogReader.RankedEvent rankedEvent)
                {
                    _solutionTextBox.Text = "Loading solution...";

                    if (_rankedSolutions != null)
                    {
                        var solutionInfo = _rankedSolutions.FirstOrDefault(rs => rs.Event.EventId == rankedEvent.EventId && rs.Event.Type == rankedEvent.Type);

                        if (solutionInfo != default)
                        {
                            var formattedSolution = new System.Text.StringBuilder();
                            
                            formattedSolution.AppendLine($"Solution Rank: {solutionInfo.Rank}");
                            formattedSolution.AppendLine();
                            formattedSolution.AppendLine("Step-by-step solution:");
                            formattedSolution.AppendLine();

                            var lines = solutionInfo.Solution.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                var trimmedLine = line.Trim();
                                if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"^\d+\."))
                                {
                                    formattedSolution.AppendLine();
                                    formattedSolution.AppendLine(trimmedLine);
                                    formattedSolution.AppendLine("----------------------------------------");
                                    formattedSolution.AppendLine();
                                }
                                else if (!string.IsNullOrWhiteSpace(trimmedLine))
                                {
                                    formattedSolution.AppendLine($"    {trimmedLine}");  // Indent non-numbered lines
                                }
                            }

                            _solutionTextBox.Text = formattedSolution.ToString();
                        }
                    }
                }
            }
        }

        private async void RefreshButton_Click(object? sender, EventArgs e)
        {
            try
            {
                string? selectedLogName = _logNameComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedLogName))
                {
                    MessageBox.Show("Please select a log type.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var events = _eventLogReader.FetchEventLogs(selectedLogName);
                var rankedEvents = _eventLogReader.GetRankedEvents(events, selectedLogName);

                DisplayRankedEvents(rankedEvents);

                try
                {
                    await GenerateRankedSolutions(rankedEvents);
                }
                catch (Exception apiEx)
                {
                    MessageBox.Show($"Error connecting to OpenAI API: {apiEx.Message}\nEvent logs have been fetched, but AI-generated solutions are not available.", 
                                  "API Connection Error", 
                                  MessageBoxButtons.OK, 
                                  MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while refreshing events: {ex.Message}", 
                               "Error", 
                               MessageBoxButtons.OK, 
                               MessageBoxIcon.Error);
            }
        }

        private void DisplayRankedEvents(List<EventLogReader.RankedEvent> rankedEvents)
        {
            _eventListView.Items.Clear();

            foreach (var rankedEvent in rankedEvents)
            {
                var item = new ListViewItem(rankedEvent.Type.ToString());
                item.SubItems.Add(rankedEvent.EventId.ToString());
                item.SubItems.Add(rankedEvent.Occurrences.ToString());
                item.SubItems.Add(rankedEvent.MostRecentOccurrence.TimeCreated?.ToString() ?? "N/A");
                
                try
                {
                    item.SubItems.Add(rankedEvent.MostRecentOccurrence.FormatDescription());
                }
                catch (EventLogException ex)
                {
                    item.SubItems.Add($"Error reading event description: {ex.Message}");
                }
                
                item.Tag = rankedEvent;

                switch (rankedEvent.Type)
                {
                    case EventLogReader.EventType.Critical:
                        item.BackColor = Color.FromArgb(255, 235, 235);  // Light red
                        item.ForeColor = Color.FromArgb(122, 0, 0);      // Dark red text
                        break;
                    case EventLogReader.EventType.Error:
                        item.BackColor = Color.FromArgb(255, 243, 235);  // Light orange
                        item.ForeColor = Color.FromArgb(122, 61, 0);     // Dark orange text
                        break;
                    case EventLogReader.EventType.Warning:
                        item.BackColor = Color.FromArgb(255, 252, 235);  // Light yellow
                        item.ForeColor = Color.FromArgb(122, 107, 0);    // Dark yellow text
                        break;
                }

                _eventListView.Items.Add(item);
            }
        }

        private async Task GenerateRankedSolutions(List<EventLogReader.RankedEvent> rankedEvents)
        {
            if (!CheckInternetConnection())
            {
                MessageBox.Show("No internet connection detected. AI-generated solutions are not available.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _rankedSolutions = new List<(EventLogReader.RankedEvent, string, int)>();

            foreach (var rankedEvent in rankedEvents)
            {
                try
                {
                    var (solution, rank) = await _gptSolutionProvider.GetRankedSolutionSuggestion(rankedEvent);
                    _rankedSolutions.Add((rankedEvent, solution, rank));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating solution for event {rankedEvent.EventId}: {ex.Message}", "API Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            _rankedSolutions = _rankedSolutions.OrderBy(rs => rs.Rank).ToList();
        }

        private bool CheckInternetConnection()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    using (var response = client.GetAsync("http://clients3.google.com/generate_204").Result)
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private string LoadApiKey()
        {
            try
            {
                var json = File.ReadAllText("appsettings.json");
                var config = JsonSerializer.Deserialize<JsonElement>(json);
                return config.GetProperty("OpenAI").GetProperty("ApiKey").GetString();
            }
            catch
            {
                MessageBox.Show("Failed to load API key from appsettings.json", "Configuration Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }
        }
    }
}
