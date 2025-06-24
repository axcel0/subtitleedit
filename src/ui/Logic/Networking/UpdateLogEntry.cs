using System;

namespace Nikse.SubtitleEdit.Logic.Networking
{
    public sealed class UpdateLogEntry
    {
        public int Id { get; }
        public string UserName { get; }
        public int Index { get; set; } // Mutable for index adjustments
        public DateTime OccurredAt { get; }
        public string Action { get; }

        public UpdateLogEntry(int id, string userName, int index, string action)
        {
            Id = id;
            UserName = userName ?? throw new ArgumentNullException(nameof(userName));
            Index = index;
            OccurredAt = DateTime.Now;
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override string ToString() => 
            $"{OccurredAt:HH:mm:ss} {UserName}: {Action}";
    }
}
