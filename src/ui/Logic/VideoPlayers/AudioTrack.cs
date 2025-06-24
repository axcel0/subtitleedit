using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Globalization;

namespace Nikse.SubtitleEdit.Logic.VideoPlayers
{
    public sealed class AudioTrack : IEquatable<AudioTrack>
    {
        public int TrackNumber { get; }
        public string Name { get; }
        public int Index { get; }

        public AudioTrack(int trackNumber, string name, int index)
        {
            TrackNumber = trackNumber;
            Name = name ?? string.Empty;
            Index = index;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Name))
            {
                return TrackNumber.ToString(CultureInfo.InvariantCulture);
            }

            var result = $"{TrackNumber}: {Name.CapitalizeFirstLetter()}";
            return result.TrimEnd(':', ' ');
        }

        public bool Equals(AudioTrack other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return TrackNumber == other.TrackNumber && 
                   Name == other.Name && 
                   Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AudioTrack);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TrackNumber, Name, Index);
        }

        public static bool operator ==(AudioTrack left, AudioTrack right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AudioTrack left, AudioTrack right)
        {
            return !Equals(left, right);
        }
    }
}
