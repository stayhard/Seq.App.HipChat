using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.HipChat
{
    [SeqApp("HipChat",
    Description = "Sends log events to HipChat.")]
    public class HipChatReactor : Reactor, ISubscribeTo<LogEventData>
    {
        static Regex placeholdersRx = new Regex("(\\[(?<key>[^\\[\\]]+?)(\\:(?<format>[^\\[\\]]+?))?\\])", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static IDictionary<LogEventLevel, string> _levelColorMap = new Dictionary<LogEventLevel, string>
        {
            {LogEventLevel.Verbose, "gray"},
            {LogEventLevel.Debug, "gray"},
            {LogEventLevel.Information, "green"},
            {LogEventLevel.Warning, "yellow"},
            {LogEventLevel.Error, "red"},
            {LogEventLevel.Fatal, "red"},
        };

        [SeqAppSetting(
        DisplayName = "Seq Base URL",
        HelpText = "Used for generating perma links to events in HipChat messages.",
        IsOptional = true)]
        public string BaseUrl { get; set; }

        [SeqAppSetting(
        HelpText = "Admin or notification token (get it from HipChat.com admin).")]
        public string Token { get; set; }

        [SeqAppSetting(
        DisplayName = "Room",
        HelpText = "ID or name of the room to post messages to.")]
        public string RoomId { get; set; }

        [SeqAppSetting(
        HelpText = "Background color for message. One of \"yellow\", \"red\", \"green\", \"purple\", \"gray\", or \"random\". (default: auto based on message level)",
        IsOptional = true)]
        public string Color { get; set; }

        [SeqAppSetting(
        HelpText = "The message template to use when writing the message to HipChat. Can consist of any standard HTML. Event property values can be added in the format [PropertyKey]. (default: <strong>[Level]:</strong> [RenderedMessage])",
        IsOptional = true)]
        public string MessageTemplate { get; set; }

        [SeqAppSetting(
        HelpText = "Whether or not messages should trigger notifications for people in the room (change the tab color, play a sound, etc). Each recipient's notification preferences are taken into account.",
        IsOptional = true)]
        public bool Notify { get; set; }

        public async void On(Event<LogEventData> evt)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://api.hipchat.com/v2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var msg = new StringBuilder();
                AddMessage(evt, msg);
                if (msg.Length > 1000)
                {
                    msg.Length = 1000;
                }

                if (!string.IsNullOrWhiteSpace(BaseUrl))
                {
                    msg.AppendLine();
                    msg.AppendLine(GenerateSeqUrl(evt));
                }

                var color = Color;
                if (string.IsNullOrWhiteSpace(color))
                {
                    color = _levelColorMap[evt.Data.Level];
                }

                var body = new
                {
                    color = color,
                    message = msg.ToString(),
                    notify = Notify
                };

                var response = await client.PostAsJsonAsync(
                    string.Format("room/{0}/notification?auth_token={1}", RoomId, Token),
                    body);

                if (!response.IsSuccessStatusCode)
                {
                    Log
                        .ForContext("Uri", response.RequestMessage.RequestUri)
                        .Error("Could not send HipChat message, server replied {StatusCode} {StatusMessage}: {Message}", Convert.ToInt32(response.StatusCode), response.StatusCode, await response.Content.ReadAsStringAsync());
                }
            }
        }

        private void AddMessage(Event<LogEventData> evt, StringBuilder msg)
        {
            var messageTemplateToUse = MessageTemplate;

            if (string.IsNullOrWhiteSpace(MessageTemplate))
            {
                MessageTemplate = "<strong>[Level]:</strong> [RenderedMessage]";
            }

            msg.AppendFormat(SubstitutePlaceholders(messageTemplateToUse, evt));
        }

        private string SubstitutePlaceholders(string messageTemplateToUse, Event<LogEventData> evt)
        {
            var data = evt.Data;
            var eventType = evt.EventType;
            var level = data.Level;

            var placeholders = data.Properties.ToDictionary(k => k.Key.ToLower(), v => v.Value);

            AddValueIfKeyDoesntExist(placeholders, "Level", level);
            AddValueIfKeyDoesntExist(placeholders, "EventType", eventType);
            AddValueIfKeyDoesntExist(placeholders, "RenderedMessage", data.RenderedMessage);

            return placeholdersRx.Replace(messageTemplateToUse, delegate(Match m)
            {
                var key = m.Groups["key"].Value.ToLower();
                var format = m.Groups["format"].Value;
                return placeholders.ContainsKey(key) ? FormatValue(placeholders[key], format) : m.Value;
            });
        }

        private string FormatValue(object value, string format)
        {
            var rawValue = value != null ? value.ToString() : "(Null)";

            if (string.IsNullOrWhiteSpace(format))
            {
                return rawValue;
            }

            try
            {
                return string.Format(format, rawValue);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not format HipChat message: {value} {format}", value, format);
            }

            return rawValue;
        }

        private static void AddValueIfKeyDoesntExist(Dictionary<string, object> placeholders, string key, object value)
        {
            var loweredKey = key.ToLower();
            if (!placeholders.ContainsKey(loweredKey))
            {
                placeholders.Add(loweredKey, value);
            }
        }

        private string GenerateSeqUrl(Event<LogEventData> evt)
        {
            return string.Format("<a href=\"{0}/#/events?filter=@Id%20%3D%3D%20%22{1}%22&show=expanded\">Click here to open in Seq</a>", BaseUrl, evt.Id);
        }
    }
}
