namespace Back_End.Models
{
    public class PlayerState
    {
        public int Id { get; }

        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public float Yaw { get; private set; }

        public PlayerState(int id)
        {
            Id = id;
        }

        public void SetPosition(float x, float y, float z, float yaw)
        {
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }
    }
}
