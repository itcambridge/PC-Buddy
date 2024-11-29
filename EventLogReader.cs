using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;

namespace WindowsEventViewerAnalyzer
{
    public class EventLogReader
    {
        public enum EventType
        {
            Critical = 1,
            Error = 2,
            Warning = 3,
            Other = 4
        }

        public class GroupedEvent
        {
            public EventType Type { get; set; }
            public List<EventRecord> Events { get; set; }
            public int Frequency => Events.Count;
        }

        public class RankedEvent
        {
            public int EventId { get; set; }
            public EventType Type { get; set; }
            public int Occurrences { get; set; }
            public EventRecord MostRecentOccurrence { get; set; }
        }

        public List<EventRecord> FetchEventLogs(string logName, int maxEvents = 100, bool filterSuccessfulEvents = true)
        {
            var eventLogs = new List<EventRecord>();

            try
            {
                var query = new EventLogQuery(logName, PathType.LogName)
                {
                    ReverseDirection = true
                };

                using (var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query))
                {
                    for (int i = 0; i < maxEvents; i++)
                    {
                        EventRecord eventInstance = reader.ReadEvent();
                        if (eventInstance == null)
                            break;

                        if (!filterSuccessfulEvents || ShouldIncludeEvent(eventInstance, logName))
                        {
                            eventLogs.Add(eventInstance);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching event logs: {ex.Message}");
            }

            return eventLogs;
        }

        private bool ShouldIncludeEvent(EventRecord eventRecord, string logName)
        {
            if (eventRecord.Level == null && !eventRecord.Keywords.HasValue)
                return false;

            // Special handling for Security logs
            if (logName.Equals("Security", StringComparison.OrdinalIgnoreCase))
            {
                // Include Audit Failure events
                if (eventRecord.Keywords.HasValue && 
                    ((StandardEventKeywords)eventRecord.Keywords.Value).HasFlag(StandardEventKeywords.AuditFailure))
                {
                    return true;
                }
            }

            // For other logs or if not an Audit Failure in Security log
            if (eventRecord.Level.HasValue)
            {
                var level = (StandardEventLevel)eventRecord.Level.Value;
                return level == StandardEventLevel.Critical ||
                       level == StandardEventLevel.Error ||
                       level == StandardEventLevel.Warning;
            }

            return false;
        }

        private EventType ClassifyEvent(EventRecord eventRecord, string logName)
        {
            if (logName.Equals("Security", StringComparison.OrdinalIgnoreCase) &&
                eventRecord.Keywords.HasValue &&
                ((StandardEventKeywords)eventRecord.Keywords.Value).HasFlag(StandardEventKeywords.AuditFailure))
            {
                return EventType.Error; // Categorize Audit Failures as Errors
            }

            if (eventRecord.Level.HasValue)
            {
                var level = (StandardEventLevel)eventRecord.Level.Value;
                return level switch
                {
                    StandardEventLevel.Critical => EventType.Critical,
                    StandardEventLevel.Error => EventType.Error,
                    StandardEventLevel.Warning => EventType.Warning,
                    _ => EventType.Other
                };
            }
            return EventType.Other;
        }

        public Dictionary<EventType, GroupedEvent> ClassifyAndGroupEvents(List<EventRecord> events, string logName)
        {
            var groupedEvents = new Dictionary<EventType, GroupedEvent>();

            foreach (var eventType in Enum.GetValues(typeof(EventType)))
            {
                groupedEvents[(EventType)eventType] = new GroupedEvent
                {
                    Type = (EventType)eventType,
                    Events = new List<EventRecord>()
                };
            }

            foreach (var eventRecord in events)
            {
                var eventType = ClassifyEvent(eventRecord, logName);
                groupedEvents[eventType].Events.Add(eventRecord);
            }

            return groupedEvents;
        }

        public List<GroupedEvent> SortEventsByFrequencyAndSeverity(Dictionary<EventType, GroupedEvent> groupedEvents)
        {
            return groupedEvents.Values
                .OrderBy(ge => (int)ge.Type)
                .ThenByDescending(ge => ge.Frequency)
                .ToList();
        }

        public List<EventRecord> SortEventsBySeverity(List<EventRecord> events, string logName)
        {
            return events.OrderBy(e => ClassifyEvent(e, logName)).ToList();
        }

        public Dictionary<int, List<EventRecord>> GroupEventsByFrequency(List<EventRecord> events)
        {
            return events
                .GroupBy(e => e.Id)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(e => e.TimeCreated).ToList()
                )
                .OrderByDescending(kvp => kvp.Value.Count)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public List<RankedEvent> GetRankedEvents(List<EventRecord> events, string logName)
        {
            var groupedEvents = ClassifyAndGroupEvents(events, logName);
            var eventsByFrequency = GroupEventsByFrequency(events);

            var rankedEvents = new List<RankedEvent>();

            foreach (var group in groupedEvents)
            {
                foreach (var eventId in eventsByFrequency.Keys)
                {
                    var eventsOfType = group.Value.Events.Where(e => e.Id == eventId).ToList();
                    if (eventsOfType.Any())
                    {
                        rankedEvents.Add(new RankedEvent
                        {
                            EventId = eventId,
                            Type = group.Key,
                            Occurrences = eventsOfType.Count,
                            MostRecentOccurrence = eventsOfType.OrderByDescending(e => e.TimeCreated).First()
                        });
                    }
                }
            }

            return rankedEvents
                .OrderBy(re => (int)re.Type)
                .ThenByDescending(re => re.Occurrences)
                .ToList();
        }
    }
}
