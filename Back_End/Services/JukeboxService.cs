using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Back_End.Services
{
    /// <summary>
    /// Máy Jukebox chung của quán bar. Danh sách nhạc nằm ở client; server chỉ giữ số thứ tự bài
    /// (client lấy theo vòng: index mod số bài), đang phát hay dừng, âm lượng và thời điểm bắt đầu
    /// để mọi người nghe cùng một đoạn.
    /// Client -> Server, Packet Type 7: byte cmd (xem <see cref="Command"/>), cmd Ended kèm int index.
    /// Server -> Client, Packet Type 16: bool playing, int index, long elapsedMs, byte volume.
    /// </summary>
    public class JukeboxService
    {
        public enum Command : byte { Next = 0, Prev = 1, Ended = 2, PlayStop = 3, VolumeUp = 4, VolumeDown = 5 }

        public const long CommandCooldownMs = 1000;
        public const long VolumeCooldownMs = 150;
        // Báo "hết bài" quá sớm thì bỏ qua (client gửi bậy hoặc lệch)
        public const long MinPlayBeforeEndMs = 10000;
        public const byte MaxVolume = 10;
        public const byte DefaultVolume = 7;

        private readonly PlayerConnectionService connections;
        private readonly Func<long> clock;
        private readonly object gate = new();

        private bool playing;
        private int index;
        private byte volume = DefaultVolume;
        private long startedAtMs;
        private long lastCommandMs = long.MinValue / 2;
        private long lastVolumeMs = long.MinValue / 2;

        public JukeboxService(PlayerConnectionService connections)
            : this(connections, () => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency)
        {
        }

        public JukeboxService(PlayerConnectionService connections, Func<long> clock)
        {
            this.connections = connections;
            this.clock = clock;
        }

        public bool Playing { get { lock (gate) return playing; } }
        public int Index { get { lock (gate) return index; } }
        public byte Volume { get { lock (gate) return volume; } }

        /// <summary>Xử lý lệnh từ người chơi. Trả true nếu trạng thái đổi (đã broadcast).</summary>
        public bool Handle(Command cmd, int endedIndex = 0)
        {
            lock (gate)
            {
                long now = clock();
                switch (cmd)
                {
                    case Command.Next:
                    case Command.Prev:
                    case Command.PlayStop:
                        if (now - lastCommandMs < CommandCooldownMs) return false;
                        lastCommandMs = now;
                        if (cmd == Command.PlayStop && playing) playing = false;
                        else StartLocked(cmd == Command.Next ? index + 1 : cmd == Command.Prev ? index - 1 : index);
                        break;

                    case Command.Ended:
                        if (!playing || endedIndex != index || now - startedAtMs < MinPlayBeforeEndMs) return false;
                        StartLocked(index + 1);
                        break;

                    case Command.VolumeUp:
                    case Command.VolumeDown:
                        if (now - lastVolumeMs < VolumeCooldownMs) return false;
                        byte next = cmd == Command.VolumeUp ? (byte)Math.Min(volume + 1, MaxVolume) : (byte)Math.Max(volume - 1, 0);
                        if (next == volume) return false;
                        lastVolumeMs = now;
                        volume = next;
                        break;

                    default:
                        return false;
                }
            }
            Broadcast();
            return true;
        }

        public NetDataWriter StatePacket()
        {
            var writer = new NetDataWriter();
            writer.Put((byte)16);
            lock (gate)
            {
                writer.Put(playing);
                writer.Put(index);
                writer.Put(playing ? clock() - startedAtMs : 0L);
                writer.Put(volume);
            }
            return writer;
        }

        private void StartLocked(int newIndex)
        {
            playing = true;
            index = newIndex;
            startedAtMs = clock();
        }

        private void Broadcast()
        {
            var writer = StatePacket();
            foreach (var entry in connections.GetAll())
            {
                if (entry.Value.ConnectionState == ConnectionState.Connected)
                    entry.Value.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }
}
