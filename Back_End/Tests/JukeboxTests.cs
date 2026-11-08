using Back_End.Services;
using Xunit;
using Cmd = Back_End.Services.JukeboxService.Command;

namespace Back_End.Tests
{
    public class JukeboxTests
    {
        private long now = 100_000;

        private JukeboxService NewJukebox() => new(new PlayerConnectionService(), () => now);

        private void Wait() => now += JukeboxService.CommandCooldownMs;

        [Fact]
        public void PlayStop_TogglesAndKeepsTrack()
        {
            var jb = NewJukebox();
            Assert.False(jb.Playing);

            Assert.True(jb.Handle(Cmd.PlayStop));
            Assert.True(jb.Playing);
            Assert.Equal(0, jb.Index);

            now += 300;
            Assert.False(jb.Handle(Cmd.PlayStop));   // bấm liên tục bị chặn

            Wait();
            jb.Handle(Cmd.Next);
            Wait();
            Assert.True(jb.Handle(Cmd.PlayStop));
            Assert.False(jb.Playing);
            Assert.Equal(1, jb.Index);   // dừng vẫn nhớ bài

            Wait();
            jb.Handle(Cmd.PlayStop);
            Assert.True(jb.Playing);
            Assert.Equal(1, jb.Index);
        }

        [Fact]
        public void NextPrev_MoveIndexAndStartPlaying()
        {
            var jb = NewJukebox();
            Assert.True(jb.Handle(Cmd.Prev));   // đang dừng bấm lùi cũng bật nhạc
            Assert.True(jb.Playing);
            Assert.Equal(-1, jb.Index);   // client lấy theo vòng -> bài cuối

            Wait();
            jb.Handle(Cmd.Next);
            Wait();
            jb.Handle(Cmd.Next);
            Assert.Equal(1, jb.Index);
        }

        [Fact]
        public void Ended_OnlyFirstMatchingReportAdvances()
        {
            var jb = NewJukebox();
            Assert.False(jb.Handle(Cmd.Ended, 0));   // đang dừng
            jb.Handle(Cmd.PlayStop);

            now += 1000;
            Assert.False(jb.Handle(Cmd.Ended, 0));   // báo hết quá sớm

            now += JukeboxService.MinPlayBeforeEndMs;
            Assert.False(jb.Handle(Cmd.Ended, 5));   // không phải bài đang phát
            Assert.True(jb.Handle(Cmd.Ended, 0));
            Assert.False(jb.Handle(Cmd.Ended, 0));   // client thứ hai báo trùng
            Assert.Equal(1, jb.Index);
        }

        [Fact]
        public void Volume_ClampsBetweenZeroAndMax()
        {
            var jb = NewJukebox();
            Assert.Equal(JukeboxService.DefaultVolume, jb.Volume);

            for (int i = 0; i < 20; i++) { now += JukeboxService.VolumeCooldownMs; jb.Handle(Cmd.VolumeUp); }
            Assert.Equal(JukeboxService.MaxVolume, jb.Volume);
            now += JukeboxService.VolumeCooldownMs;
            Assert.False(jb.Handle(Cmd.VolumeUp));

            for (int i = 0; i < 20; i++) { now += JukeboxService.VolumeCooldownMs; jb.Handle(Cmd.VolumeDown); }
            Assert.Equal(0, jb.Volume);
        }

        [Fact]
        public void StatePacket_HasPlayingIndexElapsedVolume()
        {
            var jb = NewJukebox();
            jb.Handle(Cmd.PlayStop);
            now += 4321;

            var r = new LiteNetLib.Utils.NetDataReader(jb.StatePacket());
            Assert.Equal(16, r.GetByte());
            Assert.True(r.GetBool());
            Assert.Equal(0, r.GetInt());
            Assert.Equal(4321L, r.GetLong());
            Assert.Equal(JukeboxService.DefaultVolume, r.GetByte());
        }
    }
}
