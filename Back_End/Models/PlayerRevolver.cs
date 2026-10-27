using System.Security.Cryptography;

namespace Back_End.Models
{
    public class PlayerRevolver
    {
        public const int MaxChambers = 6;

        public int BulletChamber { get; set; }
        public int CurrentChamber { get; set; }
        public int PullsCount { get; set; }

        public PlayerRevolver()
        {
            // Randomly place bullet in chamber 0..5
            BulletChamber = RandomNumberGenerator.GetInt32(MaxChambers);
            CurrentChamber = 0;
            PullsCount = 0;
        }

        public PlayerRevolver(int bulletChamber, int currentChamber = 0)
        {
            BulletChamber = bulletChamber;
            CurrentChamber = currentChamber;
            PullsCount = 0;
        }

        /// <summary>
        /// Pulls the trigger. Returns true if bullet hits (elimination), false if blank (survived).
        /// </summary>
        public bool PullTrigger()
        {
            ValidateState();

            PullsCount++;
            if (CurrentChamber == BulletChamber)
            {
                return true;
            }

            CurrentChamber = (CurrentChamber + 1) % MaxChambers;
            return false;
        }

        public void ValidateState()
        {
            if (BulletChamber < 0 || BulletChamber >= MaxChambers ||
                CurrentChamber < 0 || CurrentChamber >= MaxChambers ||
                PullsCount < 0)
            {
                throw new InvalidOperationException("Trạng thái ổ quay không hợp lệ.");
            }
        }
    }
}
