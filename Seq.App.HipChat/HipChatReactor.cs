namespace Seq.App.HipChat
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using Apps;
    using Apps.LogEvents;

    [SeqApp("HipChat",
    Description = "Sends log events to HipChat.")]
    public class HipChatReactor : Reactor, ISubscribeTo<LogEventData>
    {
        private const string DefaultHipChatBaseUrl = "https://api.hipchat.com/v2/";
        
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
        DisplayName = "HipChat Base URL",
        HelpText = "Default will be: " + DefaultHipChatBaseUrl,
        IsOptional = true)]
        public string HipChatBaseUrl { get; set; }

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
        HelpText = "Whether or not messages should trigger notifications for people in the room (change the tab color, play a sound, etc). Each recipient's notification preferences are taken into account.",
        IsOptional = true)]
        public bool Notify { get; set; }

        public async void On(Event<LogEventData> evt)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(GenerateHipChatBaseUrl());
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var msg = new StringBuilder("<strong>" + evt.Data.Level + ":</strong> " + evt.Data.RenderedMessage);
                if (msg.Length > 1000)
                {
                    msg.Length = 1000;
                }

                if (!string.IsNullOrWhiteSpace(BaseUrl))
                {
                    msg.AppendLine();
                    msg.AppendLine(
                        string.Format(
                            "<a href=\"{0}/#/events?filter=@Id%20%3D%3D%20%22{1}%22&show=expanded\">Click here to open in Seq</a>",
                            BaseUrl, evt.Id));
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

        private string GenerateHipChatBaseUrl()
        {
            return string.IsNullOrWhiteSpace(HipChatBaseUrl)
                    ? DefaultHipChatBaseUrl
                    : HipChatBaseUrl;
        }
    }
}
