using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Text.RegularExpressions;

namespace WindowsEventViewerAnalyzer
{
    public class GptSolutionProvider
    {
        private readonly OpenAIAPI _api;

        public GptSolutionProvider(string apiKey)
        {
            _api = new OpenAIAPI(apiKey);
        }

        public async Task<(string Solution, int Rank)> GetRankedSolutionSuggestion(EventLogReader.RankedEvent rankedEvent)
        {
            string prompt = $"Provide a step-by-step solution for the following Windows event log error:\nEvent ID: {rankedEvent.EventId}\nType: {rankedEvent.Type}\nDescription: {rankedEvent.MostRecentOccurrence.FormatDescription()}\n\nStep-by-step solution:";

            var chat = _api.Chat.CreateConversation();
            chat.AppendSystemMessage("You are a helpful assistant that provides step-by-step solutions for Windows event log errors.");
            chat.AppendUserInput(prompt);

            string response = await chat.GetResponseFromChatbotAsync();

            string formattedSolution = FormatStepByStepSolution(response.Trim());
            int rank = CalculateSolutionRank(rankedEvent);

            return (formattedSolution, rank);
        }

        private string FormatStepByStepSolution(string rawSolution)
        {
            var steps = rawSolution.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var formattedSolution = "Step-by-step solution:\n\n";

            for (int i = 0; i < steps.Length; i++)
            {
                var step = steps[i].Trim();
                if (!string.IsNullOrEmpty(step))
                {
                    // Remove any existing numbering
                    step = Regex.Replace(step, @"^\d+\.\s*", "");
                    formattedSolution += $"{i + 1}. {step}\n\n";  // Add an extra newline after each step
                }
            }

            return formattedSolution;
        }

        private int CalculateSolutionRank(EventLogReader.RankedEvent rankedEvent)
        {
            // Lower rank means higher priority
            int criticalityScore = (int)rankedEvent.Type;
            int relevanceScore = Math.Min(10, rankedEvent.Occurrences); // Cap at 10 to prevent overly high scores

            // Combine criticality and relevance scores
            // Criticality is weighted more heavily than relevance
            return criticalityScore * 10 + (10 - relevanceScore);
        }
    }
}
