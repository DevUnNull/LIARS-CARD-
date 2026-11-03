using System.Security.Cryptography;

namespace Back_End.Models
{
    public class PlayerRevolver
    {
        public const int MaxChambers = 6;

        public int BulletChamber { get; set; }
        public int CurrentChamber { get; set; }
        public int PullsCount { get; set; }

        /// <summary>Số phát của lần bóp cò gần nhất, đếm từ 1 (hiển thị dạng X/6 trên đầu người bắn).</summary>
        public int LastShotNumber { get; private set; }

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
            PullsCount++;
            LastShotNumber = CurrentChamber + 1;
            if (CurrentChamber == BulletChamber)
            {
                return true;
            }

            CurrentChamber = (CurrentChamber + 1) % MaxChambers;
            return false;
        }
    }
}
