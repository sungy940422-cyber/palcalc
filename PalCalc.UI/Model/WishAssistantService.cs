using System;

namespace PalCalc.UI.Model
{
    public sealed record WishAssistantNotice(string Message, string Mood);

    public static class WishAssistantService
    {
        public static event Action<WishAssistantNotice> NoticePublished;

        public static void Publish(string message, string mood = "기본")
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            NoticePublished?.Invoke(new WishAssistantNotice(message, mood));
        }
    }
}
